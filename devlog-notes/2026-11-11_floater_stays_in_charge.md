# 2026-11-11: the floater stays put during a charge

Bruno, in his manual test:

```text
orzabal has a strange behavior: when i aim my floater at someone else, the floater stays close to the target as i charge, so that means there's not a lot of feedback if decide to throw the enemy. the enemy floating should stay put until i let go or fuilly charge.
```

## The fact check: CONFIRMED

1. A charge turns her to the locked target (`LockOnController`, the face turn).
2. A floater turns with her yaw (`TelekinesisLift.HoldYawNow`, the rule of 2026-10-20: she aims the throw with the tank turn).
3. So a charge at a second enemy swung the floater around her, into the line of that enemy.
4. A new test showed it before the fix: the charge turned her 102 degrees, and the floater moved 5.59 m.

## The fix

1. While the charge key is held, the floater does not turn with her (`PsychicPowers.UpdateHeld` sets
   `StaysWhenSheTurns`). She still turns to the lock.
2. A release throws it from where it hangs. A full charge slams it where it hangs. A cancelled charge lets it turn
   with her again.
3. `StaysWhenSheTurns` now keeps the floater in place when it changes from false to true. Before, it jumped back to
   the side of the lift.

## Proof

| Check | Result |
|---|---|
| `Telekinesis_TheFloaterStaysPut_WhileSheChargesAtAnother` (new) | failed before the fix (5.59 m), passes after (0.03 m), and the release throws at Dummy_B |
| `Telekinesis_HeldEnemy_TurnsWithHer` (the tank turn with no charge) | passes |
| `PowersAndBarsTests`, `PhysicsTests`, `FloorShieldTests` | 70 of 70 |
