# 2026-11-15: the Dining Hall set piece music (G18), by event

Bruno picked take B of the Dining Hall track ("Still B") and asked to cut it ("Yes, cut it"). Round 104: the set
piece has its own music, and it replaces the three states while it runs. The record:
`~/MEGA/glorified/production/tests/T44_music_structure/README.md`, the last section. Take B is Lyria, D minor,
**165.00 BPM**. One bar is 1.4545 s, 69,818.18 samples at 48 kHz.

## The files

`tools/setpiece_import.sh` copies them (WAV, one gain for all) into `Resources/Audio/Music/_setpiece/G18/`.

| Game file | T44 file | Bars | Plays | Level |
|---|---|---|---|---|
| `entry.wav` | `setpiece_entry.wav` | 9 | Once: she goes through the glass doors and the squad sees her | -19.6 dB |
| `fight_loop.wav` | `setpiece_fight_loop.wav` | 56 | Loops (the flute solo from its bar 21) | -13.7 dB |
| `armour_entry.wav` | `setpiece_armour_entry.wav` | 8 | Once: the power armour comes in (three left) | -19.2 dB |
| `armour_loop.wav` | `setpiece_armour_loop.wav` | 16 | Loops | -13.1 dB |
| `end.wav` | `setpiece_end.wav` | 9 | Once: the last enemy is down | -19.9 dB |

`setpiece_full.wav` stays out of the game (reference only).

## How it plays

1. **The start.** `RoomScore` of G18 starts the set piece as before: she is in G18 and one of the squad is alerted.
   If `_setpiece/<room id>/` has the files, it calls `MusicDirector.SetPiece(kit)`. With no files, it plays the
   single track, as before.
2. **The entry** starts on the next bar line of the music that plays (the combat track, 83 BPM), and the music
   before it stops on that bar line. The fight loop follows on the bar line after the 9 bars of the entry. From here
   the set piece uses its own 165 BPM bars.
3. **The armour.** `HeavyArrival` raises its flag (the armour steps out) and calls
   `MusicDirector.SetPieceCue(SetPieceEvent.Armour)`. On the next set piece bar line the fight loop stops and the
   armour entry starts. The armour loop follows 8 bars later.
4. **The end.** At the last kill, `RoomScore` calls `SetPieceCue(SetPieceEvent.End)`. On the next set piece bar line
   the loop stops and the end section starts. On its last bar line (9 bars later) the normal states take the music:
   exploration, search or combat, from bar 1, at full level, with no fade.
5. **She leaves the room** (or a cutscene, or a death): `Hold(null)` as before. The sections that wait are dropped,
   and the section that plays fades out over 2 s, as any track. Back in the hall with the squad alive, the set piece
   starts again from its entry.
6. **A load after the armour came:** the entry goes to the armour loop (`RoomScore` asks `HeavyArrival`).
7. A cue that comes during a section that plays once (an armour cue in the entry, say) cuts that section on the next
   bar line too. PROPOSED.
8. While it runs, the music state is Score with a fight (`InFight`): a tape waits, as before.

The code: `Scripts/Audio/MusicDirector.SetPiece.cs` (new, `SetPieceMusic`, `SetPieceEvent`), `MusicDirector.cs`
(every source keeps its scheduled start and end, `CutAll`, the hand-back on a bar line, a pool of 7 sources),
`RoomScore.cs`, `Story/HeavyArrival.cs` (one line). The Demo scene does not change (no rebuild).

## Tests

1. `MusicKitTests` (EditMode): 5 of 5. Two new tests:
   - The five G18 files are 48 kHz WAV, each a whole number of 165 BPM bars to one sample (9, 56, 8, 16, 9).
   - `NextBarLine` at 165 BPM gives the next bar line on the 56-bar loop, at 301 times over two passes.
     `NextBarLineOnce` gives it for a section that plays once.
2. `SetPieceMusicTests` (PlayMode, new, the training room with the G18 files): 3 of 3.
   - The events in order: entry, fight loop, armour entry, armour loop, end. Each one is on the next bar line, and
     each one sounds from its start time. The log: "entry +0 bars, fight_loop +9,000000, armour_entry
     +10,000000, armour_loop +18,000000, end +19,000000; the entry at 1,000000 combat bars; the state at
     +28,000000 bars". So the entry starts on a bar line of the combat theme, and the state starts on the last bar
     line of the end section.
   - After a load with the armour in: entry, then the armour loop. A second armour cue does nothing.
   - She leaves the room during the entry: the set piece stops, and none of its files plays 2.5 s later.
3. The same run: `DemoTests` (the G18 hall test: its track plays while they see her, it stops when she leaves),
   `DiningHallTests` 3 of 3, `MusicArcTests` 4 of 4, `SoundSystemTests` 7 of 7, `MusicScopeTests` 1 of 1, the lock
   test of `HoldTests` 1 of 1. 20 of 20. One warning: a soldier aim drift in `SoldierBrain` (not music).
4. These runs compiled the uncommitted work of another session in this repo. Not run: the whole PlayMode suite and
   the bots (before a push). Not heard by ear: Bruno's check.

## Open for Bruno

1. The two tempos do not lock: 165 is not twice 83 (166). The entry starts on a combat bar line, and from there the
   set piece keeps its own grid. The hand-back to the states starts on the set piece's last bar line.
2. The levels: the entry, the armour entry and the end are 5 to 6 dB under the loops (as cut). The fight loop is
   0.4 dB over the combat theme.
3. Not committed: this waits on his OK. Another session edits this repo at the same time.
