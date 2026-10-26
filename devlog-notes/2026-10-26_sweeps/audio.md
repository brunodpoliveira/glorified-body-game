# Audio sweep, THE GLORIFIED BODY (2026-10-26, read-only)

Paths are relative to `/home/pc/MEGA/code/glorified-body-game` unless they start with `/`.
"Writer CSV" = `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/game/`.

Only 13 game scripts touch audio at run time: `ShotEffects`, `EnemyGun`, `EnemyShield`, `GasCloud`,
`EnemyGrenade`/`EnemyRocket` (through `ShotEffects.Explosion`), `DestructionFx`, `ShortController`, `PlayerHud`,
`DeathMenuUI`, `SettingsManager`, `GruesomeDeathFx` (turns sources off only). All other game code is silent.

## 1. Music

| Item | State | Evidence | Note |
|---|---|---|---|
| Music player / director | missing | grep "music" in `Assets/_Game/Scripts` finds only the volume setting | No class plays music. No states (ambient, tension, combat). No crossfade. |
| Music assets | missing | `git ls-files` audio list: only impacts, breakables, `lucy_scream.wav`, `short_zap.wav` | No track in `Assets/`, in git or out of it (under `_Game`). |
| Per-room / per-sector track | missing | `RoomDamageLedger.cs:98` keys a room by `scene.name` | A room is a scene. No sector id exists. The scene name is the only room key. |
| Menu music | missing | `MenuLoopSetup.cs` adds no AudioSource | The main menu is silent. |
| Combat signal | built (no listener) | `EnemyAlert.cs:14` static `OnAlert(origin, radius, reason)`. `SoldierBrain.cs:86,146` `Alerted` | `Alerted` never goes back to false. No "calm again" signal exists, so a combat-to-calm fade has nothing to drive it. |
| Tension signal | built (no listener) | `DummyShooter.cs:29` states Aiming/Telegraph/Firing. `DummyShooter.cs:134-139` events `OnAimStarted`, `OnFired` | An aim or a telegraph could drive tension. |
| Game state signal | partly | `GameManager.cs:15-24` enum (Playing, Paused, Inventory, GameOver...). `GameManager.cs:120-128` `SetState` | No C# event on a state change. A music system would have to poll `CurrentState`. |
| Music source | open | `RFAB_GAPS.md` row 14 says RFab "makes music, voices and dubs" | RFab Music Studio exists. Not used for the game yet. |

## 2. Mixing and volume

| Item | State | Evidence | Note |
|---|---|---|---|
| AudioMixer (game) | missing | `find Assets -name "*.mixer"`: only vendor `FootMixer.mixer` and `MISAudioMixer.mixer` | No game mixer. No source sets `outputAudioMixerGroup`. |
| Master Volume | built | `SettingsManager.cs:45-53, 330-333` sets `AudioListener.volume` | Works on every sound. |
| SFX Volume | placeholder | `SettingsManager.cs:56-64`. Only reader: `PlayerHud.cs:492` | Changes only the three HUD beeps. Guns, impacts, breaks, explosions, the zap and the scream ignore it. |
| Music Volume | placeholder | `SettingsManager.cs:67-75`. No reader in any script | The slider changes nothing (no music exists). |
| Voice Volume | missing | `OptionsMenuUI.cs:480-482` has Master, SFX, Music only | No voice slider, no voice key in PlayerPrefs (`SettingsManager.cs:253-255`). |
| Beep volume bug | built, wrong | `PlayerHud.cs:492` multiplies by `MasterVolume` and `SettingsManager.cs:332` also sets `AudioListener.volume = master` | The beeps get master twice (master x master x sfx). At master 0.5 they play at 0.25 of their level. |
| Sources that bypass SFX | fact | `ShotEffects.cs:182-195`, `EnemyGun.cs:303,340`, `DestructionFx.cs:101` (`PlayClipAtPoint`), `ShortController.cs:241-249`, `DeathMenuUI.cs:90-94,149` | All except the HUD beeps. |
| Pause / inventory / game over | missing | no `AudioListener.pause` in `Assets/_Game/Scripts` (grep). `GameManager.cs:60,137,164,179` set only `timeScale` | Sounds that play keep playing through pause, inventory and the death screen. `ShortController.cs:248` sets `ignoreListenerPause`, but nothing pauses the listener. |
| Project audio settings | default | `ProjectSettings/AudioManager.asset:7,13,14` volume 1, 512 virtual voices, 32 real voices | No spatializer plugin (line 15). |

## 3. Voice

