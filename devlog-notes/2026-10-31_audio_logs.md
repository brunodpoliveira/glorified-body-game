# 2026-10-31: the audio logs

The order from Bruno:

```text
audio logs: it can be collected and listened to while walking around; it automatically pauses when combat begins;
music goes down to almost nothing
```

```text
1: yes once the player un-pauses it
2: yes
3: map first, then the audio logs
```

His answers: after a fight, she must un-pause the tape herself. She can also play a tape again from the inventory. The words, the voice and the numbers are PROPOSED or placeholders.

## What it does

| Part | Behaviour |
|---|---|
| The item | A key item of the new kind `AudioLog` (no slot), with a clip and its lines (`ItemDefinition.audioClip`, `tapeLines`). |
| Take it | The Interact key asks "Take the test tape?". A yes puts it in the inventory, and it plays at once. |
| While she walks | The world goes on: she walks, runs and fights. The tape plays in her ear (2D, the Voice channel). |
| Subtitles | Each line shows as a subtitle at its time, with the speaker ("TAPE: This is a placeholder tape."). |
| The music | While the tape plays, the music drops to 8 % (`TapePlayer.MusicLevel`). |
| A fight | The music goes to Combat when an enemy aims at her, or after a hit from her or on her. Then the tape pauses. Its subtitle goes, and the music comes back. |
| After the fight | The tape stays paused (Bruno: "once the player un-pauses it"). The label says "TAPE PAUSED: a fight. Play it again from the inventory". |
| The inventory | The tape's entry says AUDIO LOG. Interact plays it, or pauses it while it plays. It plays on from where it stopped. An ended tape plays from the start. |
| The label | At the top right. While it plays: "TAPE  TEST TAPE  0:02 / 0:23". When it stops: "TAPE PAUSED". After the end: nothing. |
| Stops with the world | The pause, the inventory, the map, a log page, a scan and a cutscene stop the tape with the world sounds. |
| Through a door | The tape plays on in the next room (the player lives on the sound system's object). |
| The map | An audio log is a log on the map (white). |
| A new game | The main menu stops the tape. |
| Tutorial | The new step "tape" comes after the map step. The tape lies on the stand of the record. She takes it, and it plays. The tutorial has 25 steps now. |

The tool `tools/tape_placeholder.py` makes the placeholder `Assets/_Game/Audio/Logs/tape_test.wav` (23 s: a click,
hiss and hum, a muffled voice-like sound under each line). The line times are in `ItemAssets.TestTape`. The writer
gives the words. The voices come with the voice work. TRAVEL A has the test tape (`Tape_Travel`).

## How it works

- `Audio/TapePlayer.cs`: one player for the whole game, on "[GameAudio]". Its own clock runs on game time, so the tape
  stops with the world. A batch run with no sound device keeps the same time. The sound follows the clock.
- `Audio/MusicDirector.cs`: the music's level drops while a tape plays (`DuckFactor`, for the tests).
- `Interaction/Interactor.cs`: a taken audio log plays. `UI/InventoryUI.cs`: play or pause.
- `Interaction/ItemPickup.Spawn`: a pickup made in play. The tutorial lays its tape this way, so the gym needs no rebuild.

## Proof

| Check | Run | Result |
|---|---|---|
| AudioLogTests (4), listed below | `runs/20261031-022558-test-playmode-864114` | 4/4 |
| The tape bot: `tools/unity.sh play TravelTest_A Main_Menu tape 300` | `runs/20261031-022757-play-865003` | 3/3 steps |
| EditMode | `runs/20261031-022856-test-editmode-865468` | 56/56 |
| PlayMode, every suite | `runs/20261031-022919-test-playmode-865776` | 285/286: only the flaky gas pixel count (1,145 for 1,152) |
| Bots: structural, look, tutorial, ai, cover, civilians, reactions, lab, ward, map, tape | the same chain | 11/11 PASS, triage CLEAN |

The four tests:

1. She takes the tape. It plays while she walks, with its subtitle, the music at 8 % and its mark on the map.
2. A fight pauses it. It stays paused until she plays it again from the inventory.
3. The inventory pauses it. An ended tape plays from the start.
4. It stops while the map is open. It plays on through a door.

Faults found on the way:

- The label showed the old state for one frame after a pause. It now changes in the same call as the pause.
- The bot waited for the end of the frame, and that never comes in a batch run. It waits one frame now.
- In the bot the tape paused again at once, because the fight's music holds Combat for 8 s after the last hit. This
  is correct. The bot now waits for the fight to end before it plays the tape again.

Shots for Bruno: `~/MEGA/glorified-body-renders/game/tape/shots/`

| File | It shows |
|---|---|
| `tape_01_playing.png` | Lucy walks, the tape label at the top right, the tape's subtitle |
| `tape_02_fight_paused.png` | a hit: "TAPE PAUSED: a fight. Play it again from the inventory" |
| `tape_03_inventory.png` | the inventory: AUDIO LOG, "E play" |
| `tape_04_playing_again.png` | after the fight, played again from the inventory |

## Open for Bruno

1. The music level during a tape: 8 %. Lower, or silent?
2. Should a new tape stop the old one? Now a new tape takes the player, and the old tape goes back to its start.
3. A key to resume the tape without the inventory (for example Q twice)? Now only the inventory plays it.
