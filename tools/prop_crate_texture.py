#!/usr/bin/env python3
"""The placeholder crate face (256 px): planks, a dark frame and a cross brace.

The crate is unlit (Bruno, 2026-10-22: "objects you can throw" stand out with no lighting), so every face has
the same brightness. The dark frame on each face is what shows the edges of the cube.

Usage: python3 tools/prop_crate_texture.py [out.png]
"""
import random
import sys

from PIL import Image, ImageDraw

OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Game/Props/crate_placeholder.png"
SIZE = 256
FRAME = 26
BASE = (176, 128, 80)
DARK = (86, 58, 34)
LINE = (120, 84, 50)

random.seed(7)
img = Image.new("RGB", (SIZE, SIZE), BASE)
d = ImageDraw.Draw(img)

# Planks with a little grain.
plank = (SIZE - 2 * FRAME) // 4
for i in range(4):
    y0 = FRAME + i * plank
    shade = random.randint(-12, 12)
    d.rectangle([0, y0, SIZE, y0 + plank], fill=tuple(max(0, min(255, c + shade)) for c in BASE))
    for _ in range(14):
        gy = y0 + random.randint(3, plank - 3)
        gx = random.randint(0, SIZE - 60)
        d.line([gx, gy, gx + random.randint(30, 90), gy], fill=tuple(c - 18 for c in BASE), width=1)
    d.line([0, y0, SIZE, y0], fill=LINE, width=3)

# The cross brace, then the frame over it.
d.line([FRAME, FRAME, SIZE - FRAME, SIZE - FRAME], fill=DARK, width=22)
d.line([FRAME, FRAME, SIZE - FRAME, SIZE - FRAME], fill=(150, 106, 64), width=14)
d.rectangle([0, 0, SIZE - 1, FRAME], fill=DARK)
d.rectangle([0, SIZE - 1 - FRAME, SIZE - 1, SIZE - 1], fill=DARK)
d.rectangle([0, 0, FRAME, SIZE - 1], fill=DARK)
d.rectangle([SIZE - 1 - FRAME, 0, SIZE - 1, SIZE - 1], fill=DARK)
d.rectangle([4, 4, SIZE - 5, SIZE - 5], outline=(60, 40, 22), width=3)

img.save(OUT)
print(OUT)
