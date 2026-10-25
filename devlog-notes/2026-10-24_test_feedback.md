# 2026-10-24, late: Bruno's first manual test of the day's work

Bruno ran `2026-10-24_manual_tests.md` in the editor. I checked each note against the code before a fix. The
numbers are PROPOSED.

| His note | Check | Fix |
|---|---|---|
| "the animation for the shotgunner + assault rifle is incorrect" | CONFIRMED. A long gun sat in the right hand like a pistol. The builder deleted the left-hand grip (`leftHandIK`) | The grip stays. `EnemyGun.HoldWithLeftHand` moves the left hand to it. Shots: `ai/pose/` |
| "the assault rifle also keeps the muzzle flash effect even before the enemy fires" | CONFIRMED, slightly different. The vendor `Fire_1` loops. After the first burst, the flash stayed on | Every flash plays once |
| "melee attack should have smaller window than projectile attack" | A change | A strike gets half the warning of a shot (`DummyShooter.MeleeWindowShare`) |
| "blood pools disappear, and too quickly" | CONFIRMED. The floor stains were particles that lived 6 s | `FloorStains`: flat stains that stay for the room. Above 300, the oldest goes |
| B9: "the soldier says man down but does not go to cpr" | WORKS AS DESIGNED. Care starts when a soldier does not see her for 3 s. The cover room is open | F12 (debug): the care counts her as unseen |
| C3: "it never breaks (too low, they always hit her)" | WORKS AS DESIGNED, wrong for her. The soldiers shoot over a 1.2 m barricade | Two tall screens (1.9 m) on her side. They stop the bullets and wear down. They stop her powers too |
| C4, C7: "can't break tables reliably" | CONFIRMED. No soldier stood near a table | Tables at 15 HP, two beside the barricades. A full blast splashes 2 m |
| C5: "can't hit the barricade; being hit does not drop the soldier" | CONFIRMED, different cause. A weak lift gave a weak throw, which stopped at the barricade | A thrown body breaks through any cover. A prop keeps its damage |
| Tutorial: "no damage, and infinite mk charge" | He wants it ("I want the tutorial to work that way") | She takes no damage, and the MK stays full |
| Tutorial: all four dummies in the weapon steps; Target_3 moved by mk_orzabal | A change | A weapon step shows only its dummy. Every step puts the dummies back |
| "Q can't use the pills straight away ... a submenu so the player can choose which to take (or take none)" | A change | Q opens the drugs. E asks, E takes. Q or I closes. Bots use `DrugPress.TakeFirst` |
| Tutorial short step: Q first, then the powers, soft-locks | CONFIRMED from the code. The drug empties the bar, and it was the only one | The step refills the bar and gives a drug. A new test |

The screenshots also showed "IMPORTANT: MIS Refactoring is required!": the editor overlay of the MIS add-on in the
Scene view, not the game.

## Proof

- PlayMode: new `TutorialTests.TheGym_NoDamage_AndTheMkStaysFull`, `TutorialTests.ShortStep_ADrugFirst...`,
  `BreakableTests.TallScreen_StopsTheirBullets_AndWearsDown` (3 bullets at her chest behind the screen: 90 to 45).
  The tutorial, cover, gore and powers tests: 48/48.
- The "ai" bot takes two close shots of the long guns while they aim (`ai_02b_pose_*`). The left hand holds the
  gun in the aim. In a walk the hand sits by the grip, open (the hand keeps its animated turn).
- The full run: see the reply of the day.
