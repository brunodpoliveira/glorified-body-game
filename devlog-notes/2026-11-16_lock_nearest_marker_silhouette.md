# 2026-11-16: the lock on: the nearest key, the marker over everything, the silhouette

Bruno:

```text
man, lock on needs some improvements:
1 - it needs an option to lock onto the nearest target. it is very furstrating to keep scrolling until the right target is locked on to
2 - the indicator is hidden behind walls and other obstacles. it needs to be seen no matter what and if the enemy is not visible by the camera the enemysillouette must be highlighted
```

The fact-check, from the code (`LockOnController`):

1. CONFIRMED. The auto-lock takes the nearest enemy only when she has no target. After that, Tab and the wheel go round
   the targets by direction. No key goes back to the nearest one.
2. CONFIRMED. The marker was a sphere with the stock unlit material, drawn with the depth test, so a wall or a prop in
   front of it hid it. No silhouette existed.

## 1. The nearest key

A new key, **Lock Nearest Target** (`GameAction.LockNearest`), default the wheel click (the wheel already switches). It
can be remapped in the options. It locks the nearest live enemy in range and in her sight. It does not lock a civilian, an
ally or a prop, and it skips the default target of a boss (the brain). The tutorial's switch step names it.

## 2. The marker

The marker draws over everything now: a new unlit shader with no depth test, after the transparent queue
(`Resources/Shaders/GB_OnTop.shader`). It sits in Resources, so a build keeps it.

## 3. The silhouette

When the camera cannot see the locked target, its body draws as a flat red silhouette through the wall
(`Player/LockSilhouette`, `Resources/Shaders/GB_Silhouette.shader`).

1. Each mesh of the target gets a twin: the same mesh and the same bones, with the silhouette shader on every submesh.
   The twin draws only where something nearer hides the body (the depth test "greater"). It moves with the body, a
   ragdoll too.
2. "Not visible by the camera": the lines from the camera to its chest and to its head both end on a wall or a prop. When
   the camera sees it, the twins are off. On a body in view a twin would draw where one limb hides another.
3. A new target takes the silhouette, and the old twins go. No twin casts a shadow.

## Proof

NOT COMPILED, NOT RUN (no free RAM for Unity). New test: `TankControlTests.LockNearest_JumpsBackToTheNearest_...` (the key,
the marker's shader, the silhouette on the target).

To test by hand:

1. In a fight, switch the lock to a far enemy with the wheel, then click the wheel: the lock goes to the nearest one.
2. Stand so that a column or a wall stands between the camera and the locked enemy. The marker still shows over it, and
   the enemy shows as a red silhouette.
3. Walk so that the camera sees the enemy again: the silhouette goes, and the body shows as usual.
