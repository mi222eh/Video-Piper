---
name: websearch
description: This skill should be used when the user asks to "web search", "do a web search", "search the web", "look this up online", "find documentation for", "check if there's an issue/PR about", or needs general web results (docs, GitHub issues, Stack Overflow, news) that are NOT YouTube-specific. Uses DuckDuckGo's HTML endpoint via yt-dlp-free Python — no API key required. For searching YouTube videos specifically, use the yt-search skill instead.
---

# Web Search (no API key)

Search the general web from the terminal using DuckDuckGo's HTML endpoint. No
API key, no external dependencies beyond the Python standard library. Use it to
look up documentation, find GitHub issues/PRs, check Stack Overflow answers, or
verify a claim before acting on it.

## When to use

- Looking up library/API documentation (e.g., "Uno Platform TabView items",
  ".NET 10 breaking changes").
- Finding related GitHub issues/PRs for a dependency (`yt-dlp`, `ffmpeg`, Uno).
- Answering a factual question that needs a current web source.
- Verifying whether an approach is known-good before implementing it.

Do **not** use this skill to search YouTube videos — use the `yt-search` skill
for that (it uses yt-dlp and returns proper video metadata).

## Prerequisites

Outbound HTTPS access to `html.duckduckgo.com` (with a `lite.duckduckgo.com`
fallback). No packages to install. Python 3.8+ only.

## Usage

```bash
# Human-readable results (title, URL, snippet)
python3 scripts/websearch.py "uno platform tabview items"

# Limit result count
python3 scripts/websearch.py "yt-dlp flat-playlist json" --max 5

# Machine-readable JSON for further processing
python3 scripts/websearch.py ".net 10 breaking changes" --json
```

Run from the skill directory, or copy `scripts/websearch.py` next to your work.

## Interpreting results

- Each result is `{title, url, snippet}`. The script resolves DuckDuckGo's
  redirect links (`/l/?uddg=…`) to the real target URL, so URLs are directly
  usable (e.g., open in a browser or pass to `curl`).
- Snippets are truncated to ~200 chars in human mode; use `--json` for full text.
- If both endpoints fail (network block / rate limit), the script prints
  "No results" — retry once, then fall back to opening the query in a browser
  or using another search source.

## Tips

- Quote multi-word phrases in the query to keep them together: `"tabview items"`.
- Add site filters for targeted searches: `site:github.com uno platform issue`.
- Prefer `--json` when you plan to filter, count, or feed results into another
  command (e.g., pipe to `jq`).

## Notes & caveats

- DuckDuckGo's HTML endpoint is unofficial and can change markup; if parsing
  silently returns nothing, inspect the raw HTML (`python3 -c "..."` fetch) to
  update the regexes in `scripts/websearch.py`.
- This skill only *lists* results — it does not fetch page content. To read a
  result, open its URL directly.
