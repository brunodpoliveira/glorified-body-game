#!/usr/bin/env python3
"""T26 remake look: close-ups of the figures at 2x for the check before Bruno sees the frames (ART_LESSONS 5.4).

  python3 -I tools/remake_closeups.py <shots dir> <out dir>

For each camera and figure: the box of the figure's mask (plus 40 px), cut from the A and the B frame, scaled 2x
(nearest, so no pixel is invented), A left, B right: <out>/closeup_<figure>_<camera>.png.
"""
import os
import sys

import numpy as np
from PIL import Image


def main(folder, out):
    os.makedirs(out, exist_ok=True)
    cams = sorted({f[len("remake_A_"):-4] for f in os.listdir(folder) if f.startswith("remake_A_cam") and f.endswith(".png")})
    for cam in cams:
        for who in ("lucy", "guard"):
            boxes = []
            for p in ("A", "B"):
                mp = os.path.join(folder, f"mask_{p}_{who}_{cam}.png")
                if not os.path.exists(mp):
                    continue
                m = np.asarray(Image.open(mp).convert("L")) > 64
                ys, xs = np.nonzero(m)
                if len(xs):
                    boxes.append((xs.min(), ys.min(), xs.max(), ys.max()))
            if not boxes:
                continue
            x0 = max(0, min(b[0] for b in boxes) - 40)
            y0 = max(0, min(b[1] for b in boxes) - 40)
            x1 = min(1920, max(b[2] for b in boxes) + 40)
            y1 = min(1080, max(b[3] for b in boxes) + 40)
            tiles = []
            for p in ("A", "B"):
                f = os.path.join(folder, f"remake_{p}_{cam}.png")
                if os.path.exists(f):
                    crop = Image.open(f).convert("RGB").crop((x0, y0, x1, y1))
                    tiles.append(crop.resize((crop.width * 2, crop.height * 2), Image.NEAREST))
            if not tiles:
                continue
            sheet = Image.new("RGB", (sum(t.width for t in tiles) + 16 * (len(tiles) - 1), max(t.height for t in tiles)), (32, 32, 32))
            x = 0
            for t in tiles:
                sheet.paste(t, (x, 0))
                x += t.width + 16
            path = os.path.join(out, f"closeup_{who}_{cam}.png")
            sheet.save(path)
            print(f"[remake_closeups] {path} box {x0},{y0}-{x1},{y1}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
