using VideoSizeGeek.Core.Models;
using VideoSizeGeek.Core.Services;

// A plain console program rather than a test framework, so `dotnet run` proves the build on any
// machine with nothing installed. Same shape as the checks in CutGeek and DriverGeek.

var failures = 0;

void Check(string name, bool ok, string? detail = null)
{
    Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (ok || detail is null ? "" : $"  ({detail})"));
    if (!ok) failures++;
}

void Skip(string name, string why) => Console.WriteLine($"SKIP  {name} ({why})");

// ---------------------------------------------------------------- presets

Check("five presets offered", SizePreset.All.Count == 5);
Check("presets are ordered smallest first",
    SizePreset.All.Zip(SizePreset.All.Skip(1)).All(pair => pair.First.TargetBytes <= pair.Second.TargetBytes));
Check("discord matches the real 10 MB cap", SizePreset.Discord.TargetBytes == 10L * 1024 * 1024);
Check("gmail matches the real 25 MB cap", SizePreset.Gmail.TargetBytes == 25L * 1024 * 1024);

// ---------------------------------------------------------------- BitrateBudget: normal case

{
    // A 3 minute clip into a 10 MB target: comfortably above the quality floor.
    var plan = BitrateBudget.CreatePlan(targetBytes: 10L * 1024 * 1024, durationSeconds: 180, includeAudio: true);
    Check("normal case is not below the quality floor", !plan.BelowQualityFloor);
    Check("normal case keeps 128 kbps audio", plan.AudioKbps == 128, plan.AudioKbps.ToString());
    Check("normal case has a positive video bitrate", plan.VideoKbps > 0, plan.VideoKbps.ToString());

    var estimate = BitrateBudget.EstimatedOutputBytes(plan);
    Check("the estimate does not exceed the target",
        estimate <= plan.TargetBytes, $"estimate={estimate} target={plan.TargetBytes}");
}

// ---------------------------------------------------------------- BitrateBudget: the floor actually bites

{
    // A 20 minute clip into a 10 MB target. Nobody is fitting that at a watchable bitrate, and
    // the calculator needs to say so rather than silently return something unusable.
    var plan = BitrateBudget.CreatePlan(targetBytes: 10L * 1024 * 1024, durationSeconds: 1200, includeAudio: true);
    Check("long clip, small target trips the floor", plan.BelowQualityFloor);
    Check("floor warning is not empty", !string.IsNullOrWhiteSpace(plan.FloorWarning));
    Check("audio still dropped to the lowest tier first", plan.AudioKbps == 64, plan.AudioKbps.ToString());
}

// ---------------------------------------------------------------- BitrateBudget: no audio stream

{
    var withAudio = BitrateBudget.CreatePlan(10L * 1024 * 1024, 180, includeAudio: true);
    var withoutAudio = BitrateBudget.CreatePlan(10L * 1024 * 1024, 180, includeAudio: false);
    Check("dropping audio frees up video bitrate", withoutAudio.VideoKbps > withAudio.VideoKbps,
        $"with={withAudio.VideoKbps} without={withoutAudio.VideoKbps}");
    Check("no-audio plan carries no audio bitrate", withoutAudio.AudioKbps == 0);
    Check("no-audio plan is not marked as including audio", !withoutAudio.AudioIncluded);
}

// ---------------------------------------------------------------- BitrateBudget: bad input is rejected, not guessed at

Check("zero duration throws rather than dividing by zero",
    Throws(() => BitrateBudget.CreatePlan(1000, 0, true)));
Check("negative target throws",
    Throws(() => BitrateBudget.CreatePlan(-1, 60, true)));
Check("overhead percent of 100 throws (would divide by zero)",
    Throws(() => BitrateBudget.CreatePlan(1000, 60, true, overheadPercent: 100)));

// ---------------------------------------------------------------- MediaProbe.Parse

