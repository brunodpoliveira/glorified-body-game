# 2026-10-29: the armed lab workers and a carried key (rank 6)

The order from Bruno (2026-10-27, evening):

```text
let's start with 6
```

Rank 6 of the design sweep (`2026-10-26_sweeps/design.md`): the knife scientist, the armed lab worker, and a figure
that carries a key item. Bruno said the story docs are out of date, so the names and the numbers here are PROPOSED.

## 1. What is new

| Part | Behaviour (numbers PROPOSED) |
|---|---|
| `labworker_knife` | The lab worker model with a knife. Melee only (`DummyShooter.Weapon.Knife`). It runs at her and finds her again every 0.3 s (`SoldierBrain.Rusher`). No firing spot and no cover. It strikes with the same telegraph and slot rules as a soldier's melee: half the warning of a shot, a reach of 2.2 m. 35 HP. Killer id `labworker_knife`. |
| `labworker_pistol` | The lab worker model with a pistol in both hands. The gun shakes (up to 3 degrees, `EnemyGun.SetHold`), and the lock lands up to 0.35 m off her chest (`DummyShooter.AimSpread`), so a shot can miss when she stands still. No melee. Range 9 m. 35 HP. Killer id `labworker_pistol`. |
| Both | Rank `ArmedCivilian`: they retreat at a fear of 3 (design §11.5) and pay 40 exp. They tremble while they hunt her and shake hard when they cower (`Tremble`). The auto-lock takes them (they are a threat). Their barks are PLACEHOLDERS for the writer. |
| The knife | A PLACEHOLDER model of two boxes (our own data: Invector has no knife). The vendor unarmed attack plays with the overhead stab. |
| A carried item | `EnemySpawn.drop` names a hidden pickup of the room. The figure gets `ItemCarrier`. 1.2 s after the death, the item lies on the floor 0.5 m from the hips, on her side. When a limb lies there, the item goes to the next free point around the body (0.5 m, then 0.85 m). The check uses the bones, because a loaded corpse has its colliders off. |
| The saves | A taken card stays taken. A card she did not take comes back beside the body after a load or a return. |
| LAB TEST room | `LabTestSetup.BuildLabTest`, the LAB TEST button: the hall of the KIT TEST room, a knife, two pistols (`Lab_Gun1` carries the LAB KEY CARD), a barricade, a locked store room with a pill. |

`EnemyGun` runs after `Tremble` now (`DefaultExecutionOrder(30)`), so a shaking hand carries its gun.

## 2. Faults found on the way

The save counted a hidden item as taken: `SaveSystem.CaptureRoom` looked only at active pickups. A save while the
carrier lived, then a load, deleted the card for good. Now the capture looks at hidden pickups too and skips the
ones she took (`ItemPickup.Taken`). The test `ASaveBeforeHisDeath_KeepsTheHiddenCard` failed without the fix
(the card was destroyed) and passes with it.

The first record showed two more faults, now fixed and tested:

- The card floated 7 cm over the floor: the NavMesh lies above the floor. Now a ray finds the real floor.
- The card lay under his legs: the body falls with the legs toward her. Now the drop looks for a free point (the
  tests check 0.19 m of clearance from every limb, after the death and after a load).

## 3. Proof

| Check | Run | Result |
|---|---|---|
| `ArmedCivilianTests` (8 new), final | `runs/20261029-223809-test-playmode-587889` | 8/8 |
| The save fault, fix removed | the run after `runs/20261029-220139-test-playmode-573926` | 0/1, as expected |
| EditMode | `runs/20261029-220414-test-editmode-574918` | 56/56 |
| PlayMode, every suite | `runs/20261029-220452-test-playmode-575342` | 261/261 |
| Bots: structural, look, tutorial, ai, cover, civilians, reactions, lab | the chain after it | 8/8 PASS, triage CLEAN |
| Lab bot, record mode | `runs/20261029-223944-record-588627` | 6/6 steps |

Measured in the tests:

- The knife starts 15.8 m from her, comes to 1.3 m and strikes. It fires 0 rounds.
- After she moves 11.4 m away, the knife comes back to 2.2 m from her.
- The gunman: the gun shakes 3.3 degrees, the body trembles 1.7 degrees, the wrists are 0.09 m apart, the worst miss is 0.20 m.
- The card lies 0.50 m from the hips, 0.21 m from the nearest limb, on the floor.

Clips for Bruno (1920 x 1080): `~/glorified-body-renders/game/lab/clips/`, shots in `~/glorified-body-renders/game/lab/shots/`

| Clip | It shows |
|---|---|
| `lab_01_knife` | the knife runs across the hall and strikes |
| `lab_02_pistol` | the two-hand pistol, the shake, the shots |
| `lab_03_retreat` | a fear of 3: he runs and cowers |
| `lab_04_card` | the carrier dies, the card shows beside him |
| `lab_05_door` | the card opens the store room |

## 4. Open for Bruno

1. The numbers: 35 HP, the spread of 0.35 m, the shake of 3 degrees, the knife's 0.3 s path update.
2. The knife model is two boxes. A free asset, an RFab model, or keep it?
3. Should a carrier show what it carries (a card on the belt, a scan line)? Now nothing shows before the death.
4. The two-hand pistol sits a few cm in front of the fingers (the same grip point as the soldiers' pistols). Fix it now, or with the long-gun hold (item 26 of the roadmap)?
