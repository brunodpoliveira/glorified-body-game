# 2026-11-14: the booth nurse, the gurney, the surf

Bruno, after a play of the Hold:

```text
1 - i don't see a nurse behind the theather control room to teach the fire power
2 - the gurney is invisible; it should have something visible
3 - the player needs to ride the gurney themselves; i need to test if it's possible to manipulate objects to get her to surf on them
4 - for later: gap needs to be bigger
```

## The fact check

| # | Verdict | The evidence |
|---|---|---|
| 1 | CONFIRMED BUT DIFFERENT | The nurse spawns at (10.56, 0, 42.32) in the booth, and her renderer is on. The booth "glass" was an opaque unlit colour, so it was a grey wall. The cell camera of the floor in front of the booth (`Cam_H4_01`) looks south, away from the booth. The two cameras in the booth look east, and she stands at its west end. No camera showed her. |
| 2 | CONFIRMED | `RoomKit.Box` takes a local position. The gurney gave it world positions, so the deck drew at (-29.1, 0.7, 41.9), 36 m from the gurney. The four legs had one name, so only one leg existed. In the airlock only the collider stayed. |
| 3 | CONFIRMED | The ride was an Interact question that put her on the gurney. A probe in PHYSICS TEST put her on `Box_A` and held the defend key: the carry lifted the box and moved it 1.75 m in front of her, and she fell to the floor. Nothing let her surf. |
| 4 | FOR LATER | Not built. The gap of the tube in the Hold must be wider. |

## The fixes

1. The booth (`DemoHold.H4`):
   - The panes and the door are glass now (`DemoRoom.Glass`: the unlit colour with alpha 0.22, the door 0.35, no shadow).
   - A camera in front of the glass, `Cam_H4_booth`, looks from the south-east corner at the nurse. Its zone is the floor in front of the west pane and the door.
2. The gurney (`DemoHold.H5`): local places, and a deck, four legs on wheels, two side rails and a push bar.
3. THE SURF (`Interaction/Rideable.cs`, it replaces `GurneyRide`). The controls follow the PROPOSED text of `DEMO_PLAN.md` §2.1:
   - The Interact key climbs her onto the thing (no question, no cost) and down again.
   - On it, the defend key held with mk_orzabal lifts the thing 0.35 m with her on it (`PsychicPowers.TryRide`, before the carry and the shield).
   - The walk keys steer it: forward and back along her facing, and the turn keys turn it. Walls and bodies stop it.
   - The release sets it down, and she stays on it. Over a gap the release does not set it down ("Not over the gap."), and the ride goes on.
   - The cost (PROPOSED): the gurney 40 MK of the mk_orzabal bar, a box the cost of any lift (10 MK).
   - A message of the Interactor locks her feet while it shows, so the ride hint is in the hint line: "climb down (hold the
     defend key with mk_orzabal to ride it)".
   - The blocking box goes from 0.25 m over its bottom to 1.6 m over its top (her head, a little low, so the 2.9 m lintel of
     the tube passes).
   - Every box of `PropAssets.Box` has it: the PHYSICS TEST boxes, the tutorial box, and the two crates of G16.
4. `Interactor.ShowMessage` replaces a message on screen: a new message no longer waits for the old one.

A change for the writer: `DEMO_PLAN.md` §10.6 item 2 lists the prompts "as built and kept". The question "Ride the gurney with
mk_orzabal?" is gone. The empty bar text and "Not over the gap." stay. The new hint line is "climb down (hold the defend key
with mk_orzabal to ride it)".

## Proof

| Check | Result |
|---|---|
| `HoldTests.TheRide_SheClimbsOnTheGurney_...` | passes: 10.0 m in 4.5 s, 40 MK, the lowest foot 0.81 m, a release over the gap does not land it |
| `HoldTests.TheBooth_HerCameraInFrontOfTheGlass_ShowsTheNurseThroughIt` (new) | passes: `Cam_H4_booth`, the nurse at the frame centre (0.50, 0.54) |
| `PhysicsTests.TheSurf_SheClimbsOnABox_...` (new) | passes: the box moves 1.75 m in 1 s with her on it, sets down, she climbs down |
| `HoldTests`, `PhysicsTests`, `DemoTests` | 85 of 87 in `runs/20261114-230827-test-playmode-133878`: the two new ride tests failed, then passed after the fixes below (`runs/20261114-233635-test-playmode-139627`, 3 of 3) |
| `TutorialTests`, `PhysicsTests` | all pass in `runs/20261114-233315-test-playmode-138601` (18 of 20: the two ride tests) |
| The physics bot | `play PhysicsTest Main_Menu physics 300`: pass, 6 of 6 steps |

The runs found four faults of the new code, all fixed:
1. A box has an interpolated Rigidbody, and the interpolation wrote its old pose back over each move. The ride turns the
   interpolation off and puts it back at the set-down.
2. The ride test found a G16 crate first (the crates ride too). The test finds `Steel_Gurney` by name.
3. The blocking box reached 2.9 m with the lift, and the lintel of the tube stopped the gurney at x 5.15.
4. The ride hint was a message, and a message locks her feet for its time.

Shots: `~/MEGA/glorified/renders/game/probe_2026-11-14/` (`after_booth.png`, `after_gurney_rest.png`, `after_gurney_gap.png`).

FOR LATER (Bruno): the gap of the tube in the Hold must be bigger.
