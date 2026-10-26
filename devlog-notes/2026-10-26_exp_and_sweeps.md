# 2026-10-26: the experience levels, three sweeps, the exterior light

Bruno's words: "we'll need to test out experience points; program them in and test. we also need another sweep to
see if there's any issues with audio and music (i.e voice acting, and we'll have a music for combat, tension,
ambient for sectors/environments, and placeholders in case there's a gap), as well as sweep to see if there's
anything left to prototype from the design document to here. we'll likely to have exterior envs. is the method
we're doing for illuminating scenes work for them fine? and then verify gaps between systems that can be done in
isolation, so we can verify it all and start doing the actual production demo".

His answers to the four points of 2026-10-25: keep, keep, no need, no need (see that note).

## 1. The experience levels (design §11.2)

The rule, his words: "civvies may give more exp than soldiers ... kill everyone and become stronger, or be merciful
and lose out on exp and obligatory fights become tougher".

| Part | What it does | Numbers (PROPOSED) |
|---|---|---|
| Exp for a kill | Only her kills pay: her powers, the short, a body or a prop she throws, a fire she lit, a grenade or a rocket she sends back. A death she did not cause pays nothing. A hit on a body that is already dead pays nothing. Each body pays once. | Unarmed civilian 50, armed civilian 40, heavy 60, soldier 20, guard 15. A figure with no rank (the training dummies) pays as a soldier. A prop pays nothing. |
| Levels | The total exp to reach a level: 100 for level 2, then each step 50 more than the one before. | Level 3 at 250, level 4 at 450, level 5 at 700, level 10 at most. |
| What a level gives | More maximum HP, given at once. More damage from her three powers (the blast, the fire, the lift, the slam, a throw). | +10 HP and +10 % damage a level. The short stays at 12 a shock (his number). A returned bullet keeps its own damage. |
| The HUD | "LV n" and a thin green exp bar over the HP bar. "LEVEL n" at the top for 2.5 s, with two rising tones (placeholder). | |
| The save | The exp is in the save. The level comes from it. An older save file loads at level 1. | |

The firemen of §11.3 are unarmed civilians, so the farm pays the civilian rate when they exist.

A rough count for the 40-minute demo: about 25 soldiers and guards and 10 civilians. Kill all of them: about 1,000
exp, level 6. Kill only the ones who fight: about 450 to 500 exp, level 4. Tune the numbers when the rooms exist.

Code: `Player/ExperienceRules.cs` (the numbers), `PsychicStats` (the exp, the level, the credit from the hit bus),
`PsychicPowers.DamageScale` (the MK boost times the level), `Health.DeathFrame` (the hit in the frame of the death is
the kill), `PlayerState.exp`, `PlayerHud` (the level line and the banner). No scene changed: every room already has
`PsychicStats` on Lucy.

Proof:

- EditMode `ExperienceRulesTests` (3): civilians pay more than soldiers and guards, the thresholds, the cap, the
  damage and the HP of a level.
- PlayMode `ExperienceTests` (3, the training room): her blast kill pays 20, a death she did not cause and a hit on
  the body pay nothing. Level 2 adds 10 HP and 10 % damage, and the HUD shows "LV 2" and "LEVEL 2". A save and a
  load give back the exp, the level and the maximum HP.
- PlayMode `CivilianTests.HerKillOfACivilian_PaysMoreExpThanTheGuard`: a lab worker pays 50, the guard 15.
- Shot: `~/glorified-body-renders/game/exp/2026-10-26/level_up.png`.

Not in the tutorial: exp is passive, so it has no step to teach. The tutorial targets pay exp too, but the tutorial
is a separate mode, and its exp goes nowhere.

## 2. The exterior light

The method of the rooms is a live hard direct light plus a baked bounce ("Baked Indirect", `MixedLightingMode.IndirectOnly`), and light probes for the figures. It works for an exterior with these changes:

