# 2026-11-15: the theater rework and the 12 m gap

Bruno, with a still of a figure on a narrow ledge over a deep shaft, a drop warning on the floor:

```text
ok, it works. now, need to rework the initial room so that every power has its chance to shine:
1 - the gurney needs to be in the theater room
2 - the door locks behind it (an invincible one, shows that there are those), and the nurse behind the glass panel has the key
3 - she can be set on fire to unlock the door
4 - or the gurney can be used to smash the door, or warspite can be used to do so
5 - if the player did not set her on fire, then a button inside the room unlocks it
the gurney then needs to be used to traverse the gap
to signal really clearly this, let's add this camera angle close to the gap and extend the gap between the hold and the other end in the map to be clear you can't jump over it
```

## The theater (H4)

1. The steel gurney stands in the theater, in front of the door, its foot to the door (`DemoHold.Gurney`). The
   surgery table, the drapes and the arms of the Font moved 0.8 m west: with the table in the middle, a 2 m gurney could
   not turn to the door in a 6 x 3.2 m room.
2. The door from the guard post shuts and jams behind her, 1 m into the room (`H4_Lock`, flag `hold_h4_locked`). It has
   no lever, and nothing breaks it: no kick and no power (`FireDoor.canBreak` false now also stops her powers).
3. Three ways out, one for each power:
   - mk_erebus: fire on the nurse through the glass, or on the drapes. The fire alarm rings, and both the booth's door and
     the theater's door release (`H4_Alarm_Release`, flag `hold_h4_release`).
   - mk_warspite: two blasts break the booth's glass door.
   - mk_orzabal: she lifts the gurney and throws it at the glass door, and the glass breaks (`GlassDoor`: any thing she
     throws that flies past the leaf). The gurney never breaks (`LockOnTargetable.unbreakable`).
   After a broken glass door, the button in the booth (`Theater_Release`) opens the theater's door. The nurse "has the
   key": she stands by that button.
4. Then she rides the gurney out (`Rideable`): south through the door, west along the guard post, through the airlock to
   the tube. The doorways on this way are 2.9 m high, so she rides standing.

## The gap (HT)

1. 12 m from the airlock to the landing (6 m before). The landing H6 stands 6 m further west, and the gallery H7 grew 6 m
   west to keep its door to H6. Nothing else of the ward stands there.
2. A warning band and a sign on the floor at the airlock's edge: "CAUTION: DROP. WAIT FOR THE TUBE." (PLACEHOLDER words,
   for the writer).
3. A camera high over the edge, at the airlock's west end, looks down into the dark along the tube (`Cam_HT_gap`): it
   takes her at the edge and on the first part of the ride.
4. The ride across: 16 m in 7.3 s for 40 MK.

## For the writer

- `DEMO_PLAN.md` §5.0, §6 beat 4 and 5, and §10.6 place the gurney in the airlock (CANON, round 118). Bruno moved it to
  the theater and added the lock and the button. The words of the floor sign and the button's prompt ("Release the
  theater door?") are PLACEHOLDER.

## Proof

| Check | Result |
|---|---|
| `HoldTests.TheTheater_LocksBehindHer_NothingBreaksTheDoor_TheBoothButtonOpensIt` (new) | pass: it jams, 1000 points of blast on its leaf leave it jammed, two blasts break the glass, the button opens it |
| `HoldTests.TheTheater_FireOnTheNurse_RingsTheAlarm_AndOpensBothDoors` (new) | pass: the alarm, the booth open, the theater's door open |
| `HoldTests.TheGurney_ThrownAtTheGlass_BreaksIt_AndStaysWhole` (new) | pass: the glass breaks, the gurney lies whole in the booth and still rides |
| `HoldTests.TheGurney_RidesOutOfTheTheater_ThroughTheGuardPost_IntoTheAirlock` (new) | pass: 10.7 m, from the theater to the airlock |
| `HoldTests.TheRide_...` | pass: 16 m over the 12 m gap in 7.3 s |
| `HoldTests` | 16 of 18, then the two fixed (below) and passed alone |
| `DemoTests`, `DoorTests`, `FloorplanV2Tests`, `MapTests` | 85 of 89, then the four fixed (below) and passed alone |
| The fight classes (`KitTests`, `FlankingTests`, `EnemyAiTests`, `DiningHallTests`, `HumanShieldTests`, `CivilianEscortTests`, `ArmedCivilianTests`, `CoverTests`) | 49 of 50, then the shotgun fixed (below) and passed |
| The last two changes (Stevens' note, the cull) | NOT RUN: Bruno, "skip the tests; i'll do the manual testing right away". The tests exist: `HoldTests.StevensNote_...` and the drawn check in `TheRide_...` |

The layout run found four more faults:
1. The gap camera's zone took the floor just inside the tube's east doorway, and the camera looks west: the exit rule
   failed. The zone stops 1 m short of the doorway now.
2. The fire door list did not have the theater's door: added.
3. The fire team's calls dropped under the two-voice rule (yesterday): the test clears the voices before each loss.
4. A rear guard in the W5 corridor never fired: the clear-line rule stopped him behind the front guard, and he stood
   still. The spot search leaves out spots with a person on the line now (`SoldierBrain.PersonOnLine`). A first try also
   put the check in the hold-or-fire choice: then the holding element of a fire team walked whenever the other crossed
   its line (both walked 60 % of the frames, the limit is 35 %), and the shotgun of the Dining Hall never aimed. With the
   check only in the spot search: 1 % of the frames, the cone shows.

Two test faults on the way, both in my new tests: two full blasts one frame apart count as one hit (`HitEventBus` drops a
repeat within a frame, and a player never fires that fast), and the L37 test still read the old builder text.

## Stevens' note (Bruno, the same day)

```text
stevens' note unlocks the whole map, right? it shouldn't. the note starts in the inventory (we can add a quick "stenves note" added to inventory) to signal that this game has inventory. like he instructs, the full instruction thing/map is on a printer in a room. check if that's the case, if not modify the level to be so
```

The check: no note was in her inventory, and nothing opened the map early. The map item is L1, STEVENS' PRINTOUT, on the
printer of H9 (both floors). The fix: STEVENS' NOTE (`DemoItems.StevensNote`, a log, no map) goes into her inventory when
the frame lets her go (`ScriptStep.Give` with a notice: "Stevens' note added to the inventory."). Its words are a
PLACEHOLDER for the writer, from round 118 only: her name, the dizzy feeling, no time, the sheet on the printer.

## The gurney over the gap (a shot)

The ride shot showed Lucy over the gap with no gurney under her: the gurney stood in the theater's list of the room cull,
and the cull hid the theater. A thing she rides is always drawn now (`RoomTracker.Travels`).

Shots: `~/MEGA/glorified/renders/game/theater_gap/` (`theater_locked.png`, `gap_edge.png`, `gap_ride.png`,
`gap_ride_far.png`). The ride shots are from before the cull fix.
