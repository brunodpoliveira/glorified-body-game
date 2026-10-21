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
  Proof: 30 EditMode tests, 55 PlayMode tests, the bot (24 steps), see `devlog-notes/`.
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
  Next: step 4 in
  `03_GAME_DESIGN.md` §7 (the look test: one room greyboxed, then dressed two ways, for Bruno's pick).

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
