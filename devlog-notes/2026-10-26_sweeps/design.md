# Design sweep: what is not prototyped yet (2026-10-26)

Read-only audit. Code paths are under `Assets/_Game/Scripts/` unless they start with `Tests/`, which means
`Assets/_Game/Tests/`. Test names are PlayMode tests unless marked EditMode. Bots: `tools/unity.sh play ...`
(structural, look, tutorial, ai, cover, civilians).

Status key: **BUILT** = code plus a test or a bot. **PARTLY** = some code, a piece is missing. **NOT BUILT** = no
code. **CONTENT** = the mechanic exists. The demo needs only data (rooms, lines, art, audio).

The experience levels were built the same day: see `devlog-notes/2026-10-26_exp_and_sweeps.md`.

---

## 1. `03_GAME_DESIGN.md` (CANON)

| Item | Status | Evidence | What is missing |
|---|---|---|---|
| §1.3 Cutscenes: stills + voice, some Seedance cuts | NOT BUILT | No cutscene player found (searched: cutscene, PlayableDirector, Timeline). Only `Subtitles/SubtitleTrack.cs:14` (a Timeline subtitle track), test `SubtitleTests.ACutsceneClip_ShowsItsLine_ForItsTime`. `GameManager.cs:15-24` has no Cutscene state | A player for a still sequence with voice audio, a video cut, a skip key, a Cutscene state that stops the game |
| §1.4 / §6.3 Voice acting (cutscenes only) | NOT BUILT | No voice clip playback found. `Enemy/Bark.cs:20` shows text only. `UI/DeathScreenContent.cs:26` has a `voiceLine` clip field, no clips | A voice-line player (clip + subtitle). The voices themselves (CONTENT) |
| §2.1 Three powers | BUILT | `PowersAndBarsTests.FullCharge_FiresBlast_FullDamage_FullCost`, `Fire_Ignites_OnlyAtFullCharge_WithAVisibleBurn`, `Telekinesis_SecondPress_ThrowsTheHeldEnemy_IntoTheOther` | - |
| §2.2 One drug (the ammo) per power | PARTLY | `Player/PowerDefinition.cs:8` (3 types), `Player/DrugInventory.cs:52` (any type works when the wanted one is empty), `Player/PsychicPowers.cs:353` (every drug calls `RefillAll`) | The three drugs do the same thing. No drug is ammo for its power. Bruno's one-stat rule is also open (section 5) |
| §2.3 TK grabs objects, enemies, bullets | BUILT | `Props_LiftAndThrow_TheCrateHurtsTheEnemy_Breaks_AndTheLockGoesBack`, `Telekinesis_CatchesABullet_AndSendsItBack`, `EnemyAiTests.Grenade_CaughtInTheAir_...`, `Rocket_CaughtInFlight_...` | - |
| §2.3 TK throws or smashes enemies | BUILT | `Telekinesis_Holds_UntilAFullChargeSlamsIt`, `RagdollTests.Throw_IntoAnother_BothFallLimp_ThenBothStandUp` | - |
| §2.3 TK rips floor chunks as a shield | NOT BUILT | No code found (searched: slab, chunk, floor shield). `devlog-notes/2026-10-20_adjustments1.md:107` "floor shield later" | Breakable floor pieces, an RMB-hold "defensive full charge", a shield that stops bullets, its HUD |
| §2.3 TK returns projectiles | BUILT | see the catch tests above | - |
| §2.4 Circle charge. She stands still | BUILT | `HalfCharge_HalfDamage_HalfCost`, `TankControlTests.Charge_StopsTheWalk_AndFacesTheTarget` | - |
| §2.5 Skewer at full charge on a low-HP target. Others may run | NOT BUILT | No code found (searched: skewer, impale, low hp in `Player/PsychicPowers.cs`). The "others run" half exists: gruesome deaths feed fear (`EnemyMoraleTests.GruesomeDeath_TheWitnessesSeeIt_AndAreShocked`) | The rule (which power, what HP threshold), the kill pose or gore, a "gruesome" flag on it |
| §2.6 Dodge | BUILT | `TankControlTests.Dodge_*` (6 tests) | - |
| §2.7 Three bars HP / MK / SHORT | BUILT | `Short_Rises_WithKills_AndDamageTaken`, `Hud_Warns_LowHp_LowNalcon_HighShort_WithIconsAndBeeps` | - |
| §2.8 The short | BUILT | `FullShort_StartsOnTheAttackPress_SlowWalk_AndADrugEndsIt`, `Short_EndsByItself_AtAboutHalfHp`, `ShortStart_FreezesTheWorld_2_5s_...` | - |
| §2.9 Scan a thing (image or clip) | BUILT | `InteractionTests.Scan_OnTheDoor_ShowsThePicture_...`, `Scan_OnTheMonitors_PlaysTheClipToItsEnd` | - |
| §2.9 Scan a person (Marta) | PARTLY | `Interaction/Scannable.cs:12` is a plain component. It can sit on a figure. No figure has one. Nothing raises a story flag on a scan (`Saving/StoryFlags.cs:8` "Nothing sets one yet") | A scan on a moving figure (focus on the chest), a flag after the scan, the clip with Marta's voice (CONTENT) |
| §3.1 Telegraph: laser + shrinking circle | BUILT | `DummyShooterTests.Shooter_Aims_Telegraphs_Fires_AndHitsLucy`, `Telegraph_ShowsAnEdgeArrow_...` | - |
| §3.2 Difficulty = shooters at a time | BUILT | `DummyShooterTests.Difficulty_LimitsHowManyAimAtOnce`, `EnemyAiTests.EasyDifficulty_OnlyOneSoldierAimsAtATime` | - |
| §3.3 Unarmed civilians | BUILT | `CivilianTests` (19 tests), the "civilians" bot | - |
| §3.3 Armed civilians (scientists) | NOT BUILT | `Enemy/EnemyMorale.cs:27` has the rank `ArmedCivilian`, but `Integration/Invector/Editor/InvectorEnemyFactory.cs:53-58` gives no class that rank. The lab worker is unarmed | A spec: lab worker with a pistol (shaky two-hand hold), fear limit 3, its barks. The knife scientist: see section 2 |
| §3.3 Guards | BUILT | `EnemyAiTests` (guard pistol and shield), `EnemyMoraleTests.Fear_TheGuardRetreatsAndCowers_...` | - |
| §3.3 QRF soldiers (shield, SMG, grenades) | BUILT | `EnemyAiTests.Rifle_FiresABurstOfThreeRounds_AsOneAttack`, `Shield_BlocksTheFrontBlast_...`, `Grenade_ArcsToHerFeet_...`, `Gas_*` | No SMG kind. The rifle burst stands in for it (`Enemy/DummyShooter.cs:30`) |
| §3.3 Power armour | BUILT (not in demo) | `EnemyAiTests.PowerArmour_*` | - |
| §3.3 The boss | NOT BUILT (not in demo) | Only the flag: `Combat/LockOnTargetable.cs` `IsShockImmune`, test `Short_PassesAShockImmuneTarget` | Everything |
| §3.4 Civilians cower, run, beg | BUILT | `CivilianTests.AKillInTheirSight_TheyRun_ThenCower`, `SheComesClose_TheyBeg_...` | - |
| §3.4 Guards try CPR | BUILT | `EnemyMoraleTests.Care_ACollegueDragsTheBodyAway_ThenDoesCpr_...`, `CivilianTests.TheDraggedBody_GoesLimp_...` | - |
| §3.4 A fire spawns firemen on her return | NOT BUILT | No code found (searched: fireman, sprinkler, alarm). The fireman model exists only (`Editor/Characters/CreateCharacterAssets.cs`, `CharacterTests.Catalog_HasTheNineClasses_...`) | See §11.3 below |
| §3.5 Death screen: picture, voice line, hint | PARTLY | `DeathScreenTests.KilledByTheRifleSoldier_...`, EditMode `DeathScreenContentTests` | Pictures are a grey card, the voice line is a caption only. The writer's ids `scientist`, `scientist_group`, `soldier_pair`, `fire`, `short` (`game/death_screens.csv`) do not match the code ids `<class>_<weapon>`. A group kill, a fire death and a short death have no code (the short never kills: `Short_EndsByItself_AtAboutHalfHp`) |
| §4.1 About 40 min, two floors | NOT BUILT | Scenes: `Main_Menu`, `TrainingRoom`, `Tutorial`, `LookTest`, `LookTest2`, three local test rooms. No demo room | The rooms (CONTENT) and room-to-room travel (section 2) |
| §4.2 Enemies up to normal soldiers | BUILT | see §3.3 | - |
| §4.3 Training room + tutorial as a separate mode | BUILT | `TutorialTests.APlayer_WhoDoesEachThing_CompletesEveryStepInOrder`, tutorial bot, 22 steps (`Tutorial/TutorialDirector.cs`) | "Part of the story, tbc": the gym is not a room of the story path |
| §4.4 Two outfits: stats | BUILT | `Player/OutfitProfile.cs`, `Editor/CreatePowerAssets.cs:46-55` (nude 100 HP, regen. Guard 150 HP, no regen, 8 slots), `PowersAndBarsTests.Nalcon_Recharges_Nude_NotGuard`, `InteractionTests.AFullInventory_..._AndTheGuardOutfitCarriesMore` | - |
| §4.4 Two outfits: a way to change in the game | NOT BUILT | `PsychicStats.SetOutfit` (`Player/PsychicStats.cs:54`) is called only by tests and by a save load (`Saving/SaveSystem.cs:247`). No model swap: `lucy_guard` exists in the catalog, nothing puts it on her | A uniform pickup or locker, the model swap, the HUD outfit name, a save of it (the save already keeps the id) |
| §5.1 Warspite port: menus, remap, difficulty, death screen, catch and throw | BUILT | `MenuLoopTests` (6 tests), `RagdollTests` | - |
| §5.2 Real-time rooms, fixed cameras | BUILT | `LookTestTests`, `TankControlTests.CameraZone_CutsToTheZoneCamera_AndBack`, `CameraCoverage` checks | - |
| §5.3 Auto-lock + switch | BUILT | `TankControlTests.LockOn_TakesTheNearest_SwitchesOnTab_DropsOnDeath` | - |
| §6.5 Steam, PC | NOT BUILT | No player build code found (searched: BuildPipeline, BuildPlayer in `Assets/_Game`, `tools/`). Debug keys F2, F5-F9, F11, F12 are not gated (`Debug/DebugCheatKeys.cs:43`). The main menu shows AI TEST, COVER TEST, CIVILIAN TEST (`UI/MainMenuUI.cs:423-425`) | A build script, a dev flag for the debug keys and the test rooms |
| §8.1 Masks on every class | BUILT | `CharacterTests.Catalog_HasTheNineClasses_...` | The ward sister (a white nurse) has no model (CONTENT) |
| §8.4 Helmet with a diegetic UI | PARTLY | The HUD is a greybox overlay (`UI/PlayerHud.cs`). No helmet lights found (searched: helmet, diegetic) | The three lights on the helmet model (CONCEPTS §8.5, PROPOSED) |
| §10.5 Names before anything public | PARTLY | MK and the power names are on screen. The drugs are still `Nalcon, Red, DFelon` (`Player/PowerDefinition.cs:8`) | Drug names (CONTENT, Bruno's pick) |
| §11.2 Experience levels | BUILT (2026-10-26) | `ExperienceTests`, EditMode `ExperienceRulesTests` | Numbers PROPOSED |
| §11.3 Firemen, sprinklers + fire alarm after an ignite. Farm | NOT BUILT | No code found (searched: fireman, firemen, sprinkler, alarm). `BurningStatus.Ignite` (`Player/PsychicPowers.cs:281`) is the only hook | An ignite event to a room flag, the alarm sound and the sprinkler effect, the return spawn of firemen (a civilian brain with an extinguisher, the care already works for any `ICarer`), a fire on objects (none: `Combat/BurningStatus.cs` burns figures only) |
| §11.4 The short shocks one enemy at a time | BUILT | `Short_ShocksOneEnemyAtATime_TheNearestFirst_ThenTheNext` | - |
| §11.5 Retreat by rank | BUILT | `EnemyMoraleTests.Fear_*` | Armed civilians: no class has the rank (see §3.3) |
| §11.6 Look Authentic / Clear | BUILT | `LookQualityTests` | - |
| §12.1 Scan 1.5 m, free | BUILT | `InteractionTests.Scan_ReachesOnlyCloseToTheObject` | - |
| §12.2 Inventory overlay. Key items take no slot | BUILT | `InteractionTests.TheInventory_StopsTheRoom_AndListsTheItems`, `AFullInventory_...` | - |
| §12.2 The injector | CONTENT | `Interaction/ItemDefinition.cs:16` (kind `Critical`). No injector asset, and the design does not say what it does | The item and its use (Bruno) |
| §12.3 Unlit pickups and throwables | BUILT | asserts in `InteractionTests.cs:264`, `PowersAndBarsTests.cs:256` | - |
| §12.4 mk_edgar (possession) | NOT BUILT (later) | No code found (searched: edgar, possess) | Everything. Not in the demo |
| §12.5-12.7 TK hold, Fire full-charge only, MK boost | BUILT | `Telekinesis_AnotherEnemysBullet_DropsTheFloater_Unhurt`, `Fire_PartCharge_FlashesTheShooter`, `MkBoost_AtHalfABarOrMore_...` | - |
| §12.10-12.11 Props, heavy targets, single use | BUILT | `Props_TheAutoLockSkipsThem_...`, `Telekinesis_RefusesAHeavyTarget_...` | - |
| §12.13 Weapons, melee, attack icons, telegraph by difficulty | BUILT | `EnemyAiTests.Melee_WhenSheIsClose_...`, `Hud_ShowsTheAttackIcon_OfEveryAim`, `Telegraph_FollowsTheDifficulty_Hard08_Easy15` | - |
| §12.14-12.15 Morale, care, gore, ragdoll, pools | BUILT | `EnemyMoraleTests`, `GoreTests`, `RagdollTests.FullBlastKill_CutsALimb_...` | - |
| §12.16 Breakable cover, broken room stays broken | BUILT | `BreakableTests` (12 tests), EditMode `Ledger_KeepsTheRoom_WhenSheLeaves_DropsItOnARestart`, "cover" bot | - |
| §12.17 Darkness is a look only | CONTENT | no mechanic by design | Per-room lighting |

## 2. `game/story_demo.md` (PROPOSED v2.1, on the round 8 CANON)

| Beat | Status | Evidence | What is missing |
|---|---|---|---|
| Wake in the recovery room, the slit opens | CONTENT | The recovery room exists as a look room (`Editor/LookTestSetup.cs`, `LookTest2.unity`) | Cutscene 1 (needs the cutscene player) |
| Start nearly maxed on SHORT | PARTLY | `PsychicStats.AddShort` / `DebugSet` exist (`Player/PsychicStats.cs:130`, `:193`). No room sets a start value | A per-room start state. Also a rule gap: in the story the first HIT starts the short. In the game a full bar waits for her next attack or defend press (`FullShort_Holds_UntilAShortOrADrug`). Bruno decides which |
| Stevens on the speaker: name, dizzy, no time | NOT BUILT | no voice player (section 1, §1.4) | A ceiling-speaker voice line with a subtitle `STEVENS:` |
| The map on the screen, the printed sheet | NOT BUILT | No code found (searched: map screen, MapUI, floor plan) | A map key item and a map screen (floor plans, the crossed-out section, you-are-here) |
| The scientist and the collar computer: ultimatum, twice, then the knife | NOT BUILT | No scripted sequence found (searched: computer, sequence. `OnTriggerEnter` only in `Level/PlayerDeathTestVolume.cs`) | A scripted-event runner (wait, say a line, wait, line, then start the fight), a knife enemy (melee-only, it closes in). Melee with a telegraph exists (`EnemyAiTests.Melee_WhenSheIsClose_...`) but every enemy carries a gun |
| Three scientists outside with pistols. The short kills all four | PARTLY | The short and the one-victim shocks are built. Armed civilians are not (section 1, §3.3) | Armed lab workers. Story says "four seconds". The short is 16 s (`Player/ShortController.cs:35`) |
| The ampoules roll. One stops the shaking | CONTENT | Drug pickups built (`DrugPickup_AsksFirst_...`) | Ampoule props, a physics roll |
| The patient: "is mine done?" | PARTLY | Patient model exists. `CivilianBrain` would make her run | A passive NPC (stays in bed, a line when she comes close) |
| Two guards. The second does CPR and does not look up | PARTLY | Care is built, but it stops when the carer sees her (`EnemyMoraleTests.Care_..._AndStopsWhenHeSeesHer`) and starts only after 3 s unseen | A scripted care that ignores her. A "walk past or not" choice |
| Code silver on the PA, twice | NOT BUILT | No code found (searched: code silver, announce, intercom, public address) | A PA bus (chime, voice, subtitle `PA:`), a building-wide alert flag |
| The QRF is activated. Soldiers appear after | NOT BUILT | Alerts are per enemy within 25 m (`Enemy/EnemyAlert.cs`). `StoryFlags` exists, nothing reads it in a room | Flag-gated spawns and objects |
| The gym demonstration on the PA (tutorial) | PARTLY | Tutorial built (22 steps). Lines are placeholders (`Tutorial/TutorialDirector.cs:28`). The writer has `game/cutscenes/04_the_demonstration.md` | The writer's lines, the presenter's voice, a decision: story room or separate mode |
| Marta: hands up, her lines, the scan, the player's choice, "the game remembers" | PARTLY | Nurse civilian built (begs, `CivilianTests`). `StoryFlags` saved (`Saving/SaveData.cs:26`) | Scan on a person, a flag on her fate (spared, killed), anything that reads it later |
| Stairwell locked. Scan shows the card | CONTENT | `InteractionTests.TheLockedDoor_SaysItIsLocked_AndOpensWithTheKey`, scan on a door built | The card item and picture |
| The ward sister carries the card (a pistol she never fired) | NOT BUILT | No item drop on death found (searched: loot, drop, carried item) | Armed civilian class, a key item that drops or is taken from her |
| Mapping lab: scan the chair, the whiteboard, the dictaphone | PARTLY | Scan of a thing built. Logs are text only (`Interaction/ItemDefinition.cs:34`) | An audio log (the dictaphone), CONTENT for the rest |
| The window: the van in the yard | CONTENT | - | A cutscene or a set piece |
| Stairs down, the transition | NOT BUILT | `GameManager.LoadLevel` (`Systems/GameManager.cs:206`) is called only by menus, saves and debug keys | Room-to-room travel (section 6, rank 1) |
| The gallery with the gear panels | CONTENT | `LookTest.unity` is the Gallery | Make it a game room (enemies, NavMesh, bake, exits) |
| The cherry tree room, scan the wall | CONTENT | scan built | Room, painted wall art, the scan picture |
| Intake room: the CRT plays the orientation program | PARTLY | A clip plays full screen only through the scan (`Scan_OnTheMonitors_PlaysTheClipToItsEnd`) | An in-world screen that plays a clip with sound on the CRT |
| Response team in pairs, they say names | CONTENT | Barks exist as text (`Enemy/Bark.cs`, lines in the brain code) | The writer's lines in the code, voices |
| Armed scientist: "you are not supposed to be able to do that" | NOT BUILT | see armed civilians | - |
| Fire in the records room. Firemen on her return | NOT BUILT | see §11.3 | - |
| Security office: monitors, the tape | PARTLY | as the CRT row | In-world video |
| The unfinished wing. The v4. Stevens' last line. Black | PARTLY | `GameManager.CompleteLevel` (`Systems/GameManager.cs:170`), `UI/LevelCompleteUI.cs`, `UI/CreditsUI.cs` exist. Only the tutorial calls it | An end trigger, cutscene 10, the end card |

## 3. `game/CONCEPTS.md` (PROPOSED)

| Item | Status | Evidence | What is missing |
|---|---|---|---|
| §2.4 HP heals by itself in the bare body | BUILT | `Player/PsychicStats.cs:86` (1 HP/s nude) | Rate OPEN (section 5) |
| §2.5 Seams are vents. Guard uniform covers them | PARTLY | stats per outfit built | The in-game change (section 1, §4.4) |
| §2.7 / §8.5 Three helmet lights, the Crown HUD | NOT BUILT | no code found (searched: helmet, diegetic) | Lights on the model. A HUD drawn as the helmet readout |
| §3 Ward sister (white nurse) | CONTENT | no class in the catalog | A white recolour of the nurse |
| §3 Fireman | PARTLY | Model only | AI and the fire loop (section 1, §11.3) |
| §4 Names (NERVE, ampoules, the Crown) | CONTENT | The bar is MK now (design §12.9). CONCEPTS §4 still says NERVE | The writer updates the table. Drug names |
| §5 Floor 2: 9 rooms, floor 1: 9 rooms, about 25 cameras | NOT BUILT | Only the gym (Tutorial), the recovery room and the skylight corridor (LookTest2) and the Gallery (LookTest) exist, as look or tutorial rooms | 18 game rooms with NavMesh, bake, cameras, exits (CONTENT on top of the travel system) |
| §5 Linen store / records room: "a fire can start here" | NOT BUILT | no fire on objects | See §11.3 |
| §5 Pharmacy: ampoules | CONTENT | drug pickups built | - |
| §5 Lobby: the gate sealed by code silver | NOT BUILT | `Interaction/LockedDoor.cs:13` opens with a key only | A door locked by a story flag |
| §6 Ten cutscenes | NOT BUILT | No player. The writer has 4 scripts (`game/cutscenes/01..04`) | The player (section 1, §1.3), 6 more scripts, stills, voices |
| §6 Death screens and barks from the CSVs | PARTLY | `Editor/DeathScreenSetup.cs:25` reads `death_screens.csv`. The barks are typed into the brain code, not read from `barks.csv` | Id match (section 1, §3.5) |
| §7 The look | BUILT | design §11.6 | - |
| §8.7 Power armour weak point at the chest visor | NOT BUILT (not in demo) | no hit-zone rule found | - |

## 4. `01_SERIES_BIBLE.md` (game-relevant parts only)

| Item | Status | Evidence | What is missing |
|---|---|---|---|
| The Memorial, Lancaster County (§9) | CONTENT | - | Room dressing |
| Stevens, head of security, a voice only (§10) | NOT BUILT | no voice player | The voice (bible §12: Stevens needs the clone route, RFab gap 1-3), the speaker lines |
| Dr. Church, v1, a voice from a speaker (§10) | CONTENT | - | Dictaphone and tape audio (needs the audio log and in-world video) |
| Kyoko on the public tapes (§10) | CONTENT | - | The orientation program clip |
| The Ward and the Gallery, the two floors (§9) | NOT BUILT | see CONCEPTS §5 | - |
| No-lip-sync rules (§8.2) | CONTENT | masks make it moot in game | - |
| Sound language (§8.3) | NOT BUILT | `Systems/SettingsManager.cs:39` has a music volume, no music or ambience player found (searched: music, ambience) | A music and room-tone player. The sound source is OPEN (RFab gap 14) |

## 5. `memory/gb-bruno-roadmap.md` (his "later" and "tbc")

| # | Item | Status | Evidence | What is missing |
|---|---|---|---|---|
| 1 | Difficulty in percentages (easy 100, normal 150, hard 200, custom) | NOT BUILT | `Editor/CreateDifficultyModules.cs:34-44` gives the presets -25 / 0 / +25 percent labels only. `DifficultyManager.TotalDifficultyPercent` (`Systems/DifficultyManager.cs:35`) is read only by two menus. The modifiers Fragile, OneHitWonder, Scarcity (`Systems/DifficultyModuleDefinition.cs:20-22`) have NO gameplay consumer: `GetModuleParam` has no caller | A scale on enemy damage, HP and the telegraph. A custom panel. Wire or remove the three modifiers (a player can toggle them today and nothing happens) |
| 2 | Drugs, one stat each (tbc) | NOT BUILT | `Player/PsychicPowers.cs:353` (`RefillAll` for every drug) | Bruno's ruling and a design change (§2.8) first. Then about 1 h |
| 3 | HP passive recharge (nude 1/s) | BUILT, rate OPEN | `Player/PsychicStats.cs:86`, `Editor/CreatePowerAssets.cs:50` | His number |
| 4 | Second wind pill | NOT BUILT | `Core/Health.cs:130` `RearmSecondWind` exists, no caller | An item kind or a drug type that calls it |
| 5 | TK defensive forms (floor slab). BLAST and FIRE defensive forms | NOT BUILT | see design §2.3 | Slab shield. BLAST/FIRE forms are OPEN |
| 6 | The short: 16 s, slow walk, 12 per shock | BUILT | `FullShort_StartsOnTheAttackPress_SlowWalk_...` | - |
| 7 | Move or shoot, never both | BUILT | `EnemyAiTests.Soldiers_SeeLucy_MoveToASpot_StopAndShoot_NeverBoth`, "ai" bot | - |
| 8 | Props | BUILT | `Props_*` tests | Extinguisher prop (firemen) |
| 9 | Scan and logs | BUILT | `InteractionTests` | - |
| 10 | Heights | BUILT | `CharacterTests.Catalog_HasTheNineClasses_AsHumanoids_AtTheirHeights_FacingForward` | - |
| 12 | Exp levels. Firemen farm | BUILT / NOT BUILT | Exp: `ExperienceTests`. Firemen: no code | Firemen. Note: on 2026-10-21 Bruno said the fireman "is not in the first pass of the demo" (`devlog-notes/2026-10-21_characters.md:98-110`), but design §3.4 and §11.3 and CONCEPTS §5 put firemen in the demo. Ask him |
| 13-24 | Short on one, retreat, interact/scan, inventory, names, combat block, props, weapons, gas, gore, ragdoll, cover, test feedback | BUILT | see section 1 | - |
| 25 | The rifle floats during the CPR (sling it) | NOT BUILT (FOR LATER) | `devlog-notes/2026-10-24_retest.md:12` | 2-3 h |
| 26 | Long-gun hold: right arm high, left arm loose | NOT BUILT (FOR LATER, "let it be") | roadmap item 26 | Polish |
| 27 | The ward slice (story_demo first 10 min) | NOT BUILT | his pick is still open | This is the natural first production target |
| 28 | Replace the placeholder fire and shock effects | NOT BUILT (FOR LATER) | `devlog-notes/2026-10-25_civilians2_sounds_short.md` "For later". `Combat/BurningStatus.cs:8` | Art and sound |
| 30-31 | The scream is synthetic. Keep until the final SFX/voice pass | CONTENT | `Resources/Audio/lucy_scream.wav` | The SFX source (RFab gap 14) |
| 32-33 | Saves, checkpoints, subtitles, the dead and the gore in the save | BUILT | `SaveTests`, `SaveGoreTests`, `SubtitleTests` | Only one room per save (see section 6, rank 1) |
| 33 | A loaded body is never tended again. A loaded dropped shield is kinematic | DECIDED | Bruno, 2026-10-26: "keep it" (both) | - |

## 6. FOR LATER and OPEN lines in the repo's `CLAUDE.md` and `devlog-notes/`

| Line | Status | Source | Note |
|---|---|---|---|
| OPEN: drugs one stat, difficulty percentages, HP recharge | NOT BUILT / OPEN | `CLAUDE.md` State, adjustments. `devlog-notes/2026-10-20_adjustments2.md:63-78` | See section 5 |
| Later: Invector FSM AI, props, scan, logs | BUILT | `CLAUDE.md` | - |
| OPEN: the darkness level, the texture scale | OPEN | `CLAUDE.md` (look pick) | Needs his call before rooms are lit |
| FOR LATER: MK names, TK hold, Fire stun, MK boost | BUILT | `devlog-notes/2026-10-22_combat_block.md` | - |
| FOR LATER: gun floats during the CPR | NOT BUILT | `devlog-notes/2026-10-24_retest.md:12` | - |
| OPEN: the sound source (RFab gap 14) | OPEN | `devlog-notes/2026-10-24_breakable_cover.md`, `RFAB_GAPS.md` row 14 | Blocks the final SFX |
| FOR LATER: replace placeholder fire and shock effects | NOT BUILT | `devlog-notes/2026-10-25_civilians2_sounds_short.md` | - |
| BLAST and FIRE have no defensive form (OPEN) | OPEN | `devlog-notes/2026-10-20_adjustments1.md:107-108` | - |
| OPEN: the dodge cooldown 0.8 s | OPEN | `devlog-notes/2026-10-21_dodge_input.md:53` | Tuning |
| OPEN numbers: alert 25 m, hearing 12 m, soldier 80 / guard 60 / armour 250 HP, firing spot 6 m | OPEN (PROPOSED numbers in place) | `devlog-notes/2026-10-23_fsm_ai.md:116-120`, `2026-10-24_night.md:94` | Tuning on real rooms |
| OPEN: the 0.6 s freeze / white flash | Superseded (2.5 s freeze built) | `devlog-notes/2026-10-25_poses_short_pain.md:83` | - |
| Public repo: rooms with Invector enemies hold vendor data | PARTLY | `.gitignore:138-141` (AiTest local only), CLAUDE.md (CoverTest, CivilianTest local only) | Every demo room with enemies has the same problem. Decide: rooms git-ignored and built from code, or enemies spawned at run time |

---

## What the demo needs before content production (ranked)

Size guesses are hours of build work plus tests, at the pace of the devlog notes. They do not include rooms, art,
lines or voices. Items 1 to 5 block almost every room of the story. Items marked (Bruno) wait for his ruling.

| Rank | What | Why it blocks | Size |
|---|---|---|---|
| 1 | **Room-to-room travel and the world state.** An exit (door, stairs) that loads the next room and puts her at the matching entry, a fade or a load screen, and a session record per visited room: the dead, the taken items, the open doors (today only the breakables persist across rooms, `Destruction/RoomDamageLedger.cs:14`). The save keeps every visited room, not one (`Saving/SaveData.cs:23` holds one `RoomState`) | Two floors, 18 rooms, the firemen "on her return", "broken room stays broken" for the dead too | 12-20 h |
| 2 | **Scripted events and story flags.** A trigger volume, a small sequencer (wait, say a line, start a fight, open or lock a door, enable a group, set the bars), objects and spawns gated by a flag, a door locked by a flag. `StoryFlags` exists and nothing raises or reads it | The scientist ultimatum, the start near a full SHORT, code silver, the QRF wave, the sealed gate, Marta's choice, the ending trigger | 12-16 h |
| 3 | **Voice lines and the PA.** Play a clip with its subtitle from a figure, a collar, a ceiling speaker or the whole building (chime). Ducking under combat sound | Stevens, the computer, code silver, the presenter, Marta, the death screen voice | 4-6 h |
| 4 | **The cutscene player.** Stills with pans, voice clips, one video cut, subtitles (the Timeline track exists), a skip key, a Cutscene game state | 10 cutscenes in CONCEPTS §6 | 8-12 h |
| 5 | **The rooms policy and a room kit.** Decide how a room with Invector enemies lives in a public repo. Turn the look-room builders into a repeatable kit: a room = builder + NavMesh + bake + cameras + coverage test + exits + checkpoint | Every room. Today each room is a hand-written builder | 6-10 h |
| 6 | **The missing enemy kinds.** A knife scientist (melee only, closes in, the telegraph), an armed lab worker (pistol, `ArmedCivilian` rank, fear 3), a key item carried by a figure (the ward sister's card) | Beat 1, the corridor, the mapping lab, intake room B | 8-12 h |
| 7 | **The outfit change in the game.** A uniform pickup or locker, the swap to the `lucy_guard` model, the stats, the HUD name | Design §4.4 CANON. No way to reach the guard outfit today | 4-6 h |
| 8 | **The map.** Stevens' sheet as a key item, a map screen with both floors, you-are-here, the crossed-out section | The story's goal is on the map | 6-10 h |
| 9 | **In-world screens and audio logs.** A CRT that plays a clip with sound when she enters or presses E. A dictaphone log with audio | Intake room A, the security office, the mapping lab | 4-6 h |
| 10 | **Passive NPCs and the scan on a person.** A figure that stays (the patient in bed), says a line when she comes close. A `Scannable` on a figure that raises a flag. Marta's fate as a flag | Patient room, nurses' station | 4-6 h |
| 11 | **Fire in the room, the alarm, the sprinklers, the firemen** (Bruno: in the demo or not?) | Linen store, records room, the "fire" death screen, the exp farm | 16-24 h |
| 12 | **The difficulty percentages** (Bruno), and wire or remove the three dead modifiers | The player can toggle modifiers that do nothing | 4-8 h |
| 13 | **One-stat drugs** (Bruno. Design §2.8 changes first), the second wind pill | Pharmacy content, balance | 2-3 h |
| 14 | **The skewer** (Bruno: which power, which HP threshold, which gore) | Design §2.5 CANON | 4-6 h + art |
| 15 | **The floor-chunk shield** (needs breakable floor pieces. The BoxFracture kit can cut them) | Design §2.3 CANON. The power armour is not in the demo, so it can wait | 8-12 h |
| 16 | **A player build.** A build script, a dev flag for the debug keys and the three test-room buttons | A 40-minute demo someone else plays | 4-6 h |
| 17 | **Music and room tone player** (the source is OPEN, RFab gap 14) | Horror needs sound | 3-4 h |
| 18 | Small rule calls for the story (Bruno): the first hit starts the short (story) or the next press does (game). The short is 16 s (story says 4 s). The beat-two guard does CPR in her sight. The tutorial in the story path or a separate mode. The death screen ids of the CSV | Beat 1 and 2 play wrong without them | 1-3 h after the calls |
| 19 | Polish FOR LATER: gun sling during the CPR, the long-gun arms, real fire and shock effects, the helmet lights | Not blocking | 6-10 h |

Total before content: ranks 1-10 and 16 are about 72-110 hours. With 11 to 15 it is about 106-163 hours.
