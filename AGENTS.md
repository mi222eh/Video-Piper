# AGENTS.md - Video-Piper Developer & Agent Guide

Welcome to the **Video-Piper** repository. This document provides essential project context, architectural guidelines, conventions, and operational workflows for AI agents and human contributors working on this codebase.

---

## 1. Project Overview

**Video-Piper** is a lightweight cross-platform desktop application built with **Deno Desktop**, **React 19**, and **TypeScript**. Its primary purpose is to simplify downloading and converting online audio/video (such as YouTube videos to MP3) using [`yt-dlp`](https://github.com/yt-dlp/yt-dlp) and `ffmpeg`.

### Key Capabilities
- **Direct YouTube to MP3 download**: Spawns `yt-dlp` subprocesses directly through Deno's native `Deno.Command` API with real-time SSE progress streaming.
- **System Integrations**: Native folder picker dialogs (PowerShell/zenity/osascript), clipboard integration, and external link handling.
- **Modern UI**: Built with React 19, Tailwind CSS v4, and shadcn/ui components with dynamic dark/light mode support.

---

## 2. Repository Structure

The repository contains the application source code in the `video-piper/` subfolder:

```text
Video-Piper/
├── AGENTS.md                  # This guide
├── Video Piper Icon.png       # Branding & Icon asset
├── app-icon.png               # App icon
├── app-splashscreen.png       # Splash screen asset
└── video-piper/               # Main application root
    ├── package.json           # Frontend dependencies & scripts
    ├── pnpm-lock.yaml         # PNPM lockfile (primary frontend package manager)
    ├── deno.json              # Deno workspace & desktop configuration
    ├── server.ts              # Deno desktop entrypoint & HTTP/SSE backend server
    ├── vite.config.ts         # Vite configuration (React Compiler, Tailwind, Path aliases)
    ├── tsconfig.json          # TypeScript configuration
    ├── components.json        # shadcn/ui configuration
    ├── index.html             # HTML entry point
    ├── public/                # Static public assets
    └── src/                   # React frontend source code
        ├── main.tsx           # Application bootstrap
        ├── App.tsx            # Root component & state management
        ├── App.css            # Tailwind v4 theme, OKLCH variables & global styles
        ├── components/
        │   └── ui/            # shadcn/ui components (50+ accessible primitives)
        ├── hooks/             # Custom React hooks (e.g., use-mobile)
        └── lib/               # Utility functions (cn helper, class merging)
```

---

## 3. Technology Stack & Tooling

| Component | Technology | Version / Details |
| :--- | :--- | :--- |
| **Desktop Framework** | [Deno Desktop](https://docs.deno.com/runtime/desktop/) | Built-in native windowing via `Deno.BrowserWindow` |
| **Backend Runtime** | [Deno](https://deno.com/) | v2.x with standard library (`@std/http`, `@std/path`) |
| **Frontend Framework**| [React](https://react.dev/) | 19.x with `babel-plugin-react-compiler` |
| **Language** | [TypeScript](https://www.typescriptlang.org/) | 7.x with strict type checking |
| **Bundler & Dev Server**| [Vite](https://vitejs.dev/) | 8.x |
| **Styling** | [Tailwind CSS](https://tailwindcss.com/) | v4.x (CSS `@theme` and OKLCH color spaces) |
| **UI Components** | [shadcn/ui](https://ui.shadcn.com/) | New York style with Radix UI primitives |
| **Icons** | [Lucide React](https://lucide.dev/) | Icon library |
| **Package Manager** | `pnpm` / `deno` | PNPM for frontend npm dependencies, Deno for server |

---

## 4. Development Workflow & Commands

> **Important**: Always run commands inside the `video-piper/` directory.

### Prerequisites
- **Deno**: >= 2.0
- **Node.js**: >= 20.x & **pnpm**: >= 9.x (for frontend build)
- **yt-dlp & ffmpeg**: Available on system PATH for download functionality

### Common Commands

| Task | Command (from `video-piper/`) |
| :--- | :--- |
| **Start Deno Server** | `deno task serve` |
| **Launch Desktop App** | `deno task desktop` |
| **Launch Desktop with HMR** | `deno task desktop:hmr` |
| **Build Frontend** | `deno task build` (or `pnpm build`) |
| **Compile Standalone Binary** | `deno task compile` |
| **Typecheck Backend** | `deno check server.ts` |

---

## 5. Architecture & Code Conventions

### 5.1 Deno Desktop Backend Architecture
- **Window Management**: `server.ts` checks for `Deno.BrowserWindow` to configure the desktop window dimensions and title when launched under `deno desktop`.
- **HTTP / SSE APIs**:
  - `GET /api/health`: Validates presence of `yt-dlp` and `ffmpeg`.
  - `POST /api/browse`: Spawns native OS folder selection dialogs (PowerShell on Windows, Zenity/KDialog on Linux, AppleScript on macOS).
  - `GET /api/download?url=...&savePath=...`: Streams Server-Sent Events (SSE) providing live percentage, download speed, and ETA directly parsed from `yt-dlp` output.
- **Static File Serving**: Serves the compiled React SPA from `dist/` with fallback to `index.html`.

### 5.2 Frontend & React 19 Guidelines
- **Path Aliases**: Use `@/` to import from `src/` (e.g., `@/components/ui/button`, `@/lib/utils`).
- **React Compiler**: The project utilizes `babel-plugin-react-compiler`. Avoid manual `useMemo`/`useCallback` unless necessary, and follow React rules (pure render functions, immutability) so the compiler can optimize effectively.
- **Styling**:
  - Keep design tokens in OKLCH format inside `src/App.css`.
  - Use `cn(...)` from `@/lib/utils` for conditional class joining and tailwind merge.
  - Follow the existing shadcn/ui structure when creating or modifying UI components.
- **Language / Localization**: Current user-facing strings are in Swedish (e.g. *"Youtube Länk"*, *"Spara till"*, *"Ladda ner MP3"*). If introducing internationalization (i18n), preserve Swedish support or make it configurable.

---

## 6. Guidelines for AI Agents

1. **Working Directory Awareness**: Ensure commands like `pnpm install`, `pnpm build`, `deno task desktop`, or `deno task compile` are executed with `Cwd: video-piper`.
2. **Lockfile Integrity**: Use `pnpm` (not `npm` or `yarn`) to avoid creating conflicting lockfiles.
3. **Clean Code**: Follow TypeScript strict mode. Ensure type safety on HTTP endpoints and SSE streams.
