# 2026-10-25: the unarmed civilians and the death screen by killer

Bruno's order: a test room for "the unarmed civvies", then the game over screen in code ("killed by
assault_rifle_guard"), tested before the assets exist. All numbers are PROPOSED.

## 1. The civilians (`CivilianBrain`, the CIVILIAN TEST room)

The design says "unarmed civilians cower, run and beg" (§3.4) and "an unarmed civvie will always run/cower"
(§11.5). The demo story says "the ones who cannot run kneel". The brain does that:

| Phase | When | What it does |
|---|---|---|
| Idle | It does not know she is there | Stands at its desk |
| Flee | It sees her, is hurt, hears a shot, an alert or a power hit, or sees a death | Screams (the scream alerts the armed ones within 25 m), then sprints to the spot farthest from her |
| Cower | It reaches the spot | Crouches with its back to her |
| Beg | She comes within 3 m, or every route out passes within 2 m of her | Kneels, faces her, and begs (a line from the writer) |

- It never attacks. It has no gun.
- The bark lines come from the writer (`barks.csv`, civilian).
- The poses are placeholders: the vendor crouch for the kneel and the cower (RFab gap 13).
- The lift and the ragdoll work on a civilian as on a soldier. After the drop it runs again.
- Its death shocks the others. `EnemyMorale` now talks to an `IFearful` brain.
- The models are the T19 `labworker` and `nurse`, with 30 HP.
- PROPOSED rule: the auto-lock never picks a civilian. The target switch does, so a kill is her choice.

The room (`CivilianTestSetup`, local only like the AI room): a 16 x 12 m office with desks and a pillar. A dead-end
alcove holds the nurse. Two lab workers sit at desks. A guard with a pistol faces the east wall, so the scream must
call him. Lucy starts at the south end.

## 2. The death screen by killer

- A killer id now names the class and the weapon: `soldier_rifle`, `guard_pistol`, `soldier_gas`,
  `power_armour_launchers` (`DummyShooter.KillerIdFor`). Bruno's "assault_rifle_guard" is `guard_rifle` in this
  form.
- The lookup tries the whole id, then the class (`soldier`), then the fallback.
- `DeathScreenSetup` fills the content from `game/death_screens.csv` of the writer (7 rows). It adds one placeholder
  entry for each test-room killer whose weapon changes the hint.
- Until the picture and the voice clip exist, the screen shows a grey card with the picture note and the killer
  id. The voice line shows as a caption.
- A new room clears the last killer.

## 3. A bug found during the work: no enemy saw her inside a camera zone

The camera zones are trigger boxes on the Default layer, and this project lets queries hit triggers. The Invector
sight line (a Linecast on Default) hit the zone around her. So every enemy in every room was blind while she stood in
a zone. In the first civilian run, nobody saw her. `CameraZone` now puts itself on "Ignore Raycast" (the
director tests the box itself and needs no trigger events). After that change alone, the civilians saw her.

## Proof

- EditMode `DeathScreenContentTests` (4): the killer ids, the lookup order, the CSV quotes, and an entry for each
  killer.
- PlayMode `CivilianTests` (8):
  - no gun
  - they run, then cower
  - the scream calls the guard
  - they beg, then cower again
  - the cornered nurse begs and does not move
  - the auto-lock skips them, and the switch reaches them
  - a death makes the witness run
  - the camera zone stays out of the sight line
- PlayMode `DeathScreenTests` (3):
  - a real rifle kill shows the `soldier_rifle` entry, with the caption of the soldier class
  - a real guard kill shows the `guard` entry from the writer
  - a reload clears the killer
- The "civilians" bot, 7 of 7: `tools/unity.sh play CivilianTest Main_Menu civilians 300`.
- Shots: `~/glorified-body-renders/game/civilians/` (the office, the run, the cower, the beg, the cornered nurse, the
  death screen, the main menu).

## Open for Bruno

1. The auto-lock skips civilians. Keep it?
2. A civilian's scream alerts the armed ones within 25 m. Keep it?
3. The killer id form is `<class>_<weapon>`. Your example "assault_rifle_guard" becomes `guard_rifle`. Keep it?
