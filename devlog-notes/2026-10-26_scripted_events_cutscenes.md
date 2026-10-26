# 2026-10-26, night: scripted events, story flags, the PA, cutscenes

The design sweep's ranks 2 to 4: nothing set or read a story flag, no scripted beat existed (the scientist's
ultimatum, code silver, the QRF wave, the sealed gate), and no cutscene player existed. Bruno: ranks 1 to 5 first.

## What the game does now

| Part | Behaviour |
|---|---|
| A sequence | `ScriptedSequence`: steps in order. The steps: wait, say a line (a figure in the room, or a voice with no body: a radio, a collar's computer, Stevens), the PA (the chime, then "PA: ..."), raise or drop a flag, wait for a flag, show or hide objects, alert a group of soldiers, wait until a group is dead, set her SHORT bar, hold or free the music, play a cutscene, hold her feet still, free them. A line waits for its clip, or for the time to read it. |
| Once | A sequence raises its done flag at the end. The save keeps the flags, so a load or a return does not run it again. |
| A trigger | `ScriptTrigger`: a box she walks into starts its sequence. |
| A gate | `FlagGate`: its objects show or hide with a flag, at once and after a load. For the sealed gate of code silver, a squad that comes after the alarm, a body that is there after a beat. |
| An exit | `RoomExit.requiredFlag` (block 3) waits for a flag too. |
| A cutscene | `CutsceneAsset`: its shots in order. A shot is a still with a slow pan and zoom, or a video. Each shot has one line (the speaker, the voice id, the words). Its own music, or silence. |
| The player | `CutscenePlayer`: a new game state, Cutscene. The world stops, no game key passes, the world sounds pause. Each shot fills the screen under the subtitles. Its line plays `Resources/Voice/<id>` when it is recorded. A fade through black at both ends. The Interact key (or Escape, Enter) skips it, and a skipped cutscene counts as seen. |

Code: `Assets/_Game/Scripts/Story/` (namespace `GlorifiedBody.Story`), `GameManager.BeginCutscene` and `EndCutscene`,
`StoryFlags.OnChanged`, `Voice.SayAs`, `MusicDirector.Score`. The freeze of the short waits during a cutscene, as
it waits during the pause (the bug of the seam sweep).

## The test room

`ScriptTest` (`ScriptTestSetup.BuildScriptTest`), in git:

- The scientist's ultimatum of the story (design §9.3, Bruno's words, placeholder lines for the writer): the scientist
  asks his collar, the computer answers, he repeats, he reports that she did not comply, the computer answers again.
- Then her SHORT goes to 90 (the story starts her near a full SHORT), and the PA calls code silver.
- A gate opens at the end.
- A trigger plays the test cutscene. A save machine.
- The voice ids name the files to record, for example `scientist.computer_subject_has_escaped` and
  `computer.with_a_firm_tone`.

The test cutscene (`Resources/Cutscenes/cutscene_test.asset`): two stills (the scan card, the gear emblem) and the scan
video, three lines. Lucy's line uses the voice test clip, so a recorded line is proved. Frame:
`~/glorified-body-renders/game/story/2026-10-26/cutscene_shot0.png`.

## Proof

`ScriptTests` (5):

- The ultimatum says its six lines in order, with the speakers SCIENTIST, COMPUTER and PA. She stands still while they
  talk, then moves again. Her SHORT is near full, the flag is up, the gate opens. It runs once.
- She walks into the trigger, and the sequence starts.
- The flag and the open gate survive a save and a load, and the sequence does not run again.
- A cutscene stops the game: the state, the time, the keys (only the skip key passes), the world sounds. It shows the
  still, the still and the video with their speakers. The recorded line plays. The game plays again, and the flag is up.
- The skip key ends it at once, and the flag is up.

The full run with blocks 1 to 4: EditMode 49/49, PlayMode 222/222, the six bots CLEAN.

Two faults found by the tests and fixed:

- The video shot showed the last still until the video was ready. It shows the video's (black) picture at once now.
- The shot counter read one past the end during the fade-out.

## For later

- The writer's lines for the ultimatum and the other beats (`game/story_demo.md`).
- Real stills and clips (Nano Banana Pro, Seedance), and the voices.
