# vpp — Video-Piper CLI

A small, dependency-free Python command-line project manager for the Video-Piper
Uno/.NET desktop app. It wraps the `.NET` build/run/publish workflow and knows
about Uno's conditional multi-targeting, so you don't have to remember the exact
target frameworks or paths.

## Install

```bash
cd tools/vpp
python3 -m pip install -e .        # adds `vpp` to your PATH
# or with uv:
uv pip install -e .
```

Requires Python 3.9+ and the `.NET 10 SDK`. No other dependencies — the CLI is
standard-library only and shells out to `dotnet`.

## Commands

| Command | What it does |
|:---|:---|
| `vpp build` | Build the project, auto-selecting the right target for your OS (WinAppSDK on Windows, Skia desktop elsewhere). |
| `vpp build --no-incremental` | Force a full rebuild. |
| `vpp build --clean` | `dotnet clean` first, then build. |
| `vpp run` | Build and launch the app. |
| `vpp publish` | Publish a standalone executable (defaults to `win-x64`, Release). |
| `vpp doctor` | Check prerequisites: `.NET` SDK, `yt-dlp`, `ffmpeg`, and that it can find the project. |

Useful flags on `build`/`run`: `-c/--configuration` (default `Debug`) and
`-f/--framework` to override the auto-selected target framework.

## Examples

```bash
vpp doctor              # is my environment ready?
vpp build               # Debug build for this OS
vpp build -c Release    # Release build
vpp run                 # launch the app
vpp publish             # -> <project>/publish (win-x64, framework-dependent)
vpp publish --self-contained   # self-contained win-x64 bundle
```

## How it finds the project

`vpp` walks up from your current directory looking for `VideoPiper.sln`, so you
can run it from anywhere inside the repository. If it can't find the solution,
it tells you to run it from within the repo.
