# Video Piper 🎵

A lightweight Windows desktop application for downloading YouTube videos as MP3 audio files. Built with **C#**, **.NET 10**, and **Uno Platform (WinUI 3)** using declarative **C# Markup**.

<p align="center">
  <img src="Video%20Piper%20Icon.png" alt="Video Piper Logo" width="128" />
</p>

---

## ✨ Features

- ⚡ **Direct YouTube to MP3 download** — Spawns `yt-dlp` subprocesses with real-time progress streaming
- 📁 **Native folder picker** — Uses `Windows.Storage.Pickers` for native Windows folder selection dialogs
- 🎨 **Declarative C# Markup** — Fluent WinUI 3 UI built with Uno Platform C# Markup DSL
- 🌓 **Dark & Light theme** — Built-in theme switching with persistent user preferences
- 🛠️ **In-App Tool Installer** — One-click downloader and extractor for missing dependencies (`yt-dlp.exe`, `ffmpeg.exe`)
- 🇸🇪 **Swedish Localization** — All user-facing strings localized in Swedish

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10+ (version 2004+)

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

---

## 📖 Documentation & Architecture

For detailed architectural guidelines, development workflows, and conventions, see:
- [**AGENTS.md**](./AGENTS.md) — Comprehensive developer & agent guide
- [**video-piper/README.md**](./video-piper/README.md) — Application project documentation

---

## 📄 License

MIT
