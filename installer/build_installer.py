"""Build script for Video Piper installers and distribution packages.

Generates:
1. Portable distribution ZIP archive (`VideoPiper-1.0.0-win-x64-portable.zip`) with embedded `install.py`.
2. Windows Setup installer (`VideoPiper-Setup-1.0.0.exe`) via Inno Setup (if available).

Stdlib-only, zero dependencies.
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


def find_repo_root() -> Path:
    cur = Path(__file__).resolve().parent
    for candidate in [cur, *cur.parents]:
        if (candidate / "video-piper" / "VideoPiper.sln").is_file():
            return candidate
    raise SystemExit("Error: Could not locate repository root.")


def sha256_file(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while chunk := f.read(1024 * 1024):
            h.update(chunk)
    return h.hexdigest().upper()


def find_iscc() -> str | None:
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


def ensure_published(repo_root: Path, publish_dir: Path, force: bool = False) -> None:
    target_exe = publish_dir / APP_EXE
    if not force and target_exe.is_file():
        return

    print("Publishing standalone Release (dotnet publish)...", file=sys.stderr)
    proj = repo_root / "video-piper" / "VideoPiper" / "VideoPiper.csproj"
    cmd = [
        "dotnet", "publish", str(proj),
        "-f", WINDOWS_TFM,
        "-c", "Release",
        "-r", "win-x64",
        "--self-contained", "true",
        "-o", str(publish_dir),
    ]
    print(f"$ {' '.join(cmd)}", file=sys.stderr)
    subprocess.run(cmd, check=True)

    if not target_exe.is_file():
        raise SystemExit(f"Error: {target_exe} was not found after publish.")


def build_portable_zip(publish_dir: Path, output_dir: Path, version: str) -> Path:
    """Pack publish directory + install.py into a portable distribution ZIP."""
    zip_name = f"VideoPiper-{version}-win-x64-portable.zip"
    zip_path = output_dir / zip_name

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
        # Add all published files
        for root, _, files in os.walk(publish_dir):
            for file in files:
                full_path = Path(root) / file
                rel_path = full_path.relative_to(publish_dir)
                zf.write(full_path, arcname=str(rel_path))

        # Add install.py helper
        if install_py.is_file():
            zf.write(install_py, arcname="install.py")

        # Add README
        zf.writestr("README.txt", readme_content)

    return zip_path


def build_inno_setup(repo_root: Path, publish_dir: Path, output_dir: Path, version: str) -> Path | None:
    iscc = find_iscc()
    if not iscc:
        print("Notice: Inno Setup compiler (ISCC.exe) not found; skipping .exe installer.", file=sys.stderr)
        return None

    iss_file = repo_root / "installer" / "VideoPiper.iss"
    setup_name = f"VideoPiper-Setup-{version}"
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
    print(f"$ {' '.join(cmd)}", file=sys.stderr)
    subprocess.run(cmd, check=True)

    expected = output_dir / f"{setup_name}.exe"
    return expected if expected.is_file() else None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="build_installer.py",
        description="Build Video Piper portable ZIP and Windows installer.",
    )
    parser.add_argument("-v", "--app-version", default=APP_VERSION, help=f"Version string (default: {APP_VERSION}).")
    parser.add_argument("-o", "--output", help="Output directory (default: installer/output).")
    parser.add_argument("--publish", action="store_true", help="Force rebuild/republish.")
    parser.add_argument("--zip-only", action="store_true", help="Build portable ZIP archive only.")
    parser.add_argument("--setup-only", action="store_true", help="Build Inno Setup executable only.")

    args = parser.parse_args(argv if argv is not None else sys.argv[1:])

    repo_root = find_repo_root()
    publish_dir = repo_root / "video-piper" / "publish"
    output_dir = Path(args.output).resolve() if args.output else repo_root / "installer" / "output"
    output_dir.mkdir(parents=True, exist_ok=True)

    print("=========================================")
    print(f"  {APP_NAME} Installer & Package Builder")
    print(f"  Version: {args.app_version}")
    print("=========================================")

    ensure_published(repo_root, publish_dir, force=args.publish)

    results: list[Path] = []

    # 1. Portable ZIP
    if not args.setup_only:
        zip_file = build_portable_zip(publish_dir, output_dir, args.app_version)
        results.append(zip_file)

    # 2. Inno Setup
    if not args.zip_only and platform.system() == "Windows":
        setup_file = build_inno_setup(repo_root, publish_dir, output_dir, args.app_version)
        if setup_file:
            results.append(setup_file)

    print("\n=========================================")
    print("  Build Summary")
    print("=========================================")
    for path in results:
        size_mb = path.stat().st_size / (1024 * 1024)
        h = sha256_file(path)
        print(f"File:   {path.name}")
        print(f"Path:   {path}")
        print(f"Size:   {size_mb:.2f} MB ({path.stat().st_size:,} bytes)")
        print(f"SHA256: {h}\n")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
