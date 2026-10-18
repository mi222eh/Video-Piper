"""Convert a PNG image into a multi-resolution Windows .ico file (16, 32, 48, 64, 128, 256 px).

Run via uv (no local Pillow needed):

    uv run --no-project --with Pillow python installer/convert_icon.py [input.png] [output.ico]

Defaults: <repo-root>/app-icon.png -> <repo-root>/installer/app.ico

Stdlib-only apart from Pillow, which `uv run --with Pillow` provides.
This is the Python replacement for the former convert-icon.ps1.
"""

from __future__ import annotations

import sys
from pathlib import Path

SIZES = (16, 32, 48, 64, 128, 256)


def main(argv: list[str] | None = None) -> int:
    argv = list(sys.argv[1:] if argv is None else argv)
    script = Path(__file__).resolve()
    installer_dir = script.parent
    repo_root = installer_dir.parent

    input_png = Path(argv[0]) if len(argv) >= 1 else repo_root / "app-icon.png"
    output_ico = Path(argv[1]) if len(argv) >= 2 else installer_dir / "app.ico"

    if not input_png.is_file():
        print(f"Error: source image not found: {input_png}", file=sys.stderr)
        return 1

    try:
        from PIL import Image
    except ImportError:
        print("Error: Pillow is required. Run via: "
              "uv run --no-project --with Pillow python convert_icon.py", file=sys.stderr)
        return 1

    img = Image.open(input_png).convert("RGBA")
    imgs = [img.resize((s, s), Image.LANCZOS) for s in SIZES]
    imgs[-1].save(output_ico, sizes=[(s, s) for s in SIZES])
    print(f"Created {output_ico} ({len(SIZES)} resolutions)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
