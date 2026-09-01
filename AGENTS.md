# AGENTS.md - Video-Piper Developer & Agent Guide

Welcome to the **Video-Piper** repository. This document provides essential project context, architectural guidelines, conventions, and operational workflows for AI agents and human contributors working on this codebase.

---

## 1. Project Overview

**Video-Piper** is a lightweight Windows desktop application for downloading YouTube videos as MP3 audio files. Built with **C#**, **.NET 10**, and **Uno Platform (WinUI 3)**.

### Key Capabilities
- **Direct YouTube to MP3 download**: Spawns `yt-dlp` subprocesses directly through `System.Diagnostics.Process` with real-time progress parsing from stdout/stderr.
- **Native folder picker**: Uses `Windows.Storage.Pickers.FolderPicker` for native Windows folder selection dialogs.
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
        ├── App.xaml / App.xaml.cs # Application entry point & theme resources
        ├── MainPage.cs            # Declarative WinUI 3 UI built with Uno C# Markup
        ├── Models/
        │   └── DownloadProgress.cs  # Progress state model
        ├── Converters/
        │   └── BoolToVisibilityConverter.cs  # XAML value converters
        ├── Services/
        │   ├── DownloadService.cs      # yt-dlp process runner with progress parsing
        │   ├── SystemService.cs        # Tool detection (yt-dlp, ffmpeg)
        │   ├── FolderPickerService.cs  # Native Windows folder picker
        │   ├── PreferencesService.cs   # JSON-based preferences persistence
        │   └── ToolInstallerService.cs # Downloads yt-dlp.exe & ffmpeg.zip
        └── ViewModels/
            ├── MainViewModel.cs    # MVVM view model with all commands
            └── RelayCommand.cs     # ICommand implementation for WinUI
```


---

## 3. Technology Stack & Tooling

| Component | Technology | Version / Details |
|:---|:---|:---|
| **UI Framework** | Uno Platform (WinUI 3) | 6.7.x |
| **Runtime** | .NET | 10.0 |
| **Language** | C# | 13 |
| **Windows SDK** | WinAppSDK | 1.7.x |
| **Package Manager** | MSBuild Central Package Management | — |

---

## 4. Development Workflow & Commands

> **Important**: Always run commands inside the `video-piper/` directory.

### Prerequisites
- **.NET 10 SDK** — https://dotnet.microsoft.com/download
- **Windows 10+** (version 2004+)
- **yt-dlp & ffmpeg**: Available on system PATH for download functionality (or use in-app installer)

### Common Commands

| Task | Command (from `video-piper/`) |
|:---|:---|
| **Build** | `dotnet build VideoPiper/VideoPiper.csproj` |
| **Run** | `dotnet run --project VideoPiper/VideoPiper.csproj` |
| **Publish (standalone exe)** | `dotnet publish VideoPiper/VideoPiper.csproj -c Release -r win-x64 --self-contained true -o ./publish` |

---

## 5. Architecture & Code Conventions

### 5.1 Backend Architecture — Native Process Management
Unlike the previous Deno Desktop version, this app has **no local HTTP server**. All functionality runs natively:

- **yt-dlp Downloads**: `DownloadService.RunAsync()` spawns `yt-dlp` via `System.Diagnostics.Process` with stdout/stderr piped for real-time progress parsing. Progress is reported through an `Action<DownloadProgress>` callback.
- **Tool Detection**: `SystemService.CheckToolsAsync()` resolves yt-dlp and ffmpeg from either the app's local `Tools/` directory or system PATH.
- **In-App Installer**: `ToolInstallerService.InstallMissingAsync()` downloads yt-dlp.exe from GitHub releases and extracts ffmpeg from a ZIP archive into the app's local data folder.

### 5.2 Frontend — WinUI 3 with MVVM
- **Path Aliases**: Not needed — C# uses namespaces (e.g., `VideoPiper.ViewModels.MainViewModel`).
- **MVVM Pattern**: The UI binds to `MainViewModel` via XAML data binding. Commands handle all user interactions.
- **Value Converters**: `BoolToVisibilityConverter`, `InverseBoolToVisibilityConverter`, and `StringToVisibilityConverter` in `Converters/` namespace for XAML bindings.
- **Theme Toggle**: Built-in dark/light theme switching with persisted preference via `PreferencesService`.

### 5.3 Preferences & Persistence
User settings (save path, theme) are stored as JSON files in the app's local data folder (`ApplicationData.Current.LocalFolder.Path`):
- `preferences.json` — save path
- `theme.json` — current theme ("light" or "dark")

---

## 6. Migration Notes: Deno Desktop → Uno Platform

This project was migrated from a Deno Desktop (TypeScript) backend to a native C#/.NET WinUI 3 application.

### What Changed

| Aspect | Deno Desktop | Uno Platform (Current) |
|:---|:---|:---|
| **Runtime** | Deno v2.x (JavaScript/TypeScript) | .NET 10 (C#) |
| **UI Framework** | React 19 + Tailwind CSS | WinUI 3 via Uno Platform |
| **Backend** | HTTP/SSE server on localhost | Native process management |
| **Folder Picker** | PowerShell script / zenity / osascript | `Windows.Storage.Pickers.FolderPicker` |
| **Clipboard** | `navigator.clipboard` API | `Windows.ApplicationModel.DataTransfer.Clipboard` |
| **Persistence** | `localStorage` (browser) | JSON files in `ApplicationData.Current.LocalFolder` |

### Key Technical Differences

1. **No local server needed**: The Deno version ran an HTTP server on port 1420 that served the React SPA and provided SSE endpoints for downloads. The Uno version eliminates this entirely — `yt-dlp` is spawned directly from C# code.

2. **Native file I/O**: Preferences are now stored as JSON files instead of browser `localStorage`.

3. **Value converters**: XAML bindings use `IValueConverter` implementations (BoolToVisibility, StringToVisibility) instead of JavaScript template literals.

4. **Command pattern**: WinUI uses `ICommand` for button bindings, implemented via `RelayCommand` in the ViewModel.

5. **Dispatcher threading**: UI updates from background threads use `CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync()` instead of React's automatic batching.

---

## 7. Guidelines for AI Agents

1. **Working Directory Awareness**: Ensure commands like `dotnet build`, `dotnet run` are executed with `Cwd: video-piper`.
2. **Single Target Framework**: The project targets only `net10.0-windows10.0.26100` — use singular `TargetFramework` in .csproj files.
3. **Clean Code**: Follow C# conventions. Use `async`/`await` properly, avoid blocking calls on UI thread, and prefer `ICommand` for button bindings.
4. **Swedish Strings**: Preserve Swedish localization for all user-facing strings. Do not introduce English-only strings without providing Swedish translations.
