#!/usr/bin/env python3
"""Placeholder sounds and cards for the floorplan v2 (2026-11-05, DEMO_PLAN.md §3.4 and cutscene 07b). 0 art, 0 RFab.

Made here with numpy, then ffmpeg to Ogg Vorbis (the helpers of audio_placeholders.py):
- The fixtures (Resources/Audio/Sfx/<event>/): microwave_hum (a loop), microwave_ding, coffee_pump, toilet_flush,
  faucet_run (a loop).
- The flashback of the conference room (Assets/_Game/Cutscenes/sfx/): cs07b_f0 (gravel under tyres, a car door),
  cs07b_f2 (a slow metronome, 4 s: it runs under F2, F3 and the line "Encore."), cs07b_f4 (three ticks for the count,
  then a blow on something that is not padded).
- The cards of the flashback (Assets/_Game/Cutscenes/): cs07b_black.png, and a text card for each image shot (F1, F3,
  F4), the shot's words in small grey type on black, as the scans' first pass.

Usage: python3 tools/fixture_placeholders.py
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from audio_placeholders import RATE, SFX, bandpass, bell, env, loop_seam, lowpass, noise, normalise, place, t, thump, write_ogg  # noqa: E402

GAME = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game")
CUTSCENES = os.path.join(GAME, "Cutscenes")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def tick(rng, loud=1.0):
    """One tick of a wooden metronome: a short knock with a click on top."""
    x = t(0.05)
    knock = np.sin(2 * math.pi * 820 * x) * np.exp(-x / 0.012)
    click = bandpass(noise(len(x), rng), 2500, 6000) * np.exp(-x / 0.004)
    return (knock * 0.8 + click * 0.5) * loud


# ── The fixtures ───────────────────────────────────────────────────

def microwave_hum(rng):
    x = t(2.0)
    hum = sum(np.sin(2 * math.pi * f * x) * a for f, a in ((60, 0.5), (120, 0.35), (180, 0.15), (240, 0.08)))
    fan = lowpass(noise(len(x), rng), 900) * 0.35
    buf = hum * 0.5 + fan
    return normalise(loop_seam(buf, 0.4), 0.5)


def microwave_ding(rng):
    buf = np.zeros(int(RATE * 1.4))
    place(buf, bell(2093, 1.2, 0.35) * 0.6, 0.0)
    place(buf, bell(2637, 1.0, 0.3) * 0.35, 0.02)
    return normalise(buf, 0.7)


def coffee_pump(rng):
    x = t(3.6)
    buf = np.zeros(len(x))
    # The pump: a low buzz that pulses eight times a second for 2.4 s.
    pump = (np.sin(2 * math.pi * 55 * x) + 0.5 * np.sin(2 * math.pi * 110 * x)) * (0.6 + 0.4 * np.sin(2 * math.pi * 8 * x))
    pump *= env(len(x), 0.08, 1.2) * (x < 2.4)
    buf += pump * 0.45 + lowpass(noise(len(x), rng), 400) * 0.12 * (x < 2.4)
    # The drip: short wet plinks while the cup fills, and two after.
    for at in (1.0, 1.35, 1.7, 2.0, 2.3, 2.75, 3.2):
        d = t(0.09)
        plink = np.sin(2 * math.pi * 1900 * d * (1 - 0.3 * d / 0.09)) * np.exp(-d / 0.02)
        place(buf, plink * 0.35, at)
    return normalise(buf, 0.7)


def toilet_flush(rng):
    x = t(3.2)
    rush = bandpass(noise(len(x), rng), 300, 3200)
    swell = np.clip(x / 0.35, 0, 1) * np.exp(-np.maximum(x - 0.6, 0) / 1.1)
    gurgle = bandpass(noise(len(x), rng), 120, 500) * (0.5 + 0.5 * np.sin(2 * math.pi * 6 * x)) * np.clip((x - 1.2) / 0.4, 0, 1) * np.exp(-np.maximum(x - 1.8, 0) / 0.6)
    return normalise(rush * swell + gurgle * 0.8, 0.75)


def faucet_run(rng):
    x = t(2.0)
    water = bandpass(noise(len(x), rng), 800, 6500) * (0.85 + 0.15 * np.sin(2 * math.pi * 3.3 * x))
    splash = bandpass(noise(len(x), rng), 200, 900) * 0.3
    return normalise(loop_seam(water + splash, 0.3), 0.5)


# ── The flashback (cutscene 07b) ───────────────────────────────────

def cs07b_f0(rng):
    """Gravel under tyres, slow, then a heavy car door shuts."""
    x = t(2.5)
    buf = np.zeros(len(x))
    crunch = bandpass(noise(len(x), rng), 250, 2600)
    # The crackle: random short bursts, fewer as the car stops.
    gate = np.zeros(len(x))
    for at in rng.uniform(0.0, 1.7, 260):
        i = int(at * RATE)
        gate[i:i + int(RATE * 0.012)] = rng.uniform(0.4, 1.0) * (1 - at / 1.7) ** 0.5
    buf += crunch * gate * 0.8 + crunch * 0.08 * (x < 1.7) * (1 - x / 1.7)
    # The car door at 1.95 s: a latch click, then the heavy shut.
    place(buf, bandpass(noise(int(RATE * 0.02), rng), 2000, 5000) * np.exp(-t(0.02) / 0.004) * 0.5, 1.93)
    place(buf, thump(70, 0.5, 0.09, 0.6) * 0.9, 1.96)
    place(buf, lowpass(noise(int(RATE * 0.12), rng), 1200) * np.exp(-t(0.12) / 0.03) * 0.6, 1.96)
    return normalise(buf, 0.8)


def cs07b_f2(rng):
    """A metronome, slow: one tick a second for four seconds (F2, F3 and the line)."""
    buf = np.zeros(int(RATE * 4.0))
    for at in (0.0, 1.0, 2.0, 3.0):
        place(buf, tick(rng), at)
    return normalise(buf, 0.7)


def cs07b_f4(rng):
    """The count, three ticks, then the blow: a hard knock on something that is not padded. Nobody cries."""
    buf = np.zeros(int(RATE * 3.0))
    for at in (0.0, 0.8, 1.6):
        place(buf, tick(rng), at)
    x = t(0.25)
    crack = bandpass(noise(len(x), rng), 900, 4000) * np.exp(-x / 0.012)
    body = thump(140, 0.25, 0.05, 0.5)
    place(buf, (crack * 0.9 + body * 0.7), 2.35)
    return normalise(buf, 0.85)


# ── The cards ──────────────────────────────────────────────────────

def card(path, words):
    w, h = 640, 360
    img = Image.new("RGB", (w, h), (0, 0, 0))
    if words:
        d = ImageDraw.Draw(img)
        font = ImageFont.truetype(FONT, 16)
        lines, line = [], ""
        for word in words.split():
            probe = (line + " " + word).strip()
            if d.textlength(probe, font=font) > w - 120:
                lines.append(line)
                line = word
            else:
                line = probe
        lines.append(line)
        y = h // 2 - 11 * len(lines)
        for ln in lines:
            d.text(((w - d.textlength(ln, font=font)) / 2, y), ln, fill=(118, 118, 122), font=font)
            y += 22
    img.save(path)
    print(os.path.relpath(path, GAME), img.size)


def main():
    rng = np.random.default_rng(734)
    for event, make in (("microwave_hum", microwave_hum), ("microwave_ding", microwave_ding), ("coffee_pump", coffee_pump),
                        ("toilet_flush", toilet_flush), ("faucet_run", faucet_run)):
        write_ogg(os.path.join(SFX, event, event + ".ogg"), make(rng))
    for clip, make in (("cs07b_f0", cs07b_f0), ("cs07b_f2", cs07b_f2), ("cs07b_f4", cs07b_f4)):
        write_ogg(os.path.join(CUTSCENES, "sfx", clip + ".ogg"), make(rng))
    os.makedirs(CUTSCENES, exist_ok=True)
    card(os.path.join(CUTSCENES, "cs07b_black.png"), "")
    card(os.path.join(CUTSCENES, "cs07b_f1.png"), "PLACEHOLDER (07b, F1): a gravel drive, from low down, a child's height. The sill of a long black car. "
         "A woman's bare hand with a ring, on a small shoulder in a wool coat. Daylight.")
    card(os.path.join(CUTSCENES, "cs07b_f3.png"), "PLACEHOLDER (07b, F3): a bare wood floor with a white line painted on it. "
         "Two pairs of small feet in white socks on the line, side by side.")
    card(os.path.join(CUTSCENES, "cs07b_f4.png"), "PLACEHOLDER (07b, F4): a small hand around a wooden rod, the knuckles white, the end of the rod wet.")


if __name__ == "__main__":
    main()
