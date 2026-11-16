# 2026-11-16: the scan C11 is on the card carrier

Bruno, after the card beat as built (round 118: he denies the card until the scan C11 of the card reader on the landing):

```text
so the player has to go with the gurney, smack on the lock, then float back again to ask for the card? too obtuse. most won't even think to ask him in the first place
```

His pick of three: "Scan him".

## The beat now

1. She asks him for the card (the Interact key, the prompt "ask for the airlock card"). He lies: "I don't have anything. I
   swear. I don't have anything." The card hangs on his belt.
2. She scans him (the scan key, at his chest). The picture of C11: a gloved hand clips a white card to a lanyard.
3. She asks again. He gives it: "Here. Take it. It opens the landing. Take it."

All of it in the Hold, before the gap. To kill him still drops the card.

## The code

1. `EnemySpawn.scanFlag`: the flag a scan of the person raises (before: only `<fate>_scanned`).
2. `DemoHold.H2`: the card carrier gets the scan (the placeholder picture, the plan's C11 text) and the flag `hold_c11`.
3. `DemoHold.H6`: the scan of the card reader is gone. The card door and its sign "AIRLOCK CARD REQUIRED." stay.
4. `HoldTests.TheCard_DeniedBeforeTheScanOfHim_...`: the scan on him in H2.

## For the writer

`DEMO_PLAN.md`: the C11 row (the card reader, H6), the H2 and H6 rows and beat 5b put the scan on the reader. The scan is
on the scientist now. The line "It opens the landing" still fits.

NOT COMPILED, NOT RUN (no free RAM for Unity).
