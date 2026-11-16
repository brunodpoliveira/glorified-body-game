# 2026-11-16: five corrections (the death screen, scans, hints, stuns, the security card)

Bruno:

```text
some corrections that are easier to do now than later:
1 - the game over screen is a full screen; like the batman arkham games, it is a full screen of the enemy doing the speech, animation and the hint; once completed it automatically reloads to last checkpoint
2 - no dialogue, tapes etc during scanning cutscenes
3 - let the cue on screen for scan/interact to be removable, on for now; play-testing will decide if it goes for good or not
4 - when the enemy gets staggered/stunned their dialogue line gets cut out immediately
5 - the airlock card is the security card; once the code silver gets announced, the code is rotated; it is not mentioned anywhere, neither ion thepa or whatever; the card stops working to unlock stuff. and now they need a special key to get into the office to reprogram the security card
```

His answers to my questions:

1. The ward sister has the key.
2. The enrolled card opens W3 and W10.
3. The PA and the death screen:

```text
the PA does not say anything; the player tries to use a security card to open a door and it no longer works; lucy mentions that they must have rotated the security code
Skip and a quit menu
```

## 1. The death screen

`UI/DeathMenuUI` and the new `UI/DeathStage`:

1. The screen is full screen. The killer stands alone in the dark, far under the world. He is the model of his class
   from the character catalog (the killer id starts with the class: `soldier_rifle`). A camera moves in on his face. It
   draws into a texture that fills the screen. The light: see "After Bruno's manual test" below.
2. He plays the Idle of his animator on real time, because the world is stopped. His head nods a little while he speaks.
   His line shows as a subtitle, and its clip plays when it exists. The hint shows under him.
3. At the end the game reloads the latest checkpoint by itself (no checkpoint: the room starts again). The time: his line
   (the clip, or the read time of the words), then 2.5 s, 5 s at least.
4. The Interact key skips to the reload (after 0.8 s, so a key held in the fight does not skip it). Escape opens the menu:
   Restart from latest checkpoint, Main Menu, Back. The time stops while the menu is open.
5. A hazard has no model (the halon, the short, the brain, the gurney softlock): the writer's picture note shows on a
   card, as before.
6. In a development build a line at the top left names the killer and the note of the take to come.

PLACEHOLDER: the models are the catalog's, with no weapon, and the motion is the Idle with a nod. The real takes (the
speech, a gesture) replace them later.

After Bruno's manual test:

```text
on death screen: e does not skip the scene
```

```text
the lighting in the game over screens in arkham is a bit hard to describe... i think it's a spotlight on the ground pointing up?
```

```text
it is super dark, no light visible at all
```

1. The E key: `GameManager.AllowsAction` stopped every action in the game over. Now the Interact key passes there. The
   other readers of Interact check for the Playing state first, so it does nothing else.
2. The Arkham light (his five reference shots): the key light is a footlight on the floor 1.1 m in front of him. It has
   hard shadows. A weak front fill and two cool rims from behind complete it. A grey glow on an unlit quad sits behind him.
   The grade gives less colour, more contrast, a vignette and a small bloom. The camera is low, at his chest, and looks
   up at his face.
3. Why it was dark: URP drops every rendering layer that has no name in the project settings
   (`RenderingLayerUtils.ToValidRenderingLayers`). Only "Default" has a name. The stage used layer 6, so its lights
   reached nothing. Now the stage stays on Default. The room's directional lights are off while the screen shows. The
   figure has a near-black ambient probe of its own. The glow quad also faced away from the camera.
4. A fault in the same family, NOT FIXED: `Darkness.DarkLayer` (2) has no name either. So the monitor lights of a dark
   room reach no lit renderer in the dark. Only the greybox tint shows.

## 2. No lines under a scan

Under a scan (a picture or a clip, `GameState.Reading`) the subtitle box shows nothing. In a cutscene (the scan C10 too)
it shows only the cutscene's own lines (`SubtitleUI`). The world sounds already stopped there (the tape too). Before,
the lines on screen at the start of the scan stayed: the subtitles run on game time, and the scan stops it.

## 3. The hint line can go

Options > Display > Interaction Hints (`SettingsManager.InteractHintsOn`), on by default. Off: no "E  open the door",
no "F  scan" over the HUD. The keys still work.

## 4. A stun cuts the line

