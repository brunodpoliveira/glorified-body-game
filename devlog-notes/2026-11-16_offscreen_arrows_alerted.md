# 2026-11-16: the off-screen arrow for every alerted enemy

Bruno:

```text
also the enemy marker/indicator when off-screen only shows when they are about to attack. it should be visible always, when they're in alert (that allows players to keep track of enemis and allow us to spring surprises onto the player, since enemies that are huning and unaware do not have the indicator
```

The fact-check: CONFIRMED. `PlayerHud.UpdateThreats` showed an edge arrow only for a shooter that aims at her or
telegraphs. An alerted enemy who walked or held his place had no arrow. The pool also had 4 arrows, and the dining hall
fight has up to 7 enemies.

## The change

1. An enemy out of the frame has an edge arrow while he is alerted and knows where she is (`PlayerHud.AlertedOnHer`).
2. The arrow colours: pale yellow for alerted (`PlayerHud.AlertArrow`), orange for an aim, red for the telegraph. The attack
   icon shows only from the aim on.
3. No arrow for an enemy who does not know where she is: one who never saw her, one who searches for her (the search
   phase), or one whose fire team lost her (`FireTeam.HerHidden`). Also none for the dead and the cowering. So a surprise
   from a hunting enemy stays a surprise.
4. The pool has 8 arrows now.

A soldier stays alerted after he first sees her (`SoldierBrain.Alerted` does not reset). So "hunting" here is the search
phase or the team that lost her. A training dummy has no brain: its arrow comes only with its aim, as before.

## Proof

NOT COMPILED, NOT RUN (no free RAM for Unity). The tests of the dummy's arrow (`DummyShooterTests`) keep their numbers: a
dummy has no brain.

To test by hand: in the dining hall, start the fight and walk so that some soldiers are out of the frame. Expect a pale
arrow for each one, orange when he aims, red at the telegraph. Then hide from them until the team loses you: their arrows go.
