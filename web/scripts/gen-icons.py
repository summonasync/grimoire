"""Derive every shipped icon from web/design/ico.png.

The 1254x1254 master is too heavy to ship (2.3 MB) and too detailed to read at 16px,
so it lives outside public/ and never enters the export; this script writes the three
files the app actually references. Re-run after replacing the master:
    python3 web/scripts/gen-icons.py
"""
from PIL import Image

MASTER = "web/design/ico.png"
ICO_SIZES = [(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (256, 256)]
TARGETS = [
    ("web/public/favicon.ico", 256, "ICO"),
    ("web/src/app/icon.png", 512, "PNG"),
    ("web/src/app/apple-icon.png", 180, "PNG"),
]


def main() -> None:
    art = Image.open(MASTER).convert("RGBA")

    for path, size, fmt in TARGETS:
        resized = art.resize((size, size), Image.LANCZOS)
        if fmt == "ICO":
            resized.save(path, format="ICO", sizes=ICO_SIZES)
        else:
            resized.save(path, format=fmt, optimize=True)
        print("wrote", path)


if __name__ == "__main__":
    main()
