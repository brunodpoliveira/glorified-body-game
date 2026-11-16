# 2026-11-16: a checkpoint after EXTEND, the softlock cutscene, the theater swap, Restart

Bruno, after a play of the Hold with the plain-physics surf:

```text
1: needs a checkpoint right after extending the tube
1.1: we can make an exception to the float thing: it is bizzarelly stressful to float towards the other side, and needlessly so; let's have the option (needs further play testing) player be stuck on the gurney until they move (on purpose, with the direction keys) just add that option but keep it disabled for now, i need to play test it further to see if it'll be necessary
2: the player in the actual demo won't die if they softlock, but will get a special cutscene
3: it is possible to sequence break by floating the gurney outside of the room; invert the postions of the gurney and medical table so that is no longer possible
also: restart needs to be clearer: restart from latest checkpoint
and restart is also unclear on how it works: it seems to save room by room?
```

His picks: Restart loads "checkpoints only", and after the softlock cutscene "the gurney comes back".

## 1. The checkpoint after EXTEND

A checkpoint box at the EXTEND console of the landing H6, shown by the tube's flag (`hold_tube_out`). She stands in it as
she presses the button, so it saves at once. A load puts her at the console, facing the tube.

## 1.1 The option to stay on the prop (off)

`TankController.StickToProp`, off. On: on a moving prop she keeps her place on its top until a walk or turn key moves her.
To try it: in play, select Lucy, and tick "Stick To Prop" on her TankController.

## 2. The softlock: a cutscene, no death

When the gurney falls into the gap before EXTEND, with her on the Hold's side, a cutscene plays (a PLACEHOLDER card,
`cutscene_hold_gurney_lost`). Then the gurney stands in the theater where it stood at the start, and she stands in the
airlock, facing the glass door (`LostProp`). Her kills and items stay. The death screen of the softlock is gone.

## 3. The way out over the walls

The fact-check: CONFIRMED, and wider than the gurney. A held prop rose up to 2.6 m over where it lay
(`TelekinesisLift.MaxHeight`), and the ceilings have no collider. With her on its top she floated over the 3 m walls,
out of any room.

1. The table and the gurney changed places, as Bruno asked: the table and its drapes stand by the door, the gurney in the
   west half of the theater.
2. While she stands on a held prop, the wheel cannot raise it so high that she rises past the ceiling of the room it is
   in (`TelekinesisLift.HighestHeight`, her height 1.8 m). Off its top, the limit stays 2.6 m, so a box can still go onto a
   box in the puzzles.

## Restart

The fact-check: he read it right. Restart (the pause menu) and Retry (the death screen) loaded the latest checkpoint.
With none, they put her back at the doorway of the room she last entered: a hidden save at every room
(`SaveSystem.OnRoomChanged`, 2026-11-02).

1. Both buttons read "Restart from latest checkpoint". They load the latest checkpoint: a checkpoint box, a save
   machine, or a load.
2. The hidden room save is gone. With no checkpoint, the demo starts over, the wake and all.
3. DEMO: DINING HALL saves a checkpoint at its point. It stays for the session and parks the figures of the other rooms
   again after each load.
4. `DemoTests.ARestart_WithNoCheckpoint_StartsTheDemoOver` replaces the test of the room save.

NOT COMPILED, NOT RUN (no free RAM for Unity).
