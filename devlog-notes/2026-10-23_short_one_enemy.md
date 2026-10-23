# 2026-10-23: the short shocks one enemy at a time, single-use props, shock-immune bosses

Bruno (2026-10-23), on the questions of the props note: "1: crate/props thrown are single-use only"; "2: power
armor the only heavy for now; bosses will be in this category as well (and be immune to shock damage)"; and a
report: "i think shock is buggy: after eliminating the 1st enemy, all enemies left then receive equal damage".

## The fact-check of the report

- The code ([ShortController.cs](../Assets/_Game/Scripts/Player/ShortController.cs), before this change): every
  shock hit every live enemy inside 6 m, 12 damage each. Nothing changed after a kill. The first enemy most
  likely died first because it was nearer or weaker.
- Verdict: WORKS AS DESIGNED, on the old design. His ruling of 2026-10-22 (`03_GAME_DESIGN.md` §11.4, "one
  shock, one enemy, the others watch") was in the canon but not built. No test covered the shock damage to the
  others.

## What changed

1. **The short hits one victim.** Every shock hits the nearest live enemy in the radius. It stays on that enemy
   until it dies or leaves the radius, then the next nearest. A spark shows on the victim. The damage per shock
   (12) and the rate (one shock in 0.4 s) stay the same, so one victim takes 30 per second. The others take
   nothing and can react (the FSM AI work).
2. **Shock-immune targets.** `LockOnTargetable.IsShockImmune`: the shocks pass it by and go to the next enemy.
   For the bosses. The power armour is heavy but not shock immune.
3. **Single-use props.** A crate breaks where a throw or a slam lands it: six pieces with its texture fly apart
   and vanish after 1.2 s. A crate that an enemy's hit drops stays whole. The slam case is PROPOSED (his words
   name the throw).

## Proof

- PlayMode `PowersAndBarsTests`:
  1. `Short_ShocksOneEnemyAtATime_TheNearestFirst_ThenTheNext`: after 1 s the nearer dummy took the shocks and
     the other one has its full HP. After the first dies, the shocks move to the second.
  2. `Short_PassesAShockImmuneTarget`: the immune dummy keeps its full HP, the other one takes the shocks.
  3. `Props_LiftAndThrow_TheCrateHurtsTheEnemy_Breaks_AndTheLockGoesBack`: the crate is gone after the throw,
     its pieces are in the room.
- PlayMode 92 of 92, the tutorial bot 21 of 21, the structural bot 24 of 24, all CLEAN. In the tutorial bot run,
  every shock of the short step hit Target_1 ("short_shock ... hit=Target_1"), and the crate broke after its
  throw ("prop_broken Prop_Crate"). The tutorial bot also ran the bot clock of the record mode work (another
  session), in its normal real-time mode.
RUNS: 20261023-003936 (PlayMode), 004534 (the tutorial bot), 004916 (the structural bot).

## Bruno's answers (2026-10-23)

1. "keep 12 dmg per shock": one victim takes 30 per second, a 50 HP soldier dies in about 1.7 s.
2. "go to nearest target": the short picks the nearest enemy, not the locked one.
