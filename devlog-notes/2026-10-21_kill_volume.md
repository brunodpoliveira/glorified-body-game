# 2026-10-21: the kill volume is out of the training room

Bruno (2026-10-21, with a shot of the death screen): "colliding against this rectangle kills the player instantly".

## What it was

The dark box at the east wall, 1 x 2 x 1 m at (4, 1, 0), was the step 1 kill volume: a trigger cube with a
`PlayerDeathTestVolume` that kills on entry and skips the second wind. Step 1 put it there for the bot of the
menu loop. The bot and the tests kill through `Health.TakeDamage` instead, so nothing used it. It looked like a
prop, it had no mark, and the room had no other box of that shape.

## The fix

1. `MenuLoopSetup.BuildGameplayScene` removes `KillVolume` from the scene (the builder opens the existing scene,
   so the removal is an explicit `RemoveObject`). The `PlayerDeathTestVolume` script stays in `Scripts/Level/`
   for a real pit or a trap later. No scene has one now.
2. PlayMode test `TankControlTests.TheRoom_HasNoKillVolume_SheWalksTheEastWallAndLives`: the room has no
   `PlayerDeathTestVolume` and no `KillVolume`, then Lucy walks east through (4, 0, 0) for 2 s and stays alive
   at full HP, the state stays Playing.

## Proof

PlayMode 55 of 55 (with the dodge tests of the same day, `2026-10-21_dodge_input.md`), the bot 24 of 24, all
CLEAN. RUNS: 20261021-175122-exec-55310 (the scene rebuild), 20261021-175156-test-playmode-55723,
20261021-175522-play-56367.
Shot: `~/glorified-body-renders/game/kill_volume/play_training_room.png` (the wide camera, no box at the east wall).
