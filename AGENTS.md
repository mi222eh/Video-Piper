# AGENTS.md - Video-Piper Developer & Agent Guide

Welcome to the **Video-Piper** repository. This document provides essential project context, architectural guidelines, conventions, and operational workflows for AI agents and human contributors working on this codebase.

---

## 1. Project Overview

**Video-Piper** is a lightweight cross-platform desktop application built with **Tauri v2**, **React 19**, and **Rust**. Its primary purpose is to simplify downloading and converting online audio/video (such as YouTube videos to MP3) using [`yt-dlp`](https://github.com/yt-dlp/yt-dlp) as the underlying engine.

### Key Capabilities
- **Direct YouTube to MP3 download**: Spawns `yt-dlp` subprocesses directly through Tauri's shell plugin.
- **System Integrations**: Native folder picker dialogs, system clipboard integration, and external link handling.
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
    ├── pnpm-lock.yaml         # PNPM lockfile (primary package manager)
    ├── vite.config.ts         # Vite configuration (React Compiler, Tailwind, Path aliases)
    ├── tsconfig.json          # TypeScript configuration
    ├── components.json        # shadcn/ui configuration
    ├── index.html             # HTML entry point
    ├── public/                # Static public assets
    ├── src/                   # React frontend source code
    │   ├── main.tsx           # Application bootstrap
    │   ├── App.tsx            # Root component & state management
    │   ├── App.css            # Tailwind v4 theme, OKLCH variables & global styles
    │   ├── components/
    │   │   └── ui/            # shadcn/ui components (50+ accessible primitives)
    │   ├── hooks/             # Custom React hooks (e.g., use-mobile)
    │   └── lib/               # Utility functions (cn helper, class merging)
    └── src-tauri/             # Rust & Tauri backend
        ├── Cargo.toml         # Rust crate configuration & dependencies
        ├── tauri.conf.json    # Tauri configuration (window size, identifier, build commands)
        ├── capabilities/      # Tauri v2 security capabilities and permission grants
        │   └── default.json   # Main window permissions (shell, dialog, clipboard, opener)
        └── src/
            ├── main.rs        # Tauri entrypoint
            └── lib.rs         # Tauri application builder & command registration
```

---

## 3. Technology Stack & Tooling

| Component | Technology | Version / Details |
| :--- | :--- | :--- |
| **Desktop Framework** | [Tauri v2](https://v2.tauri.app/) | Core v2.11+ with official v2 plugins |
| **Backend Language** | [Rust](https://www.rust-lang.org/) | 2021 Edition (rustc 1.97+) |
| **Frontend Framework**| [React](https://react.dev/) | 19.x with `babel-plugin-react-compiler` |
| **Language** | [TypeScript](https://www.typescriptlang.org/) | 7.x with strict type checking |
| **Bundler & Dev Server**| [Vite](https://vitejs.dev/) | 8.x |
| **Styling** | [Tailwind CSS](https://tailwindcss.com/) | v4.x (CSS `@theme` and OKLCH color spaces) |
| **UI Components** | [shadcn/ui](https://ui.shadcn.com/) | New York style with Radix UI primitives |
| **Icons** | [Lucide React](https://lucide.dev/) | Icon library |
| **Package Manager** | `pnpm` | Standard package manager (v11+) |

---

## 4. Development Workflow & Commands

> **Important**: Always run frontend and package manager commands inside the `video-piper/` directory.

### Prerequisites
- **Node.js**: >= 20.x
- **pnpm**: >= 9.x
- **Rust & Cargo**: Latest stable toolchain
- **System Dependencies**: Standard Tauri prerequisites (e.g., `libwebkit2gtk`, `build-essential` on Linux)
- **yt-dlp**: Must be available on system PATH for download functionality

### Common Commands

#### Deno Workflow (Branch `feature/deno-desktop`)
| Task | Command (from `video-piper/`) |
| :--- | :--- |
| **Start Deno Server** | `deno task serve` |
| **Launch Desktop App** | `deno task desktop` |
| **Build Frontend** | `deno task build` (or `pnpm build`) |
| **Compile Standalone Binary** | `deno task compile` |

#### Legacy Tauri Workflow
| Task | Command (from `video-piper/`) |
| :--- | :--- |
| **Install Dependencies** | `pnpm install` |
| **Start Tauri Dev (Frontend + Rust)** | `pnpm tauri dev` |
| **Start Frontend Dev Server Only** | `pnpm dev` |
| **Typecheck & Build Frontend** | `pnpm build` |
| **Build Desktop App Bundle** | `pnpm tauri build` |
| **Check Rust Backend** | `cargo check --manifest-path src-tauri/Cargo.toml` |

---

## 5. Architecture & Code Conventions

### 5.1 Tauri v2 Plugin & Permission Architecture
- **Capabilities**: Tauri v2 uses fine-grained permissions defined in `src-tauri/capabilities/default.json`.
- **Subprocess Execution**: Executing `yt-dlp` requires explicit grant in `capabilities/default.json`:
  - `shell:allow-spawn` and `shell:allow-execute` configured for command `"yt" -> "yt-dlp"`.
- **Adding Permissions**: When adding new Tauri plugins or IPC commands, always ensure both:
  1. Plugin registration in `src-tauri/src/lib.rs` (or `Cargo.toml`).
  2. Permission definitions in `src-tauri/capabilities/default.json`.

### 5.2 Frontend & React 19 Guidelines
- **Path Aliases**: Use `@/` to import from `src/` (e.g., `@/components/ui/button`, `@/lib/utils`).
- **React Compiler**: The project utilizes `babel-plugin-react-compiler`. Avoid manual `useMemo`/`useCallback` unless necessary, and follow React rules (pure render functions, immutability) so the compiler can optimize effectively.
- **Styling**:
  - Keep design tokens in OKLCH format inside `src/App.css`.
  - Use `cn(...)` from `@/lib/utils` for conditional class joining and tailwind merge.
  - Follow the existing shadcn/ui structure when creating or modifying UI components.
- **Language / Localization**: Current user-facing strings are in Swedish (e.g. *"Youtube Länk"*, *"Spara till"*, *"Ladda ner MP3"*). If introducing internationalization (i18n), preserve Swedish support or make it configurable.

### 5.3 Error Handling & Process Management
- **Async Execution**: When executing long-running external processes like `yt-dlp`:
  - Provide proper loading / progress state to the user.
  - Listen to `stdout`, `stderr`, and `close` events via `@tauri-apps/plugin-shell`.
  - Handle rejection gracefully without leaving the UI stuck in loading state.

---

## 6. Guidelines for AI Agents

1. **Working Directory Awareness**: Ensure commands like `pnpm install`, `pnpm build`, or `pnpm tauri dev` are executed with `Cwd: video-piper`.
2. **Lockfile Integrity**: Use `pnpm` (not `npm` or `yarn`) to avoid creating conflicting lockfiles.
3. **Preserve Capabilities**: Do not modify `src-tauri/capabilities/default.json` in a way that revokes required permissions for `yt-dlp`, dialogs, or clipboard access.
4. **Clean Code**: Follow TypeScript strict mode. Ensure type safety on Tauri IPC responses and plugin calls.
