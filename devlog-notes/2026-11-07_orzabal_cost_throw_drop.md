# 2026-11-07: mk_orzabal lifts at once, throws by the charge, drops a box. A dead floater falls

Bruno:

```text
mk orzabal: there's no way to drop something safely so attempting to stack boxes always launches them and breaks them; right clicking should cancel the lift and drop the object gently on the ground; left click still does the usual (the slam/launch)

the player might gently drop an enemy by accident on a misclick

1: lift should cost the same no matter what is being lifted
2: a throw should cost relative to the power the player put into it with the thrown object causing more damage to the thing/enemy being hit. iof the thrown object is an enemy it too gets more damage the more power on the throw, and it is launched at greater velocities; all powers should work the same (more power = more cost). the behavior changes on a full charge (i.e the fire power sets the person on fire, the blast knocks an enemy flat on his ass). edgar/mind control on non-full charge also stuns the enemy like the fire power. orzabal needs no charge to lift; the moment the player selects and triggers it it lifts the selected target; it is only when the object is in mid-air that the charge is meaningul
3: that seems correct; if the player wantsw to interact with the box it must lock on to it first
is middle-mouse scroll used for anything? it can be an alternative input to scroll through locked targets more quickly if it's free

ignite or blast with the guy float; mind control does nothing, but it can be used; the player will need to be smart with resource allocation and not squander their powers like this

also, another aspect of the test: once the enemy dies they should drop to the ground, not keep floating mid-air
```

## The fact-check of the first report

Verdict: confirmed, but different. The carry (the right button held) already dropped a box whole. The lift by the left button had no drop. A failing test showed it: `But was: <Box_A TelekinesisLift>` after a right press.

## What changed

| Piece | What it does now |
|---|---|
| Lift | A left press lifts the locked target at once. The cost is `PsychicPowers.LiftCost`, 10 MK, on an enemy or a prop, with either button. With nothing in the air the circle does not fill |
| Her turn | She turns to what she lifts in 0.35 s. It rises where it stood, and it does not swing with her in that time |
| Throw | A release on a held thing throws it. The cost, the damage and the speed go with the charge. Both bodies take the damage. A short press counts as the least charge, 20 % |
| Speed | 0.6 to 1.4 times 14 m/s. Measured: 11.2 m/s at a charge of 0.25, 16.8 m/s at 0.75. A throw at nobody flies 0.6 to 1.4 times 8 m |
| Slam | A full circle on a held thing, 30 MK, as before |
| Drop | A right press on a box or a crate in the air lets it down whole, for no MK. Not on an enemy or on the slab: on those it is still only the catch |
| Crates | A crate of the rooms gets its Rigidbody at any first lift. Without one it fell through what lay under it |
| mk_edgar | A part circle stuns, 2 s times the charge, and costs 75 MK times the charge. Under 20 %: a fizzle. A full circle takes the mind, as before |
| mk_edgar, a target she cannot take | The circle goes on with no kneel, and the HUD says why at the press. A part circle stuns. A full circle stays free and takes nothing |
| mk_edgar on a floater | It does nothing and it costs by the charge. No kneel, no stun, no mind, no HUD word |
| Wheel | It switches the lock, up and down. With mk_orzabal and a thing in the air it raises that thing |
| Charge with a thing in the air | She turns to a locked target, but not to the thing she holds: it turns with her, and she would spin after it |
| Her rule 3 | No change: with an enemy locked, the held right button makes the shield |

All numbers are PROPOSED.

## The fault his last check found

A floater killed in the air stayed in the air. The test measured the hips at 2.40 m, in the state `Corpse`. The log showed `ragdoll_dropped` at 256.70 and `ragdoll_corpse` at 256.71.

The cause is one line in `EnemyRagdoll`, from 2026-10-27. The limit on a fall was `6 + (throesUntil - downStart)`. A body with no throes has `throesUntil` at 0, so the limit was negative after about 6 s of play, and the body froze one frame after the drop.

The same line froze a body killed by a throw where the flight ended. I expect that it also froze a dead body at the end of a drag, and a body that a load puts back in its fall. I did not measure those two.

After the fix: hips at 0.18 m (blast), 0.17 m (fire), 0.14 m (a throw).

## What I chose for you (say no if wrong)

1. The lift costs 10 MK.
2. A short press on a held thing throws it at 20 %, for 6 MK and 8 damage. Without this, a tap would do nothing.
3. The stun of mk_edgar is 2 s times the charge. A circle of 50 % costs about 37 MK for 1 s. mk_erebus gives 0.5 s for 12.5 MK at 50 %.
4. A part circle stuns a guard at full HP and a boss too.
5. No drop for an enemy or the slab: the slab costs 30 MK, and your misclick point applies to it too.
6. With no enemy locked, the held right button still takes the prop in front of her. You kept this.

