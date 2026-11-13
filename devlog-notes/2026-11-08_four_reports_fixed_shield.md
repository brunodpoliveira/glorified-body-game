# Four reports from the demo, and the fixed floor shield (2026-11-07 night, 2026-11-08)

Bruno played the demo in the editor and sent four reports with four screenshots:

```text
ward's sister card; door won't appear open but you can go pass throught it

in some areas the walls are not fully built, so i can see through scenarion; that's fine, but auto build logic needs to be verified to avoid such gaps

she won't drop the guy even after being hit multiple times
check if whole level is being rendered or if just editor convenience; i'm noticing some frame rate things (it seems to be locked at 30 fps or lower) so we nmeed to cull rendering to what the player can see; this is easy due to the fixed camera angles
```

Then, on the floor shield:

```text
floor shield does not depend on her hold; it stays there until it is destroyed
yes, it should be fixed in place; then it'll make more sense. it's like she's pulling chunks of the floor and bending them up to create a makeshift barricade
```

## The verdicts

| Report | Verdict | Cause | Proof (a test that failed first) |
|---|---|---|---|
| The card door looks shut, but she walks through | CONFIRMED | `RoomKit.Box` marks every box static, and `DemoRoom.LockedLeaf` did not clear the flag. Static batching drew the leaf in its first place, and only the collider went | `DemoTests.EveryCardDoor_SlidesItsDrawnLeaf_WhenItOpens`: "Door_S1: a static batch draws the leaf at its first place" |
| Gaps in the walls | CONFIRMED BUT DIFFERENT | The walls are complete. The rooms had no ceilings, so the top of a frame looked over the far walls into other rooms, and from floor 1 up into floor 2. 15 of 125 cameras showed a gap, `Cam_G1_00` too | `DemoTests.NoCamera_SeesAnotherRoom_ExceptThroughADoorway`: 15 cameras with a gap, then "no ceiling" in 41 rooms |
| She keeps the floater when a bullet hits her | CONFIRMED | Only a hit on the floater dropped it (rule of 2026-10-22). A hit on her did nothing | `PowersAndBarsTests.Telekinesis_ABulletOnHer_DropsTheFloater_Unhurt`: "a bullet on her breaks the hold" |
| 30 fps | CONFIRMED BUT DIFFERENT | Mostly CPU, not drawing. In batch, where no camera draws, a demo frame took 16 ms against 2 ms in AI TEST. 33 carers searched the whole scene for a body in each frame: 7 ms | `DemoTests.TheCarers_LookForABody_AFewTimesASecond_NotEachFrame`: 26,106 searches in 2 s against a limit of 330 |

## What changed

1. **The card door.** The leaf is not static. A test checks that each card door leaf is out of the static batches and that the drawn leaf slides with the door.
2. **Ceilings.** Each room has a ceiling at its height (`DemoRoom.Ceiling`), in the dark tone of the clear colour. It has no collider and casts no shadow, so the sun and the gameplay do not change. The stair room S1 has one too.
3. **The gap check.** `Rooms/RoomSight.cs` casts 96 x 54 rays from each fixed camera. A ray that leaves the camera's room outside a doorway, below the ceiling, and hits something is a gap. The test also checks that each room has its ceiling and that no camera stands over its ceiling. Result now: 0 gaps in 125 cameras.
4. **The cull.** The same scan lists the rooms each camera sees (`FixedCamera.sees`, written by `DemoWorld.Sight` at the end of `BuildAll`, or by `DemoWorld.Resight` on the saved world). `RoomTracker` now draws only those rooms and her room, and it culls again at each camera cut. A wall or a lintel shows when any room on its line shows. Before, it drew every wall and lintel of all 41 rooms. Now a camera sees 3.3 rooms on average. Door leaves and breakables do not stop the scan, so an open door or a broken barricade never shows a hidden room.
5. **The carers.** An idle carer looks for a body four times a second, in its own frame, from a list of the figures (`EnemyMorale.All`), not with a search of the scene.
6. **A hit on her drops what she holds.** The floater drops unhurt, and so does a crate.
7. **The floor shield is fixed in place.** A full circle tears a slab out of the floor in front of her. The slab bends up on its near edge into a standing wall, 1.2 m wide and 1.6 m tall, 0.5 m in front of her. No lift holds it: it does not turn with her, she cannot throw it, and a hit on her or the short leaves it. It stays until bullets break it (80 HP). Walkers go around it, because it carves the NavMesh. Another slab may rise while one stands, one for each press of the defend key. The tutorial hint, the `shield` bot and `FloorShieldTests` follow the new rule.

## The frame, measured

`DemoTests.TheFrame_Profile` (explicit, runs only when the filter names it) samples every profiler time marker for 150 frames, then turns off one script type at a time on the figures.

| | Before | After the carer fix |
|---|---|---|
| Demo frame, CPU, batch | 16.0 ms | 6.6 ms |
| Script `Update` calls | 10.1 ms | 1.7 ms |
| Frame with the 34 figures off | 1.4 ms | 1.1 ms |

The rest is mostly figure animation. The figures keep `AnimatorCullingMode.AlwaysAnimate`: the gun hold, the ragdolls and the batch tests read their bones. In the editor, the Scene view also draws all the Invector gizmos of the figures in each frame. Close the Scene view, or turn the gizmos off, before you judge the frame rate.

## Also in the rebuild

The writer removed the two CROWN readout lines from cutscene 02b in round 52 (`02b_the_chart.md`). The demo rebuild read that file, so `cutscene_02b_the_chart.asset` lost its last shot. That is the writer's change, kept.

## Proof

| Check | Result |
|---|---|
| The changed classes after the room fixes: `DemoTests`, `PowersAndBarsTests`, `EnemyMoraleTests`, `CivilianTests`, `SystemSeamTests`, `SaveGoreTests`, `FloorShieldTests`, `PhysicsTests`, `MapTests`, `EdgarTests`, `RagdollTests`, `ScriptTests`, `DoorTests`, run `20261107-235441-test-playmode-66156` | 222 of 224. The 2 failures were stale sight lists and an old cull test that expected the old rule. Both fixed |
| `FloorShieldTests`, `PowersAndBarsTests`, `DemoTests`, `TutorialTests`, after the shield change, run `20261108-003145-test-playmode-72135` | 109 of 109 |
| Bot `shield`, run `20261108-005125-play-73880` | pass, 4 of 4, clean |
| Bot `tutorial`, run `20261108-005202-play-74201` | pass, 36 of 36, clean |
| Bot `demo`, run `20261108-005544-play-76070` | pass, 172 of 172, clean |

Not run on this code: the whole PlayMode suite and the other bots. Rule 10 asks for them before a push. Nobody played this by hand.

Shots: `~/MEGA/glorified/renders/game/four_reports_2026-11-08/`. `demo_cam_g1_00.png` is the camera of the first screenshot: the top right shows no other room now. `shield_02_shield_stays.png` shows the slab in place after she steps aside.

## Limits and open points

- The cull works at room level. A thin slice of a room that the 96 x 54 grid misses would show as dark. No test found one.
- The figures are not culled by room. Their AI and animation run in every room.
- A standing slab is not in the saves: a load takes it away (the scar stays). PROPOSED.
- All numbers are PROPOSED: the slab size, its HP, the four searches a second.
