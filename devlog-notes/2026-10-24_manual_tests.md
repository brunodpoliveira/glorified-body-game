# Manual tests, everything since the FSM enemy check of 2026-10-23 (about 40 min)

Open the project in the Unity editor, open `Main_Menu`, press Play. Keys: Tab switch target, 1 mk_warspite, 2
mk_erebus, 3 mk_orzabal, hold the left mouse button to charge, right mouse button to defend (catch), F7 refill HP
and MK, F9 MK +25, F6 start the short, Q drug. For a wrong result: a screenshot, the step number, and the red
lines of the Console.

## A. AI TEST: the weapons (10 min)

| # | Do | Expected | Wrong if |
|---|---|---|---|
| A1 | Stand in the open and watch each soldier attack | A laser, the HUD circle with an icon, then the attack. Soldier_A: 3-round bursts. Guard_B: pistol shots. Armour_C: frag grenades and rockets. Gasser_D: gas grenades | A shot comes with no circle, or a soldier moves while he aims |
| A2 | Stay in a red floor ring, then in a green one | The red ring bursts and hurts you. The green ring makes a gas cloud that hurts you while you stand in it | No ring, or no damage |
| A3 | Walk next to a soldier | He strikes in melee, after the circle | No circle before the strike |
| A4 | Turn the camera so that a soldier is out of the frame while he aims | An edge arrow with the attack icon | No arrow |
| A5 | mk_orzabal selected, right mouse button as a grenade or a rocket comes at you | You catch it, and it flies back to its soldier | It hits you, or the MK does not go down |
| A6 | mk_warspite on Guard_B from the front, then from his side | Front: "BLOCKED", no damage. Side: damage | The front lets the blast through |

## B. AI TEST: the bodies and the gore (10 min). Options > Gore: Full

| # | Do | Expected | Wrong if |
|---|---|---|---|
| B1 | mk_orzabal on a soldier, then a short press to throw him at another soldier | He hangs limp from the hips. Both fall, stay down about 2.5 s, then stand up | He hangs stiff, or the one he hits does not fall |
| B2 | Lift a soldier, then hold a full circle on him (the slam) | He slams to the floor and falls limp | No fall |
| B3 | A full mk_warspite on a soldier that does not kill him | He falls, stays down about 2.5 s, stands up | No fall, or a fall from a part charge |
| B4 | A part-charge mk_warspite | A stagger only | He falls |
| B5 | Kill a soldier with a full mk_warspite | A forearm or a whole arm flies off and stays on the floor. Dark textured blood, not red discs. A dark pool grows under the body | No piece, a red card spray, or no pool |
| B6 | Kill a soldier with a throw or a slam | His head bursts. No arm comes off | An arm comes off |
| B7 | Stand near a soldier and press F6 (the short) until he dies | His head bursts | No burst |
| B8 | Watch the others after a gruesome kill | Shock barks. Guard_B retreats and cowers after enough deaths. The soldiers never run | A soldier runs |
| B9 | Hide behind a pillar after a kill | A colleague drags the body and does CPR. The body never comes back to life | A revive |
| B10 | Options > Gore: Low, then kill with a full mk_warspite | No cut, no head burst, a smaller pool | A cut on Low |

## C. COVER TEST (10 min)

| # | Do | Expected | Wrong if |
|---|---|---|---|
| C1 | Wait about 10 s | The soldiers walk to the barricades and shoot over them | They stay in the open |
| C2 | Full mk_warspite on a soldier behind a barricade | "cover" on the HUD, the barricade takes it, he does not. It breaks after 3 full blasts (2 with the MK boost), then the blast hurts him | He takes damage through an intact barricade |
| C3 | Stand behind the middle barricade while they shoot | Their bullets wear it down. It breaks after about 6 hits | It never breaks |
| C4 | F9 to MK 50 percent or more, then a full blast on a soldier beside a table | The table breaks, its items fly, the pieces stay on the floor | The table stays intact, or pieces vanish |
| C5 | Lift a soldier and throw him at a soldier behind a barricade | He flies through the barricade and breaks it | He flies over it |
| C6 | F6 (the short) under a lamp | The lamps within 5 m spark, flicker and go out. That part of the room goes dark | No lamp goes out |
| C7 | Break a table, then Esc > Restart | The room comes back as you found it: a restart resets the visit | The table stays broken after a restart |

"Broken stays broken when she comes back" has no manual test yet: no door connects two rooms, and the main menu
starts a new session by design. The PlayMode test and the cover bot prove it.

## D. TUTORIAL (10 min)

| # | Do | Expected | Wrong if |
|---|---|---|---|
| D1 | Play to the grenade step | A red ring under you: leave it before it bursts | The step does not end |
| D2 | The shield step | The front blocks. Walk to its side or back, a full mk_warspite ends the step | The step ends from the front |
| D3 | The cover step (after the shield) | A barricade in front of the last dummy. Lock the dummy (Tab), full mk_warspite: "cover" until it breaks, then the step ends | No barricade, or it never breaks |
| D4 | Look at the ceiling over the barricade | A lamp that glows. The short step (later) pops it | No lamp |
| D5 | Finish the tutorial | 22 steps, "Demonstration complete" | A step that does not end |
