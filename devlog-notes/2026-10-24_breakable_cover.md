# 2026-10-24: breakable cover (tables, barricades, plaster, lamps, items)

Bruno's answers to the cover study (`2026-10-24_study_breakable_cover.md`, open questions), evening of 2026-10-24:

1. "broken room stays broken".
2. "all collateral damage".
3. Enemy bullets wear down her cover: "yes".
4. Breakable lights: "realtime with a fake bounce".
5. A darkness mechanic: "yes, we can brainstorm that later".
6. "cover only enemies, on purpose; the player can reposition itself behind a pillar, etc, but there's no dedicated
   take cover button".

All numbers here are PROPOSED.

## What breaks

| Piece | Size | Health | Cover | What breaks it |
|---|---|---|---|---|
| Barricade (plywood panel on steel feet) | 1.6 x 1.2 m | 90 | Yes: it stops her powers | 3 full blasts (2 with the MK boost), a thrown body, about 6 enemy bullets, an explosion |
| Lab table (laminate on four legs) | 1.6 x 0.8 x 0.78 m | 30 | No: too low | A full blast with the MK boost beside it, an explosion, a thrown body, a slam |
| Plaster patch on a wall | about 1.2 x 1 m | 25 | No | Two enemy bullets, an explosion, a blast near the wall, a body thrown into it |
| Ceiling lamp (the glass panel) | 0.6 x 0.24 m | 8 | No | Anything that reaches it, and the start of the short within 5 m |
| Items on a table (a monitor, binders, a box) | | | | They wake on any hit near them and when the table under them breaks |

## The rules

- **All of it is collateral damage.** The auto-lock and the target switch never pick a breakable. The damage comes
  from what the fight does:
  - A blast splashes around its target: 1 m, plus 0.8 m at a full charge, at 0.6 of its damage.
  - A full mk_erebus does the same at a smaller radius.
  - A slam splashes 1.3 m, and the landing of a thrown body splashes 1 m.
  - Grenades and rockets splash at their own radius with double damage.
  - Enemy bullets hit the piece they strike.
  - A thrown body hits what is on its way. When the piece breaks, the body flies on through it.
- **Cover is for the enemies.** The barricade is 1.2 m high:
  - Her power leaves her chest at 1.2 m, so it stops on the barricade. The cover takes the blast, and the HUD
    result is "cover".
  - Their muzzle is at 1.3 m, so they shoot over it.
  - mk_orzabal reaches over it: the lift is not stopped.
  - A soldier picks a firing spot behind an intact barricade: on its far side from her, with a clear line over it,
    one soldier for each barricade. He walks to it (at most 6 s, no shot from the open on the way) and fights from
    there. When it breaks, he moves on.
  - Every barricade on her line takes the blast, the first one first.
- **She has no cover button.** She can stand behind a pillar. The enemy bullets wear down a barricade that stands
  between them and her.
- **A thrown body arcs down** to the target's chest as it flies (it was level at the lift height, 1.5 m, and flew
  over the barricade). Its whole height counts against walls now (a sweep from its feet to its chest), not only
  its feet.
- **Lamps are Realtime** (the bake leaves them out). Each one has a fake bounce: a point light 0.7 m above the floor
  under it, about a tenth of the lamp, with no shadow. A broken lamp sparks, flickers for 0.3 s, then its light,
  its bounce and its glow go out, and the glass falls.
- **The short's surge** pops the lamps within 5 m of her when it starts. This is my proposal, for the darkness
  brainstorm.
- **A broken room stays broken** (`RoomDamageLedger`):
  - A visit writes its damage as pending. When she leaves for another room, the room keeps it.
  - The next visit starts with the pieces and the items where they fell, the lamps dark, and the damaged cover at
    its health.
  - A restart after a death puts back what broke in that visit.
  - The main menu ends the session. The save game reads the ledger later.
