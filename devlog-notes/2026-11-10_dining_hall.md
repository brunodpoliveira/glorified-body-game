# 2026-11-10: the Dining Hall (step 4 of the day)

Beat 23b (CANON round 60: the set piece, the obligatory item; round 62: the second fireteam, the duty key, the name).
The details are PROPOSED.

## 1. The rooms

1. G18, THE DINING HALL, east of the concourse G2 through its glass doors (2.4 m). Round columns, banquettes, the bar
   at the east wall, four tables on their sides (the soldiers' cover), the host stand with L31 and L32, the brass stand
   (TONIGHT: THE WINTER MENU.), the lodge door, locked tonight, with its two texts. The words over the glass doors are on
   the G2 face of the wall.
2. G19, THE KITCHEN, south of the hall. The pass with L33, the range, two walk-ins, the night manager of the lodge by
   walk-in 1 (a hider: his lines are PLACEHOLDERS), the kitchen office with the key board, the DUTY KEY on its hook and
   the key log L34.
3. G9: the release of sector C takes the duty key. Without it the console says DUTY KEY REQUIRED.
   (`FlagSwitch.missingText`). L30 lies on the console wall, and a sign names the key switch.

## 2. One place differs from the plan

The plan has the service exit of the kitchen open into G9. On the built map G9 has no free wall: G5, G6, G9b and G12
close it in. The service exit opens into G9b, the comms closet, and G9b opens into G9. It is a jammed fire door with
one lever, on the kitchen side (the push bar), and no kick breaks it. The serving hatch to G10 is a shutter on each
face of the wall, with SERVERY CLOSED., and no opening yet. Bruno, the same day:

```text
1: it should open into g9b
```

So it stays as built.

## 3. The forward post

1. The second fireteam of four holds the hall: Ortiz, Brandt and two more (PROPOSED names from the plan).
   `EnemySpawn.leashUntilFlag` puts each one on a post (`SoldierBrain.LeashUntil`): every move of the fight ends inside
   the room until she takes the duty key (`demo_duty_key_taken`). Then they follow her.
2. A man on a post fights only with the men of his post (`FireTeam.Join`). The first test run showed why: the four of
   the hall heard the fight of G2 next door and joined its team, and the team then moved both elements 100 % of the
   time.
3. The hall has its own track (`Audio/RoomScore`, DEMO_PLAN §12, round 103): it starts when she is in G18 and one of
   the four is alerted, and it ends with the last one, or when she leaves the hall. The track is a placeholder
   (`tools/dining_hall_music_placeholder.py`: a breakbeat, a sub bass, a string pad).

## 4. A fault found by the exit rule

The rack on the east wall of G9b (`Rack_E`, 4.4 m long) stood in front of the new service exit. It hid the doorway
from every camera of G9b and blocked the way. The rack is 2.2 m long now, at the south end of the wall, and the door
moved to z = -10.8, clear of the north rack.

## 5. A flaky test made firm

`DemoTests.ACrateOnTheRim_SlidesIntoTheGap` failed once in this round, after it passed in every run since 2026-11-08.
G16 did not change. The test did not skip the wake cutscene, and the cutscene stops the world, so its physics
depended on the length of the cutscene. The card RAVEN made the cutscene 3.5 s longer, which can explain it (not
proved). It skips the wake now, as the other demo tests do.

## Proof

| Check | Result |
|---|---|
| `DemoTests`, `FloorplanV2Tests`, `FlankingTests`, `DoorTests` | 73 of 77 in the first run |
| The four failures, fixed, and the cull, gap, coverage and fire door tests again | 10 of 10 |
| New tests | `TheDiningHall_G18AndItsKitchen_TheDutyKey_TheLogs_TheServiceExit`, `TheForwardPost_HoldsTheHall_UntilSheTakesTheKey_AndItsTrackPlaysWhileTheySeeHer`. The dark room test now needs the key |
| The demo bot, the team bot | pass |

Shots: `~/MEGA/glorified/renders/game/dining_hall_2026-11-10/` (G18 with the four, G19, the service exit in G9b).
Numbers PROPOSED.

## The full check (step 5 of the day)

The whole suite: 477 of 479. `TheFrame_Profile` is explicit, so the suite skips it. One fault was in a test:
`TheCafeteria_G10_...` found the radio by the name "Radio", and W13 has a prop with the same name. It now finds the
`Broadcast` of G10, and it passes. The other names that the new tests look up appear once in the scene.

All 27 bot runs pass, the tutorial bot included.
