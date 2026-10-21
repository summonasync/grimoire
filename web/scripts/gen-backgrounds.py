"""Derive the six full-bleed backdrop arts from the owner's masters.

History of this mapping, because none of it is derivable from the filenames:

* Pass 1 shipped 900x506 from 1672x941 masters — a 1.6x upscale that read as mush.
* Pass 2 shipped the masters at native size, but they are 16:9 and the owner's primary
  monitor is 3440x1440, so object-cover threw away 26% of the height at 2.06x.
* Pass 3 (now) uses the 1942x809 masters: their 2.400 ratio sits almost on that
  viewport's 2.389, so the crop collapses to ~0.5% and the upscale to 1.78x.

Which scene lands on which screen is composition, not name: figure-forward shots to
persona and memories, machinery to settings, the orb to scry, open architecture to
stats, the widest overview to the console.

Masters are untracked owner drops; this script's output is what ships. Re-run after
replacing a master:
    python3 web/scripts/gen-backgrounds.py
"""
from pathlib import Path
from PIL import Image

MASTERS = Path("design/assets/backgrounds")
OUT = Path("web/public/design")
QUALITY = 82

_BATCH = "Изображение ChatGPT 21 окт. 2026 г., "

PAIRS = {
    "hero-console.webp": _BATCH + "01_30_13-1.png",
    "art-memories.webp": _BATCH + "01_27_44-6.png",
    "art-persona.webp": _BATCH + "01_30_14-2.png",
    "art-stats.webp": _BATCH + "01_30_15-3.png",
    "art-settings.webp": _BATCH + "01_27_40-2.png",
    "art-scry.webp": _BATCH + "01_27_42-4.png",
}


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for target, source in PAIRS.items():
        src = MASTERS / source
        if not src.exists():
            raise SystemExit(f"master missing: {src}")
        with Image.open(src) as handle:
            art = handle.convert("RGB")
        dest = OUT / target
        art.save(dest, format="WEBP", quality=QUALITY, method=6)
        w, h = art.size
        print(f"{target}: {w}x{h} a={w / h:.3f} {dest.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
