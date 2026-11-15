# 2026-11-15: the human shield, the face plant, the CIV tag

The writer's rounds 161 and 162 (`RULINGS.md`), CANON. Bruno, round 162:

```text
when orzabal slams at full speed, have them face plant like so
and when orzabal is active and the player puts a live civilian at or near ground level, the guards and soldiers won't fire through the civvie. basically, a hostage-taking mechanic (not tutorialized but can be hinted at in a death screen, since it is extremely unlikely a player will be killed by say a nurse or an unarmed civilian
```

## The human shield (`Enemy/HumanShield.cs`)

1. She holds a live civilian with mk_orzabal, and its root hangs 0.6 m or less over the floor it lay on
   (`HumanShield.LowHeight`, PROPOSED). The lift starts at 1.5 m, so she lowers it with the wheel.
2. A guard or a soldier (`EnemyMorale.Rank`) does not start an aim, and does not fire an aimed shot, when the civilian's
   body is on the line from his muzzle to her (`DummyShooter.ShieldStops`). A burst stops at the round that would go
   through.
3. He says why, at most every 6 s: the writer's `civilians present` rows ("Control, be advised, civilians in the room,
   I am not clear to engage.").
4. The power armour and the armed lab workers fire anyway. A lobbed grenade goes over. A melee strike is no shot.
5. The death screen hint of round 163 is in (`2026-11-15_two_voices_logs_hints.md`).

## The CIV tag of the barks (round 162)

A row with `CIV: woman`, `CIV: man` or `CIV: group` in its direction plays only to a matching civilian
(`BarkRow.civ`, `BarkVoice.Next(situation, civ)`). A row with no tag fits anyone. The nurse is a woman and the lab
worker is a man (`CivilianBrain.CivKind`, PROPOSED: the models). The situations `civilians present` and
`escorts a civilian` are in `BarkBook.Asked`, so `BarkBookTests` checks their rows.

## The face plant (round 162)

A full slam of mk_orzabal on a body with a ragdoll (`EnemyRagdoll.EndHoldFacePlant`): the body goes limp at the top of
the slam, the head and the chest go down at 7 m/s, the legs go up at 6 m/s and away from her, and the body pitches
forward. The hit and the splash come 0.2 s later, when it lands. A body with no ragdoll keeps the old slam. The
reference: `reference/generic/other/orzabal_slam_face_plant.webp`. Numbers PROPOSED.

## Proof

| Check | Result |
|---|---|
| `HumanShieldTests.ASoldier_DoesNotFireThroughACivilianSheHoldsLow_ButFiresWhenSheRaisesIt` (new, KIT TEST) | pass: the lab worker hangs 0.10 m over the floor, the soldier holds 2 times and fires 0, then fires when she raises the lab worker |
| `HumanShieldTests.AFullSlam_LandsTheTarget_HeadFirst_TheLegsOver` (new) | pass: the feet rise 0.88 m over the head |
| `RagdollTests`, `PowersAndBarsTests` | pass (74 of 75 in the run, the one failure was the shield test before the fix below) |

The first run found a gap in my code: a soldier who could not start an aim through the civilian did not count it or say
why. Now a blocked aim counts and speaks, once a second at most.
