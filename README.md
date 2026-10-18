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
- 🛠️ **App-managed tools** — The app installs and manages its **own** `yt-dlp` and `ffmpeg` binaries (system binaries are never used): missing tools install automatically, a **daily background update check** keeps them current, and a **settings page** (gear in the app bar) shows installed/latest per tool with manual check/update buttons
- 🇸🇪 **Swedish Localization** — All user-facing strings localized in Swedish

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows 10+ (version 2004+) for the native target, **or** Linux/macOS with GTK 3 dev packages for the Skia desktop target
- `yt-dlp` & `ffmpeg` — not required on the system; the app installs and updates its own copies automatically
- [`uv`](https://docs.astral.sh/uv/) — for building installers (optional but recommended; a plain Python 3.9+ works too)

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

### Install & Package (Python + uv / Inno Setup)
Package with the stdlib-only Python builder (run via `uv` — no environment setup):

```bash
# Portable ZIP + Windows Setup installer (publishes first if the bundle is stale)
uv run --no-project installer/build_installer.py

# Flags: --publish (force republish) · --zip-only · --setup-only · -o <dir>
```

Or via the `vpp` CLI (`tools/vpp`):

```bash
vpp installer
```

Install a portable build (Start menu / desktop shortcuts, Add/Remove registration):

```bash
python installer/install.py            # from the extracted ZIP
python installer/install.py --uninstall
```

Prereq for the Setup `.exe`: Inno Setup 6 (`winget install JRSoftware.InnoSetup`) —
the portable ZIP builds without it. The installer icon is regenerated from
`app-icon.png` on demand via `uv run --with Pillow installer/convert_icon.py`.

---

## 📖 Documentation & Architecture

For detailed architectural guidelines, development workflows, and conventions, see:
- [**AGENTS.md**](./AGENTS.md) — Comprehensive developer & agent guide
- [**video-piper/README.md**](./video-piper/README.md) — Application project documentation

---

## 📄 License

MIT
