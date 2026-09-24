"""Video Piper Python Installer & Uninstaller for Windows.

Installs Video Piper to %LOCALAPPDATA%\\Programs\\Video Piper (or a custom path),
creates Start Menu and Desktop shortcuts, and registers with Windows Add/Remove Programs.
Zero third-party dependencies — standard library only.
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

APP_NAME = "Video Piper"
APP_VERSION = "1.0.0"
APP_PUBLISHER = "Video Piper"
APP_EXE = "VideoPiper.exe"
APP_URL = "https://github.com/mi222eh/Video-Piper"
UNINSTALL_REG_KEY = r"Software\Microsoft\Windows\CurrentVersion\Uninstall\VideoPiper"


# --------------------------------------------------------------------------- #
# Paths & Environment
# --------------------------------------------------------------------------- #
def get_default_install_dir() -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        return Path(local_app_data) / "Programs" / APP_NAME
    return Path.home() / "AppData" / "Local" / "Programs" / APP_NAME


def get_start_menu_shortcut_path() -> Path:
    app_data = os.environ.get("APPDATA")
    if app_data:
        programs = Path(app_data) / "Microsoft" / "Windows" / "Start Menu" / "Programs"
    else:
        programs = Path.home() / "AppData" / "Roaming" / "Microsoft" / "Windows" / "Start Menu" / "Programs"
    return programs / f"{APP_NAME}.lnk"


def get_desktop_shortcut_path() -> Path:
    userprofile = os.environ.get("USERPROFILE")
    if userprofile:
        desktop = Path(userprofile) / "Desktop"
    else:
        desktop = Path.home() / "Desktop"
    return desktop / f"{APP_NAME}.lnk"


def find_source_dir() -> Path | None:
    """Find the publish directory containing VideoPiper.exe."""
    cur = Path(__file__).resolve().parent
    candidates = [
        cur / "publish",
        cur.parent / "video-piper" / "publish",
        cur / ".." / "video-piper" / "publish",
        cur,  # when extracted inside a portable zip
    ]
    for c in candidates:
        candidate = c.resolve()
        if (candidate / APP_EXE).is_file():
            return candidate
    return None


# --------------------------------------------------------------------------- #
# Windows Shortcuts & Registry (stdlib-only)
# --------------------------------------------------------------------------- #
def create_shortcut(target: Path, shortcut_path: Path, icon_path: Path | None = None, description: str = "") -> bool:
    """Create a Windows .lnk shortcut using WScript.Shell via PowerShell."""
    shortcut_path.parent.mkdir(parents=True, exist_ok=True)
    ps_lines = [
        "$ws = New-Object -ComObject WScript.Shell",
        f"$s = $ws.CreateShortcut('{str(shortcut_path)}')",
        f"$s.TargetPath = '{str(target)}'",
        f"$s.WorkingDirectory = '{str(target.parent)}'",
    ]
    if icon_path and icon_path.is_file():
        ps_lines.append(f"$s.IconLocation = '{str(icon_path)}'")
    if description:
        ps_lines.append(f"$s.Description = '{description}'")
    ps_lines.append("$s.Save()")

    cmd = ["powershell", "-NoProfile", "-NonInteractive", "-Command", "; ".join(ps_lines)]
    try:
        res = subprocess.run(cmd, capture_output=True, text=True, check=True)
        return shortcut_path.is_file()
    except Exception as ex:
        print(f"  Varning: Kunde inte skapa genväg ({shortcut_path.name}): {ex}", file=sys.stderr)
        return False


def register_uninstall(install_dir: Path, uninstall_cmd: str, icon_path: Path | None = None) -> bool:
    """Register the application in Windows Add/Remove Programs (HKCU)."""
    try:
        import winreg

        with winreg.CreateKey(winreg.HKEY_CURRENT_USER, UNINSTALL_REG_KEY) as key:
            winreg.SetValueEx(key, "DisplayName", 0, winreg.REG_SZ, APP_NAME)
            winreg.SetValueEx(key, "DisplayVersion", 0, winreg.REG_SZ, APP_VERSION)
            winreg.SetValueEx(key, "Publisher", 0, winreg.REG_SZ, APP_PUBLISHER)
            winreg.SetValueEx(key, "InstallLocation", 0, winreg.REG_SZ, str(install_dir))
            winreg.SetValueEx(key, "UninstallString", 0, winreg.REG_SZ, uninstall_cmd)
            winreg.SetValueEx(key, "QuietUninstallString", 0, winreg.REG_SZ, f"{uninstall_cmd} --silent")
            winreg.SetValueEx(key, "URLInfoAbout", 0, winreg.REG_SZ, APP_URL)
            winreg.SetValueEx(key, "NoModify", 0, winreg.REG_DWORD, 1)
            winreg.SetValueEx(key, "NoRepair", 0, winreg.REG_DWORD, 1)

            if icon_path and icon_path.is_file():
                winreg.SetValueEx(key, "DisplayIcon", 0, winreg.REG_SZ, str(icon_path))
            else:
                exe = install_dir / APP_EXE
                if exe.is_file():
                    winreg.SetValueEx(key, "DisplayIcon", 0, winreg.REG_SZ, str(exe))

            # Calculate estimated size in KB
            try:
                total_bytes = sum(f.stat().st_size for f in install_dir.rglob("*") if f.is_file())
                winreg.SetValueEx(key, "EstimatedSize", 0, winreg.REG_DWORD, total_bytes // 1024)
            except Exception:
                pass
        return True
    except Exception as ex:
        print(f"  Varning: Kunde inte registrera i Windows Registry: {ex}", file=sys.stderr)
        return False


def unregister_uninstall() -> None:
    """Remove from Windows Add/Remove Programs registry."""
    try:
        import winreg
        winreg.DeleteKey(winreg.HKEY_CURRENT_USER, UNINSTALL_REG_KEY)
    except FileNotFoundError:
        pass
    except Exception as ex:
        print(f"  Varning: Registreringen kunde inte tas bort: {ex}", file=sys.stderr)


def is_app_running() -> bool:
    """Check if VideoPiper.exe is currently running."""
    try:
        res = subprocess.run(["tasklist", "/FI", f"IMAGENAME eq {APP_EXE}"], capture_output=True, text=True)
        return APP_EXE.lower() in res.stdout.lower()
    except Exception:
        return False


def kill_running_app() -> None:
    """Close any running instances of VideoPiper.exe."""
    try:
        subprocess.run(["taskkill", "/F", "/IM", APP_EXE], capture_output=True, check=False)
    except Exception:
        pass


# --------------------------------------------------------------------------- #
# Installation Workflow
# --------------------------------------------------------------------------- #
def install(
    source_dir: Path,
    target_dir: Path,
    create_desktop: bool = True,
    launch_after: bool = False,
    silent: bool = False,
) -> int:
    """Execute the installation."""
    if not silent:
        print("=========================================")
        print(f"  {APP_NAME} Installation v{APP_VERSION}")
        print("=========================================")
        print(f"Källa:         {source_dir}")
        print(f"Målmapp:       {target_dir}")

    # Check if running
    if is_app_running():
        if not silent:
            print(f"\n{APP_NAME} körs just nu. Stänger programmet innan installation...")
        kill_running_app()

    target_dir.mkdir(parents=True, exist_ok=True)

    # Copy all files from publish directory
    if not silent:
        print("\nKopierar filer...")

    file_count = 0
    total_bytes = 0

    for root, dirs, files in os.walk(source_dir):
        rel_path = Path(root).relative_to(source_dir)
        dest_root = target_dir / rel_path
        dest_root.mkdir(parents=True, exist_ok=True)

        for f in files:
            src_file = Path(root) / f
            dest_file = dest_root / f
            try:
                shutil.copy2(src_file, dest_file)
                file_count += 1
                total_bytes += src_file.stat().st_size
            except Exception as ex:
                print(f"  Fel vid kopiering av {f}: {ex}", file=sys.stderr)

    if not silent:
        size_mb = total_bytes / (1024 * 1024)
        print(f"[OK] {file_count} filer installerade ({size_mb:.1f} MB).")

    # Locate icon
    target_exe = target_dir / APP_EXE
    icon_path = target_dir / "icon.ico"
    if not icon_path.is_file():
        repo_icon = Path(__file__).resolve().parent / "app.ico"
        if repo_icon.is_file():
            shutil.copy2(repo_icon, icon_path)

    # Create uninstaller scripts inside target folder
    uninstall_py = target_dir / "uninstall.py"
    uninstall_cmd = target_dir / "uninstall.cmd"
    try:
        shutil.copy2(__file__, uninstall_py)
        with open(uninstall_cmd, "w", encoding="utf-8") as f:
            f.write(f'@echo off\r\n"{sys.executable}" "%~dp0uninstall.py" --uninstall %*\r\n')
    except Exception as ex:
        print(f"  Varning: Kunde inte skapa avinstallationsskript: {ex}", file=sys.stderr)

    # Create Shortcuts
    if not silent:
        print("\nSkapar genvägar...")

    start_menu = get_start_menu_shortcut_path()
    if create_shortcut(target_exe, start_menu, icon_path, description="Ladda ner YouTube-videor som MP3 eller MP4."):
        if not silent:
            print(f"[OK] Startmeny: {start_menu}")

    if create_desktop:
        desktop = get_desktop_shortcut_path()
        if create_shortcut(target_exe, desktop, icon_path, description="Video Piper"):
            if not silent:
                print(f"[OK] Skrivbord:  {desktop}")

    # Register in Windows Add/Remove Programs
    uninstall_exec = f'"{sys.executable}" "{uninstall_py}" --uninstall'
    if register_uninstall(target_dir, uninstall_exec, icon_path):
        if not silent:
            print("[OK] Registrerad i Windows Appar & Funktioner.")

    if not silent:
        print("\n=========================================")
        print("  Installationen är klar!")
        print("=========================================")
        print(f"Starta programmet från Startmenyn eller kör:")
        print(f"  {target_exe}\n")

    if launch_after:
        if not silent:
            print(f"Startar {APP_NAME}...")
        try:
            subprocess.Popen([str(target_exe)], cwd=str(target_dir))
        except Exception as ex:
            print(f"Kunde inte starta {APP_NAME}: {ex}", file=sys.stderr)

    return 0


# --------------------------------------------------------------------------- #
# Uninstallation Workflow
# --------------------------------------------------------------------------- #
def uninstall(target_dir: Path | None = None, silent: bool = False) -> int:
    """Execute clean uninstallation."""
    if target_dir is None:
        # Determine from current script location if installed, or default
        script_dir = Path(__file__).resolve().parent
        if (script_dir / APP_EXE).is_file():
            target_dir = script_dir
        else:
            target_dir = get_default_install_dir()

    if not silent:
        print("=========================================")
        print(f"  Avinstallera {APP_NAME}")
        print("=========================================")
        print(f"Målmapp: {target_dir}")

    # Check if running
    if is_app_running():
        if not silent:
            print(f"\n{APP_NAME} körs just nu. Stänger programmet...")
        kill_running_app()

    # 1. Remove shortcuts
    start_menu = get_start_menu_shortcut_path()
    if start_menu.is_file():
        try:
            start_menu.unlink()
            if not silent:
                print("[OK] Tog bort Startmeny-genväg.")
        except Exception as ex:
            print(f"  Kunde inte ta bort {start_menu}: {ex}", file=sys.stderr)

    desktop = get_desktop_shortcut_path()
    if desktop.is_file():
        try:
            desktop.unlink()
            if not silent:
                print("[OK] Tog bort Skrivbords-genväg.")
        except Exception as ex:
            print(f"  Kunde inte ta bort {desktop}: {ex}", file=sys.stderr)

    # 2. Unregister from Windows Registry
    unregister_uninstall()
    if not silent:
        print("[OK] Avregistrerad från Windows Appar & Funktioner.")

    # 3. Remove application files
    # Note: If running uninstall.py from inside target_dir, Python locks the running script file.
    # We delete everything except this script, then schedule directory cleanup via cmd.
    if target_dir.is_dir():
        this_file = Path(__file__).resolve()
        for item in target_dir.iterdir():
            if item == this_file:
                continue
            try:
                if item.is_dir():
                    shutil.rmtree(item, ignore_errors=True)
                else:
                    item.unlink()
            except Exception:
                pass

        if not silent:
            print("[OK] Programfiler raderade.")

        # Self-delete remaining directory asynchronously after exit
        try:
            cleanup_cmd = f'cmd /c ping 127.0.0.1 -n 2 > nul & rmdir /s /q "{str(target_dir)}"'
            subprocess.Popen(cleanup_cmd, shell=True)
        except Exception:
            pass

    if not silent:
        print("\n=========================================")
        print(f"  {APP_NAME} har avinstallerats.")
        print("  Dina nedladdade musikfiler i biblioteket har sparats.")
        print("=========================================\n")

    return 0


# --------------------------------------------------------------------------- #
# Main Entry Point
# --------------------------------------------------------------------------- #
def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="install.py",
        description=f"Installer and uninstaller for {APP_NAME} on Windows.",
    )
    parser.add_argument(
        "--dir", "-d",
        type=Path,
        default=None,
        help=f"Target installation directory (default: {get_default_install_dir()}).",
    )
    parser.add_argument(
        "--source", "-s",
        type=Path,
        default=None,
        help="Source directory containing VideoPiper.exe (auto-detected if omitted).",
    )
    parser.add_argument(
        "--no-desktop",
        action="store_true",
        help="Do not create a Desktop shortcut.",
    )
    parser.add_argument(
        "--launch", "-l",
        action="store_true",
        help=f"Launch {APP_NAME} immediately after installation.",
    )
    parser.add_argument(
        "--silent",
        action="store_true",
        help="Run non-interactively with default options.",
    )
    parser.add_argument(
        "--uninstall", "-u",
        action="store_true",
        help=f"Uninstall {APP_NAME} from this computer.",
    )

    args = parser.parse_args(argv if argv is not None else sys.argv[1:])

    if sys.platform != "win32":
        print(f"Fel: {APP_NAME} installationsskript stöder endast Windows.", file=sys.stderr)
        return 1

    if args.uninstall:
        return uninstall(target_dir=args.dir, silent=args.silent)

    # Installation mode
    source_dir = args.source or find_source_dir()
    if not source_dir or not (source_dir / APP_EXE).is_file():
        # Attempt to publish if inside git repository
        repo_root = Path(__file__).resolve().parent.parent
        sln = repo_root / "video-piper" / "VideoPiper.sln"
        if sln.is_file():
            print("Bygger fristående version (dotnet publish)...")
            publish_dir = repo_root / "video-piper" / "publish"
            proj = repo_root / "video-piper" / "VideoPiper" / "VideoPiper.csproj"
            cmd = [
                "dotnet", "publish", str(proj),
                "-f", "net10.0-windows10.0.26100",
                "-c", "Release",
                "-r", "win-x64",
                "--self-contained", "true",
                "-o", str(publish_dir),
            ]
            rc = subprocess.call(cmd)
            if rc == 0 and (publish_dir / APP_EXE).is_file():
                source_dir = publish_dir

    if not source_dir or not (source_dir / APP_EXE).is_file():
        print(
            f"Fel: Hittade inte källfilerna ({APP_EXE}).\n"
            "Kör `vpp publish --self-contained` eller ange `--source <katalog>`.",
            file=sys.stderr,
        )
        return 1

    target_dir = args.dir or get_default_install_dir()
    create_desktop = not args.no_desktop

    # Interactive confirmation when not silent
    if not args.silent and sys.stdin.isatty():
        print("=========================================")
        print(f"  {APP_NAME} Installationsprogram")
        print("=========================================")
        print(f"Installationsmapp: [{target_dir}]")
        custom_dir = input("Tryck Enter för att använda standardmappen, eller ange ny sökväg: ").strip()
        if custom_dir:
            target_dir = Path(custom_dir)

        resp = input("Skapa genväg på skrivbordet? [J/n]: ").strip().lower()
        if resp in ("n", "nej", "no"):
            create_desktop = False

        resp_launch = input(f"Starta {APP_NAME} efter installationen? [J/n]: ").strip().lower()
        if resp_launch not in ("n", "nej", "no"):
            args.launch = True

    return install(
        source_dir=source_dir,
        target_dir=target_dir,
        create_desktop=create_desktop,
        launch_after=args.launch,
        silent=args.silent,
    )


if __name__ == "__main__":
    raise SystemExit(main())
