# 2026-11-15: two voices at most, the logs L38 and L39, more hints for one killer

Bruno:

```text
take a look at the bark code: two people can bark at a time, no more, or else it'll become a cacophony of voices. we can have two people barking to indicate hecticness, confusion etc. check if there are limits. if not impl them
```

```text
two people of two different archetypes, to maximize information (i.e a guiard and a solider, a scientist and a nurse etc). never two nurses, two soldiers etc at the same time
```

```text
the writer agent likely sent you multiple ideas i had during a brainstorming sessioh. take a look at them and implemente
```

## The fact check: CONFIRMED, no limit

`Bark.Say` played every line at once. Only the subtitle box kept 3 lines, so a fourth voice still played, with no
subtitle.

## The two-voice rule (`Enemy/Bark.cs`)

1. A bark plays only when fewer than two others speak (`Bark.MaxVoices`), and none of them is of its archetype.
   The archetype is `BarkVoice.Enemy`: guard, soldier, armour, nurse, labworker (the scientists too), patient. A thing
   with no voice is an archetype of its own.
2. A speaker who talks already may go on.
3. A line that drops is not said. `BarkBook.Say` asks first (`Bark.CanSpeak`), so the deck keeps its line.
4. A forced line (`Bark.SayForced`) always plays. When both places are taken, it cuts a voice of its archetype first,
   then the oldest: the clip stops and the subtitle goes. These lines are forced: an answer to the Interact key
   (`AskTalk`, `CarrierTalk`, `Fixture`, `PatientBrain`), a story beat (`HeavyArrival`, the runner of H2), a hider's
   whisper and its found line, and the answer of a figure she holds with mk_edgar (`BarkBook.SayAs`).
5. A line two rooms away is not heard (`Voice.Heard`) and takes no place, as before.

## The writer's rounds 149 to 163

Most of these rounds are lore for acts 2 and 3. The items for the game:

| Round | Item | State |
|---|---|---|
| 154 | L38 in G13, L39 in W13 | built (this note) |
| 162, 163 | The human shield hint, and more than one hint for one killer | built (this note) |
| 161, 162 | The soldier barks `civilians present` and `escorts a civilian`, the `CIV:` tag | next |
| 162 | The human shield | next |
| 162 | The face plant of a full slam | next |
| 160 | The escort | next |

### L38 and L39

1. L38, the v1 end of support from Alterius, lies in a tray on the scheduling desk of G13 (`DemoNight.G13Notice`).
2. L39, the notice on occupancy sensing, is taped to the south wall of W13 over the radio (`DemoNight.W13Notice`).
3. The words come from `DEMO_PLAN.md` §8. A "(by hand)" of the plan reads "By hand:" now, so a hand note does not
   read as print.

An older fault, found on the way: the builders of L25 to L29 and L37 added their old hand notes after the plan's
cell, and the cell holds them already. Those logs said their notes twice. The cell is the whole log now, and the old
text is only the fallback.

### More hints for one killer

1. `DeathScreenContent.Entry.moreHints`: the screen picks one of the row's hint and these at random, never the same
   twice in a row (`DeathMenuUI.PickHint`).
2. A second row of the same killer in `game/death_screens.csv` adds its hint. This is the writer's way to add more.
3. The human shield hint of round 163 ("They will not shoot through a living person. Lift one, and keep it low.") is
   one of the hints of every soldier that shoots: `soldier`, `soldier_rifle`, `soldier_shotgun`. The guard does not get
   it, and the grenadier and the gas do not either.

## Proof

| Check | Result |
|---|---|
| `BarkVoicesTests` (new, 2) | pass: two soldiers never speak together, a third voice drops, a forced line cuts the oldest |
| `DiningHallTests`, `HoldTests`, `FloorplanV2Tests`, `CivilianTests`, `BatonTests`, `WardTests`, `ArmedCivilianTests` | 69 of 70, then 70 of 70 |
| `DeathScreenContentTests.TheHumanShieldHint_IsOneOfTheSoldiersHints_NotTheGuards` (new) | pass |
| `DemoPlanTests.AHandNote_ReadsAsOne_AndNoLogRepeatsAParagraph` (new) | pass |
| `DemoTests.TheLogsL38AndL39_LieInTheirRooms` (new), `DeathScreenTests` | 4 of 4 |

The one failure: `WardTests.AFeed_FrightensTheOnesWhoSeeIt` saw 1 line, not 2. The earlier lines of the ward still held
both voices at the feed, so the rule dropped the witnesses' lines. The test clears the voices before the feed now, and
the nurse and the guard both speak (3 lines).
