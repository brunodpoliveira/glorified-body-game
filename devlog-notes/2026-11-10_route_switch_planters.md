# 2026-11-10: the spelling, the route switch and the planters

Bruno, before an event:

```text
go
```

The plan of the day: five steps that need nothing from him. This note covers steps 1 and 2.

## 1. The spelling

The surname is de Homem-Cristo (round 45). Four files still wrote HOMEN: the head card of G12 (`DemoV2`), its test
(`FloorplanV2Tests`), the whiteboard L4 (`DemoContent`) and the log asset. The builder writes the scene and the asset
again.

## 2. The route switch (DEMO_PLAN §10.5, row 3, CANON round 43)

```text
The W5 to W9 fire door opens from the W9 side: the ward becomes a ring
```

1. The doorway between W5 and W9 was a plain leaf that code silver showed for good. It is a fire door now.
2. Code silver shuts and jams it (`FireDoor.jamFlag`). A body in the doorway delays the jam until the way is clear.
3. It has one lever, on the W9 side (`Door.LeverRoom`, `BreakableAssets.Door(leverSide)`). After the gym and W8 she
   comes into W9, and her lever opens the way back to W5 and the save in W6.
4. A load sets every flag at once. The door listens for its own flag by name, so a load does not shut it again.

Test: `DemoTests.TheRouteSwitch_CodeSilverShutsTheW5ToW9Door_ItsLeverInW9OpensIt_ALoadKeepsItOpen`. The fire door test
now lists five doors, and this one has one lever.

## 3. The planters (§10.5, row 11, CANON round 43)

```text
Telekinesis moves the planters: a lane, or a wall against the pinch. A planter against a door holds it against guards,
not soldiers. The planters stay where she left them
```

1. The six planters of G2 are props for mk_orzabal now (`DemoRoom.Planter`): the mouse carry lifts one, the NavMesh
   goes around it where it rests.
2. A planter in a doorway holds it (`Interaction/DoorStop`). An enemy after her on the other side stops at it, as at a
   jammed fire door. A guard or a scientist bangs on it. A soldier or the power armour kicks it: after two kicks (the
   armour: one) it slides 1.2 m through the doorway and 1 m to one side. A wall stops the slide.
3. The save keeps the place of each planter, and of the G16 crates (`MovableProp`, `RoomState.props`). A planter that
   is not in a save broke, and a load removes it. An older save leaves them where the room put them.
4. A change from my own design of 2026-11-06: a planter was breakable cover. A planter that she moves does not break.
   It is still low cover. Two tests changed with it.

Tests (DOOR TEST, two planters in the hall): `APlanter_IsAPropSheCanCarry_AndTheSaveKeepsItsPlace`,
`APlanterInADoorway_StopsAGuardAndAScientist_TheyOnlyBang`, `APlanterInADoorway_ASoldierKicksItAway_AndComesThrough`.
The first soldier test failed: a push by physics moved the planter 0.32 m. The kick is a fixed slide now.

Numbers PROPOSED: two kicks, 1.2 m and 1 m.

## A bug fixed on the way

None this step. The first build failed on a missing `using` in the new builder code.

## Proof

| Check | Result |
|---|---|
| `DemoTests`, `FloorplanV2Tests`, `DoorTests` | 66 of 69, then the three fixed: 16 of 16 in the rerun |
| The doors bot | passes |
| The demo bot | passes |

Shots: `~/MEGA/glorified/renders/game/route_switch_planters_2026-11-10/` (G2 with the planters, W5, W9 with the
one lever on the W5 wall).

OPEN: a soldier's cover spots come from a scan at the first fight. A planter that she moves later leaves those spots
as they were.
