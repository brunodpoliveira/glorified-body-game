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

## Later that day: the aim, the stagger, its life, the default lock

```text
the brain boss is impossible to attack; it locks and tracks so effectively i don't have any opening to attack. what it
is supposed to ahappen is that the brain starts unlocked, then it tracks the player at some speed ()that they can
oputrun dogde. then once fully locked it charges up and then fires/damages the player; making enough damage staggers
the brain to cancel the attack. iot also needs hp for the boss
to be clear, the player can make enough damage to the brain so the brain will stagger
and also, lucy should aim at the brain as default; it should by default lock on onto the brain
```

The old strike was a ring that always closed on her, so she had no time to attack. The new one (numbers PROPOSED):

1. The aim, a ring on the floor, starts in the corner of the sector farthest from her. It follows her at 1.4 m/s.
   Her walk is 1.6 m/s and her run 3.6 m/s, so she can outrun it. Her slow walk on the brain floor (about 1 m/s) is
   slower than the aim.
2. While she is inside the ring (0.9 m), the lock fills in 1 s. Outside, it drains twice as fast.
3. A full lock starts the charge (2 s). The aim still follows her at its speed. At the end, the strike lands on the
   aim: 25 damage within 1.1 m. She escapes it if she runs out of the ring in time.
4. 30 damage to the brain during the charge staggers it (a full blast does 35, 70 with the MK boost). The strike does
   not come, the brain reels for 2.5 s, and the aim starts again in the far corner.
5. The brain is one body now: a mass high on the north wall, over the builders' door, with 600 HP. The four wall
   nodes are gone. Its life is on a bar at the top of the screen ("THE BRAIN").
6. The auto-lock picks the brain first, out to the drop range (`LockOnTargetable.IsPreferred`). Tab reaches a grabber,
   and when the grabber is gone the lock goes back to the brain.
7. A throw at a target high on a wall now rises to its lock point (`TelekinesisLift.HighTarget`). Before, a throw
   arced down to the chest of a target on the floor and passed under the brain.

`BrainFightTests` (12: the aim, the lock and the charge, the stagger, the life bar, the default lock) and
`PowersAndBarsTests`: 64 of 64. The brain bot: 7 of 7. Shots: `brain_fight_2026-11-08/v2_*.png`.

## Later that day: the brain at its eye, the MK bars and the charge circle

```text
the target should be where the camera is at; the idea is that we're seeing it from the brain's pov; and also have the
mk bars present and the circle charge yi, it makes it hard to gauge how much it has left when disabled as it is rn
```

1. The brain's body is at its eye now: a mass high on the south wall, just behind the camera. The camera does not
   show it. To hurt the brain, Lucy turns to the screen.
2. The brain fight keeps the HUD: the MK bars, SHORT and the charge circle. The "Crown gone" flag no longer hides it
   during the fight. The HP row goes, because "SUBJECT n%" is her HP as the brain reads it.
3. The charge circle goes to the target's place on the screen. A target off the screen (the brain behind the
   camera) puts the circle over Lucy. Before, the circle stayed where it was last.

`BrainFightTests` and `PowersAndBarsTests`: 64 of 64 (new checks: the body at the eye, the HUD shown with no HP row,
the circle over her). The brain bot passes, with a new shot of her charge (`v3_brain_03_her_charge.png`).

## Later that day: a grabber she can dodge

```text
the grabbers are still impossible to dogde
```

Verdict from the code: CONFIRMED. The reach moved the head to her chest of that frame, on every frame. At the end
of the reach the head was on her, and it held her each time. Now (numbers PROPOSED):

1. The wind-up (0.6 s): the head swells and pulses at the mouth, a little out toward her.
2. The lunge (0.5 s): it locks her place at the end of the wind-up and thrusts there in a straight line, faster at
   the end. A sound plays at the start of the lunge.
3. It holds her only if its head comes within 0.6 m of her chest in the lunge. Else it misses and goes back.

A walk, a run or a dodge to the side in the lunge escapes it. A hit on it in the wind-up also sends it back.
`BrainFightTests` 13 of 13 (new: `AGrabber_SheStepsAside_ItMisses`). The new test needs the new wind-up state, so it
did not run on the old code; the verdict rests on the code. The brain bot passes.

## Later that day: its own music

```text
there should be an unique song playing as soon as the fight starts; as it is it keeps on the nomral explortation song
until i attack, then it switches to combat ost for a while and then it goes back to it again
```

Verdict from the code: CONFIRMED. `BrainFight` never called the music system, so the room's rules played: the explore
track, then the combat track for 8 s after each hit. Now the fight starts its own track at once
(`MusicDirector.Score(clip, fight: true)`), keeps it through every hit, stops it at the brain's death (the flare in
silence), and gives the music back to the room when it is dead. The track counts as a fight, so a tape waits
(`MusicDirector.InFight`). The placeholder `Resources/Audio/Music/_placeholder/boss.ogg` (38.5 s, a heartbeat, a low
drone, a dissonant bell) comes from `tools/boss_music_placeholder.py`. `BrainFight.music` takes the real track.
`BrainFightTests` (14), `AudioLogTests` and `SoundSystemTests`: 27 of 27. The brain bot passes.
