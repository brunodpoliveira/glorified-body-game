# 2026-10-22, night: the inventory overlay, key items and grabbables, a locked door, unlit pickups

Bruno (2026-10-22, night, after the Interact and Scan keys, with a Galerians item screen): "scan's range is too
far; needs to be closer to the object"; "the scan costs nothing except time"; "build it; the inventory should be
an overlay over gameplay; it should pause the game. the inventory has mission critical items (like keys, logs,
the injector) and grababbles (the pills etc). mission critical items occupy no space, only grabables do (so
pills need to be grababble not just hover to pick up)"; "we should have a test key to open a locked door as
well"; "grababble/interacles need to stick out: resident evil 4 solved this by putting no lighting on the
interacble object". His "for later" block (the MK names, the Telekinesis hold, the Fire stun, the 2x boost at
50 percent MK) is in `03_GAME_DESIGN.md` §12, not built.

## What changed

1. **The scan reaches 1.5 m** (`Scannable.DefaultRange`, was 3 m), no farther than the Interact reach. It is
   free; she stands still while she sees it and the room goes on (the cost is time).
2. **The inventory overlay.** The Inventory key (I, new, remappable) opens it over the frozen room: a new game
   state (`GameState.Inventory`) stops time as the pause does, and only the inventory's keys pass the input gates
   (Inventory, Interact, Back, Forward, the turn keys). The layout follows the Galerians item screen: a row of
   cells at the bottom (the key items first, then the carried objects, then the drugs with their counts), the
   name above it ("Item... TEST LOG"), the words and the keys to the right, the slots used above. A and D select.
   Interact reads a log, uses a drug, or says where a key goes. The Inventory key, Back or Escape closes it.
3. **Key items and grabbables.** Keys, logs and the critical items (the injector, later) take no slot. The
   grabbables take one slot each: the carried objects and every drug. The slots come from the outfit (nude 4,
   guard 8, PROPOSED). The drug count now draws on those slots; the per-type cap of the outfit is gone.
4. **Pills ask first.** The walk-over drug pickup (`DrugPickup`) is gone. A pill is an `ItemPickup` like the log:
   Interact asks "Take the nalcon?", Interact again takes it. A full inventory says "No room." and the pill stays.
5. **A test key and a locked door.** The gym's south doorway holds a steel door (`LockedDoor`). Without the key,
   Interact shows "It is locked. It needs the test key."; with it, the game asks "Use the test key?" and a yes
   slides the leaf aside. Behind it is a small closet with a dim light, a red pill and its own camera. The key
   lies on a stand by the west wall, next to the log; it stays in the inventory.
6. **The pickups stand out.** Every pickup (the log, the key, the pills) uses an unlit material in its own colour
   (`Universal Render Pipeline/Unlit`), with no shadow on it, so the room light never hides it (after Resident
   Evil 4). The colours: nalcon green, red red, D-felon blue, the log white, the key yellow.
7. **The tutorial** has 17 steps: after the capsule (now taken with Interact), the log, the door scan (from 1.3 m,
   by the leaf's west half), the key, the door, the inventory, the monitors scan, the short, complete.

## Proof

- PlayMode `InteractionTests` (10, in the tutorial gym):
  1. Interact asks, takes and opens the log. Back leaves an item.
  2. A full inventory refuses a grabbable with "No room.". It never refuses a key item. The guard outfit has more slots.
  3. The door scan shows its picture. Interact at the locked door only says that it is locked.
  4. The monitors clip plays to its end. Facing away, or 2.1 m away, the scan finds nothing (the reach is 1.5 m).
  5. With the key, the door asks, and the leaf slides aside. The key takes no slot.
  6. The capsule is unlit. Walking onto it takes nothing. Interact asks and takes it into one slot.
  7. The inventory stops the room (time scale 0, she does not walk) and shows "2 / 4" for the log and two drugs.
     It reads the log, uses a drug, and closes on its key. Then the room runs again.
- PlayMode `PowersAndBarsTests.DrugPickup_AsksFirst_AndTakesOneSlotEach_UpToTheOutfitSlots` (the training room).
- PlayMode `TutorialTests`: the seventeen steps through the virtual pad.
- The structural bot takes its drug with the Interact key.

EditMode 36 of 36, PlayMode 85 of 85, the tutorial bot 19 of 19, the look bots 32 of 32 and 48 of 48, the
structural bot 24 of 24, all CLEAN. The new steps by the pad: key 3.4 s, door 4.6 s, inventory 1.0 s. 
In one PlayMode tutorial play, the first walk to the door stopped against the parallel bars. The second
attempt of the drive passed. The walk drive has no path finding: a fix for the props work.
The first PlayMode run failed one test and caught a real miss: the builder still set every scan target to 3 m.
RUNS: 20261022-204511 (the menu loop scenes), 205920 (the look scenes), 210833 (the tutorial), 210021 (the
tutorial bake), 210200 (EditMode), 210907 (PlayMode), 214557 (the tutorial bot), 211552 and 211644 (the look
bots), 211743 (the structural bot).

## The shots

`~/glorified-body-renders/game/tutorial/`: `sheet_door_inventory.png` (the question "Use the test key?", the
open door, the inventory on the test log and on the test key), `sheet_interact_scan.png` (the log question and
page, the two scans), `sheet_tutorial.png` (the seventeen prompts). Checked at full size: the inventory reads
"SLOTS 0 / 4, KEY ITEMS 2 (no slot)" after the capsule was used, the selected cell carries the outline. The
first bot run showed the cells spread over the row with wide gaps (the layout stretched them): fixed.

## For Bruno

1. The inventory look is a placeholder (plain panels, coloured cells with the names). Icons come with the
   item art.
2. Interact on a drug in the inventory uses it. Should it ask first there too?
3. The closet's red pill is the reward behind the door. A full nude inventory (4 slots) can refuse it.
