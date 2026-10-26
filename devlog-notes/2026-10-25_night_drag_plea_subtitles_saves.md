# 2026-10-25, night: the limp drag, the plea, the subtitles, the saves

Bruno's answers:

1. The scream: "keep it for now, once we start refining the game we'll add the final sfx, voices, etc".
2. "hands raised in a plea could be one of the poses".
3. "when dragging the body, the ragdoll should reactivate, and once the body is setup to cpr mode, freeze the ragdoll
   again".
4. The study `2026-10-25_study_saves_subtitles.md`:
   - Saves: "only in save rooms".
   - Checkpoints: "only at specific save points".
   - Slots: "3 manual save slots, one for the auto checkpoints".
   - Subtitles: "on by default", at the bottom, "yellow with black outlines", and they "must always identify speaker".
   - "english only for now".

## 1. The limp drag

- The drag makes the dead body limp again (`EnemyRagdoll.BeginDrag`). A hand holds the torso between the shoulders,
  0.5 m over the carer's feet. The hips and the legs trail on the floor.
- At the CPR the carer lets go (`EndDrag`). The body falls, settles and freezes again (`Corpse`).
- A heavy, or a body with no ragdoll, keeps the old rigid drag.
- Measured in the test: the grip 0.02 m from the carer's hands, the grip 0.50 m up, the feet 0.05 m up, then
  `Corpse` with every bone still.

## 2. Faults found on the way (all fixed)

| Fault | Cause | Fix |
|---|---|---|
| A carer stopped at a desk and never reached the body. | The step height of the NavMesh agent was 0.75 m, the height of a desk. The bake made the desk tops into steps, and the paths went over the desks. | `NavAgentSettings.Ensure` sets 0.4 m before each room bake. The three test rooms are built again. |
| The old drag pulled a body 5 m across the room. | A walk that timed out started the drag where the carer stood. | The care ends ("no way to the body") when the carer is more than 1.5 m short. The kneel spot sits on the NavMesh. |
| A leaning civilian that died sank through the floor and into the wall. | `WallLean` moves the hips every frame. On a limp body nothing resets them. They went 8 cm down each frame. | The lean acts only while the Animator plays. A new test throws a dead, leaning civilian at its wall: the hips stay on the floor, in the room. |
| A pleading figure lost its body 16 m away. | `SetHumanPose` also places the body, from the world's origin. | `PleaHands` puts the hips back where they were. The test checks the hips stay at the figure. |

The first two faults existed before tonight. The rigid drag hid them: it moved the body to the carer from any
distance, and through a wall.

## 3. The plea

- Every second civilian (the lean and the shaking variants) begs with both hands up (`PleaHands`). The palms
  point to her, and the head comes up. The others keep the hands low.
- The pose is by code, on the Unity humanoid muscles, so one set fits every rig.
- I picked the numbers from rendered sheets (`PoseProbe`, `~/glorified-body-renders/game/civilians/plea_probe/`).
- The first pick sank to the knees in the game, because the sit and the lean hunch the back. The hands are now at
  the face.

## 4. The subtitles

- Every spoken line shows at the bottom of the screen, yellow with a black outline, the speaker's name first:
  `NURSE: Breathe for me.` (`SubtitleService`, `SubtitleUI`).
- The barks go there (no more text over the head). The death screen's voice line too
  (`COMPUTER: Subject contained.`). Lucy's scream at the short shows as `LUCY: [screams]`.
- The speaker is the cast name (`Speaker`, set by the enemy factory: NURSE, LAB WORKER, SECURITY GUARD). Lucy is
  LUCY.
- Three lines at most, one per speaker, the newest at the bottom. A line stays 1 s plus 55 ms a letter, 7 s at
  most (PROPOSED). A pause or the freeze holds it.
- Options > Display: Subtitles (on), Subtitle Size (Small, Medium, Large).
- Cutscenes: a Timeline track (`SubtitleTrack`, one `SubtitleClip` per line with its speaker). No cutscene exists
  yet. The test plays one made in code.
- No paid package (the study). English only.

## 5. The saves

- The save machine (`SavePoint`) is a placeholder steel cabinet with a lit screen.
- Interact asks "Save your progress?". Then the slot menu opens over the stopped game.
- W and S move, E saves, and Escape closes. A used slot asks "Overwrite slot 2?" first.
- The note "SAVED IN SLOT 1" shows at the top.
- The checkpoint (`CheckpointPoint`, a box the room builder places): she walks in, the game saves to the checkpoint
  slot, the note "CHECKPOINT" shows.
- A death: RETRY loads the checkpoint of this game. A manual save and a load also count as the checkpoint
  (PROPOSED), so a death never goes further back than the last save. A new game from the menu ignores an old
  checkpoint.
- Main menu: CONTINUE (the newest file) and LOAD GAME (the three slots and the checkpoint).
- A save keeps her: the room, her place, HP, MK, SHORT, the outfit, the kills, every item and pill.
- A save keeps the room: the dead figures, the taken items, the open doors, the broken cover of every room she saw.
- A save keeps the story flags (none exist yet).
- After a load the dead stay dead and their bodies are gone (PROPOSED).
- The files are JSON in the player's data folder: `saves/slot_1.json` to `slot_3.json`, and `checkpoint.json`.
- Each file has a version number. A broken file, or one of another version, shows "cannot read" and does not load.
- Tests and bots write to `saves_batch`, never to the player's own saves.
- The training room has one machine (the east wall) and one checkpoint (the south-west corner).

## Proof

- New tests: `CivilianTests` (the limp drag and the freeze, the plea, the low hands, the fall at the wall),
  `SubtitleTests` (5), `SaveTests` (5), `SaveDataTests` (3, EditMode), and a subtitle check on `DeathScreenTests`.
- The bots: the civilian bot has a subtitle step (13 steps), the structural bot a save step.
- Shots in `~/glorified-body-renders/game/civilians/2026-10-25_night/`:
  - `sheet.png`: the drag, the CPR, the slide.
  - `civ_08c_beg_on_floor.png`: the plea.
  - `civ_08d_subtitle.png` and `death_subtitle.png`: the subtitles.
- Shots in `~/glorified-body-renders/game/saves/2026-10-25/`: the slot menu and the note.
- The full run (21:14): EditMode 46/46, PlayMode 181/181, and the six bots pass (ai, cover, civilians, tutorial,
  structural, look). The structural bot's slowest frame is 515 ms.

## Open for Bruno

1. A manual save also writes the checkpoint, and a load does too (PROPOSED). Keep it?
2. After a load, dead enemies are gone, not lying there (PROPOSED). Keep it?
3. The tutorial gets a save step when the save room's look exists?