| Item | State | Evidence | Note |
|---|---|---|---|
| Voice clips in the game | missing | no voice file in `git ls-files`. `lucy_scream.wav` is an ffmpeg tone (`devlog-notes/2026-10-25_slide_and_migraine.md:23`) | Every spoken line is caption-only. |
| Barks (`Bark.Say`) | caption-only | `Bark.cs:20-28` calls only `SubtitleService.Say` | No AudioClip parameter, no line id. |
| Bark words in code | hard-coded strings | `SoldierBrain.cs:96-98` (care), `:313` shock "OH GOD..." (PLACEHOLDER), `:338` "FALL BACK!" (PLACEHOLDER). `CivilianBrain.cs:41-45, 232-234` | Barks are C# literals copied from `barks.csv`. The game never reads the CSV at run time. |
| Barks written but not in the game | missing | Writer CSV `barks.csv` (41 rows): guard/soldier "spots Lucy", "aims", "fires", "hit", "reloads", "covers", "enters a room", scientist, fireman, patient, marta | No code says these. Only the care, back, cower, beg lines and the placeholder shock/retreat lines play (as text). |
| `barks.csv` columns | no clip column | header: `enemy,situation,line,direction` | No line id and no clip/file column. |
| `death_screens.csv` columns | no clip column | header: `enemy,picture,voice_line,hint` | `voice_line` is the words ("COMPUTER: Subject contained..."), not a file. |
| Death screen voice | placeholder (path built) | `DeathScreenContent.cs:26` `AudioClip voiceLine`. `DeathMenuUI.cs:147-150` `PlayOneShot` if not null | The only path that takes a clip per line today. `DeathScreenSetup.cs:66,85` fills `voiceText` only. All 14 entries in `Resources/DeathScreenContent.asset` have `voiceLine: {fileID: 0}`. The line shows as a subtitle (`DeathMenuUI.cs:134-139`). |
| Tutorial presenter | caption-only | `TutorialPromptUI.cs:10-11`. `TutorialDirector.cs:28` "lines are placeholders" | Text in its own prompt UI. It does not go through `SubtitleService` (no `Say` call), so no speaker name. No clip. |
| Subtitles | built | `SubtitleService.cs:42-60` | Text only. `Line` has Owner, Speaker, Text, Until. No clip, no line id. |
| Timeline subtitle track | built, text only | `SubtitleClip.cs:12-13` fields `speaker`, `line` | No clip field. Unity's own Timeline AudioTrack is available (`Packages/manifest.json:13` timeline 1.8.6), but no cutscene or `PlayableDirector` exists in game code (grep finds it only in `SubtitleTests.cs`). |
| Cutscene player | missing | no PlayableDirector use. Scripts exist at writer `game/cutscenes/01..04_*.md` | Design §1.3/§1.4: cutscenes with voice acting are "essential". |
| Scan clip sound | muted | `InteractionUI.cs:198` `audioOutputMode = None` | A scan video can never play sound. |
| Design conflict | open | `03_GAME_DESIGN.md:77` "Voices only in the cutscenes". But in-game barks and a death "voice line" (`:48`) exist | Bruno must say if the barks get voiced or stay text. |

## 4. Sound effects

### 4a. Events that play a sound

