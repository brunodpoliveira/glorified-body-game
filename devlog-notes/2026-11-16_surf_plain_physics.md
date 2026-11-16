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

## Later: the lock is the aim of a throw

Bruno: "how can the player aim a orzabal lifted object at an enemy rn?" Before, a throw went to the nearest enemy inside
25 degrees of her facing, else to the nearest enemy in any direction, else straight ahead. The lock stayed on the held
thing, and no marker showed the aim. His pick of two: the lock is the aim ("we need to be as consistent as possible"), and
"with no lock as a fallback it does the aim with her facing thing".

1. While she holds a thing, the lock leaves it for the nearest enemy (`LockOnController.AimAwayFromHeld`).
2. While she holds a thing, Tab and the wheel click reach only enemies. The held thing is never a target. The wheel still
   raises the held thing.
3. A throw goes to the locked enemy. With no lock, it goes to the enemy inside 25 degrees of her facing, else straight
   ahead. The search for the nearest enemy in any direction is gone.
4. A charge turns her to the locked enemy, as for the other powers.

NOT COMPILED, NOT RUN. Tests that expect the lock to stay on a held thing can fail: they wait for a run.
