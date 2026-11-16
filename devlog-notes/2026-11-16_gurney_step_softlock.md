# 2026-11-16: the gurney with no climb key, the softlock game over, the pits

Bruno:

```text
while we're at it check the gurney  on the holde code; the interact to go on top of it must go. the player must manipulate the object succesffuly so they can step on it and float to the other side. and yes, the player can softlock themselves by letting the gurney drop out of the playable area. we can check this and give an unique game over (with an unique death screen and a hint about softlocking) also check if the drop on the hold has a death volume (and the ladder puzzle area has it as well)
```

His pick for the step (the deck is 0.75 m high, and her step is 0.3 m): "Lower it over the gap".

## 1. No climb key

The fact-check: CONFIRMED. The Interact key climbed her onto the gurney and down again (`Rideable`, 2026-11-14).

Now no key reaches a rideable thing (`Rideable.Available` is false), the gurney and the boxes of the physics puzzles too.
The way on:

1. She carries the gurney with mk_orzabal (the defend key held, the mouse moves it, the wheel sets its height).
2. Over the gap, the wheel takes it lower than over a floor: down until the deck is level with the floor it stood on
   (`TelekinesisLift.LowestHeight`). Over a floor the low limit stays 0.1 m.
3. With the deck level with her feet and its near edge 0.9 m or less in front of her, the walk key steps her onto it.
   The carry becomes the ride, and the ride's cost is spent (40 MK) (`PsychicPowers.TryBoard`, `TelekinesisLift.Handover`,
   `Rideable.CanBoardFrom`).
4. The defend key is still held, so the gurney floats on with her. The walk keys steer it. A release over the gap does
   not land it. A release over a floor sets it down, and she walks off its end.

A box low enough for her step (0.3 m) she can still walk onto, and ride with the defend key.

## 2. The softlock

The gurney is the only way across before EXTEND. If she lets it go over the gap (or throws it in), it falls into the
dark, and she cannot leave the Hold.

`Interaction/LostProp` on the gurney: when it is 2 m under the floor where it stood, and she is still on the Hold's side
(H1 to H5, the tube room), and the tube is not out, the game ends 1.2 s later. The killer id is `softlock_gurney`, with its
own death screen (`DeathScreenSetup`, PLACEHOLDER words for the writer):

- the picture note: the gurney falls into the dark under the tube, and Lucy alone at the edge;
- the hint: the gurney is the only way across. Keep the defend key held over the gap: if you let go, the gurney falls,
  and you cannot leave the Hold. Lower it until the deck is level with the floor, then walk onto it.

## 3. The pits: no death volume

The fact-check: there is no death volume in either place.

| Place | What a fall does |
|---|---|
| The gap of the Hold (the tube room HT) | `PitReturn`: she is back in the airlock H5. No damage |
| The riser room G16 (the gap a crate fills; I take this as the "ladder puzzle area", the demo has no ladder puzzle) | `PitReturn`: she climbs out on the side she came from. The pit is 1.1 m deep, with a base, so a crate in it stays in reach |

G16 has no softlock: the two capsules behind its gap are not on the way on.

## Not changed

The theater test (`HoldTests.TheGurney_RidesOutOfTheTheater_...`) still rides the gurney from H4 to the airlock with the
test's climb (`Rideable.ClimbOn`, a test helper now). In the game she cannot step onto the gurney over a floor, so she
brings it to the airlock with the carry and the throw.

## Proof

NOT COMPILED, NOT RUN (Bruno's RAM). Changed tests: `HoldTests.TheRide_SheLowersTheGurneyOverTheGap_...` (the carry, the
lowering, the step on, the crossing, the walk off), `PhysicsTests.TheSurf_OnABox_...` (no key, the walk off).

To test by hand:

1. Run `Demo > Build the World`, then `Content > Build Death Screen Content` (the new death screen).
2. In the airlock, stand at the gap edge. Lock the gurney, hold the defend key, move it over the gap, and turn the wheel
   down until the deck is level with the floor.
3. Press the walk key: she steps onto it. Keep the defend key held and ride across.
4. Again, but let the defend key go over the gap: the softlock death screen comes.
