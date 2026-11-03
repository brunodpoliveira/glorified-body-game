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
| 12 | Walk from W1 into W2, then through the other doorways. | No fade and no load screen at a doorway. The camera changes at the wall. | A black fade or a pause at a door. |
| 13 | Watch the scientist in W2. | He comes into the doorway and talks first (the placeholder card shows the lines). He attacks only after the talk. | He shoots during the talk. |
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
