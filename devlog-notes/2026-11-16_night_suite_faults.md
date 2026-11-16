# 2026-11-16, night: the full suite and the bots, two faults

Bruno, at his sleep signal:

```text
I'm going to go to sleep, so feel free to close any unity instances still open and run the full tests; once all tested, commit
```

The full run: EditMode CLEAN. PlayMode 528 of 536 pass, 4 skipped (the old ride tests on `Ignore`), 4 fail. 24 of 25
bots pass. The edgar bot fails. Neither fault comes from the 180 work.

## Fault 1: a dominated man never fired at his colleagues

`EdgarTests.TheDominated_Follows_...` and the edgar bot: the guard that she takes with mk_edgar fired 0 shots. His gun
held its fire 22 times "for" Soldier_A, the man he aimed at. The human shield rule (2026-11-15, `HumanShield.Covering`)
counts every live person near the line, and the target stands at the end of his own line. Her own body is never in
that list, so the rule worked for every fight against her.

The fix: `HumanShield.Covering` takes the target and skips it. `DummyShooter` and `SoldierBrain.PersonOnLine` give it.

## Fault 2: three tests of the lock on a held thing

The lock is the aim of a throw since earlier tonight (`2026-11-16_surf_plain_physics.md`, Bruno: "we need to be as
consistent as possible"). The held thing is never a target. Three tests still expected the old lock:

1. `PowersAndBarsTests.Orzabal_SheTurnsToWhatSheLifts_...`: the charge turns her to the lock now (the other dummy). The
   test checks that turn.
2. `PowersAndBarsTests.TheWheel_SwitchesTheLock_...`: the lock leaves the lifted dummy. The wheel keeps the lock where it
   is and raises the thing.
3. `EdgarTests.OnAFloater_...`: mk_edgar can no longer aim at a floater, because the lock leaves it. The test is now
   `OnAFloater_TheLockLeavesIt_AndNoMindIsTaken`.

## Runs after the fix

1. `EdgarTests`, `PowersAndBarsTests`, `HumanShieldTests`, `CivilianEscortTests`: 87 of 87 pass.
2. The edgar bot: 9 of 9 steps pass (the soldier at 65 of 80 HP after the fight).

## For Bruno

1. mk_edgar on a floater (2026-11-07: "mind control does nothing, but it can be used") cannot happen now: the lock
   leaves the held thing. With no other target, the circle ends with "NO MIND TO TAKE" at no MK cost. Say if the full
   circle must cost MK here too.
