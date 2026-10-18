"""Build script for Video Piper installers and distribution packages.

Generates:
1. Portable distribution ZIP archive (`VideoPiper-<ver>-win-x64-portable.zip`)
   with embedded `install.py` and a Swedish README.
2. Windows Setup installer (`VideoPiper-Setup-<ver>.exe`) via Inno Setup.

Usage (uv recommended — no Python environment setup required):

    uv run --no-project installer/build_installer.py              # ZIP + Setup
    uv run --no-project installer/build_installer.py --zip-only   # portable ZIP only
    uv run --no-project installer/build_installer.py --setup-only # Inno Setup exe only
    vpp installer                                                 # same, via the vpp CLI

A plain `python installer/build_installer.py` works too, as long as
`installer/app.ico` is current (see below).

The script itself is stdlib-only. `Pillow` is fetched on demand via
`uv run --with Pillow` when the installer icon has to be (re)generated —
that step needs `uv` on PATH.

Flags:
    -v/--app-version    Version string (default: 1.0.0)
    -o/--output         Output directory (default: installer/output)
    --publish           Force a fresh `dotnet publish` even if the bundle is current
    --zip-only          Build the portable ZIP archive only
    --setup-only        Build the Inno Setup executable only
    --regenerate-icon   Regenerate installer/app.ico from app-icon.png
    --install-iscc      If ISCC is not found, try `winget install JRSoftware.InnoSetup`

Publish freshness: the standalone bundle is republished automatically when
`VideoPiper.exe` is missing, `--publish` is given, or any C#/XAML source file
is newer than the published exe (prevents packaging a stale build).
"""

from __future__ import annotations

import argparse
import hashlib
import os
import platform
import shutil
import subprocess
import sys
import zipfile
from pathlib import Path

APP_NAME = "Video Piper"
APP_VERSION = "1.0.0"
APP_EXE = "VideoPiper.exe"
WINDOWS_TFM = "net10.0-windows10.0.26100"
SETUP_BASENAME = "VideoPiper-Setup"
ZIP_BASENAME = "VideoPiper"

SOURCE_EXTS = {".cs", ".xaml", ".csproj", ".props"}


def find_repo_root() -> Path:
    cur = Path(__file__).resolve().parent
    for candidate in [cur, *cur.parents]:
        if (candidate / "video-piper" / "VideoPiper.sln").is_file():
            return candidate
    raise SystemExit("Error: Could not locate repository root (video-piper/VideoPiper.sln).")


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(1024 * 1024):
            h.update(chunk)
    return h.hexdigest().upper()


def require_dotnet() -> str:
    dotnet = shutil.which("dotnet")
    if not dotnet:
        raise SystemExit("Error: 'dotnet' not found on PATH. Install the .NET 10 SDK first.")
    return dotnet


def require_uv() -> str:
    uv = shutil.which("uv")
    if not uv:
        raise SystemExit(
            "Error: 'uv' not found on PATH (needed for on-demand Pillow icon generation).\n"
            "       Install it: https://docs.astral.sh/uv/"
        )
    return uv


def find_iscc(auto_install: bool = False) -> str | None:
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
    if auto_install:
        winget = shutil.which("winget")
        if winget:
            print("Inno Setup not found — attempting `winget install JRSoftware.InnoSetup`...")
            subprocess.run(
                [
                    winget, "install", "--id", "JRSoftware.InnoSetup", "--exact",
                    "--silent", "--accept-package-agreements", "--accept-source-agreements",
                ],
                check=False,
            )
            found = shutil.which("iscc")
            if found:
                return found
            for c in candidates:
                if c.is_file():
                    return str(c)
    return None


def new_source_mtime(src_dir: Path) -> float:
    """Newest mtime of C#/XAML sources under src_dir (bin/obj skipped)."""
    newest = 0.0
    for root, dirs, files in os.walk(src_dir):
        dirs[:] = [d for d in dirs if d not in ("bin", "obj")]
        for name in files:
            p = Path(root) / name
            if p.suffix.lower() in SOURCE_EXTS:
                newest = max(newest, p.stat().st_mtime)
    return newest


def dotnet_publish(repo_root: Path, publish_dir: Path) -> None:
    dotnet = require_dotnet()
    proj = repo_root / "video-piper" / "VideoPiper" / "VideoPiper.csproj"
    cmd = [
        dotnet, "publish", str(proj),
        "-f", WINDOWS_TFM,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-o", str(publish_dir),
    ]
    print("$ " + " ".join(cmd), file=sys.stderr)
    subprocess.run(cmd, check=True)


def ensure_published(repo_root: Path, publish_dir: Path, force: bool = False) -> None:
    target_exe = publish_dir / APP_EXE
    stale_reason = None
    if force:
        stale_reason = "--publish requested"
    elif not target_exe.is_file():
        stale_reason = "no published bundle"
    else:
        src_dir = repo_root / "video-piper" / "VideoPiper"
        if new_source_mtime(src_dir) > target_exe.stat().st_mtime:
            stale_reason = "sources are newer than the published bundle"

    if stale_reason is None:
        print("[OK] Published bundle is current, skipping dotnet publish.")
        return

    print(f"[..] Publishing standalone Release (dotnet publish) — {stale_reason}...")
    dotnet_publish(repo_root, publish_dir)

    if not target_exe.is_file():
        raise SystemExit(f"Error: {target_exe} was not found after publish.")
    print(f"[OK] Published bundle: {publish_dir}")


