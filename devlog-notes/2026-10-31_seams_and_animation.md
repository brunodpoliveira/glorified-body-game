# 2026-10-31: seam sweep part 3, and the animation check

The order from Bruno: "all of it, then check the animation errors". This note covers the seam sweep and the
animation check. A read-only agent searched the new systems for faults where two of them meet. The new systems are the ones built
since the sweep of 2026-10-26: the map, the tapes, the screens, the outfit, the shield, the feed, travel, scripts. It reported 8 faults
and 3 small ones. I treated each one as a claim. Each fix below has a test that failed on the old code.

## Seam faults, fixed

| Fault | Before | Now | Test |
|---|---|---|---|
| RETRY in a room she came into by a door, with no checkpoint | She came back blank: the default HP, no items. Her items were lost for good. | She comes back as she came in: her state at the door, the room as she found it, her place at the door. | `RoomSeamTests.ARetry_...` |
| A cutscene that starts in the fade of a door (0.25 s) | The new fade dropped the room load. The trip never ended, and every door refused. | A fade that replaces another runs the old callback. A cutscene does not start in a doorway. | `ACutscene_DuringTheDoorFade_...` |
| A lethal hit in the fade of a door | She arrived alive in the next room, with her HP from before the hit. | She dies where she is. A dead body does not use a door. | `ADeath_DuringTheDoorFade_IsADeath` |
| The short through a door | The bar arrived full, so her next attack started a second short (another freeze). | The short burns out at the door (PROPOSED). | `TheShort_EndsAtTheDoor_...` |
| A load or a RETRY to before she took a tape | The tape label stayed, and T played a tape she did not have. | The tape player forgets a tape she does not carry. | `ALoad_ForgetsATape_...` |
| The death fall (1.6 s) | Interact and the map still worked. A locker could change the outfit of a dead body. | No Interact, no map. | `InTheDeathFall_NoInteractNoMap` |
| The shield circle, then the attack key | The shield kept its lock on her feet. She could not walk until a reload. | The shield always takes its own lock off. | `FloorShieldTests.TheShieldCircle_...` |
| A fast bullet and the floor shield | The bullet checked her body first. A long step (a fast round, a slow frame) jumped the slab. | The nearer one first. | `FloorShieldTests.AFastBullet_...` |
| The inventory and a tape in a fight | It said "It plays", and the fight paused the tape again at once. | "A fight. The tape waits until it ends." | `AudioLogTests.TheInventory_InAFight_...` |

Proof that the tests find the faults: the six RoomSeamTests ran against the old code (`f99c075`, the fixes set aside).
All six failed with the expected message (`runs/20261031-090338-test-playmode-930682`). With the fixes: 34/34 in the
suites around them.

Also found on the way:

- `RoomMusic` was inside `MusicDirector.cs`, so a room could load it as a missing script (see the outfit note).
- The harness said CLEAN when a batch call named a method that does not exist (exit 0). `log-triage.py` flags it now.

## Seam faults, not fixed (open)

1. Items that fall at an outfit change are not in the saves (also in the outfit note).
2. A tape's subtitle has no end time, so it can stay on screen under a cutscene.

## The animation check

No log shows an animation error: not the batch runs of the day, and not his editor log of 2026-10-30. So I took "the
animation errors" as the animation faults he marked FOR LATER. I checked each one with a test or a shot.

| Fault (his words) | Check | Result |
|---|---|---|
| "the lower leg bends inwards in a very unnatural way" (2026-10-27), the armour knee | The knee test, 4 runs: 2 failed. The fault was not a backward bend. The shin turned 41 to 62 degrees out of the plane of the knee (the fire death of the armour, a shock death). | FIXED. Knees and elbows are hinges with both swing axes locked (`HumanoidRagdoll.Hinge`, enemy prefabs built again). Now 5 degrees or less in 7 runs. The old "bent back" check counted a knee that straightens from its death pose as a wrong bend. The test measures "past the straight leg" (the bind pose of the mesh) now. No knee passed it. |
| the gun floats in the air during the CPR (2026-10-24, "sling it on the back") | The care test, new check: the gun was in his hand, 0.77 m in front of his chest. | FIXED. During the drag and the CPR a long gun hangs across his back, a pistol at his right hip (`EnemyGun.Sling`). Bot "cpr". |
| "a pistol sits a few cm in front of the fingers" (2026-10-29) | A close shot of the hands of the guard (bot "holds"): the grip one fist-width ahead of the fist. | FIXED. The grip sits at the wrist line (the T19 rigs have no finger bones). |
| the long-gun arms (right arm high, left arm loose) | A close shot of the rifle soldier. | NOT CHANGED: it reads well at play size. His ruling was "let it be". |
| the drag and the CPR pose "sometimes comes extremely uncanny" (the nurse) | The CPR shot of a soldier. | NOT CHANGED: the poses are RFab presets. A better pose needs new clips (RFab), not code. |

Shots for Bruno: `~/MEGA/glorified-body-renders/game/animation/shots/`

| File | It shows |
|---|---|
| `cpr_01_drag.png` | the colleague runs to the body, the shotgun on his back |
| `cpr_02_cpr_side.png` | the CPR, the gun on his back |
| `holds_guard_pistol_before.png`, `holds_guard_pistol_after.png` | the pistol of the guard, before and after the grip fix |
| `holds_soldier_rifle.png` | the rifle hold, not changed |

## Open for Bruno

1. The short burns out at a door. Or should it go on in the next room?
2. A script cutscene does not play while she is in a doorway. Is that right, or should it wait for her arrival?
3. Do you want new CPR and drag clips from RFab now, or after the demo rooms?
