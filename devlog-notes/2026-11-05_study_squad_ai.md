# 2026-11-05: study, the soldiers as a team (not built)

Bruno:

```text
a refiniment to the ai: the soldiers have a tendency to bum rush; could be the level design, but i'd like to see an overhaul
of it. they need to be (and look and feel like) an organized team. it should feel like an aaccomplisment killing them all, the
player shouid success by outsmarting them

researcdh to see if invector ai has stuff like bounding overwatch etc. thius is just an nnotation for later since the level
design needs a refinement for v2 to fully shine. research it now
```

## The answer: Invector has no team AI

I searched all the Invector C# code in the project.

| Term or feature | Result |
|---|---|
| overwatch, bounding, suppress, flank, squad, fireteam | 0 files |
| Attack tokens, a combat manager, AI groups | None. The hits are other words, for example `AICombatting` and `GUI.BeginGroup` |
| `vAICover`, `vAICoverArea`, `vAICoverPoint`, the FSM action "Get Cover" | Yes, for one AI. It finds a cover point away from the threat, ducks, and changes cover on a timer or on damage. A point has `isOccuped`, so two AI do not take one point. Each room needs cover areas placed by hand. Our soldiers do not use it |
| `vAICompanion`, "Go To Friend" | One AI follows one friend. It is for an ally of the player |
| `vAISendMessage` | A message from the FSM to the same AI, not to another AI |
| Noise and target decisions | One AI hears and reacts. Our `EnemyAlert` does more |

So bounding overwatch goes in our code, behind `IEnemyBody`, on top of `FlankBoard`. The vendor cover data is a possible source
of cover points, but our own points cost less: `vAICover` is built for its own combat state, and that state moves while it fires.

## Why they rush: the code, more than the level

Your guess is partly right. From the code (I did not measure it in play):

1. `preferredRange` is 6 m, and the second try is 3.9 m. Every firing spot lies on a ring close to her. Nothing lets a soldier
   stay at 12 m when he has a clear line.
2. Each soldier decides alone. All of them walk at the same time, so all of them arrive around her in a few seconds.
3. They always run or sprint (ruling of 2026-10-24), so the approach looks like a charge.
4. They only count breakable barricades as cover. They ignore planters and columns, so G2's planters are not cover for them.
5. The level adds to it: small rooms, one way in. That part waits for the second pass (`DEMO_PLAN.md` §12).

## What other games did

- **F.E.A.R. (Orkin, GDC 2006).** One coordinator clusters the AI into squads, and a squad runs zero or one behavior at a time. Four
  simple behaviors: Get-to-Cover and Advance-Cover (one member suppresses while the others move), Orderly-Advance, and Search (pairs
  that cover each other). The paper says it had no complex squad behaviors: flanks and pincers appear when soldiers move to the only
  valid cover. Dialogue does much of the work: "Having A.I. speak to each other allows us to cue the player in to the fact that the
  coordination is intentional". It uses a dialogue between two soldiers, not one announcement.
- **Killzone (Straatman and van der Sterren, GDC Europe 2005).** A soldier scores nearby positions with a weighted sum: cover, line of
  fire, proximity, preferred range, danger zones, and squad limits. Suppression fires near a hidden threat, to deny it good attack
  positions, and not into the wall in front of it. Pathfinding uses the same scores, so a soldier avoids routes under fire.
- **Bounding overwatch.** One team moves while the other covers it, and at the new position they swap.
- **Opsive Tactical Pack (a paid package).** It has Leapfrog, Flank, Surround, Ambush, Shoot and Scoot, Defend and Retreat, and it leaves
  weapons and damage to the game. Not recommended: it needs Behavior Designer Pro, its defaults are for open ground (rows, group
  spacing 10, leap distance 20), and our small rooms and fixed cameras need other numbers. I did not try it.

## The proposal for v2 (PROPOSED)

| Piece | What it does |
|---|---|
| `FireTeam` (grows from `FlankBoard`) | One coordinator for each fight. Two elements of two. One order at a time |
| Bound | One element moves to its next position and the other holds and fires, then they swap. This fits "move or shoot, never both": a mover never fires |
| Hold range | A soldier with a clear line and cover stays there (8 to 15 m). He closes in only when the order says so |
| Suppress | When she hides, the overwatch fires near her last known spot, with a tracer the player can read |
| Flank | One element goes around only when the other one has her pinned and another route exists. This is what needs the v2 rooms |
| Search | When they lose her, pairs that cover each other go through the rooms |
| Position score | The Killzone sum over cover points that the room builder marks (planters, columns, corners) |
| Barks | "MOVING!" and "COVERING!" as a dialogue between two named soldiers (the writer, §13.4 has the names). The cheapest piece, and the one the player notices most |

How the player outsmarts them. Each order needs a weak point that she can read and use:

1. The moving element is in the open and cannot fire, so she strikes in that window.
2. Kill or lift the overwatch soldier: the bound stops, and the movers are exposed.
3. Jam a fire door to split the two elements.
4. Break the cover they stand behind, and throw one into the other.
5. The team gets worse as it dies: at two left, the two fall back by bounds to the last good cover and call for help. Soldiers never
   run (his rule of 2026-10-22), so this is an order and not a panic. The last soldier alone is the reward.

The difficulty slots stay: the overwatch element holds the slots for the number of shooters.

## Order and cost

This waits for the level design of v2. The first part, range and bound, can show in G2 before that. My estimate for all of it is 4 to 6 days,
not counting the barks and the rooms.

## Sources

- [Orkin, Three States and a Plan: The A.I. of F.E.A.R., GDC 2006](https://pages.cs.wisc.edu/~dyer/cs540/handouts/gdc2006_orkin_jeff_fear.pdf)
- [Straatman and van der Sterren, Killzone's AI: dynamic procedural combat tactics, GDC Europe 2005](https://www.guerrilla-games.com/media/News/Files/gdce05_killzone_ai.pdf)
- [Bounding overwatch, Wikipedia](https://en.wikipedia.org/wiki/Bounding_overwatch)
- [Tactical Pack for Behavior Designer Pro, Opsive documentation](https://opsive.com/support/documentation/behavior-designer-pro-tactical-pack/)
- Invector: `Assets/Invector-FSMAIController/FSM AI Documentation.pdf` (AI Cover System, AI Companion, AI Send Message) and its `Scripts/AI/`.
