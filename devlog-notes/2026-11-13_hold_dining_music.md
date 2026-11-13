# 2026-11-13: the Hold (map v3), the Dining Hall at six plus the armour, the union sheet, the music states

The writing chat sent the changes of rounds 118 to 123 (`RULINGS.md`, `DEMO_PLAN.md` §2.1, §2.2, §3.4, §3.5, §5.0,
§6, §8, §10.6, §12) and one CSV row to check. Everything is greybox. Numbers are PROPOSED.

## 1. The brain row of `death_screens.csv`: READ WRONG

1. The reader: `Editor/DeathScreenSetup.Fill` reads the writer's file with `ReadCsv` (quotes, commas, CRLF) and
   takes columns 0 to 3 only: enemy, picture, voice_line, hint. Speaker and outcome are not read.
2. The `brain` row has an unquoted hint with two commas, so it splits into 8 columns. Column 3 (hint) is only
   "Its ring is slower than your walk: keep moving. When it locks". The rest goes to the speaker and outcome columns
   and to two extra columns, and the game drops it.
3. On screen: the brain's death screen shows that cut hint. The asset in git (`Resources/DeathScreenContent.asset`)
   already holds the cut hint.
4. Proof: the new EditMode test `DeathScreenContentTests.TheWritersFile_EveryRowHasTheHeadersColumns_AndTheBrainHintIsWhole`
   prints the 8 columns and the stored entry, and fails: "brain (line 6: 8 columns)". It stays red until the writer
   puts the hint in double quotes. The game code does not change.
5. The fix for the writer: the hint cell in double quotes:
   `"Its ring is slower than your walk: keep moving. When it locks, hurt it before the charge fires, or leave the ring. A grabber swells before it lunges: step aside."`
   After that, `tools/unity.sh exec GlorifiedBody.Editor.DeathScreenSetup.Build` writes the whole hint into the asset.
6. Done the same day: the writer quoted the voice line and the hint, every row parses to 6 columns, the build wrote
   the whole hint into the asset, and the EditMode suite is green (63 of 63).

## 2. The Hold (map v3): BUILT

`Editor/Demo/DemoHold.cs`. W1 and W1B are gone. The ten rooms (H1 to H9 and the tube HT) stand where the writer's
`game/act1_map_v3.py` puts them, with three changes for play:

1. The gallery H7 is 6 m deep (the drawing has 2 m: too narrow for four fighters). So H6, the box and the hall stand
   5 m further north than in the drawing.
2. The tube is a room of its own, HT (6 x 2.4 m), so the cameras see the ride.
3. The box walls are opaque grey (the clear walls are a look item), the hall is black boxes with no collider, four
   cables.

What is in the game:

