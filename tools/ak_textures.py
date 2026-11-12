#!/usr/bin/env python3
"""T38 (the Arkham Knight look): the procedural maps. 0 tokens, no RFab call.

  python3 -I tools/ak_textures.py

Writes into Assets/_Game/Arkham/Textures/:
- ak_granite_n_soft.png: the black granite normal map at a quarter strength (RB_Granite uses bump 0.25; the sample's
  rain floor graph has no strength input, so the map carries it).
- ak_lens_dirt.png: the dirt on the lens for the bloom (switch 6): soft smudges, specks, two wipe arcs. Black = clean.
- ak_ripple.png: one ring, white on transparent (the leak's ripples, switch 7).
- ak_drip.png: one drop with a tail (the leak's drips, switch 7).
- ak_guard_ms_2k.png: the guard's metal-smoothness map (switch 5): the dark parts (vest, boots, belt) smooth with a
  breakup noise, the light cloth matte. R = metal 0, A = smoothness.
- ak_lucy_ms_2k.png: Lucy's map (switch 5): the gold veins as T26 (metal 0.6), the skin with a wet sheen and a breakup.
- ak_puddle_mask.png: where the planar reflection is strong (the puddles), soft value noise.
- ak_lut_identity_32.png: the neutral 32 x 32 x 32 strip (the start of the grade fit, tools/ak_lut.py).
Each one gets a .meta with a fixed GUID the first time, so the wet floor graph can point at the soft normal map.
"""
import hashlib
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "_Game", "Arkham", "Textures")
TEX = os.path.join(ROOT, "Assets", "_Game", "Remake", "Textures")
CHR = os.path.join(ROOT, "Assets", "_Game", "Remake", "Characters")
rng = np.random.default_rng(38)


def save(arr, name, mode=None):
    path = os.path.join(OUT, name)
    Image.fromarray(np.clip(arr * 255 + 0.5, 0, 255).astype(np.uint8), mode).save(path)
    meta = path + ".meta"
    if not os.path.exists(meta):
        guid = hashlib.md5(("t38/" + name).encode()).hexdigest()
        open(meta, "w").write(f"fileFormatVersion: 2\nguid: {guid}\n")
    print("[ak_textures]", os.path.relpath(path, ROOT))


def noise(h, w, scale, octaves=4):
    """Value noise, summed octaves, 0..1."""
    total = np.zeros((h, w))
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        s = max(1, int(scale * 2 ** o))
        small = rng.random((s + 1, int(s * w / h) + 1))
        up = np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(float) / 255
        total += up * amp
        norm += amp
        amp *= 0.5
    return total / norm


def soft_normal():
    n = np.asarray(Image.open(os.path.join(TEX, "blk_granite_2k_n.png")).convert("RGB")).astype(float) / 255 * 2 - 1
    flat = np.array([0.0, 0.0, 1.0])
    v = flat + (n - flat) * 0.25
    v /= np.linalg.norm(v, axis=-1, keepdims=True)
    save(v * 0.5 + 0.5, "ak_granite_n_soft.png")


def lens_dirt():
    h, w = 576, 1024
    d = np.zeros((h, w))
    yy, xx = np.mgrid[0:h, 0:w]
    for _ in range(70):  # soft smudges
        cx, cy, r = rng.random() * w, rng.random() * h, 12 + rng.random() * 60
        d += np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * r * r))) * rng.uniform(0.05, 0.35)
    for _ in range(260):  # specks
        cx, cy, r = rng.random() * w, rng.random() * h, 1 + rng.random() * 3.5
        d += np.exp(-(((xx - cx) ** 2 + (yy - cy) ** 2) / (2 * r * r))) * rng.uniform(0.3, 1.0)
    for k in range(2):  # wipe arcs
        cx, cy, R = w * (0.3 + 0.4 * k), h * (1.4 - 0.2 * k), h * (0.9 + 0.2 * k)
        ring = np.abs(np.hypot(xx - cx, yy - cy) - R)
        d += np.exp(-ring ** 2 / (2 * 14 ** 2)) * 0.18 * noise(h, w, 3, 3)
    d *= 0.6 + 0.8 * noise(h, w, 4, 3)
    d = np.clip(d, 0, 1)
    img = np.stack([d, d * 0.97, d * 0.92], -1)
    Image.fromarray((img * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2)).save(os.path.join(OUT, "ak_lens_dirt.png"))
    meta = os.path.join(OUT, "ak_lens_dirt.png.meta")
    if not os.path.exists(meta):
        open(meta, "w").write(f"fileFormatVersion: 2\nguid: {hashlib.md5(b't38/ak_lens_dirt.png').hexdigest()}\n")
    print("[ak_textures] ak_lens_dirt.png")


