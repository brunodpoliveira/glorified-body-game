# 2026-10-31: in-world screens, and the tape key

The order from Bruno, after the map and the audio logs:

```text
1: yes
2: keep it that way for now, once we add the real music we'll see if it needs adjusting
3: yes
next: all of it, then check the animation errors.
```

His answers: 1, the save machines stay on the map. 2, the music stays at 8 % under a tape. 3, a key plays or pauses
a tape without the inventory. "All of it": the in-world screens, the outfit change, the floor shield and a seam sweep.
The words, the clip and the numbers are PROPOSED or placeholders.

## The tape key

| Part | Behaviour |
|---|---|
| The key | T (`InputManager.GameAction.Tape`, "Play / Pause Tape" in the remap list). It pauses a playing tape, and it plays a paused or ended tape. |
| A fight | During a fight the key does nothing (the fight paused the tape). After the fight, T plays it again. |
| The label | "TAPE PAUSED ... T: play", and after a fight "TAPE PAUSED: a fight ... T: play it again". |
| The inventory | It still plays and pauses a tape, as before. |

## The in-world screens

| Part | Behaviour |
|---|---|
| A screen | `InWorldScreen` (`Scripts/Interaction/`): one video player for one or more faces. The CRT wall of the gym is one screen with 24 faces. All faces show the same clip. |
| The sound | It comes from the screen, in 3D, on the Voice channel. The pause, the inventory, the map, a log page and a cutscene stop the clip and its sound with the world. The clip goes on after. |
| Subtitles | The lines of the clip show as subtitles at their times while she is within 12 m. |
| The music | While a screen speaks near her, the music drops to the tape level (8 %). |
| A tape | A screen that starts pauses a playing tape (two voices at once). T plays the tape again. |
| Start | Interact turns an interactive screen on and off at once, with no question (the new `InteractKind.Act`). A room trigger starts a screen through a sequence step (`StepKind.Screen`), once (the done flag is in the saves). |
| No clip | A screen with lines and no clip runs its lines on its own clock and lights its faces green. This is the greybox card of the demo plan ("in greybox, a card with the subtitles"). |
| Loop | A looping screen (the orientation program of G7) plays until she turns it off. |

The placeholder clip: `tools/screen_placeholder.sh` makes `Assets/_Game/Screens/screen_placeholder.webm` with ffmpeg
alone (a green card, a moving band, a timecode, and the flite robot voice that reads four lines). 20 s, VP8 and
Vorbis: the Linux editor has no H.264. The lines are in `ScreenTestSetup.Lines`.

## The SCREEN TEST room

`ScreenTestSetup.BuildScreenTest`, the button SCREEN TEST in the test column. The therapy gym with its door shut:

- The CRT wall plays the clip once when she walks into the middle of the room (the rehearsal tape of W7).
- A CRT on a cart by the west wall plays the clip in a loop on Interact.

A fault found on the way: the 24 CRT faces of the gym faced into their boxes since 2026-10-22 (a 180 degree turn that
the north wall does not have). No green cursor showed in the tutorial or in LOOK TEST 2. Fixed in
`LookTestSetup.BuildTherapyGym`. The tutorial and LOOK TEST 2 get the fix when they are built and baked again.

## Proof

| Check | Result |
|---|---|
| ScreenTests (5): the wall plays when she walks in, with its sound and lines, once; the clip stops with the world; the cart turns on and off and loops; a screen pauses a tape; a screen with no clip runs its lines | 5/5 |
| AudioLogTests (5, one new): the tape key pauses and plays, not in a fight | 5/5 |
| The bot: `tools/unity.sh play ScreenTest Main_Menu screens 300` | 4/4 steps |

Shots for Bruno: `~/MEGA/glorified/renders/game/screens/shots/`

| File | It shows |
|---|---|
| `screens_01_wall_playing.png` | the gym from its camera, the wall plays, the subtitle |
| `screens_02_wall_close.png` | the wall of 24 CRTs close up |
| `screens_03_cart_playing.png` | the cart's CRT from the room camera |
| `screens_04_cart_close.png` | the cart's CRT close up, Lucy in front of it |

## Open for Bruno

1. The subtitles of a screen show within 12 m. Is that right, or only in the same room?
2. A screen pauses a playing tape. Should the tape play on after the screen ends, by itself?
