# AGENTS.md - Video-Piper Developer & Agent Guide

Welcome to the **Video-Piper** repository. This document provides essential project context, architectural guidelines, conventions, and operational workflows for AI agents and human contributors working on this codebase.

---

## 1. Project Overview

**Video-Piper** is a cross-platform desktop application for downloading YouTube videos and audio. Built with **C#**, **.NET 10**, and **Uno Platform**. The primary target is Windows (native WinUI 3 / WinAppSDK), with an additional Skia-rendered desktop target that builds and runs on Linux and macOS.

It has two modes, exposed as tabs in the main window:
- **Nedladdning** (Simple): one-off downloads of a single link to a chosen folder, as MP3 or MP4.
- **Bibliotek** (Library): a managed, persistent library with channel/playlist subfolders, debounced YouTube search, per-item progress, and local playback.

### Key Capabilities
- **Direct YouTube download**: Spawns `yt-dlp` subprocesses directly through `System.Diagnostics.Process` with real-time progress parsing from stdout/stderr. Audio → MP3; video → MP4 (bestvideo+bestaudio merged, capped at 1080p).
- **MP3/MP4 format choice**: A toggle selects the output format in both modes; the choice is persisted.
- **Managed library**: Persistent index (`<root>/.videopiper/library.json`) tracking items across launches. Files are organized as `<root>/<Channel>/<Playlist>/`, resolved by `LibraryStore.ResolveTargetFolder`. Interrupted downloads are marked failed on next load.
- **YouTube search**: Debounced (400 ms) search via yt-dlp's `youtubesearch` extractor; results stream back without blocking the UI and can be downloaded directly into the library.
- **Playlist support**: A playlist URL pre-fetches all entries (`--flat-playlist`) and downloads them sequentially, each with its own progress/status.
- **Folder picker**: Native `Windows.Storage.Pickers.FolderPicker` on Windows; on non-Windows targets the UI falls back to manual path entry / default Music folder.
- **In-app tool installer**: Downloads and installs missing dependencies (yt-dlp.exe, ffmpeg.exe) into the app's local data folder.
- **Dark/Light theme toggle**: Built-in theme switching with persisted preference.
- **Swedish localization**: All user-facing strings in Swedish (e.g., *"YouTube Länk"*, *"Spara till"*, *"Ladda ner MP3"*).

---

## 2. Repository Structure

The application source code is in the `video-piper/` subfolder:

```text
Video-Piper/
├── AGENTS.md                  # This guide
├── Video Piper Icon.png       # Branding & Icon asset
├── app-icon.png               # App icon
├── app-splashscreen.png       # Splash screen asset
├── tools/
│   └── vpp/                   # `vpp` CLI — Python project manager (build/run/publish)
│       ├── pyproject.toml     # Packaging + `vpp` console-script entry point
│       ├── README.md          # CLI usage docs
│       └── vpp/               # The package (stdlib-only, shells out to `dotnet`)
│           ├── __init__.py
│           └── cli.py         # argparse subcommands: build / run / publish / doctor
└── video-piper/               # Main application root
    ├── Directory.Build.props      # Shared MSBuild properties (nullable, CPM)
    ├── Directory.Build.targets    # MSBuild targets (empty, extensible)
    ├── Directory.Packages.props   # Central package version management
    ├── global.json                # Uno.Sdk version pin
    ├── VideoPiper.sln             # Solution file
    ├── .gitignore                 # Git ignore rules
    ├── README.md                  # Project documentation
    └── VideoPiper/                # C# project root
        ├── VideoPiper.csproj      # Uno Platform project file (CSharpMarkup enabled)
        ├── App.xaml / App.xaml.cs # Application entry point, shared DataTemplates & theme resources
        ├── MainPage.cs            # Declarative WinUI 3 UI built with Uno C# Markup (both tabs)
        ├── GlobalUsings.cs        # Project-wide implicit usings
        ├── Models/
        │   ├── DownloadProgress.cs  # Progress state model (simple mode)
        │   ├── MediaEntry.cs        # yt-dlp metadata for one downloadable item
        │   └── LibraryItem.cs       # Library entry + MediaKind / ItemStatus enums, INotifyPropertyChanged
        ├── Converters/
        │   └── BoolToVisibilityConverter.cs  # XAML value converters
        ├── Services/
        │   ├── DownloadService.cs      # yt-dlp process runner (simple mode), MP3 or MP4 by MediaKind
        │   ├── LibraryDownloadService.cs # Pre-fetch + sequential download into the library
        │   ├── LibraryStore.cs         # Library root, .videopiper/library.json index, folder resolution
        │   ├── YtDlpJson.cs            # Shared parser for yt-dlp -J output (playlist vs single)
        │   ├── SearchService.cs        # Debounced YouTube search via youtubesearch extractor
        │   ├── SystemService.cs        # Tool detection (yt-dlp, ffmpeg)
        │   ├── FolderPickerService.cs  # Native Windows folder picker
        │   ├── PreferencesService.cs   # JSON-based preferences persistence
        │   └── ToolInstallerService.cs # Downloads yt-dlp.exe & ffmpeg.zip
        └── ViewModels/
            ├── MainViewModel.cs    # MVVM view model for the simple (Nedladdning) tab
            ├── LibraryViewModel.cs # MVVM view model for the library (Bibliotek) tab + search
            └── RelayCommand.cs     # ICommand implementations (RelayCommand / ParameterizedRelayCommand)
```

