# 2026-11-02: the corner map, in real time

Bruno:

```text
Map: does it mark interest points like locked doors, save points, etc? it also should work in real-time i.e opening
the map does not pause the game, and shows the player's location
```

His pick (from three: a see-through overlay, a full screen while she stands, a corner map): **the corner map, she
moves**.

## Before the change (the check of the code)

| Point | State on 2026-10-31 |
|---|---|
| Key places | YES: locked doors (red, green when open), items, logs, save machines, the doors out |
| Her place | YES: a dot and a line the way she faces, drawn once when the map opened |
| Real time | NO: the map stopped the world (`GameState.Map`, the time scale at 0), as built on 2026-10-31 |

## What changed

- The Map key (M) shows a map of 600 x 390 pixels in the top left corner (1080p). The game goes on: she walks, turns,
  fights. The Map key hides it. Back (S) walks her back, so it does not close the map.
- Her dot and its line move every frame.
- The key places draw again when they change: a door that opens turns green, a taken item goes. The map checks 4
  times a second and draws only on a change.
- Through a door the map stays on and shows her new room and floor.
- It hides under the pause, the inventory, a page or a scan, a cutscene and the death, and comes back after.
- The floor change: the arrow keys (two new keys you can change in OPTIONS > CONTROLS: "Map: Previous Floor", "Map: Next
  Floor"), because A and D turn her.
- `GameState.Map` is gone. The sound, the music, the subtitles and the impact frame had a case for it: removed.
- A small UI sound when the map shows and hides.
- The tutorial's map line: "{Map} shows the map in the corner, and you can still move." (PLACEHOLDER).
- Two tests used the map to stop the world (a tape, a screen clip). They use the inventory now.

## Proof

| Check | Result |
|---|---|
| `MapTests` (8, one new: she walks with the map on, her place follows her, the map hides under the inventory and stays on through a door; the door turns green with no new show; the arrow keys change the floor and D does not), `ScreenTests`, `AudioLogTests`, `RoomSeamTests`, `SoundSystemTests`, `SubtitleTests` | 40 of 40 |
| Edit mode, all | 61 of 61 |
| Bot `play TravelTest_A Main_Menu map 300`, run `20261102-191002-play-305783` (new step "real_time": she walked 1.92 m with the map on) | 9 of 9 |

Shots: `~/MEGA/glorified/renders/game/map/corner_2026-11-02/`. The bot shots are 1280 x 720, so the words are
smaller than in the game. `map_07_walking.png` shows the room and the corner map while she walks.

## Open for Bruno

1. The corner is the top left. The tape label is at the top right. Another corner?
2. The size: 600 x 390 at 1080p (a 31 % wide corner). Smaller or larger?
3. The keys line shows "LeftArrow RightArrow". Arrow signs instead?

The player builds in `builds/` do not have this change yet.
