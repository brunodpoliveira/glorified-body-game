# 2026-11-15: movable things out of the NavMesh bake, a larger dining hall

Bruno, with two Scene view shots (a crate in its NavMesh hole, and the foyer with holes under the plinths):

```text
physic objects on the ground are interpreted by the autopath creator for ai as a permanent obstacle
also, the dining hall needs to be a lot larger; it is meant to be this opulent place, and it needs to be large enough for flanking opportunities both for the player and the enemies
```

## 1. The NavMesh holes under movable things

The fact-check: CONFIRMED.

1. Each room's NavMesh bake takes every physics collider on the Default layer (`RoomKit`, `NavMeshSurface`,
   `PhysicsColliders`).
2. The crate (`PropAssets.Crate`), the physics box (`PropAssets.Box`), the planter (`DemoRoom.Planter`) and the statue
   copies of the foyer stand on that layer, and none of them left the bake. So each one cut a fixed hole at its start
   place.
3. The box, the planter and the copy also carve a live hole (`NavMeshObstacle`). After a move there were two holes: the
   stale one from the bake and the new one. An enemy walked around a crate that was not there any more.
4. The gurney and the breakable props already did it correctly: they leave the bake and carve a live hole only.

The fix: `PropAssets.OutOfTheBake` gives a movable thing a `NavMeshModifier` that leaves the bake, and a carving
obstacle that cuts its hole only where it rests (`carveOnlyStationary`). The crate (so also every box), the planter and
the statue copy use it. When a thing moves, its old hole goes away, and its new hole comes when it stops. A thrown crate
that breaks leaves no hole.

Scenes built again: the demo, the training room, KIT TEST, the tutorial, DOOR TEST and PHYSICS TEST.

## 2. The dining hall

| | Before | Now |
|---|---|---|
| Size | 12 x 10 m, 5 m high | 24 x 13 m, 6 m high (x 35 to 59, z 0 to 13) |
| Columns | 4 | 6, in two rows |
| Cover (tables on their sides) | 4 | 8, in islands |
| Other | 2 banquettes, the bar | 4 banquettes, the bar (7 m), 2 laid tables, 3 chandeliers |
| The lodge door | x 44 | x 50, in the north wall |

1. The hall grew east and a little north. East of it, nothing stands on floor 1. North, the ward's yard backdrop stands
   1 m past the new wall, so the hall cannot grow more to the north.
2. The doors stay: the glass doors from the concourse G2 in the west wall, and the door to the kitchen G19 in the south
   wall.
3. The cover stands in islands with lanes between them and along both long walls. So each side has a way round the
   other: the soldiers can flank her, and she can flank them. Every lane is 1.2 m or wider (the NavMesh rule of
   2026-11-05).
4. The six soldiers stand deeper in the hall: the two shields at the front, the two shotguns in the middle islands, the
   two grenadiers at the back corners. The power armour still steps out of the lodge door at three left.
5. The chandeliers are gold boxes with no collider (greybox). Numbers PROPOSED.

## Three faults the tests found on the way

1. **The cover map and the live carves.** The cover map of the soldiers reads the NavMesh triangulation, and the
   triangulation does not show the live carves. With the planters of G2 out of the bake, it gave cover spots inside
   the planters' carved holes, and no soldier of G2 took cover in 40 s. The cover map now keeps a point only when the
   live NavMesh holds it (`CoverMap.LiveTolerance`). After the fix, a soldier ducks after 6 s.
2. **Six soldiers on one point.** On a leash, a move to a point outside his room went to the nearest point inside it.
   When she ran out of the hall, all six took the same point at the door, and the crowd pushed one of them into G2. Now
   a move to a point outside the room keeps him where he stands (`SoldierBrain.Go`).
3. **OPEN: the shotgun test fails in the new hall.** `DiningHallTests.TheShotgun_...` kills the other five and the
   armour around the gunner, puts her 3.5 m from him, and waits for his cone. In 4 of 5 runs he came into her reach and
   struck with the butt instead of firing (melee within 1.8 m is the design). The logged trails show three ways in:
   - He was still running from the deaths around him (4.4 m/s) when she came. The test now waits until he stands still.
   - His route to a firing spot 3.8 m from her passed her at 1.6 m, and a move stops for a strike when she is in reach
     (`Phase.Move`, `gun.HerInReach`). The flankers of 2026-11-03 did the same.
   - He left the care of a body and ran at her, 4.0 m to 0.9 m in 1 s, while his spot stayed 5.4 m from her.
   A rule that refused a spot with a route past her did not make the test pass, so it is not in this commit. The fault
   is in the soldiers' moves, not in the hall. In the old hall the test passed. It needs a session of its own.

KIT TEST shows 20 of 1002 floor points hidden from their camera (5 of 1034 this morning). The cause is the four
ceiling lamps of this afternoon's commit, not the NavMesh: they stand on the lines from `Cam_WestHalf` to the points
near x -4, z ±5. The test room allows 5 %.

## For the writer

`DEMO_PLAN.md` gives the dining hall no size. The new size and the lodge door's new place are for the plan.

## Proof

| Check | Result |
|---|---|
| The builds: the demo, the map, the training room, KIT TEST, the tutorial, DOOR TEST, PHYSICS TEST | 7 of 7 clean |
| `WarspiteWaveTests.AMovedCrate_LeavesNoHoleBehind_AndCarvesOneWhereItRests` (new) | pass: a hole where it rests, none at the old place after a 2.5 m move, a hole at the new place |
| `DemoTests` (the whole class) with the hall | the camera check passes: 0 of 10,486 floor points out of frame |
| `DemoTests.ASoldier_TakesCover_...` (G2, the planters) | failed (no cover in 40 s), passed after the cover map fix (a duck after 6 s) |
| `DemoTests.TheForwardPost_HoldsTheHall_...` | failed (a man pushed into G2), passed after the leash fix |
| `FloorplanV2Tests` | the H1 place and the room count (58) follow the Hold rework of today, then pass |
| The fight classes (`FlankingTests`, `EnemyAiTests`, `KitTests`, `ArmedCivilianTests`, `HumanShieldTests`, `CivilianEscortTests`, `CoverTests`, the fire team tests, `DiningHallTests`) | 57 of 58 (that run had the route rule, since removed). The rerun without it: stopped by Bruno ("end the final check now") |
| `DiningHallTests.TheShotgun_...` | FAILS: open, above |

Not run: the whole PlayMode suite and the bots (Bruno tests by hand).
