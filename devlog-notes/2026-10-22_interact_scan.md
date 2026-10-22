# 2026-10-22: the Interact and Scan keys, the inventory, a test log, the scan (a picture and a clip)

Bruno (2026-10-22, evening): "add a placeholder test log, scan image, scan video (and i presume also add the
interact and scan button: it'll pick up objects after confirmation (we'll have an inventory system, and her
wearing the sec guard outfit has more hp + inventory space at the cost of not passively recharging); interact
button and scan need to be separate". Then: "we can just do a placeholder img + video scan, we can add a test
one later". And on the cameras of the look rooms: "fix them too".

## What it is

1. **Two keys.** Interact (E) takes and reads. Scan (F, new, remappable) is the psychic scan. Neither does the
   other's job. The hint over the HUD says what each key would do now ("E  take the test log", "F  scan").
2. **The pickup asks first.** Interact on an item in reach and in front of her (1.6 m, 60 degrees each side)
   opens "Take the test log?". Interact again takes it, Back (S) leaves it. She stands still while she answers.
3. **The inventory.** `ItemInventory` on Lucy, one slot per item, the slots from the outfit: nude 4, security
   guard 8 (PROPOSED). A full inventory refuses the item, and it stays in the room. The guard outfit already had
   more HP (150) and no passive recharge.
4. **A log.** A taken log opens for reading at once (title and text). Interact or Back closes it. It stays in
   the inventory (an inventory screen comes later). The test log is a placeholder clipboard on a stand by the
   west wall of the tutorial gym; the writer gives the words later.
5. **The scan.** Scan on a target in reach and in front of her (3 m, 50 degrees each side) fills the screen with
   its picture or plays its clip. A picture closes with Scan, Interact or Back; a clip closes at its end or with
   those keys. She stands still while she sees it. No nalcon cost (PROPOSED). The gym door holds the placeholder
   picture, the wall of monitors holds the placeholder clip (4 s). Both are plain cards made with ffmpeg
   ("SCAN PLACEHOLDER"), in `Assets/_Game/Scan/`. The clip is VP8 webm: the Linux editor does not decode H.264.
6. **The tutorial** has three new steps after the capsule: `log` (take it and read it), `scan_image` (the door),
   `scan_video` (the monitors). Fourteen steps now.
7. **The look rooms' cameras** follow the game-room rule now: no floor point within 1.2 m of its camera. A small
   search (`camsearch.py` in the session scratchpad) tried wall spots for each band of floor and kept the
   nearest one with no miss: the Gallery has four wall cameras about 6 m across from their bands (Cam_Wide is
   the middle and the default). The corridor keeps the T12 view as the default for its middle (z -5 to 5) and
   gets two east-wall cameras for its ends. The recovery room has two bands, each seen from the west wall. The
   LookTest2 gym takes the tutorial's three cameras.

## The parts

| Part | Where |
|---|---|
| `ItemDefinition` (id, name, kind Object or Log, text) | `Scripts/Interaction/`, the test log in `Resources/Items/item_test_log.asset` |
| `ItemInventory`, `ItemPickup`, `Interactor` (the question, the reader) | `Scripts/Interaction/` |
| `Scannable` (a picture or a clip, a caption), `PsychicScanner` | `Scripts/Interaction/` |
| `InteractionUI` (the hint, the question, the page, the scan view with a VideoPlayer) | `Scripts/UI/`, on `[RuntimeUI]` of every room |
| `GameAction.Scan` (F) | `InputManager`: the remap list and the how-to-play page show it |
| `OutfitProfile.itemSlots` | nude 4, guard 8 (`CreatePowerAssets`) |

## Proof

- PlayMode `InteractionTests` (6, in the tutorial gym): Interact asks first, the second press takes the log, it
  opens for reading, a third press closes it, she stands still meanwhile, and Interact never scans; Back leaves
  the item in the room; a full inventory refuses the log and the guard outfit has more slots; Scan on the door
  shows its picture, Interact there scans nothing, a second Scan closes it; Scan on the monitors plays the clip
  (30 frames and more seen) and the view closes at its end; facing away, Scan finds nothing.
- PlayMode `TutorialTests`: the fourteen steps, played through the virtual pad, the log and both scans included.
- PlayMode `LookRoomTests`: the coverage under the game-room rule: the Gallery 0 of 412 floor points out, LookTest2
  1 of 484, the tutorial gym 1 of 144.
- The structural bot checks one remap row per action now (it counted 14 by hand and failed on the new Scan row).

EditMode 36 of 36, PlayMode 81 of 81, the tutorial bot 16 of 16, the look bots 32 of 32 and 48 of 48, the
structural bot 24 of 24, all CLEAN. The new tutorial steps by the pad: log 2.4 s, scan_image 4.2 s, scan_video
6.8 s (the clip runs 4 s). RUNS: 20261022-194555 (the menu loop scenes), 194706 (the look scenes), 194757 (the
tutorial), 194826 (the tutorial bake), 194918 (EditMode), 194940 (PlayMode), 195454 (the tutorial bot), 195604
and 195656 (the look bots), 195842 (the structural bot).

## The shots

`~/glorified-body-renders/game/tutorial/`: `sheet_interact_scan.png` (the question "Take the test log?", the open
log, the scan of the door, the clip of the monitors at 1.6 s), `sheet_tutorial.png` (the fourteen prompts), the
frames at 1920 x 1080. Checked at full size. In the shots the panels look more see-through than in the game: the
bot draws the overlays through the camera, so they pass through the dither; the game draws them on top.

## For Bruno

1. The numbers: 4 slots nude, 8 in the guard outfit, the reach 1.6 m, the scan 3 m. Say what changes.
2. The scan is free of nalcon now. Should it cost some, or take time?
3. The inventory screen (to read a log again, to see the keys) is the next piece of this system. It waits for
   your word on its look.
4. The picture and the clip are placeholders. The real test ones come later, as you said.
