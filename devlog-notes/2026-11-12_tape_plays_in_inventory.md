# 2026-11-12: the tape plays in the inventory

Bruno, in his manual test:

```text
audio logs should play if player on inventory screen and they press the play button. it should continue when inv menu dismissed (and when not in combat)
```

## The fact check: WORKS AS DESIGNED, now changed

1. The inventory stops the world. The sound system pauses the listener (`AudioHost.OnStateChanged`), and the tape
   clock ran on game time (`TapePlayer.Update`).
2. So a tape started in the inventory waited for the close. The inventory said so: "It plays. Close the inventory to
   hear it."
3. The rule of 2026-10-31 asked for this. Bruno's new rule replaces it.

## The change

1. In the inventory, the tape sound passes the listener pause (`ignoreListenerPause`), and the tape clock runs on
   real time.
2. When the inventory closes, the tape goes on from that time.
3. The pause menu, a page and a cutscene still stop the tape.
4. A fight still pauses the tape, and Interact in the inventory during a fight still says "A fight. The tape waits
   until it ends." (2026-10-31).
5. The inventory text is now "It plays."

## The subtitles in the inventory

Bruno's answer to the open question:

```text
subtitles show
```

1. In the inventory, the tape line shows above the panels (`SubtitleUI`, 560 px from the bottom of a 1080 frame).
2. Only the tape line shows there (`SubtitleService.TapeOwner`). A line of the stopped world stays hidden.
3. Over a log page, the tape line moves to the top of the screen.
4. The pause menu still hides all subtitles.

Shot: `~/MEGA/glorified-body-renders/game/tape_inventory/2026-11-12_tape_subtitles_in_inventory.png`.

## Proof

| Check | Result |
|---|---|
| `AudioLogTests.TheInventory_PlaysTheTape_AndItGoesOnWhenTheInventoryCloses` (new) | passes: the tape moves 0.8 s in the inventory, and it goes on after the close |
| `AudioLogTests.TheTape_PlaysOnThroughADoor_AndStopsWithTheWorld` | changed: the pause menu stops the tape, not the inventory |
| `AudioLogTests` | 7 of 7, `runs/20261112-020506-test-playmode-431442` |
| `ScreenTests`, `RoomSeamTests` | 14 of 14, `runs/20261112-020745-test-playmode-432662` |
| bot `tape` (TRAVEL A) | pass, 3 of 3 steps, `runs/20261112-020652-play-432193` |
| After the subtitles: `AudioLogTests`, `SubtitleTests`, `SystemSeamTests` | 21 of 21, `runs/20261112-023957-test-playmode-436709` |
| After the subtitles: bot `tape`, new step `plays_in_the_inventory` | pass, 4 of 4 steps, `runs/20261112-024241-play-437402` |
