# 2026-10-31: the outfit change

The order from Bruno: "all of it" (the in-world screens, the outfit change, the floor shield, a seam sweep). The design
sweep's rank 7: "a uniform pickup or locker, the swap to the lucy_guard model, the stats, the HUD name". The demo plan
puts the guard uniform in a locker of W6. Round 20 and round 21 of the design: the Crown grab of U1 tears the uniform
off, and the items over the limit of the bare body fall to the floor. The numbers are PROPOSED.

## What it does

| Part | Behaviour |
|---|---|
| The locker | `OutfitLocker`. Interact asks "Put on the guard uniform?". A yes changes her outfit behind a short black. In the uniform the same locker asks "Take off the uniform and leave it here?". |
| The model | Her model changes to `lucy_guard` in the uniform and back to `lucy_bare` without it (`LucyModel.Wear`, the class in `OutfitProfile.modelClass`). The new model keeps the Player layer (the stencil mark of the look). Her animator driver finds the new Animator at once. |
| The stats | The uniform: 150 HP, 8 slots, no MK recharge. The bare body: 100 HP, 4 slots, MK recharge. |
| The HP | A change keeps the damage she took. 60 of 100 becomes 110 of 150. 120 of 150 becomes 70 of 100. She never goes below 1 HP. A change never heals her (only feeding heals). |
| Too many items | When the slots go down, the grabbables over the limit fall in front of her as pickups: the carried objects first, then the pills (the rarest first). Key items never fall. She picks what to keep. |
| The uniform in the locker | It hangs in the locker while she does not wear it. |
| The HUD | The outfit name over the level: NUDE or SECURITY GUARD. |
| The grab | A script step (`StepKind.Outfit`) changes her outfit. The Crown grab uses it with "nude". |
| Saves and doors | The saves keep the outfit already. A load or a door now also puts the right model on her. |

## The OUTFIT TEST room

`OutfitTestSetup.BuildOutfitTest`, the button OUTFIT TEST. A closed hall: five black lockers on the north wall (the
middle one holds the uniform), six red pills on the floor, a save machine, and a dark red square at the east end. A
step on the square runs the grab once: the uniform tears off and the pills over four fall.

## Faults found on the way

- `RoomMusic` lived inside `MusicDirector.cs`. A scene kept no link to its script, so a room could load it as a missing
  script. OUTFIT TEST read its music key as "OutfitTest", not "outfit_test" (the test failed first). `RoomMusic` has
  its own file now, and `RoomKit.Music` removes the broken slot. KIT, LAB, WARD, SCREEN and OUTFIT TEST are built
  again, and the map atlas too.

## Proof

| Check | Result |
|---|---|
| OutfitTests (4): the locker puts it on (model, HP, slots, HUD, she walks); taking it off keeps the damage and two pills fall; the grab tears it off once; a save keeps it and the load puts the model back | 4/4 |
| The tests of the rebuilt rooms: Outfit, Screen, Kit, Ward, ArmedCivilian, Map, AudioLog | 49/49 (`runs/20261031-080913-test-playmode-909903`) |
| The bot: `tools/unity.sh play OutfitTest Main_Menu outfit 300` | 3/3 steps |

Shots for Bruno: `~/MEGA/glorified/renders/game/outfit/shots/`

| File | It shows |
|---|---|
| `outfit_01_locker.png` | Lucy at the open locker, the uniform on its hanger |
| `outfit_02_uniform_on.png` | Lucy in the guard model |
| `outfit_03_grab_torn_off.png` | after the grab: the bare body, two pills on the floor |

## Open for Bruno

1. The locker drops the extra items too, as the grab does. Or should the locker refuse until she uses or drops them?
2. A pill that fell is not in the saves. If she leaves the room, it is gone.
3. Any locker gives a uniform. The demo has one locker, so this is no problem now.
