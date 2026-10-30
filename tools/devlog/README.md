# The devlog pipeline

Script in, video out, overnight. The writer writes a script. This folder turns it into the narration (Bruno's
voice clone on RFab), the long video (16:9) and the short (9:16), plus a review packet for the morning.

## The loop

1. The game agent ends each step with a note in `devlog-notes/` and, when the editor is closed, the bot video run
   `tools/unity.sh record TrainingRoom`. The clips land in `~/MEGA/glorified-body-renders/devlog/clips/latest/`.
2. The writer writes `devlog/<ep>/long_vN.md` and `short_vN.md` in the writers' folder (reality-fabricator,
   `the-glorified-body/devlog/`). It names visuals from `VISUALS.md` in that folder (made by `catalog.py`).
3. At night: `tools/devlog/night.sh <ep>` (add `--record` for fresh clips). About 10 minutes for a 6-minute video.
4. In the morning Bruno watches `~/MEGA/glorified-body-renders/devlog/<ep>/vN/long/final.mp4` and the short, and writes
   notes in `NOTES.md` next to each.
5. The writer reads the notes and writes `vN+1`. The next night run renders only the changed sections.

## The script format

```
---
episode: 0
kind: long            long = 1920x1080. short = 1080x1920, the title on top, the section text as a caption.
title: Why the series became a game
voice: me             a name in ~/MEGA/glorified-body-renders/devlog/voices.json (RFab voice ids, private)
---

## 1. Cold open
VISUAL: clip T12/edit/T12_slice_1080.mp4 0:10-0:18
VISUAL: still game/step3/telegraph.png
VISUAL: card "This video is sponsored by RFab.ai" "More at the end."
NARRATION:
The words the voice reads. Several lines are fine. A section is one idea, 20 to 60 words.
```

- A section lasts as long as its narration plus 0.6 s. Its visuals share that time evenly.
- `still` gets a slow push-in. `clip` is cut to the range (or held on its last frame). `seq` is a folder of
  numbered PNG frames. `card` is drawn here. Ids are paths under `~/MEGA/glorified-body-renders/`.
- A missing file does not stop the build: a card names it, and the review table shows it.
- Rules for every episode (T13): the sponsor line said and shown in the first 10 s, a calm title, no shock words.
- Full transparency (Bruno, 2026-10-21, CANON): name every tool, model and provider that did work in what the
  video shows: the engine, the 3D tools, the voice provider, the image models, the music models, the AI agent.

## The tools

| File | Does |
|---|---|
| `catalog.py` | Lists the stills, clips and frame sequences the writer can name. `--copy <dir>` puts `VISUALS.md` in the writers' folder. |
| `script.js` | Parses the script format. `node script.js file.md` prints the JSON. |
| `narrate.js` | One WAV for each section through RFab Voice Scenes (`POST /api/audio/dialogue`), with the clone. The cache `devlog/<ep>/audio_cache/` is keyed by the voice and the text, so an unchanged section is never billed again, in any version or position. Writes `timing.json`. |
| `assemble.py` | `timing.json` + the visuals -> `final.mp4`, `contact_sheet.png`, `REVIEW.md`, `NOTES.md`. |
| `night.sh` | The whole night: optional bot video, catalog, narration, assembly, the packet `README.md`. |
| `rfab_client.js` | The backend client. `BASE` (default the local backend, production `https://api.rfab.ai`) and `RFAB_TOKEN`. Prints no secret. |

Cost: about 4 tokens for each character of narration. A 6-minute long video is about 25,000 tokens, a short about 3,000.

## The product shape (for RFab)

Nothing here is game-specific except the bot that records the clips. The same three steps (a script with visual
cues, a cloned voice for each section, an assembler for the long and the short) are a feature: the user drops a
folder of clips and pictures, writes or generates the script, picks a voice, and gets both videos and a review
page. The nightly revision loop (notes -> next script version -> render) is an agent routine.
