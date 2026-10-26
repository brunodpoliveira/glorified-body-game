#!/usr/bin/env python3
"""Placeholder sounds and music for the sound system (2026-10-26, Bruno: "we'll have a music for combat, tension,
ambient for sectors/environments, and placeholders in case there's a gap").

Two sources, both safe for the public repo:
- Made here (numpy, then ffmpeg to Ogg Vorbis): the placeholder music (ambient, tension, combat, menu), the room tone,
  the charge hum, the PA chime, the death sting, the gas hiss.
- Kenney CC0 packs (https://kenney.nl: "Impact Sounds", "Interface Sounds", "RPG Audio", "Sci-fi Sounds"), copied
  from the unpacked zips: steps, the dodge, the powers, the UI, doors, pickups, the scan, saves.

The game loads every event folder with Resources.LoadAll: Assets/_Game/Resources/Audio/Sfx/<event>/ holds the clips of
one event, Assets/_Game/Resources/Audio/Music/_placeholder/<state>.ogg the fallback music. A new sound is a new file.

Usage: python3 tools/audio_placeholders.py <folder with the unpacked Kenney packs>
The folder holds kenney_interface-sounds/, kenney_rpg-audio/, kenney_sci-fi-sounds/ and impact/ (Impact Sounds).
"""
import math
import os
import shutil
import subprocess
import sys
import tempfile
import wave

import numpy as np

RATE = 32000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "_Game", "Resources", "Audio")
SFX = os.path.join(ROOT, "Sfx")
MUSIC = os.path.join(ROOT, "Music", "_placeholder")

# event -> (pack folder, file name prefixes). Every match of a prefix is copied.
KENNEY = {
    "step_lucy": ("impact", ["footstep_carpet_"]),
    "step_boots": ("impact", ["footstep_concrete_"]),
    "dodge": ("kenney_rpg-audio", ["cloth"]),
    "blast": ("kenney_sci-fi-sounds", ["laserLarge_"]),
    "fire": ("kenney_sci-fi-sounds", ["thrusterFire_"]),
    "lift": ("kenney_sci-fi-sounds", ["forceField_"]),
    "throw": ("kenney_rpg-audio", ["drawKnife"]),
    "slam": ("kenney_sci-fi-sounds", ["lowFrequency_explosion_"]),
    "catch": ("impact", ["impactGeneric_light_"]),
    "refuse": ("kenney_interface-sounds", ["error_00"]),
    "flesh_hit": ("impact", ["impactPunch_medium_"]),
    "lucy_hurt": ("impact", ["impactPunch_heavy_"]),
    "body_fall": ("impact", ["impactSoft_heavy_"]),
    "grenade_bounce": ("impact", ["impactMetal_light_"]),
    "rocket_loop": ("kenney_sci-fi-sounds", ["spaceEngineSmall_000"]),
    "burn_loop": ("kenney_sci-fi-sounds", ["thrusterFire_000"]),
    "ui_move": ("kenney_interface-sounds", ["select_00"]),
    "ui_confirm": ("kenney_interface-sounds", ["confirmation_00"]),
    "ui_back": ("kenney_interface-sounds", ["back_00"]),
    "ui_open": ("kenney_interface-sounds", ["open_00"]),
    "ui_close": ("kenney_interface-sounds", ["close_00"]),
    "pickup": ("kenney_rpg-audio", ["handleSmallLeather"]),
    "drug": ("kenney_interface-sounds", ["glass_00"]),
    "door_locked": ("kenney_rpg-audio", ["metalLatch", "metalClick"]),
    "door_open": ("kenney_sci-fi-sounds", ["doorOpen_"]),
    "door_close": ("kenney_sci-fi-sounds", ["doorClose_"]),
    "scan": ("kenney_sci-fi-sounds", ["computerNoise_"]),
    "save": ("kenney_interface-sounds", ["bong_"]),
    "checkpoint": ("kenney_interface-sounds", ["maximize_001", "maximize_002"]),
    "log_open": ("kenney_rpg-audio", ["bookOpen"]),
    "log_close": ("kenney_rpg-audio", ["bookClose"]),
}


