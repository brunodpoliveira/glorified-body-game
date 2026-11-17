#!/usr/bin/env python3
"""The pace of the T49 walk and run clips from the bot `lucy` (Bruno, round 185: "From the bot reading").

  tools/characters_pace.py [--fresh] runs/<play run> [runs/<play run> ...]

The bot `lucy` (play Tutorial Main_Menu lucy) puts every T49 class on a treadmill: the root stays still and the bot
reads the speed of the planted foot over the floor ("the clip's ground speed, root still, X m/s"). That speed is the
speed of the clip in play, at the pace of the controller that the run used. This script divides it by that pace,
so the result is the speed of the feet at x1 in play. CreateCharacterAssets.ApplyPace then plays each clip at
the game speed / that speed. tools/characters_clips.py measures the same speed in the source file, but in play it
differs by up to 10 % (the Humanoid retarget, the toe roll), so the bot reading wins.

IMPORTANT: give only runs made with the controllers that are in the project now (the script reads the pace from the
controllers). After a calibration: BuildAll, then a new bot run before the next calibration. Each reading is kept as a
sample (the run, the reading, the pace of that run), and a new calibration adds the new runs to the samples already in
the file: the speed at x1 is the mean of reading / pace over all samples. A run reading spreads about 2 to 4 % from one
run to the next (the walk about 1 %), so more samples give a better pace. --fresh drops the old samples (after a model
swap or a change of the bot's measure).

Output: Assets/_Game/Characters/pace_from_bot.json.
"""
import json
import os
import re
import sys

TOP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(TOP, "Assets/_Game/Characters/pace_from_bot.json")
ANIM = os.path.join(TOP, "Assets/_Game/Characters/Anim")
READING = re.compile(r"root still, (-?[\d.,]+) m/s against the (walk|run) of")


def controller_pace(cls):
    """The timeScale of the walk (threshold 0.5) and the run (threshold 1) in the class controller."""
    text = open(os.path.join(ANIM, cls + ".controller")).read()
    pace = {}
    for threshold, scale in re.findall(r"m_Threshold: ([\d.]+)\s+m_Position: \{[^}]*\}\s+m_TimeScale: ([-\d.eE]+)", text):
        key = {0.5: "walk", 1.0: "run"}.get(float(threshold))
        if key:
            pace[key] = float(scale)
    return pace


def main(runs, fresh=False):
    readings = {}
    for run in runs:
        name = os.path.basename(os.path.normpath(run))
        steps = json.load(open(os.path.join(run, "playthrough.json")))["steps"]
        for step in steps:
            for clip in ("walk", "run"):
                suffix = f"_{clip}s"
                if not step["name"].endswith(suffix):
                    continue
                match = READING.search(step["detail"])
                if not match:
                    continue
                cls = step["name"][: -len(suffix)]
                readings.setdefault(cls, {}).setdefault(clip, []).append((name, float(match.group(1).replace(",", "."))))
    if not readings:
        sys.exit("no treadmill readings in the runs (the bot lucy)")
    old = json.load(open(OUT)) if os.path.exists(OUT) and not fresh else {"classes": []}
    entries = {e["id"]: e for e in old.get("classes", [])}
    for cls, clips in sorted(readings.items()):
        pace = controller_pace(cls)
        entry = entries.get(cls, {"id": cls})
        for clip in ("walk", "run"):
            if not clips.get(clip) or clip not in pace:
                sys.exit(f"{cls}: no {clip} reading or no {clip} pace in its controller")
            samples = [x for x in entry.get(clip + "Samples", []) if x["run"] not in {r for r, _ in clips[clip]}]
            samples += [{"run": r, "reading": v, "pace": round(pace[clip], 4)} for r, v in clips[clip]]
            entry[clip + "Samples"] = samples
            entry[clip] = round(sum(x["reading"] / x["pace"] for x in samples) / len(samples), 4)
        entries[cls] = entry
        print(f"{cls:13s} walk {entry['walk']:.3f} m/s at x1 ({len(entry['walkSamples'])} samples), "
              f"run {entry['run']:.3f} m/s at x1 ({len(entry['runSamples'])} samples): "
              + ", ".join(f"{x['reading']:.2f}@x{x['pace']:.3f}" for x in entry["runSamples"]))
    data = {
        "note": "The speed of the planted foot in play at x1 (m/s), from the bot lucy (root still): the mean of reading / pace over the samples. tools/characters_pace.py writes it; CreateCharacterAssets.ApplyPace reads it.",
        "classes": [entries[k] for k in sorted(entries)],
    }
    with open(OUT, "w") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
    print(f"wrote {OUT}")


if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if a != "--fresh"]
    if not args:
        sys.exit(__doc__)
    main(args, fresh="--fresh" in sys.argv[1:])
