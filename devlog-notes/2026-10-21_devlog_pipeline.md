# Devlog notes, the devlog pipeline (2026-10-21, night)

Not a game step. The pipeline that turns a script into the narrated long video and the short, overnight, so Bruno
reviews in the morning. Bruno's aim: the same pipeline becomes an RFab feature later, so the tools carry no game
logic except the bot that records the clips.

## What was built

1. `tools/devlog/` (see its `README.md` for the script format and the loop):
   - `catalog.py`: the list of stills, clips and frame sequences the writer can name (`VISUALS.md`, 631 entries).
   - `script.js`: the script format. Front matter (episode, kind, title, voice), then `## N. Name`, `VISUAL:`
     lines (still, clip with a time range, seq, card), `NARRATION:`.
   - `narrate.js`: one WAV for each section through RFab Voice Scenes with Bruno's clone (`me` on the local
     backend). The cache `~/glorified-body-renders/devlog/<ep>/audio_cache/` is keyed by the voice and the text,
     so a revision bills only the changed sections, at any position, long or short.
   - `assemble.py`: the long video (1920x1080) and the short (1080x1920, the title on top, the section text as a
     caption). A still gets a slow push-in, a clip is cut or held, a card is drawn. Plus `contact_sheet.png`,
     `REVIEW.md` (the section table with times, the T13 upload checklist) and an empty `NOTES.md` for Bruno.
   - `night.sh <ep> [--record]`: catalog, narration, assembly for long and short, then the bot video run when
     asked (it waits for the editor to close, up to 8 h) and a second assembly with the fresh clips.
2. The bot records video: `tools/unity.sh record TrainingRoom`. `FrameRecorder` fixes the game clock at 30 frames
   per second only inside a clip (`Time.captureFramerate`), renders every frame at 1920x1080 through the main
   camera with the HUD over it, and ffmpeg makes one MP4 for each clip: tank_controls, camera_cuts,
   lock_on_charge, telegraph, blast_kill, drug_pickup, short. Copies go to
   `~/glorified-body-renders/devlog/clips/{latest,<stamp>}/`. Outside a clip the bot runs as before.
3. The project skill `/devlog-night <ep>`: reads Bruno's notes, asks the writer for the next version, runs the
   night, reports the packet.

## Proof

- Warm-editor compile of the recorder: 0 errors, 0 warnings (`runs/20261021-010311-wcompile-402796`).
- Smoke test, two sections (a card, a still plus six seconds of the T12 slice) on Bruno's clone: 13.3 s of
  narration for 591 tokens, the long assembled in 8 s, the short in 10 s, loudness -18.9 dB mean after the
  normalisation. Frames checked at full size: the card, the push-in, the clip, the short's title and caption.
- The second narration pass of the same text: 0 sections rendered, 0 tokens (the cache works).

## The first night (2026-10-21, 01:09 to 01:32)

- Two `record` runs, both CLEAN, 23 of 23 bot steps. The first gave clips of 0.1 to 2.3 s: the bot is fast by
  design. `Linger(seconds)` now holds the game clock inside a clip, on video only. The second run: seven clips of
  1.9 to 4.9 s (`runs/20261021-011231-record-406267`, `~/glorified-body-renders/devlog/clips/latest/`).
- The writer's v1 scripts: ep00 long 18 sections 923 words, short 7 sections, ep01 long 20 sections 992 words,
  short 7 sections (`the-glorified-body/devlog/ep0{0,1}/`).
- `night.sh ep00` and `night.sh ep01`: ep00 long 353 s, short 59 s. ep01 long 387 s, short 58 s. No MISSING
  visual. Packets: `~/glorified-body-renders/devlog/ep00/v1/README.md` and `ep01/v1/README.md`.

## Owed

1. Bruno's notes in `NOTES.md` next to each video, then `/devlog-night ep00` (and `ep01`) for v2.
2. The writer's open points for Bruno: no company or product names in the narration (the rule), the child Lucy
   of the series in the ep00 slice shots (sections 6 and 11 of the long, 3 of the short), the test counts on the
   ep01 card (27 EditMode, 36 PlayMode, 21 bot steps as briefed, or the newer 45 and 23).
3. Language: the scripts are in English (the clone was recorded in English). A pt-BR version through the Dubber
   is a later step.

## Files

`tools/devlog/{README.md,catalog.py,script.js,narrate.js,assemble.py,night.sh,rfab_client.js}`,
`Assets/_Game/Scripts/Automation/FrameRecorder.cs` (new), `BotArgs.cs`, `StructuralPlaythrough.cs` (the clip
markers), `Assets/_Game/Scripts/Editor/Automation/PlaythroughRunner.cs` (`-gbRecord`), `tools/unity.sh`
(`record` mode), `.claude/skills/devlog-night/SKILL.md`. Private, outside git:
`~/glorified-body-renders/devlog/voices.json`.
