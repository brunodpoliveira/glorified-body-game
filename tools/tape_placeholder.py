#!/usr/bin/env python3
"""The placeholder audio log (2026-10-31, Bruno: audio logs play while she walks). Made here with numpy, safe for the
public repo: a tape click, hiss and hum, and a muffled voice-like sound under each line (a buzz at a speaking pitch,
shaped in syllables). The real recordings come with the writer's words and the voices.

The line times must match ItemAssets.TestTape (the subtitles). Output: Assets/_Game/Audio/Logs/tape_test.wav

Usage: python3 tools/tape_placeholder.py
"""
import os
import wave

import numpy as np

RATE = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game", "Audio", "Logs", "tape_test.wav")
LENGTH = 23.0
# (start, end) of each spoken line, in seconds: the same starts as ItemAssets.TestTape.
LINES = [(0.6, 3.6), (4.5, 8.6), (9.5, 13.6), (14.5, 18.2), (19.0, 21.8)]


def main():
    rng = np.random.default_rng(31)
    n = int(LENGTH * RATE)
    t = np.arange(n) / RATE
    # Hiss (soft, high) and a 50 Hz hum.
    hiss = rng.normal(0.0, 1.0, n)
    hiss = np.convolve(hiss, np.ones(3) / 3.0, mode="same") * 0.018
    hum = 0.01 * np.sin(2 * np.pi * 50 * t) + 0.004 * np.sin(2 * np.pi * 100 * t)
    out = hiss + hum
    # The voice: a buzz with a wandering pitch, through a soft low-pass, shaped in 4 to 6 syllables a second.
    for start, end in LINES:
        a, b = int(start * RATE), int(end * RATE)
        tt = t[a:b] - start
        pitch = 118 + 18 * np.sin(2 * np.pi * 0.7 * tt) + 8 * np.sin(2 * np.pi * 2.3 * tt)
        phase = 2 * np.pi * np.cumsum(pitch) / RATE
        buzz = sum(np.sin(k * phase) / k for k in range(1, 9))
        buzz = np.convolve(buzz, np.ones(12) / 12.0, mode="same")
        rate = 4.8 + 0.8 * np.sin(2 * np.pi * 0.31 * tt)
        syll = np.clip(np.sin(2 * np.pi * np.cumsum(rate) / RATE), 0.0, None) ** 1.5
        edge = np.minimum(1.0, np.minimum(tt / 0.08, (end - start - tt) / 0.15))
        out[a:b] += 0.32 * buzz * syll * edge
    # The tape clicks at the start and at the stop.
    for at in (0.05, LENGTH - 0.4):
        i = int(at * RATE)
        out[i:i + 400] += rng.normal(0.0, 0.5, 400) * np.exp(-np.arange(400) / 60.0)
    out = np.clip(out, -1.0, 1.0)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with wave.open(OUT, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((out * 32767 * 0.9).astype(np.int16).tobytes())
    print(f"{OUT}: {LENGTH:.1f} s")


if __name__ == "__main__":
    main()
