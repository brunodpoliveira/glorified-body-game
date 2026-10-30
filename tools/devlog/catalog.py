#!/usr/bin/env python3
"""Devlog visuals catalog: lists the stills and the clips the writer can name in a script.

Walks ~/MEGA/glorified-body-renders/ (override: GB_RENDERS), skips frame sequences (files named 0001.png) and the
proxies, caps each folder at 40 stills, and writes:
  ~/MEGA/glorified-body-renders/devlog/VISUALS.md    for the writer (a copy goes to the writers' folder, see --copy)
  ~/MEGA/glorified-body-renders/devlog/visuals.json  for assemble.py
Run: python3 -I catalog.py [--copy /path/to/the-glorified-body/devlog]
"""
import json
import os
import subprocess
import sys

ROOT = os.environ.get("GB_RENDERS", os.path.expanduser("~/MEGA/glorified-body-renders"))
OUT = os.path.join(ROOT, "devlog")
STILL = {".png", ".jpg", ".jpeg", ".webp"}
CLIP = {".mp4", ".mov", ".webm"}
SKIP_DIRS = {"proxies", "frames", "__pycache__", "assets", "post", "plates", "sets", "chars", "ui", "abtest",
             "depth", "probes", "checks", "galerians_ref", "out", "final_check", "camera_match"}
SKIP_SUFFIX = ("_alpha",)
CAP = 20

# One line for the folders and the files we know. The writer reads these.
NOTES = {
    "game": "The Unity greybox, saved by the bot and the shot runner. Lucy is a capsule. Grey room, placeholder HUD.",
    "game/step1": "Step 1 (2026-10-20): the menu loop and the empty training room.",
    "game/step2": "Step 2: tank controls, the three fixed cameras, the lock-on.",
    "game/step3": "Step 3: the telegraph, the charge, the short, the HUD with the three bars.",
    "game/adjustments1": "After Bruno's first play: the HUD telegraph ring, the how-to-play panel, the directional dodge.",
    "game/step1/main_menu_ui.png": "The main menu: START, HOW TO PLAY, DIFFICULTY, OPTIONS, CREDITS, QUIT.",
    "game/step1/options_controls.png": "Options, Controls tab: the remappable tank-control actions.",
    "game/step1/death_ui.png": "The death screen in the Arkham shape: picture, hint, Retry, Main Menu.",
    "game/step1/play_training_room.png": "The training room from the fixed camera, Lucy as a capsule.",
    "game/step3/telegraph.png": "The dummy aims: the red laser line to Lucy, the circle over its head (old place).",
    "game/step3/charge.png": "Lucy charges a power on the locked dummy.",
    "game/step3/short.png": "The short: yellow sparks around her.",
    "game/step3/hud_short.png": "The HUD during the short: the three bars, the SHORT warning, the powers, the drugs.",
    "game/step3/hud.png": "The HUD after a drug: full bars.",
    "game/adjustments1/hud_telegraph.png": "The telegraph ring on the HUD, over her chest, with the laser from the shooter.",
    "game/adjustments1/how_to_play_ui.png": "The how-to-play panel: the premise text and the control rows.",
    "devlog/clips": "Bot video clips (tools/unity.sh record). One MP4 for each mechanic, 1080p30, with the HUD.",
    "T12": "The vertical slice of the VIDEO SERIES (2026-10-18), before the pivot. PS1 look, Blender, RFab voices.",
    "T12/edit": "The finished 2:16 clip of the slice (T12_slice_1080.mp4) and its contact sheet. The masters are ProRes.",
    "T12/edit/T12_slice_1080.mp4": "The whole slice: open plug, seven shots, end plug. The best 'before' footage.",
    "T12/shots": "Frame sequences of the slice shots (s1 to s7). Name a folder to use it as a clip.",
    "T14": "The faces test: 3D Studio heads at the table. The uncanny valley lesson (ART_LESSONS.md section 3).",
    "T15": "The 3D set and head checks. Also faces that failed: see cast_heads_vs_galerians.png.",
    "T16": "The translation look test (forearm frame, textures).",
    "T17": "Ava's demon: Seedance motion tests and keyframes.",
    "T18": "The masked cast concept sheets (rounds 4 to 6). round5_sheet1 is approved with gloves on the nurse.",
    "T19_characters": "Character pictures made from the approved sheets: lab worker, guard, fireman, nurse, power armour, Lucy.",
    "tests": "The feasibility tests of 2026-10-17: T0 machine, T1 PS1 look, T2 upscale, T3 backgrounds, T7 motion, T8 voices.",
    "tests/T3/sheets": "The background paint-over sheets Bruno judged blind (chamber, corridor).",
}


