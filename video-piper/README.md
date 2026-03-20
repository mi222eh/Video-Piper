# Tauri + React + Typescript

This template should help get you started developing with Tauri, React and Typescript in Vite.

## Recommended IDE Setup

- [VS Code](https://code.visualstudio.com/) + [Tauri](https://marketplace.visualstudio.com/items?itemName=tauri-apps.tauri-vscode) + [rust-analyzer](https://marketplace.visualstudio.com/items?itemName=rust-lang.rust-analyzer)

## Features

### Download MP3
Enter a YouTube link, choose a save location, and click **Ladda ner MP3** to download the audio.

### Update yt-dlp
Click the **update** button to upgrade `yt-dlp` to the latest version via Windows Package Manager (winget).

**Prerequisites:**
- Windows with [winget](https://learn.microsoft.com/en-us/windows/package-manager/winget/) installed (included by default on Windows 10/11).
- `yt-dlp` must have been installed via winget (`winget install yt-dlp`).

The button shows "Updating..." while the upgrade runs and displays a success or failure message when complete.
