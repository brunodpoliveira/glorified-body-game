#!/usr/bin/env python3
"""The placeholder recordings of the demo's two audio logs (2026-11-09): L19, the incident review (W9), and L20, the
calibration note (W11). The writer's words (game/lines/T14_incident_review.md, L20_calibration.md) as subtitles, and
under each line the muffled voice-like sound of tools/tape_placeholder.py. The real voices come later.

Each line lasts its words at 2.6 a second (at least 1.8 s), a "(a pause)" in the script adds 1.5 s of silence after
it, and 0.5 s lies between lines. The script prints the start times: DemoItems.L19 and L20 carry the same numbers.

Usage: python3 tools/audio_log_placeholders.py
"""
import os
import wave

import numpy as np

RATE = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game", "Audio", "Logs")

LOGS = {
    "l19_incident_review": [
        ("Incident review. V4 series, unit two. Recorded for the file.", False),
        ("Summary. The unit was held at the standard interval for PCDH-Y. The replacement batch did not arrive on the day.", False),
        ("Logistics reports a hold at the hub. Six days.", False),
        ("On day six the nursing team entered to administer the replacement. The unit was in deficit. It did not wait for the injection.", False),
        ("The unit fed on a member of staff.", True),
        ("The team withdrew. The unit did not pursue.", False),
        ("Exams on day seven. Levels within the normal range. Accelerated healing of every wound the unit sustained in the incident, confirmed on imaging. The unit is calm.", False),
        ("Recommendation. Do not hold a batch. Never enter alone. End of review.", False),
    ],
    "l20_calibration": [
        ("Mapping. Day eleven. For the file.", False),
        ("Presentation stable. Separation holds under load.", False),
        ("Recommendation. Retain for v4 transfer.", False),
        ("Note. Affect flatter than day one.", True),
        ("Acceptable. End of note.", False),
    ],
}


def timings(lines):
    at, out = 0.6, []
    for text, pause in lines:
        length = max(1.8, len(text.split()) / 2.6)
        out.append((at, at + length))
        at += length + 0.5 + (1.5 if pause else 0.0)
    return out, at + 0.4


def render(name, lines, seed):
    rng = np.random.default_rng(seed)
    spans, length = timings(lines)
    n = int(length * RATE)
    t = np.arange(n) / RATE
    out = np.convolve(rng.normal(0.0, 1.0, n), np.ones(3) / 3.0, mode="same") * 0.018
    out += 0.01 * np.sin(2 * np.pi * 50 * t) + 0.004 * np.sin(2 * np.pi * 100 * t)
    for start, end in spans:
        a, b = int(start * RATE), int(end * RATE)
        tt = t[a:b] - start
        pitch = 112 + 14 * np.sin(2 * np.pi * 0.6 * tt) + 6 * np.sin(2 * np.pi * 2.1 * tt)
        phase = 2 * np.pi * np.cumsum(pitch) / RATE
        buzz = np.convolve(sum(np.sin(k * phase) / k for k in range(1, 9)), np.ones(12) / 12.0, mode="same")
        rate = 4.4 + 0.7 * np.sin(2 * np.pi * 0.29 * tt)
        syll = np.clip(np.sin(2 * np.pi * np.cumsum(rate) / RATE), 0.0, None) ** 1.5
        edge = np.minimum(1.0, np.minimum(tt / 0.08, (end - start - tt) / 0.15))
        out[a:b] += 0.32 * buzz * syll * edge
    for at in (0.05, length - 0.3):
        i = int(at * RATE)
        out[i:i + 400] += rng.normal(0.0, 0.5, 400) * np.exp(-np.arange(400) / 60.0)
    path = os.path.join(OUT, name + ".wav")
    os.makedirs(OUT, exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((np.clip(out, -1.0, 1.0) * 32767 * 0.9).astype(np.int16).tobytes())
    print(f"{name}: {length:.1f} s, starts " + ", ".join(f"{s:.1f}f" for s, _ in spans))


if __name__ == "__main__":
    for k, (name, lines) in enumerate(LOGS.items()):
        render(name, lines, 44 + k)
