#!/usr/bin/env python3
"""The look test textures made by code: tileable by construction (periodic noise), 2K, one colour map each.
The fallback for the RFab surfaces (2026-10-22, RFAB_GAPS 11) and the source of the drawn pictures: the gear
emblem, the CRT face (a green cursor on dark glass), the light panel (a white diffuser in a frame). Usage:
  tools/look_textures_procedural.py <out_dir> [name ...]
  names: concrete_wall concrete_floor concrete_ceiling steel_panel soil tile_wall tile_floor resin_floor
         vent_grille gear_emblem crt_face light_panel   (default: the three drawn pictures)
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw

SIZE = 2048
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


def holes(g, count, depth, rmin=2, rmax=6):
    for _ in range(count):
        cx, cy, r = rng.integers(0, SIZE), rng.integers(0, SIZE), rng.integers(rmin, rmax)
        yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
        mask = xx * xx + yy * yy <= r * r
        ys = (np.arange(cy - r, cy + r + 1) % SIZE)
        xs = (np.arange(cx - r, cx + r + 1) % SIZE)
        sub = g[np.ix_(ys, xs)]
        sub[mask] -= depth
        g[np.ix_(ys, xs)] = sub


def concrete_wall():
    """Precast panels: a recessed seam grid (two panels across, three down the 2 m tile), tie holes, stains, cracks."""
    base = 0.58 + (periodic_noise(8, 5) - 0.5) * 0.22
    speckle = (rng.random((SIZE, SIZE)) - 0.5) * 0.06
    g = base + speckle
    for y in range(0, SIZE, SIZE // 3):
        g[y:y + 5, :] -= 0.16
        g[y + 5:y + 9, :] += 0.04
    for x in range(0, SIZE, SIZE // 2):
        g[:, x:x + 5] -= 0.16
        g[:, x + 5:x + 9] += 0.04
    for y in range(SIZE // 6, SIZE, SIZE // 3):
        for x in range(SIZE // 8, SIZE, SIZE // 4):
            holes_at(g, x, y, 11, 0.3)
    # water stains that run down from the seams
    stain = periodic_noise(16, 2)
    streaks = np.repeat(stain[:1, :], SIZE, axis=0)
    g -= np.clip(streaks - 0.55, 0, 1) * 0.25 * (np.linspace(0, 1, SIZE)[:, None] ** 0.5)
    holes(g, 140, 0.18)
    return to_image(g, (1.0, 0.99, 0.97))


def holes_at(g, cx, cy, r, depth):
    yy, xx = np.ogrid[-r:r + 1, -r:r + 1]
    mask = xx * xx + yy * yy <= r * r
    ys = (np.arange(cy - r, cy + r + 1) % SIZE)
    xs = (np.arange(cx - r, cx + r + 1) % SIZE)
    sub = g[np.ix_(ys, xs)]
    sub[mask] -= depth
    g[np.ix_(ys, xs)] = sub


def concrete_floor():
    base = 0.55 + (periodic_noise(4, 4) - 0.5) * 0.16
    fine = (periodic_noise(32, 2) - 0.5) * 0.05
    g = base + fine
    g[0:4, :] -= 0.2  # the saw-cut joint grid: one line at the tile edge
    g[:, 0:4] -= 0.2
    img = to_image(g)
    draw = ImageDraw.Draw(img, "RGBA")
    for _ in range(90):
        x, y = rng.integers(0, SIZE), rng.integers(0, SIZE)
        length = rng.integers(40, 240)
        angle = rng.random() * math.pi
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        colour = (20, 20, 20, 40) if int(rng.integers(0, 2)) == 0 else (255, 255, 255, 25)
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                draw.line([(x + ox, y + oy), (x + dx + ox, y + dy + oy)], fill=colour, width=3)
    return img


def concrete_ceiling():
    boards = periodic_noise(64, 2)[:, :1]
    grain = np.repeat(boards, SIZE, axis=1)
    g = 0.5 + (periodic_noise(6, 4) - 0.5) * 0.18 + (grain - 0.5) * 0.08
    for y in range(0, SIZE, SIZE // 8):
        g[y:y + 3, :] -= 0.12
    holes(g, 200, 0.15, 2, 5)
    return to_image(g, (0.98, 0.98, 0.97))


def steel_panel():
    rows = periodic_noise(64, 3)[:, :1]
    grain = np.repeat(rows, SIZE, axis=1)
    streaks = (periodic_noise(128, 2) - 0.5)
    streaks = np.repeat(streaks[:, ::8][:, :SIZE // 8], 8, axis=1)[:, :SIZE]
    g = 0.34 + (grain - 0.5) * 0.10 + streaks * 0.04
    g[SIZE // 4 - 3:SIZE // 4 + 3, :] -= 0.12
    img = to_image(g, (0.96, 0.98, 1.0))
    draw = ImageDraw.Draw(img)
    for x in range(SIZE // 16, SIZE, SIZE // 8):
        y = SIZE // 4
        draw.ellipse([x - 13, y - 13, x + 13, y + 13], fill=(150, 155, 160), outline=(40, 42, 45))
        draw.ellipse([x - 6, y - 10, x + 2, y - 2], fill=(220, 224, 228))
    return img


def soil():
    coarse = periodic_noise(6, 3)
    fine = periodic_noise(48, 3)
    g = 0.22 + (coarse - 0.5) * 0.14 + (fine - 0.5) * 0.16
    img = to_image(g, (1.0, 0.82, 0.66))
    draw = ImageDraw.Draw(img)
    for _ in range(280):
        x, y = rng.integers(0, SIZE), rng.integers(0, SIZE)
        w, h = rng.integers(8, 28), rng.integers(4, 12)
        c = int(rng.integers(70, 120))
        draw.rectangle([x, y, x + w, y + h], fill=(c, int(c * 0.75), int(c * 0.55)))
    return img


def tiles(count, tint, grout=0.12, base=0.9, variation=0.05, chips=0):
    """A square tile grid with a dark grout, a slight shade per tile."""
    cell = SIZE // count
    g = np.full((SIZE, SIZE), base, dtype=np.float32)
    for ty in range(count):
        for tx in range(count):
            shade = (rng.random() - 0.5) * variation
            g[ty * cell:(ty + 1) * cell, tx * cell:(tx + 1) * cell] += shade
    line = max(4, cell // 16)
    for i in range(count):
        g[i * cell:i * cell + line, :] = grout
        g[:, i * cell:i * cell + line] = grout
        g[i * cell + line:i * cell + line + 2, :] -= 0.08  # the shadow under the top edge of each tile
    g += (periodic_noise(32, 2) - 0.5) * 0.03
    holes(g, chips, 0.35, 3, 8)
    return to_image(g, tint)


def tile_wall():
    return tiles(13, (1.0, 1.0, 0.98), chips=30)


def tile_floor():
    return tiles(7, (0.95, 0.95, 0.93), base=0.78, variation=0.04, chips=10)


def resin_floor():
    g = 0.72 + (periodic_noise(3, 3) - 0.5) * 0.08 + (periodic_noise(64, 1) - 0.5) * 0.02
    img = to_image(g, (0.95, 0.98, 0.93))
    draw = ImageDraw.Draw(img, "RGBA")
    for _ in range(40):
        x, y = rng.integers(0, SIZE), rng.integers(0, SIZE)
        length = rng.integers(60, 300)
        angle = rng.random() * math.pi
        dx, dy = math.cos(angle) * length, math.sin(angle) * length
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                draw.line([(x + ox, y + oy), (x + dx + ox, y + dy + oy)], fill=(30, 30, 30, 35), width=4)
    return img


def vent_grille():
    """A louvred grille: horizontal slats with a shadow under each, a frame, four screws."""
    g = np.full((SIZE, SIZE), 0.55, dtype=np.float32)
    slat = SIZE // 24
    for y in range(SIZE // 12, SIZE - SIZE // 12, slat):
        g[y:y + slat // 2, :] = 0.62
        g[y + slat // 2:y + slat, :] = 0.12
    frame = SIZE // 14
    g[:frame, :] = 0.5
    g[-frame:, :] = 0.5
    g[:, :frame] = 0.5
    g[:, -frame:] = 0.5
    img = to_image(g, (0.95, 0.96, 0.98))
    draw = ImageDraw.Draw(img)
    for (x, y) in [(frame // 2, frame // 2), (SIZE - frame // 2, frame // 2), (frame // 2, SIZE - frame // 2), (SIZE - frame // 2, SIZE - frame // 2)]:
        draw.ellipse([x - 22, y - 22, x + 22, y + 22], fill=(120, 122, 125), outline=(40, 42, 45))
        draw.line([(x - 14, y), (x + 14, y)], fill=(40, 42, 45), width=5)
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
        points.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    draw.polygon(points, fill=(255, 255, 255))
    draw.ellipse([cx - SIZE * 0.22, cy - SIZE * 0.22, cx + SIZE * 0.22, cy + SIZE * 0.22], fill=(0, 0, 0))
    draw.ellipse([cx - SIZE * 0.12, cy - SIZE * 0.12, cx + SIZE * 0.12, cy + SIZE * 0.12], fill=(255, 255, 255))
    draw.ellipse([cx - SIZE * 0.05, cy - SIZE * 0.05, cx + SIZE * 0.05, cy + SIZE * 0.05], fill=(0, 0, 0))
    return img


def crt_face():
    """Dark curved glass with scanlines, a faint reflection, one green cursor block near the top left."""
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) / SIZE
    vignette = 1.0 - 0.6 * ((xx - 0.5) ** 2 + (yy - 0.5) ** 2) * 2
    g = 0.07 * vignette + (periodic_noise(64, 1) - 0.5) * 0.01
    g[::6, :] *= 0.6  # scanlines
    rgb = np.stack([g * 0.75, g * 1.0, g * 0.8], axis=-1)
    # a soft reflection of a light on the glass
    rgb += (0.12 * np.exp(-(((xx - 0.3) ** 2) / 0.02 + ((yy - 0.25) ** 2) / 0.01)))[..., None]
    img = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB")
    draw = ImageDraw.Draw(img)
    cx, cy, w, h = int(SIZE * 0.2), int(SIZE * 0.22), SIZE // 40, SIZE // 24
    draw.rectangle([cx, cy, cx + w, cy + h], fill=(90, 255, 110))
    draw.rectangle([cx - 4, cy - 4, cx + w + 4, cy + h + 4], outline=(40, 140, 60), width=3)
    return img


def light_panel():
    """A white diffuser with a fine grid and a grey frame."""
    g = np.full((SIZE, SIZE), 0.96, dtype=np.float32)
    g += (periodic_noise(8, 2) - 0.5) * 0.04
    for i in range(0, SIZE, SIZE // 16):
        g[i:i + 3, :] -= 0.06
        g[:, i:i + 3] -= 0.06
    frame = SIZE // 32
    g[:frame, :] = 0.45
    g[-frame:, :] = 0.45
    g[:, :frame] = 0.45
    g[:, -frame:] = 0.45
    return to_image(g, (1.0, 1.0, 0.99))


MAKERS = {
    "concrete_wall": concrete_wall, "concrete_floor": concrete_floor, "concrete_ceiling": concrete_ceiling,
    "steel_panel": steel_panel, "soil": soil, "tile_wall": tile_wall, "tile_floor": tile_floor,
    "resin_floor": resin_floor, "vent_grille": vent_grille, "gear_emblem": gear_emblem, "crt_face": crt_face,
    "light_panel": light_panel,
}
DRAWN = ["gear_emblem", "crt_face", "light_panel"]


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "."
    names = sys.argv[2:] if len(sys.argv) > 2 else DRAWN
    if names == ["all"]:
        names = list(MAKERS)
    os.makedirs(out, exist_ok=True)
    for name in names:
        img = MAKERS[name]()
        path = os.path.join(out, name + ".png")
        img.save(path)
        print(f"[look_textures_procedural] {path} {img.size[0]}x{img.size[1]}")


if __name__ == "__main__":
    main()
