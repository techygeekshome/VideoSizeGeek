using VideoSizeGeek.Core.Models;

namespace VideoSizeGeek.Core.Services;

/// <summary>
/// Turns a target file size into encoder settings. No ffmpeg, no file I/O, nothing that needs
/// Windows: this is deliberately the one part of the app that is pure arithmetic, because it is
/// also the one part that actually has to be right. Get the video bitrate a few percent too high
/// and the whole point of the app fails: the file is still too big to attach.
/// </summary>
public static class BitrateBudget
{
    /// <summary>
    /// MP4 muxing, the moov atom and a handful of container-level bookkeeping bytes are not
    /// covered by the video/audio bitrate the encoder is told to hit, and x264's own bitrate
    /// control overshoots its target slightly by design (it is a target, not a ceiling, unless
    /// a hard vbv-maxrate is set). Reserving a slice of the budget up front is cheaper and more
    /// reliable than trying to predict the overshoot exactly.
    /// </summary>
    public const int DefaultOverheadPercent = 8;

    /// <summary>Audio bitrates to try, in order, before letting audio eat into video quality.</summary>
    private static readonly int[] AudioTiersKbps = { 128, 96, 64 };

    /// <summary>
    /// Below this, video stops looking like a size problem and starts looking like a shredded
    /// slideshow. Anything under 300 kbps at typical resolutions is macroblocking territory.
    /// This is a warning threshold, not a hard limit: the plan is still produced, just flagged.
    /// </summary>
    public const int MinAcceptableVideoKbps = 300;

    public static EncodePlan CreatePlan(
        long targetBytes,
        double durationSeconds,
        bool includeAudio,
        int overheadPercent = DefaultOverheadPercent)
    {
        if (targetBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBytes), "Target size must be greater than zero.");
        if (durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be greater than zero.");
        if (overheadPercent is < 0 or >= 100)
            throw new ArgumentOutOfRangeException(nameof(overheadPercent), "Overhead percent must be between 0 and 100.");

        var usableBits = targetBytes * 8.0 * (100 - overheadPercent) / 100.0;
        var totalKbps = usableBits / durationSeconds / 1000.0;

        if (!includeAudio)
        {
            var videoOnlyKbps = (int)Math.Floor(totalKbps);
            var belowFloorNoAudio = videoOnlyKbps < MinAcceptableVideoKbps;
            return new EncodePlan(
                targetBytes, durationSeconds,
                VideoKbps: Math.Max(videoOnlyKbps, 1),
                AudioKbps: 0,
                AudioIncluded: false,
                BelowQualityFloor: belowFloorNoAudio,
                FloorWarning: belowFloorNoAudio ? FloorMessage(videoOnlyKbps) : null,
                ContainerOverheadPercent: overheadPercent);
        }

        // Work down the audio tiers. Each one is tried against what is left for video; the
        // first tier that still leaves video above the floor wins. If none do, audio settles
        // on the lowest tier and video takes whatever is left, flagged as below the floor.
        foreach (var audioKbps in AudioTiersKbps)
        {
            var videoKbps = (int)Math.Floor(totalKbps - audioKbps);
            if (videoKbps >= MinAcceptableVideoKbps)
            {
                return new EncodePlan(
                    targetBytes, durationSeconds,
                    VideoKbps: videoKbps,
                    AudioKbps: audioKbps,
                    AudioIncluded: true,
                    BelowQualityFloor: false,
                    FloorWarning: null,
                    ContainerOverheadPercent: overheadPercent);
            }
        }

        var lowestAudio = AudioTiersKbps[^1];
        var remainingVideoKbps = Math.Max((int)Math.Floor(totalKbps - lowestAudio), 1);
        return new EncodePlan(
            targetBytes, durationSeconds,
            VideoKbps: remainingVideoKbps,
            AudioKbps: lowestAudio,
            AudioIncluded: true,
            BelowQualityFloor: true,
            FloorWarning: FloorMessage(remainingVideoKbps),
            ContainerOverheadPercent: overheadPercent);
    }

    /// <summary>What the plan's total encoded size would come to, overhead included, in bytes.</summary>
    public static long EstimatedOutputBytes(EncodePlan plan)
    {
        var encodedBits = (plan.VideoKbps + plan.AudioKbps) * 1000.0 * plan.DurationSeconds;
        var encodedBytes = encodedBits / 8.0;
        return (long)Math.Round(encodedBytes / (100 - plan.ContainerOverheadPercent) * 100);
    }

    private static string FloorMessage(int videoKbps) =>
        $"Fitting this into the target size leaves about {videoKbps} kbps for video, below the " +
        "300 kbps usually needed to avoid visible blocking. The clip will look rougher than the " +
        "source. Trim the clip, pick a bigger target, or accept the trade.";
}
