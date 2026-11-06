# 2026-11-06, afternoon: auto-fire, cancel, inventory, the mouse carry, mk_edgar

Bruno:

```text
when the circle for the power is full the power is unleashed automatically. i also need to know if there's any way to cancel a power
a press of the defend key during a charge cancels it. agreed
the inventory needs a rework: three categories: consumables, keys, logs, in that order; can be scrolled each cat by going up or down
orzabal needs another rework: the tk'd object should be able to be moved with a mouse. this will allow us to do physics puzzles with boxes and whatnot
[a picture of a game's hint: press and hold the right mouse button to select and lift object, move object with mouse, release the right mouse button to drop]
the mind control power (mk_edgar) will work like the domination in middle earth games: either in puzzles (so it controls enemies at full health) or when the enemy has at least damage. could be that civvies can be controlled on full health, guards and above need damage to be dominated. bosses cannot be dominated ofc
to not make it too op the player needs to be in range like in the other powers; the enemy is on his knees on the floor
mk edgar needs to spend huge amounts of its mk bar. they are only dominated when the circle is full. mk edgar also needs its own pill
this is the last core gameplay element that needs an isolated test before being implemented in the final game
```

## Auto-fire and cancel

| Piece | What it does |
|---|---|
| Auto-fire | A power fires when the circle fills. Her feet go free. A new charge needs a new press |
| A release before the full circle | As before: the power fires at that charge. Under 20 %: a fizzle, no MK |
| Cancel | A press of the defend key during a charge. No MK and no SHORT. "CANCELLED" on the HUD. That key press starts no floor shield |

## Inventory

Three lists side by side: CONSUMABLES (capsules, the SHORT capsule, the second wind pill, carried objects), KEYS (keys, cards, floor plans), LOGS (logs and tapes). W and S scroll the list and wrap, A and D change the list and skip an empty one. A list shows 6 rows and scrolls.

**S no longer closes it**, because S is down now. I and Q close it, and Esc too. In a question or on a page, S still means no.

## The mouse carry (mk_orzabal)

| Piece | What it does |
|---|---|
| Lift | Hold the defend key for 0.2 s with mk_orzabal. It takes the prop she has switched to. With no enemy locked, it takes the prop she faces |
| Move | Mouse left and right: left and right on the screen. Mouse up and down: away and near. Wheel: the height. Inside her 12 m range |
| Drop | Release the key: the box falls straight down and stays whole |
| Cost | No MK and no SHORT. A puzzle must not eat ammunition, because MK does not come back. A throw from the carry costs a full charge (30 MK) |
| Her feet | She stands still and faces the box while she carries it |
| Floaters | The mouse moves a floating enemy too, and it does not turn her then |
| Physics | A box goes by velocity: a wall or a rail stops it, it pushes a loose box, and it falls and stacks. `PropAssets.Box`, `PressurePlate` |
| Test room | PHYSICS TEST. A rail she cannot cross, a plate behind it, two boxes, a gate that opens while a box lies on the plate |

With an enemy locked, the same key still makes the floor shield. That keeps both on one key.

## mk_edgar

| Rule | What it does |
|---|---|
| Key, bar, capsule | Key 4, its own bar, the EDGAR capsule. All hidden until the flag `mk_edgar_unlocked`. The demo does not have it |
| Who at full HP | Civilians (armed and unarmed) and figures marked for a puzzle (`EnemySpawn.dominableUnhurt`) |
| Who needs damage | Guards, soldiers, the power armour. Any lost HP counts |
| Who never | Bosses (`EnemySpawn.boss`): "IT RESISTS" |
| Circle | Only a full circle, 2.5 s. The boost does not speed it. A release or a cancel does nothing and costs nothing |
| Kneel | He goes down on both knees and holds still during the circle. A hit on her breaks it: "BROKEN", no cost |
| Cost | 75 of 100 MK. So one capsule pays for one domination, and 25 MK stays |
| Range | 6 m. The other powers have 12 m |
| After | He follows her and fights the others. They fire at him when he is nearer, or when they have no line to her. His bullets never hurt her. A violet light over his head marks him (PLACEHOLDER) |
| Puzzle | The defend key sends him to the nearest `SentSwitch`, and he uses it. Test: a guard at full HP behind a rail opens the gate |
| One at a time | A new domination lets the old one go, and that one is an enemy again |
| Save | A load brings him back as an enemy: only the dead are saved |

Test room: EDGAR TEST (local, like the other rooms: its figures are markers, so the scene holds no vendor data).

## What I chose for you (say no if wrong)

1. mk_edgar range 6 m, cost 75 MK, circle 2.5 s, one at a time, no timer.
2. Armed civilians count as civvies: full HP is enough.
3. The power armour can be taken with damage ("guards and above"). Only marked bosses refuse.
4. The carry costs no MK, and her feet stay still.
5. Inventory: columns with up and down inside a column. If you meant up and down to change the category, it is a small change.
6. Kills by a dominated figure give her no exp, but the SHORT bar still rises with each kill, as with every death.

## Limits

- **A dominated figure's grenade or rocket can still hurt her.** Only its bullets are safe. The test room has pistols and rifles only.
- The kneel leans forward with a hand near the floor: the CPR kneel, as a placeholder.
- The mouse moves on the floor plane and the wheel moves the height. Psi-Ops, in your picture, may use the mouse for height. Say if you want that.
- The lines of a dominated figure are placeholders until I take the writer's.
- A box drop hurts nobody. A thrown prop still breaks, as before.
- The tutorial has no step for the carry, the cancel or mk_edgar yet. They come when the power enters the game.
- Nobody played any of it by hand.

## Proof

Filled in at the end of the run.

Shots: `~/MEGA/glorified-body-renders/game/physics_2026-11-06/`, `~/MEGA/glorified-body-renders/game/edgar_2026-11-06/`, `~/MEGA/glorified-body-renders/game/demo/team_ai_2026-11-06/team_00_inventory.png`.