| Event | State | Evidence | Clip source |
|---|---|---|---|
| Enemy aim start (pistol/rifle/launcher) | built | `EnemyGun.cs:294-303` | Invector paid (`LightWeaponAim.mp3`, `MediumWeaponAim(B).mp3`), `InvectorEnemyFactory.cs:512,532,533` |
| Enemy shot (pistol, rifle, launcher, rocket) | built | `EnemyGun.cs:330-341` | Invector paid (`LightWeaponShot*.mp3`, `MediumWeaponShot*.mp3`, `PumpShotgun@Fire*.mp3`, `RPG@Fire*`), `InvectorEnemyFactory.cs:511,531,540,541` |
| Melee swing / hit | built | `EnemyGun.cs:351` | Invector paid (`slash_A-C.wav`, `PunchA/B`, `Kick`), factory `:534-535` |
| Shield block | built | `EnemyShield.cs:76` | Invector paid (`Block 1/2.wav`), factory `:536` |
| Frag grenade / rocket burst | built | `EnemyGrenade.cs:181`, `EnemyRocket.cs:191` -> `ShotEffects.cs:146` | Invector paid (`Explosion_A.wav`), factory `:539` |
| Gas grenade pop | built | `GasCloud.cs:67` | Invector paid (`grenade-explosion-sfx-medium-sized.flac`), factory `:542` |
| Bullet hit on room (per surface) | built | `ShotEffects.cs:105-123` | Concrete: synthesised by `tools/impact_sfx.py`. Metal/wood/glass/soft: Kenney CC0 (`Audio/Impacts/Kenney_License.txt:10`) |
| Chip on intact breakable | built | `DestructionFx.cs:64-67` | Surface set above, else `chip.wav` from `tools/destruction_sfx.py` |
| Breakable break (5 kinds) | built | `DestructionFx.cs:40-42, 85-102` | `tools/destruction_sfx.py` (ours, synthesised) |
| Lamp pop / sparks | built | `BreakableLight.cs:46` -> `DestructionFx.cs:72-75` | `tools/destruction_sfx.py` |
| Short start scream (Lucy) | placeholder | `ShortController.cs:44,108` | ffmpeg tone (`Resources/Audio/lucy_scream.wav`) |
| Short shock zap | placeholder | `ShortController.cs:45,171` | ffmpeg (`Resources/Audio/short_zap.wav`) |
| HUD warnings (low HP, low MK, high SHORT) | placeholder | `PlayerHud.cs:166-170, 457-459, 527-541` | Sine beeps made in code (880 / 620 / 1180 Hz) |
| Death screen voice | placeholder | `DeathMenuUI.cs:149` | None assigned (see §3) |

### 4b. Events that are silent (checked: none of these files has AudioSource, AudioClip or PlayOneShot)

| Event | State | Evidence (the silent code) |
|---|---|---|
| Lucy footsteps (walk, run) | missing | `Player/TankController.cs` |
| Enemy and civilian footsteps | missing | factory adds `vControlAI` only (`InvectorEnemyFactory.cs:177-182`), no `vFootStep`. No footstep code in `Assets/_Game/Scripts` |
| Dodge | missing | `Player/TankController.cs:34-42` |
| Power charge (circle fill), per power | missing | `Player/PsychicPowers.cs`, `Player/PowerDefinition.cs` (no clip field) |
| mk_warspite release / hit | missing | `Player/PsychicPowers.cs` |
| mk_erebus ignite, part-charge flash, burning loop | missing | `Player/PsychicPowers.cs`, `Combat/BurningStatus.cs` |
| mk_orzabal lift, hold, throw, slam, "TOO HEAVY" | missing | `Combat/TelekinesisLift.cs`, `Player/PsychicPowers.cs` |
| Bullet / grenade / rocket catch and return | missing | `Player/PsychicPowers.cs`, `Combat/TelekinesisLift.cs` |
| MK boost (x2) on | missing | `Player/PsychicPowers.cs` |
| Short: hum during the 16 s, end | missing | `ShortController.cs` (study `2026-10-25_study_wall_lean_shock_pain.md:57` proposed a hum. Not built) |
| Lucy hurt, Lucy death, second wind | missing | `Combat/HitEventBus.cs`, `Gore/BloodFeedbackController.cs`, `PlayerHud.cs` (second wind is text only) |
| Bullet hit on flesh (Lucy or enemy) | missing | `ShotEffects.Impact` is for the room only |
| Enemy hurt / death cry | missing | `Enemy/SoldierBrain.cs`, `Enemy/CivilianBrain.cs`, `Integration/Invector/InvectorEnemyBody.cs` |
| Civilian scream (the alert) | logic only | `CivilianBrain.cs:161` raises `EnemyAlert` "scream". No clip, no subtitle |
| Gore: head burst, limb cut, blood pool | missing | vendor gore prefabs have no AudioSource (grep). Vendor gore `.wav` files exist but nothing plays them. `GruesomeDeathFx.cs:159` turns sources off |
| Ragdoll fall / body thud | missing | `Enemy/EnemyRagdoll.cs` |
| Prop crate throw / break | missing | `Combat/PropPiece.cs` has no sound call. Only a `Breakable` plays `DestructionFx` |
| Grenade bounce, rocket flight loop, gas hiss | missing | `Enemy/EnemyGrenade.cs`, `Enemy/EnemyRocket.cs`, `Enemy/GasCloud.cs` (one pop only) |
| Telegraph laser / ring | missing | `Enemy/DummyShooter.cs` (the aim click is the only cue) |
| Item pickup, "Take it?" prompt | missing | `Interaction/ItemPickup.cs`, `UI/InteractionUI.cs` |
| Door locked / door open | missing | `Interaction/LockedDoor.cs` |
| Log open / read | missing | `Interaction/Interactor.cs`, `UI/InteractionUI.cs` |
| Scan start / end, scan video sound | missing | `Interaction/PsychicScanner.cs`, `InteractionUI.cs:198` |
| Inventory open / close / use, drug use | missing | `UI/InventoryUI.cs`, `Player/DrugInventory.cs` |
| Save machine, save done, checkpoint | missing | `Saving/SavePoint.cs`, `Saving/CheckpointPoint.cs`, `UI/SaveMenuUI.cs` |
| UI navigate / confirm / back, pause, options | missing | `UI/*MenuUI.cs`, `OptionsMenuUI.cs` |
| Death screen sting | missing | `UI/DeathMenuUI.cs` (only the null voice clip) |
| Tutorial step done | missing | `Tutorial/TutorialDirector.cs` |
| Room ambience (hum, vents, drips) | missing | no looping AudioSource in any scene builder |

