# THE GLORIFIED BODY (the game): read first

A tank-control horror game in Unity 6, a refinement of Galerians (PS1, 1999). Lucy, a young adult woman with no
memory and three psychic powers, escapes the cult's lab. Started 2026-10-20 (the pivot from the video series).

- **Design (CANON):** `/home/pc/MEGA/code/the-glorified-body/DEMO_PLAN.md` (the rules, the map, the beats, the
  puzzles, what waits on Bruno). If the code and this file disagree, the file wins, or you ask Bruno.
- **World and story:** `/home/pc/MEGA/code/the-glorified-body/LORE.md`, and the lines in `DEMO_SCRIPT.md` there.
  Bruno's words by round: `RULINGS.md`. The old `03_GAME_DESIGN.md` and the bible are in its `archive/`
  (consolidated 2026-11-06).
- **Art rules, read before any art work:** `ART_LESSONS.md` (this folder).
- **Asset lessons, full detail:** `/home/pc/MEGA/code/the-glorified-body/tests/T15_3d_set/LESSONS_LEARNED.md`.
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
  record the tutorial with a 900 s limit). The armour knee test failed in about half the runs until the locked hinges of 2026-10-31; it passed in every run since (checked 2026-11-03).
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
  FOR LATER (Bruno): replace the temp audio, music, SFX and graphics with the real ones; refine the character models
  and the texture placement.
- 2026-10-31, day, his answers: save machines stay on the map, the tape music stays at 8 %, a tape key yes; "all of it,
  then check the animation errors" (PC free all day). Built and committed, not pushed:
  - The tape key T (`GameAction.Tape`), not in a fight. IN-WORLD SCREENS (`devlog-notes/2026-10-31_screens_tape_key.md`):
    `Interaction/InWorldScreen.cs` (one video for many faces, 3D sound on Voice, stops with the world, subtitles within
    12 m, ducks the music, pauses a tape; no clip = a greybox card), `InteractKind.Act` (no question),
    `StepKind.Screen`. SCREEN TEST room (`ScreenTestSetup`), placeholder clip `tools/screen_placeholder.sh` (ffmpeg +
    flite). The gym CRT faces faced into their boxes since 10-22: fixed in `BuildTherapyGym`.
  - THE OUTFIT CHANGE (`devlog-notes/2026-10-31_outfit_change.md`): `OutfitLocker`, `OutfitChange` (keeps the damage, the
    grabbables over the limit fall at her feet), `LucyModel.Wear` (`OutfitProfile.modelClass`, also on a load or a door),
    the HUD outfit name, `StepKind.Outfit` (the Crown grab). OUTFIT TEST room. `RoomMusic` has its own file (it loaded
    as a missing script); `RoomKit.Music`. Map: TEST FLOOR 3.
  - THE FLOOR SHIELD (`devlog-notes/2026-10-31_floor_shield.md`): mk_orzabal + the defend key held = a full circle tears
    a slab out of the floor (`Combat/FloorShield.cs`, 80 HP, 30 MK), a held prop (throw, slam), bullets stop on it, a
    scar stays. Tutorial: 26 steps (floor_shield). Bot "shield" (AI TEST).
  - SEAM SWEEP 3 and THE ANIMATION CHECK (`devlog-notes/2026-10-31_seams_and_animation.md`): nine seams fixed with tests
    that failed first (`RoomSeamTests`: RETRY at the door, a cutscene or a death in a door fade, the short at a door, a
    tape after a load, the death fall); knees and elbows are locked hinges (`HumanoidRagdoll.Hinge`; enemy prefabs
    built again); the gun hangs on the back in the care (`EnemyGun.Sling`); the pistol grip sits in the fist. The
    harness flags a missing exec method. Bots "cpr", "holds".
  OPEN for Bruno: in those notes.
