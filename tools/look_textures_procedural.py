#!/usr/bin/env python3
"""The look test textures, made by code: tileable by construction (periodic noise), 1024 px, one colour map each.
The fallback when RFab draws scenes instead of surfaces (2026-10-22, RFAB_GAPS 11). Usage:
  tools/look_textures_procedural.py <out_dir>   -> concrete_wall.png concrete_floor.png steel_panel.png soil.png gear_emblem.png
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

SIZE = 1024
rng = np.random.default_rng(1999)


def periodic_noise(cells, octaves=4, persistence=0.5):
    """Value noise on a torus: random values on a cells x cells grid, bilinear, octaves double the grid."""
    total = np.zeros((SIZE, SIZE), dtype=np.float32)
    amplitude = 1.0
    norm = 0.0
    for o in range(octaves):
        n = cells * (2 ** o)
        grid = rng.random((n, n)).astype(np.float32)
        ys = np.linspace(0, n, SIZE, endpoint=False)
        xs = np.linspace(0, n, SIZE, endpoint=False)
        y0 = np.floor(ys).astype(int) % n
        x0 = np.floor(xs).astype(int) % n
        fy = (ys - np.floor(ys))[:, None]
        fx = (xs - np.floor(xs))[None, :]
        fy = fy * fy * (3 - 2 * fy)
        fx = fx * fx * (3 - 2 * fx)
        y1 = (y0 + 1) % n
        x1 = (x0 + 1) % n
        a = grid[np.ix_(y0, x0)]
        b = grid[np.ix_(y0, x1)]
        c = grid[np.ix_(y1, x0)]
        d = grid[np.ix_(y1, x1)]
        layer = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy
        total += layer * amplitude
        norm += amplitude
        amplitude *= persistence
    return total / norm


def to_image(gray, tint=(1.0, 1.0, 1.0)):
    gray = np.clip(gray, 0, 1)
    rgb = np.stack([gray * tint[0], gray * tint[1], gray * tint[2]], axis=-1)
    return Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB")


def concrete_wall():
    base = 0.58 + (periodic_noise(8, 5) - 0.5) * 0.22
    speckle = (rng.random((SIZE, SIZE)) - 0.5) * 0.06
    g = base + speckle
    # faint formwork lines every 256 px (one metre at 2 m per tile... the tile is 2 m: four lines)
    for y in range(0, SIZE, 256):
        g[y:y + 2, :] -= 0.10
        g[y + 2:y + 4, :] += 0.03
    # air holes
    for _ in range(90):
        cx, cy, r = rng.integers(0, SIZE), rng.integers(0, SIZE), rng.integers(2, 6)
        yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
        mask = xx * xx + yy * yy <= r * r
        ys = (np.arange(cy - r, cy + r + 1) % SIZE)
        xs = (np.arange(cx - r, cx + r + 1) % SIZE)
        sub = g[np.ix_(ys, xs)]
        sub[mask] -= 0.18
        g[np.ix_(ys, xs)] = sub
    return to_image(g, (1.0, 0.99, 0.97))


def concrete_floor():
    base = 0.55 + (periodic_noise(4, 4) - 0.5) * 0.16
    fine = (periodic_noise(32, 2) - 0.5) * 0.05
    g = base + fine
    # scuffs: short low-contrast streaks
    img = to_image(g)
    draw = ImageDraw.Draw(img, "RGBA")
    for _ in range(60):
        x, y = rng.integers(0, SIZE), rng.integers(0, SIZE)
        length = rng.integers(20, 120)
        angle = rng.random() * math.pi
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        shade = int(rng.integers(0, 2))
        colour = (20, 20, 20, 40) if shade == 0 else (255, 255, 255, 25)
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                draw.line([(x + ox, y + oy), (x + dx + ox, y + dy + oy)], fill=colour, width=2)
    return img


def steel_panel():
    rows = periodic_noise(64, 3)[:, :1]  # one value per row: the brushed grain
    grain = np.repeat(rows, SIZE, axis=1)
    streaks = (periodic_noise(128, 2) - 0.5)
    streaks = np.repeat(streaks[:, ::8][:, :SIZE // 8], 8, axis=1)[:, :SIZE]
    g = 0.34 + (grain - 0.5) * 0.10 + streaks * 0.04
    # one seam with rivets at a quarter of the tile, every 128 px
    g[254:258, :] -= 0.12
    img = to_image(g, (0.96, 0.98, 1.0))
    draw = ImageDraw.Draw(img)
    for x in range(64, SIZE, 128):
        draw.ellipse([x - 7, 256 - 7, x + 7, 256 + 7], fill=(150, 155, 160), outline=(40, 42, 45))
        draw.ellipse([x - 3, 256 - 5, x + 1, 256 - 1], fill=(220, 224, 228))
    return img


def soil():
    coarse = periodic_noise(6, 3)
    fine = periodic_noise(48, 3)
    g = 0.22 + (coarse - 0.5) * 0.14 + (fine - 0.5) * 0.16
    img = to_image(g, (1.0, 0.82, 0.66))
    draw = ImageDraw.Draw(img)
    for _ in range(140):
        x, y = rng.integers(0, SIZE), rng.integers(0, SIZE)
        w, h = rng.integers(4, 14), rng.integers(2, 6)
        c = int(rng.integers(70, 120))
        draw.rectangle([x, y, x + w, y + h], fill=(c, int(c * 0.75), int(c * 0.55)))
    return img


def gear_emblem():
    """The cult emblem: a white gear on black (ART_LESSONS 1.5), twelve teeth, a hub ring."""
    img = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    draw = ImageDraw.Draw(img)
    cx = cy = SIZE / 2
    outer, inner, teeth = SIZE * 0.44, SIZE * 0.35, 12
    points = []
    steps = teeth * 4
    for i in range(steps):
        a = 2 * math.pi * i / steps
        phase = i % 4
        r = outer if phase in (0, 1) else inner
        # a flat tooth top: two points at the outer radius, two at the inner
        points.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    draw.polygon(points, fill=(255, 255, 255))
    draw.ellipse([cx - SIZE * 0.22, cy - SIZE * 0.22, cx + SIZE * 0.22, cy + SIZE * 0.22], fill=(0, 0, 0))
    draw.ellipse([cx - SIZE * 0.12, cy - SIZE * 0.12, cx + SIZE * 0.12, cy + SIZE * 0.12], fill=(255, 255, 255))
    draw.ellipse([cx - SIZE * 0.05, cy - SIZE * 0.05, cx + SIZE * 0.05, cy + SIZE * 0.05], fill=(0, 0, 0))
    return img


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out, exist_ok=True)
    for name, make in [("concrete_wall", concrete_wall), ("concrete_floor", concrete_floor), ("steel_panel", steel_panel), ("soil", soil), ("gear_emblem", gear_emblem)]:
        img = make()
        path = os.path.join(out, name + ".png")
        img.save(path)
        print(f"[look_textures_procedural] {path} {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
