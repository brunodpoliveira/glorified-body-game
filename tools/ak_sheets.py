#!/usr/bin/env python3
"""T38 (the Arkham Knight look): the sheets and the numbers from the bot "arkham".

  python3 -I tools/ak_sheets.py <shots dir> <out dir> <room: dark|bright> <reference_stats.txt>

Writes into <out dir>:
- BA_<room>_<camera>.png: B-game (left) beside AK all on (right), 1920 x 1080 each, labelled.
- ablation_<room>_<camera>.png: AK all on, AK with each of the eight switches off, B-game; 4 x 3 at full size.
- stats_<room>.txt: the numbers of tests/T38_arkham_look/ak_stats.py for each B-game and AK frame, their median,
  the reference median, and the distance of each to it.
- faults_<room>.txt: the four faults of run 1 (fault 4, the HUD, is checked on the clips), PASS or FAIL against the
  numbers Bruno set.
- contrast_<room>.txt: the T22 Weber contrast of Lucy and the guard against a ring around each (B-game and AK), Lucy's
  gold pixel share (the T26 count), the guard's colour, the walls (the median of the upper third, figures out).
No reference picture is read or copied here: only its numbers, from reference_stats.txt.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

CAMS = ["cam_recovery", "cam_recovery_bed", "cam_recovery_long"]
SWITCHES = ["grade", "rim", "wet", "planar", "specular suits", "practicals", "leak", "lens"]
KEYS = ["p1", "p5", "p50", "p95", "p99", "sh_a", "sh_b", "hi_a", "hi_b", "chroma", "colored", "teal", "redorange"]


def font(size):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", "/usr/share/fonts/TTF/DejaVuSans.ttf"):
        if os.path.exists(f):
            return ImageFont.truetype(f, size)
    return ImageFont.load_default()


def sheet(images, labels, cols, out, pad=8):
    f = font(36)
    w, h = images[0].size
    rows = (len(images) + cols - 1) // cols
    s = Image.new("RGB", (cols * (w + pad) + pad, rows * (h + 60 + pad) + pad), (32, 32, 32))
    d = ImageDraw.Draw(s)
    for i, (im, lab) in enumerate(zip(images, labels)):
        x, y = pad + (i % cols) * (w + pad), pad + (i // cols) * (h + 60 + pad)
        s.paste(im, (x, y))
        d.text((x + 10, y + h + 10), lab, fill=(235, 235, 235), font=f)
    s.save(out)
    print(f"[ak_sheets] {out}")


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lab(lin):
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def ak_stats(path):
    """The same numbers as tests/T38_arkham_look/ak_stats.py (640 wide)."""
    im = Image.open(path).convert("RGB")
    w, h = im.size
    im = im.resize((640, int(640 * h / w)))
    lin = srgb_to_lin(np.asarray(im, dtype=np.float64) / 255).reshape(-1, 3)
    Y = lin @ np.array([0.2126, 0.7152, 0.0722])
    L = lab(lin)
    p = np.percentile(Y, [1, 5, 50, 95, 99])
    sh, hi = Y <= np.percentile(Y, 25), Y >= np.percentile(Y, 95)
    C = np.hypot(L[:, 1], L[:, 2])
    hue = (np.degrees(np.arctan2(L[:, 2], L[:, 1])) + 360) % 360
    col = C > 10
    teal = np.mean((hue[col] >= 150) & (hue[col] <= 230)) if col.any() else 0
    red = np.mean((hue[col] >= 345) | (hue[col] <= 45)) if col.any() else 0
    return dict(p1=p[0], p5=p[1], p50=p[2], p95=p[3], p99=p[4], sh_a=L[sh, 1].mean(), sh_b=L[sh, 2].mean(),
                hi_a=L[hi, 1].mean(), hi_b=L[hi, 2].mean(), chroma=C.mean(), colored=col.mean(), teal=teal, redorange=red)


def reference_median(path):
    for line in open(path):
        if line.startswith("MEDIAN"):
            return dict(zip(KEYS, [float(v) for v in line.split()[1:]]))
    raise SystemExit("no MEDIAN line in " + path)


def distance(s, ref):
    """One number: luminance on a log scale (p5, p50, p95, p99), the chroma / 3, the four tints / 2."""
    e = sum((np.log(s[k] + 0.002) - np.log(ref[k] + 0.002)) ** 2 for k in ("p5", "p50", "p95", "p99"))
    e += ((s["chroma"] - ref["chroma"]) / 3) ** 2 + sum(((s[k] - ref[k]) / 2) ** 2 for k in ("sh_a", "sh_b", "hi_a", "hi_b"))
    return float(np.sqrt(e))


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


def contrast(render, masks):
    """T26's contrast line (tools/remake_game_sheets.py): the T22 Weber contrast, the gold, the guard, the walls."""
    a = rgb(render)
    L = 0.2126 * a[..., 0] + 0.7152 * a[..., 1] + 0.0722 * a[..., 2]
    anyfig = np.zeros(L.shape, bool)
    for m in masks.values():
        anyfig |= m
    out, parts = {}, []
    for who, m in masks.items():
        grown = np.asarray(Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(25))) > 128
        ring = grown & ~anyfig
        lf, ls = L[m].mean(), L[ring].mean()
        weber = (lf - ls) / max(ls, 1e-3)
        col = [int(c) for c in (a[m].mean(0) * 255).round()]
        h, s, v = hsv(a[m])
        out[who] = weber
        out[who + "_mean"] = np.array(col, float)
        out[who + "_bmr"] = col[2] - col[0]
        out[who + "_over6"] = float((L[m] > 0.6).mean() * 100)
        out[who + "_over9"] = float((L[m] > 0.9).mean() * 100)
        if who == "guard":
            extra = f"rgb {tuple(col)} blue-red {col[2] - col[0]:+d} white {(L[m] > 0.6).mean() * 100:.1f}%"
        else:
            gold = ((h > 0.08) & (h < 0.17) & (s > 0.3) & (v > 0.25)).mean() * 100
            out["gold"] = gold
            extra = f"rgb {tuple(col)} gold {gold:.1f}%"
        parts.append(f"{who}: fig {lf:.3f} bg {ls:.3f} weber {weber:+.2f} | {extra}")
    up = L[: L.shape[0] // 3][~anyfig[: L.shape[0] // 3]]
    out["walls"] = float(np.median(up))
    parts.append(f"upper third median {out['walls']:.3f}")
    return " || ".join(parts), out


def main(folder, out, room, refpath):
    os.makedirs(out, exist_ok=True)
    ref = reference_median(refpath)
    slines = ["T38 " + room + " room: ak_stats.py numbers (linear luminance; Lab a*, b*; chroma). Reference = the median of the 19 Arkham Knight shots.",
              "frame".ljust(30) + "".join(k.rjust(9) for k in KEYS) + "  distance"]
    slines.append("REFERENCE MEDIAN".ljust(30) + "".join(f"{ref[k]:9.3f}" for k in KEYS) + "      0.00")
    clines = []
    per = {"BG": [], "AK": []}
    for cam in CAMS:
        bg, ak = os.path.join(folder, f"ak_BG_{cam}.png"), os.path.join(folder, f"ak_AK_{cam}.png")
        if not (os.path.exists(bg) and os.path.exists(ak)):
            continue
        sheet([Image.open(bg).convert("RGB"), Image.open(ak).convert("RGB")],
              [f"B-game: Bruno's pick, the control ({room} room)", f"AK: B-game + the eight switches ({room} room)"], 2,
              os.path.join(out, f"BA_{room}_{cam}.png"))
        tiles, labels = [Image.open(ak).convert("RGB")], ["AK all on"]
        for i, name in enumerate(SWITCHES, 1):
            f = os.path.join(folder, f"ak_AKoff{i}_{cam}.png")
            if os.path.exists(f):
                tiles.append(Image.open(f).convert("RGB"))
                labels.append(f"off: {i} {name}" + (" (no leak in this room)" if name == "leak" and room == "bright" else ""))
        tiles.append(Image.open(bg).convert("RGB"))
        labels.append("all eight off = B-game")
        sheet(tiles, labels, 4, os.path.join(out, f"ablation_{room}_{cam}.png"))
        for prof, f in (("BG", bg), ("AK", ak)):
            s = ak_stats(f)
            per[prof].append(s)
            slines.append(f"{prof} {cam}".ljust(30) + "".join(f"{s[k]:9.3f}" for k in KEYS) + f"{distance(s, ref):10.2f}")
        for prof in ("BG", "AK"):
            masks = {w: mask(os.path.join(folder, f"mask_{prof}_{w}_{cam}.png")) for w in ("lucy", "guard")}
            line, nums = contrast(os.path.join(folder, f"ak_{prof}_{cam}.png"), masks)
            clines.append(f"{prof} {cam}: {line}")
            per.setdefault(prof + "_c", []).append(nums)
    for prof in ("BG", "AK"):
        if per[prof]:
            m = {k: float(np.median([r[k] for r in per[prof]])) for k in KEYS}
            slines.append(f"{prof} MEDIAN (3 cameras)".ljust(30) + "".join(f"{m[k]:9.3f}" for k in KEYS) + f"{distance(m, ref):10.2f}")
    # The four faults of run 1, each against its pass number (B-game is the reference).
    flines = [f"T38 run 2, {room} room: the four faults of run 1 against their pass numbers"]
    for cam, bg, ak in zip(CAMS, per.get("BG_c", []), per.get("AK_c", [])):
        d = float(np.abs(ak["guard_mean"] - bg["guard_mean"]).max())
        checks = [
            ("1 guard mean within 15 levels of B-game", d <= 15, f"{ak['guard_mean'].astype(int).tolist()} vs {bg['guard_mean'].astype(int).tolist()} (max diff {d:.0f})"),
            ("1 guard blue over red +6 to +12", 6 <= ak["guard_bmr"] <= 12, f"{ak['guard_bmr']:+.0f} (B-game {bg['guard_bmr']:+.0f})"),
            ("1 guard pixels over 0.6 under 10 %", ak["guard_over6"] < 10, f"{ak['guard_over6']:.1f} % (B-game {bg['guard_over6']:.1f} %)"),
            ("2 Lucy pixels over 0.9 under 5 %", ak["lucy_over9"] < 5, f"{ak['lucy_over9']:.1f} % (B-game {bg['lucy_over9']:.1f} %)"),
            ("2 Lucy gold at 80 % of B-game or more", ak["gold"] >= 0.8 * bg["gold"], f"{ak['gold']:.1f} % vs {bg['gold']:.1f} % ({ak['gold'] / max(bg['gold'], 1e-6) * 100:.0f} %)"),
            ("3 walls at most B-game + 0.01", ak["walls"] <= bg["walls"] + 0.01, f"{ak['walls']:.3f} vs {bg['walls']:.3f} ({ak['walls'] - bg['walls']:+.3f})"),
        ]
        for name, ok, detail in checks:
            flines.append(f"{cam.ljust(18)} {'PASS' if ok else 'FAIL'}  {name}: {detail}")
    open(os.path.join(out, f"faults_{room}.txt"), "w").write("\n".join(flines) + "\n")
    print("\n".join(flines))
    slines.append("distance: sqrt of the sum of (log luminance p5, p50, p95, p99)^2, ((chroma)/3)^2, ((tints)/2)^2 against the reference median. Lower is closer.")
    open(os.path.join(out, f"stats_{room}.txt"), "w").write("\n".join(slines) + "\n")
    open(os.path.join(out, f"contrast_{room}.txt"), "w").write("\n".join(clines) + "\n")
    print("\n".join(slines))
    print("\n".join(clines))


if __name__ == "__main__":
    main(*sys.argv[1:5])
