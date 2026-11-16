# 2026-11-16: the surf is plain physics

Bruno, at the gap, on the gurney:

```text
why can't i float on? i'm stuck on some invisible obstacle
and didn't i said there was supposed to be no special treatment on the gurney re interaction?
it still auto puts her on top
it is not supposed to do that
i'm also testing if it's possible for players to surf other physics objects
```

The fact-check: CONFIRMED. Two parts of the code put her on top by script. The step from the hold
(`PsychicPowers.TryBoard`, earlier tonight) moved her onto the deck at the walk key. The ride (`Rideable`, 2026-11-14) put
her at the centre of the deck on every frame, and the walk keys steered it. Only the gurney and the puzzle boxes could be
surfed. The blocker of the stuck ride was not found: that ride is gone now.

His picks: "Mouse moves it", "Hold stays put".

## The surf now

1. A held prop keeps its place in the world while she walks (`TelekinesisLift`): the left-press lift and the defend-key
   carry. Only the mouse and the wheel move it, inside her reach. An enemy in the air still follows her.
2. Over a gap the wheel lowers a prop until its top is level with the floor (as before).
3. She walks onto it as onto any floor. No script puts her there.
4. A moving prop under her feet carries her: its move and its turn since the last frame (`TankController.PlatformDelta`).
   Any prop with a body: a crate, a box, a planter, the gurney. Not a body of a person.
5. The mouse moves it with her on it. Her walk keys walk her on its top, and she walks off where she wants.
6. If she lets it go over the gap, it falls: the softlock death screen, as before.
7. A long prop turns its end away from her only while she is off it.

Gone: `PsychicPowers.TryBoard`, `TryRide` and `TelekinesisLift.Handover`. `Rideable` stays as the marker of a thing that
travels (the room cull never hides it), and nothing starts its ride.

## Tests

Three tests of the old ride are on hold (`Ignore`): `HoldTests.TheRide_...`, `HoldTests.TheGurney_RidesOutOfTheTheater_...`
and `PhysicsTests.TheSurf_...`. A test of the new surf waits for a run.

NOT COMPILED, NOT RUN (no free RAM for Unity).

To test by hand:

1. Lift the gurney with the left press and walk to the gap edge. The gurney stays where it was: move it over the gap with
   the mouse, and lower it with the wheel until the deck is level with the floor.
2. Walk onto it. Move it with the mouse: she goes with it.
3. Try the same with a crate or a box in another room.
