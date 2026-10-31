# 2026-10-31: the floor shield

The design (§2.3, CANON): "It rips chunks out of the floor as a shield". Bruno, 2026-10-20: "RMB hold = the floor
shield, the defensive full charge". The design sweep's rank 15. The numbers and the look are PROPOSED.

## What it does

| Part | Behaviour |
|---|---|
| The key | With mk_orzabal selected, hold the defend key (right mouse button). A tap is still the bullet catch. A hold past 0.2 s fills the circle. She stands still and faces her target, as for an attack. |
| The full circle | A slab of the floor in front of her (1.0 x 1.2 m) tears loose, stands up and hangs 0.9 m in front of her. It costs the full charge of mk_orzabal (30 MK). A release before the full circle costs nothing. |
| The scar | A dark plate stays on the floor where the slab tore out. |
| It turns with her | The slab is a held prop. It swings around with her and faces the way she faces. She walks with it in front of her. |
| Bullets | Enemy bullets stop on the slab and wear it down (80 HP: six pistol rounds, four rifle rounds). At 0 it shatters. Her returned bullets fly through it. |
| The soldiers | A soldier needs a clear line to fire. With the slab between them he holds his fire and walks to find a line from the side. She turns the slab to him with the turn keys. |
| Throw and slam | The attack key throws the slab at the target in front of her, like a crate. It hurts a heavy target too (the power armour cannot be lifted, but a slab hits him). A full circle slams it. Either breaks it. |
| One at a time | A second hold does nothing while a slab hangs. |
| Other powers | The hold does nothing with mk_warspite or mk_erebus. She can blast with mk_warspite while the slab hangs. |
| The HUD | The circle over her fills for the shield as for an attack. |
| The remap list | "Defend (tap: catch, hold: floor shield)". |
| Tutorial | A new step "floor_shield" after "catch": lock the gun, hold the defend key, let the gun hit the slab. The tutorial has 26 steps now. |

The code: `Combat/FloorShield.cs` (the slab, its HP, the scar, the turn), `PsychicPowers.UpdateShield` and
`TryRaiseShield` (the hold and the full circle), `EnemyProjectile` (a bullet stops on the slab).

## Proof

| Check | Result |
|---|---|
| FloorShieldTests (4): the hold tears a slab out in front of her, between her and her target, upright, it turns with her, one at a time; bullets stop on it and break it at 80 HP, and the next one rises; the attack key throws it and it hurts the target; another power does nothing | 4/4 |
| The bot: `tools/unity.sh play AiTest Main_Menu shield 300` | 4/4 steps: the slab tears out; in 12 s facing the rifle soldier no rifle bullet reached her (he held his fire); three rounds stop on the slab; the throw takes 80 HP off the power armour |

Shots for Bruno: `~/MEGA/glorified-body-renders/game/shield/shots/`

| File | It shows |
|---|---|
| `shield_01_shield_up.png` | the slab in front of her, the scar on the floor, the soldier and the armour behind it |
| `shield_02_shield_takes_fire.png` | the soldier runs for a clear line, the slab takes his rounds |
| `shield_03_thrown.png` | after the throw at the power armour |

## Open for Bruno

1. The slab tears out of any floor. Should only some floors give a slab (cracked tiles, loose plates)?
2. A rocket bursts on the slab, but the blast can still reach her at 0.9 m. Should the slab stop the blast too?
3. The scar is not in the saves: a load or a return shows the floor whole.
4. 80 HP and 30 MK: good numbers for a first test?
