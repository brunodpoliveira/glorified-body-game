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
  OPEN for Bruno: difficulty as percentages (easy 100, normal 150, hard 200, custom). Drugs and HP: see 2026-10-30. Later (Bruno, 2026-10-21): the Invector FSM AI with "move or shoot, never
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
  training room loads the look test room. Shots for Bruno's pick: `~/MEGA/glorified-body-renders/game/look_test/`.
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
- 2026-10-24, his review (`devlog-notes/2026-10-24_ragdoll.md`): `EnemyRagdoll` on every soldier but the heavies
  (11 bones from `HumanoidRagdoll`, editor code; Unity's ragdoll wizard does not run in batch), on the BodyPart
  layer, kinematic and colliders off while animated. The lift holds the floater limp by the hips; a throw (it steers
  to its target) and a slam drop it limp, and the struck enemy falls too (2.5 s); a full mk_warspite knocks a
  soldier down 2.5 s; he stands up with the vendor `StandUp@FromBack/Belly` clips; a dead body freezes when it
  settles. `IRagdollBody` / `RagdollBodies.IsDown` suspend the brain, the gun and the shield. Gore: the head burst
  only for a kill by the short; a full mk_warspite kill (and the telekinesis and explosions, PROPOSED) cuts a limb
  (`BoneStump`, the vendor `goreLimb-prefab`); `HitInfo.Charge`; `BodyBloodPool` grows a dark pool under a dead
  body (`GoreLimbSetup.Assign` fills the profile). `AiTest.unity` and its NavMesh are git-ignored: build them with
  `AiTestSetup.BuildAiTest`. Studies (not built): `2026-10-24_study_breakable_cover.md`, `2026-10-24_study_gore.md`.
- 2026-10-24, evening, his answers to both studies:
  - Gore (`devlog-notes/2026-10-24_gore_answers.md`): the short and mk_orzabal kills burst the head. A full
    mk_warspite kill and an explosion (a returned grenade or rocket, explosion damage now) cut a forearm or a whole
    arm at random. The piece flies off (`SeveredLimb`: the skinned mesh baked, the limb's triangles; the character
    meshes are readable now). Gore Low: no cut. The body pools have no limit.
  - Breakable cover (`devlog-notes/2026-10-24_breakable_cover.md`), namespace `GlorifiedBody.Destruction`: barricades
    (1.2 m: they stop her powers, the soldiers shoot over them and take cover behind them), tables with items
    (`Greeble`), plaster patches, Realtime lamps with a fake bounce. All of it is collateral damage. The pieces are
    cut in the editor (`BoxFracture`, `BreakableAssets`) and lie on the new Debris layer (slot 19). A broken room
    stays broken (`RoomDamageLedger`: kept when she leaves, dropped on a restart). The surge of the short pops the
    lamps near her. A thrown body arcs down to the chest of the target. The COVER TEST room (`CoverTestSetup`, local
    only like the AI test room) and its "cover" bot. Tutorial: 22 steps (cover: break the barricade in front of
    `Target_3`). His answers: a dark room is only a look (no mechanic), the thrown arc stays, the sprays are the
    vendor's textured ones (`GoreSprayLook`, smoothness capped at 0.55). OPEN: the sound source (RFab gap 14).
- 2026-10-24, late, his first manual test of the day (`devlog-notes/2026-10-24_test_feedback.md`): long guns in both
  hands, one flash per shot, melee warning half a shot's, floor blood that stays
  (`FloorStains`), F12 makes the care count her as unseen, tall screens for her cover, weaker tables beside the
  barricades, a thrown body breaks any cover, the tutorial is a safe room (no damage, MK full) with one dummy per
  weapon step, Q opens the drug choice (E asks, E takes), the short step cannot soft-lock.
- 2026-10-24, night, his re-test (`devlog-notes/2026-10-24_retest.md`): a long gun sits on the right shoulder
  (`EnemyGun.Shoulder`: the butt in the shoulder pocket, the barrel on the aim or at a low ready, both arms by
  two-bone IK with the elbows down). The retreat releases the aim's brace (a braced guard ran in place). The
  tutorial's log step counts a read on the inventory screen, and the capsule step gives a capsule when hers is
  gone. FOR LATER (Bruno): the gun floats during the CPR (sling it on the back).
- 2026-10-25, the civilians and the death screen (`devlog-notes/2026-10-25_civilians_death_screen.md`):
  `CivilianBrain` (unarmed `labworker` and `nurse`, no gun): it runs from her with a scream (the scream alerts the
  armed ones), cowers, begs when she comes near or when every route out passes her (`EnemyNav.TryEscape`).
  `EnemyMorale` talks to `IFearful` (the soldier and the civilian brains). The auto-lock skips a civilian, the switch
  reaches one (`LockOnTargetable.IsCivilian`, PROPOSED). CIVILIAN TEST room (`CivilianTestSetup`, local only) and the
  "civilians" bot. Killer ids are `<class>_<weapon>` (`DummyShooter.KillerIdFor`); the death screen looks up the
  whole id, then the class, then the fallback; `DeathScreenSetup` fills it from the writer's `death_screens.csv` plus
  placeholders; a grey card shows the picture note and the killer id, the voice line is a caption. Camera zones are
  on "Ignore Raycast": on Default they blocked the Invector sight line (no enemy saw her inside a zone).
  His answers: keep the lock skip, the 25 m scream, the id form; the writer's lines may go public.
- 2026-10-25, afternoon (`devlog-notes/2026-10-25_civilians2_sounds_short.md`): civilians are Wary first (they walk
  backward, facing her, to 6 m), and run and cower only after a kill they see or a hurt (`CivilianBrain.Panic`).
  `Tremble` (noise turns over the clip) when wary, cowering, begging. `CorpseCare` takes any `ICarer`: the nurse
  tends a body in her sight, a lab worker only unseen. The care barks are the writer's. One sound per event:
  `ShotEffects.SpawnSilent` turns off the vendor prefabs' own sources (the impact clip opens with a gunshot), one
  family of shots per gun. The short starts when the bar fills during a held charge. STUDIES (not built):
  `2026-10-25_study_wall_lean_shock_pain.md`. FOR LATER: replace the placeholder fire and shock effects.
- 2026-10-25, evening (`devlog-notes/2026-10-25_poses_short_pain.md`): only a kill makes a civilian run (a hurt alerts).
  No collars on lab workers. RFab fear presets (`afraid`, `scared_01`, `scared_02`) on the lab worker:
  `Models/labworker_fear.fbx`, played over the controller by `PoseOverride` from `Resources/Characters/pose_clips`
  (`CreateCharacterAssets.BuildPoseClips`). Each civilian has a pose variant: crouch, wall lean (`WallLean`: level rays
  find the wall, the skeleton moves back to it), self-hug, shaking stance. The short's start freezes the game 0.6 s
  while Lucy plays a pain clip; each shock arcs to its victim (`ShockArc`), zaps (`Resources/Audio/short_zap.wav`,
  placeholder), stuns it (`IStaggerable`, civilians too) and jolts it (`Tremble.Jolt`).
- 2026-10-25, late (`devlog-notes/2026-10-25_slide_and_migraine.md`): a civilian on its wall slides down to the floor
  within 5 m (the RFab `hurt` sit, a 0.8 s blend) and begs from there within 3 m. The short's start: the world freezes
  2.5 s (`ShortController.StartFreeze`), Lucy holds her head (RFab `frustrated_02` on Lucy, 0.5 s to 3.0 s, from
  `Models/lucy_pain.fbx`) and screams (`Resources/Audio/lucy_scream.wav`, placeholder), and the shocks start after the
  freeze. Bots wait for the world before a key press (`BotClock.WaitWorld`).
- 2026-10-25, night (`devlog-notes/2026-10-25_night_drag_plea_subtitles_saves.md`): the drag makes the dead body limp
  (`EnemyRagdoll.BeginDrag`, a hand at the shoulders, the legs trail) and it freezes again at the CPR. Half the
  civilians beg with both hands up (`PleaHands`, humanoid muscles, numbers from `PoseProbe` sheets; it keeps the hips
  in place: `SetHumanPose` moves the body). `WallLean` acts only over a playing Animator (on a limp body it sank the
  hips through the floor). NavMesh step height 0.4 m (`NavAgentSettings.Ensure` before every room bake; 0.75 m made
  desks into steps). SUBTITLES (`Scripts/Subtitles/`): every spoken line at the bottom, yellow with a black outline,
  "SPEAKER: words" (`Speaker` from the cast names), barks and the death line included, a Timeline track for
  cutscenes, Options > Display on/off and size. SAVES (`Scripts/Saving/`): save machines (`SavePoint`, 3 manual
  slots, `SaveMenuUI`), checkpoints (`CheckpointPoint`, 1 auto slot; a manual save and a load count too, PROPOSED),
  RETRY loads this game's checkpoint, CONTINUE / LOAD GAME on the main menu, JSON files with a version, batch runs
  write to `saves_batch`. Rooms place them with `SaveAssets`; the training room has one of each.
- 2026-10-25, late night (`devlog-notes/2026-10-25_surface_sounds_gore_saves.md`): SURFACE SOUNDS (Bruno: the bullet
  hit "sounds like metal-on-metal"; it was Invector's knife clip on every surface). `Surfaces.Of(collider)`: an intact
  breakable's kind, else the material name (concrete words first), default Concrete; `ShotEffects.surfaces` holds
  Invector's five impact prefabs and the sounds (concrete made by `tools/impact_sfx.py`, the rest Kenney CC0 in
  `Assets/_Game/Audio/Impacts/`). One sound per hit: an intact breakable's chip plays its surface. His answers: a save
  and a load count as the checkpoint (kept), no tutorial save step. BODIES AND GORE IN THE SAVES (`RoomGore`): the
  dead keep their pose, stumps, dropped shield, cut arms, floor stains and pools; a load kills them again quietly
  (no witness, no care, no new pool), the Gore option of the moment applies. Training dummies vanish at death.
- 2026-10-26 (`devlog-notes/2026-10-26_exp_and_sweeps.md`): EXP LEVELS (design §11.2): `ExperienceRules` (PROPOSED:
  civilians pay most), `PsychicStats` credits her kills from the hit bus (the hit in the frame of the death,
  `Health.DeathFrame`; each body once), a level adds 10 HP and 10 % power damage (`PsychicPowers.DamageScale`), "LV n"
  and "LEVEL n" on the HUD, the exp in the save. Three sweeps (`devlog-notes/2026-10-26_sweeps/`): no music and no voice
  system yet, the demo lacks room travel, scripted events, voice, cutscenes and a room kit, 39 seams between systems.
  Seam fixes with tests (`SystemSeamTests`): a pause or the inventory during the short's freeze stopped the game for
  good (the freeze now waits), a drug in the death beat revived her, a save of a missing room hung the load. The
  exterior light: the same method with a Mixed directional sun (study only). His answers to the 2026-10-25 points:
  keep, keep, no, no.
- 2026-10-26, night, THE SOUND SYSTEM (`devlog-notes/2026-10-26_sound_system.md`, `Scripts/Audio/`, no scene change:
  `AudioHost` "[GameAudio]" starts with the game): channels SFX / Music / Voice / UI with their sliders (Voice is
  new), one 3D curve, the pause and the inventory and the death screen pause the world sounds, the listener on Lucy
  (`ListenerRig`), `MusicDirector` (Ambient / Tension / Combat / Menu, crossfades, `Resources/Audio/Music/<room key>/`
  else `_placeholder/`, `RoomMusic` for a sector key, a room tone), `Voice.Say` / `Voice.PA` (`Resources/Voice/<id>`
  or the subtitle alone, `voice_missing` in the log; barks go through it), `Sfx` events (one folder each in
  `Resources/Audio/Sfx/`; steps, dodge, powers, hits, falls, UI, doors, pickups, scan, saves). Placeholders:
  `tools/audio_placeholders.py` (numpy music and hums, Kenney CC0 packs). Tests: `SoundSystemTests` (7), the AI room's
  Tension test. Bruno's answers: exp numbers yes, a civilian kill does not fill SHORT, build the sound system, the
  exterior test and the demo ranks 1 to 5 next ("all isolated systems ... tested and fine-tuned").
- 2026-10-26, night, THE EXTERIOR LOOK (`devlog-notes/2026-10-26_exterior_look.md`): `LookTestExterior.unity`
  (`ExteriorLookSetup.BuildExterior`, `BakeExterior`, F11): the yard, one Mixed directional sun (live, hard), the sky
  and the bounce baked at 4 texels a metre (`GB_ExteriorBake.lighting`), fog, seven fixed cameras (an open area needs
  one per 10 m cell), `FixedCamera.shadowDistance` (`Rendering/ShadowDistance` restores the asset's 40 m on a new
  room). A room face shows its front to -z at no rotation.
- 2026-10-26, night, ROOM TRAVEL (`devlog-notes/2026-10-26_room_travel.md`): `RoomExit` (a trigger past a doorway:
  the next scene and the entry id, an optional story flag), `RoomEntry` (an ISpot), `WorldState` (her state crosses,
  each room she leaves keeps its dead with their bodies, taken items, open doors; a save holds every room,
  `SaveData.rooms`; the menu clears it), `ScreenFade`. `SaveSystem.CapturePlayer/CaptureRoom/ApplyPlayer/ApplyRoom`.
  Test rooms in git: `TravelTest_A`, `TravelTest_B` (`TravelTestSetup`), `TravelTests` (4). A builder must load the
  character catalog after it opens a new scene (the new scene unloads the prefab links).
- 2026-10-26, night, SCRIPTED EVENTS AND CUTSCENES (`devlog-notes/2026-10-26_scripted_events_cutscenes.md`,
  `Scripts/Story/`): `ScriptedSequence` (steps: wait, say, PA, flags, show/hide, alert, wait dead, SHORT, music, cutscene,
  hold her; once, by its done flag), `ScriptTrigger`, `FlagGate`, `StoryFlags.OnChanged`, `CutsceneAsset` (stills with
  a pan, video, one line a shot) and `CutscenePlayer` (`GameState.Cutscene`: time stops, only Interact passes = skip).
  Test room in git: `ScriptTest` (the ultimatum of §9.3, a gate, the test cutscene), `ScriptTests` (5).
- 2026-10-26, night, THE ROOM KIT (`devlog-notes/2026-10-26_room_kit.md`): `InvectorEnemyFactory.BuildEnemyPrefabs`
  (eight kinds in `Resources/Enemies/`, git-ignored, vendor data), `EnemySpawn` markers (the figure replaces the marker
  at load, same name and path: a room with enemies holds no vendor data), `RoomKit` (boxes, NavMesh, spawns, exits,
  entries, sequences, triggers), `CameraCoverage` reads box floors. `KitTest` in git: 3 armed, 2 civilians, cover,
  items, save, checkpoint, a door to TRAVEL B; `KitTests` (5). AI/COVER/CIVILIAN TEST use markers now (0 vendor refs)
  and are in git since 2026-10-26 (Bruno). AFTER A VENDOR IMPORT: run `BuildEnemyPrefabs` once.
- 2026-10-26, night, five more seams (`devlog-notes/2026-10-26_seams_part2.md`): the second wind on game time,
  subtitles hidden under the pause and the inventory, the scream caption only for the freeze, per-room statics reset,
  the URP asset's MSAA restored at the end of play. The main menu: the test rooms in their own right column (KIT,
  SCRIPT, TRAVEL TEST added).
