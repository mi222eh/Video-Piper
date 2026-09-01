# Video Piper

A lightweight Windows desktop application for downloading YouTube videos as MP3 audio files. Built with C#, .NET 10, and Uno Platform (WinUI 3).

## Features

- **Direct YouTube to MP3 download** — Spawns `yt-dlp` subprocesses with real-time progress streaming
- **Native folder picker** — Uses Windows.Storage.Pickers for native Windows folder selection dialogs
- **Uno Platform C# Markup** — Fluent declarative C# DSL for UI composition instead of XAML
- **System tray & theme toggle** — Minimal, modern WinUI 3 UI with dark/light theme support
- **Tool installation** — In-app installer for missing dependencies (yt-dlp, ffmpeg)
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
├── App.xaml / App.xaml.cs     # Application entry point & theme initialization
├── MainPage.cs                # Main UI built declaratively with Uno C# Markup
├── Models/
│   └── DownloadProgress.cs    # Progress state model
├── Converters/
│   └── BoolToVisibilityConverter.cs  # Value converters
├── Services/
│   ├── DownloadService.cs     # yt-dlp process runner with progress parsing
│   ├── SystemService.cs       # Tool detection (yt-dlp, ffmpeg)
│   ├── FolderPickerService.cs # Native Windows folder picker with HWND binding
│   ├── PreferencesService.cs  # JSON-based preferences persistence
│   └── ToolInstallerService.cs # Downloads yt-dlp.exe & ffmpeg.zip
└── ViewModels/
    ├── MainViewModel.cs       # MVVM view model with all commands and reactive state
    └── RelayCommand.cs        # ICommand implementation for WinUI
```

### Key Design Decisions

- **C# Markup DSL**: The UI is authored declaratively in C# using Uno Platform C# Markup (`Uno.Extensions.Markup`), providing strong typing, refactoring safety, and fluent layout construction.
- **MVVM Pattern**: The UI binds to `MainViewModel` via fluent `.Binding(...)` expressions. Commands handle user interactions.
- **No HTTP Server**: Runs entirely as a native Windows application with no local server needed.
- **Process-based Downloads**: `yt-dlp` is spawned directly via `System.Diagnostics.Process` with stdout/stderr piped for real-time progress parsing.
- **JSON Preferences**: User settings (save path, theme) are stored as JSON files in the app's local data folder (`ApplicationData.Current.LocalFolder`).


## Building & Running

### Prerequisites

- **.NET 10 SDK** — https://dotnet.microsoft.com/download
- **Windows 10+** (version 2004+)
- **Visual Studio 2022** or **VS Code** with C# Dev Kit

### Build

```bash
cd video-piper
dotnet build VideoPiper/VideoPiper.csproj
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

## External Dependencies

The app requires two external tools to function:

| Tool | Purpose | Installation |
|------|---------|-------------|
| **yt-dlp** | Video download & metadata extraction | In-app installer or system PATH |
| **ffmpeg** | Audio conversion (MP3 encoding) | In-app installer or system PATH |

### In-App Installer

When the app starts and detects missing tools, it offers one-click installation:
- **yt-dlp**: Downloaded from GitHub releases (`yt-dlp.exe`)
- **ffmpeg**: Downloaded as a ZIP archive and extracted (`ffmpeg.exe`)

Installed tools are stored in the app's local data folder under `Tools/` and take priority over system PATH binaries.

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