| Beat | What it does | Code |
|---|---|---|
| 1, 1b | Black, cutscene 01 (version 3), then she is in the upright frame. Every MK bar at 0, SHORT 90. Interact 12 times: the straps give one by one, the line tears out, the cage falls. The flags `hold_free` and `hold_lockdown`; the airlock lamps go red. One WARSPITE on the tray | `Interaction/UprightRestraint`, `StepKind.Restraint`, `PsychicStats.EmptyMk` |
| 2 | H2: the first scientist behind the screen talks (02) and attacks. The capsules (EREBUS, ORZABAL) fall. The second scientist runs to the door of H3 with `h2_runner_1`, cowers there with `h2_runner_2`. The recorder L35 on the bench | `CivilianBrain.SetEscape`, `EnemySpawn.escapeTo` |
| 3 | 6 s after the first death the guard post opens the door (`h3_enter`), two guards with shields. L9 on the desk | `FireDoor.openFlag`, `DemoHold.Wire` |
| 4 | H4: the booth. A full circle of mk_erebus on the drapes or the nurse starts the fire alarm: the glass door slides open. Or two blasts break the glass (a crack after the first). The console plays L36 (dark cards, the writer's lines); its end unlocks the feed and gives the tape. The second wind pill. The nurse hides (`h4_booth_*`) | `Interaction/GlassDoor`, `Interaction/FireAlarm` |
| 5 | H5: the steel gurney. Interact: "Ride the gurney with mk_orzabal?" It costs 40 MK (a full bar crosses twice). The walk keys steer it. Over the gap it does not land. A fall into the gap puts her back in the airlock. A spare ORZABAL capsule in the airlock (so a spent bar cannot lock the way) | `Interaction/GurneyRide`, `PitReturn` |
| 5b | H6: EXTEND brings the tube floor out. The card door: "It needs the airlock card". The scan C11 of the reader | `FlagSwitch`, `LockedDoor`, `Scannable` |
| 5c | The scientist denies the card before C11 (`h2_deny_card`), gives it after (`h2_give_card`). The guards and the nurse deny it (`h3_deny_card`, `h4_ask_card`). In H5 with the card: KNIGHT. Feed locked: the urge, then 02b on the nearest body, full HP, the feed unlocks. Feed unlocked: the other line | `CarrierTalk.giveAfterFlag`, `Interaction/AskTalk`, `StepKind.Feast`, `StopUnlessFlag`, `WaitRoom` |
| 4, 2.2 | After L36, her first "Feed on the body?" plays 02b and heals her to full | `Story/FirstFeed` |
| 5d | H7: the three armed scientists of W2 and a guard with a shield, three crates | |
| 5e | H8: the save machine, three capsules. The hidden door to H9 has a push bar on the H8 side only; when she is in H9 it shuts and jams. H9: L1 in the printer. H9 opens into W2 | `FireDoor.jamFlag`, `DemoWard` |

W2 is a quiet corridor now (the three moved to H7, the chart beats of W2 and W5 are gone). L19 no longer unlocks the
feed. The bot and the tests free her from the frame with `UprightRestraint.FreeAll` (as the last press).

## 3. The Dining Hall (rounds 121 and 122): BUILT

1. Six soldiers in two sets: Ortiz and Brandt with shields, two grenadiers (`soldier_grenades`, frag), two
   shotgunners (`soldier_shotgun`).
2. The shotgun: a new weapon. Its telegraph is a wedge (two red edges from the muzzle, 18 degrees each side, out to
   its 6 m range), no laser line. Then 7 pellets fan out in the wedge, 5 damage each.
3. At three standing (dead or cowering do not stand), a living soldier says `armour_call`, the power armour steps out
   in front of the lodge door, says `armour_enter` and hunts her (`Story/HeavyArrival`, flag `demo_g18_armour`).
4. Its weapon is the autofire archetype (`power_armour_guns`): the laser sweeps 1.6 m each side of her through the aim
   and the circle, then 12 rounds walk across her, 0.08 s apart, 5 damage each. No grenades, no rockets, no brand.
   The vendor rifle stands in for the gun pod.
5. The death screen `power_armour_guns` comes from the writer's row (the CSV already had it).
6. The doorways of G18 stay 2.3 m. A 3.2 m doorway let the camera of G19 see into G18 over the line of a door (the sight test failed). The 2.9 m armour clips the lintel if it follows her out: a look item.

## 4. G11, the union sheet L37: BUILT

On a corkboard on the east wall of G11, by the union letter L14 on the fridge (L14 was not in the game: it is now,
with the plan's words). The text of L37 is the plan's row, in the order of the board: the notice over the sheet,
then the sheet, then the hand lines.

## 5. The music: the state switch BUILT (placeholder audio; the ledger now calls the music round 126)

1. The music director already had the three states: Ambient (exploration), Tension (search: an alerted soldier
   hunts her, no gun aims), Combat (a gun aims, or a hit in the last 8 s). The set piece of G18 keeps its own track
   (`RoomScore`).
2. New: when the state changes between two clips of one piece (the same length in samples), the new clip starts at
   the sample of the old one. The crossfade keeps the beat (`MusicDirector.SamePiece`, `Locks`).
3. The placeholders are the T41 study loops (`complex/state_*_loop.wav`, 34.3 s each, the same length):
   `tools/music_states_placeholder.sh`. They are studies, not the final track.

## 6. The scope of the search music (Bruno, the same day): FIXED

Bruno: "have the game agent fix the search scope to the room", then "how about it being on the room and the
attached room? like if lucy is a room right next to where they're searching the song changes and the player has
time to strategize".

1. The fact check (`MusicScopeTests`, before the fix): one alerted soldier of G2 hunts, his gun off. Lucy in W6, on
   the other floor: the music was Tension. `MusicDirector.Decide` counted every alerted soldier of the world scene,
   and `Alerted` never goes back to false.
2. The fix: `MusicDirector.InSearchScope`. A hunter counts in her room and in every room that shares a doorway with
   it (`Room.neighbours`, from the exits; a locked or jammed door still joins two rooms). Two doors away does not
   count. A scene with no rooms (the test rooms) counts every hunter. Combat does not change (an aim, a hit).
3. The test picks the rooms from where the hunter stands: the other floor and two doors away give Ambient, the room
   next door gives Tension.

## 7. A finding for Bruno: the hall's six hear the concourse

The DemoTests cover test of G2 failed after the Dining Hall change: in 40 s no man of G2 took cover (3 runs). With
the hall's squad switched off, one ducks after 3.7 s. The six of G18 hear the G2 fight through the glass doors
(a shot or an alert within 25 m), and they join it from their post. The test now leaves the hall's squad out. OPEN:
should the hall's squad hear a fight in G2?
