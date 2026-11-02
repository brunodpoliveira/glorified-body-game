#!/usr/bin/env python3
"""T26, Bruno's fault of 2026-11-01: "the C3 veins must read as gold". Lucy's maps for the three texture tiers.

  python3 tools/remake_lucy_gold.py

The T20 "subtle" maps (tools/remake_lucy_c3.py) kept the ORIGINAL painted colour inside the veins: a pale beige
(mean 170, 148, 112, the skin is 164, 148, 135), full metal. A metal reflects its room, and a black room gave the
veins nothing to reflect: 0.0 percent gold pixels in B. The fix, inside the vein mask only (metal over 0.5 in the
_ms map, feathered by 1 px):
1. The base colour becomes the T20 gold (1.00, 0.76, 0.22), shaded by the painted brightness (gold_maps.py SHADE 0.4).
2. The metal goes from 1.0 to 0.6: 40 percent of the gold is diffuse, so the veins read as gold under any light,
   and the metal part still catches the highlights.
The sunken groove (the normal map and the darker skin around each vein) stays as it was.
In:  Assets/_Game/Remake/Characters/lucy_c3_{base,ms,n}_4k (from remake_lucy_c3.py; the 4k ones are the source).
Out: lucy_c3_{base,ms,n}_{4k,2k,1k}: the three tiers of B-game's texture pick (and A's 1k).
"""
import os

import numpy as np
from PIL import Image, ImageFilter

TOP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DIR = os.path.join(TOP, "Assets/_Game/Remake/Characters")
SRC = os.path.expanduser("~/MEGA/glorified-body-renders/T26_remake_look/lucy_src")  # outside Assets: no import
GOLD = np.array([1.00, 0.76, 0.22], np.float32)
SHADE = 0.4
METAL = 0.6


def main():
    os.makedirs(SRC, exist_ok=True)
    # keep the untouched 4k sources once, so the script can run again
    for name in ("lucy_c3_base_4k.jpg", "lucy_c3_ms_4k.png", "lucy_c3_n_4k.png"):
        src = os.path.join(SRC, name.replace("_4k", "_src4k"))
        if not os.path.exists(src):
            Image.open(os.path.join(DIR, name)).save(src, quality=95) if name.endswith(".jpg") else Image.open(os.path.join(DIR, name)).save(src)
    base = np.asarray(Image.open(os.path.join(SRC, "lucy_c3_base_src4k.jpg")).convert("RGB"), np.float32) / 255.0
    ms = np.asarray(Image.open(os.path.join(SRC, "lucy_c3_ms_src4k.png")).convert("RGBA"), np.float32) / 255.0
    normal = Image.open(os.path.join(SRC, "lucy_c3_n_src4k.png")).convert("RGB")
    vein = Image.fromarray(((ms[..., 0] > 0.5) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.0))
    m = (np.asarray(vein, np.float32) / 255.0)[..., None]
    v = base.max(-1, keepdims=True)
    gold = np.clip(GOLD * ((1 - SHADE) + SHADE * 1.6 * v), 0, 1)
    out_base = base * (1 - m) + gold * m
    metal = ms[..., 0:1] * (1 - m) + METAL * m * (ms[..., 0:1] > 0.01)
    out_ms = np.concatenate([metal, metal, metal, ms[..., 3:4]], -1)
    print(f"[remake_lucy_gold] vein mask {(m > 0.5).mean() * 100:.2f} % of the texture; vein colour before {base[m[..., 0] > 0.5].mean(0).round(3)}, after {out_base[m[..., 0] > 0.5].mean(0).round(3)}")
    b8 = Image.fromarray((out_base * 255 + 0.5).astype(np.uint8))
    ms8 = Image.fromarray((np.clip(out_ms, 0, 1) * 255 + 0.5).astype(np.uint8), "RGBA")
    for size, tag in ((4096, "4k"), (2048, "2k"), (1024, "1k")):
        b8.resize((size, size), Image.LANCZOS).save(os.path.join(DIR, f"lucy_c3_base_{tag}.jpg"), quality=95)
        ms8.resize((size, size), Image.LANCZOS).save(os.path.join(DIR, f"lucy_c3_ms_{tag}.png"), optimize=True)
        normal.resize((size, size), Image.LANCZOS).save(os.path.join(DIR, f"lucy_c3_n_{tag}.png"), optimize=True)
        print(f"[remake_lucy_gold] lucy_c3_*_{tag}")
    # the guard's tiers: the T24 light kit at 4096, a 2048 from it, and the game's own 1024
    g4 = Image.open(os.path.join(DIR, "guard_basecolor_4k.jpg")).convert("RGB")
    g4.resize((2048, 2048), Image.LANCZOS).save(os.path.join(DIR, "guard_basecolor_2k.jpg"), quality=95)
    Image.open(os.path.join(TOP, "Assets/_Game/Characters/Models/guard_basecolor.png")).convert("RGB").save(os.path.join(DIR, "guard_basecolor_1k.png"))
    print("[remake_lucy_gold] guard_basecolor_2k.jpg, guard_basecolor_1k.png")


if __name__ == "__main__":
    main()
