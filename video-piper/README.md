# Video Piper

A lightweight cross-platform desktop application for downloading YouTube videos and audio as MP3 or MP4. Built with C#, .NET 10, and Uno Platform (WinUI 3). Targets Windows natively plus a Skia desktop build for Linux/macOS.

## Features

- **Direct YouTube download** — Spawns `yt-dlp` subprocesses with real-time progress streaming
- **MP3 or MP4 output** — Audio (MP3) or video (MP4, up to a user-set resolution, default 1080p); the choice is persisted
- **Per-item format override** — Any library item can be re-downloaded as MP3 **or** MP4 regardless of the global toggle (per-item buttons in the selected-item bar)
- **MP3 reuse for MP4** — When an item already has a downloaded MP3 and you request the MP4 edition, only the **video stream** is downloaded and the existing MP3 is merged into it with the app-managed ffmpeg — the audio is never re-downloaded or re-encoded. If the source has no separate video stream it falls back to a normal full video download
- **Video quality settings** — Choose a maximum resolution (480–2160p) and a minimum framerate (24/30/60) for new downloads (default 1080p / 60 fps). The height is capped "at-or-below" the preference, so the **closest available** resolution is picked — a preference bigger than the source's max yields the largest available stream
- **Library mode** — YouTube-style layout: a centered player with a right-hand search + collection list; persistent library organized as `Channel / Playlist`, with per-item progress and local playback
- **No auto-play** — Selecting an item loads it into the player without starting playback; a **missing-file prompt** with a re-download button appears when a completed item's file is gone
- **Download queue** — Library downloads run through a single-worker queue with a persistent queue card (one row per job, live progress, "Kö N" badge); failed items can be retried in bulk from the **Misslyckades** section
- **Per-item logs** — Click a queue row to open its live log panel, streaming that job's `yt-dlp` output line by line
- **Delete modes** — Deleting an item always removes its index entry; with the default **delete mode** it also removes the file, orphaned fragments, and now-empty folders. A settings toggle keeps files on disk instead.
- **Native folder picker** — Uses Windows.Storage.Pickers for native Windows folder selection dialogs
- **Uno Platform C# Markup** — Fluent declarative C# DSL for UI composition instead of XAML
- **Theme toggle** — Minimal, modern WinUI 3 UI with dark/light theme support
- **App-managed tools** — The app installs and manages its **own** yt-dlp and ffmpeg binaries (system binaries are never used): missing tools install automatically, a **daily background update check** keeps them current, and a **settings page** (gear in the app bar) shows installed/latest version per tool with manual check/update buttons
- **Swedish localization** — All user-facing strings in Swedish

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **UI Framework** | Uno Platform (WinUI 3 + C# Markup) | 6.6.x |
| **Runtime** | .NET | 10.0 |
| **Language** | C# | 13 |
| **Windows SDK** | WinAppSDK | 1.7.x |

## Architecture

```
VideoPiper/
├── VideoPiper.csproj          # Uno Platform project with CSharpMarkup feature
├── App.xaml / App.xaml.cs     # Application entry point, shared DataTemplates & theme init
├── MainPage.cs                # Main UI (both tabs) built declaratively with Uno C# Markup
├── GlobalUsings.cs            # Project-wide implicit usings
├── Models/
│   ├── DownloadProgress.cs    # Progress state model (simple mode)
│   ├── MediaEntry.cs          # yt-dlp metadata for one downloadable item
│   └── LibraryItem.cs         # Library entry + MediaKind / ItemStatus enums
├── Converters/
│   ├── BoolToVisibilityConverter.cs  # Value converters
│   └── StatusToBrushConverter.cs     # Maps ItemStatus to Vp* state brushes for queue rows
├── Services/
│   ├── DownloadService.cs     # yt-dlp process runner (simple mode), MP3 or MP4 by MediaKind
│   ├── LibraryDownloadService.cs # Pre-fetch + sequential download into the library
│   ├── LibraryStore.cs        # Library root, .videopiper/library.json index, folder resolution
│   ├── YtDlpJson.cs           # Shared parser for yt-dlp -J output (playlist vs single)
│   ├── SearchService.cs       # Debounced YouTube search via youtubesearch extractor
│   ├── SystemService.cs       # Tool resolution (app-local Tools/ only) + once-per-day background update check
│   ├── FolderPickerService.cs # Native Windows folder picker with HWND binding
│   ├── PreferencesService.cs  # JSON-based preferences persistence
│   └── ToolUpdateService.cs   # GitHub latest-release check, version compare, download/extract of yt-dlp & ffmpeg
└── ViewModels/
    ├── ShellViewModel.cs      # App bar: mode switcher, tool status, theme, settings toggle
    ├── SettingsViewModel.cs   # Settings page: per-tool installed/latest version + check/update
    ├── MainViewModel.cs       # MVVM view model for the simple (Nedladdning) tab
    ├── LibraryViewModel.cs    # MVVM view model for the library (Bibliotek) tab + search
    ├── LibraryViewModel.Queue.cs # partial: download queue + single worker + retry
    ├── LibraryQueueEntry.cs   # One queue row: status, percent, per-item log lines
    └── RelayCommand.cs        # ICommand implementations (RelayCommand / ParameterizedRelayCommand)
```

### Key Design Decisions

- **C# Markup DSL**: The UI is authored declaratively in C# using Uno Platform C# Markup (`Uno.Extensions.Markup`), providing strong typing, refactoring safety, and fluent layout construction.
- **MVVM Pattern**: Each tab binds to its own view model via fluent `.Binding(...)` expressions. Commands handle user interactions.
- **No HTTP Server**: Runs entirely as a native application with no local server needed.
- **Process-based Downloads**: `yt-dlp` is spawned directly via `System.Diagnostics.Process` with stdout/stderr piped for real-time progress parsing.
- **Persistent Library**: The library index lives at `<root>/.videopiper/library.json` (written atomically); media files are organized as `<root>/<Channel>/<Playlist>/`.
- **JSON Preferences**: User settings (save path, app mode, library root, output format, video quality, delete mode, theme) are stored as JSON files in the app's local data folder (`ApplicationData.Current.LocalFolder`).


## Building & Running

### Prerequisites

- **.NET 10 SDK** — https://dotnet.microsoft.com/download
- **Windows 10+** (version 2004+) for the native target, **or** Linux/macOS with GTK 3 dev packages for the Skia desktop target
- **Visual Studio 2022** or **VS Code** with C# Dev Kit

### Build

```bash
cd video-piper
dotnet build VideoPiper/VideoPiper.csproj            # auto-selects target per OS
dotnet build -f net10.0-windows10.0.26100           # Windows (WinAppSDK) target — Windows host only
dotnet build -f net10.0                             # Skia desktop target — any OS
```

### Run

```bash
cd video-piper
dotnet run --project VideoPiper/VideoPiper.csproj
```

### Publish (standalone executable)

```bash
cd video-piper
dotnet publish VideoPiper/VideoPiper.csproj -c Release -r win-x64 --self-contained true -o ./publish
```

### Package (installer + portable ZIP)

From the repository root, build the Windows Setup installer and portable ZIP
(auto-publishes this bundle if stale):

```bash
uv run --no-project installer/build_installer.py     # or: vpp installer
```

## External Dependencies

The app requires two tools, which it **installs and manages itself** — system binaries are never used:

| Tool | Purpose | Managed by the app |
|------|---------|--------------------|
| **yt-dlp** | Video download & metadata extraction | Auto-installed on first run; auto-updated daily from GitHub releases |
| **ffmpeg** | Audio conversion (MP3) and video merging (MP4) | Auto-installed on first run; auto-updated daily from `GyanD/codexffmpeg` releases |

### App-managed tools & updates

- **Install**: on first run (or when a binary is missing) the app downloads the latest release — `yt-dlp.exe` for yt-dlp, the `ffmpeg-<tag>-full_build.zip` archive for ffmpeg — into its local data folder under `Tools/`.
- **Update check**: once per day the app compares the installed version with the newest GitHub release and updates in the background when a newer version exists. The last-check timestamp is stored in `tools.json` in the app's local data folder.
- **Settings page**: the gear button in the app bar opens *Inställningar*, which shows the installed and latest version per tool with "Kontrollera uppdatering" (check) and "Uppdatera" (install latest) buttons. Manual checks do not affect the once-per-day background gate.

## Migration Notes: Deno Desktop → Uno Platform

This project was migrated from a Deno Desktop (TypeScript) backend to a native C#/.NET WinUI 3 application.

### What Changed

| Aspect | Deno Desktop | Uno Platform (Current) |
|--------|-------------|----------------------|
| **Runtime** | Deno v2.x (JavaScript/TypeScript) | .NET 10 (C#) |
| **UI Framework** | React + Tailwind CSS | WinUI 3 via Uno Platform |
| **Backend** | HTTP/SSE server on localhost | Native process management |
| **Folder Picker** | PowerShell script / zenity / osascript | `Windows.Storage.Pickers.FolderPicker` |
| **Clipboard** | `navigator.clipboard` API | `Windows.ApplicationModel.DataTransfer.Clipboard` |
| **Persistence** | `localStorage` (browser) | JSON files in `ApplicationData.Current.LocalFolder` |

### Key Technical Differences

1. **No local server needed**: The Deno version ran an HTTP server on port 1420 that served the React SPA and provided SSE endpoints for downloads. The Uno version eliminates this entirely — `yt-dlp` is spawned directly from C# code.

2. **Native file I/O**: Preferences are now stored as JSON files instead of browser `localStorage`.

3. **Value converters**: XAML bindings use `IValueConverter` implementations (BoolToVisibility, StringToVisibility) instead of JavaScript template literals.

4. **Command pattern**: WinUI uses `ICommand` for button bindings, implemented via `RelayCommand<T>` in the ViewModel.

5. **Dispatcher threading**: UI updates from background threads use `CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()` instead of React's automatic batching.

## License

MIT
