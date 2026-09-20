using System.Diagnostics;
using System.Globalization;
using VideoSizeGeek.Core.Models;

namespace VideoSizeGeek.Core.Services;

/// <summary>
/// Runs the actual two-pass x264 encode and checks the result against the target size. This
/// is the one class in the range that invokes ffmpeg, and it invokes it the same way the rest
/// of the range invokes anything GPL: as a separate process it starts and waits on, never as a
/// linked library. Nothing here modifies the source file; every run writes to a new path.
/// </summary>
public sealed class TwoPassEncoder(string ffmpegPath)
{
    /// <summary>
    /// x264's bitrate control is a target, not a hard ceiling, so a real encode can land a few
    /// percent over the plan even with a sane -bufsize. Anything inside this tolerance is
    /// treated as a pass. Only a genuine overshoot triggers the one-shot retry below.
    /// </summary>
    public const double OvershootToleranceFraction = 0.04;

    public async Task<EncodeOutcome> EncodeAsync(
        string inputPath,
        string outputPath,
        EncodePlan plan,
        IProgress<EncodeProgress>? progress = null,
        CancellationToken ct = default)
    {
        var attempt = await RunOnePassPairAsync(inputPath, outputPath, plan, progress, ct);

        if (attempt.Success && attempt.FinalBytes is { } finalBytes)
        {
            var overshoot = (double)(finalBytes - plan.TargetBytes) / plan.TargetBytes;
            if (overshoot > OvershootToleranceFraction)
            {
                // One retry, with the video bitrate trimmed by however far over we landed.
                // No loop: a second miss means something about this clip does not fit the
                // model (very short, very high motion) and the honest answer is to say so,
                // not to keep guessing.
                var correctionFactor = 1.0 - overshoot;
                var retryPlan = plan with { VideoKbps = Math.Max(1, (int)(plan.VideoKbps * correctionFactor)) };
                progress?.Report(new EncodeProgress(EncodeStage.FirstPass, 0,
                    $"First attempt landed {overshoot:P0} over target. Retrying at a lower bitrate."));
                attempt = await RunOnePassPairAsync(inputPath, outputPath, retryPlan, progress, ct);
            }
        }

        return attempt;
    }

    private async Task<EncodeOutcome> RunOnePassPairAsync(
        string inputPath, string outputPath, EncodePlan plan,
        IProgress<EncodeProgress>? progress, CancellationToken ct)
    {
        var workDir = Path.Combine(Path.GetTempPath(), "VideoSizeGeek", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDir);
        var passLogPrefix = Path.Combine(workDir, "pass");
        var nullSink = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

        try
        {
            var probe = await MediaProbe.ProbeAsync(FfmpegTools.FindFfprobe() ?? ffmpegPath, inputPath, ct);

            progress?.Report(new EncodeProgress(EncodeStage.FirstPass, 0, "Analysing motion (pass 1 of 2)"));
            var pass1Args =
                $"-y -i \"{inputPath}\" -c:v libx264 -b:v {plan.VideoKbps}k " +
                $"-preset medium -pass 1 -passlogfile \"{passLogPrefix}\" -an -f mp4 \"{nullSink}\"";
            await RunFfmpegAsync(pass1Args, probe.DurationSeconds,
                p => progress?.Report(new EncodeProgress(EncodeStage.FirstPass, p * 0.45, "Analysing motion (pass 1 of 2)")),
                ct);

            progress?.Report(new EncodeProgress(EncodeStage.SecondPass, 0.45, "Encoding (pass 2 of 2)"));
            var audioArgs = plan.AudioIncluded ? $"-c:a aac -b:a {plan.AudioKbps}k" : "-an";
            var pass2Args =
                $"-y -i \"{inputPath}\" -c:v libx264 -b:v {plan.VideoKbps}k " +
                $"-preset medium -pass 2 -passlogfile \"{passLogPrefix}\" {audioArgs} \"{outputPath}\"";
            await RunFfmpegAsync(pass2Args, probe.DurationSeconds,
                p => progress?.Report(new EncodeProgress(EncodeStage.SecondPass, 0.45 + p * 0.50, "Encoding (pass 2 of 2)")),
                ct);

            progress?.Report(new EncodeProgress(EncodeStage.Verifying, 0.97, "Checking the result"));
            var finalBytes = new FileInfo(outputPath).Length;
            var withinTarget = finalBytes <= plan.TargetBytes * (1 + OvershootToleranceFraction);

            progress?.Report(new EncodeProgress(EncodeStage.Done, 1.0, "Done"));
            return new EncodeOutcome(
                Success: true,
                OutputPath: outputPath,
                FinalBytes: finalBytes,
                TargetBytes: plan.TargetBytes,
                WithinTarget: withinTarget,
                Message: withinTarget
                    ? $"{FormatBytes(finalBytes)}, at or under the {FormatBytes(plan.TargetBytes)} target."
                    : $"{FormatBytes(finalBytes)}, over the {FormatBytes(plan.TargetBytes)} target even after a retry.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report(new EncodeProgress(EncodeStage.Failed, 0, ex.Message));
            return new EncodeOutcome(false, null, null, plan.TargetBytes, false, ex.Message);
        }
        finally
        {
            try { Directory.Delete(workDir, recursive: true); } catch { /* best effort cleanup */ }
        }
    }

    private async Task RunFfmpegAsync(string arguments, double totalDurationSeconds,
        Action<double> onFractionComplete, CancellationToken ct)
    {
        var fullArgs = arguments.Replace("-y -i", "-y -progress pipe:1 -nostats -i");
        var psi = new ProcessStartInfo(ffmpegPath, fullArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start ffmpeg.");

        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(ct)) is not null)
        {
            if (line.StartsWith("out_time_ms=", StringComparison.Ordinal) &&
                long.TryParse(line.AsSpan("out_time_ms=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var outTimeMicros))
            {
                var seconds = outTimeMicros / 1_000_000.0;
                var fraction = totalDurationSeconds > 0 ? Math.Clamp(seconds / totalDurationSeconds, 0, 1) : 0;
                onFractionComplete(fraction);
            }
        }

        await process.WaitForExitAsync(ct);
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {LastLines(stderr, 6)}");
    }

    private static string LastLines(string text, int count) =>
        string.Join('\n', text.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(count));

    private static string FormatBytes(long bytes) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:0.0} MB",
        >= 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B"
    };
}
