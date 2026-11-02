#!/usr/bin/env python3
"""T26, Bruno's pick (2026-11-01, design §28a): the sheets and the numbers of B-game, from the bot "remake_game".

  python3 -I tools/remake_game_sheets.py <shots dir> <out dir> <room name> <tier>

Writes into <out dir>:
- AB_<room>_<camera>.png: A (left) beside B-game at <tier> with the room's air (right), 1920 x 1080 each, labelled.
- air_<room>_<camera>.png: B-game with the room's air setting (left) and the other one (right).
- tiers_<room>_<who>_<camera>.png: the figure cut from B-game at 1024, 2048 and 4096, at 2x (nearest), labelled. The
  bot freezes the figures, pauses the dust and turns the grain off for these frames, so only the maps differ.
- tiers_<room>.txt: per figure and camera, the difference of 1024 and 2048 against 4096 inside the figure mask
  (PSNR in dB: over 40 dB no eye sees it; mean absolute difference in 0..255 levels).
- contrast_<room>.txt: the T22 Weber contrast of Lucy and the guard (A, B-game, B-game with the air), Lucy's gold
  pixel share, the guard's colour, the walls (the upper third's median).
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

CAMS = ["cam_recovery", "cam_recovery_bed", "cam_recovery_long"]
AIR = {"bright": "off", "dark": "on"}  # the light in the air per room (Bruno, design §28a item 5)
OTHER = {"on": "off", "off": "on"}


def font(size):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def labelled(images, labels, out, pad=8):
    f = font(36)
    w = sum(i.width for i in images) + pad * (len(images) + 1)
    h = max(i.height for i in images) + 60 + pad * 2
    sheet = Image.new("RGB", (w, h), (32, 32, 32))
    d = ImageDraw.Draw(sheet)
    x = pad
    for im, lab in zip(images, labels):
        sheet.paste(im, (x, pad))
        d.text((x + 10, pad + im.height + 10), lab, fill=(235, 235, 235), font=f)
        x += im.width + pad
    sheet.save(out)
    print(f"[remake_game_sheets] {out}")


def rgb(path):
    return np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255.0


def mask(path):
    return np.asarray(Image.open(path).convert("L")) > 64


def hsv(a):
    mx, mn = a.max(-1), a.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def contrast_line(render, masks):
    a = rgb(render)
    L = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    anyfig = np.zeros(L.shape, bool)
    for m in masks.values():
        anyfig |= m
    parts = []
    for who, m in masks.items():
        grown = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(25))) > 128
        ring = grown & ~anyfig
        lf, ls = L[m].mean(), L[ring].mean()
        col = [int(c) for c in (a[m].mean(0) * 255).round()]
        h, s, v = hsv(a[m])
        if who == "guard":
            extra = f"rgb {tuple(col)} blue-red {col[2] - col[0]:+d} white {(L[m] > 0.6).mean() * 100:.1f}%"
        else:
            gold = ((h > 0.08) & (h < 0.17) & (s > 0.3) & (v > 0.25)).mean() * 100
            extra = f"rgb {tuple(col)} gold {gold:.1f}%"
        parts.append(f"{who}: fig {lf:.3f} bg {ls:.3f} weber {(lf - ls) / max(ls, 1e-3):+.2f} | {extra}")
    up = L[: L.shape[0] // 3][~anyfig[: L.shape[0] // 3]]
    parts.append(f"upper third median {np.median(up):.3f}")
    return " || ".join(parts)


def main(folder, out, room, tier):
    os.makedirs(out, exist_ok=True)
    clines, tlines = [], []
    for cam in CAMS:
        a = os.path.join(folder, f"remake_A_{cam}.png")
        g = os.path.join(folder, f"remake_Bg{tier}_{cam}.png")
        air = os.path.join(folder, f"remake_BgAlt_{cam}.png")
        if not (os.path.exists(a) and os.path.exists(g)):
            continue
        labelled([Image.open(a).convert("RGB"), Image.open(g).convert("RGB")],
                 [f"A: the look before T26 ({room} room)", f"B-game: Bruno's pick, {tier} maps, air {AIR[room]} ({room} room)"],
                 os.path.join(out, f"AB_{room}_{cam}.png"))
        if os.path.exists(air):
            labelled([Image.open(g).convert("RGB"), Image.open(air).convert("RGB")],
                     [f"B-game, air {AIR[room]} (this room's setting)", f"B-game, air {OTHER[AIR[room]]} (for comparison)"],
                     os.path.join(out, f"air_{room}_{cam}.png"))
        mA = {w: mask(os.path.join(folder, f"mask_A_{w}_{cam}.png")) for w in ("lucy", "guard")}
        mB = {w: mask(os.path.join(folder, f"mask_Bg_{w}_{cam}.png")) for w in ("lucy", "guard")}
        clines.append(f"A {cam}: " + contrast_line(a, mA))
        clines.append(f"B-game {cam}: " + contrast_line(g, mB))
        if os.path.exists(air):
            clines.append(f"B-game air {OTHER[AIR[room]]} {cam}: " + contrast_line(air, mB))
        ref = rgb(os.path.join(folder, f"remake_Tier4096_{cam}.png"))
        for who, m in mB.items():
            ys, xs = np.nonzero(m)
            if len(xs) == 0:
                continue
            x0, y0, x1, y1 = max(0, xs.min() - 30), max(0, ys.min() - 30), min(1920, xs.max() + 30), min(1080, ys.max() + 30)
            tiles, labs = [], []
            for t in (1024, 2048, 4096):
                p = os.path.join(folder, f"remake_Tier{t}_{cam}.png")
                crop = Image.open(p).convert("RGB").crop((x0, y0, x1, y1))
                tiles.append(crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST))
                labs.append(f"{t} (2x)")
                if t != 4096:
                    diff = rgb(p)[m] - ref[m]
                    mse = float((diff ** 2).mean())
                    psnr = 99.0 if mse == 0 else 10 * np.log10(1.0 / mse)
                    tlines.append(f"{room} {cam} {who} {t} vs 4096: PSNR {psnr:.1f} dB, mean abs diff {np.abs(diff).mean() * 255:.2f} levels, max {np.abs(diff).max() * 255:.0f}")
            again = os.path.join(folder, f"remake_Tier4096b_{cam}.png")
            if os.path.exists(again):
                diff = rgb(again)[m] - ref[m]
                mse = float((diff ** 2).mean())
                psnr = 99.0 if mse == 0 else 10 * np.log10(1.0 / mse)
                tlines.append(f"{room} {cam} {who} 4096 vs 4096 again (the noise floor): PSNR {psnr:.1f} dB, mean abs diff {np.abs(diff).mean() * 255:.2f} levels")
            labelled(tiles, labs, os.path.join(out, f"tiers_{room}_{who}_{cam}.png"))
    with open(os.path.join(out, f"contrast_{room}.txt"), "w") as f:
        f.write("\n".join(clines) + "\n")
    with open(os.path.join(out, f"tiers_{room}.txt"), "w") as f:
        f.write("\n".join(tlines) + "\n")
    print("\n".join(clines))
    print("\n".join(tlines))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4]))
