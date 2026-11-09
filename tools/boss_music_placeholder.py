"""The placeholder track of the brain fight (2026-11-08, Bruno: "there should be an unique song playing as soon as the
fight starts"). Not the combat track: a slow heartbeat, a low organ drone that bends, a dissonant bell every two bars.
Writes Assets/_Game/Resources/Audio/Music/_placeholder/boss.ogg (BrainFight.PlaceholderMusic). The real track replaces it.

Usage: python3 tools/boss_music_placeholder.py
"""
import math
import os

import numpy as np

import audio_placeholders as ap


def boss(rng):
    bpm = 96
    beat = 60 / bpm
    bars = 16
    seconds = beat * 4 * bars
    n = int(ap.RATE * seconds)
    buf = np.zeros(n)
    x = np.arange(n) / ap.RATE
    # The drone: two low saws a semitone apart, a slow bend, through a low pass.
    bend = 1 + 0.01 * np.sin(2 * math.pi * x / (beat * 8))
    drone = sum(2 * ((f * bend * x) % 1) - 1 for f in (36.7, 38.9))
    buf += 0.25 * ap.lowpass(drone, 300)
    # The heartbeat: two thumps on each beat pair.
    lub = ap.thump(52, 0.3, 0.1, 0.5)
    dub = ap.thump(46, 0.3, 0.12, 0.5)
    for b in range(0, bars * 4, 2):
        ap.place(buf, 1.0 * lub, b * beat)
        ap.place(buf, 0.7 * dub, b * beat + beat * 0.35)
    # A dissonant bell every two bars, falling.
    for k, f in zip(range(0, bars, 2), [622.3, 587.3, 554.4, 523.3, 622.3, 587.3, 554.4, 466.2]):
        ap.place(buf, 0.22 * (ap.bell(f, 2.5, 1.2) + 0.6 * ap.bell(f * 1.06, 2.5, 1.0)), k * 4 * beat)
    # Breath: filtered noise that swells over each 4 bars.
    breath = ap.bandpass(ap.noise(n, rng), 300, 1200) * (0.5 - 0.5 * np.cos(2 * math.pi * x / (beat * 16)))
    buf += 0.08 * breath
    return ap.normalise(ap.loop_seam(buf), 0.85)


if __name__ == "__main__":
    ap.write_ogg(os.path.join(ap.MUSIC, "boss.ogg"), boss(np.random.default_rng(58)))
