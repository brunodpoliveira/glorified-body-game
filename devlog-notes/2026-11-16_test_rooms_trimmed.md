# 2026-11-16: the test rooms, trimmed

Bruno: "remove all redundant debug rooms: keep only the current useful ones for us". His picks: the menu only for the
test rooms, keep six, delete the finished look tests.

## The TEST ROOMS column: 6 of 18

Kept: TRAINING ROOM, DEMO: DINING HALL, AI TEST, KIT TEST, BRAIN TEST, PHYSICS TEST.

Out of the column: COVER, CIVILIAN, SCRIPT, TRAVEL, LAB, WARD, SCREEN, OUTFIT, DOOR, EDGAR, CODE, DARK. Every system of
these rooms lives in a demo room now. Their scenes, builders, tests and bots stay: the automated checks load them (for
example, LAB TEST holds `ArmedCivilianTests`, DOOR TEST holds `DoorTests`).

## The look tests: deleted

1. The scenes: LookTest, LookTest2, LookTestExterior, RemakeLook, RemakeLookBright, ArkhamLook, ArkhamLookBright, with
   their bakes. Out of the build list too.
2. Their builders and their scene-only parts: `ExteriorLookSetup`, `RemakeLookSetup`, `ArkhamLookSetup`, `RemakeDirector`,
   `ArkhamDirector`, `RemakeBodySwap`, `RemakeBakeSwap`.
3. Their bots: `LookPlaythrough` ("look"), `RemakePlaythrough` ("remake"), `ArkhamPlaythrough` ("arkham", "arkham_time"),
   `T27Playthrough` ("t27").
4. F11 (the next look room) is gone from the debug keys and from How to Play.
5. Two EditMode tests checked the look scenes and their bakes: gone.

Kept, because other parts use them: `LookTestSetup` (the tutorial's gym, materials and textures for eight test-room
builders), `LookDirector` (the tutorial tests, a renderer feature), the renderer assets and the textures, and
`T27BakedCharacters` (the enemy tests on the baked guard). The renders of the look tests stay in MEGA.

NOT COMPILED, NOT RUN (no free RAM for Unity).
