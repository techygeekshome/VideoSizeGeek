using System.Diagnostics;

namespace VideoSizeGeek.Core.Services;

/// <summary>
/// Finds ffmpeg and ffprobe. VideoSizeGeek does not bundle them and does not silently fetch them
/// from a URL baked into the app either: a build that downloads and runs an executable from a
/// hardcoded link the day it ships is exactly the kind of thing that goes stale or gets
/// hijacked years later with nobody noticing. Instead it looks in its own cache folder, then
/// on PATH, and if neither has a working copy it says so and points at the official builds
/// page so the user (or a later, deliberate update to this class) can be the one who chooses
/// where the binary comes from.
/// </summary>
public static class FfmpegTools
{
    public const string OfficialBuildsUrl = "https://www.gyan.dev/ffmpeg/builds/";

    public static string CacheDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TechyGeeksHome", "VideoSizeGeek", "bin");

    private static string ExeName(string baseName) =>
        OperatingSystem.IsWindows() ? baseName + ".exe" : baseName;

    public static string? FindFfmpeg() => Find(ExeName("ffmpeg"));
    public static string? FindFfprobe() => Find(ExeName("ffprobe"));

    private static string? Find(string exeName)
    {
        var cached = Path.Combine(CacheDirectory, exeName);
        if (File.Exists(cached) && Runs(cached))
            return cached;

        var onPath = FindOnPath(exeName);
        if (onPath is not null && Runs(onPath))
            return onPath;

        return null;
    }

    private static string? FindOnPath(string exeName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>A binary that exists is not proof it works. Actually run it.</summary>
    private static bool Runs(string exePath)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exePath, "-version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p is null) return false;
            if (!p.WaitForExit(5000)) { p.Kill(entireProcessTree: true); return false; }
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Registers a pair of binaries the user has pointed at manually (the "I already have
    /// ffmpeg here" path), by copying them into the cache folder so every later run finds them
    /// the same way it would a downloaded copy.
    /// </summary>
    public static void AdoptManualCopies(string ffmpegPath, string ffprobePath)
    {
        if (!Runs(ffmpegPath)) throw new InvalidOperationException("That ffmpeg does not run.");
        if (!Runs(ffprobePath)) throw new InvalidOperationException("That ffprobe does not run.");

        Directory.CreateDirectory(CacheDirectory);
        File.Copy(ffmpegPath, Path.Combine(CacheDirectory, ExeName("ffmpeg")), overwrite: true);
        File.Copy(ffprobePath, Path.Combine(CacheDirectory, ExeName("ffprobe")), overwrite: true);
    }
}
