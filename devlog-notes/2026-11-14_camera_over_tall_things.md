# 2026-11-14: no cell camera over a tall thing

Bruno, with two shots of H1:

```text
this box dissapears depending on view; it's in the very first room
```

## The fact check: CONFIRMED BUT DIFFERENT

1. The orange box is `Filter_Box` on the upright frame in H1 (`DemoHold.H1`).
2. A probe rendered the 12 cameras that can show H1. The box renderer stayed on in all of them. The room cull did
   not hide it, and the scene has no occlusion data.
3. `Cam_H1_04` stood at (25.50, 2.75, 35.50), 0.7 m over the top of the 2 m frame back. It looked down along the
   frame, so the frame back covered the box and most of Lucy. Bruno's second shot is this camera.
4. The cause: the cell camera search (`DemoRoom.BestPose`) checked the floor points it covers, but not what stands
   under the camera.

## The fix

1. `DemoRoom.OverATallThing`: the search skips a camera place with anything taller than 1.4 m less than 0.15 m
   under it. The extra cameras (a beat, a foreground) are placed by hand and stay out of the rule.
2. `DemoWorld.BuildAll` and `MapAtlasSetup.BuildAtlas` ran again. Seven cell cameras moved:

| Camera | Was over |
|---|---|
| `Cam_H1_04` | `Frame_Back` (the H1 frame) |
| `Cam_H4_03` | `Booth_Door` |
| `Cam_W16_00` | `Rack_E` |
| `Cam_W16_01` | `Sample_Box_1`, `Rack_W` |
| `Cam_W4_00` | `Stall_Wall_A` |
| `Cam_W4_01` | `Stall_Wall_B` |
| `Cam_W6_01` | `Side_E`, `Locker_3`, `Locker_2` |

3. The demo has 190 cell cameras now (186 before). Every cell still has all its floor points and exits in frame.

## Proof

| Check | Result |
|---|---|
| `DemoTests.NoCellCamera_HangsOverATallThing` (new) | failed before the fix (7 cameras), passes after (0 of 190) |
| `DemoTests`, `HoldTests` | 76 of 76, `runs/20261114-183330-test-playmode-67219` |
| The probe after the fix | `Cam_H1_04` stands at (25.50, 2.75, 36.75), and the box shows |

Shot, before and after: `~/MEGA/glorified/renders/game/h1_camera/2026-11-14_cam_h1_04_before_after.png`.
