# 2026-10-31, evening: four pieces the demo needs

Before the greybox of the demo, I checked each beat of the demo plan (`game/DEMO_PLAN.md`) against the code. Four beats
needed a system that the game did not have. Bruno: "Build them now". The words are the writers' drafts. The numbers
are PROPOSED.

## 1. The feed lock (round 18, CANON)

"The feed starts locked. Reading log L9 sets one flag that unlocks it for the rest of the game. Before the flag, a
body gives no prompt and no HP."

- A log can raise a story flag when she reads it (`ItemDefinition.readFlag`), from the page of the pickup or from the
  inventory.
- The feed is locked while the flag `feed_locked` is up and `feed_unlocked` is not (`Feeding.Locked`). A locked body
  gives no prompt, and the feed refuses.
- The story raises `feed_locked` at its start (a sequence step in W1). Her care chart (L9) carries
  `readFlag = feed_unlocked`. A room with neither flag (the test rooms, the tutorial) feeds as before.
- The flags are in the saves already.

## 2. The hiders (the demo plan: W4, W8, G6)

A civilian who hides: the nurse in the W4 shower stall, the technician behind the W8 chair, the clerk in the G6 stall.
They give the most HP, so the player who needs HP must choose a person who hides.

- `EnemySpawn.hides` with two lines makes a hider (`CivilianBrain.SetHider`).
- It waits crouched at its place. It makes no sound to the others: no alert by sight, no scream, and a death or a
  shot near it does not move it.
- When she comes within 4.5 m it whispers its line (the writer's: "It is sown in weakness. It is raised in power...").
- Within 2 m she finds it: it says its found line ("I'm not here. Please. I'm not here.") and begs where it is.
- It never runs: a cornered hider cowers at its place.
- WARD TEST has one in the north-east corner.

## 3. The bulletin (T11)

The last card of the demo: white monospace on black, typed one character and one line at a time, after cutscene 10.

- The wording comes from the number of people she killed. A new count, `PsychicStats.People`, counts every person
  she killed, a civilian too (before, only armed kills counted). It is in the saves.
- The bands: up to 10 low, up to 20 middle, more high (PROPOSED: the map forces about 8 kills).
- The world stops while it shows. Interact skips the typing, then closes the card.
- A sequence step shows it (`StepKind.Bulletin`).

## 4. Wall signs (§2.3 of the plan)

`RoomKit.Sign`: a dark plate with the words on a world-space canvas, the face to the room. OUTFIT TEST has the W6
poster "THANK YOU FOR THE HARD DAYS." over the lockers.

## Proof

| Check | Result |
|---|---|
| WardTests (3 new: the feed lock, the hider, the bulletin), OutfitTests (the sign), ScriptTests | 25/25 |
| CivilianTests, KitTests, SaveGoreTests, TravelTests, TutorialTests, InteractionTests | 58/58 |
| The bot: `tools/unity.sh play WardTest Main_Menu pieces 300` | 4/4 steps |

Shots for Bruno: `~/MEGA/glorified/renders/game/demo_pieces/shots/`

| File | It shows |
|---|---|
| `pieces_01_hider_whispers.png` | the hider crouched in her corner, the whisper as a subtitle |
| `pieces_02_hider_found.png` | Lucy close: the hider begs where she is |
| `pieces_03_bulletin_typing.png`, `pieces_04_bulletin.png` | the card, typing and whole (15 deaths: the middle wording) |
| `pieces_05_sign.png` | the W6 poster over the lockers |

## His answers

```text
1,2: keep that
```

The bulletin bands stay at 10 and 20. She finds a hider at 2 m, with no facing rule.
