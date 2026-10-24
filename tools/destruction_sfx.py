#!/usr/bin/env python3
"""Placeholder break sounds for the breakable cover (2026-10-24).

Synthesised from noise and decaying tones, so the files are ours (GPL repo) and need no vendor pack.
RFab has no sound-effect generation (RFAB_GAPS.md): replace these with recorded or generated sounds later.

Writes 16-bit mono 44.1 kHz WAV files to Assets/_Game/Breakables/Audio/:
  wood_break, metal_break, glass_break, plaster_break, plastic_break, chip, sparks.
Usage: python3 tools/destruction_sfx.py
"""
import math
import os
import random
import struct
import wave

import numpy as np

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Breakables", "Audio")


def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def env(n, attack, decay):
    x = np.arange(n) / RATE
    a = np.clip(x / max(attack, 1e-4), 0, 1)
    return a * np.exp(-x / decay)


def lowpass(x, cutoff):
    # One-pole low-pass, run twice.
    k = 1 - math.exp(-2 * math.pi * cutoff / RATE)
    for _ in range(2):
        y = np.empty_like(x)
        acc = 0.0
        for i, v in enumerate(x):
            acc += k * (v - acc)
            y[i] = acc
        x = y
    return x


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(n, rng):
    return rng.uniform(-1, 1, n)


def place(buf, clip, at):
    i = int(at * RATE)
    j = min(len(buf), i + len(clip))
    if i < len(buf):
        buf[i:j] += clip[: j - i]


def crack(rng, length, cutoff, decay):
    n = int(RATE * length)
    return lowpass(noise(n, rng), cutoff) * env(n, 0.0005, decay)


def thump(freq, length, decay):
    x = t(length)
    return np.sin(2 * math.pi * freq * x * (1 - 0.3 * x / length)) * np.exp(-x / decay)


def ping(freq, length, decay):
    x = t(length)
    return np.sin(2 * math.pi * freq * x) * np.exp(-x / decay)


def wood(rng):
    buf = np.zeros(int(RATE * 0.9))
    place(buf, 0.9 * crack(rng, 0.12, 3500, 0.02), 0.0)
    place(buf, 0.8 * thump(95, 0.4, 0.09), 0.0)
    for _ in range(9):
        place(buf, rng.uniform(0.2, 0.5) * crack(rng, 0.05, rng.uniform(1500, 4000), 0.008), rng.uniform(0.02, 0.5))
    for _ in range(4):
        place(buf, rng.uniform(0.15, 0.3) * thump(rng.uniform(140, 260), 0.15, 0.03), rng.uniform(0.2, 0.75))
    return buf


def metal(rng):
    buf = np.zeros(int(RATE * 1.0))
    place(buf, 0.7 * crack(rng, 0.08, 6000, 0.015), 0.0)
    for f in (420, 655, 1190, 1730):
        place(buf, 0.18 * ping(f * rng.uniform(0.97, 1.03), 0.9, 0.25), 0.0)
    for _ in range(5):
        place(buf, rng.uniform(0.1, 0.25) * ping(rng.uniform(900, 2400), 0.2, 0.05), rng.uniform(0.15, 0.7))
    return buf


def glass(rng):
    buf = np.zeros(int(RATE * 1.0))
    place(buf, 0.8 * highpass(crack(rng, 0.15, 12000, 0.03), 2500), 0.0)
    for _ in range(40):
        place(buf, rng.uniform(0.05, 0.22) * ping(rng.uniform(3000, 9000), 0.08, 0.012), rng.uniform(0.0, 0.8))
    return buf


def plaster(rng):
    buf = np.zeros(int(RATE * 1.0))
    place(buf, 0.8 * crack(rng, 0.2, 1800, 0.05), 0.0)
    place(buf, 0.6 * thump(70, 0.4, 0.1), 0.0)
    n = int(RATE * 0.8)
    grains = lowpass(noise(n, rng) * (rng.uniform(0, 1, n) > 0.985), 2500) * 6
    place(buf, 0.35 * grains * np.exp(-np.arange(n) / RATE / 0.35), 0.1)
    return buf


def plastic(rng):
    buf = np.zeros(int(RATE * 0.6))
    place(buf, 0.8 * crack(rng, 0.06, 5000, 0.01), 0.0)
    place(buf, 0.4 * thump(220, 0.2, 0.04), 0.0)
    for _ in range(5):
        place(buf, rng.uniform(0.15, 0.35) * crack(rng, 0.03, 4500, 0.006), rng.uniform(0.05, 0.4))
    return buf


def chip(rng):
    buf = np.zeros(int(RATE * 0.25))
    place(buf, 0.6 * crack(rng, 0.06, 3000, 0.012), 0.0)
    place(buf, 0.4 * thump(160, 0.15, 0.03), 0.0)
    return buf


def sparks(rng):
    buf = np.zeros(int(RATE * 0.8))
    x = t(0.5)
    buzz = np.sign(np.sin(2 * math.pi * 120 * x)) * 0.12 * np.exp(-x / 0.25)
    place(buf, lowpass(buzz, 2000), 0.0)
    for _ in range(30):
        place(buf, rng.uniform(0.1, 0.5) * highpass(crack(rng, 0.01, 12000, 0.002), 3000), rng.uniform(0.0, 0.5) ** 1.5)
    place(buf, 0.5 * highpass(crack(rng, 0.05, 12000, 0.01), 2000), 0.0)
    return buf


def write(name, buf):
    peak = np.max(np.abs(buf)) or 1.0
    buf = buf / peak * 0.89
    fade = min(len(buf), int(RATE * 0.02))
    buf[-fade:] *= np.linspace(1, 0, fade)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(v * 32767)) for v in buf))
    print(f"{path}: {len(buf) / RATE:.2f} s")


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, make, seed in (("wood_break", wood, 1), ("metal_break", metal, 2), ("glass_break", glass, 3),
                             ("plaster_break", plaster, 4), ("plastic_break", plastic, 5), ("chip", chip, 6),
                             ("sparks", sparks, 7)):
        random.seed(seed)
        write(name, make(np.random.default_rng(seed)))


if __name__ == "__main__":
    main()
