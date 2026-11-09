# 2026-11-09: the four small items in isolation

Bruno, after the list of what was left to test in isolation:

```text
add the isolated items
```

## 1. The brain's death screen

The row was in the writer's `game/death_screens.csv`: the brain's three lines ("Your life has been full of suffering.
But don't worry. It'll be over soon.") as its voice line. Its hint still told the old fight ("it strikes where you
stand when the ring closes"). I changed the hint in the writer's file to the new fight:

```text
Its ring is slower than your walk: keep moving. When it locks, hurt it before the charge fires, or leave the ring.
A grabber swells before it lunges: step aside.
```

The writer can change the words. `DeathScreenSetup.Build` read the file again, and with it the newer rows of the
writer (the rifle soldier has his own voice line now). `DeathScreenTests.KilledByTheRifleSoldier_...` expected the
class line, so the test now takes the row's own line when it has one.
`BrainFightTests.ItsKill_ShowsItsDeathScreen_...`: a strike kills her, and the screen shows the brain's row.

## 2. The core's voice

DEMO_PLAN, annotated by Bruno on 2026-11-07 (round 49): after she takes a mind, the core speaks through her, in a voice
that is not hers. It does not count against P7. The label is VOICE (round 50).

`Dominated.SayCore`: at the take, the order to a switch and the snap, one line of the writer's set (`barks.csv`, kind
`voice`: eight take lines with Bruno's three, three sent lines, three snap lines) shows under VOICE, before the
figure's own line. No clip exists, so the subtitle shows alone (`voice_missing` in the log).
`EdgarTests.TheCore_SpeaksThroughHer_AtTheTake_TheOrder_AndTheSnap`.

## 3. The knock-out gas

The writer's row `soldier_gas` says "She is knocked out" ("Knock-out's working. She's out."). Bruno's ruling of
2026-10-24 says that the gas is poison. Both stay:

1. The cloud still hurts her (the poison).
2. Each second in a cloud fills a meter (`Player/Drowsiness`). Out of the clouds it drains at half the speed.
3. The screen darkens as it fills. Over half, she walks slowly.
4. Full (4 s in the clouds in all): she is knocked out. The game is over, and the `soldier_gas` death screen shows.
5. While she is invulnerable (a dodge, the test rooms), the knock-out waits.

Numbers PROPOSED. `EnemyAiTests.Gas_KnocksHerOut_AfterItsSecondsInside_AndOutsideSheWakes`: 2 s inside is half the
meter, the screen darkens and she walks slowly. 1 s outside, she wakes. Back inside, she is knocked out.

A fault found on the way: the first version knocked out an invulnerable Lucy. In AI TEST the gas soldier threw gas
during other tests, and she fell in the middle of `Lift_...`. That run crashed in the audio thread after the game
over, twice. Hence item 5.

## 4. One pose for every power

DEMO_PLAN §1 (CANON 2026-11-05): "she lifts one arm, the palm out. With mk_orzabal the arm follows the floater."

`Player/PowerArm` (`PsychicPowers` adds it, so no scene changes): while she charges a power or holds a thing in the
air, one arm rises to the aim (the held thing, else the locked target, else straight ahead). It uses the arm muscles
of the humanoid pose, as `PleaHands` does, so the numbers fit every model she wears: the forearm straight, the hand
bent back. Two muscles aim the arm, by a small solver each frame. The arm on the side of the target rises: the
floater of the test hung 82° to her left, out of the reach of the right arm.

`PowersAndBarsTests.OnePose_...`: on a charge the arm points 0.2° off the target, on the floater (left arm) 0.7° off.
The palm: the first value of the hand muscle (Hand Down-Up +0.7) bent the hand toward her. The other sign (-0.7)
shows the palm to the target. Shots: `~/MEGA/glorified-body-renders/game/isolated_items_2026-11-09/`
(`arm_first_try_palm_in.png`, `arm_palm_out.png`, from the brain bot: she charges at the brain, at the camera).

## Proof

| Check | Result |
|---|---|
| `PowersAndBarsTests`, `BrainFightTests`, `EdgarTests`, `DeathScreenTests` | 96 of 96 after the two fixes |
| `DeathScreenSetup.Build` | the seven test-room death screens of the writer (2026-11-07) replace the placeholder hints |
| `EnemyAiTests` (the knock-out test, and the room's other tests with the gas soldier) | 22 of 22, no crash |
| The brain bot (a new shot: her arm) | passes |
