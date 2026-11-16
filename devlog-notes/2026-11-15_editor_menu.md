# 2026-11-15: the GlorifiedBody menu, production only

Bruno, with a shot of the menu:

```text
remove the ones that are legacy, and keep only the useful ones for a production pipeline in this dropdown
```

The menu had 49 entries in one long list. It now has 35 entries in seven groups, in the order of the work.

| Group | Entries |
|---|---|
| Demo | Build the World, Build the Map Atlas (after a room change), Build the Main Menu, Build the Tutorial |
| Content | Characters, Pose Clips, Enemy Prefabs (after a vendor import), Breakable Assets, Death Screen Content (after the writer's CSV changes), Difficulty Modules, Power Assets |
| Gore | the three steps after a vendor import |
| Checks | World Sanity (the open scene) |
| Scene View | Hide Roofs, Show Roofs |
| Player Build | Release, Dev (no player builds until the demo ships) |
| Test Rooms | the 16 test rooms: the tests and the TEST ROOMS column use them |

Removed from the menu (14 entries): the look tests of 2026-10-22 (three builds, two bakes, "both"), the T26 remake look
(five), the T38 Arkham look (two), and "Remove the dither" (done on 2026-11-03).

Only the menu entries changed. Every method stays, so `tools/unity.sh exec <Class>.<Method>` still runs each one. The
tutorial still uses `LookTestSetup.BuildTherapyGym`.

NOT COMPILED: Bruno's RAM is in use. The change removes or renames attributes only.