- 2026-10-31, afternoon, his answers (`devlog-notes/2026-10-31_answers_cpr_rifle.md`): a short goes on in the next room
  (`ShortController.GoOn`, `PlayerState.shortLeft`); a cutscene in a doorway stays off; the locker warns and keeps the
  uniform when she carries too much, the key X (`GameAction.Drop`) drops a thing from the inventory; the room saves the
  floor items (`FloorItem`) and the shield scars (`FloorScar`). THE CPR AND DRAG: RFab has no such motion (gap 13), so
  the vendor `Pick_Down` held at 0.42 s (a kneel) plus the arms as humanoid muscles (`CorpseCare.CprArms`, from a
  `PoseProbe` sheet; `ClipProbe` measures a clip), the drag hands on the shoulders by the two-bone reach. THE RIFLE ARMS:
  the wrists 6 cm behind and 4 cm under the grips, the right elbow down (bot "holds" measures it).
- 2026-10-31, evening, four pieces the demo beats need (`devlog-notes/2026-10-31_demo_pieces.md`): the FEED LOCK
  (`Feeding.Locked`: `feed_locked` up and `feed_unlocked` not; a log raises a flag on reading, `ItemDefinition.readFlag`),
  HIDERS (`EnemySpawn.hides`, `CivilianBrain.SetHider`: silent, whispers at 4.5 m, begs at 2 m, never runs), THE
  BULLETIN (`Story/Bulletin.cs`, `StepKind.Bulletin`, by `PsychicStats.People`, every person she killed, saved), WALL
  SIGNS (`RoomKit.Sign`). The brain fight is for later, built separately (Bruno). The push needs Bruno's hand (the
  session may not run `git push`).
