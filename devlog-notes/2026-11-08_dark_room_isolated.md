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
