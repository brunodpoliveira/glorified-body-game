# 2026-11-10: manual tests for Bruno

The work of 2026-11-10, to play by hand. Start the demo with START. Each row has what you do, what you must see, and
what you see if it is broken. Write the row number and a screenshot for each fault.

## The ward

| # | Do | Expected | Broken |
|---|---|---|---|
| 1 | Start the demo. | The card RAVEN, then the wake. | The wake first, or no card. |
| 2 | Press Interact during the card. | The skip menu. SKIP skips the card and the wake together. | The wake still plays after SKIP. |
| 3 | In W5, let the code silver beat play. | The fire door from W5 to W9 slides shut, with its yellow bar. | The door stays open, or a grey wall shows. |
| 4 | In W5, look for a lever at that door. | No lever on the W5 side. | A lever on the W5 side. |
| 5 | Go through the gym W7 and W8 to W9. Find the lever by that door. | One red lever on the W9 side. Interact opens the door. | No lever, or the door stays shut. |
| 6 | Walk from W9 back to W5. | The way is open: the ward is a ring. | Blocked. |
| 7 | In W9, look at the counter monitor. | L24, the thread of three emails. | No log. |
| 8 | In W9, look out of the window in the east wall. | The yard 4 m down, a fence, two grey vans with the gear, lamps, the night. | Black, or the old blue window on the north wall. |
| 9 | Try to walk out of that window. | The pane stops her. | She falls out. |
| 10 | In W14, walk in. | The TV plays the end of Things to Come as subtitles, then the credits and the copyright line. | Nothing plays. |
| 11 | Interact on the TV table in W14. | Her text: "Aw, man. I tuned in right at the end!..." | A placeholder line. |

## The public floor

| # | Do | Expected | Broken |
|---|---|---|---|
| 12 | In G2, select mk_orzabal. Hold the defend key at a planter, move the mouse. | The planter rises and follows the mouse. A release drops it. | No lift, or "TOO HEAVY". |
| 13 | Drop a planter in the G3 doorway. Lure a soldier to the other side. | He kicks it twice. It slides out of the doorway, and he comes through. | He walks through it, or he stops for good. |
| 14 | Save at a machine, move a planter, load. | The planter is where it was at the save. | It goes back to its first place. |
| 15 | In G10, walk in. | The radio speaks mid-sentence: the police chief, then the sports news, then low music. | Silence, or it starts each time she enters. |
| 16 | In G10, look at table B. | The desktop tree, its lights blink in two groups. HAPPY BIRTHDAY GENE!!! over it. The fridge with the leftovers. | No tree, or the lights do not blink. |
| 17 | Scan the tree. | Cutscene 07c: text cards, the carol and the toast as subtitles, the cork and a cheer. | A still picture, or nothing. |
| 18 | Read L25 (corkboard) and L26 (by the tree). | The winter schedule, and Gene's birthday email. | Missing. |
| 19 | Read L27 (G13 monitor), L28 (G11 lockers), L29 (G9 north wall). | The reply-all thread, the law flyer, the holiday roster. | Missing. |
| 20 | In G2, walk through the glass doors in the east wall. | G18, the Dining Hall: four soldiers, tables on their sides, the bar, the lodge door. | No doorway. |
| 21 | Let the four see her. | A fast track starts (drum and bass, placeholder). | The normal combat music. |
| 22 | Run back to G2. | They stay in the hall and do not follow her. The track stops. | They chase her into G2. |
| 23 | Read L31 and L32 at the host stand. | The book of tonight, a guest comment card. | Missing. |
| 24 | Go to G19, the kitchen. Find the night manager by walk-in 1. | He whispers, then begs her to take the key (placeholder words). | No figure. |
| 25 | Take the DUTY KEY from the board in the kitchen office. Read L33 and L34. | The key in the inventory, the two logs. | Missing. |
| 26 | After the key, go back to the hall. | The surviving soldiers now follow her. | They still hold the hall. |
| 27 | In G19, use the lever of the service exit. | The door opens into G9b, the closet of G9. | No lever, or a lever on the G9b side. |
| 28 | From G9b, try to open that door before the kitchen. | No lever on that side. "NO ENTRY FROM THIS SIDE." | A lever. |
| 29 | In G9, after the dark room, turn the release with no key. | "DUTY KEY REQUIRED." | The release works. |
| 30 | Turn it with the key. | The release works. L30 is on the console wall. | Nothing. |

## The halon room

| # | Do | Expected | Broken |
|---|---|---|---|
| 31 | From G19 go south into G17b, FACILITIES. Read L15. | The memo of the halon. The lockout cabinet by the door, the DANGER words over it. | No room. |
| 32 | Go into G17 without the lockout. Fire mk_erebus at the box wall with a part circle. | A flash only. Nothing burns. | The boxes burn. |
| 33 | Fire a full circle at the box wall. | Fire on the boxes. The horn. The door seals. The panel counts from 30. | No horn, or the door stays open. |
| 34 | Press ABORT on the panel. | The horn stops, the door opens, the boxes burn for 8 s, then the cage. | Nothing. |
| 35 | Take the cage items. | One MK capsule for each power, one second wind pill, L18. The empty violet tray. | Out of reach, or two of each. |
| 36 | Load, and do 33 again. Do not press ABORT. | At 0 the fire goes out. 3 s later she passes out: the halon death screen. | She lives, or another death screen. |
| 37 | Load. Turn the lockout in G17b first, then fire a full circle. | The boxes burn with no horn. The door stays open. | The horn sounds. |
| 38 | Read L16 in G12, at Colton's place. | The note from counsel. | Missing. |

## The coda

| # | Do | Expected | Broken |
|---|---|---|---|
| 39 | Step into R1. | The TV behind the till plays the bulletin. The poster beat waits for its end. | The poster beat starts first. |
| 40 | Interact on the TV while it plays. | The picture of the line, as text. | Nothing. |
| 41 | Wait for the end. | The last line follows the body count, then "Police are asking citizens to be on the lookout for -", then music. Then the poster beat. | The line never ends, or the beat never comes. |
