# 2026-10-25, late: the slide down the wall, the 2.5 s migraine

Bruno's answers:

1. The slide down the wall before the beg: "yes".
2. The start of the short: "it should be 2.5 s; the world freezes as the main character plays an anim of pain on the
   head; the player char will also play a scream sound. the shock is their power going out of their control".

## 1. The slide down the wall

- A civilian on its wall (the lean) slides down to the floor when she comes within 5 m. The slide is a 0.8 s blend
  from the self-hug to the sit (`PoseOverride` can now blend one clip into the next).
- Within 3 m it begs from the floor: the beg lines, its back still on the wall.
- When she goes past 5.5 m, it stands up the wall into the lean again (the same blend back).
- The sit is the RFab preset `hurt`: on the floor, the knees up. It needs no leg IK.
- A wary civilian on a wall does the same. It has no step back left, so it stays on its wall.

## 2. The migraine at the start of the short

- The world freezes for 2.5 s (`ShortController.StartFreeze`), then comes back over 0.2 s. Measured: 2.69 s.
- Lucy holds her head through the freeze. The clip is the RFab preset `frustrated_02` on Lucy, from 0.5 s to 3.0 s:
  a hand on the head, then the body hunches over it (frames checked). `PoseOverride` can now play a segment of a clip.
- She screams: `Resources/Audio/lucy_scream.wav`, a placeholder made with ffmpeg (a voice-like tone with vibrato, the
  "ah" vowel peaks, breath noise, a fall at the end). The real sound waits for the sound source (RFab gap 14).
- No shock during the pain. The first shock comes after the freeze: the power goes out of her control. The 16 s of the
  short start then.
- A key press during the freeze is lost (the game skips its input while the time stands still). The bots wait for the
  world before they press a key (`BotClock.WaitWorld`).

RFab: `frustrated_01`, `frustrated_02` and `hurt` on Lucy (model `ce7c93c2-…`), 30,000 tokens ($0.60). Sheets:
`~/glorified-body-renders/game/short/rfab_pain/` (`pain_sheet.png`, `frustrated_02_frames.png`). `frustrated_01` (a
stamp of the foot) is not in use.

## Proof

- The full run: EditMode 43/43, PlayMode 168/168, and the six bots pass (ai, cover, civilians, tutorial,
  structural, look). The AI bot logs 0 warnings.
- The civilian bot, 12 of 12. The new step `civ_slide_beg` passes: it sat, then begged from the floor. The short step
  checks the freeze, the scream, no shock during the pain, then the arc, the zap and the stun.
- New tests: `ShortStart_FreezesTheWorld_2_5s_LucyHoldsHerHead_AndScreams_ThenTheShocks` and
  `OnTheWall_SheComesNear_ItSlidesDown_ThenBegsFromTheFloor`.
- The structural bot's slowest frame is 650 ms. The earlier runs gave 477 to 493 ms. I did not find the cause yet.
  The first load of the new clips and sounds at the short's start is my first suspect.
- Shots: `~/glorified-body-renders/game/civilians/2026-10-25_late/`. `civ_08b` shows the slide (half-way down),
  `civ_08c` the beg from the floor, and `civ_09` and `civ_09b` Lucy's head pain in the frozen world.

## Open for Bruno

1. The scream is a synthetic placeholder. It is not a real voice. Do you want a recorded scream from a free sound
   library now, or do you wait for the sound source decision?
2. The beg from the floor uses the sit clip, with the hands low. Do you want the hands up in a plea (a pose by code,
   about 1 h)?
