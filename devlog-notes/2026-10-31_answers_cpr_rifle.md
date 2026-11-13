# 2026-10-31, afternoon: his answers, the new CPR and drag, the rifle arms

His answers to the open points of the morning:

```text
1: carry the short into the next room
2:keep it for now
3:give the player a warning and a chance to drop items
4:fix both
5: do them now
```

```text
Also fix the rifle arms when you can
```

## What changed

| His word | Now |
|---|---|
| 1, the short at a door | A running short goes on in the next room with its seconds left (`PlayerState.shortLeft`, `ShortController.GoOn`). No second freeze, no second pain. The slow walk goes on. A save during a short keeps it too. |
| 2, a cutscene in a doorway | Kept: it does not start. |
| 3, the locker and too many items | The locker shows a message and keeps the uniform on: "Without the uniform she carries 4 things. She has 6. Drop 2 first: inventory, X drop." The new key X ("Drop Item", remappable) in the inventory asks "Drop the RED?", and Interact puts it on the floor at her feet. Key items stay ("She needs it."). The Crown grab still drops the extra items by itself (round 21). |
| 4, fix both | The room keeps the items on the floor that came in play (`FloorItem`: dropped, or fallen at the grab) and the scars of the floor shield (`FloorScar`). A save, a load and a return put them back. |
| 5, the CPR and drag clips | RFab cannot make them: none of its 101 motion presets is a CPR, a drag or a kneel (RFab gap 13 is still open). The vendor has no such clip either. So they come from the vendor kneel and from code, below. |
| the rifle arms | Fixed, below. |

## The CPR and the drag

- The kneel: the vendor clip `Pick_Down` (a pick from the floor), held at 0.42 s. There, one knee is on the floor and the
  hips are 0.44 m up. A new tool measured it (`Editor/Characters/ClipProbe.cs`).
- The arms: both arms straight down to the chest, the back bent over it, the head down. These are humanoid muscle
  values (`CorpseCare.CprArms`), so every rig takes the same pose. The old code turned the bones along one axis, and
  only some rigs have that axis: this gave the "uncanny" nurse. The compressions bob the back at 110 a minute.
- The numbers come from a sheet of seven muscle sets over the kneel (`PoseProbe`, which now takes any clip). Set B
  won: `animation/shots/cpr_probe_sheet.png`.
- The drag: the vendor crouch walk backward, as before. Both hands now hold the shoulders of the body, by the two-bone
  reach of the gun hold.
- The enemy factory puts the vendor clip on each enemy prefab. The prefabs are not in git, so no vendor data enters
  the repo.

## The rifle arms

His words of 2026-10-25: "the right arm is too high up", and the left arm hung loose. The reach put each wrist on its
grip point, so the fist sat ahead of the grip and the forearm went up. Now each wrist goes 6 cm behind and 4 cm under
its grip, so the palm holds it. The right elbow hangs closer to the body. The bot "holds" measures it: both hands on
their targets (gaps 0 to 3 cm), and the right elbow 0.25 m under the shoulder.

## The trigger hand (his report after the first fix)

```text
However I still see the trigger hand out of position on the photos. Invector has animations built in? I don't recall
seeing such issues with the firing animations on warspite
```

```text
You can do the 6 8 hour fix if the first hand fix fails, I'm patient
```

- Confirmed in close shots: the right hand on the rifle and the shotgun had its fingers up and its palm out.
- Why Warspite did not show it: its enemies use the vendor shooter stack. The gun is a child of the hand, the vendor
  animations pose the arms, and IK moves only the left hand. Here the enemies use the plain AI, and `EnemyGun` places
  the gun and reaches both arms. The RFab (T19) rigs have no finger bones, so the reach set the wrist and left the turn
  of the hand wrong.
- The fix (the first, short one): each hand gets its frame from the bind pose of the mesh (the line of the fingers, the
  side of the palm, `EnemyGun.FindHandFrame`). After the reach, the grip hand turns to wrap the grip, and the support
  hand cups the handguard from below. The bigger change to the vendor shooter stack is not needed now.
- Shots: `trigger_hand_before.png`, `trigger_hand_after.png`, `rifle_hold_after.png`.

## Proof

| Check | Result |
|---|---|
| RoomSeamTests: the short goes on in the next room | pass |
| OutfitTests (5): the warning and the drop key; a save keeps the floor items and the scars | 5/5 |
| The care tests: EnemyMoraleTests, CivilianTests, WardTests, SaveGoreTests | 47/47 |
| The gun and AI tests: EnemyAiTests, ArmedCivilianTests, RagdollTests, EnemyMoraleTests | 44/44 |
| Bots: "cpr" in AI TEST and WARD TEST, "holds" in AI TEST | pass |

Shots for Bruno: `~/MEGA/glorified/renders/game/animation/shots/`

| File | It shows |
|---|---|
| `cpr_02_cpr_side.png`, `cpr_03_cpr_front.png` | a soldier kneels and does the CPR |
| `cpr_ward_02_cpr_side.png` | the nurse does the CPR |
| `cpr_01_drag.png`, `cpr_ward_01_drag.png` | the drag, both hands on the shoulders |
| `cpr_probe_sheet.png` | the seven muscle sets over the kneel (B is in the game) |
| `rifle_hold_before.png`, `rifle_hold_after.png` | the rifle and shotgun holds, before and after |

## Open for Bruno

1. The CPR kneels on one knee (the vendor kneel). Real CPR kneels on both. Is one knee good enough?
2. RFab gap 13 stays open: a text-to-motion stage would give real CPR, drag and kneel clips.
