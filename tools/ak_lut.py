#!/usr/bin/env python3
"""T38 (the Arkham Knight look), switch 1: fit the grade's LUT to the colour numbers of the Arkham Knight references.

  python3 -I tools/ak_lut.py <room> <shots dir> [--out Assets/_Game/Arkham/Textures/ak_lut_<room>.png] [--params a,b,...]

Input: the bot's ak_AKprelut_<cam>.png frames (profile AK, all eight switches on, the LUT out) and the masks
mask_AK_lucy_<cam>.png. URP applies a user LUT after the tone map, on the sRGB-encoded value, with trilinear
interpolation of a 32 x 32 x 32 grid, so applying the grid here to the PNG gives the same frame as the game, short of
the grain that comes after it.

The LUT is a function with twelve numbers: a gain and a contrast around 0.05 for the mid-tones, a lift (a floor in the
shadow tint's colour), a highlight curve (a boost above a knee) and a shoulder (the roll-off), the saturation, and two
tints (red-green, yellow-blue) for the shadows and two for the highlights, and a global warm balance. The tints
multiply, so black stays black. A small Nelder-Mead (three starts) fits the numbers to the targets (the median of the
19 reference shots, tests/T38_arkham_look/reference_stats.txt): luminance p5, p50, p95, p99 on a log scale, the mean
chroma, the shadow a*, b* and the highlight a*, b*. The four faults of run 1 are guards against the B-game frames of
the same run (ak_BG_<cam>.png, mask_BG_*): the guard's mean colour within 15 levels, his blue over red +6 to +12, under
10 % of his pixels over 0.6; under 5 % of Lucy's pixels over 0.9; the walls (the upper third's median luma, figures
out) at most 0.01 over B-game; Lucy's gold at 80 % or more of the B-game count.
Writes the LUT strip (1024 x 32, linear) and prints the numbers before and after for each frame.
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGET = dict(p5=0.003, p50=0.026, p95=0.426, p99=0.750, chroma=7.413, sh_a=-1.735, sh_b=-0.013, hi_a=-1.870, hi_b=3.373)
N = 32


def srgb_to_lin(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * c ** (1 / 2.4) - 0.055)


def lab(lin):
    M = np.array([[0.4124, 0.3576, 0.1805], [0.2126, 0.7152, 0.0722], [0.0193, 0.1192, 0.9505]])
    xyz = lin @ M.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 0.008856, np.cbrt(xyz), 7.787 * xyz + 16 / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], -1)


def stats(srgb):
    """ak_stats.py's numbers on an sRGB float image (already at 640 wide)."""
    lin = srgb_to_lin(srgb).reshape(-1, 3)
    Y = lin @ np.array([0.2126, 0.7152, 0.0722])
    L = lab(lin)
    p = np.percentile(Y, [5, 50, 95, 99])
    sh, hi = Y <= np.percentile(Y, 25), Y >= np.percentile(Y, 95)
    C = np.hypot(L[:, 1], L[:, 2])
    return dict(p5=p[0], p50=p[1], p95=p[2], p99=p[3], sh_a=L[sh, 1].mean(), sh_b=L[sh, 2].mean(),
                hi_a=L[hi, 1].mean(), hi_b=L[hi, 2].mean(), chroma=C.mean())


def gold_share(srgb, mask):
    """T26's count (remake_game_sheets.py): hue 0.08-0.17, saturation over 0.3, value over 0.25, in Lucy's mask."""
    a = srgb[mask]
    mx, mn = a.max(-1), a.min(-1)
    d = np.maximum(mx - mn, 1e-6)
    r, g, b = a[:, 0], a[:, 1], a[:, 2]
    h = np.where(mx == r, ((g - b) / d) % 6, np.where(mx == g, (b - r) / d + 2, (r - g) / d + 4)) / 6.0
    s = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
    return float(((h > 0.08) & (h < 0.17) & (s > 0.3) & (mx > 0.25)).mean() * 100) if len(a) else 0.0


