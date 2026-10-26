# 2026-10-26, night: travel between rooms and the world state

The design sweep's rank 1 (`2026-10-26_sweeps/design.md`): no exit loaded another room, Lucy was an object of each
scene (a new scene reset her), and a save held only the room of the moment. Bruno: ranks 1 to 5 first, each system
"tested and fine-tuned to a reasonable degree".

## What the game does now

| Part | Behaviour |
|---|---|
| An exit | `RoomExit`: a trigger box past a doorway names the next room and the entry there. She walks into it, the screen fades to black (0.25 s), the next room loads, and the screen comes back (0.3 s). An exit can wait for a story flag (`requiredFlag`), for the sealed gate of code silver later. |
| An entry | `RoomEntry`: the place she comes in, inside the doorway, facing into the room. Its id is the object's name. |
| Her state | It crosses with her: HP and maximum HP, MK, SHORT, kills, exp and level, outfit, key items, objects, pills. |
| The room she left | It stays as she left it: the dead lie where they fell (bodies, stumps, blood, pools), the items she took stay gone, the open doors stay open. A death there does not count again: no second kill, no second exp. Broken cover keeps its own ledger, as before. |
| A save | It holds every room she left, and the room of the moment. A load gives them back. An older save file loads with no other rooms. |
| A new game | The main menu clears the rooms. |

Code: `Saving/RoomExit.cs`, `Saving/RoomEntry.cs`, `Saving/WorldState.cs`, `UI/ScreenFade.cs`. `SaveSystem` splits
its capture and its apply into the player part and the room part (`CapturePlayer`, `CaptureRoom`, `ApplyPlayer`,
`ApplyRoom`), so a save and a crossing use the same code. `SaveData.rooms` is new.

## The test rooms

`TravelTest_A` and `TravelTest_B` (`TravelTestSetup.BuildTravelTest`). They hold no vendor data, so both are in git.

- Room A: 12 x 10 m. Lucy, a guard dummy whose body stays at death, a NALCON pill, a door east to B.
- Room B: 10 x 8 m. A door west to A, a save machine.

A builder fault found and fixed: the character catalog loaded before a new scene opened, and the new scene unloaded
the prefab links. The rooms got no models. The builder loads the catalog after each scene opens now.

## Proof

`TravelTests` (4):

- An exit takes her to the next room with her HP, MK, exp, level, key and pills, at the entry of the door she came
  through. The screen comes back.
- The room she left stays as she left it: the dummy's body lies there, dead. The pill she took stays taken. No second
  kill and no second exp.
- She walks through the door with the forward key and arrives in the next room.
- A save keeps every room. The main menu clears them. A load gives them back, and room A has its dead.

The save, gore, exp, seam and death screen suites still pass (17/17). EditMode 49/49.

## Not in this step

- A short in progress does not cross with her (the bar crosses, the 16 s do not). An exit during a short could wait
  for its end. To decide later.
- A hurt enemy that is still alive comes back at full HP when she returns. The gaps sweep asked Bruno about this
  (`2026-10-26_sweeps/gaps.md`).
