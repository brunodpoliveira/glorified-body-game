# 2026-11-16: the scan C11 is on the card carrier

Bruno, after the card beat as built. In round 118, he denies the card until she scans the card reader on the landing (C11).

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

## Later: the card door on her side, two scans

Bruno:

```text
there's also a scannable card reader near the drop, that can be scanned and will also point to the card the scientist holds; we'll add a glass lock on the player's end to avoid the player floating and smacking onto the lock. they'll see the locked glass door right away and knows there needs to be unlocked

that way the player has two chances to use the scan hability
```

1. The card door moved from the landing H6 to the airlock H5: a locked glass door in the doorway to the tube, on her side
   of the gap. She sees it before the ride. The sign "AIRLOCK CARD REQUIRED." and a card reader stand beside it.
2. The scan of that reader (C11) and the scan of the scientist both raise `hold_c11`. After either one, he gives the card.
3. The way from the landing to the gallery H7 is open.
4. The glass is on the Ignore Raycast layer, as the booth's panes. The gap camera, the room cull and her powers see
   through it. Her body and the gurney do not pass until the card opens it.
5. The card's text: "It opens the glass door of the airlock, to the tube."
6. `HoldTests`: the card test opens the airlock door, and the ride test opens it before the crossing.

## For the writer

`DEMO_PLAN.md` puts the scan on the reader in four places: the C11 row, the H2 row, the H6 row and beat 5b. The scan is
on the scientist and on a reader in the airlock now, and the card door is in the airlock. His line "It opens the landing"
does not fit any more: the card opens the airlock.

NOT COMPILED, NOT RUN (no free RAM for Unity).
