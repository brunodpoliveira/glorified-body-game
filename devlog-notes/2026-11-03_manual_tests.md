# 2026-11-03: the manual test list (one world, fire doors)

Play from `Main_Menu` in the editor. About 30 minutes. For each fault, note the step number and take a screenshot.
If the console shows a red line, copy it too.

## A. DOOR TEST (main menu, TEST ROOMS column, the last button)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 1 | Look at the menu. | All 13 test room buttons show, DOOR TEST at the bottom. | DOOR TEST is cut off. |
| 2 | Open DOOR TEST. Walk to the north doorway. | Three fire doors stand open. A red lever stands beside each doorway, handle up. You walk through. | A leaf blocks a doorway. |
| 3 | Face the lever left of the north doorway (hall side). Press E. | The leaf slides shut. A yellow bar lies across it. The handle points down. | No hint, or the door does not move. |
| 4 | Press E again. | The door opens. The bar goes. | It stays shut. |
| 5 | Stand in the doorway. Face a lever and press E. | "Something is in the way." The door stays open. | The leaf shuts through you. |
| 6 | Walk into the barracks until the soldiers see you. Run back to the hall and jam the north door. | They stop at the door and kick it: a thud and a shake each time. About 5 s later the leaf breaks into pieces and they come through. | They walk through the shut leaf, or they stand still. |
| 7 | Jam the east door. Fire a power near the armour bay to call the power armour. | It breaks the door in two blows. | It cannot fit through the 2.9 m doorway after the break. |
| 8 | Jam the west door. Call the guard and the scientist. | They bang on it (softer sounds). The door holds. They never pull their lever. | The door opens or breaks. |
| 9 | Press E at your lever of the west door while they bang. | The door opens, and they come in. | Nothing happens. |
| 10 | Go to the south door (the card door). Press E. | "It is locked. It needs the red card." The closet soldier stays behind it. | You or the soldier walk through. |
| 11 | Lock on to a soldier through an open fire door. Jam the door. | The lock marker goes after about 1 s. | The red marker stays over him. |

## B. The demo (START)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 11b | Press START. Walk around W1. | The game opens on the cutscene with no game frame first. W1 is quiet: no enemy, no talk, however long you stay. | The game shows first, or something happens in W1. |
| 11c | Walk through the doorway into W1B. | A step past the doorway she stops. The scientist walks out from behind the screen, the talk plays, and he attacks after the last line. The door to W2 stays shut, and its lever does nothing before the talk. | The talk starts in W1, or the three in W2 join the fight. |
| 11d | Kill him. Take the two green capsules on the floor. Pull the lever by the door and walk out. | The capsules go to your items. The door opens. W2 holds one pistol and two knives. | No capsules, or the door stays shut. |
| 11e | In AI TEST, lift a soldier beside a wall (key 3). Turn and walk so that he swings into the wall. | He stops at the wall and slides along it. Turn back: he comes back. | He goes through the wall. |
| 11f | In W2, kill the three. Do it again from a fresh start, but run past them into W5. | About 1.5 s after the last one falls, or as you step into W5, the chart cutscene plays. After it, E on a body asks "Feed on the body?", and the chart is in your items. | No cutscene, or the feed stays locked. |
| 11g | In each room, walk up to each doorway. | The camera there shows the doorway. | A door out of frame, or seen edge on. |
| 11h | In G2, meet the second pair. | One soldier carries a shield and a pistol. He circles you, and your powers stop on his front. | He comes straight at you, or he has the guard's clothes. |
| 11i | At the end of the demo, on the bulletin card, press nothing. | "E  continue" shows. About 12 s later the title shows. | The card stays. |
| 11j | In W5, scare a guard: kill the other guard with two full charges in his view, or feed in his view. Walk up to him where he hides. | He runs, crouches with his back to you, and his gun lies on the floor. Within 4 m he says a line, for example "I'm done. I'm not gonna shoot." | He keeps the gun, or says nothing. |
| 12 | Walk from W1 into W1B, then through the other doorways. | No fade and no load screen at a doorway. The camera changes at the wall. | A black fade or a pause at a door. |
| 14 | During a cutscene, press E. | A menu: PAUSED, RESUME, SKIP. The picture holds. Up and down choose, E confirms, Escape resumes. | The cutscene ends at once, or keeps playing under the menu. |
| 15 | At the W2 to W5 doorway, and at W5 to W7, use a lever. | A fire door with a lever on each side, as in DOOR TEST. | No door, or a leaf sticks out of a wall. |
| 16 | In W7, look at the wall of TVs. | The TV bodies stand behind the screens. | Floating screens with no bodies. |
| 17 | Go to the W12 stair door without the card. | The leaf blocks the doorway. You cannot pass. Negative check: before today you could walk through. | You walk through with no card. |
| 18 | Get the card (the ward sister in W11), open the stair door, walk down. | You reach G1 on floor 1 with no load. | You fall, or you stop on the ramp. |
| 19 | In G2, use a lever at the G5 doorway. | A fire door, as in step 15. | No door. |
| 20 | Press M. | The corner map shows your room. It follows you down the stair. | No map, or the wrong floor. |

## C. The look (no dither)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 21 | Open Options > Display > Look. | Two choices: "Sharp (no anti-aliasing)" and "Clear (anti-aliasing)". | "Authentic (1999)" shows. |
| 22 | Pick Sharp. Look at a dark wall or a planter in a look test room (F11 in the training room). | A smooth gradient, no dot pattern. | A grid of dots on the walls. |

## D. The graphics options (Options > Display/Graphics)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 23 | Open the tab. | Sections DISPLAY, GRAPHICS, GAME. Quality Preset shows High. | Rows overlap, or "Look" or "Rain Effects" shows. |
| 24 | Set the preset to Low, then Ultra. | The six rows under it change with it. The picture changes (softer at Low). | The rows stay, or nothing changes. |
| 25 | Change one row, for example Anti-Aliasing. | The preset shows Custom. | It stays at the old preset. |
| 26 | Set Shadows to Off, walk into a lit room. Then set High. | No shadows, then the shadows come back. | Shadows stay off after High. |
| 27 | Move Brightness to +1.0 and to -1.0. | The picture gets brighter, then darker. At 0 it is as before. | No change. |
| 28 | Quit the game and start it again. | Your choices stay. | The defaults come back. |
| 29 | Open the Controls tab. | Mouse Turn, Mouse Sensitivity and the key rows. No Wallrun, Slide or Catch/Throw row. | The old rows show. |
| 29b | In a room, move the mouse left and right. Then set Mouse Turn off and move it again. | She turns with the mouse, faster at a higher sensitivity. With Mouse Turn off, the mouse does nothing. | No turn, or a turn with the option off. |

## E. Flanking (TEST ROOMS > AI TEST, or the demo)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 30 | In AI TEST, stand still and let the soldiers see you. | The rifle soldier, the gas soldier and the power armour circle you: one to each side, one behind you. The shield guard comes straight at you, and so does the pistol scientist in LAB TEST. | They all come at you from the front. |
| 31 | Watch them move. | They walk around you at a distance, stop, aim, fire. Never at arm's reach on the way. | A soldier walks right past you and strikes. |
| 32 | Turn to face one flanker. | The others move again to spots out of your view. | They stay in front of you. |
| 33 | In LAB TEST, let the knife scientist see you. | It runs to your side, then strikes. | It runs straight at your face. |
