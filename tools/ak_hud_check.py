#!/usr/bin/env python3
"""T38 fault 4: does the HUD in the AK walk clip match the HUD in the B-game walk clip?

  python3 -I tools/ak_hud_check.py <BG walk.mp4> <AK walk.mp4> <out.png> [seconds ...]

For each time (default 1, 2.5, 4 s) it takes the frame of each clip. The pass number: the two opaque bars (HP, MK) have
the same pixels in both clips (the mean difference, 0..255). It also looks at the two HUD boxes (the bars at the
bottom left, the power and capsule text at the bottom right). The HUD pixels are the ones that are bright in both
frames (luma over 0.45: the bar fills, the white bar, the text). It prints the mean and the 99th percentile of the
absolute difference (0..255) on those pixels and the colour fringe (the mean of max - min channel on the white HUD
pixels over 0.6, which a chromatic aberration raises). Writes a sheet of the two HUD boxes at 2x, B-game above AK.
"""
import subprocess
import sys

import numpy as np
from PIL import Image

BOXES = [(0, 860, 520, 1080), (1040, 960, 1920, 1080)]  # x0, y0, x1, y1 at 1920 x 1080
# The two opaque bars of the HUD (the HP bar, the MK bar): their pixels are HUD only, no scene shows through.
BARS = {"HP bar": (944, 953, 140, 440), "MK bar": (981, 989, 140, 280)}  # y0, y1, x0, x1


def frame(path, t):
    raw = subprocess.run(["ffmpeg", "-loglevel", "error", "-ss", str(t), "-i", path, "-frames:v", "1", "-f", "rawvideo",
                          "-pix_fmt", "rgb24", "-"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, np.uint8).reshape(1080, 1920, 3).astype(np.float64)


def main(bg_clip, ak_clip, out, times):
    rows, tiles = [], []
    for t in times:
        bg, ak = frame(bg_clip, t), frame(ak_clip, t)
        for name, (y0, y1, x0, x1) in BARS.items():
            d = np.abs(ak[y0:y1, x0:x1] - bg[y0:y1, x0:x1])
            rows.append(f"t {t:.1f} s {name}: B-game {bg[y0:y1, x0:x1].mean((0, 1)).round().astype(int).tolist()} AK "
                        f"{ak[y0:y1, x0:x1].mean((0, 1)).round().astype(int).tolist()}, diff mean {d.mean():.1f} p99 {np.percentile(d, 99):.0f} levels")
        for i, (x0, y0, x1, y1) in enumerate(BOXES):
            b, a = bg[y0:y1, x0:x1], ak[y0:y1, x0:x1]
            lb, la = b @ [0.2126, 0.7152, 0.0722] / 255, a @ [0.2126, 0.7152, 0.0722] / 255
            hud = (lb > 0.45) & (la > 0.45)
            d = np.abs(a - b)[hud]
            white = hud & (lb > 0.6)
            fb = (b.max(-1) - b.min(-1))[white].mean() if white.any() else 0
            fa = (a.max(-1) - a.min(-1))[white].mean() if white.any() else 0
            rows.append(f"t {t:.1f} s box {i}: {hud.sum()} HUD pixels, diff mean {d.mean():.1f} p99 {np.percentile(d, 99):.0f} levels; "
                        f"white fringe B-game {fb:.1f}, AK {fa:.1f}")
            if t == times[len(times) // 2]:
                tiles += [Image.fromarray(b.astype(np.uint8)).resize(((x1 - x0) * 2, (y1 - y0) * 2), Image.NEAREST),
                          Image.fromarray(a.astype(np.uint8)).resize(((x1 - x0) * 2, (y1 - y0) * 2), Image.NEAREST)]
    w = max(t.width for t in tiles)
    sheet = Image.new("RGB", (w * 2 + 12, sum(t.height for t in tiles[::2]) + 12 * len(tiles) // 2), (32, 32, 32))
    y = 0
    for k in range(0, len(tiles), 2):
        sheet.paste(tiles[k], (0, y))
        sheet.paste(tiles[k + 1], (w + 12, y))
        y += tiles[k].height + 12
    sheet.save(out)
    print("\n".join(rows))
    print(f"[ak_hud_check] {out} (left B-game, right AK)")


if __name__ == "__main__":
    ts = [float(x) for x in sys.argv[4:]] or [1.0, 2.5, 4.0]
    main(sys.argv[1], sys.argv[2], sys.argv[3], ts)