- 2026-10-26, day, his answers (`devlog-notes/2026-10-26_answers_difficulty.md`): the AI/COVER/CIVILIAN rooms in git; a
  short drops the floater at once; a hurt soldier comes back full and calm after a load or a return (only the dead are
  saved); a log page or a scan stops the world (`GameState.Reading`: only Interact, Back, Scan pass, the clip plays on
  real time). THE DIFFICULTY: presets 100 / 150 / 200 % (no easy/normal/hard) or a custom set of modules in two
  sections, HARDER and EASIER (`DifficultyModuleDefinition.section`, `exclusiveGroup`, `presets`; 16 assets from
  `CreateDifficultyModules`). The total is 100 plus the modules; 150 % (the old normal) is the default. One screen
  (`DifficultyMenuUI`) for the main menu and the pause. Enemy damage on her goes through `DifficultyManager.EnemyDamage`;
  `PsychicStats` reads the HP, MK and SHORT scales and ONE-HIT WONDER. Batch runs ignore the saved choice (150 %).
  On screen the drug NALCON is the MK CAPSULE (code ids stay `Nalcon`/`nalcon`).
- 2026-10-27, REACTIONS AND PHYSICAL DEATHS (`devlog-notes/2026-10-27_reactions_and_physical_deaths.md`). Delirium
  (active ragdoll) is a BLOCKER: its tuning is GTA IV data, locked to a GTA IV file (`/home/pc/delirium-spike/REPORT.md`).
  Every death falls as a ragdoll from the living pose (`EnemyRagdoll`: the push of the killing hit, muscles that fade,
  shock convulsions, fire writhing; the armour has a death-only ragdoll; Lucy: `PlayerDeathFall`, the death screen waits
  1.6 s). `HumanoidRagdoll` runs in the game now; its joint sign was wrong since 10-24 (knees bent backward): fixed,
  test `TheKnees_BendOnlyLikeKnees_InEveryDeath`. Bruno's rule: "use invector clips for everyone, for all animations
  that are applicable; we only generate the ones that invector does not have". `HitReactions`: the vendor small and
  big hits from the attacker's side (`IEnemyBody.Flinch(from, big)`), a step, a limp (`IEnemyBody.SetAnimatorSpeed`:
  vAIMotor writes `animator.speed` every frame), a shock spasm and a fire panic in code (Invector has no clip), a jolt
  under a fear pose. Lucy flinches on an upper-body "Hit" layer. The RFab hit clips are out of the game. Bot
  `play AiTest Main_Menu reactions 400` (record mode for clips).
