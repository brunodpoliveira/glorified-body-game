#!/usr/bin/env python3
"""The door sounds of the chase (2026-11-03, DEMO_PLAN 13.2): a boot on a locked door (a soldier, the power armour breach
it) and fists on it (a guard, a scientist bang on it). Synthesised here, so the files are ours. PLACEHOLDERS.

Writes Assets/_Game/Resources/Audio/Sfx/door_kick/ and door_bang/ (one Sfx event each).
Usage: python3 tools/door_sfx.py
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import destruction_sfx as sfx  # noqa: E402

SFX = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Resources", "Audio", "Sfx")


def kick(rng):
    """A boot on the leaf: a deep boom, the crack of the frame, the rattle of the latch."""
    buf = np.zeros(int(sfx.RATE * 0.7))
    sfx.place(buf, 1.0 * sfx.thump(rng.uniform(55, 70), 0.35, 0.09), 0.0)
    sfx.place(buf, 0.6 * sfx.crack(rng, 0.07, 2600, 0.02), 0.0)
    sfx.place(buf, 0.25 * sfx.ping(rng.uniform(900, 1200), 0.18, 0.05), 0.01)
    n = int(sfx.RATE * 0.25)
    rattle = sfx.highpass(sfx.noise(n, rng) * (rng.uniform(0, 1, n) > 0.97), 1800) * 4
    sfx.place(buf, 0.3 * rattle * np.exp(-np.arange(n) / sfx.RATE / 0.07), 0.04)
    return buf


def bang(rng):
    """Two or three fists on the leaf: soft thumps, no crack."""
    buf = np.zeros(int(sfx.RATE * 0.9))
    at = 0.0
    for _ in range(int(rng.integers(2, 4))):
        sfx.place(buf, rng.uniform(0.7, 1.0) * sfx.thump(rng.uniform(85, 110), 0.18, 0.05), at)
        sfx.place(buf, 0.2 * sfx.lowpass(sfx.crack(rng, 0.04, 1200, 0.012), 1500), at)
        at += rng.uniform(0.2, 0.28)
    return buf


def main():
    for event, make in (("door_kick", kick), ("door_bang", bang)):
        sfx.OUT = os.path.join(SFX, event)
        os.makedirs(sfx.OUT, exist_ok=True)
        for i in range(3):
            sfx.write(f"{event}_{i}", make(np.random.default_rng(300 + i + (10 if event == "door_bang" else 0))))


if __name__ == "__main__":
    main()
