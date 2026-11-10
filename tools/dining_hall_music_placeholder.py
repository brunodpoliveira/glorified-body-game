"""The placeholder track of the Dining Hall fight (DEMO_PLAN §12, round 103: "one song for the set piece fight. Drum and
bass, a violin orchestra and IDM"). Not the real track: a breakbeat at 172 bpm, a sub bass, a string pad that climbs.
Writes Assets/_Game/Resources/Audio/Music/_placeholder/dining_hall.ogg (RoomScore in G18). The real track replaces it.

Usage: python3 tools/dining_hall_music_placeholder.py
"""
import math
import os

import numpy as np

import audio_placeholders as ap


def hall(rng):
    bpm = 172
    beat = 60 / bpm
    bars = 16
    seconds = beat * 4 * bars
    n = int(ap.RATE * seconds)
    buf = np.zeros(n)
    x = np.arange(n) / ap.RATE
    kick = ap.thump(55, 0.25, 0.06, 0.6)
    snare_len = int(ap.RATE * 0.15)
    for bar in range(bars):
        at = bar * beat * 4
        # The two-step break: kick on 1 and the "and" of 2, snare on 2 and 4, ghost hats on the eighths.
        for k in (0.0, 1.5):
            ap.place(buf, kick * 0.9, at + k * beat)
        for s in (1.0, 3.0):
            snare = ap.bandpass(ap.noise(snare_len, rng), 900, 6000) * np.exp(-np.arange(snare_len) / (ap.RATE * 0.04))
            ap.place(buf, snare * 0.55, at + s * beat)
        for h in range(8):
            hat_len = int(ap.RATE * 0.03)
            hat = ap.bandpass(ap.noise(hat_len, rng), 7000, 12000) * np.exp(-np.arange(hat_len) / (ap.RATE * 0.008))
            ap.place(buf, hat * (0.25 if h % 2 else 0.15), at + h * beat * 0.5)
    # The sub bass: one root a bar, a minor line.
    roots = [43.65, 43.65, 51.91, 49.0]
    for bar in range(bars):
        f = roots[bar % len(roots)]
        start, end = int(ap.RATE * bar * beat * 4), int(ap.RATE * (bar + 1) * beat * 4)
        seg = x[start:end] - x[start]
        buf[start:end] += 0.35 * np.sin(2 * math.pi * f * seg) * np.minimum(1, seg * 20)
    # The strings: a slow pad that climbs over the 16 bars.
    climb = 1 + 0.5 * x / seconds
    pad = sum(np.sin(2 * math.pi * f * climb * x + 0.3 * np.sin(2 * math.pi * 5.2 * x)) for f in (220.0, 261.6, 329.6))
    buf += 0.08 * ap.lowpass(pad, 2500) * np.minimum(1, x / 4)
    return ap.normalise(ap.loop_seam(buf), 0.8)


if __name__ == "__main__":
    ap.write_ogg(os.path.join(ap.MUSIC, "dining_hall.ogg"), hall(np.random.default_rng(60)))
