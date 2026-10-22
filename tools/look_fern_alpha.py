#!/usr/bin/env python3
"""The fern card of the look test: the RFab picture (fronds on black) keyed to alpha, cropped to the plant, padded
to a square. The leaf card in the room is a quad with alpha clip, so the black must go. Usage:
  tools/look_fern_alpha.py <in.png> <out.png>
"""
import sys

import numpy as np
from PIL import Image


def main():
    src, dst = sys.argv[1], sys.argv[2]
    img = Image.open(src).convert("RGB")
    rgb = np.asarray(img).astype(np.float32) / 255.0
    lum = rgb.max(axis=2)
    # The black key: a soft ramp between 0.08 and 0.30 of the brightest channel, so thin stems keep an edge.
    alpha = np.clip((lum - 0.08) / 0.22, 0.0, 1.0)
    # Un-premultiply the dark fringe: push the colour of half-covered pixels toward the leaf colour.
    safe = np.maximum(alpha[..., None], 0.2)
    colour = np.clip(rgb / safe, 0.0, 1.0)
    colour = np.where(alpha[..., None] > 0.02, colour, rgb)
    ys, xs = np.where(alpha > 0.5)
    if len(ys) == 0:
        print("[look_fern_alpha] no plant found")
        sys.exit(1)
    pad = 8
    y0, y1 = max(0, ys.min() - pad), min(alpha.shape[0], ys.max() + pad)
    x0, x1 = max(0, xs.min() - pad), min(alpha.shape[1], xs.max() + pad)
    side = max(y1 - y0, x1 - x0)
    out = np.zeros((side, side, 4), dtype=np.float32)
    oy, ox = (side - (y1 - y0)) // 2, (side - (x1 - x0)) // 2
    out[oy:oy + (y1 - y0), ox:ox + (x1 - x0), :3] = colour[y0:y1, x0:x1]
    out[oy:oy + (y1 - y0), ox:ox + (x1 - x0), 3] = alpha[y0:y1, x0:x1]
    result = Image.fromarray((out * 255).astype(np.uint8), "RGBA")
    if side > 2048:
        result = result.resize((2048, 2048), Image.LANCZOS)
    result.save(dst)
    covered = float((alpha > 0.5).mean())
    print(f"[look_fern_alpha] {dst} {result.size[0]}x{result.size[1]} plant covers {covered:.0%} of the source")


if __name__ == "__main__":
    main()