`Bark.Silence` stops the figure's line at once: its clip and its subtitle (`Voice.Stop`). It runs on a stagger or a stun
from outside (`DummyShooter.Stagger`: a power, the short, a blast, a thrown body). It also runs on the stun of a civilian
or a patient, and on a knockdown (`EnemyRagdoll.KnockDown`). The gun's own aim drops (a kneel, a door, a retreat, a shock) go
through `DummyShooter.DropAim` now: they cut no line.

## 5. The security card

1. The airlock card is THE SECURITY CARD (`security_card`). It opens the airlock door in the Hold, and on the ward W3,
   W10 and the stairwell W12.
2. Code silver (beat 9 in W5) rotates the codes (`CodeRotation.rotateOnFlag`). No PA call. A closed card door refuses the
   card from then on: the reader shows CODE ROTATED. SEE SECURITY. An open door stays open.
3. At the first refusal Lucy says: "They must have rotated the code." (PLACEHOLDER: the writer's words replace it.)
   One time in the game (`LockedDoor.rotatedLine`, flag `demo_codes_noticed`).
4. The ward security post W13 is locked from W12 by THE SECURITY POST KEY (`office_key`). The ward sister of W11 carries
   it: she gives it when asked, or drops it at her death. The scan C3 is on that door now and shows the key on her
   (PLACEHOLDER caption).
5. The terminal of W13 enrols the card with the code of W14, 8675309. Then W3, W10 and W12 open.
6. A card enrolled before code silver works no more after it (the rotation drops the enrolment).

Gone: THE WARD SISTER'S CARD, the PA call 1C at the rotation, the rotation at the first try at the stairs.

## Tests

`DemoTests.TheCodeRotation_AtCodeSilver_TheCardFails_SheSaysIt_TheKeyOpensW13_AndTheTerminalEnrols` and
`ACardEnrolledBeforeCodeSilver_FailsAfterIt` replace the two tests of the old rotation. The ward sister test asks for the
key. `HoldTests` name the security card.

RESULTS: NOT RUN. Bruno stopped the run of the 30 classes and 4 bots for his manual test, then said "commit everything".
Bruno tested the death screen by hand. To do before a push: `DemoWorld.BuildAll` (the writer's round 172 and the C3
caption), `MapAtlasSetup.BuildAtlas`, `DeathScreenSetup.Build`, then the changed test classes and the bots.

## For the writer

The writer did round 172: `DEMO_PLAN.md`, `RULINGS.md`, `LORE.md`, call 1C cut, cutscenes 02 and 06, `barks.csv`, and
the hint in `death_screens.csv` ("The key of the post is on her."). OPEN for Bruno:

1. Lucy's line at the first refused card door. The writer's takes: (1) "They rotated the code on me. Security post,
   then." (the writer's pick), (2) "They must've rotated it, so... security, then.", (3) "They rotated the code. Okay.
   Security." The game says "They must have rotated the code." (PLACEHOLDER) until he picks.
2. Is W3 open before code silver?
3. Does the give line of the ward sister fit a key?
4. A sign on the W13 door?

His answers:

```text
1: we'll apply the actual lines and interactbles once we start building out the actual level. this greybox is to test gameplay features and the map
2: yes
3: the ward sister has the key that unlocks the office that allows the reprogramming of the security card
4: yes
```

1. The placeholder line stays until the real level is built.
2. W3 opens with the card before code silver. The build already does this.
3. The build already does this: the key opens W13, and the terminal there enrols the card.
4. The sign "WARD SECURITY POST" is over the W13 door, on the W12 side (`Sign_Security_Post`). It shows after the next
   `DemoWorld.BuildAll`.

## The rendering layer of the dark room

```text
fix the rendering layer
```

1. `Editor/RenderingLayerNames` names rendering layer 1 "Dark" at each editor load (GlorifiedBody > Checks > Name The
   Rendering Layers). URP then keeps `Darkness.DarkLayer`.
2. `Darkness.LightOnlyTheDark` puts a monitor light on the dark layer through its URP light data. The G9 builder and
   DARK TEST use it.
3. A scene built before today: when a room goes dark, `DarkRoom` calls `Darkness.RefreshGlow`, which repairs the monitor
   lights.

NOT RUN: `DarkRoomTests` and the DARK TEST bot.
