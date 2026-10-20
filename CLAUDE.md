# THE GLORIFIED BODY (the game): read first

A tank-control horror game in Unity 6, a refinement of Galerians (PS1, 1999). Lucy, a young adult woman with no
memory and three psychic powers, escapes the cult's lab. Started 2026-10-20 (the pivot from the video series).

- **Design (CANON):** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/03_GAME_DESIGN.md`.
- **World and story:** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/01_SERIES_BIBLE.md`.
- **Asset lessons:** `/home/pc/MEGA/code/reality-fabricator/the-glorified-body/tests/T15_3d_set/LESSONS_LEARNED.md`.
- **Agents:** `glorified-body-game` builds the game, `glorified-body-writer` writes the story, the cutscenes, the
  barks and the devlog scripts. Bruno decides.

## State

- 2026-10-20: step 1 done. The Unity project sits at the repo root (public GitHub repo, GPL-3.0). The menu loop
  (main menu, pause, options with key remap, difficulty presets + modifiers, death screen) and the training room
  greybox are built by code. The batch harness runs: 27 EditMode tests, 6 PlayMode tests, the menu-loop bot.
  The three Asset Store packages are imported. Next: step 2 in `03_GAME_DESIGN.md` §7 (tank controls, fixed
  cameras, auto-lock). See `devlog-notes/2026-10-20_step1.md`.

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
