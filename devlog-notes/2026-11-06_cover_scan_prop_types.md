# 2026-11-06: cover that finds itself, props that break by type

Bruno:

```text
Question: how will/can a player deliberately target a civilian? how about a physics object? also, the ai does not take cover
because there are no places assigned as a cover (both destructible and indestrucible types). can those be added
automatically/programatically? adding them manuially one by one is a non-starter
build this now, and add some prop types
```

Verdict on the report: confirmed. `TryCoverSpot` looked only at `Breakable` objects marked as cover, and the demo builder placed none.

## What changed

| Piece | What it does |
|---|---|
| `Enemy/CoverMap.cs` | At the first need in a scene it walks the live NavMesh on a 0.5 m grid and casts rays. A hit at 0.7 m with a clear line at 1.35 m is low cover. A hit up to the chest with a clear side is a corner. No marks, no build step, every room and test room. It stays one list, and a spot on a broken piece ends |
| `SoldierBrain.TryCoverSpot` | The nearest free spot with the thing between him and her, inside his gun's range, a clear line from where he fires, no line from where he hides, and a walk that keeps 3 m from her. A corner costs 3 m more than low cover. The power armour takes no cover (2.6 m tall). The knife still rushes |
| `Phase.Duck` | Between shots he hides for 1.5 to 3 s: he crouches behind low cover, or goes back behind the corner. He stands or steps out to fire. He leaves when she gets past the thing, when the piece breaks, or when she leaves his range |
| `LockOnTargetable.InCover` | While he hides, his lock point drops 0.5 m. A thing on her line stops mk_warspite and mk_erebus: a breakable takes the damage, a solid thing takes none. mk_orzabal still reaches him. He is open while he fires |
| `DemoRoom.Prop` by type, `BreakableAssets.PropBox` | Fifteen types break: planter, desk, reception desk, lectern, low table, the four carts, gurney, filing, cabinet, copier, box, bin. Counters, racks, shelves, fridges, lockers, sinks and walls do not. A prop chest high (0.7 m or more) is cover. Each one stays out of the bake and carves the NavMesh, so the way opens when it breaks |
| `Breakable.DropWhatRests` | An item on a broken prop falls to the floor, and she can still take it. A plain prop on top goes with the break |

All numbers are PROPOSED.

## Faults found on the way (the tests failed first)

1. A soldier chose a spot past her and ran by her at arm's reach: the walk now keeps 3 m from her.
2. He preferred a wall corner to a barricade next to him: a corner costs 3 m more.
3. He stopped 1 m short and beside the barricade, so the barricade did not stand between them. He now walks to within 0.4 m (or 1.2 m if the vendor AI stops short).
4. He never fired over a 1.2 m barricade: the gate read the line from the gun at the low ready. `DummyShooter.FromCover` reads it from the shoulder while he fires from cover.

## Two faults outside the cover work

1. **The writer's folder moved** to `/home/pc/MEGA/glorified` (2026-11-05, 17:01). A rebuild then emptied the cutscene and beat lines with only a warning. I corrected the path in `DemoContent`, `DeathScreenSetup`, three tools, one skill, `CLAUDE.md` and one memory. The rebuild restored the lines. A warning does not stop a build, so the next move can do the same.
2. **The suite now takes 31 minutes**, and `tools/unity.sh` stops a run at 30. Use `GB_BATCH_TIMEOUT=5400 tools/unity.sh test playmode`.

The old path also hid a stale test: `DeathScreenContentTests` wanted 7 writer rows and a "COMPUTER:" guard line. The writer's file has 6 rows since round 28. The test now reads that. `DeathScreenContent.asset` still holds the older lines: run `DeathScreenSetup.Build` to take the new ones (not done, not part of this task).

## Proof

| Check | Result |
|---|---|
| Whole PlayMode suite, run `20261106-025420-test-playmode-376315` | 373 of 373 |
| EditMode (after the test update) | 61 of 61 |
| `DemoTests.TheCoverMap_FindsCover_InTheRooms_WithNoMarks` | 512 spots in 48 ms: 322 low, 190 corners, 189 on a breakable. G2 76, G5 146, W1 4, W1B 7 |
| `DemoTests.TheProps_BreakByType_AnItemFalls_AndTheWayOpens` | pass |
| `DemoTests.ASoldier_TakesCover_HidesBetweenShots_StandsToFire_AndLeavesWhenSheGetsPast` | pass, in G2 behind a planter |
| `BreakableTests.Soldiers_TakeCover_BehindTheBarricades` (now also: he fires over it) | pass |
| Exits and floor rules | 0 of 78 exits out of view, 0 of 6168 floor points out of frame |
| Bots `demo` `20261106-032536-play-381127`, `cover` `20261106-032704-play-381510`, `ai` `20261106-032750-play-381822` | pass, triage clean |

Limits:
- Nobody played it by hand. The feel of the crouch and of the 1.5 to 3 s rest needs your check.
- A soldier with no spot still uses the old 6 m ring, so he can still close in. The team AI (§13.6) changes that.
- W1 and W1B have spots but no fight in W1. A soldier can choose a spot in the next room when the line through a doorway is clear.

Shots: `~/MEGA/glorified/renders/game/demo/cover_2026-11-06/`.

## The answer on targeting (no code)

A civilian: Tab until the marker sits on it. A physics object: key 3, then Tab. Both within 14 m and in her sight. The marker is the same red for all. Open for Bruno: a different marker colour for a civilian and a prop?