def ripple():
    s = 256
    yy, xx = np.mgrid[0:s, 0:s]
    r = np.hypot(xx - s / 2 + 0.5, yy - s / 2 + 0.5) / (s / 2)
    a = np.exp(-((r - 0.82) ** 2) / (2 * 0.035 ** 2)) + 0.35 * np.exp(-((r - 0.62) ** 2) / (2 * 0.03 ** 2))
    a *= r < 0.98
    save(np.dstack([np.ones((s, s)), np.ones((s, s)), np.ones((s, s)), np.clip(a, 0, 1)]), "ak_ripple.png", "RGBA")


def drip():
    w, h = 32, 128
    yy, xx = np.mgrid[0:h, 0:w]
    x = (xx - w / 2 + 0.5) / (w / 2)
    y = yy / h  # 0 top (the tail) .. 1 bottom (the drop)
    width = 0.12 + 0.5 * y ** 3
    a = np.exp(-(x ** 2) / (2 * width ** 2)) * (0.15 + 0.85 * y ** 2)
    a += np.exp(-((x ** 2) + ((y - 0.9) * 2.5) ** 2) / (2 * 0.18 ** 2))
    save(np.dstack([np.ones((h, w)), np.ones((h, w)), np.ones((h, w)), np.clip(a, 0, 1)]), "ak_drip.png", "RGBA")


def guard_ms():
    b = np.asarray(Image.open(os.path.join(CHR, "guard_basecolor_2k.jpg")).convert("RGB")).astype(float) / 255
    h, w = b.shape[:2]
    lum = b.mean(-1)
    dark = np.clip((0.2 - lum) / 0.12, 0, 1)  # the vest, the boots, the belt (L under about 0.15)
    brk = noise(h, w, 24, 4)
    smooth = 0.22 + dark * (0.48 + 0.22 * (brk - 0.5) * 2)  # cloth about 0.22 (ART_LESSONS 3.8 caps it at 0.25)
    out = np.dstack([np.zeros((h, w)), np.zeros((h, w)), np.zeros((h, w)), np.clip(smooth, 0, 0.92)])
    save(out, "ak_guard_ms_2k.png", "RGBA")


def lucy_ms():
    m = np.asarray(Image.open(os.path.join(CHR, "lucy_c3_ms_2k.png")).convert("RGBA")).astype(float) / 255
    h, w = m.shape[:2]
    vein = m[..., 0] > 0.3
    brk = noise(h, w, 32, 4)
    a = m[..., 3].copy()
    a[~vein] = np.clip(0.55 + 0.25 * (brk[~vein] - 0.5) * 2, 0, 0.85)  # the wet sheen on the doll skin
    out = m.copy()
    out[..., 3] = a
    save(out, "ak_lucy_ms_2k.png", "RGBA")


def puddle_mask():
    s = 512
    n = noise(s, s, 3, 4)
    m = np.clip((n - 0.48) / 0.12, 0, 1)  # about a third of the floor in puddles, soft edges
    save(np.dstack([m, m, m]), "ak_puddle_mask.png")


def identity_lut(n=32):
    r = np.arange(n) / (n - 1)
    img = np.zeros((n, n * n, 3))
    for b in range(n):
        for g in range(n):
            img[n - 1 - g, b * n:(b + 1) * n, 0] = r
            img[n - 1 - g, b * n:(b + 1) * n, 1] = g / (n - 1)
            img[n - 1 - g, b * n:(b + 1) * n, 2] = b / (n - 1)
    save(img, "ak_lut_identity_32.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    soft_normal()
    lens_dirt()
    ripple()
    drip()
    guard_ms()
    lucy_ms()
    puddle_mask()
    identity_lut()
