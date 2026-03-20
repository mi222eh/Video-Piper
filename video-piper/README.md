# Video Piper

A desktop app for downloading YouTube videos as MP3 files, built with Tauri, React, and TypeScript.

## Features

- Download YouTube videos as MP3 files.
- Choose a save location that is remembered between sessions.
- **Update yt-dlp** – keeps the underlying `yt-dlp` tool up to date via Windows Package Manager (`winget`).

## Update yt-dlp button

The **Update yt-dlp** button runs `winget upgrade yt-dlp` to update the `yt-dlp` binary used internally by the app.

### Prerequisites

- **Windows** – the button uses Windows Package Manager (`winget`), which ships with Windows 10 (1809+) and Windows 11.
- **winget** must be available in the system `PATH`. If it is not installed, the update will fail with an appropriate error message in the UI.
- `yt-dlp` should already be installed via winget (i.e. `winget install yt-dlp`) for the upgrade command to succeed.

### Behaviour

1. Click **Update yt-dlp** in the main window.
2. The button is disabled and shows "Updating yt-dlp…" while the command runs.
3. On success the status message changes to "yt-dlp updated successfully!".
4. On failure the status message shows the exit code and any captured output so you can diagnose the problem.

## Development

### Recommended IDE Setup

- [VS Code](https://code.visualstudio.com/) + [Tauri](https://marketplace.visualstudio.com/items?itemName=tauri-apps.tauri-vscode) + [rust-analyzer](https://marketplace.visualstudio.com/items?itemName=rust-lang.rust-analyzer)

### Running locally

```bash
cd video-piper
pnpm install
pnpm tauri dev
```
