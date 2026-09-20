using TechyGeeksHome.Common;

namespace VideoSizeGeek;

/// <summary>
/// Everything the shared About window and update check need to know about this app. One place,
/// so the wording here and the wording on the product page can be kept in step.
/// </summary>
internal static class AppFacts
{
    public static readonly AppInfo Info = new()
    {
        Name = "VideoSizeGeek",
        Tagline = "Fits a video into a size limit, exactly",
        Description =
            "Pick a video and a target, Discord's 10 MB, Gmail's 25 MB, WhatsApp's 16 MB or a " +
            "size of your own, and VideoSizeGeek works out the bitrate that fits it and re-encodes a " +
            "copy. Two passes, so the size lands where it is told rather than roughly near it. " +
            "If the target is too small for the clip to look right, it says so before it starts, " +
            "not after. Runs on your own machine, on ffmpeg, which you install once yourself.",
        GitHubOwner = "techygeekshome",
        GitHubRepo = "VideoSizeGeek",
        ProductUrl = "https://techygeekshome.info/videosizegeek/",
        IconUri = "avares://VideoSizeGeek/Assets/videosizegeek.png",
        LicenceLine = "Free to use, including at work. GPL-3.0. No paid tier, ever.",
        Credits = new[]
        {
            new Credit("ffmpeg", "GPL-2.0-or-later (as built for the -gpl variant)", "https://ffmpeg.org"),
            new Credit("Avalonia", "MIT", "https://avaloniaui.net")
        }
    };
}
