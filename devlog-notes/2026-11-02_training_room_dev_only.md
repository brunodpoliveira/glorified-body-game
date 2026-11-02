# 2026-11-02: the training room is dev only

Bruno:

```text
remove the training room, or set it to dev only
```

I set it to dev only. The room stays in the project, because the tests and the bots use it (the structural bot, the
menu loop tests, the save tests, and more).

## What changed

- `MainMenuUI`: the TRAINING ROOM button is the first button of the TEST ROOMS column. A release build hides that column.
  The main column is START, CONTINUE, LOAD GAME, TUTORIAL, HOW TO PLAY, DIFFICULTY, OPTIONS, CREDITS, QUIT.
- `PlayerBuild.ReleaseScenes`: a release has the menu, the tutorial and the Demo_* rooms. The training room scene is
  not in it. A dev build has every scene, as before.
- On 2026-10-31 Bruno said "Training room is for the players too". This ruling replaces it.

## Proof

| Check | Result |
|---|---|
| `PlayerBuildTests` (edit mode: a release has no training room, a dev build has it) | 2 of 2 |
| `PlayerBuildFlagTests` (a release menu has no TRAINING ROOM button, START loads W1; in a dev menu the button is in the test column and loads the room), `MenuLoopTests` | 11 of 11 |
| Bot `play TrainingRoom Main_Menu structural 300`, run `20261102-203246-play-322604` | pass |

Shot: `~/MEGA/glorified-body-renders/game/menu/main_menu_dev_2026-11-02.png` (the dev menu: the test column fits on the
screen with one more button).

The player builds in `builds/` still have the training room. Build them again before an upload.
