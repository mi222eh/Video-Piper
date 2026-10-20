# AGENTS.md - Video-Piper Developer & Agent Guide

Welcome to the **Video-Piper** repository. This document provides essential project context, architectural guidelines, conventions, and operational workflows for AI agents and human contributors working on this codebase.

---

## 1. Project Overview

**Video-Piper** is a cross-platform desktop application for downloading YouTube videos and audio. Built with **C#**, **.NET 10**, and **Uno Platform**. The primary target is Windows (native WinUI 3 / WinAppSDK), with an additional Skia-rendered desktop target that builds and runs on Linux and macOS.

It has two modes, exposed as tabs in the main window:
- **Nedladdning** (Simple): one-off downloads of a single link to a chosen folder, as MP3 or MP4.
- **Bibliotek** (Library): a managed, persistent library with channel/playlist subfolders, YouTube search (run on Sök/Enter, not per-keystroke), per-item progress, and local playback.

### Key Capabilities
- **Direct YouTube download**: Spawns `yt-dlp` subprocesses directly through `System.Diagnostics.Process` with real-time progress parsing from stdout/stderr. Audio → MP3; video → MP4 (bestvideo+bestaudio merged). The video format string is built by `DownloadService.BuildVideoFormat(maxHeight, minFps)` from the user's quality settings (default 1080p / 60 fps): it caps the height and, when a framerate floor is set, prefers `fps>=N` but always falls back to ≤height variants, so a 60 fps setting never breaks a 24/30 fps-only source.
- **MP3/MP4 format choice**: A global toggle selects the output format in both modes (persisted). In addition, **any individual library item can be re-downloaded as either MP3 or MP4 regardless of the global toggle** — the selected-item bar has MP3/MP4 buttons (`LibraryViewModel.DownloadSelectedAsAsync(kind)`) that queue the item with a per-job `kindOverride`, so the global toggle is never flipped.
- **MP3 reuse for MP4**: When an item already has a downloaded MP3 and the user asks for the MP4 edition (per-item MP4 button), the library runner downloads **only the video stream** (`DownloadService.BuildVideoOnlyFormat` — `bestvideo[height<=N]` branches, no audio, no combined-stream fallback) and merges the existing MP3 into it with the app-managed ffmpeg (`LibraryDownloadService.MergeVideoWithMp3Async`): `ffmpeg -c:v copy` first (instant for h264/AV1 mp4 streams), falling back to a libx264 re-encode only when the codec can't be copied into MP4 (VP9); audio is transcoded MP3→AAC (`-b:a 192k -shortest`), the output is written to a `<stem>.merging.mp4` temp file and then moved to `<stem>.mp4` (same stem as the MP3). If no separate video stream exists (or the video-only download fails), it logs `"Endast-videodel finns inte …"` and falls back to the normal full video download (no merge). The MP3 is **kept** — deleting the item removes both files via the `[id]` marker.
- **Managed library**: Persistent index (`<root>/.videopiper/library.json`) tracking items across launches. Files are organized as `<root>/<Channel>/<Playlist>/`, resolved by `LibraryStore.ResolveTargetFolder`. Interrupted downloads are marked failed on next load.
- **Download queue**: Library downloads go through a single-worker queue (`ConcurrentQueue<QueueJob>` in `LibraryViewModel`) — playlists and repeated single downloads enqueue, the worker picks them up one at a time. Active-download feedback comes from the compact **progress card** (ring + percent + Avbryt) plus the app bar **queue icon** (badge = jobs still in flight). The full **queue panel** (per-job rows + live log) is **hidden by default** and toggled by that icon; it auto-closes when every job has finished (kept open while a row's log is being read). A single failure marks that item `Failed` and the queue continues; a cancel (Avbryt) stops the active job and drains the rest.
- **Retry failed items**: Failed items are collected in a dedicated **"Misslyckades"** sidebar section (with count badge) and can be re-queued from three places — the section's **Försök igen** CTA, the error-card **Försök igen** button (bulk, `RetryFailedCommand`), and a **Försök igen** button in the queue log panel (`RetryQueueEntryCommand`) after selecting a failed queue row.
- **Delete items**: The selected-item bar's **Ta bort** button deletes the selected item — in the default `DeleteFiles` mode it first shows a Swedish confirmation dialog naming the consequence (the file is removed from disk, it cannot be undone); `KeepFiles` mode never asks because the media survives. It always removes the index entry; whether the media on disk is removed depends on the **delete mode** setting (`PreferencesService.DeleteMode`): `DeleteFiles` (default) removes the file + any orphaned `.part`/fragment files (matched by the item's `[id]` output-template marker) via `LibraryStore.DeleteMediaFiles`, **and** deletes now-empty channel/playlist folders up to (never including) the library root via `LibraryStore.DeleteEmptyFolders`; `KeepFiles` removes only the index entry, leaving files on disk.
- **Player pop-out / fullscreen**: The docked player header has a **pop-out** button that opens the item in a standalone `PlayerPopOutWindow` (its own player + transport controls). The pop-out has a **fullscreen** toggle (`AppWindow.SetPresenter`) and a close button; closing it restores the docked player. While the pop-out is open, the docked `MediaPlayer` is **paused** (not just hidden) so only one pipeline plays; it resumes automatically on pop-out close if it was playing when the pop-out opened. Windows-only.
- **Simple-mode success actions**: After a one-off download the finished file path (parsed from yt-dlp's `[destination]` line via `DownloadService.ParseDestination`) is surfaced as a success card with **Öppna fil** / **Öppna mapp** / **Ladda ner en till**.
- **YouTube-style library layout**: The Bibliotek view is a left sidebar (`BuildLibrarySidebar`) beside a **full-width main content area** (`BuildLibraryMainContent`). The main area's top holds the search row (search box + Sök/Ladda ner + the global MP3/MP4 toggle + Klistra in); below it, the **browse content is the hero** — the collection list (Alla filer / Ljudfiler / Videofiler / Misslyckades) or the search results, switched by `IsSearchActive`/`IsCollectionActive`. The player dock, selected-item bar, status cards and download queue are demoted to a bottom band under the browse area. Selecting a collection row loads it into the player.
- **No auto-play**: Selecting an item **loads it into the player without starting playback** (both players use `AutoPlay(false)`); "Spela" / the transport controls start it.
- **Missing-file prompt**: When the selected item is `Complete` but its file is gone, the player area shows a **"Filen saknas på disk"** prompt with a **Ladda ner** re-download button (the docked player is cleared/hidden); when the re-download completes the item reloads into the player automatically.
- **Video quality settings**: A settings card lets the user pick a **maximum resolution** (480/720/1080/1440/2160, default 1080) and a **minimum framerate** (24/30/60, default 60) for new downloads; both are persisted and fed into `BuildVideoFormat` / `BuildVideoOnlyFormat`. The height cap is `height<=N`, i.e. **closest-available at-or-below**: if the preference is bigger than what the source offers, the largest available stream is used (e.g. a 1080p setting downloads 1440×1080 AV1, which is ≤1080 in height); the fps floor is only a preference — sources below the floor still download (verified live).
- **Library settings card**: The settings page's **Bibliotek** card holds the delete-mode toggle (Ta bort filer och tomma mappar / Behåll filer) plus the resolution and framerate combos (bound to `SettingsViewModel.DeleteFiles` / `ResolutionIndex` / `FramerateIndex`).
- **Sorting**: Library lists sort by Nyast först / Titel A–Ö / Längst först (collection header combo box, `LibraryViewModel.SortIndex`).
- **YouTube search + video info page**: Search via yt-dlp's `youtubesearch` extractor — it runs only when the user presses **Sök** or **Enter** (never per keystroke; typing only updates the box and invalidates previous results). Results render as the **full-width main content**. Clicking a result row **never downloads** — it opens a **video info page** (title, "Av {uploader} · {duration}", the full description fetched in the background via `SearchService.FetchDetailJsonAsync` → `YtDlpJson.GetDescription`). The info page has two buttons — **Ladda ner ljud** (MP3) and **Ladda ner video** (MP4) — that enqueue the download with the chosen kind without flipping the global toggle. A result that's already in the library (or already queued) shows "Den här videon finns redan i biblioteket." with the buttons disabled; once a download from the page completes, the page updates itself. The info card is scrollable so long descriptions never hide the buttons.
- **Playlist support**: A playlist URL pre-fetches all entries (`--flat-playlist`) and downloads them sequentially, each with its own progress/status.
- **Folder picker**: Native `Windows.Storage.Pickers.FolderPicker` on Windows; on non-Windows targets the UI falls back to manual path entry / default Music folder.
- **App-managed tools**: The app downloads and manages its **own** yt-dlp and ffmpeg binaries (into its local `Tools/` directory) — system binaries are never used. Missing tools install automatically at first run; a **once-per-day background update check** compares the installed version with the latest GitHub release and updates in the background. A **settings page** (gear in the app bar → *Inställningar*) shows installed/latest version per tool with manual "Kontrollera uppdatering" / "Uppdatera" buttons.
- **Dark/Light theme toggle**: Built-in theme switching with persisted preference.
- **Remembered window size**: the app restores the last window size on launch and saves it on close (default 1060×740, Windows-only).
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
├── installer/
│   ├── build_installer.py # Python builder: publish + portable ZIP + Inno Setup exe (run with `uv run --no-project`)
│   ├── convert_icon.py    # PNG → multi-resolution .ico for the installer (run with `uv run --with Pillow`)
│   ├── install.py         # Portable-ZIP helper: Start menu / desktop shortcuts + Add/Remove registration
│   ├── VideoPiper.iss     # Inno Setup script (Swedish + English, lzma2/ultra64)
│   └── app.ico            # Installer icon (generated from app-icon.png; cached)
├── tools/
│   └── vpp/                   # `vpp` CLI — Python project manager (build/run/publish/installer)
│       ├── pyproject.toml     # Packaging + `vpp` console-script entry point
│       ├── README.md          # CLI usage docs
│       └── vpp/               # The package (stdlib-only, shells out to `dotnet`/`uv`)
│           ├── __init__.py
│           └── cli.py         # argparse subcommands: build / run / publish / installer / doctor
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
        ├── App.xaml / App.xaml.cs # Application entry point, shared DataTemplates, design tokens & theme resources
        ├── MainPage.cs            # App shell (app bar + mode views) and both feature views, in Uno C# Markup
        ├── PlayerPopOutWindow.cs  # Windows-only pop-out player window (own player + fullscreen toggle)
        ├── Log.cs                 # Ambient logging facade (Log.For(typeof(T)) → MEL ILogger; NullLogger when unwired)
        ├── GlobalUsings.cs        # Project-wide implicit usings
        ├── Models/
        │   ├── DownloadProgress.cs  # Progress state model (simple mode) — includes Destination (finished file path)
        │   ├── MediaEntry.cs        # yt-dlp metadata for one downloadable item
        │   └── LibraryItem.cs       # Library entry + MediaKind / ItemStatus enums, INotifyPropertyChanged (PropertyChanged marshaled to UI via static PostToUi hook)
        ├── Converters/
        │   ├── BoolToVisibilityConverter.cs  # XAML value converters
        │   └── StatusToBrushConverter.cs     # Maps ItemStatus to Vp* state brushes for queue rows
        ├── Services/
        │   ├── DownloadService.cs      # yt-dlp process runner (simple mode), MP3 or MP4 by MediaKind; BuildVideoFormat / BuildVideoOnlyFormat(maxHeight,minFps); parses [destination]
        │   ├── LibraryDownloadService.cs # Metadata fetch + single-entry download (queue lives in the VM); per-item MediaKind, MP3-reuse → video-only + ffmpeg merge
        │   ├── LibraryStore.cs         # Library root, .videopiper/library.json index, folder resolution, DeleteMediaFiles + DeleteEmptyFolders (platform-safe sanitize)
        │   ├── YtDlpJson.cs            # Shared parser for yt-dlp -J output (playlist vs single)
        │   ├── SearchService.cs        # YouTube search via youtubesearch extractor (triggered by Sök/Enter)
        │   ├── SystemService.cs        # Tool resolution (app-local Tools/ only) + once-per-day background update check
        │   ├── ToolUpdateService.cs    # GitHub latest-release check, version compare, download/extract of yt-dlp & ffmpeg into the app's Tools/
        │   ├── FolderPickerService.cs  # Native Windows folder picker
        │   ├── PreferencesService.cs   # JSON-based preferences persistence
        │   └── SafeWorkingDirectory.cs # Validates a working directory before spawning a child (WinError 448 guard); temp fallback
        └── ViewModels/
            ├── ShellViewModel.cs   # App bar: mode switcher (ShellMode), tool status pill, theme, settings toggle
            ├── SettingsViewModel.cs # Settings page: per-tool (yt-dlp/ffmpeg) versions + check/update, and the Bibliotek card (delete mode + resolution + framerate)
            ├── MainViewModel.cs    # MVVM view model for the simple (Snabbnedladdning) view
            ├── LibraryViewModel.cs # MVVM view model for the library view + search (partial; queue in the file below)
            ├── LibraryViewModel.Queue.cs # partial: download queue (ConcurrentQueue<QueueJob>), single worker, retry
            ├── LibraryQueueEntry.cs # One queue row: title, status, percent, per-item log lines
            └── RelayCommand.cs     # ICommand implementations (RelayCommand / ParameterizedRelayCommand)
```

> `video-piper/tests/VideoPiper.Tests/` is an MSTest project that compiles the platform-neutral sources
> directly (no project reference): all of `Models/*.cs` plus `YtDlpJson`, `DownloadService`,
> `SearchService`, `LibraryStore`, `PreferencesService`, `SystemService`, `ToolUpdateService`.
> When you make a service testable in isolation, add it to that `<Compile>` list.

> Note: the earlier Tauri/Deno scaffolding (`src-tauri/`, `dist/`, `node_modules/`, the root Deno-era `.vscode/`) has been removed from the repository. The app is entirely the Uno/C# project under `video-piper/VideoPiper/` — if old frontend files reappear, they are leftovers, not part of the build.


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
- **yt-dlp & ffmpeg**: The app installs and manages its own copies (no system install required); see "App-managed tools" in Key Capabilities.
- **Python 3.9+**: needed for the `vpp` CLI (`tools/vpp/`) and the installer builder
- **[uv](https://docs.astral.sh/uv/)**: recommended — runs `installer/build_installer.py` without environment setup, and provides Pillow on demand for icon regeneration

### vpp CLI (recommended)

A small, dependency-free Python CLI (`tools/vpp/`) wraps the .NET workflow and
knows about Uno's conditional multi-targeting. It finds `VideoPiper.sln` from
anywhere in the repo and auto-selects the right target for your OS.

```bash
cd tools/vpp
uv tool install -e .     # or: python3 -m pip install -e .   (adds `vpp` to PATH)
```

| Task | Command (run from anywhere in the repo) |
|:---|:---|
| **Check prerequisites** (.NET SDK, yt-dlp, ffmpeg, uv, Inno Setup) | `vpp doctor` |
| **Build (auto-selects target per OS)** | `vpp build` |
| **Full rebuild / clean first** | `vpp build --no-incremental` · `vpp build --clean` |
| **Run the app** | `vpp run` |
| **Publish standalone exe** (win-x64, Release) | `vpp publish` (`--self-contained` for a full bundle) |
| **Build Installer & Packages** | `vpp installer` (runs `installer/build_installer.py` via `uv`; flags `--publish`, `--zip-only`, `--setup-only`) |
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
| **Build Installer & Packages** | `uv run --no-project ../installer/build_installer.py` (Setup exe + portable ZIP; `--zip-only` / `--setup-only`) |
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

- **Simple-mode downloads**: `DownloadService.RunAsync(url, savePath, MediaKind, …)` spawns `yt-dlp` via `System.Diagnostics.Process` with stdout/stderr piped for real-time progress parsing. Audio uses `-x --audio-format mp3`; video uses `DownloadService.BuildVideoFormat(PreferencesService.GetMaxHeight(), PreferencesService.GetMinFps())` merged to MP4 (capped at the user's max resolution; when a min framerate is set it prefers `fps>=N` but always falls back to ≤height variants). Progress is reported through an `Action<DownloadProgress>` callback; the finished file path comes from yt-dlp's `[destination]` line (see `DownloadService.ParseDestination`).
- **Library downloads**: `LibraryDownloadService.FetchEntriesAsync()` pre-fetches metadata (one entry for a single video, all entries for a playlist via `--flat-playlist`). The **queue** lives in `LibraryViewModel`: entries are pushed onto a `ConcurrentQueue<QueueJob>` (`EnqueueEntries`) and a single worker task (`QueueWorkerAsync`) pops them and calls `LibraryDownloadService.DownloadEntryAsync` one at a time, passing the job's `Kind` (the per-item format override) for both the `-f` args and the downloaded-file lookup. Each entry is registered in the `LibraryStore` before its download starts and finalized afterwards. A failed entry is marked `Failed` and the queue continues; cancellation stops the active job and drains the rest.
- **MP3 reuse for MP4**: `DownloadEntryAsync` checks `FindExistingMp3(folder, entryId)` when `kind == Video` and the app-managed ffmpeg exists; if found it downloads only the video stream (`BuildVideoOnlyFormat`, no audio/combined fallback — a combined stream would double the audio) and then `MergeVideoWithMp3Async` merges the existing MP3: `ffmpeg -y -i video -i mp3 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart <stem>.merging.mp4`, retrying with `-c:v libx264 -preset veryfast -crf 21` when the codec can't be copied into MP4 (VP9 — h264 and AV1 copy fine; the live test was an AV1 copy). The temp is moved/replaced to `<stem>.mp4` (same stem as the MP3) and the video-only file is deleted unless it already is the final file. Cancellation kills ffmpeg mid-merge and cleans the temp; any video-only failure falls back to the normal full download. The MP3 survives the merge and is only removed by the delete flow.
- **Working directory (critical)**: every child process (yt-dlp and ffmpeg) must pin its `WorkingDirectory`, and that path must be **validated** — never trust it blindly. Use `SafeWorkingDirectory.Resolve(preferred)` (`Services/SafeWorkingDirectory.cs`): it returns `preferred` if `IsUsable` (verified by a real `Directory.EnumerateFiles` in try/catch — a missing dir **or** an untrusted reparse point/junction both fail), else a verified `Path.GetTempPath()`, else `~/.videopiper/work` (created on demand). This exists because Python 3.12+ crashes with `WinError 448` (`ERROR_UNTRUSTED_REPARSE_POINT`) when resolving a path that descends through an untrusted mount point (e.g. nvm-windows' `C:\nvm4w\nodejs` junction). One subtlety: when `TEMP`/`TMP` are unset, `Path.GetTempPath()` silently returns the **process CWD** — so the temp fallback itself is validated, and if it too is unusable it falls to `~/.videopiper/work`. All spawn sites go through `Resolve`: `LibraryDownloadService.RunYtDlpAsync` (per-item target folder), `RunMergeAsync` (folder), `DefaultWorkingDirectory` (`Resolve(null)`), simple-mode `DownloadService` (save folder — the app has no `-P` there, so an unusable folder moves output to temp and the success card reports the real `[destination]`), `SearchService` probes (`Resolve(null)`), and `SystemService.RunAsync` (previously the one unpinned spawn).
- **Retry**: Three paths, all funneling into `EnqueueEntries` (whose existing retry branch resets the entry's status/logs and logs "Överför till kön igen.") — `RetryFailedAsync()` re-queues every `Failed` item (bulk, from the error card); `RetryQueueEntryAsync(entry)` re-queues a single failed queue entry (log-panel button). Both set `_isRetryingFailed` so the stale error banner is cleared once the re-queue lands.
- **Pop-out player**: `PlayerPopOutWindow` (Windows-only, `#if WINDOWS`) is a standalone `Window` hosting its own `MediaPlayerElement`. It exposes `ClosedChanged` (NOT `Closed` — that would hide the inherited `Window.Closed` and the handler would never fire) and `Configure(w, h)` (call after `Activate()` so the HWND exists). Fullscreen uses `AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen / Overlapped)` — `AppWindow.IsFullScreen` / `.Bounds` do not exist in WinUI 3 1.7. **The docked player is paused, not just hidden, while the pop-out is open**: a `PlayerIsPopOut` handler in `BuildPlayerDock` (`MainPage.cs`) reads `MediaPlayer.PlaybackSession.PlaybackState` (`Windows.Media.Playback.MediaPlaybackState`), calls `Pause()` on pop-out, and `Play()` on pop-out close only if it was playing when the pop-out opened — otherwise two pipelines would double the audio. `ClosePlayer` clears `PlayingItem` *before* closing the pop-out so the close-time resume cannot blip.
- **yt-dlp JSON parsing**: Centralized in `YtDlpJson.cs` (playlist vs single). Both download and search parse through it — don't add a second parser.
- **Search**: `SearchService.SearchAsync()` runs `ytsearchN:<query>` with `--flat-playlist -J` (one fast call, no downloads) and maps entries to `SearchResult`. The search is triggered only by the **Sök** button or **Enter** (`LibraryViewModel.RunSearchNowAsync`); typing into the box never starts a search — it only updates the query, invalidates previous results, and switches the main area to the search section once the query is real.
- **Tool Detection**: `SystemService.CheckToolsAsync()` resolves yt-dlp and ffmpeg from the **app's local `Tools/` directory only** (system PATH is intentionally not used — the app owns its binaries). Reuse it for any new tool invocation rather than re-implementing path lookup.
- **App-managed tools & updates**: `ToolUpdateService` checks the GitHub `releases/latest` API for the newest yt-dlp / ffmpeg release, compares it with the locally installed version (`IsNewerVersion`, first dotted number sequence), and downloads/extracts the update when one is available. `SystemService.EnsureToolsAsync()` installs any missing tools, then runs a **once-per-day background update check** gated by a UTC timestamp persisted in `tools.json` (`ToolUpdatesPrefs`). The **settings page** (`SettingsViewModel` + `BuildSettingsView`/`BuildToolCard` in `MainPage.cs`) shows installed/latest per tool with manual "Kontrollera uppdatering" / "Uppdatera" buttons (manual checks do not bump the daily-gate timestamp).
- **Logging (minimal, debug-only)**: `App.InitializeLogging()` (debug builds only, called at the top of `OnLaunched`) builds a `Microsoft.Extensions.Logging` console factory and wires the ambient `Log` facade (`VideoPiper/Log.cs`): `Log.For(typeof(T)).LogX(…)` resolves an `ILogger` for the category; with no factory (release builds, unit tests) it is a no-op `NullLogger`, so there is zero startup cost or output outside debug. Structured log points: download failure, tool update success/failure, index corrupt-warning + save, and item delete. No dedicated log files are written (the user asked for minimal footprint) — output goes to the console. The test project compiles `Log.cs` plus a global `using Microsoft.Extensions.Logging;` so the compiled-in services stay testable.
- **Worker-thread rule (critical)**: the library queue runs `DownloadEntryAsync` on a
  background worker. Never let a UI-bound object raise `PropertyChanged` off the UI
  thread — WinRT throws `COMException`, and a fire-and-forget worker task then **dies
  silently** (no crash, no log; the queue just freezes and the worker is absent from
  every thread stack in a dump). Three rules follow:
  - View-model mutations from the worker go through the private static `OnUi(...)`
    helper (`LibraryViewModel.cs`): post every observable-state change to the UI dispatcher.
  - `LibraryItem` model mutations are safe from any thread: `LibraryItem` routes
    `PropertyChanged` through a pluggable static `PostToUi(Action)` hook (wired to
    `App.PostToUiThread` in `App.OnLaunched`). Backing fields are still assigned
    synchronously, so `LibraryStore.SaveAsync()` always reads current state — only the
    notification is marshaled. The hook is pluggable (no hard `App` reference) because the
    test project compiles `Models/*.cs` standalone; leave it that way.
  - **`LibraryQueueEntry` is NOT safe from any thread** — unlike `LibraryItem` it is a plain
    `INotifyPropertyChanged` (no `PostToUi` hook). Every observable mutation from the worker
    (e.g. `queueEntry.Kind`, `queueEntry.Status`, `queueEntry.Percent`) MUST go through
    `OnUi(...)`. The per-item format feature once set `queueEntry.Kind = job.Kind` directly on
    the worker; a format change (MP3→MP4) raised `KindLabel` off the UI thread → `COMException`
    → the worker died before the download started (entry stuck "I kön", no child, no file). It
    is now set inside `OnUi(...)`.

### 5.2 Dependency Injection (Uno Hosting)

The app uses Uno Platform's **Hosting** feature (`<UnoFeatures>` in `VideoPiper/VideoPiper.csproj` is `CSharpMarkup; SkiaRenderer; Hosting;`) and follows the pattern from Uno's official `MyExtensionsApp.1` template: services are registered as singletons on an `IHost` built with `this.CreateBuilder(args)`, and view models receive their dependencies via **constructor injection**.

- **Host**: `App.Host` (`public static IHost` on `App`) is built in `App.OnLaunched`: `var builder = this.CreateBuilder(args).Configure(host => host.ConfigureServices((context, services) => { … })); MainWindow = builder.Window; …; Host = builder.Build();`. The app uses native `Frame.Navigate(typeof(MainPage))` (not Uno.Extensions Navigation), so per the docs' non-navigation path `MainPage` resolves the view models from the container: `App.Host.Services.GetRequiredService<MainViewModel>()` (and `LibraryViewModel` / `SettingsViewModel`). The feature emits `global using Uno.Extensions.Hosting;` (generated `GlobalUsings.g.cs`), which is where `CreateBuilder`/`IHost` come from.
- **Registration graph** (all singletons, interface → implementation, registered in `App.OnLaunched`):
  - `IToolUpdateService` → `ToolUpdateService` (no dependencies)
  - `ISystemService` → `SystemService` (depends on `IToolUpdateService`)
  - `IDownloadService` → `DownloadService` (depends on `ISystemService`)
  - `ILibraryDownloadService` → `LibraryDownloadService` (depends on `ISystemService`)
  - `ISearchService` → `SearchService` (depends on `ISystemService`)
  - `MainViewModel` (ISystemService, IDownloadService, IToolUpdateService); `LibraryViewModel` (ILibraryDownloadService, ISearchService); `SettingsViewModel` (IToolUpdateService, passed through to its `ToolSettingsModel` rows)
- **Interfaces live in the same file as their implementation** (top of each `Services/*.cs` file) so the test project's `<Compile>` include list is unchanged (it compiles the service files directly, with no project reference) and no new files are needed.
- **Cycle break**: `ToolUpdateService` only reaches `SystemService` through the two **static** members `ToolsDirectory` and `GetLocalVersionAsync`; the only instance edge is `SystemService` → `IToolUpdateService`. The container graph is therefore acyclic.
- **What stays static**: the pure/stateless helpers the test suite calls statically — `DownloadService.BuildVideoFormat` / `BuildVideoOnlyFormat` / `ParseDestination`, `ToolUpdateService.IsNewerVersion` / `TryParseVersion` / `FormatVersion` / `ApiBaseUrl` / `Http`, `SystemService.ToolsDirectory` / `GetLocalVersionAsync`, plus the `YtDlpJson` parser and the thin static `PreferencesService` utility. Don't instance-ify these "for DI" — the 35 pure-logic tests depend on them being static.
- **Not DI-registered**: `LibraryStore` (a per-root repository — `new LibraryStore(root)` in `LibraryViewModel` when the user picks a library root), `ShellViewModel` (only uses static `PreferencesService`, still `new`'d by `MainPage`), and the static utilities above.
- **Unit tests**: the test project compiles the services directly, so tests build the real dependency chain without a container (`new SystemService(new ToolUpdateService())`, `new SearchService(system)`, …). New VM tests should fake the `I*` interfaces instead.
- **Do NOT enable `.UseLogging(...)`** on the host: the app keeps its own ambient `Log` facade (`App.InitializeLogging`, debug builds only, see §5.1) and Uno's logging would duplicate it.

### 5.3 Frontend — WinUI 3 with C# Markup & MVVM
- **C# Markup DSL**: UI is authored declaratively in C# using Uno Platform C# Markup (`Uno.Extensions.Markup`) in `MainPage.cs`, providing type-safe markup, fluent styling, and direct refactoring support. Shared row `DataTemplate`s live in `App.xaml` resources.
- **App shell & mode switcher**: `MainPage` is a single `Page` hosting an **app bar** (`BuildAppBar`, bound to `ShellViewModel`) above a view host that shows the **Snabbnedladdning** view (`BuildSimpleView` → `MainViewModel`), the **Bibliotek** view (`BuildLibraryView` → `LibraryViewModel`), or the **Inställningar** page (`BuildSettingsView` → `SettingsViewModel`). The app bar's two-segment mode switcher drives `ShellViewModel.SetMode`; the **gear button** (`ToggleSettingsCommand`) toggles `ShellViewModel.SettingsOpen`; `MainPage.SyncModeViews` reacts to `IsSimple`/`IsLibrary`/`SettingsOpen` (settings overlays both mode views). Mode persistence lives in `ShellViewModel` via `PreferencesService.SetAppMode`.
- **Library layout (YouTube-style)**: the library view is a left sidebar (`BuildLibrarySidebar`) with navigation (YouTube-sökning, Alla filer, Ljudfiler, Videofiler, **Misslyckades**) + counts and a bottom folder box, beside a **full-width main content area** (`BuildLibraryMainContent`). The main area is a vertical two-row grid: row 0 (star) is the **browse hero** — the search row (`BuildSearchBoxRow` — search box + Sök/Ladda ner + global MP3/MP4 toggle + Klistra in) over `BuildCollectionHeader`/`BuildSearchHeader` + the collection list / search results (switched by `IsSearchActive`/`IsCollectionActive`); row 1 (auto) is a bottom band stacking the player dock `BuildPlayerDock` → selected-item bar `BuildSelectedItemBar` → status cards → `BuildQueuePanel` (each self-collapses when idle). **Search results**: `searchList.ItemClick` calls `vm.SelectSearchResult(result)` which sets `SelectedSearchResult` — **no download**. The list shows when `ShowSearchList` (`HasSearchResults && !HasSelectedSearchResult`); otherwise `BuildSearchDetailPage(vm)` (the video info page, scrollable) is shown. **Video info page**: title, "Av {uploader} · {duration}" (`SearchResult.DetailText`), the full description (fetched in the background by `FetchSearchDetailAsync` via `ISearchService.FetchDetailJsonAsync` → `YtDlpJson.GetDescription`, with a "Hämtar detaljer…" ring and a `SelectedSearchDetailError` box), and two download buttons — **Ladda ner ljud** (`DownloadSearchResultAudioCommand`, the accent button — one primary action per view) and **Ladda ner video** (`DownloadSearchResultVideoCommand`, neutral) — bound to VM command properties (not inline `RelayCommand`s) so their enabled state tracks `CanDownloadSearchResult` via `RefreshCanExecute()`. A result already in the library (or queued) shows "Den här videon finns redan i biblioteket." with the buttons disabled.
- **Download queue panel**: `BuildQueuePanel(vm)` renders a "Laddningskön" card — one
  row per job (`QueueEntryTemplate` in `App.xaml`: status glyph, title, status text, thin
  progress bar, MP3/MP4 badge, colored via `StatusToBrushConverter`) plus a per-item log
  panel (`BuildQueueLogPanel`) that opens when a row is clicked and streams that job's
  live `yt-dlp` output (auto-scrolling). The card is **hidden by default** — it is shown
  while `vm.QueuePanelOpen` (toggled by the app bar queue icon; auto-closes when every job
  has finished unless a row's log is selected, and never shows with an empty queue). The
  compact progress card (`BuildLibraryProgressCard`, visible while `IsQueueActive`) covers
  active-download feedback while the panel is closed. Row selection is wired via
  `ListView.ItemClick` (the Uno XAML gotcha above).
- **Player dock & pop-out**: `BuildPlayerDock(vm)` shows the docked player (title, a
  `MediaPlayerElement` with transport controls, a seek bar). It is `AutoPlay(false)`:
  selecting an item loads it (`PlayingItem`) **without** starting playback. The docked
  surface visibility is bound to `IsDockedPlayerVisible` (playing item present and pop-out
  closed). **Missing-file prompt**: when `SelectedFileMissing` (the selected item is
  `Complete` but its file is gone) the docked player is cleared/hidden and a prompt takes its
  place — a glyph + "Filen saknas på disk" + a **Ladda ner** button (`DownloadSelectedCommand`)
  that re-downloads the item; on re-download completion the item reloads into the player. The
  header has a **pop-out** button (`PopOutPlayerCommand`) that opens `PlayerPopOutWindow`; while
  pop-out is open the docked `MediaPlayerElement` is hidden (not destroyed) via
  `PlayerIsPopOut` so playback position/controls persist, and its `MediaPlayer` is paused
  (resumed on close if it was playing) so only one pipeline plays. Both header buttons live in a
  horizontal `StackPanel` (a shared `Grid(column: n)` would make them overlap).
- **Selected-item bar & per-item format**: `BuildSelectedItemBar(vm)` sits under the player
  (visible when `SelectedItem` is set): the item title, a **MP3** and **MP4** button (each a
  direct inline `new RelayCommand(() => vm.DownloadSelectedAsAsync(MediaKind.X))`), **Visa**
  (open the file's folder — labeled "Öppna mapp" to match the simple view's success card),
  and **Ta bort** (delete — confirmation dialog in `DeleteFiles` mode). The MP3/MP4 buttons let **any item be
  downloaded as either format regardless of the global toggle** — `DownloadSelectedAsAsync(kind)`
  builds a `MediaEntry` from the item and enqueues it with `kindOverride: kind`, so the worker
  downloads and registers the file as that format without ever flipping the persisted global
  `Kind`. **Gotcha:** these per-item buttons must stay inline `RelayCommand`s — `LibraryItem`
  doesn't expose the id, so a shared `ParameterizedRelayCommand` bound through the item's
  command properties can't carry both the item and the fixed kind cleanly.
- **Design tokens**: state colors (success/warning/error) are centralized as `Vp*` brushes in `App.xaml` (`VpSuccessBrush`, `VpWarningBgBrush`, `VpErrorBorderBrush`, …) so both themes stay coherent. Prefer these over hardcoded `ColorHelper.FromArgb` where practical.
- **MVVM Pattern**: Each view binds to its own view model via fluent `.Binding(...)` expressions. Commands (`ICommand` via `RelayCommand` / `ParameterizedRelayCommand`) handle all user interactions. View models implement `INotifyPropertyChanged` with a private `Set(ref field, value)` helper and refresh command `CanExecute` when relevant state changes. **Gotcha (bit us twice):** a `Button` bound to `.Command(...)` only updates its enabled state on `CanExecuteChanged`, and `CanExecuteChanged` is only raised by an explicit `command.RefreshCanExecute()`. If the gating state (e.g. `PlayingItem`, `FailedCount`, `IsDownloading`) changes without refreshing the command, the button stays disabled and silently ignores clicks — always refresh every gated command in the setter of the state that feeds its `CanExecute`.
- **Value Converters & Inlines**: Property builders support inline lambdas (e.g., `.Convert(...)`) as well as standalone `IValueConverter` implementations. Inside `.Convert(...)` lambdas, do **not** call `ThemeResource.Get<Brush>(…)` (it's a markup helper that returns a builder, not a `Brush`) — use a plain `new SolidColorBrush(...)` or the `BrushFromResource(key, fallback)` runtime lookup in `MainPage`.
- **Uno XAML gotchas** (hit during development): `TabView<T>` generic parameters and `TabView.Items` are not supported. The XAML compiler rejects `RelativeSource AncestorType=…`; for a command inside an ItemTemplate that must reach the page/VM, wire `ListView.ItemClick` in code instead of an ancestor binding. **Cross-VM markup bindings fail silently:** `x.Binding(() => otherVm.Member)` resolves its path against the nearest ancestor **DataContext**, not against the captured variable — if the DataContext is a different view model (e.g. the app bar Grid has `.DataContext(shell)`), the binding resolves to nothing and a `.Command(...)` bound that way is silently `null`: the button renders, looks enabled, and does nothing when invoked (UIA `Invoke` included). Bind only to members of the DataContext view model; for another VM's command, assign it **directly** (`.Command(otherVm.SomeCommand)`) — command objects are stable singletons, so a direct reference is safe (the mode-switcher buttons and the app bar queue icon both do this).
- **Theme Toggle**: Built-in dark/light theme switching via `App.SetTheme()` and persisted preference via `PreferencesService`.
- **Window size is remembered**: the window is resized to the last size via `PreferencesService.GetWindowSize()` at startup (fallback 1060×740) and saved on `AppWindow.Closing` via `SetWindowSize` (clamped to 400×300–4000×4000 so a collapsed window never poisons the next launch). Windows-only (`#if WINDOWS` — `AppWindow` is WinAppSDK-only); stored in `preferences.json`.

### 5.4 Preferences & Persistence
User settings are stored as JSON files in the app's local data folder (`ApplicationData.Current.LocalFolder.Path`):
- `preferences.json` — save path, app mode (Simple/Library), library root, output format (`MediaKind`), **max resolution** (`GetMaxHeight`, default 1080), **min framerate** (`GetMinFps`, default 60; 0 = no floor), **delete mode** (`GetDeleteMode`: `DeleteFiles` default / `KeepFiles`), and **window size** (`GetWindowSize`/`SetWindowSize`, null = default 1060×740)
- `theme.json` — current theme ("light" or "dark")

The **library index** is separate from preferences: it lives inside the user-chosen library root at `<root>/.videopiper/library.json`, written atomically (temp file + rename) by `LibraryStore`, and is the single source of truth for item status. Media files are organized as `<root>/<Channel>/<Playlist>/` via `ResolveTargetFolder`. `ResolveTargetFolder` sanitizes channel/playlist names with a **platform-independent** invalid-character set (`Path.GetInvalidFileNameChars()` unioned with the Windows set `< > : " | ? *`) so names that are legal on Linux are still safe on Windows; a missing/empty channel falls back to a `Misc` folder.

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

- **Design skill**: use the `designer` skill (`.agents/skills/designer/SKILL.md`) for any UI work: it codifies this project's design system, the Fluent 2 / WinUI 3 conventions, and the UI/UX behavior rules (states, feedback, destructive actions, scannability, accessibility).

1. **Build via `vpp` (preferred)**: Use the `vpp` CLI (`tools/vpp/`) — `vpp doctor`, `vpp build`, `vpp run`, `vpp publish`. It finds `VideoPiper.sln` from anywhere in the repo and auto-selects the correct target per OS, so you don't have to set `Cwd: video-piper` or remember TFMs. If the CLI isn't installed, fall back to running `dotnet build/run/publish` with `Cwd: video-piper`.
2. **Multi-Targeting**: The project uses conditional `<TargetFrameworks>` (WinAppSDK on Windows, `net10.0` Skia desktop elsewhere). Do not revert to a singular `<TargetFramework>`. When adding targets or OS-specific config, keep the existing MSBuild conditions intact.
3. **Platform-Conditional Code**: Use Uno's predefined symbols (`WINDOWS`, `__WASM__`, `HAS_UNO`, etc.) for platform-specific APIs. Windows-only APIs such as `Windows.Storage.Pickers`, `WinRT.Interop`, and in-app `MediaElement` playback must be wrapped in `#if WINDOWS` so the `net10.0` Skia target still compiles on Linux/macOS — see `FolderPickerService.cs` and `MainPage.cs` (player card) for the pattern.
4. **Clean Code**: Follow C# conventions. Use `async`/`await` properly, avoid blocking calls on UI thread, and prefer `ICommand` for button bindings.
5. **Swedish Strings**: Preserve Swedish localization for all user-facing strings. Do not introduce English-only strings without providing Swedish translations.
6. **Library changes**: When touching library behavior, keep the index as the single source of truth — mutate in memory then call `LibraryStore.SaveAsync()`. Route new downloads through the **queue** in `LibraryViewModel` (`EnqueueEntries` → `QueueWorkerAsync` → `LibraryDownloadService.DownloadEntryAsync`); don't spawn parallel downloads or call yt-dlp directly from the UI. Parse any yt-dlp `-J` output via `YtDlpJson` (never a second ad-hoc parser). Keep per-item progress/status updates on the UI thread via the view model's dispatcher helper.
7. **Testing**: Platform-neutral logic (models, `YtDlpJson`, `DownloadService.ParseDestination`, `LibraryStore`, `SearchService`) has MSTest coverage in `video-piper/tests/VideoPiper.Tests`. The test project compiles those sources directly (no project reference) — add any newly-testable service file to its `<Compile>` list. Run with `dotnet test` from `video-piper/`. Keep new pure-logic code testable this way.
8. **No Tauri/Deno scaffolding anymore**: the earlier experiments' artifacts (`video-piper/src-tauri/`, `video-piper/dist/`, `node_modules/`, the root Deno-era `.vscode/`) were removed. If similar frontend leftovers ever reappear, treat them as junk — never build, run, or "fix" them; the app is entirely the Uno/C# project.

