namespace VideoSizeGeek.Core.Models;

/// <summary>
/// A named size target. The numbers here are the actual limits of the place the file is
/// going, not round numbers picked for looks. Discord's 10 MB is the real free-tier cap;
/// Nitro users get 500 MB per file. Gmail rejects an attachment over 25 MB outright.
/// WhatsApp's video cap moved around for years and settled at 16 MB.
/// </summary>
public sealed record SizePreset(string Name, long TargetBytes, string Note)
{
    public static readonly SizePreset Discord = new("Discord (free)", 10L * 1024 * 1024,
        "The free-tier upload cap. Over this, Discord embeds a download link instead of playing it inline.");

    public static readonly SizePreset DiscordNitro = new("Discord Nitro", 500L * 1024 * 1024,
        "The Nitro upload cap.");

    public static readonly SizePreset Gmail = new("Gmail attachment", 25L * 1024 * 1024,
        "Gmail refuses anything larger as a direct attachment.");

    public static readonly SizePreset WhatsApp = new("WhatsApp", 16L * 1024 * 1024,
        "WhatsApp compresses or refuses a video over this on send.");

    public static readonly SizePreset Twitter = new("X / Twitter", 512L * 1024 * 1024,
        "The per-video cap for a standard account.");

    /// <summary>Smallest target first: this is a UI dropdown and that is the order people scan it in.</summary>
    public static readonly IReadOnlyList<SizePreset> All = new[]
    {
        Discord, WhatsApp, Gmail, DiscordNitro, Twitter
    };
}

/// <summary>What ffprobe found on the source file. Only what the budget calculator needs.</summary>
public sealed record ProbeResult(
    double DurationSeconds,
    bool HasVideoStream,
    bool HasAudioStream,
    int? Width,
    int? Height,
    double? FrameRate);

/// <summary>
/// The result of turning "this file, this many seconds long" and "fit inside this many bytes"
/// into real encoder settings. This is the part worth getting right: get the bitrate wrong and
/// the output either overshoots the target (the whole point of the app) or drops to a quality
/// nobody would accept without being told first.
/// </summary>
public sealed record EncodePlan(
    long TargetBytes,
    double DurationSeconds,
    int VideoKbps,
    int AudioKbps,
    bool AudioIncluded,
    bool BelowQualityFloor,
    string? FloorWarning,
    int ContainerOverheadPercent);

public enum EncodeStage
{
    Probing,
    FirstPass,
    SecondPass,
    Verifying,
    Done,
    Failed
}

public sealed record EncodeProgress(EncodeStage Stage, double FractionComplete, string Message);

public sealed record EncodeOutcome(
    bool Success,
    string? OutputPath,
    long? FinalBytes,
    long TargetBytes,
    bool WithinTarget,
    string Message);
