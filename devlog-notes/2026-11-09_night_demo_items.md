# 2026-11-09, night: the five demo items

Bruno, before sleep:

```text
i'll go to sleep now so i'll review this in the morning; you can get started with all 5 items and i'll review progress
when i wake up in the moirning
```

The five items: (1) the brain fight in B1, (2) the dark room in G9 and G9b, (3) the audio logs L19 and L20, (4) the
writer's backlog lines, (5) the full check before a push.

## 1. The brain fight in B1

1. B1, THE BRAIN SECTOR, and B2, THE BUILDERS' DOOR, stand east of the coda on their own map floor, THE BRAIN
   (`Editor/Demo/DemoBrain.cs`). B1 is the sector of BRAIN TEST: `BrainTestSetup.DressSector` and `Wire` place it
   from the room's own corner now, so both scenes build the same sector.
2. The U1 Crown beat ends with a cut to B1 (it cut to X1 before, with a placeholder line for B1). The fight starts on
   her first step. Its death opens the north doorway. In B2 the exit lines of cutscene 10 play, then the cut to X1.
3. The eye camera turns up to 100° from its rest (55° before): it hangs on the south wall, and it must look along the
   walls into the corners. The coverage check now turns a camera that follows her toward each point as the director
   does (`CameraCoverage.TurnedTo`): no blind spot in B1.
4. A fault found on the way: the brain's track held into the next scene when its scene went in the fight (a death
   and RETRY, a quit). The music system lives for the whole game. `BrainFight.OnDestroy` gives the music back.

Tests: `DemoTests.TheBrainSector_B1_ItsDeathOpensB2_AndB2CutsToTheCarPark` (new), `U1_TheGrab..._AndSheStandsInTheBrainSector`
(it expected the car park and no HUD: now B1, the fight, and the HUD of the fight with no HP row), the coverage test,
the room count (43), `BrainFightTests.ItsSceneGoing_InTheFight_GivesTheMusicBack` (new). `BrainFightTests` and
`KitTests` 22 of 22. The demo bot passed before the eye's wider turn.

## 2. The dark room in G9 and G9b

`Editor/Demo/DemoDark.cs` wires DEMO_PLAN §10.1 into the demo, as DARK TEST has it:

1. She enters G9: the lights of G9 and G9b go out, the ten faces of the monitor wall go black, and the three units in
   G9b beep. The sector C release waits: "WAITING FOR NET."
2. The units stand on the floor of G9b, not in their order (ACCESS, NET, REC), each with a PC on it
   (`DarkTestSetup.Unit`, now shared). A wrong unit: the fault and the countdown, 5 s.
3. NET, REC, ACCESS: the monitors come back one by one with her face on them (`UpsBank.restoreOwnLook`: a screen
   shows its own material again), then the room lights. The release works.
4. The sources of the order: the hub's units under the counter of W9, with their up-times (NET 11:52:07, REC
   11:51:40, ACCESS 11:51:02), and the IT note on the clerk's monitor in G8.
5. The demo's sun lights only the lit layer now, so the dark room can leave it.

Test: `DemoTests.TheDarkRoom_G9_...` (new). With `DarkRoomTests` and the coverage test: 7 of 7. The dark bot passes.
