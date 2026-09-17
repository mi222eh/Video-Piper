#!/usr/bin/env python3
"""Standalone YouTube search/info helper for the Video-Piper project.

Mirrors the app's own pipeline (SearchService + YtDlpJson) so an agent can
query YouTube from the terminal without launching the desktop app: verify a
search returns sane metadata, resolve a video id to a title/duration, or grab
download URLs before wiring them into the library.

Requires `yt-dlp` on PATH (or in ~/.local/bin). No other dependencies.

Usage:
  yt_search.py search "radiohead creep" [--max 10] [--json]
  yt_search.py info <video-id-or-url> [--json]
  yt_search.py url   <video-id-or-url>        # print the watch URL for an id
"""

import argparse
import json
import os
import shutil
import subprocess
import sys


def find_ytdlp() -> str:
    """Locate yt-dlp: PATH, then common install locations."""
    found = shutil.which("yt-dlp")
    if found:
        return found
    candidates = [
        os.path.expanduser("~/.local/bin/yt-dlp"),
        "/usr/local/bin/yt-dlp",
        "/usr/bin/yt-dlp",
    ]
    for c in candidates:
        if os.path.isfile(c) and os.access(c, os.X_OK):
            return c
    sys.exit("error: yt-dlp not found on PATH or in common locations")


def run_ytdlp(ytdlp: str, args: list[str]) -> dict:
    cmd = [ytdlp, "--flat-playlist", "-J", "--no-warnings", *args]
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=120)
    except subprocess.TimeoutExpired:
        sys.exit("error: yt-dlp timed out after 120s")
    if proc.returncode != 0:
        err = (proc.stderr or "").strip()
        sys.exit(f"error: yt-dlp exited {proc.returncode}: {err[:500]}")
    return json.loads(proc.stdout)


def fmt_duration(seconds) -> str:
    if not seconds or seconds <= 0:
        return ""
    s = int(seconds)
    h, rem = divmod(s, 3600)
    m, sec = divmod(rem, 60)
    return f"{h}:{m:02d}:{sec:02d}" if h else f"{m}:{sec:02d}"


def entry_to_dict(e: dict) -> dict:
    return {
        "id": e.get("id", ""),
        "title": e.get("title") or e.get("id", ""),
        "channel": e.get("channel") or e.get("uploader"),
        "duration": e.get("duration"),
        "url": f"https://www.youtube.com/watch?v={e.get('id', '')}",
    }


def cmd_search(ytdlp: str, query: str, max_results: int, as_json: bool) -> None:
    data = run_ytdlp(ytdlp, [f"ytsearch{max_results}:{query}"])
    entries = data.get("entries", [])
    results = [entry_to_dict(e) for e in entries if e.get("id")]
    if as_json:
        print(json.dumps(results, indent=2, ensure_ascii=False))
        return
    if not results:
        print(f"No results for: {query}")
        return
    print(f"{len(results)} result(s) for '{query}':\n")
    for i, r in enumerate(results, 1):
        dur = fmt_duration(r["duration"])
        dur_part = f"  [{dur}]" if dur else ""
        chan = f"  — {r['channel']}" if r["channel"] else ""
        print(f"{i:2}. {r['title']}{dur_part}{chan}")
        print(f"     id={r['id']}")


def cmd_info(ytdlp: str, target: str, as_json: bool) -> None:
    # Full (non-flat) metadata for a single video/URL.
    if target.startswith("http"):
        arg = target
    else:
        arg = f"https://www.youtube.com/watch?v={target}"
    data = run_ytdlp(ytdlp, [arg])
    out = {
        "id": data.get("id"),
        "title": data.get("title"),
        "channel": data.get("channel") or data.get("uploader"),
        "duration": data.get("duration"),
        "url": f"https://www.youtube.com/watch?v={data.get('id', '')}",
    }
    if as_json:
        print(json.dumps(out, indent=2, ensure_ascii=False))
        return
    print(f"Title:   {out['title']}")
    print(f"Channel: {out['channel'] or '(unknown)'}")
    print(f"Length:  {fmt_duration(out['duration']) or '(unknown)'}")
    print(f"ID:      {out['id']}")
    print(f"URL:     {out['url']}")


def cmd_url(_ytdlp: str, target: str, _as_json: bool) -> None:
    if target.startswith("http"):
        print(target)
    else:
        print(f"https://www.youtube.com/watch?v={target}")


def main() -> None:
    parser = argparse.ArgumentParser(description="YouTube search/info via yt-dlp")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_search = sub.add_parser("search", help="search YouTube")
    p_search.add_argument("query")
    p_search.add_argument("--max", type=int, default=10, help="max results (default 10)")
    p_search.add_argument("--json", action="store_true", help="machine-readable output")

    p_info = sub.add_parser("info", help="full metadata for one video id/URL")
    p_info.add_argument("target")
    p_info.add_argument("--json", action="store_true")

    p_url = sub.add_parser("url", help="print watch URL for an id or pass through a URL")
    p_url.add_argument("target")
    p_url.add_argument("--json", action="store_true")

    args = parser.parse_args()
    ytdlp = find_ytdlp()

    if args.cmd == "search":
        cmd_search(ytdlp, args.query, args.max, args.json)
    elif args.cmd == "info":
        cmd_info(ytdlp, args.target, args.json)
    elif args.cmd == "url":
        cmd_url(ytdlp, args.target, args.json)


if __name__ == "__main__":
    main()
