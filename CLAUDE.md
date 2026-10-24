# THE GLORIFIED BODY (the game): read first

A tank-control horror game in Unity 6, a refinement of Galerians (PS1, 1999). Lucy, a young adult woman with no
memory and three psychic powers, escapes the cult's lab. Started 2026-10-20 (the pivot from the video series).

- **Design (CANON):** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/03_GAME_DESIGN.md`.
- **World and story:** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/01_SERIES_BIBLE.md`.
- **Art rules, read before any art work:** `ART_LESSONS.md` (this folder).
- **Asset lessons, full detail:** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/tests/T15_3d_set/LESSONS_LEARNED.md`.
- **Agents:** `glorified-body-game` builds the game, `glorified-body-writer` writes the story, the cutscenes, the
  barks and the devlog scripts. Bruno decides.

## State

- 2026-10-20: steps 1, 2 and 3 done. Step 1: the project at the repo root (public GitHub repo, GPL-3.0), the
  menu loop and the batch harness. Step 2: tank controls with a dodge, fixed cameras per room, auto-lock with a
  target switch. Step 3: the circle charge with BLAST, FIRE and TELEKINESIS, the HP / NALCON / SHORT bars, the
  short, the drugs, a capsule dummy shooter with the canon telegraph, the greybox HUD.
  Proof: 34 EditMode tests, 59 PlayMode tests, the structural bot (24 steps) and the look bot (19 steps), see
  `devlog-notes/`.
  The step 1 kill volume (the dark box at the east wall) is out of the room since 2026-10-21: Bruno walked into
  it and died (`devlog-notes/2026-10-21_kill_volume.md`). The bot and the tests kill through `Health`.
- 2026-10-21, the cast in the engine (`devlog-notes/2026-10-21_characters.md`): the nine T19 models are in the
  room. `tools/characters.sh` (Blender, GLB to FBX plus a 1024 base colour, the figure turned to +z) then
  `CreateCharacterAssets.BuildAll` (Humanoid import, matte URP materials, the Lucy and Enemy animator
  controllers with Invector clips as placeholder poses, prefabs, the catalog in `Resources/Characters/`). Lucy
  (lucy_bare, 1.70 m) stands on her root with a 1.7 m controller, the dummies wear the guard and the soldier, the
  other six stand along the north wall. Body points in `Characters.Body` (chest 1.2, muzzle 1.3). The T19 walk
  and run clips travel in the source: `tools/characters_clips.py` measures each clip into `<class>.clips.json`,
  the import reads it and gives the travel to the root motion (not applied), so every clip plays in place
  (Bruno's morning check: "her model slides back"). A new class needs its measurement file, or the EditMode
  test `CharacterImportTests` fails.
- Adjustments after Bruno's two plays (2026-10-20, `devlog-notes/2026-10-20_adjustments1.md` and `_adjustments2.md`):
  the dodge is directional (Space plus a direction key, or a double tap). The press is forgiving (Bruno,
  2026-10-21, `devlog-notes/2026-10-21_dodge_input.md`): a press inside the cooldown waits 0.5 s and fires when
  the dodge is ready, a direction key up to 0.1 s after Space aims the dash, the key pressed last wins, the
  double tap window is 0.4 s. Debug keys F5 to F9: fill the SHORT
  bar, start the short, refill her, nalcon -25 / +25. A full SHORT bar holds until a short or a drug (CANON), and
  with a full bar the attack or the defend press starts the short at once. The short lasts 16 s, she walks slowly
  (no run, no dodge), same damage per shock, half her HP. The telegraph circle is on the HUD over her, with an
  edge arrow for a shooter outside the frame, and `Cam_SouthWest` looks along the south wall so the arrow shows
  in the room. Telekinesis: a held enemy turns with her, a second press throws it where she faces (both hurt),
  and RMB catches a bullet near her and sends it back (props and the floor shield later). FIRE ignites only at a
  full charge, a part charge is a flash that breaks the aim. The HUD warns on low HP, low nalcon and high SHORT
  (icon, "!", word, three beeps once). The second wind: the first lethal hit leaves her at 1 HP, invulnerable
  2.5 s, once. Nude outfit nalcon recharge 2 per second.
  OPEN for Bruno: drugs with one stat each (tbc), difficulty as percentages (easy 100, normal 150, hard 200,
  custom), the HP recharge rate. Later (Bruno, 2026-10-21): the Invector FSM AI with "move or shoot, never
  both", props from free assets or Invector's placeholder weapons, the psychic scan (a clue from an object), the
  logs (grab and read).
- 2026-10-22, step 4 built, the look test (`devlog-notes/2026-10-22_look_test.md`): `LookTest.unity` is the
  Gallery of floor 1 (16 x 8 m concrete, planters, columns, conduits, the steel door, the gear panels, hard
  spots and wall washers, ambient near black), Lucy, a soldier and a guard, three fixed cameras with zones.
  `LookTestSetup.BuildLookTest` makes the renderers (GB_Full: a character stencil mark plus the 32-level Bayer
  dither on the background, GB_Expensive: plus ambient occlusion, GB_Plate: the plate blit plus the room as
  depth only), the Simple Lit materials, the texture imports and the scene. `LookDirector` (F3 cycles since the
  morning, a label at the top left) swaps greybox / real time / plate: the plate is the room rendered once per camera into a
  720p texture, the characters drawn over it (`PlateBlitFeature`, Render Graph). The URP assets carry the look
  for the whole game: render scale 2/3 (720p of 1080p), linear upscale, hard shadows only, per-pixel spots.
  Textures: `tools/look_textures.sh` (RFab Nano Banana, 1024 px, mirror wrap) with
  `tools/look_textures_procedural.py` as the fallback (RFab gap 11: the default model draws scenes). F11 in the
  training room loads the look test room. Shots for Bruno's pick: `~/glorified-body-renders/game/look_test/`.
- 2026-10-22, the second look test (`devlog-notes/2026-10-22_look_test2.md`), after Bruno's morning rulings
  (F10 is the editor's menu key, 2K textures and clarity, the relief painted in the texture and subtle greebles
  on concrete, a camera for every section): `LookTest2.unity` is the skylight corridor of T12 GALLERY_v3 plus
  the recovery room and the therapy gym of T18, with seven cameras; the Gallery got a fourth camera
  (Cam_Corner), skirting, a steel band, grilles and a junction box. `LookTestSetup.BuildAll` builds both,
  `BakeAll` bakes the bounce light (lightmaps indirect only, probes; the spots stay live and hard). Four
  dressings on F3: Greybox, RealTime (no bounce, the ruling), RealTimeBaked, Plate (bounce, ambient occlusion).
  F11 cycles the look rooms. The textures are 2K from Nano Banana Pro (`--res 2k`, $0.32 each, 66 MB in git),
  trilinear, aniso 8. The shadow atlas is 4096 (eleven spots in 2048 were halved). The PlayMode coverage test
  (every floor point in the frame of its zone's camera) runs on both rooms.
- 2026-10-22, THE PICK (`devlog-notes/2026-10-22_look_pick.md`): "real time with the baked bounce looks the
  best; the pure plates lose char shadows and aren't that much better image-wise". So: every room is lit live
  by hard spots with the bounce baked (`LookTestSetup.BakeAll` per look scene; a game room needs its bake
  too), the plate stays in the look test as a reference. The frame is 1080p (`LookDirector.LookRenderScale`
  1). The LOOK option (Options > Display, `SettingsManager.LookQuality`, `LookQualityApplier` on the gameplay
  systems): Authentic (GB_Full: the dither, no anti-aliasing) or Clear (the stock renderer, MSAA 4x, SMAA).
  Lucy is on the Player layer and every figure on the Enemy layer in every room (the stencil mark). Skylight
  shafts point straight down, the ferns stay crossed cutout cards. Bruno's rule: "we want the vibes of the
  look, but we don't need to be clamped by the same tech limitations; we can cheat".
  OPEN for Bruno: the darkness level, the texture scale.
- 2026-10-22, step 5, the tutorial mode (`devlog-notes/2026-10-22_tutorial.md`; Bruno's order: "tutorial
  mode, a test log, psychic scan (one test image, one test video), props, fsm ai"; his rulings of the day in
  `03_GAME_DESIGN.md` §11: exp levels with civilians worth more, the firemen farm, the short shocks one enemy
  at a time, retreat by rank). `Tutorial.unity` (`TutorialSetup.BuildTutorial`, then `BakeTutorial`) is the
  therapy gym (the shared `LookTestSetup.BuildTherapyGym`) with four padded targets (`Target_1..3`,
  `Target_Gun` with a DummyShooter off until its step), a capsule, three cameras. `TutorialDirector` runs
  eleven steps on the player's own actions (walk, run, dodge, switch, blast, fire, lift_throw, catch, capsule,
  short, complete), `TutorialPromptUI` shows the presenter's placeholder line and the hint with the bound
  keys, F2 skips. The TUTORIAL button in the main menu. `TutorialDrive` plays it through the virtual pad for
  the test and the bot (`play Tutorial Main_Menu tutorial 400`). `CameraCoverage` is the shared coverage
  check. The later features add a step each.
- 2026-10-22, the Interact and Scan keys (`devlog-notes/2026-10-22_interact_scan.md`): E interacts (an item asks
  "Take it?" first: E takes, S leaves; a log opens for reading), F scans (a picture or a clip fills the screen),
  separate keys (Bruno). `ItemInventory` with slots from the outfit (nude 4, guard 8, PROPOSED), `Interactor`,
  `PsychicScanner`, `Scannable`, `ItemPickup`, `ItemDefinition` (`Scripts/Interaction/`), `InteractionUI` on
  every room. Placeholders: the test log (`Resources/Items/item_test_log.asset`), the scan picture and clip
  (`Assets/_Game/Scan/`, ffmpeg cards, the clip VP8 webm because the Linux editor has no H.264). The tutorial
  has 14 steps (log, scan_image, scan_video added). The look rooms' cameras follow the game-room coverage rule
  (1.2 m near skip) now. Bruno's words on the presenter's lines: refine them after the scan step.
- 2026-10-22, night, the inventory (`devlog-notes/2026-10-22_inventory.md`): I opens an overlay that pauses the
  game (`GameState.Inventory`, only its keys pass `GameManager.AllowsAction`), after the Galerians item screen
  (`InventoryUI`). Key items (keys, logs, the injector) take no slot; grabbables (objects, every drug) take one
  each; `DrugInventory` draws on the item slots. `DrugPickup` is gone: pills are `ItemPickup`s that ask first.
  `Interactable` is the base of `ItemPickup` and `LockedDoor` (a message without the key, a question with it).
  The tutorial gym has a test key, a locked door with a closet (a red pill, its own camera). Pickups are unlit
  (`ItemAssets`, after Resident Evil 4). The scan reaches 1.5 m. Tutorial: 17 steps. FOR LATER (§12 of the
  design): nalcon becomes MK, the power names mk_warspite / mk_erebus / mk_orzabal / mk_edgar, Telekinesis holds
  until thrown, slammed or hit, Fire damages only at a full charge, MK at 50 percent or more doubles damage and
  charge speed.
- 2026-10-22, late, the combat block (`devlog-notes/2026-10-22_combat_block.md`): Telekinesis holds with no
  timer until a short press throws it, a full charge on it slams it (`PsychicPowers.TrySlam`, full cost, full
  damage), or another enemy's hit drops it unhurt (`TelekinesisLift.Release`, the enemy bullet hook, the hit
  bus). Fire hurts only at a full charge; a part charge only stuns. The MK boost: nalcon at 50 percent or more
  doubles damage and charge speed (`PsychicPowers.IsBoosted`, the HUD tick and "x2"). The inventory asks
  before it uses a drug. Tutorial: 18 steps (a slam step), targets 400 HP. Tests hold nalcon at 45 with the
  regen paused for the base numbers.
- 2026-10-22, late, the names and the props (`devlog-notes/2026-10-22_props.md`): on screen the bar is MK and
  the powers are mk_warspite, mk_erebus, mk_orzabal (display strings only; the code keeps Blast, Fire,
  Telekinesis, Nalcon; the drug NALCON keeps its name). Props: `LockOnTargetable.IsProp` / `IsHeavy`. The
  auto-lock never picks a prop; the target switch reaches props only while mk_orzabal is selected
  (`LockOnController.IncludeProps`, set by `PsychicPowers`); a thrown prop hurts the enemy it hits and the lock
  goes back to an enemy; a heavy target refuses the lift at no cost ("TOO HEAVY" on the HUD). Crates
  (`PropAssets.Crate`, unlit, `tools/prop_crate_texture.py`): two in the training room, one in the gym.
  Tutorial: 19 steps (a prop step). The tutorial drive walks to the door through the lane west of the bars.
- 2026-10-23, the short on one enemy (`devlog-notes/2026-10-23_short_one_enemy.md`): every shock of the short
  hits one victim, the nearest live enemy in the radius, until it dies (design §11.4, after Bruno's report that
  all enemies took equal damage). `LockOnTargetable.IsShockImmune` for the bosses (heavy and shock immune; the
  power armour is only heavy). A thrown or slammed prop breaks (single use, `PropPiece`).
- 2026-10-23, the FSM AI (`devlog-notes/2026-10-23_fsm_ai.md`): Invector's AI body (`vControlAI`, NavMeshAgent,
  root motion, the vendor shooter animator) on the T19 models, built by `InvectorEnemyFactory` (editor code in
  `Scripts/Integration/Invector/Editor/`), driven by our FSM asset `Assets/_Game/AI/FSM_GB_Soldier.asset` (Idle,
  Engage, Search; it opens in Invector's FSM window). The Engage state runs the custom action "Move Or Shoot"
  (`GbEngageAction`), which hands the fight to `SoldierBrain` (`Scripts/Enemy/`, behind `IEnemyBody`): walk to a
  firing spot, stop, aim with the `DummyShooter` telegraph gun (hold-fire gate, line of sight), fire, walk again,
  never both. `InvectorEnemyBody` bridges our Health (the real one), the lift (suspend), the stagger, the death
  (Invector's `removeComponentsAfterDie` must stay OFF: it destroys every MonoBehaviour), near awareness 6 m,
  the brace during the aim. `AiTest.unity` (`AiTestSetup.BuildAiTest`, the AI TEST button): three soldiers,
  the power armour heavy. Proof: `EnemyAiTests`, the "ai" bot (`play AiTest Main_Menu ai 300`).
  Midday (Bruno: guns, effects, sounds, "the enemies weren't this dumb"): `EnemyGun` (the vendor `AI_Handgun`
  stripped of scripts, at the right hand every frame, turned by its muzzle; flash, smoke, light, fire and aim
  sounds), `ShotEffects` (`Resources/Effects/shot_effects.asset`: clips, `impactConcrete`), a tracer trail and a
  wall impact on every enemy bullet. Awareness: `EnemyAlert` and `SoldierBrain.Alert` (sight, hurt, a power hit
  within 12 m, a shot or an alert within 25 m); alerted soldiers keep a fixed Invector target (Warspite's rule)
  and pick firing spots with a clear line. FSM: Idle <-> Engage on `GbAlertedDecision`.
  Bruno's test: 7 of 7 checks passed.
- 2026-10-24, the weapons (`devlog-notes/2026-10-24_weapons.md`): `DummyShooter` attack kinds (`Weapon`, `Attack`):
  pistol shot, rifle burst (3 rounds), frag grenade (`EnemyGrenade`, a red floor ring), gas grenade (a green ring,
  `GasCloud` poison, the masked enemies immune), rocket (`EnemyRocket`), melee within 1.8 m. Every kind takes a
  slot and the telegraph. The time from the laser to the attack by difficulty (presets' `attackSeconds` 1.5 / 1.0
  / 0.8, `restSeconds` 2). `EnemyShield` (`IFrontGuard`): the front stops mk_warspite, mk_erebus and returned
  bullets ("BLOCKED"), the lift tears it away. mk_orzabal's catch takes a rocket (25 MK, it steers back), a
  grenade (15 MK), a bullet, in that order. HUD attack icons next to the circle and on the edge arrows. Alerted
  soldiers run or sprint, never walk. The lock marker floats over the target's meshes (the 2.6 m armour hid it).
  AI TEST: `Soldier_A` rifle, `Guard_B` shield + pistol, `Armour_C` frag + rockets, `Gasser_D` gas.
  `Assets/_Game/Effects/GasCloud_Smoke.prefab` is vendor data: git-ignored, the factory makes it.
- 2026-10-24, night (`devlog-notes/2026-10-24_night.md`): `EnemyMorale` (§11.4 shock with `Bark` lines, §11.5
  fear by rank: guards retreat and cower at 3, soldiers and heavies never), `CorpseCare` (Bruno: CPR and the drag
  of a body; a colleague tends a body when he has not seen her for 3 s; placeholder poses, RFab gap 13),
  `GoreBootstrap` (the GoreManager and the That Individual driver in every gameplay room, their debug keys
  removed), `GruesomeDeathFx` (a gruesome kill bursts the head, `HeadStump`; only figures with an animator bleed),
  the Gore option (Off / Low / Full). Tutorial: 21 steps (grenade: leave the red ring; shield: flank `Target_2`).
  Next: Bruno's review.

## Rules

1. Unity 6000.3.15f1 (installed at `/home/pc/Unity/Hub/Editor/6000.3.15f1`). Start it only through `tools/unity.sh`
   (RAM gate, one batch slot shared with Warspite, run folders in `runs/`). One batch Unity at a time. Close the
   editor before a batch run.
2. `.megaignore` keeps Library, Temp, Logs, UserSettings, builds, `runs/` and the vendor folders out of MEGA. Keep
   it. Renders and captures go in `~/glorified-body-renders/game/`.
3. Keyboard and mouse only for now. Render pipeline: URP. Input: the legacy Input Manager with the KeyCode remapper.
4. Packages (Bruno, 2026-10-20): Invector Shooter + FSM AI templates and Gore System URP (That Individual), imported
   from `~/.local/share/unity3d/Asset Store-5.x/` with `tools/unity.sh import`, plus Warspite2's gore and Invector
   bridge folders. PUBLIC REPO: the paid packages and `MiddlewareForInvectorTemplate` never enter git (see
   `.gitignore`). Check `git ls-files` before a commit. Never commit secrets. Push only when Bruno says so.
   A headless import does not add Invector's scripting defines: `ProjectSettings.asset` carries
   `INVECTOR_BASIC;INVECTOR_MELEE;INVECTOR_SHOOTER;INVECTOR_AI_TEMPLATE;MIS;MIS_FSM_AI` for Standalone.
5. Every mechanic has a play-mode test in the training room that runs in batch mode (`tools/unity.sh test playmode`).
   The bot (`tools/unity.sh play TrainingRoom Main_Menu structural 300`) proves the whole loop and saves shots.
6. Scenes and data assets come from editor code (`MenuLoopSetup`, `CreateDifficultyModules`), never from hand-edited
   YAML. Game code lives in `Assets/_Game/Scripts/<Subsystem>/`, namespace `GlorifiedBody.<Subsystem>`. Only
   `GlorifiedBody.Integration.Invector` may `using Invector.*`. No asmdefs (the vendor code has none).
7. Writing style for anything Bruno reads: ASD-STE100.
8. Every step ends with a note in `devlog-notes/` and the shots Bruno can open, checked at full size first.
9. The game is RFab's dogfood (Bruno, 2026-10-22). Every RFab bug, limit or missing piece found while building
   goes the same day as one row in `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/RFAB_GAPS.md`,
   with the evidence file. The RFab agent works that list.
