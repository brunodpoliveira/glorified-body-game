# 2026-11-16: START asks for the tutorial first

Bruno:

```text
and also add a prompt when starting the game: play tutorial first (Recommended) yes/no. and add a flag to the user so we can know they done the tutorial, so as to not pester them if they did so already bvefore clicking on start game
```

1. START asks "Play the tutorial first?" until the player finished the tutorial once. Three buttons: YES (RECOMMENDED),
   NO, START THE GAME, and BACK. YES is selected first.
2. YES plays the tutorial, and at its end the game starts (the demo). NO starts the game. The next START asks again.
3. The flag (`Systems/TutorialRecord`): set at the end of the tutorial (`TutorialDirector.Complete`), kept in PlayerPrefs
   (`tutorial_done`). With it, START goes straight to the game.
4. The TUTORIAL button of the menu works as before: at its end, the end screen. It also sets the flag.
5. Batch runs keep their own key (`batch_tutorial_done`), and START in a batch run does not ask. So the tests never
   mark the editor's player as done, and the START test still loads the demo.

To see the question again in the editor, delete the PlayerPrefs key `tutorial_done` (Edit > Clear All PlayerPrefs).

NOT COMPILED, NOT RUN (no free RAM for Unity). New EditMode test: `TutorialRecordTests` (2).