## Limits and risks

- **Puzzles now cost MK.** The carry was free, and now each lift costs 10. G16 has one door and its capsules are the reward, so a player with no MK loses only that room. A puzzle on the required path needs a capsule beside it.
- **No MK with an enemy in the air:** she cannot throw or slam it, and the right button does not drop it. It stays up until she kills it with another power or takes a capsule.
- **A fast mouse:** the hold point leads the box by up to 0.6 m (`TelekinesisLift.MaxLead`). The box goes on for up to 0.6 m after the mouse stops, in under 0.1 s. The bot measured it: -1.07, then -0.47 after 0.4 s. It may make stacking feel slippery. The margin stops the box from getting stuck behind a wall.
- **The triage line does not read a bot result.** It said CLEAN on two bots with a failed step, and I reported them as passed. Read `playthrough.json` for each bot.
- **While she charges a throw she stands still.** She aims with the tank turn before she holds the button.

## EDGAR TEST starts with its AI off

Bruno:

```text
holy shit there are so many fucking enemies in the edgar test room i get my shit rocked in before i can do anything. please disable the ai or something. i can't test the power as-is
```

Every figure of the room is now passive (`EnemySpawn.passiveUntilFlag`, `SoldierBrain.PassiveUntil`, `CivilianBrain.PassiveUntil`). A sight of her, a hit, a power or a scream does not wake it. mk_edgar works on it, and a taken mind acts. A yellow post beside her start wakes the rest (Interact, flag `edgar_test_awake`).

With the room asleep, a taken guard shoots at figures that do not answer. To see them fight back, use the switch.

He also kept the rule: with no enemy locked, the held right button takes the box in front of her.

Proof: `EdgarTests` 15 of 15 (run `20261107-022747-test-playmode-630386`), bot `edgar` 6 of 6 (run `20261107-023107-play-631762`). The test presses no real key on the switch: it reads the flag of the switch and raises that flag.

## mk_edgar: a limit, a snap, and a target she can switch to

Bruno:

```text
what do you suggest to make the edgar less op? the soldier keeps mind controlled until it is killed. in this scenario the soldier keeps following her; for guards and soldiers there needs to be a limit where they snap out of it after a while, but civvies are different: they can't fight, and i'm planning on doing puzzles where mind controlling a civvie is required

i couldn't switch targets to the mind controlled soldier in the test room
```

He agreed with my two proposals: an armed lab worker has the fighters' timer, and a soldier or the armour must be at 50 % HP or less.

| Piece | What it does now |
|---|---|
| The limit | `Dominated.LimitFor`: guard 30 s, soldier 20 s, armed lab worker 20 s, armour 12 s. An unarmed civilian and a puzzle figure have no limit |
| The warning | In the last 5 s the violet mark flickers and he shakes (`Tremble`) |
| The snap | He kneels for 2 s and says a line (`LetGoLines`), then he is an enemy who knows where she is |
| No second take | A figure that snapped wears `MindSnapped`. The HUD says IT RESISTS, as for a boss. A part circle still stuns him |
| A figure she sends | The timer stops while he walks to a switch, and it goes on after he uses it |
| The damage rule | A guard needs any damage. A soldier or the armour must be at 50 % HP or less (`Domination.TakeHpShare`) |
| Her target | The verdict on his report: confirmed, and it was deliberate. `Bind` made him unselectable. Now he is an ally (`LockOnTargetable.IsAlly`): the switch and the wheel reach him, and her powers hurt him. The auto-lock and the aim of a throw skip him |

A figure that she lets go by taking another mind can still be taken again. Only a snap closes him. All numbers are PROPOSED.

In the room with its AI off, a figure that snaps is hostile at once: `TakeMind` already took him out of the passive hold.

Proof: `EdgarTests` 21 of 21 (run `20261107-024221-test-playmode-634180`), bot `edgar` 7 of 7 with the snap (run `20261107-024617-play-635357`), bot `tutorial` 36 of 36 (run `20261107-024716-play-635774`). The whole suite did not run on this code. Nobody played it by hand.

## The order to a switch, and the mark

Bruno:

```text
the player can't control the mind-controlled person directly so it never goes to the switch. thoughts on how to fix this?
1+2; the guard is under my control atm when i took the screnncap
```

The verdict on the report: the order worked (the right button, with a test), but nothing on screen said so outside the tutorial.

