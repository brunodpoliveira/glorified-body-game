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

## Bruno's check, the same night

```text
still firing through the hostage
face plant is not dramatic enough: the person goes face first onto the ground and hits the ground head first
```

### Firing through the hostage: NOT REPRODUCED, and a fault of my own found

1. A new test plays the fight as Bruno did: every brain on, she holds the lab worker 0.10 m over the floor, and the
   test checks every shot (`HumanShieldTests.WithTheirBrainsOn_NoShotGoesThroughTheHostage`).
2. Result: 27 shots in 20 s. None went through the hostage, in 3D or seen from above. The soldiers and the guard
   flanked, and every shot passed 0.66 m or more beside it (the body is about 0.5 m wide).
3. The held civilian stands upright (the civilians have no ragdoll): its body reaches 1.80 m, so the line at her chest
   height crosses it when it is in the way.
4. The fault: the escort's scan says "civilians present" half the time and starts a 25 s gap even when it says nothing.
   The gun's "hold fire" used the same gap, so it often said nothing. The soldier held his fire with no word, and from
   a fixed camera a laser 0.7 m beside the hostage can look like a shot through it. The "hold fire" has its own 6 s gap
   now (`DummyShooter.nextShieldBark`).

### The face plant, more dramatic

At the top of the slam the body turns upside down about the hips, the head to the floor and a little away from her, at
least 1 m over the floor, and it drops at 9 m/s. The head hits the floor first, then the legs go on over
(`EnemyRagdoll.EndHoldFacePlant`, `FacePlantFirst`). The test: the head is first on the floor, and the feet rise 1.38 m
over the head (it asks for 1.0). Frames: `~/MEGA/glorified/renders/game/round162/face_plant_v2_sheet.png`.

| Check | Result |
|---|---|
| `HumanShieldTests` (3) | 3 of 3 |
