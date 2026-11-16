# 2026-11-16: the concourse fight stays in the concourse

Bruno, with a Scene view shot of G2:

```text
the fight on the main hallway has 10+ enemies, way too many. we need to refine this so that only the ones that are supposed to come in do so; reorganize the level if you have to. they come in from every which way.;  this is toughter than the setpiece fight on the restaurant!
i think that enemies from the foyer are coming in as well
i see some scientists as well
```

The fact-check: CONFIRMED, from the code. The concourse G2 has its four soldiers. A soldier who heard an alert, a shot
or a power hit checked `Room.Hear`: his room and the rooms next door (2026-11-02). The foyer G5 is next door through its
fire door, with two soldiers and two scientists with pistols (`Lab_Pistol_A`, `Lab_Pistol_B`). So the first shot in G2
called the four of G5, and they came through the open fire door. The dining hall's soldiers hold their post, and its
other neighbours have no armed figure.

The change: a soldier hears an alert, a shot or a power hit in his own room only (`SoldierBrain.OnAlertHeard`,
`OnHitHeard`: `Room.Same`). A room's fight stays in its room. A soldier who sees her still chases her through the doors
(Bruno's pick of 2026-11-02). Civilians still hear next door: they hide and run, and their screams call only the soldiers
of their own room.

No change to the level: G2 keeps its four, and the reinforcement of four still comes after G2's four die and sector C is
released.

NOT COMPILED, NOT RUN (no free RAM for Unity).