def luma(srgb):
    """The luma of the sRGB values, 0..1, as the T26 contrast script (remake_game_sheets.py) measures the walls."""
    return srgb @ np.array([0.2126, 0.7152, 0.0722])


def wall(srgb, figures):
    """The walls: the median luma of the upper third of the frame, the figures out (the T26 check)."""
    L = luma(srgb)
    top = L[: L.shape[0] // 3][~figures[: L.shape[0] // 3]]
    return float(np.median(top))


def figure(srgb, m):
    """Mean colour (0..255), blue over red, the share of luma over 0.6 and over 0.9 inside a figure mask."""
    a = srgb[m]
    if len(a) == 0:
        return dict(mean=np.zeros(3), bmr=0.0, over6=0.0, over9=0.0)
    L = luma(a)
    mean = a.mean(0) * 255
    return dict(mean=mean, bmr=float(mean[2] - mean[0]), over6=float((L > 0.6).mean() * 100), over9=float((L > 0.9).mean() * 100))


NAMES = ["gain", "contrast", "lift", "hi_boost", "knee", "shoulder", "sat", "sh_ka", "sh_kb", "hi_ka", "hi_kb", "warm"]
START = np.array([1.0, 1.0, 0.002, 1.0, 0.25, 0.4, 1.15, -0.10, 0.0, -0.05, 0.08, 0.04])


def grade(srgb, x):
    gain, con, lift, hb, knee, shoulder, sat, ska, skb, hka, hkb, warm = x
    lin = srgb_to_lin(np.clip(srgb, 0, 1))
    lum = lin @ np.array([0.2126, 0.7152, 0.0722])
    # tone (on the luminance, the colour ratio kept): a gain and a contrast for the mid-tones, then the highlight
    # curve (the lift above the knee, run 2: the brightness lift out of the global exposure), then the roll-off
    y = lum * max(gain, 0.05)
    piv = 0.05
    y = piv * np.power(np.maximum(y, 1e-7) / piv, max(con, 0.3))
    k = min(max(knee, 0.02), 0.9)
    t = np.clip((y - k) / (3 * k), 0, 1)
    y = y * (1 + max(hb, 0) * t * t * (3 - 2 * t))
    sh = max(shoulder, 0)
    y = y * (1 + sh) / (1 + y * sh)
    ratio = np.where(lum > 1e-7, y / np.maximum(lum, 1e-7), 0)
    out = lin * ratio[..., None]
    # a global warm balance (run 2: ACES left the guard bluer than B-game, and every lift scaled his blue over red up)
    out = out * np.array([1 + warm, 1.0, 1 - warm])
    # saturation around the luminance
    l2 = out @ np.array([0.2126, 0.7152, 0.0722])
    out = l2[..., None] + (out - l2[..., None]) * max(sat, 0)
    # the split: shadow tint under 0.015 (the near blacks only: at 0.05 it took the red out of the guard's folds, run 2),
    # highlight tint over 0.1 (display-linear luminance)
    ws = np.clip(1 - l2 / 0.015, 0, 1) ** 2
    wh = np.clip((l2 - 0.1) / 0.4, 0, 1)
    a_dir = np.array([1.0, -1.0, 0.0])    # + red, - green (Lab a*)
    b_dir = np.array([0.5, 0.5, -1.0])    # + yellow, - blue (Lab b*)
    tint = 1 + (ska * a_dir + skb * b_dir) * ws[..., None] + (hka * a_dir + hkb * b_dir) * wh[..., None]
    out = out * np.maximum(tint, 0)
    # the lift: a floor in the shadow tint's colour (the references' black level, p5 0.003, is a tinted black)
    floor = np.maximum(1 + ska * a_dir + skb * b_dir, 0)
    out = out + max(lift, 0) * floor * (1 - np.clip(out / 0.015, 0, 1))
    return lin_to_srgb(out)


def lut_grid(x):
    r = np.arange(N) / (N - 1)
    R, G, B = np.meshgrid(r, r, r, indexing="ij")  # [r, g, b]
    grid = np.stack([R, G, B], -1)
    return grade(grid.reshape(-1, 3), x).reshape(N, N, N, 3)


def apply_lut(srgb, grid):
    """Trilinear, as URP's ApplyLut2D (bilinear in a slice, linear between the blue slices)."""
    p = np.clip(srgb, 0, 1) * (N - 1)
    i0 = np.floor(p).astype(int)
    i0 = np.clip(i0, 0, N - 2)
    f = p - i0
    out = np.zeros_like(srgb)
    for dr in (0, 1):
        for dg in (0, 1):
            for db in (0, 1):
                w = (f[..., 0] if dr else 1 - f[..., 0]) * (f[..., 1] if dg else 1 - f[..., 1]) * (f[..., 2] if db else 1 - f[..., 2])
                out += w[..., None] * grid[i0[..., 0] + dr, i0[..., 1] + dg, i0[..., 2] + db]
    return out


def save_lut(grid, path):
    img = np.zeros((N, N * N, 3))
    for b in range(N):
        for g in range(N):
            img[N - 1 - g, b * N:(b + 1) * N] = grid[:, g, b]
    Image.fromarray(np.clip(img * 255 + 0.5, 0, 255).astype(np.uint8)).save(path)


def median_stats(frames, fn):
    rows = [stats(fn(f)) for f in frames]
    return {k: float(np.median([r[k] for r in rows])) for k in TARGET}, rows


def loss(m):
    e = 0.0
    for k in ("p5", "p50", "p95", "p99"):
        e += (np.log(m[k] + 0.002) - np.log(TARGET[k] + 0.002)) ** 2 * (2.0 if k in ("p50", "p95") else 1.0)
    e += ((m["chroma"] - TARGET["chroma"]) / 3.0) ** 2
    for k in ("sh_a", "sh_b", "hi_a", "hi_b"):
        e += ((m[k] - TARGET[k]) / 2.0) ** 2 * 0.5
    return e


def nelder_mead(f, x0, step, iters=500):
    n = len(x0)
    pts = [x0] + [x0 + np.eye(n)[i] * step[i] for i in range(n)]
    vals = [f(p) for p in pts]
    for _ in range(iters):
        order = np.argsort(vals)
        pts = [pts[i] for i in order]
        vals = [vals[i] for i in order]
        c = np.mean(pts[:-1], axis=0)
        xr = c + (c - pts[-1])
        fr = f(xr)
        if fr < vals[0]:
            xe = c + 2 * (c - pts[-1])
            fe = f(xe)
            pts[-1], vals[-1] = (xe, fe) if fe < fr else (xr, fr)
        elif fr < vals[-2]:
            pts[-1], vals[-1] = xr, fr
        else:
            xc = c + 0.5 * (pts[-1] - c)
            fc = f(xc)
            if fc < vals[-1]:
                pts[-1], vals[-1] = xc, fc
            else:
                pts = [pts[0]] + [pts[0] + 0.5 * (p - pts[0]) for p in pts[1:]]
                vals = [vals[0]] + [f(p) for p in pts[1:]]
    i = int(np.argmin(vals))
    return pts[i], vals[i]


def load(path):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    return np.asarray(im.resize((640, int(640 * h / w)), Image.BILINEAR)).astype(np.float64) / 255


def load_mask(path):
    im = Image.open(path).convert("L")
    w, h = im.size
    return np.asarray(im.resize((640, int(640 * h / w)), Image.NEAREST)) > 64


def guards(graded, masks_l, masks_g, ref):
    """The four faults of run 1 as penalties (0 when every number passes with a small margin)."""
    e, rows = 0.0, []
    for gf, ml, mg, r in zip(graded, masks_l, masks_g, ref):
        gd, lu = figure(gf, mg), figure(gf, ml)
        w = wall(gf, ml | mg)
        row = dict(guard_dmean=float(np.abs(gd["mean"] - r["guard"]["mean"]).max()), guard_bmr=gd["bmr"], guard_over6=gd["over6"],
                   lucy_over9=lu["over9"], lucy_dmean=float(np.abs(lu["mean"] - r["lucy"]["mean"]).max()), wall=w, wall_bg=r["wall"])
        rows.append(row)
        e += max(0.0, row["guard_dmean"] - 12) ** 2 * 1.0            # fault 1: within 15 levels of B-game
        e += (max(0.0, 7.5 - row["guard_bmr"]) ** 2 + max(0.0, row["guard_bmr"] - 10.5) ** 2) * 5.0  # +6 to +12
        e += max(0.0, row["lucy_dmean"] - 25) ** 2 * 0.2              # Lucy keeps her pale skin (no yellow cast)
        e += max(0.0, row["guard_over6"] - 8) ** 2 * 2.0                # under 10 %
        e += max(0.0, row["lucy_over9"] - 3.5) ** 2 * 2.0               # fault 2: under 5 %
        e += max(0.0, w - (r["wall"] + 0.008)) ** 2 * 4e4               # fault 3: B-game + 0.01
    return e, rows


def main():
    room, shots = sys.argv[1], sys.argv[2]
    out = os.path.join(ROOT, "Assets", "_Game", "Arkham", "Textures", f"ak_lut_{room}.png")
    if "--out" in sys.argv:
        out = sys.argv[sys.argv.index("--out") + 1]
    files = sorted(glob.glob(os.path.join(shots, "ak_AKprelut_*.png")))
    cams = [os.path.basename(f)[len("ak_AKprelut_"):-4] for f in files]
    frames = [load(f) for f in files]
    lucy = [load_mask(os.path.join(shots, f"mask_AK_lucy_{c}.png")) for c in cams]
    guard = [load_mask(os.path.join(shots, f"mask_AK_guard_{c}.png")) for c in cams]
    # B-game, the reference of the four guards (its own masks)
    ref = []
    for c in cams:
        bg = load(os.path.join(shots, f"ak_BG_{c}.png"))
        bl, bgm = load_mask(os.path.join(shots, f"mask_BG_lucy_{c}.png")), load_mask(os.path.join(shots, f"mask_BG_guard_{c}.png"))
        ref.append(dict(guard=figure(bg, bgm), lucy=figure(bg, bl), wall=wall(bg, bl | bgm)))
    # gold at full size (thin veins), against the B-game count
    full = [np.asarray(Image.open(f).convert("RGB")).astype(np.float64) / 255 for f in files]
    fullmask = [np.asarray(Image.open(os.path.join(shots, f"mask_AK_lucy_{c}.png")).convert("L")) > 64 for c in cams]
    gold_bg = [gold_share(np.asarray(Image.open(os.path.join(shots, f"ak_BG_{c}.png")).convert("RGB")).astype(np.float64) / 255,
                          np.asarray(Image.open(os.path.join(shots, f"mask_BG_lucy_{c}.png")).convert("L")) > 64) for c in cams]
    print(f"[ak_lut] {room}: {len(frames)} frames; B-game: gold {['%.1f' % g for g in gold_bg]} %, walls {['%.4f' % r['wall'] for r in ref]}, "
          f"guard {[np.round(r['guard']['mean']).astype(int).tolist() for r in ref]} b-r {['%+.0f' % r['guard']['bmr'] for r in ref]}")

    def box(f):
        return (f[0::2, 0::2] + f[1::2, 0::2] + f[0::2, 1::2] + f[1::2, 1::2]) / 4

    # Half size by a 2 x 2 mean (close to ak_stats.py's 640 resize; a plain [::2] kept the grain and the thin grout).
    small = [box(f) for f in frames]
    sl = [m[::2, ::2] for m in lucy]
    sg = [m[::2, ::2] for m in guard]

    def objective(x):
        grid = lut_grid(x)  # through the 32-point grid, as the game samples it
        graded = [apply_lut(f, grid) for f in small]
        rows = [stats(gf) for gf in graded]
        m = {k: float(np.median([r[k] for r in rows])) for k in TARGET}
        g, _ = guards(graded, sl, sg, ref)
        return loss(m) + g + max(0.0, x[6] - 1.4) ** 2 * 50  # the saturation at most 1.4 (2 made the skin yellow)

    starts = (START, np.array([1.2, 0.9, 0.003, 0.5, 0.35, 0.2, 1.1, -0.1, 0.0, -0.05, 0.1, 0.08]),
              np.array([0.9, 1.1, 0.003, 2.0, 0.2, 1.0, 1.2, -0.2, 0.0, 0.0, 0.1, 0.0]))
    best = None
    if "--params" in sys.argv:
        # No fit: these numbers, in the order of NAMES (a hand change after a fit, written down in the fit log).
        x = np.array([float(v) for v in sys.argv[sys.argv.index("--params") + 1].split(",")])
        best = (x, objective(x))
        starts = ()
    for start in starts:
        xs, es = nelder_mead(objective, start.copy(), np.array([0.3, 0.2, 0.002, 0.5, 0.1, 0.3, 0.15, 0.08, 0.08, 0.08, 0.08, 0.04]), iters=400)
        print(f"[ak_lut]   start {np.round(start, 2).tolist()}: loss {es:.3f}")
        if best is None or es < best[1]:
            best = (xs, es)
    x, e = best
    grid = lut_grid(x)
    gold1 = [gold_share(apply_lut(fr, grid), m) for fr, m in zip(full, fullmask)]
    for k in range(10):
        if all(g1 >= 0.8 * g0 for g0, g1 in zip(gold_bg, gold1)):
            break
        x[6] += 0.05  # more saturation until the veins hold
        grid = lut_grid(x)
        gold1 = [gold_share(apply_lut(fr, grid), m) for fr, m in zip(full, fullmask)]
    save_lut(grid, out)
    before, _ = median_stats(frames, lambda f: f)
    after, rows1 = median_stats(frames, lambda f: apply_lut(f, grid))
    _, grows = guards([apply_lut(f, grid) for f in frames], lucy, guard, ref)
    keys = list(TARGET)
    print("[ak_lut] numbers: " + "  ".join(f"{k}={v:.3f}" for k, v in zip(NAMES, x)))
    print("[ak_lut] " + "".ljust(16) + "".join(k.rjust(9) for k in keys))
    print("[ak_lut] " + "target".ljust(16) + "".join(f"{TARGET[k]:9.3f}" for k in keys))
    print("[ak_lut] " + "before (median)".ljust(16) + "".join(f"{before[k]:9.3f}" for k in keys))
    print("[ak_lut] " + "after (median)".ljust(16) + "".join(f"{after[k]:9.3f}" for k in keys))
    for c, r, g in zip(cams, rows1, grows):
        print("[ak_lut]   " + c[:14].ljust(14) + "".join(f"{r[k]:9.3f}" for k in keys)
              + f"  | lucy d{g['lucy_dmean']:.0f} | guard d{g['guard_dmean']:.0f} b-r {g['guard_bmr']:+.0f} >0.6 {g['guard_over6']:.1f}% | lucy >0.9 {g['lucy_over9']:.1f}% | walls {g['wall']:.3f} (B-game {g['wall_bg']:.3f})")
    print(f"[ak_lut] gold after {['%.1f' % g for g in gold1]} % (B-game {['%.1f' % g for g in gold_bg]})")
    print(f"[ak_lut] wrote {os.path.relpath(out, ROOT)} (loss {e:.3f})")


if __name__ == "__main__":
    main()
