#!/usr/bin/env python3
"""T26 remake look: the figure-against-background contrast of Lucy and the guard, the way T22 measured it
(~/MEGA/glorified/production/tests/T22_black_look/contrast.py), plus the colour checks of the brief.

  python3 -I tools/remake_contrast.py <shots dir> [out.txt]

For each profile (A, B) and camera, the bot saves remake_<P>_<cam>.png and one mask per figure
(mask_<P>_<lucy|guard>_<cam>.png: the figure alone, unlit white on black, the same camera and frame).
Per figure: the mean luminance of the figure and of a ring just outside it (a 25 px max filter minus both figures),
the Weber contrast (fig - bg) / bg, the share of figure pixels brighter than the ring mean.
Colour checks: the guard's mean colour (grey-blue: blue over red) and the share of near-white pixels (the bands);
Lucy's mean colour (pale) and the share of gold pixels (hue 30-60 degrees, saturation over 0.3: the veins).
The walls: the median luminance of the upper third of the frame without the figures (black walls stay low; the white
grout lifts the mean, so the median is the number).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageFilter


def load_mask(path):
    m = Image.open(path).convert("L")
    return np.asarray(m) > 64, m


def hsv(a):
    mx, mn = a.max(-1), a.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def main(folder, out=None):
    lines = []
    cams = sorted({f[len("remake_A_"):-4] for f in os.listdir(folder) if f.startswith("remake_A_cam") and f.endswith(".png")})
    for cam in cams:
        for p in ("A", "B"):
            render = os.path.join(folder, f"remake_{p}_{cam}.png")
            if not os.path.exists(render):
                continue
            a = np.asarray(Image.open(render).convert("RGB")).astype(np.float32) / 255.0
            L = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
            masks = {}
            for who in ("lucy", "guard"):
                mp = os.path.join(folder, f"mask_{p}_{who}_{cam}.png")
                if os.path.exists(mp):
                    masks[who] = load_mask(mp)
            anyfig = np.zeros(L.shape, bool)
            for m, _ in masks.values():
                anyfig |= m
            parts = []
            for who, (m, img) in masks.items():
                if m.sum() < 50:
                    parts.append(f"{who}: not in frame")
                    continue
                grown = np.asarray(img.point(lambda v: 255 if v > 64 else 0).filter(ImageFilter.MaxFilter(25))) > 128
                ring = grown & ~anyfig
                lf, ls = L[m].mean(), L[ring].mean()
                col = [int(c) for c in (a[m].mean(0) * 255).round()]
                h, s, v = hsv(a[m])
                if who == "guard":
                    extra = f"rgb {tuple(col)} blue-red {col[2] - col[0]:+d} white {((L[m] > 0.6).mean() * 100):.1f}%"
                else:
                    gold = ((h > 0.08) & (h < 0.17) & (s > 0.3) & (v > 0.25)).mean() * 100
                    extra = f"rgb {tuple(col)} gold {gold:.1f}%"
                parts.append(f"{who}: fig {lf:.3f} bg {ls:.3f} weber {(lf - ls) / max(ls, 1e-3):+.2f} brighter {(L[m] > ls).mean() * 100:.0f}% | {extra}")
            hgt = L.shape[0]
            upper = L[: hgt // 3][~anyfig[: hgt // 3]]
            parts.append(f"upper third median {np.median(upper):.3f} mean {upper.mean():.3f}")
            lines.append(f"{p} {cam}: " + " || ".join(parts))
    text = "\n".join(lines)
    print(text)
    if out:
        with open(out, "w") as f:
            f.write(text + "\n")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