## 5. Placeholders and fallbacks

| Item | State | Evidence | Note |
|---|---|---|---|
| `lucy_scream.wav` | placeholder | `ShortController.cs:44`. `devlog-notes/2026-10-25_slide_and_migraine.md:23,49` | ffmpeg tone with vibrato, "not a real voice". |
| `short_zap.wav` | placeholder | `ShortController.cs:45`. `devlog-notes/2026-10-25_poses_short_pain.md:14,57` | ffmpeg. |
| Breakable sounds (7) | placeholder | `tools/destruction_sfx.py:2-5`. `DestructionFx.cs:9`. `devlog-notes/2026-10-24_breakable_cover.md:78,142` | Noise and decaying tones. |
| Impact sounds (concrete + Kenney) | placeholder | `devlog-notes/2026-10-25_surface_sounds_gore_saves.md:37` "All are placeholders until the final sound effects" | |
| HUD beeps | placeholder | `PlayerHud.cs:526` "the placeholder warning sound" | |
| Shock / retreat barks | placeholder | `SoldierBrain.cs:312,338` | Words, not clips. |
| Tutorial presenter lines | placeholder | `TutorialDirector.cs:28`. `devlog-notes/2026-10-22_tutorial.md:89` | |
| Death voice | placeholder | `devlog-notes/2026-10-20_step1.md:40` "no voice clip" | Caption shows instead. |
| Placeholder when a music track is missing | missing | no music system | Nothing to fall back from. |
| Fallback: impact per surface | built | `ShotEffects.cs:112` uses `impactClips` when a surface has no set | |
| Fallback: chip | built | `DestructionFx.cs:67` | Surface sound, else `chip.wav`. |
| Null-safe: scream, zap | built | `ShortController.cs:236` returns false if missing | Silent, no default clip. |
| Null-safe: explosion, gun, beeps, death voice | built | `ShotEffects.cs:146`, `EnemyGun.cs:303,340`, `PlayerHud.cs:489`, `DeathMenuUI.cs:147` | Silent, no default clip. |
| Null bug: gas pop | broken | `GasCloud.cs:67` -> `ShotEffects.cs:182-184` reads `clip.name` with no null check | If `gasClip` is null (a clone without the Invector folders), a gas grenade throws a NullReferenceException. Other `PlayAt(AudioClip[])` calls are safe (`:176-178`). |
| Missing-vendor behaviour | fact | `Resources/Effects/shot_effects.asset` is in git and points at 30+ Invector GUIDs (aim, fire, melee, block, explosion, gas, impact prefabs) | On a clean public clone every enemy weapon is silent. No "missing clip" default or warning at run time. |

## 6. Licensing

| Item | State | Evidence | Note |
|---|---|---|---|
| Audio files in git | clean | `git ls-files` audio: `Assets/_Game/Audio/Impacts/*` (4 ours + 12 Kenney CC0), `Assets/_Game/Breakables/Audio/*` (7 ours), `Assets/_Game/Resources/Audio/*` (2 ffmpeg) | No `.wav/.ogg/.mp3/.flac` from a paid folder. |
| Other media in git | clean | `Assets/_Game/Scan/scan_placeholder_video.webm` (ffmpeg card, `devlog-notes/2026-10-22_interact_scan.md:24`) | |
| Vendor folders in git | clean | `git ls-files` finds no Invector, ThatIndividual or Middleware path. `.gitignore:113-122` | `Assets/SkinGore/` is in git with its `LICENSE.txt` (MIT, per memory notes). |
| Kenney licence | present | `Assets/_Game/Audio/Impacts/Kenney_License.txt:10` CC0 | Credit optional (`:14`). |
| Vendor references in tracked asset | note | `Resources/Effects/shot_effects.asset` (GUIDs only, no audio data) | Not a licence leak. It is the silent-clone problem in §5. |
| Shipping the vendor clips | note | Invector clips play in the local build | The Asset Store licence allows them in a built game. Only the repo must stay clean. |

