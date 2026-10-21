# 2026-10-21: the dodge press is forgiving

Bruno (2026-10-21, in the editor): "it's a bit hard to trigger the dodge at the sides, not sure why; i tap twice,
i press space + directions and sometimes the player just rotates/walks a bit forward instead of dodging".

## Fact check

The keys arrive. Three rules in `TankController` dropped the press or aimed it wrong:

1. The direction was read on the Space frame only. Space one frame before A gave the step back (no key held).
   A and D are turn keys, so the failed side dodge showed as a small turn. W was held while she walked, so the
   forward dodge seemed fine, and Space alone is the step back by design. Only the sides needed the two keys
   on the same frame.
2. With W held, Space plus D gave the forward dodge: the priority was W, S, A, D, not the key pressed last.
3. A press inside the cooldown (0.8 s from the dodge start, 0.45 s after the dash ends) was dropped without a
   sign. In a test loop, the next press often lands there. She then turned (A or D held) or walked a bit
   forward (W held), which is the "rotates/walks a bit forward" he saw. The double tap window was 0.3 s, at the
   edge of a hand.

Verdict: CONFIRMED BUT DIFFERENT. Not a detection failure: a frame-exact rule, a fixed priority and a silent
cooldown drop.

## The fix (PROPOSED numbers, Bruno decides)

- A dodge press (Space, or a double tap with its direction) waits up to 0.5 s (`dodgeBufferTime`) and fires when
  the dodge is ready. The dead time after a dash is 0.45 s, so a press after the dash ends always counts. A
  press during the charge or the short is dropped as before (no dodge there, so no dodge after them either).
- A direction key up to 0.1 s (`dodgeAimWindow`) after the dash started turns the dash that way. Space alone
  still steps back at once (the panic button keeps its zero delay), and A a frame later makes it a dodge to the
  left. A key that goes down while a press waits aims that press.
- With two keys held, the one pressed last wins (W held, then Space plus D: to the right).
- The double tap window is 0.4 s (was 0.3 s).
- The scene builder sets the three numbers on the kept component (the pitfall of 2026-10-20: a changed default
  never lands by itself). Telemetry: a re-aim logs `dodge_aim`.

## Proof

PlayMode tests in `TankControlTests` (three new, one extended):

| Test | Shows |
|---|---|
| `Dodge_DashesBack_GrantsInvulnerability_AndAPressInTheCooldownWaits` | a press 0.6 s after a dodge waits, then fires at 0.8 s |
| `Dodge_SpaceOneFrameBeforeTheSideKey_DashesToTheSide` | Space, then D one frame later: a dodge to the right |
| `Dodge_WhileWalking_TheDirectionKeyPressedLastWins` | W held, Space plus D: to the right, not forward |
| `DoubleTap_AtAThirdOfASecond_Dodges` | two taps 0.35 s apart dodge |

Each of the three new tests fails on the old code by construction (the old rule gave Back, Forward and no dodge).

PlayMode 55 of 55, the bot 24 of 24 (its dodge step: 2.50 m, invulnerable), all CLEAN. The scene carries the
three numbers (`doubleTapWindow: 0.4`, `dodgeAimWindow: 0.1`, `dodgeBufferTime: 0.5`). RUNS:
20261021-175122-exec-55310, 20261021-175156-test-playmode-55723, 20261021-175522-play-56367.

Open (Bruno): the cooldown of 0.8 s from the dodge start. With the buffer a press after the dash fires up to
0.45 s late. A shorter cooldown (0.6 s) makes it 0.25 s. Feedback on a waiting press (a HUD blip) is possible.
