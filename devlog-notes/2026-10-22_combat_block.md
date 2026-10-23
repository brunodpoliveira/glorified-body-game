# 2026-10-22, late: the combat block (the Telekinesis hold, the Fire stun, the MK boost)

Bruno (2026-10-22, late): "yes, makes more sense: combat block, props, FSM AI"; "tk still drops the dummy after a
time fyi"; and yes to "should a drug used from the inventory ask first". The rules are his words of the night,
in `03_GAME_DESIGN.md` §12:

- "once target grabbed and floating, telekinesis last until tgt thrown, slammed (full charge on target when it
  is floating) or damaged by another enemy (in this case, the floater is released with no damage)"
- "fire only damages on full charge; anything less just stuns them"
- "if mk/nalcon at 50%+ charge, attacks have 2x damage, and charge up 2x as quickly; a risk-reward for the player
  (keep yourself topped up for max strength, or let it drain to save up on resources)"

The drop he saw was the old design: the lift hung for `holdSeconds` (1.5 s) and then slammed by itself.

## What changed

1. **Telekinesis holds.** `TelekinesisLift` has no timer now. The floater hangs and turns with her until one of
   three things happens:
   - a short press of the attack key throws it (into the enemy she faces, both hurt, as before);
   - a full charge while it hangs slams it into the floor (the power's full damage, the power's nalcon cost,
     PROPOSED);
   - a hit from someone other than her drops it, unhurt: an enemy bullet that strikes it, or any hit on the
     hit bus from another attacker. A floater that dies (a burn) drops too.
   The attack key charges as usual while she holds it; the release decides: under a full circle it throws, at a
   full circle it slams.
2. **Fire.** A full charge is the heat hit and the burn, as before. A part charge does no damage: it only stuns
   (it breaks a shooter's aim for `staggerSeconds` times the charge).
3. **The MK boost.** At half the nalcon bar or more, every attack does 2x damage (the burn too) and the circle
   fills 2x as fast. The boost is read before the cost lowers the bar. The HUD shows a white tick at half the
   NALCON bar and "x2" on the bar while the boost is on. The name stays "nalcon" until the rename (later).
4. **The inventory asks** before it uses a drug: "Use the red?", Interact yes, Back no.
5. **The tutorial** has a slam step after the throw (18 steps). The padded targets have 400 HP now: the boost
   doubles every hit and they must last the whole demonstration.

## Proof

- PlayMode `PowersAndBarsTests`: `Telekinesis_Holds_UntilAFullChargeSlamsIt` (still up after the old 1.5 s window,
  a full charge slams it, full damage, full cost), `Telekinesis_AnotherEnemysBullet_DropsTheFloater_Unhurt` (a
  real enemy bullet from the other dummy), `MkBoost_AtHalfABarOrMore_DoublesTheDamage_AndTheChargeSpeed` (the
  circle full in half the time, 2x damage, "x2" on the HUD), the Fire tests (a part charge does no damage and
  breaks the aim), the throw by a short press. The setup holds nalcon at 45 with the regen paused for the base
  numbers.
- PlayMode `InteractionTests`: the inventory asks "Use the red?" before it uses a drug.
- PlayMode `TutorialTests`: eighteen steps, the slam included.
- Three tests and the structural bot needed their numbers fixed for the new rules: a boosted blast kills the
  50 HP dummy at once (the stagger test now fires without the boost, the bot reads the SHORT bar before the
  blast), the SHORT drain needs the regen running, the inventory asks first.

EditMode 36 of 36, PlayMode 87 of 87, the tutorial bot 20 of 20, the structural bot 24 of 24, all CLEAN. In the
bot's play the slam step took 16 s: the earlier steps drained the nalcon, and the slam waited for the regen to
pay its 30. The walk to the door stopped once against the parallel bars again (the second try passed): the
path issue of the drive, for the props work.
RUNS: 20261022-222853 (the tutorial), 223714 (EditMode), 224501 (PlayMode), 225052 (the tutorial bot), 225406
(the structural bot).

## For Bruno

1. The slam costs a full charge of nalcon (30) and does the power's full damage (40, 80 boosted). The throw is
   free (the lift paid). Say if the slam should be free too.
2. Her own power on a floater (a blast) does not release it now: only another enemy's hit does, as you said.
3. The rename to MK and the new power names wait for your word on when.