# ── Synthesis helpers ──────────────────────────────────────────────

def t(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def lowpass(x, cutoff, order=2):
    """A smooth low-pass in the frequency domain (fast for long buffers)."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / RATE)
    spec *= 1 / (1 + (f / cutoff) ** (2 * order))
    return np.fft.irfft(spec, len(x))


def highpass(x, cutoff, order=2):
    return x - lowpass(x, cutoff, order)


def bandpass(x, low, high):
    return lowpass(highpass(x, low), high)


def env(n, attack, decay):
    x = np.arange(n) / RATE
    return np.clip(x / max(attack, 1e-4), 0, 1) * np.exp(-x / decay)


def place(buf, clip, at):
    i = int(at * RATE) % len(buf)
    j = i + len(clip)
    if j <= len(buf):
        buf[i:j] += clip
    else:  # wrap round: the music loops
        k = len(buf) - i
        buf[i:] += clip[:k]
        buf[: len(clip) - k] += clip[k:]


def loop_seam(buf, seconds=1.5):
    """Folds the tail over the head so the end runs into the start with no click."""
    n = int(RATE * seconds)
    fade = np.linspace(0, 1, n)
    head = buf[:n].copy()
    buf[:n] = head * fade + buf[-n:] * (1 - fade)
    return buf[:-n]


def thump(freq, length, decay, drop=0.5):
    x = t(length)
    return np.sin(2 * math.pi * freq * x * (1 - drop * x / length)) * np.exp(-x / decay)


def bell(freq, length, decay):
    x = t(length)
    return (np.sin(2 * math.pi * freq * x) + 0.4 * np.sin(2 * math.pi * freq * 2.76 * x)
            + 0.2 * np.sin(2 * math.pi * freq * 5.4 * x)) * env(len(x), 0.004, decay)


def noise(n, rng):
    return rng.uniform(-1, 1, n)


def normalise(buf, peak=0.85):
    m = np.max(np.abs(buf)) or 1.0
    return buf / m * peak


# ── Music ──────────────────────────────────────────────────────────

def ambient(rng):
    seconds = 42
    x = t(seconds)
    buf = np.zeros_like(x)
    for f, a, lfo in ((55.0, 0.5, 0.05), (55.4, 0.4, 0.07), (82.6, 0.25, 0.03), (110.3, 0.12, 0.11), (164.8, 0.05, 0.09)):
        buf += a * np.sin(2 * math.pi * f * x) * (0.6 + 0.4 * np.sin(2 * math.pi * lfo * x + rng.uniform(0, 6)))
    air = lowpass(noise(len(x), rng), 500) * (0.5 + 0.5 * np.sin(2 * math.pi * 0.04 * x))
    buf += 0.6 * air / (np.max(np.abs(air)) or 1)
    for at in rng.uniform(2, seconds - 6, 5):  # far metal pings
        place(buf, 0.18 * bell(rng.uniform(520, 900), 5.0, 1.6), at)
    return loop_seam(normalise(buf, 0.6))


def tension(rng):
    beat = 60 / 72
    seconds = beat * 40
    x = t(seconds)
    buf = 0.45 * np.sin(2 * math.pi * 49 * x) + 0.2 * np.sin(2 * math.pi * 73.5 * x)
    cluster = sum(np.sin(2 * math.pi * f * x) for f in (311.1, 329.6, 349.2))
    buf += 0.07 * cluster * (0.5 - 0.5 * np.cos(2 * math.pi * x / (beat * 8)))
    for i in range(40):  # the heartbeat, lub-dub
        place(buf, 0.9 * thump(52, 0.35, 0.09), i * beat)
        place(buf, 0.55 * thump(46, 0.3, 0.07), i * beat + 0.26)
    for k in range(0, 40, 8):  # a scrape that swells each 8 beats
        n = int(RATE * beat * 4)
        scrape = bandpass(noise(n, rng), 1800, 4200) * np.sin(np.linspace(0, math.pi, n)) ** 2
        place(buf, 0.35 * scrape / (np.max(np.abs(scrape)) or 1), k * beat + beat * 2)
    return loop_seam(normalise(buf, 0.75), 0.5)


def combat(rng):
    bpm = 140
    beat = 60 / bpm
    bars = 12
    seconds = beat * 4 * bars
    buf = np.zeros(int(RATE * seconds))
    kick = thump(58, 0.35, 0.12, 0.6)
    snare_n = int(RATE * 0.2)
    hat_n = int(RATE * 0.05)
    for b in range(bars * 4):
        at = b * beat
        if b % 4 in (0, 2) or (b % 8 == 7):
            place(buf, 1.0 * kick, at)
        if b % 4 in (1, 3):
            snare = bandpass(noise(snare_n, rng), 900, 6000) * env(snare_n, 0.001, 0.05) + 0.4 * thump(190, 0.2, 0.05)
            place(buf, 0.7 * snare, at)
        for half in (0, 0.5):
            hat = highpass(noise(hat_n, rng), 7000) * env(hat_n, 0.0005, 0.012)
            place(buf, 0.25 * hat, at + half * beat)
    # Bass ostinato in E phrygian, eighths.
    notes = [41.2, 41.2, 43.65, 41.2, 49.0, 43.65, 41.2, 38.9]
    for i in range(bars * 8):
        f = notes[i % len(notes)] * (2 if (i // 16) % 2 else 1)
        n = int(RATE * beat * 0.5)
        x = np.arange(n) / RATE
        saw = 2 * ((f * x) % 1) - 1
        place(buf, 0.35 * lowpass(saw, 600) * env(n, 0.003, 0.18), i * beat * 0.5)
    for k in range(0, bars, 4):  # a dissonant stab each 4 bars
        n = int(RATE * 0.6)
        x = np.arange(n) / RATE
        stab = sum(np.tanh(3 * np.sin(2 * math.pi * f * x)) for f in (164.8, 174.6, 233.1)) * env(n, 0.002, 0.25)
        place(buf, 0.18 * stab, k * 4 * beat)
    return normalise(buf, 0.85)


def menu(rng):
    seconds = 40
    x = t(seconds)
    buf = sum(a * np.sin(2 * math.pi * f * x) for f, a in ((65.4, 0.3), (65.7, 0.25), (98.1, 0.12)))
    buf = buf * (0.7 + 0.3 * np.sin(2 * math.pi * 0.05 * x))
    scale = [261.6, 311.1, 349.2, 392.0, 415.3, 523.3]
    at = 1.0
    while at < seconds - 5:
        place(buf, 0.22 * bell(rng.choice(scale), 4.5, 1.4), at)
        at += rng.uniform(2.2, 4.0)
    return loop_seam(normalise(buf, 0.55))


# ── Effects ────────────────────────────────────────────────────────

def room_tone(rng):
    seconds = 20
    x = t(seconds)
    hum = sum(a * np.sin(2 * math.pi * f * x) for f, a in ((60, 0.4), (120, 0.25), (180, 0.08), (240, 0.05)))
    vent = lowpass(noise(len(x), rng), 900)
    buf = 0.3 * hum + 0.8 * vent / (np.max(np.abs(vent)) or 1)
    return loop_seam(normalise(buf, 0.35))


def charge_hum(rng):
    x = t(2.0)
    buf = sum(a * np.sin(2 * math.pi * f * x) for f, a in ((110, 0.5), (220.5, 0.3), (331, 0.15), (440.8, 0.08)))
    buf *= 0.8 + 0.2 * np.sin(2 * math.pi * 6 * x)
    buf += 0.1 * bandpass(noise(len(x), rng), 2000, 5000)
    return loop_seam(normalise(buf, 0.5), 0.25)


def pa_chime(rng):
    buf = np.zeros(int(RATE * 1.8))
    place(buf, bell(784.0, 1.6, 0.6), 0.0)
    place(buf, bell(622.3, 1.6, 0.7), 0.45)
    return normalise(buf, 0.7)


def death_sting(rng):
    buf = np.zeros(int(RATE * 3.0))
    place(buf, thump(45, 2.5, 0.8, 0.4), 0.0)
    n = int(RATE * 2.5)
    swell = lowpass(noise(n, rng), 1200) * np.linspace(1, 0, n) ** 2
    place(buf, 0.5 * swell / (np.max(np.abs(swell)) or 1), 0.0)
    x = t(2.8)
    place(buf, 0.25 * sum(np.sin(2 * math.pi * f * x) for f in (110, 116.5)) * np.exp(-x / 1.2), 0.05)
    return normalise(buf, 0.8)


def gas_hiss(rng):
    n = int(RATE * 2.0)
    buf = highpass(noise(n, rng), 3000) * 0.6 + bandpass(noise(n, rng), 500, 1500) * 0.3
    return loop_seam(normalise(buf, 0.5), 0.25)


def voice_check(rng):
    """Not a voice: three falling tones of 1.2 s, the stand-in clip of the voice test."""
    buf = np.zeros(int(RATE * 1.2))
    for i, f in enumerate((440.0, 392.0, 349.2)):
        place(buf, 0.5 * bell(f, 0.5, 0.15), i * 0.35)
    return normalise(buf, 0.7)


# ── Output ─────────────────────────────────────────────────────────

def write_ogg(path, buf):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        wav = tmp.name
    with wave.open(wav, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((np.clip(buf, -1, 1) * 32767).astype("<i2").tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", wav, "-c:a", "libvorbis", "-q:a", "4", path], check=True)
    os.unlink(wav)
    print(f"{os.path.relpath(path, ROOT)}: {len(buf) / RATE:.1f} s, {os.path.getsize(path) // 1024} KB")


def copy_kenney(packs):
    for event, (pack, prefixes) in KENNEY.items():
        folder = os.path.join(packs, pack)
        files = []
        for dirpath, _, names in os.walk(folder):
            for name in names:
                if name.endswith(".ogg") and any(name.startswith(p) for p in prefixes):
                    files.append(os.path.join(dirpath, name))
        if not files:
            raise SystemExit(f"no Kenney file for {event} in {folder} ({prefixes})")
        out = os.path.join(SFX, event)
        os.makedirs(out, exist_ok=True)
        for f in sorted(files):
            shutil.copy(f, os.path.join(out, os.path.basename(f)))
        print(f"Sfx/{event}: {len(files)} Kenney files")
    for pack in ("kenney_interface-sounds", "kenney_rpg-audio", "kenney_sci-fi-sounds", "impact"):
        shutil.copy(os.path.join(packs, pack, "License.txt"), os.path.join(SFX, f"License_{pack}.txt"))


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    rng = np.random.default_rng(26)
    for state, make in (("ambient", ambient), ("tension", tension), ("combat", combat), ("menu", menu)):
        write_ogg(os.path.join(MUSIC, state + ".ogg"), make(rng))
    for event, make in (("room_tone", room_tone), ("charge_hum", charge_hum), ("pa_chime", pa_chime),
                        ("death_sting", death_sting), ("gas_hiss", gas_hiss)):
        write_ogg(os.path.join(SFX, event, event + ".ogg"), make(rng))
    # The checks of the sound system (SoundSystemTests): a voice line with a clip, and a room with its own ambient
    # track (the training room), so the room path wins over the placeholder.
    write_ogg(os.path.join(ROOT, "..", "Voice", "test.voice_check.ogg"), voice_check(rng))
    write_ogg(os.path.join(ROOT, "Music", "TrainingRoom", "ambient.ogg"), ambient(np.random.default_rng(7)))
    copy_kenney(sys.argv[1])


if __name__ == "__main__":
    main()
