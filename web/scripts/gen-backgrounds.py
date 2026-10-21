"""Derive the six full-bleed backdrop arts from the owner's masters.

History of this mapping, because none of it is derivable from the filenames:

* Pass 1 shipped 900x506 from 1672x941 masters — a 1.6x upscale that read as mush.
* Pass 2 shipped those masters at native size, but they are 16:9 and the owner's primary
  monitor is 3440x1440, so object-cover threw away 26% of the height at 2.06x.
* Pass 3 used a 1942x809 (2.400) batch: crop fell to ~0.5%, upscale to 1.78x.
* Pass 4 (now) is the owner's redrawn set — same six scenes, new style, 1916x821 (2.334).
  The batch also ships a 16:9 twin of every scene, unused here: the primary monitor is
  21:9, and on 16:9 the 2.334 art loses 23% of its width.

Which scene goes on which screen is literal, not decorative: she reads the glowing ledger
on the console, sorts through photographs on memories, looks at the viewer on persona,
touches the floating charts on stats, works the instruments on settings, and bends over
the scrying orb on scry.

Masters are untracked owner drops; this script's output is what ships. Re-run after
replacing a master:
    python3 web/scripts/gen-backgrounds.py
"""
from pathlib import Path
from PIL import Image

MASTERS = Path("design/assets/backgrounds/new/21:9")
OUT = Path("web/public/design")
QUALITY = 82

_BATCH = "Изображение ChatGPT 21 окт. 2026 г., "

PAIRS = {
    "hero-console.webp": _BATCH + "14_20_12-1.png",
    "art-memories.webp": _BATCH + "14_20_14-2.png",
    "art-persona.webp": _BATCH + "14_20_15-3.png",
    "art-stats.webp": _BATCH + "14_20_15-4.png",
    "art-settings.webp": _BATCH + "14_20_16-5.png",
    "art-scry.webp": _BATCH + "14_20_17-6.png",
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
