#!/usr/bin/env python3
"""T26 remake look, restart (2026-11-01): the maps of the BLACK recovery room (act 1 is nigredo, design §13.8).

  python3 tools/remake_maps.py

Source: the T22 tiles (~/MEGA/glorified-body-renders/T22_black_look/tex/, the _1 of each): tile_black, ceiling_grid,
chrome, granite_black, gear_decal. No RFab call.
1. Profile A (the current look): the 1024 pictures as they are (blk_<name>.png). A uses Simple Lit with the colour
   map only, as LookTest2 does.
2. Profile B (switch 4, the richer surfaces): each picture at 2048 (Lanczos, a light unsharp mask), plus a normal map
   (blk_<name>_2k_n.png: the relief from the brightness, the grout sunk) and a URP Lit metallic map
   (blk_<name>_2k_ms.png: R metal, A smoothness: the glaze of a tile against its grout, polished granite, chrome).
   The 2048 maps carry no new painted detail: the extra comes from the normal and smoothness maps and the decals.
3. A black leather (the straps, the mattress), A and B.
4. The decals of B, only what a clean, cared-for ward has: the scorch where the straps burned through, the soot on
   the mattress, a mop haze on the polished floor, rubber scuffs by the door. Plus the contact blob and the dust mote.
5. The prop colours for the black room (Props/<prop>_base_blk.jpg): the locker black enamel with its white gear, the
   planter black granite with the green fern kept, the chair and the tray stainless (their own shading kept).
Writes to Assets/_Game/Remake/Textures/ and Assets/_Game/Remake/Props/. Deterministic (fixed seeds).
"""
import os

import numpy as np
from PIL import Image, ImageFilter

TOP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
T22 = os.path.expanduser("~/MEGA/glorified-body-renders/T22_black_look/tex")
OUT = os.path.join(TOP, "Assets/_Game/Remake/Textures")
PROPS = os.path.join(TOP, "Assets/_Game/Remake/Props")
os.makedirs(OUT, exist_ok=True)


def box_blur(a, r):
    """A separable box blur with wrap (the textures tile)."""
    if r < 1:
        return a
    out = a.copy()
    for axis in (0, 1):
        c = np.cumsum(np.concatenate([out.take(range(-r - 1, 0), axis=axis), out, out.take(range(0, r), axis=axis)], axis=axis), axis=axis)
        n = out.shape[axis]
        out = (c.take(range(2 * r + 1, 2 * r + 1 + n), axis=axis) - c.take(range(0, n), axis=axis)) / (2 * r + 1)
    return out


def gauss(a, r):
    return box_blur(box_blur(box_blur(a, r), r), r)


def lum_of(rgb):
    return rgb[..., 0] * 0.299 + rgb[..., 1] * 0.587 + rgb[..., 2] * 0.114


