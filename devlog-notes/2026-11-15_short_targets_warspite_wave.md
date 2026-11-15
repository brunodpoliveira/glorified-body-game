# 2026-11-15: the short's targets, the warspite wave

Bruno:

```text
hmm, one thing: the lights and grebles: do we have enough of them to test destruction? short should aim and destroy those lights and grebles when no enemies present
and also warspite should move those psychic objects when they're in the way: the idea is to create more chaos and desturction, and to show that warspite is kinda like a pressure wave that ends when the enemy/object the player is aiming for is hit
```

```text
Breakable lamps are to be added when demo run is satisfactory; this will be the last step before the texture and actual level build out happens (when we start removing placeholders everywhere as we validate the level is fund and it works)
```

## The check: how many breakables to test with

| Room | Breakable lamps | Tables with things | Other |
|---|---|---|---|
| COVER TEST | 6 | 3 | 4 plaster patches, barricades |
| KIT TEST, before | 0 | 1 | 2 barricades |
| KIT TEST, now | 4 | 3 | 2 barricades, 2 crates |
| The demo | 0 | 0 | the 15 prop types break by name |

The demo gets its breakable lamps last, after a good demo run (Bruno's second message).

## The short with no one in reach (`ShortController.ShockAThing`)

With no live enemy in the shock radius, each shock goes to the nearest intact lamp (it breaks), else to the nearest thing
on a table (it jumps off, `Greeble.Disturb`, 4 m/s). Only in her room or in a line of sight. The arc, the zap and the
spark show it, as on a person. An enemy in reach still takes the shock first.

## mk_warspite as a pressure wave (`PsychicPowers.PressureWave`)

The wave runs from her chest to the point where it ends: the target, or the cover that takes it (the cover rule of
2026-10-24 stays). Every thing her powers can move within 0.6 m of the line flies, forward and away from the line: a
thing on a table, a crate or a box (it takes a body if it had none), a piece of debris. 5 m/s at a part charge, 10 m/s at
a full circle. People are not pushed (the blast is for the target), nor the target, nor a heavy thing, nor a thing she
rides. Numbers PROPOSED.

## Proof

NOT TESTED: Bruno, "skip the tests; i'll do the manual testing right away". The KIT TEST build compiles and is CLEAN
(`runs/20261115-154449-exec-22986`). No test covers the two new rules yet.

## For the manual test (KIT TEST)

1. Kill the three armed figures, or stand far from them. Start a short (F6). The shocks break the four ceiling lamps,
   then knock the things off the tables.
2. With an enemy within the shock radius, the shocks go to him, not to the lamps.
3. Put a crate between Lucy and a soldier, and fire mk_warspite at him. The crate flies forward and to the side, and the
   soldier takes the blast.
4. Fire mk_warspite across a table at a target behind it. The things on the table fly.

## Bruno's test, the same day

```text
kit test: 1: works
2: no blast on the physics objects, including the grebles on the table and the crate
```

CONFIRMED, two causes in the first wave:
1. It was a 0.6 m sphere along her chest line (1.2 m), so its bottom was at 0.6 m. A 0.5 m crate on the floor lay under it.
2. A thing on a table rests with its colliders off (`Greeble`: they come on only when something knocks it), so no cast
   found it.

The wave is a corridor now: 0.75 m to each side of her line seen from above, from the floor to 2.2 m. The table things
are found by where they are, the crates and the debris by a box along the corridor.

| Check | Result |
|---|---|
| `WarspiteWaveTests.TheWave_PushesACrateOnTheFloor_AndTheThingsOnATable` (new, KIT TEST) | pass: the crate flies 6.1 m, 4 of the 4 things on the table fly |