def ensure_icon(repo_root: Path, force: bool = False) -> Path:
    """Make sure installer/app.ico is current, regenerating from app-icon.png via Pillow."""
    ico = repo_root / "installer" / "app.ico"
    png = repo_root / "app-icon.png"
    if not force and ico.is_file() and ico.stat().st_mtime >= png.stat().st_mtime:
        return ico

    if not png.is_file():
        raise SystemExit(f"Error: source icon not found: {png}")

    uv = require_uv()
    script = repo_root / "installer" / "convert_icon.py"
    print("[..] Regenerating installer icon (Pillow via uv)...")
    cmd = [uv, "run", "--no-project", "--with", "Pillow", "python",
           str(script), str(png), str(ico)]
    print("$ " + " ".join(cmd), file=sys.stderr)
    subprocess.run(cmd, check=True)
    if not ico.is_file():
        raise SystemExit(f"Error: icon generation did not produce {ico}")
    return ico


def build_portable_zip(publish_dir: Path, output_dir: Path, version: str) -> Path:
    """Pack publish directory + install.py into a portable distribution ZIP."""
    zip_path = output_dir / f"{ZIP_BASENAME}-{version}-win-x64-portable.zip"

    print(f"\nBuilding portable archive: {zip_path.name}...")
    readme_content = f"""{APP_NAME} v{version} - Portable Edition
==============================================

Alternativ 1 - Kör direkt (Portabelt):
  Dubbelklicka på '{APP_EXE}' för att starta Video Piper direkt.

Alternativ 2 - Installera på datorn:
  Öppna en terminal i denna mapp och kör:
    python install.py
  Detta skapar Startmeny- och Skrivbordsgenvägar samt
  registrerar programmet i Windows Appar & Funktioner.

Avinstallera:
  Kör 'python install.py --uninstall' eller avinstallera via
  Windows Inställningar -> Appar.
"""

    install_py = Path(__file__).resolve().parent / "install.py"

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for root, _, files in os.walk(publish_dir):
            for file in files:
                full_path = Path(root) / file
                rel_path = full_path.relative_to(publish_dir)
                zf.write(full_path, arcname=str(rel_path))

        if install_py.is_file():
            zf.write(install_py, arcname="install.py")

        zf.writestr("README.txt", readme_content)

    return zip_path


def build_inno_setup(repo_root: Path, publish_dir: Path, output_dir: Path, version: str,
                     auto_install: bool) -> Path:
    iscc = find_iscc(auto_install=auto_install)
    if not iscc:
        raise SystemExit(
            "Error: Inno Setup compiler (ISCC.exe) not found.\n"
            "       Install it via `winget install JRSoftware.InnoSetup`\n"
            "       or download from https://jrsoftware.org/isdl.php\n"
            "       (use --zip-only to build just the portable ZIP)"
        )
    print(f"[OK] Inno Setup compiler: {iscc}")

    iss_file = repo_root / "installer" / "VideoPiper.iss"
    setup_name = f"{SETUP_BASENAME}-{version}"
    cmd = [
        iscc,
        "/Qp",
        f"/DMyAppVersion={version}",
        f"/DSourceDir={publish_dir}",
        f"/O{output_dir}",
        f"/F{setup_name}",
        str(iss_file),
    ]
    print(f"\nBuilding Inno Setup installer: {setup_name}.exe...")
    print("$ " + " ".join(cmd), file=sys.stderr)
    subprocess.run(cmd, check=True)

    expected = output_dir / f"{setup_name}.exe"
    if not expected.is_file():
        raise SystemExit(f"Error: expected installer was not produced: {expected}")
    return expected


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="build_installer.py",
        description="Build Video Piper portable ZIP and Windows Setup installer.",
    )
    parser.add_argument("-v", "--app-version", default=APP_VERSION,
                        help=f"Version string (default: {APP_VERSION}).")
    parser.add_argument("-o", "--output", help="Output directory (default: installer/output).")
    parser.add_argument("--publish", action="store_true", help="Force a fresh dotnet publish.")
    parser.add_argument("--zip-only", action="store_true", help="Build portable ZIP archive only.")
    parser.add_argument("--setup-only", action="store_true", help="Build Inno Setup executable only.")
    parser.add_argument("--regenerate-icon", action="store_true",
                        help="Regenerate installer/app.ico from app-icon.png (needs uv).")
    parser.add_argument("--install-iscc", action="store_true",
                        help="Try `winget install JRSoftware.InnoSetup` if ISCC is not found.")

    args = parser.parse_args(argv if argv is not None else sys.argv[1:])
    if args.zip_only and args.setup_only:
        parser.error("--zip-only and --setup-only are mutually exclusive.")
    if args.setup_only and platform.system() != "Windows":
        parser.error("the Windows Setup installer can only be built on a Windows host.")

    repo_root = find_repo_root()
    publish_dir = repo_root / "video-piper" / "publish"
    output_dir = Path(args.output).resolve() if args.output else repo_root / "installer" / "output"
    output_dir.mkdir(parents=True, exist_ok=True)

    print("=========================================")
    print(f"  {APP_NAME} Installer & Package Builder")
    print(f"  Version: {args.app_version}")
    print("=========================================")

    ensure_published(repo_root, publish_dir, force=args.publish)
    if not args.zip_only:
        ensure_icon(repo_root, force=args.regenerate_icon)

    results: list[Path] = []

    if not args.setup_only:
        results.append(build_portable_zip(publish_dir, output_dir, args.app_version))

    if not args.zip_only:
        results.append(build_inno_setup(repo_root, publish_dir, output_dir, args.app_version,
                                        auto_install=args.install_iscc))

    print("\n=========================================")
    print("  Build Summary")
    print("=========================================")
    for path in results:
        size_mb = path.stat().st_size / (1024 * 1024)
        print(f"File:   {path.name}")
        print(f"Path:   {path}")
        print(f"Size:   {size_mb:.2f} MB ({path.stat().st_size:,} bytes)")
        print(f"SHA256: {sha256_file(path)}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
