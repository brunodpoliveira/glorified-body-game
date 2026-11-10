# 2026-11-10: the halon room

Bruno:

```text
Create tha halon room now
```

DEMO_PLAN §10.4 (CANON round 39: the fire, the gas, the pass-out; round 41: the reserve cage. The rest PROPOSED).

## 1. The rooms (the places PROPOSED: the plan places G17 "when it is built")

1. G17b, FACILITIES, off the kitchen G19 (its south wall). The desk with L15, a filing cabinet, the lockout cabinet by
   the door of the server room with its words (§7), and the key switch LOCKOUT. The words of the door: DANGER, and the
   line by hand under it.
2. G17, SERVER ROOM, south of G17b. Four racks, four red cylinders of halon, the panel inside by the door (ABORT, its
   display, its words, L17 beside it), and the strapped wall of archive boxes in front of the reserve cage.
3. The door between them is a steel fire door with no lever and no kick that breaks it (`Door.LeverRoom = "none"`):
   only the halon system shuts it and opens it.
4. L16 lies in the conference room G12, at the place marked COLTON.

## 2. How it runs (`Interaction/HalonSystem`)

1. The box wall is a heavy lock target: the auto-lock takes it, the lift refuses it.
2. A part circle of mk_erebus only flashes on it (the rule of the power). A full circle lights it.
3. LOCKOUT turned first: the boxes burn for 8 s with no horn, then they are gone, the ash stays, and the cage shows.
4. Else the horn sounds (the two-tone alarm of the dark room), the door seals, and the display counts down from 30.
   ABORT on the panel (`HalonAbort`) stops it: the door opens, and the boxes burn.
5. No ABORT: the halon comes. The fire goes out, and 3 s later she passes out: game over, killer `halon` (the
   writer's death screen row). The bottles are empty then: a later fire burns with no horn.
6. The save keeps the burned room (`demo_g17_burned`).

## 3. The cage

The cage holds one MK capsule for each of her three powers, one second wind pill, the empty tray with the violet
label, and L18 on its clip. Bruno, the same day:

```text
1:go with your picks. 2:one of each.
```

So the places, the 8 s burn and the 3 s pass-out stand. FOR THE WRITER: L18 still counts WARSPITE 2. EREBUS 2. ORZABAL
2. The count needs a 1 for each to match the cage.

## Proof

| Check | Result |
|---|---|
| New tests (`DemoTests`) | `TheHalonRoom_G17_...` (the rooms, the wall, the cage hidden, L15 to L18 in their rooms), `TheHalon_LockoutFirst_...` (a part circle flashes, a full one burns, no horn), `TheHalon_TheHornSealsTheDoor_AndAbortOpensIt`, `TheHalon_NoAbort_TheHalonComes_AndSheGoesUnder` (killer `halon`) |
| The world, camera, exit, cull, gap and fire door tests, `FloorplanV2Tests` (47 rooms) | pass: 14 of 14 with the new ones |
| The demo bot | passes |

Shots: `~/MEGA/glorified-body-renders/game/halon_room_2026-11-10/`. The red sphere over the boxes is the lock marker:
the auto-lock takes the wall. Numbers PROPOSED: the burn 8 s, the pass-out 3 s.

## The whole check after the cage change

The rebuild, then the whole suite: 481 of 483 (`TheFrame_Profile` is explicit, so the suite skips it). One failure:
`TheCapsules_LieOnTheWay_...` asks that every capsule lie within 1.2 m of the floor she walks at the start. The cage
capsules lie behind the box wall on purpose, as a reward, so the test skips the cage now. In exchange, the LOCKOUT test
checks the reach after the fire: a free place on the floor where the boxes were, and every cage item within its range
from there. The three tests pass (3 of 3). All 27 bot runs pass, the tutorial bot included.
