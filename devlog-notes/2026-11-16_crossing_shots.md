# 2026-11-16: the crossing of the gap in three shots

Bruno sent three frames of a film as the reference: a look down on feet at a drop notice, a side shot of a man on a
floating disc, and a shot from the far end.

```text
and after it being unlocked there's an extreme closeup of the "careful with drop" notice. this will be both dramatic and help the player align the gurney with her feet before she steps on it; then a side angle and front-facing towards the destination. side angle needed to avoid violating 180 rule
```

The words and the look are our own: the notice stays "CAUTION: DROP. WAIT FOR THE TUBE." (PLACEHOLDER).

| Shot | Camera | Zone | Looks |
|---|---|---|---|
| 1. The close-up (`Cam_H5_drop`) | over her shoulder, 2.6 m up, 1.1 m behind the notice on the floor | the floor of the airlock in front of the glass door (x 4 to 5.4), only once the card opened the door | west and down: the notice, the edge, the dark, her feet and the gurney as it comes down |
| 2. The side shot (`Cam_HT_side`) | in the dark of the hall, 4.3 m south of the tube, at her height | the near half of the tube (x -2.5 to 4) | north |
| 3. The front shot (`Cam_H6_front`) | on the landing, 2 m back from its doorway | the far half of the tube (x -8 to -2.5) | east, at her as she comes |

The close-up looks west, and the front shot looks east. The side shot comes between them, so no cut flips her
direction. I used the same check as for the dining hall, for each pair.

## What the shots needed

1. `LockedDoor.openedFlag`: the glass door raises `hold_airlock_open` when the card opens it. The zone of the close-up
   waits for that flag (`DemoRoom.ExtraCamera(..., zoneFlag)`, a FlagGate).
2. The exit rule skips the three doorways of the tube (`Door.ExitShot`, `Room.Exit.noShot`). The close-up and the front
   shot cannot show the doorway they stand at. The tube itself shows the way.
3. The invisible rails of the tube are on the Ignore Raycast layer, so the side shot sees through them. The check of
   the ride (`Rideable`) still meets them.
4. `RoomSight`: a camera outside the plan of its room (the side shot) skips the gap check. It still lists the rooms it
   sees.
5. The old gap camera of the airlock (`Cam_H5_gap`) is gone: the close-up and the side shot replace it.
6. I checked the frames offline with the coverage rule: 0 points out of frame in each zone. For the front shot, every
   line passes through the doorway of the landing.

NOT COMPILED, NOT RUN (no free RAM for Unity). Run `Demo > Build the World`, then `DemoTests` (the coverage, the exits,
the gap check).