| Part | Interior today | Exterior |
|---|---|---|
| The direct light | Hard spots (the additional lights, a 4096 atlas) | One directional sun, Mixed, live and hard. Its shadow is the main light shadow map (2048, 4 cascades, 40 m), not the spot atlas. Both are on in `PC_RPAsset` now. |
| The bounce | Baked from the spots, 10 texels a metre | Baked from the sun and the sky. The sun cannot move: the bounce is for one time of day. A night exterior is the same as an interior (lamps). |
| The sky | Near black ambient | A sky that lights the scene (it bakes into the lightmaps). For the 1999 look, a painted backdrop behind the fixed camera is the cheap path. |
| Shadow sharpness | Spot shadows, sharp | The sun shadow spreads 2048 texels over 40 m. A fixed camera sees a known area. Set the shadow distance for each camera (about 15 to 25 m) for sharp edges. |
| Bake time | Minutes for a room | 10 texels a metre on a big yard gives many lightmaps on the CPU baker. Give the far ground and walls a low lightmap scale, and bake only what the cameras see. |
| Depth | None | URP fog for the distance. |

Terrain: the template turned off the Terrain modules on 2026-10-20. Warspite's module list fixed it, so terrain is
available. Meshes are better for the 1999 look.

No test proves this yet. An exterior look test is about half a day: a yard with the sun, a sky, two fixed cameras, the bake
and the shots.

## 3. The audio and music sweep

