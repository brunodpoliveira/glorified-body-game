# 2026-11-14: the combat music kit in the game (T43 v4, the drum break)

Bruno picked the T43 v4 arc the same day: the theme, a 4-bar drum break, then the payoff ("v4, drum break"). The quiet
violin break is out ("the quiet violin just destroys the energy of the song"). The record:
`~/MEGA/glorified/production/tests/T43_combat_kit/README.md`. Round 126 (CANON): one composition at 84 BPM in D
minor, three states cut from the same audio.

## What is in the game

1. **The demo's piece:** `Resources/Audio/Music/_demo/`, from `tools/music_kit_import.sh`. The files are WAV copies
   (no MP3, no re-encode). Unity compresses them on import: Vorbis in memory, quality 0.8, 48 kHz kept, loaded with
   the asset (`Editor/MusicImport.cs`).

   | Game file | Source | Bars | Plays |
   |---|---|---|---|
   | `ambient.wav` | T41 `complex/state_explore_loop.wav` | 12 | Exploration, loops |
   | `tension.wav` | T41 `complex/state_search_loop.wav` | 12 | Search, loops |
   | `combat.wav` | T43 `game/combat_theme_loop.wav` | 12 | Combat, loops (the theme) |
   | `combat_break.wav` | T43 `game/combat_drum_break.wav` | 4 | Once, in the arc |
   | `combat_payoff.wav` | T43 `game/combat_payoff.wav` | 14 | Once, in the arc |

   `combat_full_v4.wav` stays out of the game (reference only).
2. **Where the director looks:** the room's own folder, then `_demo`, then `_placeholder`. No demo room has its own
   folder, so every demo room plays the kit. The training room keeps its own `TrainingRoom/ambient.ogg` for
   exploration and takes the kit for search and combat.
3. **Every track starts on the DSP clock** (`AudioSource.PlayScheduled`, 0.1 s ahead). So the director knows the
   DSP time of the bar 1 of the loop that plays (`MusicDirector.Anchor`), and every bar line is that time plus n bars
   (one bar = 20/7 s = 137,142.86 samples at 48 kHz). A state change still starts the new state at the sample of the
   old one, now from the DSP clock and not from a main-thread read.
4. **The states still lock.** The T43 theme is 1,645,715 samples, the T41 states 1,645,714 (two roundings of 12 bars).
   `MusicDirector.SamePiece` now accepts a difference of 2 samples (`SamePieceSlack`).
5. **The combat arc (`MusicDirector.TryArc`):**
   1. The trigger (my choice, PROPOSED): **the fight lasts 12 bars** (`ArcAfterBars`, 34.3 s, one full theme). This
      is the place of the break in the v4 file Bruno picked. A scripted beat can also ask for it now:
      `MusicDirector.StartArc()`. Nothing calls it yet.
   2. The theme ends on its next bar line (`SetScheduledEndTime`). The drum break starts on that bar line. The payoff
      starts 4 bars later. The theme starts again from its bar 1, 14 bars after that. All three are scheduled at
      once, on the DSP clock, with bar math in seconds. No fade between the sections: the files have 5 ms at their ends.
   3. After the arc, the next arc waits 12 more bars of combat.
   4. **The fight ends in the arc** (PROPOSED): the sections that did not start yet stop. The section that plays fades
      out over the usual 2 s, and the new state fades in from its bar 1. If the fight ends before the break, the theme
      goes on and the new state locks to it as before.
6. The pool has five music sources now (the theme, the break, the payoff, the theme again, and one for a crossfade).

## Proof

1. Compile: CLEAN.
2. `MusicKitTests` (EditMode, new): 3 of 3. The five clips are 48 kHz stereo WAV, each on the 84 BPM grid to one
   sample, imported as set. The states and the theme are one piece. `NextBarLine` gives the first bar line after
   the lead, on the grid to one sample, at 401 times over three passes of the loop.
3. `MusicArcTests` (PlayMode, new, the training room): 4 of 4.
   - Search to combat keeps the beat into the kit's theme (one lock), and the theme is not the placeholder.
   - After 12 bars of combat, the break starts on a bar line of the theme (log: "break at bar 13,000005 of the
     theme, payoff +4,000000 bars, theme +14,000000 bars"). The 0.000005 is the 0.71 samples the loop holds over
     12 bars: that start is the end of a pass. Each section really sounds from its start time (position against
     the DSP clock, within one audio buffer read).
   - After the payoff, the theme plays from its bar 1, and its bar 1 is the bar line after the payoff.
   - A fight that ends in the break leaves the arc. The search state plays, the payoff never starts.
4. The music tests of the changed code. The first run found two failures and one inconclusive result:
   - `SoundSystemTests.TheMusic_IsTheRoomsAmbient_...` expected the placeholder for combat in the training room. The
     kit fills that gap now, by design. The test now expects the T43 theme (not a placeholder).
   - `SoundSystemTests.ThePause_...` took the first music source with the current clip, and it was a stopped one: with
     five sources, a stopped source kept an old copy of the clip. FIX: the director clears the clip of every source
     it stops (`MusicDirector.Release`).
   - `MusicScopeTests` was inconclusive by its own rule (the hunter walked next to Lucy). The scope code is the same.
   - The run after the fix: `SoundSystemTests` 7 of 7, `MusicScopeTests` 1 of 1, `MusicArcTests` 4 of 4, the lock test
     of `HoldTests` 1 of 1. Before the fix, in the same run: `AudioLogTests` 7, `BrainFightTests` 16, `EnemyAiTests` 22
     and `KitTests` 6, all passed.
5. The lock test of `HoldTests` read the position two frames after the change, before the scheduled start: 80 to
   103 ms off against its 120 ms limit. It now reads after `ScheduleLead`: 5 to 8 ms off.
6. Not run: the whole PlayMode suite and the bots (the cadence: before a push). Not heard by ear: Bruno's check.

## Levels (for Bruno, not changed)

The files keep the levels of the kit. The combat state is now the T43 theme at -16.4 dB. The T41 combat loop was
-14.2 dB. Search is -17.2 dB and exploration -23.6 dB. So combat is now only 0.8 dB over search (it was 3 dB). The
payoff (-14.3 dB) is the loudest part, as in v4.

## Open for Bruno

1. The trigger: 12 bars of combat (PROPOSED). Another choice: a set piece calls `StartArc()`, for example the heavy
   of the Dining Hall when he steps out (`Story/HeavyArrival`).
2. The 37 MB of WAV go into the public repo with the next commit. Not committed: this waits on his OK.
3. The writing chat updates `DEMO_PLAN.md` §12 (it still has the older music plans).