> Note: `video-piper/` also contains leftover, **untracked** scaffolding from the earlier Tauri/Deno experiments (`src-tauri/`, `dist/`, `node_modules/`). These are not part of the build — the app is entirely the Uno/C# project under `VideoPiper/`.


---

## 3. Technology Stack & Tooling

| Component | Technology | Version / Details |
|:---|:---|:---|
| **UI Framework** | Uno Platform (WinUI 3 + C# Markup) | 6.6.x |
| **Runtime** | .NET | 10.0 |
| **Language** | C# | 13 |
| **Windows SDK** | WinAppSDK (WinUI 3 target) | 1.7.x |
| **Renderer** | Skia Renderer (`SkiaRenderer` UnoFeature) | cross-platform desktop |
| **Package Manager** | MSBuild Central Package Management | — |

---

## 4. Development Workflow & Commands

> **Important**: Always run commands inside the `video-piper/` directory.

### Prerequisites
- **.NET 10 SDK** — https://dotnet.microsoft.com/download
- **Windows**: Windows 10+ (version 2004+) for the native WinUI 3 target; **or Linux/macOS** with GTK 3 dev packages for the Skia desktop target (e.g., `dnf install gtk3-devel` on Fedora/Nobara)
- **yt-dlp & ffmpeg**: Available on system PATH for download functionality (or use in-app installer)
- **Python 3.9+** (optional): only needed to install the `vpp` CLI from `tools/vpp/`

### vpp CLI (recommended)

A small, dependency-free Python CLI (`tools/vpp/`) wraps the .NET workflow and
knows about Uno's conditional multi-targeting. It finds `VideoPiper.sln` from
anywhere in the repo and auto-selects the right target for your OS.

```bash
cd tools/vpp
python3 -m pip install -e .     # or: uv tool install -e .   (adds `vpp` to PATH)
```

| Task | Command (run from anywhere in the repo) |
|:---|:---|
| **Check prerequisites** (.NET SDK, yt-dlp, ffmpeg, Inno Setup) | `vpp doctor` |
| **Build (auto-selects target per OS)** | `vpp build` |
| **Full rebuild / clean first** | `vpp build --no-incremental` · `vpp build --clean` |
| **Run the app** | `vpp run` |
| **Publish standalone exe** (win-x64, Release) | `vpp publish` (`--self-contained` for a full bundle) |
| **Build Installer & Packages** | `vpp installer` (or `python .\installer\build_installer.py` / `.\installer\build-installer.ps1`) |
| **Install via Python** | `python .\installer\install.py` (`--uninstall` to remove) |

Use `-c/--configuration` (default `Debug`) or `-f/--framework` to override. The
raw `dotnet` equivalents below remain available if you prefer not to install the CLI.

### Common Commands

| Task | Command (from `video-piper/`) |
|:---|:---|
| **Build (auto-selects target per OS)** | `dotnet build VideoPiper.sln` |
| **Build Windows target** (Windows host only) | `dotnet build -f net10.0-windows10.0.26100` |
| **Build Skia desktop target** (any OS) | `dotnet build -f net10.0` |
| **Run** | `dotnet run --project VideoPiper/VideoPiper.csproj` |
| **Publish Windows exe** | `dotnet publish -f net10.0-windows10.0.26100 -c Release -r win-x64 --self-contained true -o ./publish` |
| **Build Installer & Packages** | `python ../installer/build_installer.py` (or `powershell -ExecutionPolicy Bypass -File ../installer/build-installer.ps1`) |
| **Install / Uninstall via Python** | `python ../installer/install.py` / `python ../installer/install.py --uninstall` |

### Target Frameworks & Multi-Targeting

The project uses **conditional multi-targeting** in `VideoPiper/VideoPiper.csproj`:

| TFM | Renderer | Builds on |
|:---|:---|:---|
| `net10.0-windows10.0.26100` | WinUI 3 / WinAppSDK (native) | **Windows only** |
| `net10.0` | Skia Renderer | Windows, Linux, macOS |

- On a **Windows host**, only the WinAppSDK target is built (matching previous behavior).
- On **Linux/macOS hosts**, only the `net10.0` Skia desktop target is built. The WinAppSDK TFM is excluded because Uno cannot build it off-Windows (`UNOB0014`).
- Do **not** revert to a singular `<TargetFramework>` — use `<TargetFrameworks>` with the OS conditions shown in the csproj.
- Cross-compiling the WinAppSDK target from Linux via `-p:EnableWindowsTargeting=true` is not supported by Uno.WinUI (MSB4006) — Windows builds must run on a Windows host or `windows-latest` CI runner.

---

## 5. Architecture & Code Conventions

### 5.1 Backend Architecture — Native Process Management
Unlike the previous Deno Desktop version, this app has **no local HTTP server**. All functionality runs natively:

- **Simple-mode downloads**: `DownloadService.RunAsync(url, savePath, MediaKind, …)` spawns `yt-dlp` via `System.Diagnostics.Process` with stdout/stderr piped for real-time progress parsing. Audio uses `-x --audio-format mp3`; video uses `bestvideo[ext=mp4][height<=1080]+bestaudio[ext=m4a]/…` merged to MP4. Progress is reported through an `Action<DownloadProgress>` callback.
- **Library downloads**: `LibraryDownloadService.FetchEntriesAsync()` pre-fetches metadata (one entry for a single video, all entries for a playlist via `--flat-playlist`), then `DownloadManyAsync()` downloads entries sequentially so each item gets its own progress and status. Every mutation is registered in the `LibraryStore` before/after each step.
- **yt-dlp JSON parsing**: Centralized in `YtDlpJson.cs` (playlist vs single). Both download and search parse through it — don't add a second parser.
- **Search**: `SearchService.SearchAsync()` runs `ytsearchN:<query>` with `--flat-playlist -J` (one fast call, no downloads) and maps entries to `SearchResult`. Debouncing + cancellation live in `LibraryViewModel`.
- **Tool Detection**: `SystemService.CheckToolsAsync()` resolves yt-dlp and ffmpeg from either the app's local `Tools/` directory or system PATH. Reuse it for any new tool invocation rather than re-implementing path lookup.
- **In-App Installer**: `ToolInstallerService.InstallMissingAsync()` downloads yt-dlp.exe from GitHub releases and extracts ffmpeg from a ZIP archive into the app's local data folder.

### 5.2 Frontend — WinUI 3 with C# Markup & MVVM
- **C# Markup DSL**: UI is authored declaratively in C# using Uno Platform C# Markup (`Uno.Extensions.Markup`) in `MainPage.cs`, providing type-safe markup, fluent styling, and direct refactoring support. Shared row `DataTemplate`s live in `App.xaml` resources.
- **Two tabs**: `MainPage` hosts a `TabView` with two `TabViewItem`s — **"Nedladdning"** (`BuildSimpleTab` → `MainViewModel`) and **"Bibliotek"** (`BuildLibraryTab` → `LibraryViewModel`).
- **MVVM Pattern**: Each tab binds to its own view model via fluent `.Binding(...)` expressions. Commands (`ICommand` via `RelayCommand` / `ParameterizedRelayCommand`) handle all user interactions. View models implement `INotifyPropertyChanged` with a private `Set(ref field, value)` helper and refresh command `CanExecute` when relevant state changes.
- **Value Converters & Inlines**: Property builders support inline lambdas (e.g., `.Convert(...)`) as well as standalone `IValueConverter` implementations.
- **Uno XAML gotchas** (hit during development): `TabView<T>` generic parameters and `TabView.Items` are not supported — use plain `TabView` + `tab.TabItems.Add(TabViewItem)`. The XAML compiler rejects `RelativeSource AncestorType=…`; for a command inside an ItemTemplate that must reach the page/VM, wire `ListView.ItemClick` in code instead of an ancestor binding.
- **Theme Toggle**: Built-in dark/light theme switching via `App.SetTheme()` and persisted preference via `PreferencesService`.
- **Window Size**: Compact window footprint (`620x720`) configured via `AppWindow.Resize()` in `App.xaml.cs`.

### 5.3 Preferences & Persistence
User settings are stored as JSON files in the app's local data folder (`ApplicationData.Current.LocalFolder.Path`):
- `preferences.json` — save path, app mode (Simple/Library), library root, and output format (`MediaKind`)
- `theme.json` — current theme ("light" or "dark")

The **library index** is separate from preferences: it lives inside the user-chosen library root at `<root>/.videopiper/library.json`, written atomically (temp file + rename) by `LibraryStore`. Media files are organized as `<root>/<Channel>/<Playlist>/` via `ResolveTargetFolder` (invalid filename characters are sanitized; missing channel falls back to a `Misc` folder).

---

## 6. Migration Notes: Deno Desktop → Uno Platform

This project was migrated from a Deno Desktop (TypeScript) backend to a native C#/.NET WinUI 3 application.

### What Changed

| Aspect | Deno Desktop | Uno Platform (Current) |
|:---|:---|:---|
| **Runtime** | Deno v2.x (JavaScript/TypeScript) | .NET 10 (C#) |
| **UI Framework** | React 19 + Tailwind CSS | WinUI 3 with C# Markup via Uno Platform |
| **Backend** | HTTP/SSE server on localhost | Native process management |
| **Folder Picker** | PowerShell script / zenity / osascript | `Windows.Storage.Pickers.FolderPicker` (with HWND binding) |
| **Clipboard** | `navigator.clipboard` API | `Windows.ApplicationModel.DataTransfer.Clipboard` |
| **Persistence** | `localStorage` (browser) | JSON files in `ApplicationData.Current.LocalFolder` |

### Key Technical Differences

1. **No local server needed**: The Deno version ran an HTTP server on port 1420 that served the React SPA and provided SSE endpoints for downloads. The Uno version eliminates this entirely — `yt-dlp` is spawned directly from C# code.

2. **Native file I/O**: Preferences are now stored as JSON files instead of browser `localStorage`.

3. **C# Markup**: Instead of JSX or XAML, the UI is written in declarative C# Markup with strongly-typed fluent bindings.

4. **Command pattern**: WinUI uses `ICommand` for button bindings, implemented via `RelayCommand` in the ViewModel.

5. **Dispatcher threading**: UI updates from background threads use `App.MainWindowInstance.DispatcherQueue` instead of React's automatic batching.

---

## 7. Guidelines for AI Agents

1. **Build via `vpp` (preferred)**: Use the `vpp` CLI (`tools/vpp/`) — `vpp doctor`, `vpp build`, `vpp run`, `vpp publish`. It finds `VideoPiper.sln` from anywhere in the repo and auto-selects the correct target per OS, so you don't have to set `Cwd: video-piper` or remember TFMs. If the CLI isn't installed, fall back to running `dotnet build/run/publish` with `Cwd: video-piper`.
2. **Multi-Targeting**: The project uses conditional `<TargetFrameworks>` (WinAppSDK on Windows, `net10.0` Skia desktop elsewhere). Do not revert to a singular `<TargetFramework>`. When adding targets or OS-specific config, keep the existing MSBuild conditions intact.
3. **Platform-Conditional Code**: Use Uno's predefined symbols (`WINDOWS`, `__WASM__`, `HAS_UNO`, etc.) for platform-specific APIs. Windows-only APIs such as `Windows.Storage.Pickers`, `WinRT.Interop`, and in-app `MediaElement` playback must be wrapped in `#if WINDOWS` so the `net10.0` Skia target still compiles on Linux/macOS — see `FolderPickerService.cs` and `MainPage.cs` (player card) for the pattern.
4. **Clean Code**: Follow C# conventions. Use `async`/`await` properly, avoid blocking calls on UI thread, and prefer `ICommand` for button bindings.
5. **Swedish Strings**: Preserve Swedish localization for all user-facing strings. Do not introduce English-only strings without providing Swedish translations.
6. **Library changes**: When touching library behavior, keep the index as the single source of truth — mutate in memory then call `LibraryStore.SaveAsync()`. Route new downloads through `LibraryDownloadService` and parse any yt-dlp `-J` output via `YtDlpJson` (never a second ad-hoc parser). Keep per-item progress/status updates on the UI thread via the view model's dispatcher helper.
7. **Don't touch untracked scaffolding**: `video-piper/src-tauri/`, `video-piper/dist/`, and `node_modules/` are leftover, untracked artifacts from earlier experiments — ignore them; they are not part of the build.

