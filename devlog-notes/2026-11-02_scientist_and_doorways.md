# 2026-11-02: the W1 scientist talks first, the doorways change the room at the wall

Bruno:

```text
1 - the first scientist should be in a secondary room; as it is the dude just pops into the room and begins attacking
her immediately (even as the dialogue continues)
1.1 - it should do the dialogue then he attacks her
2 - there is a huge gap between all rooms that is necessary for me to take at least 3-4 steps blindly down the doorway
before it switches room. this happens in every room
```

## The fact-check (two tests that failed before the change)

| Report | Test | Before |
|---|---|---|
| 1 | `DemoTests.W1_TheScientist_ComesInFromOutside_TalksFirst_ThenAttacks` | FAILED: "alerted at step 2 of 9" (the first line). He started at z 14.5, inside the room |
| 2 | `DemoTests.ADoorway_ChangesTheRoom_AsSheStepsThrough_InFrame` | FAILED: the room changed 1.40 m past the wall line, and the camera did not see her |

The causes:

1. The script showed him inside the doorway (`ScriptStep.Show`). His brain then saw her and alerted itself
   (`SoldierBrain`, the sight alert). The script had no way to hold him during the lines.
2. Each doorway has a 2.2 m corridor stub. The exit trigger was at the far end of the stub, outside the camera's frame.

## What changed

- `ScriptStep.Approach(group, point)` (`StepKind.Approach`): every soldier under the group talks first. He sees her but
  does not attack, walks to the point, and turns to her. The script's Alert ends it. A hit from her or a power near
  him also ends it (`SoldierBrain.ScriptHold`, `ScriptHeld`).
- W1: the scientist waits in the corridor behind the door (z 12.3). On the first line he walks to the doorway. On
  "Lie down. On the ground." he steps in. After "Or else." he attacks.
- `DemoRoom.Doorway`: the exit trigger fills the doorway (from 0.15 m into the wall to 0.4 m past it). The stub stays
  as the dark corridor behind the door.
- All 22 demo rooms and the map atlas built again.

## Proof

| Check | Result |
|---|---|
| The two tests above, after the change | pass. The room changes at -0.03 m, in frame |
| `DemoTests` (7), `ScriptTests`, `ArmedCivilianTests`, `MapTests`, `EnemyAiTests` | 49 of 49 |

No shots for this step: the next step (all the rooms in one scene) changes the rooms again, and its shots show both
fixes.
