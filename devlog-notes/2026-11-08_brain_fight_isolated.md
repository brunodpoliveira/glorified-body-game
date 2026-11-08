# The brain fight, a first slice in isolation (2026-11-08)

This is B1, the fight the demo waits for (`DEMO_PLAN.md` §14.1 and `LORE.md` §5, CANON round 10). CANON round 52 says "the demo does not ship before it exists". I built a first slice in a test room, after Bruno's words of the night:

```text
add what can be tested in isolation after this round of testing is done
```

## What the design fixes, and what I chose

| Part | Source | Built as |
|---|---|---|
| The camera is the brain's eye, high in the sector | CANON | A fixed camera high on the south wall that follows her (55 degrees at most) |
| A ring closes on her. The strike lands where she stood at the close | CANON ("Keep moving: it strikes where you stand when the ring closes") | A red ring on the floor shrinks over 1.6 s, with a laser from the eye. Then a red disk marks where she stood. The strike lands 0.35 s later: 25 damage within 1.1 m |
| Standing still slows her | CANON | 1 s still, then the slow walk of the short for 2.5 s |
| Grabbers pin her for 2 s. A blast frees her. mk_orzabal rips one out and throws it | CANON | A chain of capsules comes from a mouth in the wall. Any hit of hers frees her (the design says a blast). mk_orzabal tears a grabber out, and she throws it at a node |
| No HP bar. The work lights go out as it dies | CANON | Six work lights. The share of its life sets how many are on |
| Its death. Every light flares, then they go out one by one. Its line, then the exit opens | CANON (round 58) | As written. "I was meant to be beautiful." is a subtitle |
| The short and mk_edgar do nothing to it | CANON (rounds 36, 37) | Its parts are shock immune and too heavy to lift |
| How she hurts it | Open | PROPOSED: four nodes on the walls, 150 HP each. Her powers hurt them, and so does a thrown grabber |
| What the screen shows without the Crown HUD | Open (cutscene 09, note 3: "The game agent decides") | PROPOSED: the brain's own view. A lock frame on her closes with the ring and turns red. "SUBJECT 64%" shows in red at the top left. The Crown HUD is hidden (`demo_crown_gone`) |

All numbers are PROPOSED.

## The code

- `Scripts/Boss/BrainFight.cs`: the eye, the strike loop, the floor, the grabber timer, the lights, the death.
- `Scripts/Boss/Grabber.cs`, `Scripts/Boss/BrainNode.cs`.
- `Scripts/UI/BrainHud.cs`: the lock frame and the vitals.
- `Editor/Demo/BrainTestSetup.cs`: BRAIN TEST, in the TEST ROOMS column. She starts in B0, the wing. B1 is the pink-grey sector, 18 x 12 m and 6 m high, with the label L6 on its wall. B2 is the builders' door.

## Proof

| Check | Result |
|---|---|
| `BrainFightTests` (8), run after the last HUD change | 8 of 8 |
| Bot `brain`, run `20261108-025132-play-103590` | pass, 6 of 6 |

The first bot shots showed the lock frame beside her, not on her. The frame used screen pixels, and a shot renders at another size. It sits at her viewport point now.

Shots: `~/MEGA/glorified-body-renders/game/brain_fight_2026-11-08/`.

## Not yet done

1. The death screen from the brain's view: the row `brain` exists, and the strike sets the killer id. Nobody died to it in a run yet.
2. The real look: the room, the eye, the grabbers and the nodes are greybox.
3. The feel: the timings and the damage need Bruno's hands on the keys.
4. B1 in the demo, between U1 and X1, with cutscenes 09 and 10 around it. About 0.5 day after Bruno's feedback.
