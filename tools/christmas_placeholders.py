#!/usr/bin/env python3
"""Placeholder cards and sounds for the night of 30 December (2026-11-10). 0 art, 0 RFab.

- The title card of the demo (DEMO_PLAN §14 item 14, rounds 102 to 105): Assets/_Game/Cutscenes/cs00_raven.png, the word
  RAVEN on black, a grey note under it.
- Cutscene 07c, the party (the writer's game/cutscenes/07c_the_party.md): cs07c_s1.png to cs07c_s7.png, text cards. The
  present on black, the memory on a warm card (the one warm light of act 1). Sounds in Cutscenes/sfx/: cs07c_cork (a
  pop and the foam), cs07c_cheer (a room that cheers once). The carol stays silent until the voices (the writer's
  needs, item 2).
- The music of the sets (Assets/_Game/Audio/Broadcast/): station_loop (a radio's hits of the season, low and thin,
  12 s), tv_music_loop (the channel the attendant switches to). No real song: a few chords on bells.

Usage: python3 tools/christmas_placeholders.py
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from audio_placeholders import RATE, bandpass, bell, env, loop_seam, lowpass, noise, normalise, place, t, write_ogg  # noqa: E402

GAME = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game")
CUTSCENES = os.path.join(GAME, "Cutscenes")
BROADCAST = os.path.join(GAME, "Audio", "Broadcast")
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
BOLD = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

BLACK = (0, 0, 0)
WARM = (92, 58, 30)  # a kitchen at night, under one lamp


def wrap(d, words, font, width):
    lines, line = [], ""
    for word in words.split():
        probe = (line + " " + word).strip()
        if d.textlength(probe, font=font) > width:
            lines.append(line)
            line = word
        else:
            line = probe
    lines.append(line)
    return lines


def card(path, words, colour=BLACK, title=""):
    w, h = 640, 360
    img = Image.new("RGB", (w, h), colour)
    d = ImageDraw.Draw(img)
    y = h // 2
    if title:
        big = ImageFont.truetype(BOLD, 64)
        d.text(((w - d.textlength(title, font=big)) / 2, h // 2 - 60), title, fill=(232, 232, 236), font=big)
        y = h // 2 + 40
    if words:
        font = ImageFont.truetype(FONT, 15 if title else 16)
        lines = wrap(d, words, font, w - 120)
        if not title:
            y = h // 2 - 11 * len(lines)
        tint = (150, 120, 92) if colour == WARM else (118, 118, 122)
        for ln in lines:
            d.text(((w - d.textlength(ln, font=font)) / 2, y), ln, fill=tint, font=font)
            y += 22
    img.save(path)
    print(os.path.relpath(path, GAME), img.size)


# ── The sounds ─────────────────────────────────────────────────────

def cork(rng):
    """A cork that goes, then the foam: a pop, a hiss that falls away."""
    out = np.zeros(int(RATE * 1.4))
    x = t(0.03)
    pop = np.sin(2 * math.pi * 420 * x) * np.exp(-x / 0.006) + bandpass(noise(len(x), rng), 900, 4000) * np.exp(-x / 0.008)
    place(out, pop, 0.05)
    fizz = bandpass(noise(int(RATE * 1.2), rng), 3000, 9000) * env(int(RATE * 1.2), 0.02, 0.9)
    place(out, fizz * 0.35, 0.08)
    return normalise(out, 0.8)


def cheer(rng):
    """A room that cheers once: many voices as noise in the voice band, a swell, then the claps under it."""
    n = int(RATE * 2.2)
    voices = bandpass(noise(n, rng), 250, 2200)
    wobble = 1 + 0.35 * np.sin(2 * math.pi * 5.5 * t(2.2)[:n]) * np.sin(2 * math.pi * 0.7 * t(2.2)[:n])
    out = voices * wobble * env(n, 0.12, 1.6)
    for i in range(18):
        at = 0.2 + rng.random() * 1.6
        x = t(0.02)
        clap = bandpass(noise(len(x), rng), 1200, 5000) * np.exp(-x / 0.004)
        place(out, clap * 0.6, at)
    return normalise(out, 0.75)


CHORDS = [  # a few chords on bells, nothing a listener knows: the hits of the season, far away
    (261.6, 329.6, 392.0),
    (220.0, 261.6, 329.6),
    (174.6, 220.0, 261.6),
    (196.0, 246.9, 293.7),
]


def bells_loop(rng, seconds, thin):
    out = np.zeros(int(RATE * seconds))
    beat = seconds / (len(CHORDS) * 4)
    for c, chord in enumerate(CHORDS):
        for b in range(4):
            at = (c * 4 + b) * beat
            note = chord[(b + c) % 3] * (2 if b == 3 else 1)
            place(out, bell(note, 1.2, 0.5) * 0.5, at)
        place(out, sum(bell(f / 2, beat * 4, 1.4) for f in chord) * 0.18, c * 4 * beat)
    out = loop_seam(out)
    if thin:  # a small speaker: no bass, no air
        out = bandpass(out, 320, 3200)
    return normalise(out, 0.6)


def main():
    os.makedirs(BROADCAST, exist_ok=True)
    os.makedirs(os.path.join(CUTSCENES, "sfx"), exist_ok=True)
    rng = np.random.default_rng(1230)
    write_ogg(os.path.join(CUTSCENES, "sfx", "cs07c_cork.ogg"), cork(rng))
    write_ogg(os.path.join(CUTSCENES, "sfx", "cs07c_cheer.ogg"), cheer(rng))
    write_ogg(os.path.join(BROADCAST, "station_loop.ogg"), bells_loop(rng, 12.0, thin=True))
    write_ogg(os.path.join(BROADCAST, "tv_music_loop.ogg"), bells_loop(rng, 9.6, thin=True))

    card(os.path.join(CUTSCENES, "cs00_raven.png"), "PLACEHOLDER: the picture of act 1 and its bird.", title="RAVEN")
    card(os.path.join(CUTSCENES, "cs07c_s1.png"), "PLACEHOLDER (07c, S1): a desktop tree a foot tall, small lights that blink, paper ornaments. "
         "Over it, in marker on printer paper: HAPPY BIRTHDAY GENE!!! Under the next table, two pairs of work shoes that do not move.")
    card(os.path.join(CUTSCENES, "cs07c_s2.png"), "PLACEHOLDER (07c, S2): the same table under full light, a paper chain. A foil tray, a turkey. "
         "A hand in a red scrubs cuff holds a carving knife, a hand in a slate-grey sleeve holds the fork.", WARM)
    card(os.path.join(CUTSCENES, "cs07c_s3.png"), "PLACEHOLDER (07c, S3): open pizza boxes, a stack of paper plates, a paper cup with GENE on it in marker.", WARM)
    card(os.path.join(CUTSCENES, "cs07c_s4.png"), "PLACEHOLDER (07c, S4): a row of paper cups held out, one sleeve each: red scrubs, slate grey, "
         "light blue, white. One white cotton glove.", WARM)
    card(os.path.join(CUTSCENES, "cs07c_s5.png"), "PLACEHOLDER (07c, S5, the animated cut): a hand in a light blue sleeve, a green bottle, "
         "a thumb on the cork. The cork goes. The foam runs over the knuckles and onto the tile.", WARM)
    card(os.path.join(CUTSCENES, "cs07c_s6.png"), "PLACEHOLDER (07c, S6): hands that clap. A paper cup held up high. Foam drips off the edge of the table.", WARM)
    card(os.path.join(CUTSCENES, "cs07c_s7.png"), "PLACEHOLDER (07c, S7): the tree, the lights blink. The room is empty and colder than it was.")


if __name__ == "__main__":
    main()
