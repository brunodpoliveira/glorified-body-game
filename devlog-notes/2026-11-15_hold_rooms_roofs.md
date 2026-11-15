# 2026-11-15: two rooms more in the Hold, a bigger theater, the runner out of reach, the roofs

Bruno, after his test of the Hold:

```text
1 - theater room needs to be bigger to accomodate gurney and surgical bed. as it is it is hard to grab it again
2 - wonder if just for vizulaiztion sake i can slice off the roof to better indicate changes directly (and if you can remove the mis refactor warning from the screen)
3 - the short kills the scientist with the card every time (because there's no way to get through the 3 enemies without filling up the short bar)... so there's no way for a player to trigger the alternative solution
4 - need a room between where she is and where the two securit guards are;
5 - need a room between the security office and the theater control room
4 and 5 are because the short lasts long enough to kill the nurse on the control room
```

His answer to item 3: "Run out of reach".

## The new layout

| Room | Before | Now |
|---|---|---|
| H1 THE CELL | x 21 to 27 | x 25 to 31 (4 m east) |
| H2 TREATMENT | x 15 to 21 | x 19 to 25 (4 m east) |
| H2b STORES (new) | - | x 15 to 19, between H2 and the guard post |
| H3 GUARD POST | x 9 to 15 | the same |
| H3b SCRUB ROOM (new) | - | x 9 to 15, z 38 to 42, between the guard post and the theater |
| H4 THE THEATER | 6 x 5 m, z 38 to 43 | 11 x 7 m, x 4 to 15, z 42 to 49 |

The black hall around the box grew to match. The map shows the two new rooms.

## The theater (item 1)

1. The room is 11 x 7 m. The booth is the north strip (1.8 m deep) behind the glass front.
2. The surgery table stands in the west half. The gurney stands on the line of the door, with its foot 1.6 m from the
   door. She walks in past it, and the ride out goes straight through the door. (A first place 1 m east of the doorway
   failed the ride test: the gurney hit the door frame.)
3. The nurse stands at the west end of the booth, 2 m from the release button. The booth camera looks along the glass
   from the south-east corner.

## The runner out of reach (item 3)

1. The door from H2 to the stores is a fire door. It is shut at the start, it has no lever, and nothing breaks it.
2. When the first scientist dies, the door opens (`hold_h2_runner_out`). The card carrier runs through it into the
   stores and cowers in the north-west corner, behind a linen shelf.
3. When he cowers, the door shuts and jams behind him (`hold_h2_runner_shut`).
4. The guards come 8 s after the death (6 s before). They open the guard post's door and the stores' door on their way
   in.
5. The short does not pass a wall or a thing on its line. The shelf stands across every line from H2 through the
   doorway to his corner. So the short in H2 does not reach him. If she walks into the stores during a short, the short
   can still kill him. That is her choice.
6. If the guards open the door first, the door does not shut again. A shut door on her way is a lock with no key.

`FireDoor.openFlag` and `jamFlag` now take a list of flags, with commas between them.

## The short and the nurse (items 4 and 5)

The short lasts 16 s, and she walks at 0.96 m/s in it. The two new rooms add about 8 m to the walk from treatment to
the theater, and the nurse stands at the far end of the booth. From the guard post, the short reaches the nurse only
after about 10 s of walk, through the scrub room and 3 m into the theater. Numbers PROPOSED.

## The roofs and the MIS banner (item 2)

1. `GlorifiedBody > Scene View > Hide Roofs` hides every room's ceiling and the black hall of the Hold, in the Scene
   view only. `Show Roofs` brings them back. The game, the scene file and the bake do not change
   (`Editor/SceneViewTools.cs`, `SceneVisibilityManager`).
2. The MIS refactoring banner is gone from the Scene view. The vendor's `MISRefactoringChecker` drew it on every frame.
   Our editor code removes that hook when the editor loads. We do not use the MIS refactor, so the banner only covered
   the view. The code uses reflection, so a clone with no MIS still compiles.

## For the writer

1. The room names STORES and SCRUB ROOM are PLACEHOLDER.
2. The runner's lines (`h2: runner`, round 118) were written for a door that does not open: "Open the door! Open it!
   Please!" and "Let me through!". Now the door opens for him and shuts behind him. The lines still work, but the
   direction "he hits the panel of the door to H3" is out of date.
3. `DEMO_PLAN.md` §5.0 and the map v3 need the two new rooms and the bigger theater.

## Stevens' note: a toast now

The first test run found a fault from this morning's change. The "Stevens' note added to the inventory." line was a
message of the Interactor, and a message locks her feet. So after the frame let her go, she could not walk for 2.5 s,
and her first Interact press only closed the message. The line is now a toast at the corner of the screen (the same as
"SAVED IN SLOT 1", `SaveSystem.Toast`). It does not stop her.

## Proof

| Check | Result |
|---|---|
| `DemoWorld.BuildAll`, `MapAtlasSetup.BuildAtlas` | clean: H2b 4 x 5 m with 3 cameras, H3b 6 x 4 m with 2 cameras, H4 11 x 7 m with 4 cameras |
| `HoldTests` and `DemoTests`, first run | 5 failures: the restraint, the W14 TV and the art logs (all three: the note's message, above), the gurney ride out (the first gurney place), the fire team's last call |
| The same 5 and all of `HoldTests`, after the two fixes | 22 of 22 pass |
| `HoldTests.TheTwoScientists_..._TheSecondRunsOutOfReach_...` (changed) | pass: the stores' door opens at the death, he cowers 4.6 s later in H2b, 0.37 m from his corner, the door shuts behind him, no line from 99 points of H2 to him, then the guards open both doors |
| `HoldTests.TheGurney_RidesOutOfTheTheater_ThroughTheScrubRoom...` (changed) | pass: 15.3 m from the theater to the airlock |
| The fire team's last call | it failed once and passed in the rerun with no change: a timing fault of the test, not of the Hold |

Not run: the whole PlayMode suite and the bots (Bruno tests by hand). The Scene view menu and the MIS banner need his
editor: open the demo scene, use `GlorifiedBody > Scene View > Hide Roofs`, and look for the banner.
