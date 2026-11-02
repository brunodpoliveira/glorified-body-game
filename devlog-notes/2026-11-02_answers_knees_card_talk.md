# 2026-11-02: his answers. The CPR on both knees, the card by a word, the second pass

Bruno, on the open points of 2026-10-31:

```text
1: both knees
2: it should work on every floor
3: keep plain boxes for now
4: the player should be able to press a button to unpause; it sohuldn't autoplay
5: no need; the second way to grab cards from civvies is to simply interact/talk with them
also, to be noted for a second pass: the design needs backtracking and some other rooms and puzzles (more bathrooms,
storage rooms, a child's play room, a cofee break room, a conference room, an office with rows of computers, a cold
materials/freezer room)
```

| # | Point | Result |
|---|---|---|
| 1 | The CPR kneels on one knee | CHANGED: both knees (below) |
| 2 | The floor shield on any floor, or some floors | No change: it works on every floor (as built) |
| 3 | The map look | No change: plain boxes |
| 4 | A tape after a screen | No change: the screen pauses the tape, and only the tape key (T) plays it again. The test now also waits for the end of the screen and checks that the tape stays paused |
| 5 | Code signing for Windows | No |
| 5 | The second way past the W11 stair card | BUILT: she asks the ward sister for it (below) |

## 1. The CPR on both knees

Invector has no clip that kneels on both knees. The vendor `Pick_Down` kneels on one knee. Over it, `CorpseCare.CprLegs`
sets four leg muscles: the thighs straight down, the shins flat on the floor behind. I picked the numbers from two
PoseProbe sheets over `Pick_Down` at 0.43 s (the probe now logs the knee and foot heights):

| Shin value | Knees over the floor | Ankles |
|---|---|---|
| -1.0 | 0.07 to 0.09 m | 0.50 m (the heel at the seat) |
| -0.4 | 0.07 to 0.09 m | 0.18 m |
| -0.25 (the pick) | 0.08 to 0.10 m | about 0.09 m |
| 0 | 0.07 to 0.09 m | -0.08 m (under the floor) |

The care test measures the knees in the game: before the change L 0.46 m, R 0.23 m (the test failed), after it L 0.08 m,
R 0.10 m.

Shots: `~/MEGA/glorified-body-renders/game/animation/shots/cpr_both_knees_side.png`, `cpr_both_knees_front.png`. The probe
sheets: `cpr_knees_probe_sheet.png`, `cpr_knees_probe_sheet2.png` (same folder).

## 2. The card by a word (`Interaction/CarrierTalk.cs`)

- A living, unarmed civilian (`CivilianBrain`) who carries an item gets `CarrierTalk` from its spawn marker. The
  Interact key on it asks "Ask for the ward sister's card?". A yes puts the item in her inventory. The civilian says
  "Take it. Take it and go." (PLACEHOLDER for the writers), and the card leaves the belt.
- The item counts as taken, so the saves keep it. After a load the belt shows no card (`ItemCarrier` hides the copy
  when its item is gone).
- A death after the gift drops nothing.
- The armed lab workers fight, so they do not give an item on a word (PROPOSED).
- The kill stays the other way: the card drops beside the body.
- Fixed on the way: a name that starts with "THE" showed as "the the ward sister's card" in the question.
  `ItemPickup.Name` no longer adds a second "the".

## 3. For the second pass (not built)

His list is in `the-glorified-body/game/DEMO_PLAN.md` §12: backtracking, and new rooms and puzzles (more bathrooms,
storage rooms, a child's play room, a coffee break room, a conference room, an office with rows of computers, a cold
materials freezer room).

## Proof

| Check | Result |
|---|---|
| The care test before the change (the knee check) | FAILED: L 0.46 m, R 0.23 m |
| `EnemyMoraleTests` (both care tests), `ArmedCivilianTests`, `DemoTests` (new: `W11_TheWardSister_GivesTheCard_WhenSheAsks`), `ScreenTests`, `CivilianTests` | 43 of 43 |
| Bot `play AiTest Main_Menu cpr 300`, run `20261102-122949-play-224181` | pass |

The player builds in `builds/` do not have these changes yet. Build them again before an upload.
