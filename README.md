# THE GLORIFIED BODY (the game)

A tank-control horror game in Unity 6, a refinement of Galerians (PS1, 1999). Lucy, a young adult woman with no
memory and three psychic powers, escapes the cult's lab. Steam, PC, keyboard and mouse.

## Status

Build order steps 1 to 3 (2026-10-20): the Unity project, the menu loop (main menu, pause, options with key
remap, difficulty presets and modifiers, death screen), the batch harness and its tests, tank controls with a dodge,
fixed cameras per room, the auto-lock with a target switch, the circle charge with the three powers, the HP /
NALCON / SHORT bars, the short, the drugs, and a dummy shooter with the telegraph. The cast (2026-10-21): the nine
character models of the demo are in the greybox room, Lucy on the tank controls with idle, walk and run, the
guard and the soldier as the dummies, the other six along the north wall.

Next: step 4, the look test (one room greyboxed, then dressed two ways).

## The characters

The models come from the RFab 3D Studio (T19, `~/glorified-body-renders/T19_characters/models/`), 2,500
triangles each, a mixamo rig, idle, walk and run. `tools/characters.sh` turns each one into an FBX plus a 1024 px
base colour under `Assets/_Game/Characters/Models/` (Blender 5.2 in the background), and measures the travel of
each clip into `<class>.clips.json` (`tools/characters_clips.py`). Then
`tools/unity.sh exec GlorifiedBody.Editor.Characters.CreateCharacterAssets.BuildAll` makes the materials, the
animator controllers, the prefabs, the catalog in `Resources/Characters/` and rebuilds the scenes. The FBX, the
base colours and the measurements are in the repository, so a clone needs no Blender. The walk and the run of
T19 travel across the floor in the source: the import reads the measurement and gives that travel to the root
motion, which the Animator does not apply, so every clip plays in place (an EditMode test checks each FBX, a
PlayMode test samples each class). The single-pose states (dodge, aim, stagger, fall, death) use Invector's
humanoid clips. Without the Invector package they play the idle.

## Open the project

1. Unity 6000.3.15f1, URP.
2. The paid Asset Store packages are not in this repository. Import them before you open the project, in this
   order: Invector Third Person Controller (Shooter Template), Invector FSM AI Template, Gore System URP
   (That Individual). Without them the gore and Invector bridge scripts do not compile.
3. Open the folder in the Unity Hub. Press Play in `Assets/_Game/Scenes/Main_Menu.unity`.
4. In the training room: W/S walk, A/D turn, Shift runs, Space dodges, Tab switches the lock target, hold LMB to
   charge the selected power on the target, RMB is the defensive use of the power (TELEKINESIS: a bullet near
   her goes back at the shooter). With an enemy in the air, turn to aim and press LMB again to throw it.
   1, 2, 3 select BLAST, FIRE, TELEKINESIS. Q uses a drug.
   Space plus a direction key, or a double tap on a direction key, dodges that way. With a full SHORT bar, LMB or
   RMB starts the short: 16 s of shocks, she walks slowly, no run, no dodge. F4 kills Lucy (shows the death
   screen). F5 fills the SHORT bar, F6 starts the short, F7 refills her and gives drugs, F8 and F9 take and give
   25 nalcon. F1 hides the status line. Esc pauses.

## Batch tools (no editor open)

All Unity runs go through `tools/unity.sh`. It has a RAM gate, one batch slot for the machine, and a log triage.
Each run writes `runs/<id>/` with `editor.log` and `summary.json`.

| Command | What it does |
|---|---|
| `tools/unity.sh compile` | Open, compile, quit. CLEAN or DIRTY. |
| `tools/unity.sh test editmode` | NUnit EditMode tests. |
| `tools/unity.sh test playmode` | The play-mode tests (the menu loop). |
| `tools/unity.sh exec GlorifiedBody.Editor.MenuLoopSetup.BuildMenuLoopScenes` | Rebuild the two scenes from code. |
| `tools/unity.sh exec GlorifiedBody.Editor.CreateDifficultyModules.CreateAll` | Rebuild the difficulty assets. |
| `tools/unity.sh play TrainingRoom Main_Menu structural 300` | The bot plays the menu loop. Writes `playthrough.json` and shots. |
| `tools/unity.sh shots TrainingRoom` | Still frames from the ShotPoints of a scene. |
| `tools/unity.sh sanity TrainingRoom` | Static checks of a scene. |
| `tools/unity.sh import <file.unitypackage>` | Import an Asset Store package headless. |

Close the Unity editor before a batch run. The wrapper refuses to start when an editor holds the project.

## Layout

| Path | Content |
|---|---|
| `Assets/_Game/Scripts/<Subsystem>/` | Game code, namespaces `GlorifiedBody.<Subsystem>`. Only `Integration/Invector` may use `Invector.*`. |
| `Assets/_Game/Scenes/` | `Main_Menu`, `TrainingRoom`. Built by `MenuLoopSetup`, never hand-edited. |
| `Assets/_Game/Tests/` | EditMode and PlayMode tests. |
| `Assets/_Game/Resources/` | Difficulty modules, death screen content. |
| `tools/` | `unity.sh`, `log-triage.py`, `log-noise.txt`. |
| `tools/settings-baseline/` | Known-good copies of the project settings files. The wrapper restores one when a Unity run or a package import resets it. |
| `devlog-notes/` | What was built each step, for the weekly devlog. |

Design canon and story live outside this repository (Bruno's design folder). The code of the menus, the remap,
the difficulty modules, the death flow and the harness comes from Project Warspite (the same author).

## License

GPL-3.0 for the code in this repository. The Asset Store packages keep their own licenses and stay out of git.