- 2026-10-29, THE ARMED LAB WORKERS (`devlog-notes/2026-10-29_armed_civilians.md`, the sweep's rank 6; Bruno: the story
  docs are out of date, so names and numbers are PROPOSED): kinds `labworker_knife` (`DummyShooter.Weapon.Knife`, melee
  only, `SoldierBrain.Rusher` runs at her, the vendor short sword at 0.32 m as the knife) and `labworker_pistol` (both hands,
  `EnemyGun.SetHold` shake, `DummyShooter.AimSpread` 0.35 m), rank `ArmedCivilian` (fear limit 3, `Tremble` nerves).
  `EnemyGun` runs at order 30 (after `Tremble`). A carried item: `EnemySpawn.drop` (a hidden pickup of the room) gives
  the figure an `ItemCarrier`: a copy hangs on the belt while it lives, and at the death it lies beside the body, clear of the limbs. The saves count hidden pickups
  (`ItemPickup.Taken`). LAB TEST room (`LabTestSetup`, in git), `ArmedCivilianTests` (8), bot `play LabTest Main_Menu lab 300`.
- 2026-10-30, DRUGS, FEEDING, THE WARD (`devlog-notes/2026-10-30_drugs_feeding_ward.md`, Bruno: "the only form to regain hp
  is by feeding on fallen enemies"): each drug does one thing (`PsychicPowers.UseDrug`: MK CAPSULE `Nalcon` fills MK,
  SHORT CAPSULE `Red` takes half the SHORT bar and ends a short, SECOND WIND PILL `DFelon` rearms the second wind; no drug
  gives HP; a drug with nothing to do stays). NO HP REGEN in any outfit, a level gives no HP, MENDING doubles the feed.
  `FeedableBody` (every figure) + `Feeding` (Lucy, vendor `Crouch_Idle`): Interact on a body, HP by rank (civilian 40,
  armed civilian 30, heavy 30, soldier/guard 20), once per body, a hit stops it, `BodyState.feedTaken` in the saves. Rank
  10: `PatientBrain` (kind `patient`: stays in bed, one line), a scan on a person (`EnemySpawn.scanImage`, at the chest),
  `FateFlag` (`<fate>_killed/_spared/_scanned`, `RoomExit.OnLeaving`). WARD TEST room (`WardTestSetup`), `WardTests` (9),
  bot `play WardTest Main_Menu ward 400`. Later that night (his answers): the feed sprays blood (`Feeding.Bleed`, the gore
  driver's `PlayForCheck`), witnesses react (`EnemyMorale.SeeFeed`, placeholder lines), a tutorial "feed" step (23 steps,
  record the tutorial with a 900 s limit). FOR LATER: the armour knee test fails in about half the runs.
  His answers after his manual test: rank 11 (fire, firemen) is NOT in the demo, rank 14 on hold, the short starts at
  the attack press and lasts 16 s (as built), the tutorial goes into the story path, the writer makes the death
  screens. A restart of a room puts back the story flags she came in with (`SaveSystem`, the ward's mark stayed up).
- 2026-10-31, THE MAP (`devlog-notes/2026-10-31_map.md`, Bruno: "the map will start hidden, and being revealed
  room-by-room unless the player picks up a floor plan item; key places ... are to marked"): M opens it
  (`GameAction.Map`, `GameState.Map` stops the room), A and D change the floor. `Scripts/Map/`: `MapAtlas`
  (`Resources/Map/map_atlas.asset`), `MapKnowledge` (a room shows when she is or was in it, `WorldState`, or when she
  carries a floor plan, `ItemKind.Map` + `ItemDefinition.mapFloor`; no save data of its own), `MapScreen` ("[Map]",
  one object for the game). Key places: locked doors red/green, items, logs, saves. `MapAtlasSetup.BuildAtlas` reads
  each room scene (objects named "...Floor", exits, doors, pickups, save machines) from its layout table: RUN IT after
  a room changes, and a demo room needs a line there. Opening a scene unloads the atlas: read the rooms first.
  Test floors: TRAINING, TEST FLOOR 1 (TRAVEL A with a test log, TRAVEL B with the floor plan, KIT), TEST FLOOR 2
  (WARD, LAB). Tutorial: 24 steps (map). `MapTests` (7), bot `play TravelTest_A Main_Menu map 300`.
- 2026-10-31, THE AUDIO LOGS (`devlog-notes/2026-10-31_audio_logs.md`, Bruno: "collected and listened to while walking
  around; it automatically pauses when combat begins; music goes down to almost nothing"; it plays again "once the
  player un-pauses it"; replay from the inventory): `ItemKind.AudioLog` (`audioClip`, `tapeLines` with times and
  speakers), `Audio/TapePlayer.cs` on "[GameAudio]" (plays on through a door, its own clock on game time, the label at
  the top right, the lines as subtitles), the music at `TapePlayer.MusicLevel` 8 % (`MusicDirector.DuckFactor`),
  MusicState.Combat pauses it and only the inventory's Interact plays it again. `ItemPickup.Spawn` makes a pickup in
  play. Placeholder clip: `tools/tape_placeholder.py`; `ItemAssets.TestTape`; TRAVEL A has `Tape_Travel`. Tutorial: 25
  steps (tape). `AudioLogTests` (4), bot `play TravelTest_A Main_Menu tape 300`.
  NEXT (all in isolation before the demo level): in-world screens (a CRT clip with sound), the outfit change, the
  floor shield, a seam sweep (the gas pixel test is flaky). FOR LATER (Bruno): replace the temp audio, music, SFX and
  graphics with the real ones; refine the character models and the texture placement.

## Rules

1. Unity 6000.3.15f1 (installed at `/home/pc/Unity/Hub/Editor/6000.3.15f1`). Start it only through `tools/unity.sh`
   (RAM gate, one batch slot shared with Warspite, run folders in `runs/`). One batch Unity at a time. Close the
   editor before a batch run.
2. `.megaignore` keeps Library, Temp, Logs, UserSettings, builds, `runs/` and the vendor folders out of MEGA. Keep
   it. Renders and captures go in `~/MEGA/glorified-body-renders/game/`.
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
