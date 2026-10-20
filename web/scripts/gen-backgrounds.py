"""Derive the six full-bleed backdrop arts from the owner's masters.

The first pass shipped the arts at 900x506 while the masters are 1672x941, so a
1440-wide viewport upscaled them ~1.6x and every screen except the console read as
zoomed-in mush. Emitting at native master size removes the upscale on typical
viewports; the masters themselves are untracked owner drops, this output is tracked.

Mapping was established by mean-abs-difference against the shipped arts, not by name.
Re-run after replacing a master:
    python3 web/scripts/gen-backgrounds.py
"""
from pathlib import Path
from PIL import Image

MASTERS = Path("design/assets/backgrounds")
OUT = Path("web/public/design")
QUALITY = 82

# The app asks for these paths; the master files are named by ChatGPT export timestamp.
PAIRS = {
    "hero-console.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_15-1.png",
    "art-stats.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_16-2.png",
    "art-persona.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_17-3.png",
    "art-memories.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_18-4.png",
    "art-settings.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_19-5.png",
    "art-scry.webp": "Изображение ChatGPT 19 окт. 2026 г., 00_49_20-6.png",
}


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for target, source in PAIRS.items():
        src = MASTERS / source
        art = Image.open(src).convert("RGB")
        dest = OUT / target
        art.save(dest, format="WEBP", quality=QUALITY, method=6)
        print(f"{target}: {art.size[0]}x{art.size[1]} {dest.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