| Piece | What it does now |
|---|---|
| The prompt | With mk_edgar selected, a mind taken and a switch in reach, the HUD shows "RMB: SEND TO THE SWITCH", with the bound key (`PlayerHud.OrderShown`). How to Play has a line |
| The lamp | That switch glows violet (`SentSwitch.Offer`, `PsychicPowers.OfferedSwitch`, a check each 0.25 s) |
| No route | `Dominated.SwitchInReach` asks the NavMesh for a whole path. Without one: no prompt, and the press says NOTHING TO SEND IT TO. Before this, he took the order, could never arrive, and his timer stood still for ever |
| The mark | It sat far over his head: the root carries the model scale, and the offset was local. Now it is 2.25 m over his feet (3 m for the armour) in metres, and 0.14 m wide |

Not built, his choice for now: orders by lock (Tab to a switch, a plate or a spot) and direct control.

Proof: `EdgarTests` 24 of 24 (run `20261107-033057-test-playmode-645145`), bot `edgar` 9 of 9, with the prompt and the gate (run `20261107-033344-play-646070`).

## Annotated only, not built

**Lucy speaks after she takes a mind** (for later, a task for the writer): "you will obey!", "suffer me now!", "my command is your wish!", and the writer adds more. It must fit P7 (Lucy speaks once in act 1) and the coda, where she does not want the power. The task is in `DEMO_PLAN.md`, before §15.

Men in black, an enemy type: "man in black types that can use one of her power sets; 4 types for each power lucy has; they'll not appear in act ." I read "act ." as act 1. The writer's files do not have this yet.

## Proof

| Check | Result |
|---|---|
| `PowersAndBarsTests`, `EdgarTests`, `RagdollTests`, `PhysicsTests`, run `20261107-015000-test-playmode-621718` | 89 of 91. The 2 failures were the dead floater, fixed after that run |
| `RagdollTests`, `GoreTests`, `HitReactionTests`, `SaveGoreTests`, `EnemyMoraleTests`, `CivilianTests`, `EnemyAiTests`, `SystemSeamTests`, `PhysicsTests`, run `20261107-020204-test-playmode-624406` | 106 of 106 |
| `FloorShieldTests`, run `20261107-021556-test-playmode-627586` | 6 of 6 |
| Bot `physics`, run `20261107-021454-play-627196` | pass, 6 of 6 |
| Bot `edgar`, run `20261107-015950-play-623576` | pass, 5 of 5 |
| Bot `cpr`, run `20261107-021345-play-626875` | pass, 1 of 1 |
| Bots `shield` and `tutorial`, runs `20261107-014310-play-620079` and `20261107-014357-play-620410` | pass, 4 of 4 and 36 of 36. These ran before the wheel fix, the rule for mk_edgar on a floater and the fall fix |

Nobody played this by hand.

Shots: `~/MEGA/glorified/renders/game/orzabal_2026-11-07/`.

## The whole run, overnight, on commit `10acf04`

| Check | Result |
|---|---|
| PlayMode, all classes, run `20261107-033505-test-playmode-648702` | 418 of 419. The one failure is a fault in the test (see below) |
| EditMode, run `20261107-041437-test-editmode-655536` | 61 of 61 |
| 23 bot runs, from `20261107-041500-play-655813` to `20261107-043518-play-662877` | All pass, with 0 failed steps. The triage of each run is clean, with 0 exceptions. The `tutorial` bot passes 36 of 36 and the `demo` bot passes 172 of 172 |
| `InteractionTests` after the test fix, run `20261107-043851-test-playmode-663947` | 11 of 11 |

`TheFireteam_Bounds` passed in this run, so it now has 1 failure in 10 runs. Its cause is still not proved.

### The one failure

`TheInventory_StopsTheRoom_AndListsTheItems` failed with "a drug used from the inventory, expected 1, was 2". Verdict: a fault in the test, not in the game.

- The tutorial empties the SHORT bar in each frame of play, except in the short step (`TutorialDirector.Update`, since 2026-11-06).
- The test filled the bar one frame before the inventory stopped the room.
- `TutorialDirector` and `InventoryUI` have no fixed order. When the director ran first, the bar went to 0 and the capsule refused ("short_empty").
- Proof: one added frame forced that order and gave the same message (run `20261107-043801-test-playmode-663497`).
- Fix: the test now fills the bar after the room stops. Only the test file changed, so I did not run the whole suite again.

### The shots

I read contact sheets of 13 bots, plus a sheet of 5 smaller bots and 3 of the 6 `demo` sheets. I found no hanging body and no broken room.

One flaw in the bots, not in the game: `CloseShot` puts its camera at a fixed offset, and a wall can hide the subject. In this run `team_02`, `team_03`, `team_04`, `ai_22` and `ai_23` show almost nothing. Earlier runs have the same flaw, in some shots and not others (`ai_23` on 2026-11-06, `team_04` on 2026-11-06). The `cpr` bot shows the drag and the CPR correctly. Five files hold a copy of `CloseShot`. I did not change them.
