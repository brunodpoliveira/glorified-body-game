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

## Version 2, the same night (01:50 to 02:11)

Bruno (2026-10-21, CANON, now in `03_GAME_DESIGN.md` section 6): full transparency, the devlogs name every tool,
model and provider. The writer changed 20 of 52 sections (Unity 6, Blender 5.2, Mixamo, Tripo behind 3D Studio,
Fish Audio behind the Voice Cloner, Gemini TTS behind Voice Scenes, Nano Banana Pro, GPT Image 2, Seedream 4.5,
Seedance, Mureka V9, Lyria 3 Pro, Claude Code and Claude Fable 5.1, ffmpeg) and showed the nine game models in
ep01. The audio cache paid only for those sections: 18,591 tokens for v2 against 35,136 for v1. Packets:
`~/glorified-body-renders/devlog/ep00/v2/README.md`, `ep01/v2/README.md`. Lengths: ep00 long 6:48, short 1:16,
ep01 long 7:14, short 1:05. The whole night: about 54,000 tokens.

## Owed

1. Bruno's notes in `NOTES.md` next to each v2 video, then `/devlog-night ep00` (and `ep01`) for v3.
2. Open for Bruno: the child Lucy of the series in the ep00 slice shots (sections 6 and 11 of the long, 3 of the
   short), the test counts on the ep01 card (27 EditMode, 36 PlayMode, 21 bot steps as briefed, or the newer
   45 and 23), and the voice section's new line that the script is drafted by Claude Code from his notes.
3. The nine-model strip in ep01 section 18 is wide and small in the frame. A crop or a seq of single models
   would read better. The writer picks in v3.
4. Language: the scripts are in English (the clone was recorded in English). A pt-BR version through the Dubber
   is a later step.

## Files

`tools/devlog/{README.md,catalog.py,script.js,narrate.js,assemble.py,night.sh,rfab_client.js}`,
`Assets/_Game/Scripts/Automation/FrameRecorder.cs` (new), `BotArgs.cs`, `StructuralPlaythrough.cs` (the clip
markers), `Assets/_Game/Scripts/Editor/Automation/PlaythroughRunner.cs` (`-gbRecord`), `tools/unity.sh`
(`record` mode), `.claude/skills/devlog-night/SKILL.md`. Private, outside git:
`~/glorified-body-renders/devlog/voices.json`.

## Episodes 2 and 3 (2026-10-23, 00:30 to 02:10)

- The tutorial bot and the look bot record clips now: one clip per tutorial step with the prompt and the HUD
  (`FrameRecorder.OverlayProvider`), a camera tour and a run for each dressing in both look rooms, the LOOK option.
  `BotClock` (real time in a normal run, 1/30 s per frame in a record run) replaces the real-time waits of the
  tutorial drive, so its timing holds on video. The other session's tutorial bot ran it in real time: 21/21.
- Fault found: the hold after the last tutorial step used the game clock, and the game pauses at "complete", so
  the clip never ended (12,647 frames, 22 GB). Fixed: the hold uses `BotClock.Wait`, and `FrameRecorder` ends any
  clip at 60 s. The 18 other step clips were good. No script uses step 19.
- Four record runs, all CLEAN: training room 7 clips (with the models), tutorial 18, LookTest 10, LookTest2 10.
- The first render failed at the narration: the local backend was down ("fetch failed"). `night.sh` now checks
  `/status` first and stops with a clear message.
- Renders: ep02 long 7:02, short 1:10. ep03 long 6:34, short 1:09. Narration 37,388 tokens. Packets in
  `~/glorified-body-renders/devlog/ep02/v1/` and `ep03/v1/`.

## Episode 4 (2026-10-25, 01:20 to 02:10)

- The AI bot records 12 clips (commit `50189bf`). `FrameRecorder.CameraProvider` lets a bot film with its own
  camera: a close camera follows the carer through the drag and the CPR, and the guard who cowers (no HUD).
- Record run `runs/20261025-013936-record-603053`: AI bot 29/29, CLEAN. The batch slot was shared by message
  with the session that builds the civilians.
- Ep04 v1: long 7:09 (22 sections), short 1:03 (7 sections), 19,728 tokens of narration. One gore shot, the
  limb cut, 3.4 s, wide and low detail, long video only. A "This is a work of fiction" card at the end.

## Episodes 4 (re-planned), 5 and 6 (2026-10-31, 00:30 to 01:40)

- The renders of episode 4 were lost in the move to `~/MEGA/glorified-body-renders`. Its scripts and the 12 AI
  clips (in `runs/20261025-013936-record-603053/clips/`) survived.
- `DevlogClip` (Room, Close, End) is the shared clip helper. The civilians bot records 8 clips (`7b3b1da`).
  The AI room recorded again: 12 clips, CLEAN.
- RAM gate: the record runs waited at 4,500 MB with 4,197 MB free. `GB_MIN_AVAIL_MB=3800` ran both.
- Known fault: a Close clip whose side comes from `Lean.WallNormal` (cower, slide) films the wall, while the bot's
  still from the same spot is right. The videos use the stills. The drag clip is good for its first 17 s.
- Renders: ep04 v2 7:03 and 0:59, ep05 v1 6:54 and 1:01, ep06 v1 6:56 and 1:05. Morning summary:
  `~/MEGA/glorified-body-renders/devlog/MORNING_2026-10-31.md`.
