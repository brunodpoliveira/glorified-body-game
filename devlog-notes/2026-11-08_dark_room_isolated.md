# The dark room, in isolation (2026-11-08)

This is Bruno's puzzle of `DEMO_PLAN.md` §10.1 (CANON, round 52: "the details stand as written"). I built it in a test room first, after his words of the night:

```text
add what can be tested in isolation after this round of testing is done
```

## The light method

The demo has one sun and a flat ambient light, and its greybox is unlit. So a dark room needs two ways:

1. **Lit materials** (the figures now, the real art later): a dark renderer leaves the sun's rendering layer for a dark layer, and it gets a near-black ambient probe of its own. Only lights on the dark layer reach it: the monitors. The URP assets already had rendering layers on.
2. **Unlit materials** (the greybox): a dark renderer shows 20 % of its colour. Each monitor light near it adds a glow, up to 50 %. This is a fake of the monitors' light for the greybox. A renderer with `Emissive` (a monitor, an LED) keeps its own colour.

`Rendering/Darkness.cs` holds both. A room that has a dark room must give its sun the lit layer only.

## What works

1. **`DarkRoom`** on a room root. On her first entry the room goes dark for good, with the rooms in `alsoDark`. Its floor, walls, props and the wall pieces on its lines go dark. Lucy and every figure inside are dark while they stay in it. Its flag is up for the saves, and a room whose flag is up starts dark.
2. **`UpsBank` and `UpsUnit`.** When the room goes dark, the monitors go black and the three units beep, each at its own pitch. The order is NET, REC, ACCESS. A unit out of order trips the alarm: the PCs drop, a continuous tone sounds, and the beeps come back after 20 s. No enemy comes (CANON round 40). REC plays the recorder line. The last unit brings the monitors back one by one, and the room lights stay out.
3. **The release.** `FlagSwitch.requiredFlag` and `waitingText`: the release says "WAITING FOR NET." until the units run.
4. **DARK TEST room** (`Editor/Demo/DarkTestSetup.cs`, the TEST ROOMS column). The hall has the IT note. The office has six monitors with their lights and the release. The closet has the three units in the shelf order REC, ACCESS, NET.

The sounds are placeholders made in code. The recorder line and the console line are placeholders for the writer's words. The 20 s and the glow numbers are PROPOSED.

## Proof

| Check | Result |
|---|---|
| `DarkRoomTests` (4), run after the glow change | 4 of 4 |
| The office camera, the mean brightness of the frame | lit 0.289, dark 0.052, with the monitors 0.127 |
| Bot `dark`, run `20261108-023846-play-99528` | pass, 6 of 6 |

The first run failed twice, and both faults were real. The closet rack stood over the NET unit, so Lucy could not reach it. The light layers alone changed the frame by 17 %, because the greybox is unlit. That fault led to the unlit tint. The first glow made the dark room almost as bright as the lit one, so I cut it to a third.

Shots: `~/MEGA/glorified-body-renders/game/dark_room_2026-11-08/`.

## Not yet in the demo

G9 and G9b exist with their props. The demo needs four things, about 0.5 day:

1. `DarkRoom` on G9 with G9b, and the demo sun on the lit layer.
2. The three units and the bank in G9b, and the monitor wall of G9 as the bank's monitors.
3. The release in G9 waits for the bank.
4. The hub units of W9 with their uptimes, and the IT note in G8 (the two sources of the order).

## Later that day: the alarm time

```text
dark test: when i press the wrong one the thing keeps on alarm and never lets me try it again, softlocking me
20 seconds is too long
```

The alarm was not a soft lock. With the room's own time it ended after 20.0 s, and then the right order worked. The
tests and the bot shortened the time to 1.5 s, so no test used the real 20 s. Now the alarm lasts 5 s (PROPOSED).
`DarkRoomTests.TheAlarm_WithTheRoomsOwnTime_Ends_AndTheUnitsTakeTheKeyAgain` uses the room's own time and fails above
5 s. `DarkRoomTests` 5 of 5, the dark bot passes. The rebuild also gives the props of DARK TEST their comments.

## Later that day: the restart countdown

```text
needs to have something that will let the player know that they can try again in 5s, something that also makes sense
in-universe
```

A UPS unit after a fault restarts by itself, and its front display says so. PROPOSED:

1. During the alarm, each unit's display counts down: "FAULT 5" to "FAULT 1". Its LED blinks red.
2. The Interact key on a unit during the alarm reads the display out: "NET: FAULT. AUTOMATIC RESTART IN 3 S." The hint
   says "read the NET unit". A press during the fault is no new mistake.
3. At the end, two short high beeps play. The displays show the labels again, and the units beep as before.

`DarkRoomTests` 5 of 5 (the countdown, the read-out, the ready beeps). The dark bot passes. Shot:
`dark_room_2026-11-08/dark_04_alarm_countdown.png`. The black shapes over the HUD in that shot come from the bot's
shot camera, not from the game.

## Later that day: the lights come back

```text
on the actual dark room, the lights will need to turn on again after the puzzle is complete; as it is rn it stays dark
```

The room stayed dark by my design ("the room lights never come back", PROPOSED). Now the monitors come back one by one,
and 1.5 s after the last one the room lights come back for good (`DarkRoom.SetLit`, `UpsBank.lightsBackSeconds`). The
room never goes dark again (`DarkRoom.litFlag`, the puzzle's done flag), and a load after the puzzle starts lit.
`DarkRoomTests` measure the office: lit 0.289, dark 0.052, the monitors 0.127, the lights back 0.285. A walk out and
back in leaves it lit. The dark bot has the step `the_lights_come_back` (shot `dark_08_the_lights_come_back.png`).
Lucy looks green beside the monitor wall: the monitors' point lights reach her model, as they do before the dark.
