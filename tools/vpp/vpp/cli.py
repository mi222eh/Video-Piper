"""Command-line project manager for Video-Piper.

Wraps the .NET build/run/publish workflow so contributors don't have to remember
Uno's conditional multi-targeting or the exact paths. Shells out to `dotnet` and,
for `doctor`, checks for yt-dlp/ffmpeg. Stdlib-only.
"""

from __future__ import annotations

import argparse
import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path

try:
    from vpp import __version__
except ImportError:
    try:
        from . import __version__
    except ImportError:
        __version__ = "0.1.0"

# TFM per host OS, mirroring the conditional <TargetFrameworks> in VideoPiper.csproj.
WINDOWS_TFM = "net10.0-windows10.0.26100"
DESKTOP_TFM = "net10.0"  # Skia desktop target (Windows/Linux/macOS)


# --------------------------------------------------------------------------- #
# Project root discovery
# --------------------------------------------------------------------------- #
def find_project_root(start: Path | None = None) -> Path:
    """Locate the folder containing VideoPiper.sln.

    The solution sits at ``video-piper/VideoPiper.sln`` in this repo, so we check
    each ancestor directory both directly and under a ``video-piper/`` subfolder.
    This works whether you run ``vpp`` from the repo root or from inside it.
    """
    cur = (start or Path.cwd()).resolve()
    for candidate in [cur, *cur.parents]:
        if (candidate / "VideoPiper.sln").is_file():
            return candidate
        if (candidate / "video-piper" / "VideoPiper.sln").is_file():
            return candidate / "video-piper"
    raise SystemExit(
        "error: could not find VideoPiper.sln from the current directory.\n"
        "       Run `vpp` from inside the repository (or a subfolder of it)."
    )


def default_tfm() -> str:
    """The TFM that builds on this host (WinAppSDK only builds on Windows)."""
    return WINDOWS_TFM if platform.system() == "Windows" else DESKTOP_TFM


# --------------------------------------------------------------------------- #
# dotnet helpers
# --------------------------------------------------------------------------- #
def require_dotnet() -> str:
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit(
            "error: 'dotnet' was not found on PATH.\n"
            "       Install the .NET 10 SDK: https://dotnet.microsoft.com/download"
        )
    return dotnet


def run_dotnet(args: list[str], cwd: Path) -> int:
    """Run a dotnet command, streaming output live. Returns the exit code."""
    cmd = [require_dotnet(), *args]
    print(f"$ {' '.join(cmd)}", file=sys.stderr)
    try:
        return subprocess.call(cmd, cwd=str(cwd))
    except KeyboardInterrupt:
        print("\ninterrupted", file=sys.stderr)
        return 130


# --------------------------------------------------------------------------- #
# Commands
# --------------------------------------------------------------------------- #
def cmd_build(args: argparse.Namespace) -> int:
    root = find_project_root()
    tfm = args.framework or default_tfm()

    if args.clean:
        rc = run_dotnet(["clean", "VideoPiper.sln", "-v", "quiet"], root)
        if rc != 0:
            return rc

    cmd = ["build", "VideoPiper.sln", "--framework", tfm, "--configuration", args.configuration]
    if not args.incremental:
        cmd.append("--no-incremental")
    return run_dotnet(cmd, root)


def cmd_run(args: argparse.Namespace) -> int:
    root = find_project_root()
    tfm = args.framework or default_tfm()
    cmd = [
        "run", "--project", "VideoPiper/VideoPiper.csproj",
        "--framework", tfm, "--configuration", args.configuration,
    ]
    return run_dotnet(cmd, root)


def cmd_publish(args: argparse.Namespace) -> int:
    root = find_project_root()
    out = Path(args.output).resolve() if args.output else root / "publish"
    tfm = args.framework or WINDOWS_TFM  # standalone exe publish targets Windows
    cmd = [
        "publish", "VideoPiper/VideoPiper.csproj",
        "--framework", tfm,
        "--configuration", args.configuration,
        "-r", args.runtime,
        f"-o", str(out),
    ]
    if args.self_contained:
        cmd.append("--self-contained")
        cmd.append("true")
    else:
        cmd.append("--self-contained")
        cmd.append("false")
    return run_dotnet(cmd, root)


def find_iscc() -> str | None:
    """Locate the Inno Setup compiler (ISCC.exe)."""
    found = shutil.which("iscc")
    if found:
        return found
    candidates = [
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("ProgramFiles(x86)", "")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("ProgramFiles", "")) / "Inno Setup 6" / "ISCC.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "Inno Setup 7" / "ISCC.exe",
        Path(os.environ.get("ProgramFiles", "")) / "Inno Setup 7" / "ISCC.exe",
    ]
    for c in candidates:
        if c.is_file():
            return str(c)
    return None


def cmd_installer(args: argparse.Namespace) -> int:
    """Package the standalone Release into a Windows Setup installer and portable ZIP.

    Delegates to ``installer/build_installer.py``, preferring ``uv run`` so no
    Python environment setup is required. The script handles publish
    freshness, icon generation, Inno Setup discovery and the build itself.
    """
    if platform.system() != "Windows":
        print("error: building the Windows installer is only supported on Windows.", file=sys.stderr)
        return 1

    root = find_project_root()
    repo_root = root if (root / "installer").is_dir() else root.parent
    py_builder = repo_root / "installer" / "build_installer.py"
    if not py_builder.is_file():
        print(f"error: installer build script not found: {py_builder}", file=sys.stderr)
        return 1

    uv = shutil.which("uv")
    if uv:
        cmd = [uv, "run", "--no-project", "python", str(py_builder), "-v", args.app_version]
    else:
        print("notice: uv not found; falling back to the current Python interpreter.", file=sys.stderr)
        cmd = [sys.executable, str(py_builder), "-v", args.app_version]

    if args.output:
        cmd.extend(["-o", str(args.output)])
    if args.publish:
        cmd.append("--publish")
    if getattr(args, "zip_only", False):
        cmd.append("--zip-only")
    if getattr(args, "setup_only", False):
        cmd.append("--setup-only")
    return subprocess.call(cmd)


