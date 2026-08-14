# Video-Piper (Deno Desktop + React + TypeScript)

Video-Piper is a desktop application for downloading and converting video/audio to MP3 using **Deno Desktop** and `yt-dlp`.

---

## 🚀 Quickstart & Commands

Run all commands from the `video-piper/` directory:

| Task | Command | Description |
| :--- | :--- | :--- |
| **Build Frontend** | `deno task build` | Builds React SPA assets with Vite to `dist/` |
| **Run Desktop App** | `deno task desktop` | Launches the app in a native OS desktop window |
| **Desktop Dev (HMR)** | `deno task desktop:hmr` | Launches desktop with hot reload |
| **Run Local Server** | `deno task serve` | Runs HTTP server backend on `http://localhost:1420` |
| **Compile Executable** | `deno task compile` | Packages self-contained desktop binary to `bin/` |

---

## 🛠️ Architecture

- **Desktop Framework**: Built-in [`deno desktop`](https://docs.deno.com/runtime/desktop/)
- **Backend & Serving**: Deno HTTP server with [`@std/http/file-server`](https://jsr.io/@std/http) serving the Vite build from `dist/`
- **Native Windowing**: [`Deno.BrowserWindow`](https://docs.deno.com/runtime/desktop/windows/)
- **Frontend**: React 19, Tailwind CSS v4, shadcn/ui, Lucide Icons
- **Media Engine**: `yt-dlp` & `ffmpeg`

---

## 💻 Recommended IDE Setup

- [VS Code](https://code.visualstudio.com/) + [Deno Extension](https://marketplace.visualstudio.com/items?itemName=denoland.vscode-deno) + [Tailwind CSS IntelliSense](https://marketplace.visualstudio.com/items?itemName=bradlc.vscode-tailwindcss)