{
    const string sampleJson = """
    {
      "streams": [
        {"codec_type": "video", "width": 1920, "height": 1080, "r_frame_rate": "30000/1001", "duration": "12.500000"},
        {"codec_type": "audio", "duration": "12.500000"}
      ],
      "format": {"duration": "12.500000"}
    }
    """;
    var probe = MediaProbe.Parse(sampleJson);
    Check("duration parsed", Math.Abs(probe.DurationSeconds - 12.5) < 0.001, probe.DurationSeconds.ToString());
    Check("video stream detected", probe.HasVideoStream);
    Check("audio stream detected", probe.HasAudioStream);
    Check("resolution parsed", probe.Width == 1920 && probe.Height == 1080);
    Check("NTSC frame rate parsed to about 29.97", probe.FrameRate is not null && Math.Abs(probe.FrameRate.Value - 29.97) < 0.01,
        probe.FrameRate?.ToString() ?? "null");
}

Check("probing empty json throws instead of returning a zero-length plan",
    Throws(() => MediaProbe.Parse("{}")));

// ---------------------------------------------------------------- end to end: a real ffmpeg encode

// This only runs where ffmpeg is actually on PATH. The build agent for `build.yml` is Linux and
// does not carry ffmpeg, so this step honestly skips there; it is not what proves the release
// build works (that is release.yml, on a real Windows runner). What it does prove, on any
// machine that does have ffmpeg including this one, is that the whole pipeline (probe, plan,
// two-pass encode, size check) genuinely produces a file at or under the target, not just that
// the arithmetic looks right on paper.
var ffmpeg = FfmpegTools.FindFfmpeg();
var ffprobe = FfmpegTools.FindFfprobe();

if (ffmpeg is null || ffprobe is null)
{
    Skip("end to end encode against a real target size", "ffmpeg/ffprobe not found on this machine");
}
else
{
    await RunEndToEndCheck(ffmpeg, ffprobe);
}

Console.WriteLine();
Console.WriteLine(failures == 0 ? $"All checks passed." : $"{failures} check(s) FAILED.");
return failures == 0 ? 0 : 1;

bool Throws(Action action)
{
    try { action(); return false; }
    catch { return true; }
}

async Task RunEndToEndCheck(string ffmpegPath, string ffprobePath)
{
    var temp = Path.Combine(Path.GetTempPath(), "sizegeek-tests-" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(temp);
    var source = Path.Combine(temp, "source.mp4");
    var output = Path.Combine(temp, "output.mp4");

    try
    {
        // Ten seconds of a synthetic test pattern with a tone, generated by ffmpeg itself, so
        // this check needs no binary fixture checked into the repo.
        var generate = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ffmpegPath,
            $"-y -f lavfi -i testsrc=duration=10:size=640x360:rate=25 " +
            $"-f lavfi -i sine=frequency=440:duration=10 " +
            $"-c:v libx264 -c:a aac -shortest \"{source}\"")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        })!;
        await generate.WaitForExitAsync();
        Check("test source clip generated", File.Exists(source) && new FileInfo(source).Length > 0);

        var probe = await MediaProbe.ProbeAsync(ffprobePath, source);
        Check("real probe reports about 10 seconds", Math.Abs(probe.DurationSeconds - 10) < 0.5, probe.DurationSeconds.ToString());

        const long target = 512 * 1024; // 512 KB - small enough to actually constrain a 10s clip
        var plan = BitrateBudget.CreatePlan(target, probe.DurationSeconds, probe.HasAudioStream);

        var encoder = new TwoPassEncoder(ffmpegPath);
        var progressLog = new List<string>();
        var result = await encoder.EncodeAsync(source, output, plan,
            new Progress<EncodeProgress>(p => progressLog.Add($"{p.Stage} {p.FractionComplete:P0}")));

        Check("encode reported success", result.Success, result.Message);
        Check("output file exists", File.Exists(output));
        Check("output landed at or under the target size (within tolerance)",
            result.WithinTarget, result.Message);
        Check("progress was reported at least once", progressLog.Count > 0);

        if (File.Exists(output))
        {
            var actualBytes = new FileInfo(output).Length;
            Console.WriteLine($"      target={target}B actual={actualBytes}B plan=({plan.VideoKbps}kbps video, {plan.AudioKbps}kbps audio)");
        }
    }
    finally
    {
        try { Directory.Delete(temp, recursive: true); } catch { /* best effort */ }
    }
}
