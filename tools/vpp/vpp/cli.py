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

from vpp import __version__

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
    """Build the standalone release and package it into a Windows installer."""
    if platform.system() != "Windows":
        print("error: building the Windows installer is only supported on Windows.", file=sys.stderr)
        return 1

    root = find_project_root()
    repo_root = root if (root / "installer").is_dir() else root.parent
    installer_dir = repo_root / "installer"
    publish_dir = root / "publish"
    exe_path = publish_dir / "VideoPiper.exe"

    if args.publish or not exe_path.is_file():
        print("Publishing standalone release...", file=sys.stderr)
        pub_args = argparse.Namespace(
            framework=WINDOWS_TFM,
            configuration="Release",
            runtime="win-x64",
            output=str(publish_dir),
            self_contained=True,
        )
        rc = cmd_publish(pub_args)
        if rc != 0:
            return rc

    iscc = find_iscc()
    if not iscc:
        print(
            "error: Inno Setup compiler (ISCC.exe) not found.\n"
            "       Install it via `winget install JRSoftware.InnoSetup` or from https://jrsoftware.org/isdl.php",
            file=sys.stderr,
        )
        return 1

    iss_file = installer_dir / "VideoPiper.iss"
    output_dir = Path(args.output).resolve() if args.output else installer_dir / "output"
    output_dir.mkdir(parents=True, exist_ok=True)
    version = args.app_version

    cmd = [
        iscc,
        "/Qp",
        f"/DMyAppVersion={version}",
        f"/DSourceDir={publish_dir}",
        f"/O{output_dir}",
        f"/FVideoPiper-Setup-{version}",
        str(iss_file),
    ]
    print(f"$ {' '.join(cmd)}", file=sys.stderr)
    rc = subprocess.call(cmd)
    if rc == 0:
        setup_exe = output_dir / f"VideoPiper-Setup-{version}.exe"
        if setup_exe.is_file():
            size_mb = setup_exe.stat().st_size / (1024 * 1024)
            print(f"\nInstaller generated: {setup_exe} ({size_mb:.2f} MB)")
    return rc


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

    # Inno Setup (optional, for installer)
    iscc = find_iscc()
    iscc_status = "[ OK ]" if iscc else "[INFO]"
    iscc_detail = iscc if iscc else "not found (needed for `vpp installer`: `winget install JRSoftware.InnoSetup`)"
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

    p_inst = sub.add_parser("installer", help="Build the Windows installer via Inno Setup.")
    p_inst.add_argument("-v", "--app-version", default="1.0.0", help="Version string for installer (default: 1.0.0).")
    p_inst.add_argument("-o", "--output", help="Output directory for the setup .exe (default: installer/output).")
    p_inst.add_argument("--publish", action="store_true", help="Force a fresh `vpp publish` before packaging.")
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