- **The pieces stay** where they fall: they settle and freeze, with no collider. Small ones stop casting a shadow.
  Nothing shrinks away (the study: Warspite's 6 s shrink looks "gamey" in a slow horror game).

## How it is made

- **The cut** (`Editor/Destruction/BoxFracture.cs`): the editor cuts each design into convex pieces with flat
  cuts. The cut faces are closed, and a seed gives the same pieces every build. So a break costs no CPU at run time.
  The pieces of a design are one asset (`Assets/_Game/Breakables/Fragments/`). Each piece is a convex MeshCollider
  and a rigidbody on the new **Debris** layer (slot 19). Debris hits the room, the other pieces and the ragdolls,
  not her and not the capsules.
- **The builders** (`Editor/Destruction/BreakableAssets.cs`): the table, the barricade, the plaster patch, the lamp
  and the items, with greybox materials. OpenFracture (MIT) or Blender's cell fracture is for the real models later.
- **The run-time code** (`Scripts/Destruction/`, namespace `GlorifiedBody.Destruction`): `Breakable`,
  `BreakableLight`, `DebrisPiece`, `Greeble`, `Breakables` (splash, hit, cover line, surge, the Debris
  collisions), `RoomDamageLedger`, `DestructionFx` with `DestructionFxSettings`. The hooks are in
  `PsychicPowers.FireAt`, `TelekinesisLift`, `EnemyProjectile.Impact`, the `EnemyGrenade` and `EnemyRocket`
  bursts, `ShortController.Begin`, and `SoldierBrain.PickSpot`.
- **The effects:** dust, glints and sparks are particle systems made in code with a soft round dot (not squares).
- **The sounds** are synthesised placeholders (`tools/destruction_sfx.py`: wood, metal, glass, plaster, plastic, a
  chip, sparks). RFab has no sound-effect generation: RFAB_GAPS row 14.
- **The NavMesh:** the barricades and the tables cut it while they stand (`NavMeshObstacle`, carve). A break
  turns that off, and the way is open.

## The rooms

- **COVER TEST** (a new main menu button, `CoverTestSetup.BuildCoverTest`, local only like the AI test room: the
  soldiers carry vendor data):
  - A dark office hall of 16 x 12 m, lit by six breakable lamps and a weak blue fill (so a room with every lamp out
    is dark, not black).
  - Three barricades, three tables with items, four plaster patches.
  - A rifleman and a soldier. Lucy at the south end.
- **The tutorial gym:**
  - A new step "cover" after "shield" (22 steps): a barricade in front of Target_3, off until its step. Break it
    with mk_warspite.
  - One breakable lamp over it, Realtime in the baked gym. The gym is baked again.

## Proof

- EditMode `BreakableAssetsTests` (3):
  - The cut pieces are convex and closed, they face out, and they fill the box to 1 percent.
  - The same seed gives the same pieces.
  - The layer, the piece assets and the sounds are in place.
  - The ledger keeps a room when she leaves, drops a restart, and clears at the menu.
- PlayMode `BreakableTests` (9):
  - The barricade stops her blast until it breaks (3 full blasts with no boost), then the blast reaches the
    soldier.
  - A boosted blast beside a table breaks it, and the monitor lands on the floor.
  - 3 bullets take 45 from a barricade.
  - A grenade breaks a table, and the items fly.
  - A thrown body breaks through a barricade.
  - The surge puts out the 3 lamps near her, not the other 3, and their bounces go out.
  - Bullets bring the plaster down to the bare wall.
  - The room is still broken when she comes back (a piece within 1 cm of where it fell), and a restart puts back
    what broke in that visit.
  - A soldier takes cover behind a barricade on its far side from her.
- The "cover" bot (`tools/unity.sh play CoverTest Main_Menu cover 300`, 8 steps): the room, a soldier in cover,
  the blast on cover, the barricade breaks, the table breaks (4 of 4 items move), the plaster falls, the lamps go
  out, the room is still broken when she comes back. Shots: `~/glorified-body-renders/game/ai/cover/`.
- The tutorial bot: the cover step in 29 s, 24 of 24 bot steps (22 tutorial steps).
- The full run: EditMode 39/39, PlayMode 134/134, the bots cover 8/8, tutorial 24/24, structural 24/24, look 32/32,
  ai 29/29. The first ai run missed its "telegraph" watch (12 s) while the soldiers fired. The second run passed:
  the AI test room has no barricade, so the cover code does not run there.

## Fixed on the way

- The piece assets were cut again on every call. That broke the mesh references of the earlier tables in the same
  build, and those pieces fell through the floor. The main asset takes the file's name (`table_top`, not
  `table_top_0`), so the names never matched.
- An interpolated body put its old pose back over a restored piece: a frozen body now has no interpolation.
- The greybox surfaces on Simple Lit rendered about 5 times darker than the wall's Lit material on the same
  object, and the lamp's emission did not show (a probe: 5 against 24 in grey, 68 against 247 on the panel). They
  use URP Lit now, and the lamp panel uses URP Unlit. Setting a material's shader again also clears its keywords:
  the builder sets it only when it differs.
- The barricade took its own splash on top of the blast (45 in place of 35).
- The vendor AI stops about 0.8 m short of its point: a soldier counts as at his cover within 1 m.
- The tutorial drive walked into the parallel bars' south posts on its way back: it goes south of them now.

## Open, for Bruno

- The darkness mechanic (your answer 5): the surge already puts lamps out. What does a dark room do?
- Should the thrown body's arc down to the chest stay? It also changes throws with no cover. The RagdollTests throw
  still knocks both bodies down.
- The sounds are placeholders. They need a source (RFab row 14, or a CC0 pack).