- 2026-10-31, night, THE PLAYER BUILD AND THE DEMO GREYBOX (`devlog-notes/2026-10-31_player_build_demo_greybox.md`):
  `tools/unity.sh build release|dev [linux|windows]` (`Editor/PlayerBuild.cs`, 2 h limit, output in `builds/`, out of
  git and MEGA); a release has the menu, training, tutorial and the Demo_* rooms; `GameBuild.Dev` (editor, development
  build or `-gbDev`) gates the debug keys and the TEST ROOMS column. The triage marks a run that did not end DIRTY.
  THE DEMO: 22 rooms in plan metres (`Scenes/Demo/Demo_<id>.unity` until 2026-11-02, now one world scene, `Editor/Demo/`: `DemoRoom` the kit, `DemoWard`
  W1 to W12, `DemoPublic` G1 to G9 and U1, `DemoContent` the logs and the writers' cutscene lines as subtitles), the
  map floors "THE WARD" and "THE PUBLIC FLOOR". START loads the demo (W1); TRAINING ROOM is dev only since 2026-11-02 (the
  top of the TEST ROOMS column, not in a release). NO BLIND SPOTS (Bruno): each camera cell searches its pose by the coverage rule
  (`CameraCoverage.Sees`) and splits while it misses any point; `DemoTests` fails on one point out of frame (G5 has
  16 cameras, U1 13). A room change: run its BuildAll, then `MapAtlasSetup.BuildAtlas`. Proof: `DemoTests` (4), bot
  `play Demo Main_Menu demo 900` (one shot a camera). `FlagSwitch` (a switch that raises a flag),
  `ScriptedSequence.runOnStart`, `StepKind.EndDemo`.
  TBD (Bruno): a second way past the W11 stair card, a bit out of the way, that lets her go on without the ward
  sister's death. He reports on the cameras after his playthrough.
- 2026-10-31, night, THE WEB AND WINDOWS BUILDS (`devlog-notes/2026-10-31_web_windows_builds.md`, Bruno: itch.io first):
  `tools/unity.sh build release web|windows|linux`. Both bots pass in all three players (web: `node tools/web_bot.mjs
  <build> demo|tutorial <dir>`, headless Chrome; Windows: Wine; the bots quit a player at the end). Web: clips play by
  URL (`Core/VideoClips.Assign`, copied to StreamingAssets for the build only), the template keeps the saves
  (`autoSyncPersistentDataPath`), Standalone's defines (`PlayerBuild.SyncWebDefines`), no pointer lock, no QUIT,
  `WebTexturePostprocessor` (DXT for the web only), every timer from a clip length through `GameAudio.Length`. A
  release hides the debug line and the debug keys of How to Play. 90 MB on itch.io. A BUILD RUNS IN ITS OWN systemd
  SCOPE (9 GB): the web IL2CPP step once took 4.7 GB and the OOM stopped the whole Devin app. Every build switches the
  editor back to Linux. OPEN for Bruno: the manual web check (§5 of the note), code signing for Windows.
- 2026-11-01, evening, THE REMAKE LOOK TEST, T26 (`devlog-notes/2026-11-01_remake_look.md`, restarted: the first pass had
  the white room and the navy guard). The guard and Lucy's disguise wear the T24 light kit in the game
  (`guard_basecolor.png`, `lucy_guard_basecolor.png`). `RemakeLook.unity` (`RemakeLookSetup.Build`, then `.Bake`): W1 in
  the act 1 black look, profile A (the current look) and profile B (ten switches, `RemakeDirector`, F3) in one scene,
  the full-mesh 3D Studio guard in B (`RemakeBodySwap`), bot `play RemakeLook Main_Menu remake 1500`. Static batching
  is off in that scene (a batch broke the bake swap). OPEN for Bruno: the pick (the note's last section).
- 2026-11-02, his answers (`devlog-notes/2026-11-02_answers_knees_card_talk.md`): the CPR kneels on BOTH knees
  (`CorpseCare.CprLegs`, leg muscles over the vendor kneel; the care test measures the knees); the floor shield works
  on every floor; the map stays plain boxes; a tape paused by a screen waits for the tape key (no autoplay); no code
  signing. THE CARD BY A WORD: the Interact key on a living, unarmed civilian who carries an item asks for it
  (`Interaction/CarrierTalk.cs`, added by `EnemySpawn`), the second way past the W11 stair card. FOR THE SECOND PASS
  (not built): backtracking and more rooms and puzzles, his list in `DEMO_PLAN.md` §12.
- 2026-11-02, THE CORNER MAP (`devlog-notes/2026-11-02_corner_map.md`, Bruno: "opening the map does not pause the game,
  and shows the player's location"; his pick: a corner map, she moves): M shows `MapScreen` in the top left corner and
  the game goes on (no `GameState.Map` any more); her dot moves every frame; the key places draw again on a change
  (`MapScreen.Draws`); it stays on through a door and hides under the pause, the inventory, a page, a cutscene and the
  death; the arrow keys change the floor (`GameAction.MapPreviousFloor`, `MapNextFloor`). Tests that need a stopped
  world use the inventory.
- 2026-11-02, THE TRAINING ROOM IS DEV ONLY (`devlog-notes/2026-11-02_training_room_dev_only.md`, Bruno: "remove the
  training room, or set it to dev only"): its button is the first of the TEST ROOMS column, and
  `PlayerBuild.ReleaseScenes` leaves the scene out. The tests and the bots still use the room.
- 2026-11-02, THE W1 SCIENTIST AND THE DOORWAYS (`devlog-notes/2026-11-02_scientist_and_doorways.md`): the scientist
  waits in the corridor behind the door and talks first (`ScriptStep.Approach`, `SoldierBrain.ScriptHold`: no sight
  alert, he walks to a point and turns to her; the script's Alert, a hit or a power ends it). A demo exit fills its
  doorway (`DemoRoom.Doorway`), not the end of the stub.
- 2026-11-02, ONE WORLD (`devlog-notes/2026-11-02_one_world.md`, Bruno: "i want all the map to be built and loaded up
  in one go, we just cull rendering"): the demo is ONE scene, `Scenes/Demo/Demo.unity` (`DemoWorld.BuildAll`, then
  `MapAtlasSetup.BuildAtlas`), loaded once by START; the room scenes `Demo_W1`.. are gone. A room is a root with a
  `Room` (`Scripts/Rooms/`); `RoomTracker` finds her room, raises `OnChanged`, culls the far rooms' drawing (colliders
  stay). Doorways open into the next room (no stubs, no exits); shared walls are built once (`WallLedger`). Floor 1 is
  at `DemoWorld.Floor1Offset` (12.5, -4, -31); the stair room S1 (a ramp) joins W12 to G1 (his pick: a real stair).
  One NavMesh: enemies chase her through doors (his pick); hearing reaches her room and next door (`Room.Hear`), a
  heard death, a whisper and a care search stay in one room (`Room.Same`); lock-on needs a line in the world. RETRY:
  the retry point moves when she walks into a room (`SaveSystem.OnRoomChanged`). Save version 2 (`visited`). The music,
  the map, `runOnStart` beats, fate flags and `FlagGate.waitUnseen` follow her room. FMV: the skip key opens a menu
  (PAUSED, RESUME, SKIP; `CutscenePlayer.MenuOpen`), `ScriptStep.Give(itemId)`, the W1 beats are FMV slots
  (`DemoScripts.Fmv`; a clip at `Assets/_Game/Cutscenes/<id>.webm` becomes the cutscene). The scientist waits in W2 and
  talks first (`ScriptStep.Approach`). Bot `play Demo Main_Menu demo 900`. FOR LATER: flanking (§13.1).
- 2026-11-03, FIRE DOORS AT CHOKEPOINTS (`devlog-notes/2026-11-03_doors.md`, Bruno: "only certain doors that can open
  and close in selected chokepoints on the map ... a lever that closes+ locks/jams the door shut (justified by them being
  firebreaks)"; a normal doorway has no door): `Interaction/FireDoor.cs` (open at the start; a lever on each side,
  `DoorLever`, jams it and opens it again; a yellow bar across each face while jammed), the leaf a 100 HP breakable
  (`Destruction/DoorLeaf.cs`: enemy kicks, explosions and her powers' splash; not the enemy bullets or the short). At a
  jammed door `SoldierBrain` (`Enemy/IDoorWaiter.cs`) stands and swings: soldiers kick (25), the power armour hits (50),
  guards and scientists bang; the enemies never pull a lever. Card doors stay `LockedDoor`. The demo has three: W2-W5,
  W5-W7, G2-G5 (his pick). `new Door(..., fire: true, pocket: ±1)` in a room spec builds one (`BreakableAssets.Door`).
  The saves keep jammed doors (`RoomState.jammed`). DOOR TEST room (`Demo/DoorTestSetup`, five rooms), `DoorTests` (11),
  bot `play DoorTest Main_Menu doors 300`. Fixed on the way: the card door leaf and the CRT bodies of the demo stood at
  twice their place (a local position under a placed parent: the W12 stair card stopped nothing); a held lock drops
  after 1 s out of her sight in the world (`LockOnController.LostSightSeconds`).
- 2026-11-03, NO DITHER (`devlog-notes/2026-11-03_no_dither.md`, Bruno: "since we're reformulating the art style, you
  may remove the dithering effect"): the Dither pass, its CharacterStencil pass, the shader and the material are gone
  (`LookTestSetup.RemoveDither`).
- 2026-11-03, THE GRAPHICS OPTIONS (`devlog-notes/2026-11-03_graphics_options.md`, Bruno: "the usual knobs and changes
  that most games allow"): `Rendering/GraphicsOptions.cs` (the store, the presets Low/Medium/High/Ultra/Custom, the
  apply). Display: display mode, resolution, VSync, frame rate limit, brightness (a volume). Graphics: render scale,
  anti-aliasing (Off, FXAA, SMAA, MSAA 2x/4x/8x), texture quality, filtering, shadows (Off to Ultra, hard only),
  ambient occlusion. High is the default. The LOOK option and "Rain Effects" are gone; the game draws with the stock
  renderer. `LookQualityApplier` keeps its name (every room holds it) and applies the options. Batch runs use their own
  keys (`batch_gfx_`). `GraphicsOptionsTests` (4).
- 2026-11-03, night, FLANKING (`devlog-notes/2026-11-03_flanking.md`, DEMO_PLAN 13.1, Bruno: "guards would try to face
  her head-on, soldiers and power armor flank her"): `Enemy/FlankBoard.cs` holds the sides of a fight (the axis from her
  to the first flanker; the flanks first, then her back, then the corners: two take opposite sides, the pinch).
  `SoldierBrain.Style`: soldiers and the power armour flank (they circle her at range, 60 degrees a move, and a spot in
  her view costs 8 m), guards and the pistol scientist keep to their line (head-on), the knife runs to her side.
  The mouse turns her (`SettingsManager.MouseTurn`, Mouse Sensitivity degrees a unit; Bruno: "have it actually do
  something"). Fixed on the
  way: a spot lies inside the weapon's distances (`DummyShooter.MinAttackDistance`), the brain asks the gun for melee
  reach (`DummyShooter.HerInReach`). `FlankingTests` (2), `ArmedCivilianTests.TheKnife_ComesInFromHerSide`. Numbers
  PROPOSED.
- 2026-11-03, night, the backlog sweep: the armour knee test was already fixed (2026-10-31); the patient test counts her
  own lines (`Bark.CountOf`); the gas cloud test camera leaves out the figures. Manual list for the morning:
  `devlog-notes/2026-11-03_manual_tests.md` (33 steps).
- 2026-11-04, THE OPENING (`devlog-notes/2026-11-04_opening.md`, Bruno: "it needs to start on the curscene right away";
  "the second cutsene triggers even before i leave the room"): `ScriptedSequence.startsBlack` + `ScreenFade.Cover` (black
  from the first frame, no game frame before the wake). 2026-11-05 (his pick): the W1 door is a fire door, shut at the start
  (`FireDoor.startsJammed`, `leverFlag`). At the door it opens, the scientist talks in the doorway, steps in, it shuts
  behind him (`ScriptStep.DoorOpen`, `DoorJam`, `AlertAlone`), the fight is in W1, her lever opens the way on. The three of
  W2 (one pistol, two knives) wait behind it (`EnemySpawn.heldUntilFlag`, `SoldierBrain.ScriptHoldUntil`).
- 2026-11-05, THE SECOND ROOM (same note, Bruno: "add a second room that the first scientist will be in ... a
  mini-tutorial/safe space"): W1 (x 0..8, z 19..26) is the quiet room, no enemy, no beat after the wake, 5.2 m to the plain
  doorway. W1B, TREATMENT ROOM (x 1..7, z 14..19, the old footprint): the scientist behind a privacy screen (no line from W1),
  a wall-to-wall trigger just past the doorway, the talk, then he attacks. `FireDoor_W1B_W2` stays shut through it, her lever
  opens it after the talk (`demo_w1b_scientist`), the three of W2 behind it. `ScriptStep.DoorOpen` and `DoorJam` are gone.
  His answer: no control hints in W1.
- 2026-11-05, THE FLOATER AND WALLS (`devlog-notes/2026-11-05_floater_walls_capsules.md`, Bruno: "the soldier that floats can
  phase through walls"): `TelekinesisLift.HangMove` sweeps the hang move, a wall stops it and it slides, the hold goes on;
  `Obstacle` also sweeps the middle line. Two MK capsules in W1B fall in the talk (`FlagGate`, `demo_w1b_capsules`). FOR LATER
  (Bruno, `DEMO_PLAN.md` §13.5): one pose for every power, one arm up with the palm out, and with mk_orzabal the arm follows
  the floater.
  His answer: a floater left behind does not drop.
- 2026-11-05, after his playthrough (`devlog-notes/2026-11-05_exits_feed_cutscene_end.md`): THE EXIT RULE (Bruno: "i also need to see the
  exits"): the camera of the floor in front of a doorway shows that doorway (`CameraCoverage.SeesExit`, `Room.exits`, `DemoRoom.BestPose`),
  0 of 48 out of view. THE FEED opens by cutscene 02b (`W2_Chart` when the three die, `W5_Chart` if she runs past them, `ScriptStep.StopIfFlag`);
  the chart is no floor item. The bulletin closes by itself after 12 s, with a hint. Kind `soldier_shield`. The cowering guard is the fear rule, as
  designed. FOR LATER (`devlog-notes/2026-11-05_study_squad_ai.md`, `DEMO_PLAN.md` §13.6, Bruno: "they need to be ... an organized team"): Invector
  has no team AI, so a `FireTeam` on `FlankBoard` with the v2 level design.
  Later that day: a figure that cowers says a line within 4 m (`SoldierBrain.CowerLine`, `barks.csv` `cower_*`) and drops its weapon and shield
  (`EnemyGun.Drop`, Bruno: "cowering figure drop its weapon").
- 2026-11-05, night, THE FLOORPLAN V2 (`devlog-notes/2026-11-05_floorplan_v2.md`, Bruno: "you can get started on the v2 map";
  the writer's `game/act1_map_v2.py`, `DEMO_PLAN.md` §3.4): twelve rooms in greybox (`Editor/Demo/DemoV2.cs`: W13 to W17,
  G9b to G15), the doors of the map (two loops: G10 and G13), G6 and G8 moved to make room. The fixtures of round 30
  (`Interaction/Fixture.cs`: microwave, coffee machine, toilet, faucet; placeholder sounds from `tools/fixture_placeholders.py`).
  The conference room beat (`Interaction/NameCard.cs`, cutscene 07b as black and text cards with sounds, `CutsceneAsset.Shot.sound`,
  the flag `demo_g12_card`, "Encore." in U1 under a flag gate). Two older faults fixed: the cull keeps the wall pieces drawn
  (a shared wall vanished with its builder), and every wall-mounted sign stands 0.17 m off the room line (it stood inside the
  0.3 m wall). A NavMesh rule for the dressing: the agent is 0.5 m, so an aisle needs 1.2 m and a gap between chairs 0.9 m or
  less (1.0 m makes a sliver island). `DemoV2.NavMeshReport` prints the mesh per room. `FloorplanV2Tests` (4).
  OPEN for Bruno: in the note.

- 2026-11-06, COVER THAT FINDS ITSELF (`devlog-notes/2026-11-06_cover_scan_prop_types.md`, Bruno: "adding them manuially one by one is a
  non-starter"; "add some prop types"): `Enemy/CoverMap.cs` scans the live NavMesh at the first need (rays at 0.7 m and 1.35 m: low cover, corners; 512
  spots in 48 ms). `SoldierBrain`: `Phase.Duck` between shots, `DummyShooter.FromCover`, `LockOnTargetable.InCover` (a thing on her line stops her power while
  he hides). `DemoRoom.Prop` makes fifteen types breakable by name (`BreakableAssets.PropBox`). The whole PlayMode suite takes 31 minutes: run it with
  `GB_BATCH_TIMEOUT=5400`. The writer's folder is now `/home/pc/MEGA/code/the-glorified-body`.

- 2026-11-06, night, THE FIRE TEAM (`devlog-notes/2026-11-06_fire_team.md`, DEMO_PLAN §13.6): `Enemy/FireTeam.cs`: the soldiers of a fight take turns in two elements,
  one moves, one holds and fires (`SoldierBrain.BoundNow`), a wanted range of 8 m and no spot under 5 m, the writer's calls (`barks.csv`, `team_*`), the last two fall back.
  Not built: suppression, search in pairs. Numbers PROPOSED.

- 2026-11-07, MK_ORZABAL (`devlog-notes/2026-11-07_orzabal_cost_throw_drop.md`): a left press lifts the lock at once, `PsychicPowers.LiftCost` on anything, with either button. A release on a held thing throws it, and the cost, the damage and the speed go with the charge (`TryLaunch(charge)`); a short press is the least charge; a full circle slams. A right press drops a box or a crate (`TelekinesisLift.IsBox`), never an enemy or the slab (a misclick). mk_edgar: a part circle stuns and costs by the charge; on a floater it does nothing and costs. The wheel switches the lock, and with mk_orzabal and a thing in the air it raises that thing. A dead body with no throes froze one frame after a drop (`EnemyRagdoll`, since 10-27): fixed. A bot result is in `playthrough.json`: the triage line does not read it. EDGAR TEST starts with its AI off: `EnemySpawn.passiveUntilFlag`, a switch wakes the figures. mk_edgar has a limit by rank (`Dominated.LimitFor`: a civilian and a puzzle figure have none), a warning, a snap on his knees, no second take (`MindSnapped`); a soldier or the armour needs 50 % HP or less; the one she holds is an ally (`LockOnTargetable.IsAlly`): the switch reaches him, the auto-lock skips him. The order to a switch has a HUD prompt and a violet lamp, and it needs a NavMesh route (`Dominated.SwitchInReach`). FOR LATER (Bruno): men in black, one enemy type for each of her powers, not in act 1; Lucy speaks after she takes a mind (a task for the writer, `DEMO_PLAN.md` before §15). Numbers PROPOSED.


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
   goes the same day as one row in `/home/pc/MEGA/code/the-glorified-body/RFAB_GAPS.md`,
   with the evidence file. The RFab agent works that list.
10. Test cadence (Bruno, 2026-11-06): during a round, run only the test classes of the changed code and the bots of that feature. Run the whole PlayMode suite and all the bots once, before a push or overnight (about 50 minutes). Only the tutorial bot walks the whole tutorial, so it runs before every push.

- 2026-11-06, day, MK AS AMMUNITION (`devlog-notes/2026-11-06_mk_ammo_short_death_search.md`, Bruno: "that basically makes mk an ammo counter and the pills clips"): one MK bar for each power
  (`PsychicStats.Mk(kind)`; `Nalcon` reads the selected bar; only that bar shows on the HUD), no recharge, one capsule for each power (`DrugType.Nalcon`, `NalconFire`, `NalconLift`). Every point of MK spent fills
  SHORT (`ShortPerMkPoint`). A short can kill her (killer id `short`, its own death screen row); a second wind saves her and ends the short. SCARCITY and DEEP WELL change the cost. `FireTeam`: suppression
  (`DummyShooter.SuppressPoint`, 1.5 s with no line), the team knows only what it saw (`LastSeen`), the search in pairs (`Phase.Search`, 8 s). `Story/TeamReinforcement.cs`: the four of G2 dead and sector C released = a
  second fireteam of four after 20 s. Bot `play Demo Main_Menu team 300`. Numbers PROPOSED.

- 2026-11-06, afternoon and evening (`devlog-notes/2026-11-06_autofire_inventory_carry_edgar.md`, `2026-11-06_coda.md`): a full circle fires by itself, the defend key cancels a charge. The inventory has three lists (W S scroll, A D change the list, S no longer closes). THE MOUSE CARRY: mk_orzabal, hold the defend key to lift a prop, the mouse moves it, the wheel raises it, a release drops it, no MK (`TelekinesisLift.SteerBy`, `PropAssets.Box`, `PressurePlate`, PHYSICS TEST). MK_EDGAR: key 4, a full circle only, 75 MK, 6 m, the target kneels (`KneelPose`), civilians and puzzle figures at full HP, guards need damage, bosses never (`Enemy/Domination.cs`, `Dominated`, `SentSwitch`, EDGAR TEST, flag `mk_edgar_unlocked`); before the flag the capsule is UNKNOWN CAPSULE. A short that takes the second wind goes on with no more HP loss. The TUTORIAL is grey boxes with no bake, 34 steps, an annex. G16 has a gap a crate fills (`PitReturn`). THE CODA (`Editor/Demo/DemoCoda.cs`): X1, R1, R2, R3 on the map floor THE ROAD, the title at its end. This demo is not the final demo. Devlog brief: `the-glorified-body/devlog/ep07/BRIEF.md`. Numbers PROPOSED.

- 2026-11-06, night (`devlog-notes/2026-11-06_last_hit_and_the_physics_step.md`, Bruno: "any hit that would kill the player will first degrade them to 1 hp"): a lethal hit from above 1 HP leaves her at 1 HP with 0.5 s of invulnerability (`Health.LastHpInvulnerableSeconds`), the next one takes the second wind or kills her; `Health.Kill()` goes past the rule. The gas smoke is white. A STOP WRITES ONLY THE TIME SCALE: `Time.fixedDeltaTime = 0` threw a landing body 149 m up; never write it. OPEN: `TheFireteam_Bounds` failed in 1 run of 9 (37 % against 35 %, cause not proved). FOR LATER (his annotations, in the design): firemen with CO2 grenades, the computer room with halon (G17).
- 2026-11-06, night, the pistol hold (same note, Bruno: "the scientist with the gun is holding the weapon wrong"): `EnemyGun.PistolHold` (the forearm along the barrel, a low ready at rest, a lab worker's left hand under the grip; every pistol). AFTER A MODEL SWAP: run the `holds` bot in AI TEST and LAB TEST, and `ArmedCivilianTests`; a rig with finger bones needs a finger curl (none exists). FOR LATER (Bruno): power armour loadouts, "some power armor troopers carry a minigun, others a laser gun, others a rocket; all of them can lob rockets".
- 2026-11-06, night, the baked guard (same note): `T27BakedCharacters.RehearseSwap` runs the enemy tests on `t27_guard`. Its bind pose faces away from the root, so `HumanoidRagdoll.BodyFront` and `EnemyGun.FindHandFrame` read the facing from the skeleton. AFTER A MODEL SWAP: read `[RagdollTests] the worst knee` in the log; `TheKnees` stops at the first bad knee.