def probe_seconds(path):
    try:
        out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", path],
                             capture_output=True, text=True, timeout=20).stdout.strip()
        return round(float(out), 2)
    except Exception:
        return None


def is_frame(name):
    stem = os.path.splitext(name)[0]
    return stem.isdigit()


def walk():
    items = []
    for dirpath, dirnames, filenames in os.walk(ROOT):
        rel_dir = os.path.relpath(dirpath, ROOT)
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS and not d.startswith(".") and not d.endswith(SKIP_SUFFIX))
        if rel_dir == "devlog" or rel_dir.startswith("devlog/ep"):
            dirnames[:] = [d for d in dirnames if d == "clips" or rel_dir == "devlog/clips"]
            if rel_dir != "devlog/clips" and not rel_dir.startswith("devlog/clips"):
                continue
        stills, clips = [], []
        frames = 0
        for f in sorted(filenames):
            ext = os.path.splitext(f)[1].lower()
            rel = os.path.normpath(os.path.join(rel_dir, f))
            if ext in STILL:
                if is_frame(f):
                    frames += 1
                    continue
                stills.append(rel)
            elif ext in CLIP:
                clips.append(rel)
        if frames >= 10:
            # A frame sequence: the folder itself is a clip (assemble.py reads %04d.png or %05d.png).
            items.append({"id": rel_dir, "kind": "sequence", "frames": frames})
        for c in clips:
            items.append({"id": c, "kind": "clip", "seconds": probe_seconds(os.path.join(ROOT, c))})
        for i, s in enumerate(stills):
            if i >= CAP:
                items.append({"id": rel_dir, "kind": "more", "count": len(stills) - CAP})
                break
            items.append({"id": s, "kind": "still"})
    return items


def note_for(item_id):
    if item_id in NOTES:
        return NOTES[item_id]
    return ""


def write(items):
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "visuals.json"), "w") as f:
        json.dump({"root": ROOT, "items": items}, f, indent=1)
    lines = ["# Devlog visuals (generated by tools/devlog/catalog.py)", "",
             "Name a visual in a script as `still <id>`, `clip <id> [m:ss-m:ss]` or `seq <id>`. Ids are paths under",
             f"`{ROOT}/`. Do not use `reference/` pictures (third-party)."
             " A card is `card \"big line\" \"small line\"`.", ""]
    last_dir = None
    for it in items:
        d = os.path.dirname(it["id"]) if it["kind"] in ("still", "clip") else it["id"]
        if d != last_dir:
            lines.append("")
            lines.append(f"## {d or '.'}")
            n = note_for(d)
            if n:
                lines.append(n)
            lines.append("")
            last_dir = d
        if it["kind"] == "still":
            n = note_for(it["id"])
            lines.append(f"- still `{it['id']}`" + (f": {n}" if n else ""))
        elif it["kind"] == "clip":
            n = note_for(it["id"])
            sec = f" ({it['seconds']} s)" if it.get("seconds") else ""
            lines.append(f"- clip `{it['id']}`{sec}" + (f": {n}" if n else ""))
        elif it["kind"] == "sequence":
            lines.append(f"- seq `{it['id']}` ({it['frames']} frames)")
        elif it["kind"] == "more":
            lines.append(f"- ... and {it['count']} more stills in `{it['id']}` (ls the folder)")
    with open(os.path.join(OUT, "VISUALS.md"), "w") as f:
        f.write("\n".join(lines) + "\n")
    return os.path.join(OUT, "VISUALS.md")


if __name__ == "__main__":
    items = walk()
    md = write(items)
    print(f"{len(items)} items -> {md}")
    if "--copy" in sys.argv:
        dst = sys.argv[sys.argv.index("--copy") + 1]
        os.makedirs(dst, exist_ok=True)
        with open(md) as a, open(os.path.join(dst, "VISUALS.md"), "w") as b:
            b.write(a.read())
        print(f"copied to {dst}/VISUALS.md")
