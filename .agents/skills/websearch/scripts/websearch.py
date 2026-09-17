#!/usr/bin/env python3
"""Web search helper for the Video-Piper project — no API key required.

Queries DuckDuckGo's HTML endpoint (with a Lite fallback) and prints clean,
ranked results: title, URL, and snippet. Uses only the Python standard library.

Usage:
  websearch.py "uno platform tabview items" [--max 8] [--json]
  websearch.py "yt-dlp playlist flat-playlist json" --max 5
"""

import argparse
import html
import json
import re
import sys
import urllib.parse
import urllib.request

USER_AGENT = (
    "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 "
    "(KHTML, like Gecko) Chrome/124.0 Safari/537.36"
)


def fetch(url: str) -> str:
    req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(req, timeout=20) as resp:
        return resp.read().decode("utf-8", errors="replace")


def decode_ddg(href: str) -> str:
    """Resolve a DuckDuckGo redirect link to the real target URL."""
    if href.startswith("//"):
        href = "https:" + href
    parsed = urllib.parse.urlparse(href)
    qs = urllib.parse.parse_qs(parsed.query)
    if "uddg" in qs:
        return qs["uddg"][0]
    # Already a plain external link.
    return href


def parse_html_results(page: str) -> list[dict]:
    """Parse result links + snippets from DuckDuckGo HTML/Lite markup."""
    results = []
    # Result anchors: class contains 'result__a'; href may appear before or after it.
    link_re = re.compile(
        r'<a\b(?=[^>]*class="[^"]*result__a)[^>]*href="([^"]+)"[^>]*>(.*?)</a>',
        re.IGNORECASE | re.DOTALL,
    )
    snippet_re = re.compile(
        r'<a[^>]+class="[^"]*result__snippet[^"]*"[^>]*>(.*?)</a>',
        re.IGNORECASE | re.DOTALL,
    )

    def clean(fragment: str) -> str:
        text = re.sub(r"<[^>]+>", "", fragment)
        return html.unescape(text).strip()

    snippets = [clean(s) for s in snippet_re.findall(page)]
    for i, (href, title_html) in enumerate(link_re.findall(page)):
        url = decode_ddg(html.unescape(href))
        if not url.startswith("http"):
            continue
        results.append(
            {
                "title": clean(title_html),
                "url": url,
                "snippet": snippets[i] if i < len(snippets) else "",
            }
        )
    return results


def parse_lite_results(page: str) -> list[dict]:
    """Fallback parser for lite.duckduckgo.com (simpler table layout)."""
    results = []
    # Each result row: a link then its snippet in the same <tr>.
    row_re = re.compile(r"<tr[^>]*>(.*?)</tr>", re.IGNORECASE | re.DOTALL)
    link_re = re.compile(
        r'<a[^>]+href="([^"]+)"[^>]*class="result-link"[^>]*>(.*?)</a>',
        re.IGNORECASE | re.DOTALL,
    )
    snippet_re = re.compile(r'class="result-snippet"[^>]*>(.*?)</td>', re.IGNORECASE | re.DOTALL)

    def clean(fragment: str) -> str:
        text = re.sub(r"<[^>]+>", "", fragment)
        return html.unescape(text).strip()

    for row in row_re.findall(page):
        lm = link_re.search(row)
        if not lm:
            continue
        url = decode_ddg(html.unescape(lm.group(1)))
        if not url.startswith("http"):
            continue
        sm = snippet_re.search(row)
        results.append(
            {"title": clean(lm.group(2)), "url": url, "snippet": clean(sm.group(1)) if sm else ""}
        )
    return results


def search(query: str, max_results: int) -> list[dict]:
    seen = set()
    out = []
    attempts = [
        (f"https://html.duckduckgo.com/html/?q={urllib.parse.quote_plus(query)}", parse_html_results),
        (f"https://lite.duckduckgo.com/lite/?q={urllib.parse.quote_plus(query)}", parse_lite_results),
    ]
    for url, parser in attempts:
        try:
            page = fetch(url)
        except Exception:
            continue
        for r in parser(page):
            if r["url"] in seen or not r["title"]:
                continue
            seen.add(r["url"])
            out.append(r)
            if len(out) >= max_results:
                return out
    return out


def main() -> None:
    parser = argparse.ArgumentParser(description="Web search (DuckDuckGo, no API key)")
    parser.add_argument("query")
    parser.add_argument("--max", type=int, default=8, help="max results (default 8)")
    parser.add_argument("--json", action="store_true", help="machine-readable output")
    args = parser.parse_args()

    results = search(args.query, args.max)

    if args.json:
        print(json.dumps(results, indent=2, ensure_ascii=False))
        return

    if not results:
        print(f"No results for: {args.query}")
        return
    print(f"{len(results)} result(s) for '{args.query}':\n")
    for i, r in enumerate(results, 1):
        print(f"{i}. {r['title']}")
        print(f"   {r['url']}")
        if r["snippet"]:
            snippet = r["snippet"][:200] + ("…" if len(r["snippet"]) > 200 else "")
            print(f"   {snippet}")
        print()


if __name__ == "__main__":
    main()