Full report: `devlog-notes/2026-10-26_sweeps/audio.md` (an agent's read of the code, checked by me where marked).

| Area | State |
|---|---|
| Music | None. No player, no track, no ambient, tension or combat state, no track for each sector, no fallback. The Music slider is saved, but no code reads it (checked). |
| Voice | None. Every spoken line is text with a subtitle. Only the death screen has a clip field, and it is empty. The writer's `barks.csv` has no clip column and no line id. The game does not read it: the barks are in the brain code. |
| Mixing | No AudioMixer. Master sets `AudioListener.volume`. The SFX slider reaches only the HUD beeps. No Voice slider. |
| Pause | A pause or the inventory does not stop the sound (checked: no `AudioListener.pause`). |
| Listener | On the fixed camera. Each camera cut moves it, so her own sounds change side and level. |
| Silent events | Footsteps, the dodge, the charge and the release of each power, the lift, throw, slam and catch, her hurt and death, flesh hits, falls, pickups, doors, the scan, the inventory, the drugs, saves, menus, room tone. The scan clip plays muted. |
| Placeholders | All sounds that exist: the scream and the zap (ffmpeg), concrete hits and breaks (code), the beeps (sine), the other hits (Kenney CC0). The sound source is OPEN (RFab gap 14). |
| Licence | Clean. No paid audio in git. The enemy gun sounds are Invector clips, outside git. A clone compiles only after the vendor import, so they are always there. |

A first sound system (about 1 day): an AudioMixer with Master, Music, SFX and Voice groups and the sliders on them,
a music director with the states Ambient (each room or sector names its track), Tension (an enemy hunts her) and
Combat (an aim or a hit, back to Tension after 8 s), crossfades, and a placeholder track for every state that
has no track. A voice player that takes a line id, plays the clip and shows the subtitle. With no clip, it shows only the
subtitle. The listener on Lucy. The pause stops the sound. Then the silent events one family at a time.

## 4. The design sweep: what is not prototyped

Full report: `devlog-notes/2026-10-26_sweeps/design.md`. The combat sandbox is almost complete. The demo lacks
the parts that join rooms into a game. I checked three claims in the code. No exit in a room loads another room. Nothing sets a story flag. No gameplay code reads the three difficulty modifiers.

| Rank | Missing piece | Size |
|---|---|---|
| 1 | Travel from room to room, and the world state of every room (the dead, the items, the doors). Lucy is a scene object: a new scene resets her bars, items and exp. The save holds only the current room. | 12 to 20 h |
| 2 | Scripted events and story flags: a trigger, a small sequencer, doors and spawns that a flag controls | 12 to 16 h |
| 3 | Voice lines and the PA (see section 3) | 4 to 6 h |
| 4 | The cutscene player: stills, voice, one video cut, a skip key | 8 to 12 h |
| 5 | How a room with Invector enemies lives in a public repo, and a room kit | 6 to 10 h |
| 6 | Enemy kinds: the knife scientist, the armed lab worker, a figure that carries a key item | 8 to 12 h |
| 7 | The outfit change in the game (the guard uniform and its model) | 4 to 6 h |
| 8 | The map screen | 6 to 10 h |
| 9 | In-world screens and audio logs | 4 to 6 h |
| 10 | NPCs that stay put, the scan on a person | 4 to 6 h |
| 11 | Fire in rooms, the alarm, sprinklers, firemen | 16 to 24 h |
| 12 | Difficulty percentages, and the three modifiers (they do nothing today) | 4 to 8 h |
| 13 | One-stat drugs, the second wind pill | 2 to 3 h |
| 14 | The skewer | 4 to 6 h |
| 15 | The floor-chunk shield | 8 to 12 h |
| 16 | A player build: a build script, the debug keys and the test-room buttons off | 4 to 6 h |

## 5. The gaps between systems

Full report: `devlog-notes/2026-10-26_sweeps/gaps.md` (39 seams). I checked the worst claims in the code. Then I wrote a test for each one
and saw each test fail before the fix (Bug Fact-Check: CONFIRMED).

| Seam | Before | Fix | Test |
|---|---|---|---|
| A pause or the inventory during the short's 2.5 s frozen start | The pause stored the frozen time scale of 0. The freeze routine quit. The resume put back 0: the game stopped for good (the test read 0.0 after 5.5 s). | The freeze waits during a pause or the inventory and goes on after. A game over or a load still ends it. | `SystemSeamTests.APause_...`, `TheInventory_...` |
| A drug in the 0.45 s between her death and the death screen | The drug brought her back (`IsDead` false) under the death screen. | `UseDrug` refuses when she is dead. The drug is not spent. | `ADrug_InTheDeathBeat_DoesNotBringHerBack` |
| A save of a room that is not in the build (a renamed room) | The load wrote the checkpoint, then stayed in Loading. | The load refuses with a warning, and the checkpoint stays. | `ASaveOfAMissingRoom_RefusesToLoad_AndKeepsTheCheckpoint` |

Not fixed, they need a ruling or the travel system (see the report for the evidence):

- A civilian kill gives no SHORT and no kill count. Only `DummyShooter` reports a kill, and civilians have none. The
  exp counts it.
- 70 PlayMode tests need the three local rooms (AI TEST, COVER TEST, CIVILIAN TEST), which are not in git. A clean
  clone fails them.
- A short started while she holds an enemy in the air leaves it there for 16 s.
- The second wind's 2.5 s runs on real time, so a pause uses it.
- No room has soldiers, civilians, cover, items and a save machine together. The first demo room is the first test of
  all of them at once.

## Proof of the day

- Before the seam fixes: EditMode 49/49, PlayMode 193/193 (the flaky gas test passed), the six bots CLEAN.
- The seam tests: 4 of 4 failed before the fix, 51 of 51 passed after (the seam tests plus the save, powers, menu and
  exp suites).
- After the seam fixes: PlayMode 197/197, the six bots CLEAN (the tutorial bot uses the short's freeze). The cover
  bot's two warnings are the soldier drift warning, also in the runs of 2026-10-24 and 2026-10-25.

## Open for Bruno

1. The exp numbers (section 1), PROPOSED.
2. Should a civilian kill fill the SHORT bar? Design §2.7 says "every kill".
3. The sound system of section 3: build it now with placeholders? About 1 day.
4. The exterior look test of section 2: build it now? About half a day.
5. The order for the demo: section 4, ranks 1 to 5 first?
