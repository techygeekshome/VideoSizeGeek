<div align="center">

<img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/icons/videosizegeek-256.png" alt="VideoSizeGeek logo" width="96" height="96">

# VideoSizeGeek

**Fits a video into an exact size limit for Discord, Gmail or WhatsApp, on your own machine.**

[![Version](https://img.shields.io/github/v/release/techygeekshome/VideoSizeGeek?label=version&color=4c9bff)](https://github.com/techygeekshome/VideoSizeGeek/releases)
[![Platform](https://img.shields.io/badge/platform-Windows-0078d4)](#%EF%B8%8F-download--run)
[![License](https://img.shields.io/badge/license-GPL--3.0-3fb950)](LICENSE)
[![Made by TechyGeeksHome](https://img.shields.io/badge/made%20by-TechyGeeksHome-b191f2)](https://techygeekshome.info)
[![Support on Ko-fi](https://img.shields.io/badge/support-Ko--fi-ff5e5b)](https://ko-fi.com/techygeekshome)

[Download](#%EF%B8%8F-download--run) · [Features](#-what-it-does) · [Requirements](#-what-your-pc-needs) · [Build from source](#-build-from-source) · [License](#-license)

</div>

---

<!--VIDEO-->
Type 10 MB, get a video that fits Discord's free upload cap. Type 25 MB for Gmail, 16 for WhatsApp, or a number of your own. VideoSizeGeek works out the bitrate that actually lands there, encodes it in two passes so the size is hit rather than guessed at, and tells you before it starts if the target is too small for the clip to still look right.

Nothing is uploaded. No account, no watermark, no file size limit of its own.

## 📸 Screenshots

<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/screenshots/screenshot-start.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/screenshots/screenshot-discord.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/screenshots/screenshot-too-small.png" width="49%" />
  <img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/screenshots/screenshot-encoding.png" width="49%" />
</p>
<p float="left">
  <img src="https://raw.githubusercontent.com/techygeekshome/VideoSizeGeek/main/screenshots/screenshot-done.png" width="49%" />
</p>

## ⬇️ Download & run

| What it is | Get it |
| --- | --- |
| **Installer** *(recommended)*: Start menu entry, proper uninstall, no admin rights needed | [**VideoSizeGeekSetup.exe**](https://github.com/techygeekshome/VideoSizeGeek/releases/latest) |
| **Portable**: one `.exe`, nothing to install | [**VideoSizeGeek-portable.exe**](https://github.com/techygeekshome/VideoSizeGeek/releases/latest) |

Both files are checked against `SHA256SUMS.txt` on every release. You can also read about it on the [product page](https://techygeekshome.info/videosizegeek/).

> **Windows will warn you the first time.** VideoSizeGeek isn't code signed yet, so SmartScreen shows a warning on first run. Click **More info → Run anyway**. [Why that happens, and how to check the download is genuine.](https://techygeekshome.info/why-windows-warns-about-this-download/)

## ✨ What it does

- 🎯 **Named presets**: Discord's 10 MB free cap, Discord Nitro's 500 MB, Gmail's 25 MB attachment limit, WhatsApp's 16 MB, X's 512 MB, or type your own target in MB.
- 🔁 **Two-pass encoding**, so the output lands at or under the target instead of somewhere near it. If the first pass overshoots, it corrects once and re-encodes rather than shipping a file that is still too big.
- ⚠️ **Says so before it starts** if the target is too small for the clip to hold a watchable bitrate, rather than quietly handing back a blocky mess.
- 🔇 **Keep or drop the audio**, and the bitrate is worked out again either way.
- 🛡️ **Never touches the original.** The output is always a new file next to the source, named with the target size.
- 🔒 **Private.** Everything happens on your PC. Nothing is uploaded.

## 💻 What your PC needs

- Windows 10 or 11, 64-bit.
- **ffmpeg**, fetched with one click the first time you need it. VideoSizeGeek does not bundle it (it would add well over 100 MB to every download), but the first time it can't find a copy it offers a one-click download of the same pinned, hash-verified ffmpeg build TranscribeGeek and SoundGeek use, from [gyan.dev's builds](https://www.gyan.dev/ffmpeg/builds/), checked against a known checksum before anything runs. Already have ffmpeg on your PATH? It's picked up automatically and nothing is downloaded.

## 🤔 Why this exists

HandBrake has turned this feature down twice, on the record ([issue #2483](https://github.com/HandBrake/HandBrake/issues/2483), [issue #4622](https://github.com/HandBrake/HandBrake/issues/4622)), both closed as out of scope. Every tool that does it now is a web upload competing for "shrink video to 10MB" in search results, which means sending the clip to a stranger's server to find out if it fits. VideoSizeGeek does the same arithmetic ffmpeg has always been capable of, locally, with the honesty about quality that a web tool has no reason to volunteer.

## 🔧 Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). The whole solution is plain `net8.0` with Avalonia, so it builds on Windows, Linux or macOS.

```powershell
dotnet build VideoSizeGeek.sln -c Release
dotnet run --project tests/VideoSizeGeek.Tests -c Release
```

The tests cover the bitrate maths, the ffprobe parsing and a real end-to-end encode (skipped when ffmpeg is not on PATH). Releases are built by GitHub Actions from a tag, so the binary you download provably comes from the source here.

## ☕ Support

VideoSizeGeek is free and always will be. If it saved you an afternoon, you can [buy us a coffee on Ko-fi](https://ko-fi.com/techygeekshome). Welcome, but never expected.

## 🐛 Support & contributing

Found a bug or have a request? [Open an issue](https://github.com/techygeekshome/VideoSizeGeek/issues) or [get in touch](https://techygeekshome.info/contact/). Pull requests are welcome.

## 📄 License

VideoSizeGeek is free software, licensed under the [GNU General Public License v3.0](LICENSE). The VideoSizeGeek name, logo and TechyGeeksHome branding are not covered by that licence and remain ours.

© 2026 TechyGeeksHome. The rest of the range is at [techygeekshome.info/geek-tools](https://techygeekshome.info/geek-tools/).

---

<div align="center">

Made with ❤️ by [**TechyGeeksHome**](https://techygeekshome.info)

[Website](https://techygeekshome.info) · [YouTube](https://www.youtube.com/channel/UCtEuFj1SMLiuRoucD1hv8dA) · [X](https://x.com/TechyGeeks1) · [Instagram](https://www.instagram.com/andrewarmstrongtgh/)

</div>
