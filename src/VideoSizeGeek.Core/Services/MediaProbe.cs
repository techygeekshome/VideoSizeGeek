using System.Diagnostics;
using System.Text.Json;
using VideoSizeGeek.Core.Models;

namespace VideoSizeGeek.Core.Services;

public sealed class MediaProbeException(string message) : Exception(message);

/// <summary>Wraps ffprobe. Reads, never writes: this class cannot touch the source file.</summary>
public static class MediaProbe
{
    public static async Task<ProbeResult> ProbeAsync(string ffprobePath, string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            throw new MediaProbeException($"File not found: {filePath}");

        var args = $"-v quiet -print_format json -show_format -show_streams \"{filePath}\"";
        var psi = new ProcessStartInfo(ffprobePath, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new MediaProbeException("Could not start ffprobe.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
            throw new MediaProbeException($"ffprobe exited with code {process.ExitCode}: {stderr.Trim()}");

        return Parse(stdout);
    }

    /// <summary>Separated from the process call so the JSON handling can be unit tested directly.</summary>
    public static ProbeResult Parse(string ffprobeJson)
    {
        using var doc = JsonDocument.Parse(ffprobeJson);
        var root = doc.RootElement;

        double duration = 0;
        if (root.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durationEl) &&
            double.TryParse(durationEl.GetString(), out var parsedDuration))
        {
            duration = parsedDuration;
        }

        bool hasVideo = false, hasAudio = false;
        int? width = null, height = null;
        double? frameRate = null;

        if (root.TryGetProperty("streams", out var streams))
        {
            foreach (var stream in streams.EnumerateArray())
            {
                var type = stream.TryGetProperty("codec_type", out var t) ? t.GetString() : null;
                if (type == "video")
                {
                    hasVideo = true;
                    if (stream.TryGetProperty("width", out var w)) width = w.GetInt32();
                    if (stream.TryGetProperty("height", out var h)) height = h.GetInt32();
                    if (stream.TryGetProperty("r_frame_rate", out var fr))
                        frameRate = ParseFrameRate(fr.GetString());
                }
                else if (type == "audio")
                {
                    hasAudio = true;
                }

                // A stream can carry its own duration when the container-level one is missing
                // or zero, which happens with some odd MOV files. Fall back to the longest one.
                if (duration <= 0 && stream.TryGetProperty("duration", out var streamDur) &&
                    double.TryParse(streamDur.GetString(), out var sd) && sd > duration)
                {
                    duration = sd;
                }
            }
        }

        if (duration <= 0)
            throw new MediaProbeException("Could not determine the clip's duration.");

        return new ProbeResult(duration, hasVideo, hasAudio, width, height, frameRate);
    }

    private static double? ParseFrameRate(string? rational)
    {
        if (string.IsNullOrWhiteSpace(rational)) return null;
        var parts = rational.Split('/');
        if (parts.Length != 2) return null;
        if (!double.TryParse(parts[0], out var num) || !double.TryParse(parts[1], out var den) || den == 0)
            return null;
        return num / den;
    }
}
