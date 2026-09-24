# Video Piper 🎵

A lightweight cross-platform desktop application for downloading YouTube videos and audio as **MP3** or **MP4**. Built with **C#**, **.NET 10**, and **Uno Platform (WinUI 3)** using declarative **C# Markup**. Targets Windows natively (WinUI 3 / WinAppSDK) plus a Skia desktop build for Linux and macOS.

<p align="center">
  <img src="Video%20Piper%20Icon.png" alt="Video Piper Logo" width="128" />
</p>

---

## ✨ Features

- ⚡ **Direct YouTube download** — Spawns `yt-dlp` subprocesses with real-time progress streaming
- 🎚️ **MP3 or MP4 output** — Pick audio (MP3) or video (MP4, up to 1080p); the choice is remembered
- 📚 **Library mode** — A persistent library organized as `Channel / Playlist`, with per-item progress and local playback
- 🔎 **YouTube search** — Debounced in-app search; download results straight into the library
- 📁 **Native folder picker** — Uses `Windows.Storage.Pickers` for native Windows folder selection dialogs
- 🎨 **Declarative C# Markup** — Fluent WinUI 3 UI built with Uno Platform C# Markup DSL
- 🌓 **Dark & Light theme** — Built-in theme switching with persistent user preferences
- 🛠️ **In-App Tool Installer** — One-click downloader and extractor for missing dependencies (`yt-dlp.exe`, `ffmpeg.exe`)
- 🇸🇪 **Swedish Localization** — All user-facing strings localized in Swedish

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10+ (version 2004+) for the native target, **or** Linux/macOS with GTK 3 dev packages for the Skia desktop target
- `yt-dlp` & `ffmpeg` on PATH (or use the in-app installer)

### Build & Run
```bash
cd video-piper
dotnet build VideoPiper.sln
dotnet run --project VideoPiper/VideoPiper.csproj
```

### Publish Standalone Executable
```bash
cd video-piper
dotnet publish VideoPiper/VideoPiper.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

### Install & Package (Python / Inno Setup)
You can package and install Video Piper using the zero-dependency Python tools:

```bash
# Package portable ZIP and setup executable
python installer/build_installer.py

# Install to %LOCALAPPDATA%\Programs\Video Piper with Start menu & desktop shortcuts
python installer/install.py

# Uninstall cleanly
python installer/install.py --uninstall
```

Alternatively, use the `vpp` CLI (`tools/vpp`):
```bash
vpp publish --self-contained
vpp installer
```

---

## 📖 Documentation & Architecture

For detailed architectural guidelines, development workflows, and conventions, see:
- [**AGENTS.md**](./AGENTS.md) — Comprehensive developer & agent guide
- [**video-piper/README.md**](./video-piper/README.md) — Application project documentation

---

## 📄 License

MIT
