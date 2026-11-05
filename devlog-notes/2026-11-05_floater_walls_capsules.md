# 2026-11-05: the floater stops at walls, the capsules of W1B

Bruno, with two frames of Neo as a pose reference:

```text
A bug, to be fixed now, and a refinement, to annotated and added later: when using telekinesis, the soldier that floats can
phase through walls
refinement for later: the animation for all powers should be her lifting one arm and palms extended, for all powers, and oin
telekinese arms should track where hte enemy is floating to

also, yes add the capsules
```

## The bug: CONFIRMED

`TelekinesisLift.FollowHolder` set the floater's position each frame with no wall check. Only the throw had one. Three tests
failed first:

| Test (`RagdollTests`) | Before the fix | After the fix |
|---|---|---|
| `TheFloater_StopsAtAWall_WhenSheWalksTowardIt` | 1.99 m past the middle of the wall | stops 0.43 m on her side |
| `TheFloater_StopsAtAWall_WhenSheTurns_AndComesBackWhenSheTurnsBack` | 11.23 m on the far side | 0.00 m (he slides along it) |
| `TheFloater_LiftedAgainstAWall_DoesNotGoThroughIt` | 2.58 m past | stops 0.42 m on her side, and the throw stops too |

## The fix

1. Each frame the body sweeps from where it hangs to the hang point (`TelekinesisLift.HangMove`). A wall stops it, and the rest
   of the move slides along that wall.
2. When the way is free, the body goes back to the hang point. It does not end the hold: only a throw, a slam or a hit does
   (his rule of 2026-10-22).
3. The sweep skips a collider it starts in, so a body lifted beside a wall went through it. `Obstacle` now also sweeps the middle
   line of the body. The throw uses `Obstacle` too, so the throw gets the same check.
4. A slam lands where the body hangs now, so it lands on her side of the wall.

A result for you to check: a body hangs at about 1.5 m, so it does not fit under a door head. If she walks through a doorway with
a floater, it stays at that wall and comes back when she comes back. It hangs there until she throws it or slams it.

## The capsules

Note 1 of cutscene 02 says that they roll off his tray and that she can take them after the fight. W1B had none. Now two MK
capsules (`Capsule_W1B_A`, `_B`, the number is PROPOSED) lie on the floor where he talks. A `FlagGate` shows them under the cut,
so nothing pops up in view, and a load keeps them.

`DemoTests.TheCapsules_FallInTheTalk_SheCanTakeThem_ARetryKeepsTheTakenOneGone`: hidden before the talk, shown when the cutscene
plays, on the floor of W1B, and a retry keeps the taken one gone and the other one there.

## For later: the arm of the powers

Written in `game/DEMO_PLAN.md` §13.5. Not built. One pose for every power: one arm up, the palm out. With mk_orzabal the arm
follows the floater.

## Proof

| Check | Result |
|---|---|
| `RagdollTests`, `PowersAndBarsTests`, `BreakableTests`, `DemoTests`, `ScriptTests`, `SaveTests`, `MapTests`, run `20261105-023057-test-playmode-158783` | 92 of 93. The failure was my capsule test: a retry destroys a taken item, and the test looked it up |
| The capsule test after that correction and the scientist test, run `20261105-024151-test-playmode-160590` | 2 of 2 |
| `FloorShieldTests` (its slab hangs with the same code) | 6 of 6 |
| Bot `play Demo Main_Menu demo 900`, run `20261105-024357-play-161408` | pass |

Limits: the whole PlayMode suite did not run. The tests use a test wall (a box on the wall layer), and nobody tried the fix by
hand in a game room.

Shot: `~/MEGA/glorified-body-renders/game/demo/opening_2026-11-05b/demo_meet_2_capsules.png`.
