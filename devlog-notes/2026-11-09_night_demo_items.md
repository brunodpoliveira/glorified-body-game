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
