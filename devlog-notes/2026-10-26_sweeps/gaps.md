# Seam sweep: the gaps between systems (2026-10-26)

Read-only audit. No file changed, no Unity run. Line numbers are at commit `1aa3038` (HEAD). Paths are under
`/home/pc/MEGA/code/glorified-body-game/Assets/_Game/Scripts/` unless they are given in full.

Note: the exp levels were built while this audit ran. The audit does not cover them.

## What exists in each room

The shared builders give every playable room the same base: `MenuLoopSetup.EnsureLucy` (Editor/MenuLoopSetup.cs:150),
`EnsureGameplaySystems` (:210, with `LookQualityApplier`), `EnsureRuntimeUi` (:224, with `PlayerHud`, `InteractionUI`,
`InventoryUI`). `SubtitleUI` and `SaveMenuUI` make themselves at run time and stay across scenes (DontDestroyOnLoad).
`GoreBootstrap` adds the gore on each scene load that has a `GameplayInitializer` (Gore/GoreBootstrap.cs:31).

| Room | Enemies | Items, doors, scan | Cover | Save point / checkpoint | NavMesh | In git |
|---|---|---|---|---|---|---|
| TrainingRoom | 2 capsule dummies (1 `DummyShooter`), cast is visual only | 3 pills, 2 crates | none | YES, the only room (MenuLoopSetup.cs:112-113) | no | yes |
| Tutorial | 4 `DummyShooter` targets, 3 shields | log, key, `LockedDoor`, closet pill, capsule, `Scannable` x3, crate | 1 barricade, 1 lamp | none (Bruno's ruling) | no | yes |
| AiTest | 4 FSM soldiers (rifle, shield guard, armour, gasser) | none | none | none | yes | NO (local only) |
| CoverTest | 2 FSM soldiers | none | barricades, tables, plaster, screens, lamp | none | yes | NO (local only) |
| CivilianTest | 1 FSM guard, 2 lab workers, 1 nurse | none | none | none | yes | NO (local only) |
| LookTest / LookTest2 | 1 dummy + figures | none | none | none | no | yes |

Facts that follow:
- No room has soldiers, civilians, cover, items and a save point together. The demo's real rooms will be the first.
- The only door is `LockedDoor` (Interaction/LockedDoor.cs:12). It slides a leaf. No code loads another scene from a
  door. Every scene change is a menu button, a debug key (F11), a save load, or a test call to `GameManager.LoadLevel`.
- 70 of the PlayMode tests load a git-ignored room: BreakableTests 11, CivilianTests 20, DeathScreenTests 3,
  EnemyAiTests 20, EnemyMoraleTests 5, GoreTests 4, RagdollTests 5, SaveGoreTests 2. None has an `Assert.Ignore` guard.
  On a fresh clone they fail.

## The seam table

| # | Seam | Systems | Covered? | Risk: what could break | Testable alone? |
|---|---|---|---|---|---|
| 1 | Pause (Esc) during the short's 2.5 s freeze | GameManager, ImpactFrameController, ShortController | none | `Pause()` stores `Time.timeScale` = 0 (Systems/GameManager.cs:134). The freeze routine stops when the state is not Playing and does not put the time back (Systems/ImpactFrameController.cs:75-79, 88-92). `Resume()` restores 0 (GameManager.cs:147). The game stays frozen in the Playing state. A pause in the 0.2 s ramp leaves a slow-motion game. | yes, TrainingRoom |
| 2 | Inventory (I), save machine, drug menu (Q) during the freeze | InventoryUI, SaveMenuUI, GameManager.OpenInventory | none | Same trap: `OpenInventory` stores the frozen time scale (GameManager.cs:58). InventoryUI opens while `IsPlaying` (UI/InventoryUI.cs:99, 116). The Interactor too (Interaction/Interactor.cs:55). | yes, TrainingRoom |
| 3 | A pause during the freeze, and the shocks | ShortController, pause | none | The pain timer is unscaled (Player/ShortController.cs:65, 106). The run loop waits with `yield return null` (:135), which runs at time scale 0. At 2.5 s the first shock lands while the pause menu shows: damage to the victim and to her, a zap that ignores the listener pause (:248). | yes, TrainingRoom |
| 4 | A drug in the 0.45 s death beat | PsychicPowers, PlayerDeathFlowController, Health | none | `PsychicPowers.Update` has no dead check (Player/PsychicPowers.cs:94-103). Q works in the 0.35 s ramp after the 0.12 s freeze, the inventory works for the whole beat. `UseDrug` (:343) calls `RefillAll` -> `Health.ReviveToFull`, which clears `IsDead` (Core/Health.cs:119). The game over still fires (Systems/PlayerDeathFlowController.cs:92-102): a death screen over a living Lucy at full HP. The inventory drug path is the same. | yes, TrainingRoom |
| 5 | A save at 0 HP | SaveSystem, Interactor, CheckpointPoint | none | `Capture` does not check `IsDead` (Saving/SaveSystem.cs:157). In the death beat the state is still Playing, so E at the machine opens the slot menu. A load calls `SetHealth(0)`, which calls `Die` (Core/Health.cs:103). The checkpoint copy (SaveSystem.cs:207) makes RETRY a death loop. | yes, TrainingRoom |
| 6 | A save during the short | SaveSystem, ShortController, PsychicStats | none | The bar is full during the short. The short itself is not saved. After a load she has the HP of the moment, a full bar, and the next press starts a new short with a new freeze. | yes, TrainingRoom |
| 7 | A save with a door opened | SaveSystem, LockedDoor | none | The `opened` path (SaveSystem.cs:276-279, `Open(instant: true)`) never runs in a test. The only door is in the Tutorial, which has no save point. EditMode `SaveDataTests.TheFile_KeepsEveryField` does not check `opened`. | yes, Tutorial (call `SaveSystem.SaveCheckpoint` directly) |
| 8 | A save with a carried object and the guard outfit | SaveSystem, ItemInventory, OutfitProfile | none | `SaveTests.ASave_ThenALoad_...` checks a key item and pills only. The carried list and the outfit restore (SaveSystem.cs:244-248) are not tested. No game code changes the outfit yet. Only tests call `SetOutfit`. | yes, TrainingRoom |
| 9 | A save: bars, key, pills, place, a dead dummy, a taken pill | SaveSystem, PsychicStats, DrugInventory | `SaveTests.ASave_ThenALoad_GivesBackHerBarsHerItemsHerPlace_AndTheRoom` | covered | - |
| 10 | A save: bodies, stumps, pools, stains, cut arm, shield. Gore Off | SaveSystem, RoomGore, gore | `SaveGoreTests.TheDead_LieWhereTheyLay_WithTheirGore_AfterALoad`, `SaveGoreTests.GoreOff_PutsBackTheBodies_ButNoBlood` | covered (AiTest, local only) | - |
| 11 | A save with a broken room | SaveSystem, RoomDamageLedger, Breakable | partial: EditMode `SaveDataTests.ABrokenRoom_SurvivesTheLedgerExportAndImport`. `BreakableTests.BrokenRoom_StaysBroken_WhenSheComesBack_ARestartPutsItBack` (leave and come back, no save) | No PlayMode load into a broken room. Soldier cover choice after a load is untested. | yes, CoverTest |
| 12 | A save with live enemies that are hurt or alerted | SaveSystem, SoldierBrain | none | Only dead or alive is saved. A soldier at 10 % HP comes back full, at his spawn, not alerted. A design choice, not written down. | yes, AiTest |
| 13 | A save whose scene is not in the build | SaveSystem, GameManager, MainMenuUI | none | No scene check (no `CanStreamedLevelBeLoaded` anywhere). `Load` writes the checkpoint first (SaveSystem.cs:207), then `LoadLevel` (:216) sets the Loading state (GameManager.cs:211) and the load fails. CONTINUE (UI/MainMenuUI.cs:146-150) hangs in Loading. A room rename in the demo makes old saves do this. | yes, EditMode or Main_Menu |
| 14 | RETRY after a death, with dead bodies | DeathMenuUI, SaveSystem, RoomGore | partial: `SaveTests.ACheckpoint_SavesWhenSheWalksIn_AndADeathRestartsThere` (TrainingRoom, the dummies vanish at death). The bodies only by a direct load in SaveGoreTests | Same code path (`Load(0)`), so low risk. No test goes death screen -> RETRY in a room with bodies. | yes, AiTest |
| 15 | Room-to-room travel | GameManager, Lucy, SaveSystem, RoomDamageLedger | none: there is no exit to another scene | Lucy is a scene object. A new scene gives a fresh Lucy: HP, MK, SHORT, items, pills, outfit, kills reset. The save's room state is the active scene only (SaveSystem.cs:176-197). Only the ledger keeps every room. So when she comes back to a room: soldiers alive again, pickups there again (item copies), but the barricades still broken. | partly: TrainingRoom <-> Tutorial once an exit exists |
| 16 | Difficulty presets with FSM soldiers | DifficultyManager, ShooterScheduler, SoldierBrain | `EnemyAiTests.EasyDifficulty_OnlyOneSoldierAimsAtATime`, `EnemyAiTests.Telegraph_FollowsTheDifficulty_Hard08_Easy15`, `DummyShooterTests.Difficulty_LimitsHowManyAimAtOnce`, `MenuLoopTests.PresetSwitch_ChangesMaxSimultaneousShooters` | covered | - |
| 17 | Difficulty modifiers Fragile, OneHitWonder, Scarcity | DifficultyManager, bars | none: no consumer | Only the menus read them (UI/MainMenuUI.cs:302, UI/DifficultyMenuUI.cs:73). The enum still says "Consumer: step 3" (Systems/DifficultyModuleDefinition.cs:20-22). A player can turn them on. Nothing changes, but the total percent goes up. | yes, TrainingRoom |
| 18 | Story flags | StoryFlags, SaveSystem | none: no writer | `StoryFlags.Raise` has no caller. The flags survive a `RestartLevel` (they reset only at the menu or a load), unlike the ledger. | yes |
| 19 | A civilian's scream calls a soldier | CivilianBrain, EnemyAlert, SoldierBrain | `CivilianTests.TheScream_CallsTheGuard_WhoCannotSeeHer`, civilians bot `guard_called` | covered | - |
| 20 | A civilian kill and the SHORT bar | PsychicStats, CivilianBrain, DummyShooter | none | `NotifyKill` (+25 SHORT, Kills+1) is called only from `DummyShooter.Die` (Enemy/DummyShooter.cs:434-442). Civilians get no DummyShooter (Integration/Invector/Editor/InvectorEnemyFactory.cs:231-248). So a civilian kill gives no SHORT and no kill. The new exp code counts kills from the hit bus, so exp and SHORT will disagree. | yes, CivilianTest |
| 21 | mk_erebus (fire) on a civilian | PsychicPowers, BurningStatus, CivilianBrain | none | The burn's hurt, the panic rule ("only a kill makes it run") and the fire kill witness are not tested together. | yes, CivilianTest |
| 22 | mk_orzabal: lift a civilian, throw it into a soldier | TelekinesisLift, CivilianBrain, EnemyRagdoll | none | The civilian has its own suspend path (Enemy/CivilianBrain.cs:327-342). Only soldiers are lifted in tests. | yes, CivilianTest |
| 23 | The short on a civilian | ShortController, CivilianBrain | `CivilianTests.TheShort_HurtsACivilian` | covered | - |
| 24 | The short while she holds a floater | ShortController, TelekinesisLift, PsychicPowers | none | A shock comes from her, the holder, so it does not drop the floater (Combat/TelekinesisLift.cs:116-121). Powers are off in the short. The floater hangs for the whole 16 s. | yes, AiTest or TrainingRoom |
| 25 | The second wind and a freeze or a pause | Health, ShortController, GameManager | none (`PowersAndBarsTests.SecondWind_...` alone) | The 2.5 s invulnerability runs on unscaled time (Core/Health.cs:52, 84). A pause or the short's 2.5 s freeze uses it up. A lethal hit adds SHORT. If that fills the bar during a charge, the short starts at once and the second wind ends as the world starts again. | yes, TrainingRoom |
| 26 | The short and the lamps | ShortController, Breakables | `BreakableTests.TheShort_PopsTheLampsNearHer_TheirLightAndBounceGoOut` | covered | - |
| 27 | Death during the short | ShortController, death flow | none | Low. The run coroutine stops at time scale 0, the reload clears it. | yes, TrainingRoom |
| 28 | Death screen and the killer | DeathMenuUI, weapons | `DeathScreenTests.KilledByTheRifleSoldier_...`, `..._FallsBackToTheGuardClass`, `ANewRoom_ForgetsTheLastKiller`. EditMode `DeathScreenContentTests.TheBuiltContent_HasAnEntry_ForEveryKillerOfTheTestRooms` | partial: grenade, rocket, gas and melee deaths are not checked end to end | yes, AiTest |
| 29 | Subtitles during a pause and the freeze | SubtitleService, SubtitleUI, pause | none | Lines expire on scaled `Time.time` (Subtitles/SubtitleService.cs:52, 77) and draw at sort order 112 (Subtitles/SubtitleUI.cs:18), above the pause menu at 100 (UI/RuntimeOverlayMenu.cs:14). A bark freezes on top of the pause menu. The "[screams]" caption (ShortController.cs:109) is set during the freeze, so it stays about 2.5 s after the scream ends. | yes, TrainingRoom |
| 30 | Gore Off and Low on every path | GoreManager, GruesomeDeathFx, BodyBloodPool, driver | partial: `GoreTests.Gore_AHitBleeds_TheOptionOffStopsIt`, `GoreTests.Gore_AGruesomeKill_BurstsTheHead_OnFullOnly`, `SaveGoreTests.GoreOff_...` | Not tested: the limb cut on Low and Off, the body pool on Off, a change of the option in the middle of a room (old stains stay). Morale does not depend on gore (Enemy/EnemyMorale.cs `IsGruesome` uses the damage type): good. | yes, AiTest |
| 31 | Blood on a breakable that breaks | ThatIndividualGoreDriver, FloorStains, Breakable | none | Stains lie on any surface the ray hits (Gore/ThatIndividual/ThatIndividualGoreDriver.cs:463). When the table or the plaster breaks, the stain floats. A save keeps it floating. | yes, CoverTest |
| 32 | LOOK option in every room | LookQualityApplier, rooms | partial: `LookQualityTests` (TrainingRoom), `LookTestTests.TheClearLook_UsesTheStockRenderer_InTheLiveDressings` (look rooms) | Not tested in Tutorial, AiTest, CoverTest, CivilianTest. The applier writes `msaaSampleCount` on the shared URP asset (Rendering/LookQuality.cs:53). In the editor that change stays on disk after the run. | yes, each room |
| 33 | The tutorial with FSM soldiers | TutorialDirector, SoldierBrain | none | The gym uses capsule `DummyShooter` targets and has no NavMesh. The player learns the telegraph on a dummy, not on the AI that moves. | needs a NavMesh bake in the gym |
| 34 | Inventory and slot menu stop the game | GameManager, InventoryUI, SaveMenuUI | `InteractionTests.TheInventory_StopsTheRoom_AndListsTheItems`, `SaveTests.TheSlotMenu_StopsTheGame_...` | covered when the time scale is 1 before (see rows 1-2) | - |
| 35 | Lock-on with props and civilians | LockOnController, LockOnTargetable | `PowersAndBarsTests.Props_TheAutoLockSkipsThem_...`, `CivilianTests.TheAutoLock_NeverPicksACivilian_TheSwitchDoes` | covered | - |
| 36 | Camera coverage in the fight rooms | CameraZone, FixedCamera | partial: `TutorialTests.EveryFloorPoint_IsInTheFrameOfItsCamera`, `LookTestTests.EveryFloorPoint_...` | Not in TrainingRoom, AiTest, CoverTest, CivilianTest. | yes, each room |
| 37 | Reading a log or a scan clip in a fight | Interactor, PsychicScanner, enemies | none | The world does not stop while she reads or watches (only the inventory and the slot menu stop it). A design question. | yes, Tutorial with a shooter on |
| 38 | Key remap and the other actions | InputManager, GameInput, TutorialPromptUI | partial: `MenuLoopTests.Remap_Dodge_SavesToPlayerPrefs_AndResets`, EditMode `InputBotTests` | The tutorial hints with a remapped key are not tested. | yes, Tutorial |
| 39 | Static state from room to room | see the list below | none | Small, see below. | partly |

### Static state that a scene load does not reset

Most statics reset only once per play (`SubsystemRegistration`), not per scene. The registries clean themselves in
`OnDisable`, so they are safe. These are not:
- `CivilianBrain.nextVariant` (Enemy/CivilianBrain.cs:80, 121): the pose variant of each civilian depends on how many
  civilians the session made before. After a RETRY the wall leaner can be a different civilian.
- `BodyBloodPool.bodies` (Gore/BodyBloodPool.cs:26): grows with every body of the session (memory only).
- `SoldierBrain.coverClaims` (Enemy/SoldierBrain.cs:76): keeps entries of destroyed barricades (memory only. The
  `owner != null` check at :486 keeps it correct).
- `StoryFlags` (row 18): survives `RestartLevel`.
- `CorpseCare.DebugHidden` (Enemy/CorpseCare.cs:339): the F12 debug flag stays on in the next room.
- `GameManager.storedTimeScale` (rows 1-2): the real leak between systems.
- `DeathMenuUI.LastKillerId`: resets on each scene load (UI/DeathMenuUI.cs:29). Good.
- `SaveSystem` baseline and `goneByLoad`, `RoomDamageLedger`: reset or move on each scene load. Good, but they know
  only the active room (row 15).

## The 10 worst gaps, ranked

Sizes are hours to write a PlayMode test that shows the gap (and to fix it where the fix is small).

1. **Time scale stuck after a pause or a menu during a freeze** (rows 1, 2, 3). Any player who presses Esc, I or Q in
   the 2.5 s migraine freezes the game for good, or leaves it in slow motion. One shock also lands under the pause
   menu. Test in TrainingRoom: fill the bar, start the short, `Pause()` at 1 s, `Resume()` at 2 s, wait 4 s, check
   `Time.timeScale == 1` and `ShocksFired == 0` while paused. Same test for `OpenInventory`. **1.5 h** (fix: the freeze
   owns its time through GameManager, or Resume never restores a value under 1 left by the freeze).
2. **No travel between rooms, and no state that crosses a scene** (row 15). Lucy, her bars and her items live in the
   scene. Dead, taken and opened are saved for the active room only. Coming back means live soldiers and copied
   pickups. The demo needs this first. Test after an exit exists: TrainingRoom -> Tutorial -> TrainingRoom, check HP,
   MK, pills, key, the dead dummy and the taken pill. **3 h for the test, 6-10 h for the exit and a session store.**
3. **The death beat accepts a drug, the inventory and the save machine** (rows 4, 5). A drug revives a dead Lucy under
   the death screen. A save at 0 HP makes RETRY a death loop. Test in TrainingRoom: kill her, press Q (bot source) in
   the beat, check she stays dead. Call `SaveSystem.Capture` at 0 HP, check it refuses. **1.5 h** (fix: dead and
   not-Playing guards in `PsychicPowers.Update`, `InventoryUI`, `Interactor`, `Capture`).
4. **A save whose room is missing hangs CONTINUE** (row 13). Room names will change in the demo. Test: write a slot
   with `scene = "NoSuchRoom"`, call `Load`, check it returns false and the checkpoint file is not overwritten.
   **1 h.**
5. **Save holes: doors, carried objects, outfit, the short in progress** (rows 6, 7, 8). The door path never ran.
   Test in Tutorial: open the closet door, take the log and the pill, `SaveCheckpoint`, load, check the door is open
   and the items are there. Second test in TrainingRoom: guard outfit, a carried object, a save in the short.
   **2.5 h.**
6. **Civilian kills give no SHORT and no kill** (row 20). A civilian kill should raise the bar if the design means
   "every kill". With the new exp, exp counts them and SHORT does not. Test in CivilianTest: kill a lab worker with
   a full blast, check `Short` rose by 25 and `Kills` by 1. **1 h** (fix: count kills from the hit bus, as the exp code
   does, not from `DummyShooter.Die`). Ask Bruno first.
7. **Difficulty modifiers have no effect** (row 17). The menu offers three switches that do nothing. Either hide
   them (0.5 h) or build the consumers with one test each in TrainingRoom (Fragile: HP drain x2. OneHitWonder: HP 1,
   no second wind. Scarcity: MK drain x1.5). **3-4 h.**
8. **70 tests need rooms that are not in git** (the room table). A fresh clone or a CI run fails them, and the demo
   rooms get no AI coverage by default. Add an `Assert.Ignore` when the scene is not in the build (1 h), or build the
   rooms in the test setup. Better for the demo: one "mixed room" builder (soldiers, civilians, cover, items, a save
   point, a NavMesh) and a smoke test that runs one fight, one save and one load in it. **1 h guards, 5-6 h mixed room.**
9. **The short with a floater, and the second wind with a freeze** (rows 24, 25). The floater hangs 16 s. The second
   wind's invulnerability burns in the freeze. Test in AiTest: lift a soldier, fill the bar, start the short, check
   the floater drops (or define the rule). Test in TrainingRoom: lethal hit, start the short, check she is still
   invulnerable after the freeze. **1.5 h.** Ask Bruno for the rule on the floater.
10. **Civilians and the other two powers** (rows 21, 22). Fire on a civilian and a thrown civilian are untested. The
    civilian has its own suspend code. Test in CivilianTest: full fire on the nurse (hurt alerts, death panics the
    lab worker), lift the lab worker and throw it into the guard (both fall, the guard stands up). **2 h.**

Smaller, not in the 10: captions frozen over the pause menu and the long "[screams]" caption (row 29, 0.5 h). Blood
floating over a broken table (row 31, 1 h). The LOOK option in the four other rooms and the URP asset write (row 32,
1 h). Camera coverage in the fight rooms (row 36, 1 h with `CameraCoverage`). `CivilianBrain.nextVariant` per room
(0.5 h). Death-screen ids for grenade, rocket, gas and melee (row 28, 1 h).

## Open questions for Bruno

- Does a civilian kill fill the SHORT bar? (gap 6)
- Does a load bring back a hurt soldier at full HP and unalerted? (row 12)
- During the short, does a floater drop at once, or hang? (gap 9)
- Does reading a log or watching a scan clip stop the world? (row 37)
- The difficulty modifiers: build now, or hide until later? (gap 7)