## 7. 3D audio and the listener

| Item | State | Evidence | Note |
|---|---|---|---|
| AudioListener place | built | `MenuLoopSetup.cs:350` on "Main Camera". One listener in each scene in git (`LookTest`, `LookTest2`, `Main_Menu`, `TrainingRoom`, `Tutorial`) | |
| Listener moves with camera cuts | fact | `FixedCameraDirector.cs:160` `SetPositionAndRotation` on `Camera.main` | The listener jumps to each fixed camera. It sits metres from Lucy. Left/right swaps when a cut faces the other way. |
| Shot one-shots | 3D | `ShotEffects.cs:189-193` blend 1, min 1.5 m, max 40 m, Logarithmic, doppler 0 | Loudness depends on camera-to-source distance, not Lucy-to-source. |
| Gun source | 3D | `InvectorEnemyFactory.cs:384-388` same values | |
| Breakables | 3D | `DestructionFx.cs:101` `PlayClipAtPoint` (Unity default: blend 1, min 1, max 500, Logarithmic) | Different curve from the guns. |
| Lucy's scream | 3D | `ShortController.cs:108` at her head, blend 1. `:243-246` min 1.5 m, max 30 m | Her own voice gets quieter when the camera is far. With log rolloff, a camera 8 m away gives about 0.19 of full level. |
| Zap | mostly 3D | `ShortController.cs:171` blend 0.8 | |
| HUD beeps, death voice | 2D | `PlayerHud.cs:168` blend 0. `DeathMenuUI.cs:93` default source (2D) | |
| Mix of curves | fact | three rolloff setups above | No shared audio settings object. |

## Ranked issues, worst first

1. **No music at all.** No player, no tracks, no states, no per-room choice, no fallback. The Music slider does nothing (`SettingsManager.cs:67-75`, no reader). The signals to drive it exist (`EnemyAlert.OnAlert`, `SoldierBrain.Alerted`, `DummyShooter` aim events), but nothing tells "calm again" and `GameManager` has no state-change event.
2. **No voice acting in the game.** Every line is a caption. No clip path exists for barks, subtitles or the Timeline track. The death screen has a clip field, but all 14 entries are null. The writer CSVs have no clip or line-id column. There is no cutscene player, and the design says voice acting is essential (`03_GAME_DESIGN.md:10-11`).
3. **On a clean clone, enemy weapons are silent, and the gas grenade throws an exception.** `shot_effects.asset` points at paid Invector clips that are not in git. `GasCloud.cs:67` reaches `ShotEffects.cs:184` with no null check.
4. **The listener is on the fixed camera.** Lucy's own sounds (the scream, later her steps and powers) change loudness and side at every cut (`FixedCameraDirector.cs:160`, `ShortController.cs:243-246`).
5. **Most player-side events are silent.** Footsteps, dodge, the charge and release of all three powers, lift/throw/slam, the catch, hurt and death, pickups, doors, scan, inventory, drugs, saves, checkpoints, UI and menus (§4b).
6. **The volume options are mostly fake.** No mixer. SFX changes only the HUD beeps. Music changes nothing. No Voice slider. The beeps apply master twice (`PlayerHud.cs:492` with `SettingsManager.cs:332`).
7. **Pause does not pause audio.** No `AudioListener.pause` (pause, inventory, game over set only `timeScale`).
8. **Most written barks are not in the game.** Spots / aims / fires / hit / reload lines in `barks.csv` have no code. The shock and retreat barks are placeholder words (`SoldierBrain.cs:313,338`). Lines are C# literals, not read from the CSV.
9. **No room ambience and no gore sounds.** No looping source in any room. Head burst, limb cut, ragdoll fall and flesh hits make no sound.
10. **Every sound that exists is a placeholder** (ffmpeg scream and zap, synthesised breaks and concrete, sine beeps, Kenney impacts). The SFX source is still OPEN (`RFAB_GAPS.md` row 14). The scan video is muted by code (`InteractionUI.cs:198`).
11. **Open design question.** `03_GAME_DESIGN.md:77` says voices only in cutscenes. The game has spoken in-game barks and a death voice line. Bruno must decide if barks get voiced.

Licensing is clean: no paid audio is in git (§6).
