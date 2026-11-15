# 2026-11-15: the guards of the Hold carry batons, not shields

Bruno:

```text
the guards in the hold all have shields, which is too quick of an escalation so soon, especially in a tight space like the hold; check if all guards have shields; some are supposed to have it, the others have baton + pistol (at the start) and then just the pistol as the act goes on
```

## The check: CONFIRMED

| Where | Guards | Before | Now |
|---|---|---|---|
| H3, the guard post | Guard_H3_A, Guard_H3_B | shield + pistol | baton + pistol |
| H7, the gallery (the arena) | Guard_H7 | shield + pistol | shield + pistol (the "some") |
| W5, the ward | Guard_A, Guard_B | pistol (a baton only if overconfident) | baton + pistol |
| R1, the coda booth | Guard_Booth, its spare | pistol | pistol (late in the act) |

Before, a guard took the baton only with the "overconfident" personality (round 134), about 3 guards in 10.

## The change

`EnemySpawn.baton` sets `SoldierBrain.AlwaysBaton`: the guard takes the baton at his alert whatever his personality, and
draws the gun when she attacks (as in round 134). The H3 guards are `guard_pistol` now. The baton lines of `barks.csv`
belong to the overconfident personality, so a baton guard of another personality strikes with no baton line. In batch
runs the baton stays off (`SoldierBrain.BatonEnabled`), so the tests see the old fight. PROPOSED: which guards keep a
shield, and where the act turns to the pistol only.

## Proof

NOT TESTED (Bruno tests by hand). The world build is clean, and the scene holds 4 baton guards, 1 shield guard and 6
pistol guards.
