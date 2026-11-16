# 2026-11-15: the round chapel, the dev start in the dining hall, the tube console

Bruno, with two Scene view shots (the chapel, and the main entrance G13b):

```text
okay, the chapel room is also circular;
also, tell me what beat occurs in this triangular room?
no automated testing; once it is done i suppose to get the player dragged to the dining hall so i won't have to test the whole level, right?
and also, the tube (on the hold) starts retracted. as it is, i think it is fully extended, at least when i inspeacted it (on the inspector screen) it starts extended. we can have a button on a computer on the other side to exteend the tube so the player can backtrack to the hold if they need/want to without needing to surf the gurney again
```

No Unity run: Bruno's RAM is in use by other apps. NOT COMPILED, NOT RUN. Bruno tests by hand.

## 1. The round chapel

The fact-check: CONFIRMED. `DEMO_PLAN.md` §10.5b item 3 (rounds 93, 147 and 148) makes G4 circular, with the altar in
its centre. The builder made a square room with two columns of pews and a lectern.

G4 now (`DemoPublic.G4`, PROPOSED where the plan says PROPOSED):

1. A ring of 24 dark panels, radius 3.6 m, in the square of the old room. The corners behind the ring are solid. The
   way in from the door of G2 is open. The foyer and the chapel share the corner code (`DemoPublic.SolidCells`).
2. The altar in the centre: three round marble steps and the marble block on the top step.
3. The screen behind the altar: 72 brass flakes from the floor to the ceiling, with no collider.
4. The shaft of light: a lit disc in the ceiling over the altar. The greybox materials are unlit, so a real light does
   nothing here yet.
5. Six pews in an arc face the altar. The way from the door to the altar stays open (1.4 m between the two nearest pews).
   The lectern is gone (the plan has no lectern).
6. The gear and the verse are on a wood board on the west arc. The screen stands where the gear stood (the north), and
   it hid the gear from the door.
7. The nurse kneels behind the east pew. The prayer card L8 lies on the south-west pew. The capsule lies in the north-west
   aisle.
8. The candle camera (`Cam_G4_candles`) stood in the south-west corner, which is solid now. It stands inside the ring, west
   of the door, with the candle stand in front of it.

## 2. The beat of the triangular room

There is no beat. The triangular room is G13b, THE MAIN ENTRANCE (2026-11-14, Bruno's pictures of the glass roof). It is
a passage from the admissions office G13 to the main doors. The steel shutters are down, with the sign "LOCKDOWN. SHELTER
IN PLACE." It shows the player that the front door is shut by code silver. It never opens in the demo. A beat for it is a
question for the writer.

### The main entrance, larger

Bruno, after the answer:

```text
if that is the main entrance, then it needs to be both wider and larger
```

G13b was 8 x 3 m, a corridor. It is now a hall 16 m long and 8 m wide (x -12 to 4, z -8 to 0), with the ridge of the
glass roof at 7.2 m and the eaves at 4.2 m. The floor to the west and the south was free. G1 stands north of its east
half, so it cannot grow north. The door from the admissions office stays where it was. The shutters rise to the eaves,
and six benches stand along the glass walls. The axis camera keeps a strip 4 m wide down the middle, and the cameras of
the cells take the sides. PROPOSED.

## 3. The dev start in the dining hall

The fact-check: no such start existed. Only the tests put her in the hall.

The TEST ROOMS column of the main menu has a new button, DEMO: DINING HALL (dev builds and the editor only). It:

1. loads the demo,
2. skips the wake and frees her from the frame,
3. refills her as F7 does (HP, every MK bar, SHORT at 0, two drugs of each type),
4. puts her inside the glass doors of G18, the hall in front of her.

The rooms before the hall stay as they are at the start (nobody dead, no door open). `DebugTools/DevRoomStart.Begin(room,
point, yaw)` takes any room of the demo, for the next room Bruno wants to test.

## 4. The tube

The fact-check: WORKS AS DESIGNED, and the button already exists.

1. In play, the tube starts retracted. Its floor and ribs are under a `FlagGate` (`hold_tube_out`), and the gate hides them
   when the room loads. In the Scene view and the Inspector, the gate does not act, so the tube shows extended there.
2. The EXTEND button stands on the landing H6, by the tube's doorway: the far side from the Hold. After the gurney ride she
   presses it, the tube comes out, and it stays out. Then she can walk back to the Hold. `HoldTests.Extend_...` proves it.

The change: the button was a small yellow box on the wall. It now sits on a console with the sign TUBE CONTROL over it, so
it reads as a computer.

## For the writer

1. G4: the pews in an arc and the gear on the west arc are PROPOSED.
2. G13b: does the main entrance get a beat?

## Before the next push

Run `DemoWorld.BuildAll`, then `MapAtlasSetup.BuildAtlas` (the chapel, the main entrance and the H6 console change the demo scene). Then
`DemoTests` (the cameras of the round chapel), `HoldTests.Extend_...`, `PlayerBuildFlagTests`, and the demo bot.