def normal_from_height(h, strength):
    dx = (np.roll(h, -1, axis=1) - np.roll(h, 1, axis=1)) * 0.5
    dy = (np.roll(h, -1, axis=0) - np.roll(h, 1, axis=0)) * 0.5
    nx, ny, nz = -dx * strength, dy * strength, np.ones_like(h)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / length, ny / length, nz / length], axis=-1)
    return Image.fromarray(np.clip((n * 0.5 + 0.5) * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB")


def save_rgb(arr, folder, name):
    Image.fromarray(np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGB").save(os.path.join(folder, name), optimize=True)
    print(f"[remake_maps] {name}")


def save_rgba(arr, name):
    Image.fromarray(np.clip(arr * 255.0 + 0.5, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name), optimize=True)
    print(f"[remake_maps] {name}")


def fbm(shape, seed, octaves=6, base=8):
    rng = np.random.default_rng(seed)
    h, w = shape
    total = np.zeros(shape, np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        cells = base * (2 ** o)
        grid = rng.random((cells + 1, cells + 1)).astype(np.float32)
        img = Image.fromarray((grid * 255).astype(np.uint8), "L").resize((w, h), Image.BICUBIC)
        total += amp * (np.asarray(img, np.float32) / 255.0)
        norm += amp
        amp *= 0.5
    return total / norm


def radial(shape, cx=0.5, cy=0.5, sx=0.5, sy=0.5):
    h, w = shape
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    return np.sqrt(((x / w - cx) / sx) ** 2 + ((y / h - cy) / sy) ** 2)


# The first strengths (6, 5) drew every grout line as a black groove: the slope at a 3 px grout edge lay nearly flat.
# name: (T22 file, normal strength, high-pass radius at 2048, metal, smoothness of the face, of the joint, joint is bright?)
SURFACES = {
    "tile": ("tile_black_1.png", 0.25, 40, 0.0, 0.93, 0.30, True),        # glazed black tile, white grout (bright = joint)
    "granite": ("granite_black_1.png", 0.15, 24, 0.0, 0.90, 0.80, False),  # polished black granite: a mirror floor
    "ceiling": ("ceiling_grid_1.png", 0.4, 40, 0.0, 0.35, 0.15, False),   # matte black ceiling tiles, a darker grid
    "chrome": ("chrome_1.png", 0.3, 12, 1.0, 0.86, 0.70, False),          # brushed chrome
}


def surfaces():
    for name, (src, strength, hp, metal, s_face, s_joint, joint_bright) in SURFACES.items():
        img = Image.open(os.path.join(T22, src)).convert("RGB")
        img.save(os.path.join(OUT, f"blk_{name}.png"), optimize=True)
        print(f"[remake_maps] blk_{name}.png (A, 1024)")
        big = img.resize((2048, 2048), Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2))
        big.save(os.path.join(OUT, f"blk_{name}_2k.png"), optimize=True)
        rgb = np.asarray(big, np.float32) / 255.0
        lum = lum_of(rgb)
        local = gauss(lum, hp)
        # the joint: brighter (grout) or darker (a grid) than the local mean by a clear step
        step = (lum - local) if joint_bright else (local - lum)
        joint = gauss(np.clip((step - 0.04) / 0.08, 0.0, 1.0), 1)
        # height: the faces up, the joints down (a sunk grout line, a recessed grid); plus the fine relief
        height = -joint * 0.6 + (gauss(lum, 1) - local) * (0.15 if joint_bright else 0.5)
        normal_from_height(height, strength * 30.0).save(os.path.join(OUT, f"blk_{name}_2k_n.png"), optimize=True)
        print(f"[remake_maps] blk_{name}_2k_n.png")
        var = fbm(lum.shape, 7 + len(name), octaves=5, base=4)
        smooth = (s_face * (1.0 - joint) + s_joint * joint) * (0.95 + 0.08 * var)
        ms = np.zeros(lum.shape + (4,), np.float32)
        ms[..., 0] = metal
        ms[..., 1] = metal
        ms[..., 2] = metal
        ms[..., 3] = np.clip(smooth, 0.0, 1.0)
        save_rgba(ms, f"blk_{name}_2k_ms.png")
        print(f"[remake_maps] {name}: joint fraction {joint.mean():.3f}, smoothness {smooth.mean():.2f}, mean colour {rgb.reshape(-1, 3).mean(0).round(3)}")
    # The gear: white on black. The alpha keeps the gear only (a cutout on the tiles, as on the board).
    gear = Image.open(os.path.join(T22, "gear_decal_1.png")).convert("RGB")
    for size, name in ((1024, "blk_gear.png"), (2048, "blk_gear_2k.png")):
        g = gear.resize((size, size), Image.LANCZOS)
        alpha = g.convert("L").point(lambda v: 255 if v > 110 else 0)
        rgba = g.copy()
        rgba.putalpha(alpha)
        rgba.save(os.path.join(OUT, name), optimize=True)
    print("[remake_maps] blk_gear.png, blk_gear_2k.png")


def leather():
    """Black leather: a fine pebble grain, creases. A 1024 colour map, B 2048 colour plus normal and smoothness."""
    for size, tag in ((1024, ""), (2048, "_2k")):
        grain = fbm((size, size), 71, octaves=4, base=size // 16)
        crease = fbm((size, size), 72, octaves=5, base=6)
        lum = 0.045 + 0.02 * grain + 0.015 * crease
        save_rgb(np.stack([lum * 1.05, lum * 0.98, lum * 0.92], -1), OUT, f"blk_leather{tag}.png")
        if tag:
            normal_from_height(grain * 0.5 + crease * 0.8, 18.0).save(os.path.join(OUT, f"blk_leather{tag}_n.png"), optimize=True)
            ms = np.zeros((size, size, 4), np.float32)
            ms[..., 3] = 0.55 + 0.15 * crease - 0.1 * grain
            save_rgba(ms, f"blk_leather{tag}_ms.png")


def decals():
    s = 1024
    # The scorch where a strap burned through: a charred core, a brown halo, soot sprayed out.
    n = fbm((s, s), 81, base=4)
    d = radial((s, s), sx=0.42, sy=0.42) + (n - 0.5) * 0.55
    core = np.clip((0.45 - d) / 0.25, 0, 1)
    halo = np.clip((1.0 - d) / 0.55, 0, 1) ** 1.5
    a = np.clip(core * 0.95 + halo * 0.55, 0, 1) * (0.75 + 0.25 * fbm((s, s), 82, base=24))
    t = core[..., None]
    rgb = np.array([0.02, 0.015, 0.01]) * t + np.array([0.16, 0.08, 0.03]) * (1 - t)
    save_rgba(np.concatenate([rgb, a[..., None]], -1), "decal_scorch.png")

    # A mop haze on the polished floor: wide soft arcs, very faint (the floor is cleaned every day).
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    rng = np.random.default_rng(91)
    a = np.zeros((s, s), np.float32)
    for _ in range(9):
        cx, cy, r = rng.uniform(0, s), rng.uniform(0, s), rng.uniform(200, 520)
        dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        a += np.exp(-((dist - r) / rng.uniform(25, 60)) ** 2) * rng.uniform(0.3, 0.6)
    a = np.clip(a, 0, 1) * 0.16 * (0.6 + 0.4 * fbm((s, s), 92, base=8)) * np.clip(1.15 - radial((s, s)), 0, 1)
    save_rgba(np.concatenate([np.ones((s, s, 3), np.float32) * 0.42, a[..., None]], -1), "decal_mop.png")

    # Rubber scuffs by the door: a few short dark arcs (the wheels of a trolley).
    a = np.zeros((s, s), np.float32)
    rng = np.random.default_rng(51)
    for _ in range(9):
        cx, cy, r = rng.uniform(200, s - 200), rng.uniform(200, s - 200), rng.uniform(80, 300)
        th0, span = rng.uniform(0, 6.28), rng.uniform(0.15, 0.5)
        dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        ang = (np.arctan2(yy - cy, xx - cx) - th0) % 6.283
        a += np.exp(-((dist - r) / rng.uniform(1.5, 3.5)) ** 2) * (ang < span) * rng.uniform(0.25, 0.5)
    a = np.clip(a, 0, 0.6) * np.clip(1.2 - radial((s, s)), 0, 1)
    save_rgba(np.concatenate([np.ones((s, s, 3), np.float32) * 0.012, a[..., None]], -1), "decal_scuffs.png")

    # The contact blob under a foot (switch 3) and the dust mote (switch 5).
    s2 = 256
    d = radial((s2, s2))
    save_rgba(np.concatenate([np.zeros((s2, s2, 3), np.float32), (np.clip(1.0 - d, 0, 1) ** 1.8 * 0.9)[..., None]], -1), "decal_contact.png")
    s3 = 64
    d = radial((s3, s3))
    save_rgba(np.concatenate([np.ones((s3, s3, 3), np.float32), (np.clip(1 - d, 0, 1) ** 2.5)[..., None]], -1), "dust_mote.png")


def hsv(rgb):
    mx, mn = rgb.max(-1), rgb.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def props():
    # The locker: black enamel; the white gear stays white, its black square stays black.
    rgb = np.asarray(Image.open(os.path.join(PROPS, "cabinet_base.jpg")).convert("RGB"), np.float32) / 255.0
    lum = lum_of(rgb)
    white = np.clip((lum - 0.55) / 0.2, 0, 1)[..., None]
    body = (0.035 + 0.06 * lum)[..., None] * np.array([1.0, 1.0, 1.04])
    save_rgb(body * (1 - white) + rgb * white, PROPS, "cabinet_base_blk.jpg")
    # The planter: the grey concrete becomes polished black granite (the T22 speckle); the green fern stays.
    rgb = np.asarray(Image.open(os.path.join(PROPS, "planter_fern_base.jpg")).convert("RGB"), np.float32) / 255.0
    h, s, v = hsv(rgb)
    green = np.clip((s - 0.18) / 0.12, 0, 1) * ((h > 0.12) & (h < 0.45))
    green = gauss(green.astype(np.float32), 1)[..., None]
    gran = np.asarray(Image.open(os.path.join(T22, "granite_black_1.png")).convert("RGB").resize(rgb.shape[1::-1], Image.LANCZOS), np.float32) / 255.0
    shade = (0.6 + 0.5 * lum_of(rgb))[..., None]
    save_rgb(rgb * green + gran * shade * (1 - green), PROPS, "planter_fern_base_blk.jpg")
    # The chair and the tray: stainless; their painted shading is kept, the colour a cool grey.
    for prop in ("chair", "tray_set"):
        rgb = np.asarray(Image.open(os.path.join(PROPS, f"{prop}_base.jpg")).convert("RGB"), np.float32) / 255.0
        l = lum_of(rgb)[..., None]
        save_rgb(np.clip(l * 1.1, 0, 1) * np.array([0.93, 0.95, 0.98]), PROPS, f"{prop}_base_blk.jpg")


if __name__ == "__main__":
    surfaces()
    leather()
    decals()
    props()
