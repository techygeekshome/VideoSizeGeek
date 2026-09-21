using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace VideoSizeGeek.Core.Services;

/// <summary>
/// Fetches ffmpeg on demand, the same technique TranscribeGeek and SoundGeek already use for
/// exactly this problem: without ffmpeg VideoSizeGeek cannot encode anything at all, so telling
/// a new user to go and find ffmpeg themselves before the app can do its one job is not good
/// enough. This is that fix, applied here too.
///
/// Why it is downloaded rather than bundled:
///
/// 1. Licence. The build is GPL. ffmpeg is run as a separate process and never linked, which
/// keeps its terms off this application (see TwoPassEncoder). Shipping the
/// binary inside our installer would make us a redistributor of GPL software and drag in
/// obligations we have not signed up to. Fetching it from its own publisher, at the user's
/// request, does not.
/// 2. Size. It is over 100 MB, several times the installer. Most of that is codecs a given
/// user will never touch.
///
/// The archive is pinned to an exact version and checked against a known SHA-256 before anything
/// is extracted. A moving "latest" URL cannot be verified, so it is not used. Same archive, same
/// pinned version and hash TranscribeGeek and SoundGeek already ship, verified from the real
/// downloaded file, not invented.
/// </summary>
public static class FfmpegCatalog
{
  /// <summary>
  /// A specific published build, not a "latest" link. Pinned so the hash below means something.
  /// To move to a newer ffmpeg: change both of these together, never one alone.
  /// </summary>
  public const string DownloadUrl =
    "https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.1-essentials_build.zip";

  /// <summary>SHA-256 of the archive at DownloadUrl.</summary>
  public const string ExpectedSha256 =
    "fec81ae03971d9dd4be3ebe02e263bd2ec1d789483f931bdba5f5715e65da2e9";

  public const long ApproxBytes = 111_253_802;

  public const string Version = "9.0.1";

  /// <summary>Where the publisher's terms and source can be read. Shown next to the button.</summary>
  public const string PublisherUrl = "https://www.gyan.dev/ffmpeg/builds/";

  /// <summary>
  /// Same folder FfmpegTools already checks first, so a completed download is
  /// found with no other change needed anywhere else in the app.
  /// </summary>
  public static string ToolsDirectory => FfmpegTools.CacheDirectory;

  private static string ExeName(string stem) =>
    OperatingSystem.IsWindows() ? stem + ".exe" : stem;

  public static string FfmpegPath => Path.Combine(ToolsDirectory, ExeName("ffmpeg"));
  public static string FfprobePath => Path.Combine(ToolsDirectory, ExeName("ffprobe"));

  /// <summary>
  /// True when we have fetched a copy ourselves. A part-extracted file is worse than none, so
  /// anything suspiciously small counts as absent.
  /// </summary>
  public static bool IsDownloaded =>
    File.Exists(FfmpegPath) && new FileInfo(FfmpegPath).Length > 1_000_000;

  /// <summary>
  /// Downloads, verifies and extracts ffmpeg and ffprobe. Progress runs 0 to 1 across the
  /// download; the verify and extract at the end are quick by comparison.
  ///
  /// Everything happens in a .part file and a scratch folder, so a cancelled or corrupt
  /// download can never leave a half binary behind that looks installed.
  /// </summary>
  public static async Task DownloadAsync(
    HttpClient http,
    IProgress<double>? progress = null,
    CancellationToken ct = default)
  {
    Directory.CreateDirectory(ToolsDirectory);
    var archive = Path.Combine(ToolsDirectory, "ffmpeg.zip.part");
    var scratch = Path.Combine(ToolsDirectory, "unpack.tmp");

    TryDelete(archive);
    TryDeleteDirectory(scratch);

    try
    {
      using (var response = await http
             .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct)
             .ConfigureAwait(false))
      {
        response.EnsureSuccessStatusCode();
        var expected = response.Content.Headers.ContentLength ?? ApproxBytes;

        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var dest = File.Create(archive);

        var buffer = new byte[1 << 20];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
          await dest.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
          total += read;
          progress?.Report(Math.Min(1.0, (double)total / expected));
        }
      }

      var actual = await Sha256Async(archive, ct).ConfigureAwait(false);
      if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
      {
        throw new FfmpegDownloadException(
          "The downloaded file did not match the expected checksum, so it was discarded. " +
          "Nothing was installed. This usually means the download was interrupted or " +
          "something on the network altered it.");
      }

      Directory.CreateDirectory(scratch);
      ZipFile.ExtractToDirectory(archive, scratch);

      var ffmpeg = FindInside(scratch, ExeName("ffmpeg"))
        ?? throw new FfmpegDownloadException("The archive did not contain ffmpeg.");
      var ffprobe = FindInside(scratch, ExeName("ffprobe"));

      File.Copy(ffmpeg, FfmpegPath, overwrite: true);
      if (ffprobe is not null) File.Copy(ffprobe, FfprobePath, overwrite: true);

      progress?.Report(1.0);
    }
    finally
    {
      TryDelete(archive);
      TryDeleteDirectory(scratch);
    }
  }

  /// <summary>Removes our copy. Offered from Settings, always behind a confirm.</summary>
  public static void Delete()
  {
    TryDelete(FfmpegPath);
    TryDelete(FfprobePath);
  }

  private static string? FindInside(string root, string fileName) =>
    Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).FirstOrDefault();

  private static async Task<string> Sha256Async(string path, CancellationToken ct)
  {
    await using var fs = File.OpenRead(path);
    var hash = await SHA256.HashDataAsync(fs, ct).ConfigureAwait(false);
    return Convert.ToHexString(hash).ToLowerInvariant();
  }

  private static void TryDelete(string path)
  {
    try { if (File.Exists(path)) File.Delete(path); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }

  private static void TryDeleteDirectory(string path)
  {
    try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
  }
}

public sealed class FfmpegDownloadException : Exception
{
public FfmpegDownloadException(string message) : base(message) { }
}
