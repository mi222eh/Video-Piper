---
name: yt-search
description: This skill should be used when the user asks to "search YouTube", "look up a video", "check a YouTube link", "verify a video id", "what is this video's title/duration/channel", or needs to resolve/inspect YouTube metadata for the Video-Piper app (e.g. validating that a search query returns sane results, confirming a video id before wiring it into the library, or getting download URLs). Uses yt-dlp — the same engine the app uses — so results match what the app will actually see.
---

# YouTube Search & Metadata (Video-Piper)

Query YouTube from the terminal using `yt-dlp` — the exact engine Video-Piper's
`SearchService` and `YtDlpJson` use. This verifies that a search or video id
resolves to sane metadata *before* relying on it in the app, and is faster than
launching the desktop app for a quick check.

## When to use

- Verifying a YouTube search query returns real results (id/title/channel/duration).
- Resolving a bare video id or URL to its title, channel, and duration.
- Getting a canonical watch URL for an id before adding it to the library.
- Debugging why a download/search in the app returned nothing or wrong metadata.

## Prerequisites

`yt-dlp` must be available (on `PATH`, `~/.local/bin`, `/usr/local/bin`, or
`/usr/bin`). Check with:

```bash
command -v yt-dlp || ls ~/.local/bin/yt-dlp
```

If missing, install it (`uv tool install yt-dlp`) before running the script.

## Usage

The bundled helper wraps `yt-dlp --flat-playlist -J` (search) and full `-J`
(single video), matching the app's behavior:

```bash
# Search YouTube (human-readable, up to 10 results by default)
python3 scripts/yt_search.py search "radiohead creep"

# More/fewer results, or machine-readable JSON
python3 scripts/yt_search.py search "radiohead creep" --max 5
python3 scripts/yt_search.py search "radiohead creep" --json

# Full metadata for one video id or URL
python3 scripts/yt_search.py info jNQXAC9IVRw
python3 scripts/yt_search.py info "https://www.youtube.com/watch?v=jNQXAC9IVRw" --json

# Print the canonical watch URL for an id (or pass a URL through)
python3 scripts/yt_search.py url jNQXAC9IVRw
```

Run these from the skill directory, or copy `scripts/yt_search.py` next to where
you're working. The script needs no Python dependencies beyond the standard
library.

## Interpreting results

- **Search** returns flat entries: `{id, title, channel, duration?, url}`. Duration
  is often absent for search hits (flat mode skips stream resolution) — that's
  expected and matches `YtDlpJson.TryParsePlaylist`.
- **Info** does a full extraction, so it includes an accurate `duration` and
  channel — use it when the app shows "okänd längd" or a wrong uploader.
- A result with an empty/missing `id` is not usable; skip it (the app does too).

## Connecting to the app

The library routes downloads through `LibraryDownloadService`, which builds a
watch URL as `https://www.youtube.com/watch?v=<id>` — the same shape this skill's
`url` command emits. When verifying an app search, compare the script's output
against what `SearchService.SearchAsync` would produce: both parse the identical
`ytsearchN:` + `--flat-playlist -J` JSON via `YtDlpJson`.

## Notes & caveats

- Network access is required; results depend on YouTube's live state. A video can
  be "unavailable" or region-locked — the script surfaces yt-dlp's error message.
- Newer yt-dlp versions may warn about a missing JS runtime for some formats; that
  warning does not affect search/info and is safe to ignore.
- This skill only *inspects* metadata — it never downloads media. To actually
  download, use the app (or `yt-dlp` directly).
