# 2026-11-15: the T44 combat track replaces the T43 kit (83 BPM)

Bruno picked a new combat track: "yes, hunted a works", then "yes, cut it". The track is Lyria "hunted a", cut into a
kit. The record: `~/MEGA/glorified/production/tests/T44_music_structure/README.md` (round 4). The director logic of
`2026-11-14_combat_music_kit.md` stays. The files and the tempo change.

## What changed

1. **The files.** `tools/music_kit_import.sh` now copies from
   `~/MEGA/glorified/renders/T44_music_structure/game/` into `Resources/Audio/Music/_demo/`. They are WAV copies, with
   one gain for all files.

   | Game file | T44 file | Bars | Samples | Level | Plays |
   |---|---|---|---|---|---|
   | `ambient.wav` | `combat_explore_loop.wav` (the theme, low-passed at 300 Hz, 6 dB down) | 12 | 1,665,542 | -21.1 dB | Exploration, loops |
   | `tension.wav` | `combat_search_loop.wav` (the theme with no drums, x0.8) | 12 | 1,665,542 | -18.9 dB | Search, loops |
   | `combat.wav` | `combat_theme_loop.wav` | 12 | 1,665,542 | -14.1 dB | Combat, loops |
   | `combat_break.wav` | `combat_break.wav` (the song's own drop-out) | 4 | 555,180 | -17.1 dB | Once, in the arc |
   | `combat_payoff.wav` | `combat_payoff.wav` (solo, build, climax, a dip at its end) | 24 | 3,331,085 | -13.9 dB | Once, in the arc |

   `combat_full.wav` stays out of the game (reference only).
2. **The tempo.** `MusicDirector.Bpm` is 83 (it was 84). One bar is 2.8916 s, 138,795.18 samples at 48 kHz.
3. **The payoff.** `MusicDirector.PayoffBars` is 24 (it was 14). The theme starts again from its bar 1 on the bar
   line after the payoff, as in the song.
4. **The same logic.** The arc starts after 12 bars of combat (`ArcAfterBars`, PROPOSED, now 34.7 s), or at
   `MusicDirector.StartArc()`. The break starts on the next bar line, the payoff 4 bars later, and the theme 24 bars
   after that. A full arc is now 28 bars (81 s).
5. **The states.** The three loops have the same length, to the sample. They lock with no slack now.
6. **The T43 kit** (84 BPM) stays in the git history: commit `b7e4ef7`.

## Tests

1. `MusicKitTests` (EditMode): the files are checked against `MusicDirector.Bpm` and `PayoffBars`. The bar-line test
   now uses the T44 loop length (1,665,542).
   - The first run failed one check, and the cause was in the test. It asked for the bar line 1 microsecond after 12
     bars. The T43 loop was 0.71 samples longer than 12 bars, so that time was still in the loop. The T44 loop is 0.17
     samples shorter, so that time is in the next pass, and the director gave the correct next bar line. The test now
     asks 1 microsecond before the end of the loop. It works for both lengths.
   - Then 3 of 3.
2. `MusicArcTests` (PlayMode): the wait for the return is now 150 s (the arc rides 28 bars). 4 of 4. The log: "break at bar 12,999999 of
   the theme, payoff +4,000000 bars, theme +24,000000 bars". The 0.000001 is the 0.17 samples the loop is short of 12
   bars: that start is the end of a pass.
3. The same run: `SoundSystemTests` 7 of 7, `MusicScopeTests` 1 of 1, the lock test of `HoldTests` 1 of 1 (the state
   changes are 1, -8 and 6 ms off). 13 of 13, CLEAN.
4. These runs compiled the uncommitted work of another session in this repo. Not run: the whole PlayMode suite and
   the bots (before a push). Not heard by ear: Bruno's check.

## Levels (for Bruno, not changed)

Combat (-14.1 dB) is now 4.8 dB over search (-18.9 dB). With T43 it was only 0.8 dB. Exploration is -21.1 dB. The
payoff (-13.9 dB) is the loudest part. Its last bars dip (the breakdown), so the theme comes back from a lower level.

## Open

1. Round 126 says D minor. T44 reads as an unclear key (C major 0.76, a minor key 0.71, in the T44 record). The
   writing chat updates `DEMO_PLAN.md` §12 and the ledger.
2. Not committed: this waits on Bruno's OK. Another session edits this repo at the same time. Commit only the
   music files.
