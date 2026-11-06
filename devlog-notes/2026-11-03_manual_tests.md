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
| 11k | In G2, stand in the open and let the soldiers see you. | Most run to the planters and counters, crouch between shots, and stand to fire. | They stand in the open beside cover. |
| 11l | Fire at one while he crouches. Then fire as he stands. | While he crouches, the planter takes it and breaks after some hits. As he stands, he takes it. | The power goes through the cover. |
| 11m | Walk past a soldier's cover to his side. | He leaves that spot and takes another. | He stays crouched with you beside him. |
| 11n | Break a desk that holds a log (W10, Desk_A). | The log lies on the floor, and you can take it. Soldiers can walk there. | The log floats. |
| 11o | In G2, stand still at the west end and watch the four soldiers for 30 s. | Half move while half fire, then they swap. "Moving up." then "Covering. Go." at the bottom. Nobody comes to arm's reach. | All run at you at once. |
| 11p | Kill all but two, then all but one. | "Two left. Fall back to cover.": they go back. The last one calls Control, and nobody answers. | They charge. |
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

## F. MK as ammunition, the short (2026-11-06, any test room)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 34 | Press 1, 2, 3 and look at the HUD. | One MK row. It takes the name and the colour of the selected power. | Three rows, or the row does not change. |
| 35 | Fire mk_warspite, then press 2. | The mk_erebus bar is still full. The SHORT bar rose a little. | One bar for all, or SHORT stays. |
| 36 | Wait 30 s after a power. | No bar comes back by itself. | A bar rises. |
| 37 | In the training room, take hits until your HP is low, press F5, then attack. | Under half your HP the short kills you. The death screen shows the PA line and the short's hint. | She stays at 1 HP, or the screen names an enemy. |
| 38 | Do it again with the second wind ready. | You stay at 1 HP, and the short ends at once. | You die, or the shocks go on. |

## G. Auto-fire and cancel

| # | Do | Expected | If faulty |
|---|---|---|---|
| 39 | Hold the attack key on an enemy. | The power fires by itself when the circle fills. Your feet go free. | It waits for the release. |
| 40 | Keep the key down after that. | No new circle until you press again. | A new charge starts. |
| 41 | Hold the attack key, then press the defend key. | "CANCELLED". No MK spent, nothing fires. | It fires, or MK falls. |

## H. The inventory

| # | Do | Expected | If faulty |
|---|---|---|---|
| 42 | Open it with I after you take a capsule, a key and a log. | Three lists: CONSUMABLES, KEYS, LOGS, in that order. | One row. |
| 43 | Press W and S. | The selection moves in the list and wraps. The inventory stays open. | S closes it. |
| 44 | Press A and D. | The selection changes list and skips an empty one. | Nothing moves. |
| 45 | Press I or Esc. | It closes. | It stays. |

## I. The mouse carry (TEST ROOMS > PHYSICS TEST)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 46 | Press 3, face a box, hold the defend key. | It rises. She faces it and stands still. | Nothing, or a floor slab rises. |
| 47 | Move the mouse. | The box follows: left and right, up is away. A wall stops it. | It moves the wrong way, or goes through a wall. |
| 48 | Roll the wheel up. | The box rises. | Nothing. Tell me if you want the mouse for the height. |
| 49 | Take a box over the rail and release it on the yellow plate. | It falls straight down. The red gate in the east wall opens. | It flies on, or the gate stays. |
| 50 | Lift it off the plate again. | The gate shuts. | It stays open. |
| 51 | Drop one box on the other. | It stays on top. | It falls through or jumps. |

## J. mk_edgar (TEST ROOMS > EDGAR TEST)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 52 | Press 4, lock the soldier or the guard (Tab), hold the attack key. | "NOT WEAK ENOUGH", no MK spent. | The circle goes on. |
| 53 | Hurt him with another power, then charge mk_edgar within 6 m. | He goes down on both knees and holds still. At the full circle he stands with a violet light over his head. 75 MK gone. | He keeps fighting. |
| 54 | Release early, or press the defend key. | He gets up. No MK spent. | MK falls. |
| 55 | Tab to the lab worker at full HP. | It works with no damage. | "NOT WEAK ENOUGH". |
| 56 | Tab to the armour marked as boss, hurt it. | "IT RESISTS". | It kneels. |
| 57 | With a dominated guard, walk away. | He follows. With an enemy near, he fights it, and it fires at him. | He stands, or he fires at you. |
| 58 | Stand at the rail, take the guard behind it, press the defend key. | He runs to the switch. The red gate opens. | "NOTHING TO SEND IT TO". |
| 59 | Open the demo. | Key 4 does nothing, and no EDGAR text shows. | mk_edgar is in the demo. |

## K. The team (the demo, G2)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 60 | Fight in G2, then run out of their sight. | Within 2 s, lasers and shots at the place where you were, not at you. | They walk straight to you. |
| 61 | Stay away for 8 s. | "Lost her." They walk in pairs, one at a time. | All run at once. |
| 62 | Step into a soldier's sight. | "Contact!" The fight starts again. | They walk past you. |
| 63 | Kill all four of G2, then take the release in G9, and wait 20 s. | Four new soldiers come for you. | Nobody comes. |

## L. The tutorial, the gap, the coda (2026-11-06)

| # | Do | Expected | If faulty |
|---|---|---|---|
| 64 | Main menu > TUTORIAL. Walk through the steps. | Grey boxes only. 34 steps. No step blocks you, even if you break the box or kill a figure. | A texture shows, or a step cannot end. |
| 65 | In the demo, G2: the door north at x 18, the riser room. Walk into the shaft. | You stand again on your side, unhurt. | You stay in the pit. |
| 66 | Press 3, lift a crate, drop it in the shaft, walk over. | It lies level with the floor. Two capsules wait behind it. | You trip or fall. |
| 67 | In a short with the second wind ready and low HP. | At 1 HP the short goes on to its end. HP stays at 1. | It ends, or you die. |
| 68 | Play the demo to the end of U1. | A cut to the car park, no HUD. The car door gives no prompt. | The title comes, or the door opens. |
| 69 | Take the sunglasses from the glove box, then the tin. | The HUD comes back. The tin says UNKNOWN CAPSULE, and it does not work. | The tin works. |
| 70 | Use the car door. | The tail lights text, the bulletin, then the shop. | Nothing. |
| 71 | Walk to the counter in the shop. | The attendant's lines, the flashback, "Get up.", "...Yes." Key 4 works now. He follows you. | Key 4 stays dead. |
| 72 | In the service bay, take the mechanic with a full circle. Press the defend key. | The attendant wakes and runs. The mechanic uses the switch, and the shutter opens. | The shutter stays. |
| 73 | Kill the mechanic instead. | After 15 s a spare comes out, and the shutter stays shut. | The shutter opens, or nobody comes. |
| 74 | In the toll yard, try the guard unhurt, then hurt him, take him, send him. | NOT WEAK ENOUGH, then the barrier rises, then the title. | |
| 75 | A gas grenade in AI TEST. | The smoke is white. The ring on the floor is still green. | Green smoke. |
