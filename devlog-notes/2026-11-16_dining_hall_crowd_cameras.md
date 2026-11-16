# 2026-11-16: the dining hall, the crowd and the cameras

Bruno, after a short test with DEMO: DINING HALL:

```text
I'm counting at least 12 enemies crowding up on the dining hall, which makes tuning the fight impossible. i think that the soldiers and scientists from other rooms are coming in as well; there also too many tight angles from up looking down. we need angles that capture wide shots of the room, we need angles that capture it sideways, etc. and also we need to verify if the change from one camera to the next does not violate the 180 rule, which i think from my short testing, some camera angles on the dining hall do
```

## 1. The crowd

The fact-check: CONFIRMED, from the code. The dev start put her in the hall with every room before it still full.

1. A shot or a hit calls every enemy who hears it: her room and the rooms next door (`EnemyAlert`, `Room.Hear`).
2. The gunfire in the hall called the four soldiers of the concourse G2, next door.
3. Their gunfire called the four figures of the foyer G5, next door to G2: two soldiers and two scientists with pistols.
4. So 6 in the hall, 4 from G2, 4 from G5, and the power armour: 15.

In the order of the demo, she fights G2 before the hall. So this is mostly a fault of the dev start. The fix
(`DevRoomStart`): every figure outside the target room is parked (inactive). The hall fight is the six and the armour.

## 2. The cameras

The fact-check: CONFIRMED. I read the cameras of the hall from the scene of the last build: 21 cameras. The builder
splits a cell each time a column hides a point, so some zones were 2 x 3 m. Some cameras looked down at 70 degrees.

The 180 rule, as the check reads it:

1. At each edge between two zones, she walks across the edge, and she walks along it.
2. In both cameras, each walk goes the same way on the screen, left or right.
3. A walk straight into the screen in one camera passes.
4. A camera that turns to her counts at its turned direction.

The check found 22 pairs of neighbours that flip her direction. Two pairs looked at each other (yaw 90 and 270).

The new hall has five cameras of its own (`DemoDining.Cameras`, `RoomSpec.AuthoredCameras`: no cells in this room).

| Zone | Camera | Shot |
|---|---|---|
| The north aisle (z 9.2 to 13, the whole length) | the north-east corner, 3 m high, yaw 240, turns 25 degrees | the wide shot down the hall |
| The entry bay (x 35 to 41) | from the north, 5 m high, yaw 200, turns 25 degrees | the glass doors and the kitchen door in frame |
| The west bay (x 41 to 47) | from the north, 5 m high, yaw 190, turns 25 degrees | the side shot across the hall |
| The east bay (x 47 to 53) | from the north, 4 m high, yaw 170 | the side shot across the hall |
| The bar end (x 53 to 59) | the north-east corner, 3 m high, yaw 200 | along the bar |

The two exits are in the west wall and the south wall. So every camera looks south or west, and no cut between two
zones of the hall flips her direction. Four cameras look down at 15 degrees, the entry camera at 38. The camera of the
candelabrum is gone: it looked east, against the other five. The candelabrum stays.

How I chose them: a search offline in Python, on the colliders of the hall from the saved scene. It copies the C# rules
(`CameraCoverage.Sees`, `SeesExit`, the turn of a following camera). On the old 21 cameras it gives the result of the
build: 0 points out of frame, both exits in view. So it reads the room the same way. For the five: 0 of 1006 floor
points out of frame, both exits in view, 0 flips.

## Open: the doorway cuts

The cut at the glass doors from G2 into the hall still flips her direction. The exit rule makes the camera on each side
of a doorway face it. So in a doorway she walks into the screen in one shot and out of it in the next. A doorway needs
its own form of the rule. That is part of the check of the whole demo, on the signal of Bruno (memory
`gb-camera-180-rule`).

## Proof

NOT COMPILED, NOT RUN (no free RAM for Unity). Run `Demo > Build the World`, then `DemoTests` (the coverage and the exit rule check
the five cameras). By hand: DEMO: DINING HALL, walk the hall, and watch the five cuts.