def _check_tool(name: str, extra_paths: list[str]) -> tuple[bool, str]:
    """Return (found, location). Checks PATH then a few common install spots."""
    found = shutil.which(name)
    if found:
        return True, found
    for p in extra_paths:
        full = Path(p)
        if full.is_file() and os.access(full, os.X_OK):
            return True, str(full)
    return False, ""


def cmd_doctor(args: argparse.Namespace) -> int:
    print("Video-Piper environment check\n")

    # .NET SDK
    dotnet = shutil.which("dotnet")
    if not dotnet:
        print("  [FAIL] .NET SDK     not found on PATH (install .NET 10 SDK)")
        return 1
    ver = subprocess.run([dotnet, "--version"], capture_output=True, text=True)
    print(f"  [ OK ] .NET SDK     {ver.stdout.strip()}")

    # yt-dlp & ffmpeg (needed at runtime for downloads)
    local_tools = Path.home() / ".local" / "bin"
    for tool in ("yt-dlp", "ffmpeg"):
        found, loc = _check_tool(tool, [str(local_tools / tool), f"/usr/local/bin/{tool}"])
        status = "[ OK ]" if found else "[MISS]"
        detail = loc if found else "not found (app can install it in-app, or add to PATH)"
        print(f"  {status} {tool:<10} {detail}")

    # uv (preferred runner for installer/build_installer.py; optional)
    uv = shutil.which("uv")
    uv_status = "[ OK ]" if uv else "[INFO]"
    uv_detail = uv if uv else "not found (optional; `vpp installer` falls back to the current Python)"
    print(f"  {uv_status} {'uv':<10} {uv_detail}")

    # Inno Setup (needed for `vpp installer`'s Setup executable; ZIP works without it)
    iscc = find_iscc()
    iscc_status = "[ OK ]" if iscc else "[MISS]"
    iscc_detail = iscc if iscc else "not found (`winget install JRSoftware.InnoSetup` for the Setup exe, or use --zip-only)"
    print(f"  {iscc_status} {'Inno Setup':<10} {iscc_detail}")

    # Project root sanity
    try:
        root = find_project_root()
        print(f"\n  [ OK ] project      {root}")
    except SystemExit:
        print("\n  [MISS] project      VideoPiper.sln not found from CWD")
        return 1

    print(f"\nvpp {__version__} — ready to build with `vpp build`.")
    return 0


# --------------------------------------------------------------------------- #
# Parser
# --------------------------------------------------------------------------- #
def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="vpp",
        description="Command-line project manager for the Video-Piper Uno/.NET app.",
    )
    parser.add_argument("--version", action="version", version=f"vpp {__version__}")
    sub = parser.add_subparsers(dest="command", required=True)

    p_build = sub.add_parser("build", help="Build the project (auto-selects target per OS).")
    p_build.add_argument("-f", "--framework", help="Target framework to build (default: host-appropriate).")
    p_build.add_argument("-c", "--configuration", default="Debug", help="Build configuration (default: Debug).")
    p_build.add_argument("--no-incremental", dest="incremental", action="store_false",
                         help="Force a full rebuild (no incremental build).")
    p_build.add_argument("--clean", action="store_true", help="Run `dotnet clean` before building.")
    p_build.set_defaults(func=cmd_build)

    p_run = sub.add_parser("run", help="Build and run the app.")
    p_run.add_argument("-f", "--framework", help="Target framework (default: host-appropriate).")
    p_run.add_argument("-c", "--configuration", default="Debug", help="Configuration (default: Debug).")
    p_run.set_defaults(func=cmd_run)

    p_pub = sub.add_parser("publish", help="Publish a standalone executable.")
    p_pub.add_argument("-f", "--framework", default=None,
                       help=f"Target framework (default: {WINDOWS_TFM}).")
    p_pub.add_argument("-c", "--configuration", default="Release", help="Configuration (default: Release).")
    p_pub.add_argument("-r", "--runtime", default="win-x64", help="Runtime identifier (default: win-x64).")
    p_pub.add_argument("-o", "--output", help="Output directory (default: <project>/publish).")
    p_pub.add_argument("--self-contained", action="store_true",
                       help="Produce a self-contained bundle (default: framework-dependent).")
    p_pub.set_defaults(func=cmd_publish)

    p_inst = sub.add_parser("installer", help="Build Windows setup installer or portable distribution package.")
    p_inst.add_argument("-v", "--app-version", default="1.0.0", help="Version string for installer (default: 1.0.0).")
    p_inst.add_argument("-o", "--output", help="Output directory for packages (default: installer/output).")
    p_inst.add_argument("--publish", action="store_true", help="Force a fresh `vpp publish` before packaging.")
    p_inst.add_argument("--zip-only", action="store_true", help="Build portable ZIP archive only.")
    p_inst.add_argument("--setup-only", action="store_true", help="Build Inno Setup executable only.")
    p_inst.set_defaults(func=cmd_installer)

    p_doc = sub.add_parser("doctor", help="Check prerequisites (.NET SDK, yt-dlp, ffmpeg, Inno Setup).")
    p_doc.set_defaults(func=cmd_doctor)

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv if argv is not None else sys.argv[1:])
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main())
