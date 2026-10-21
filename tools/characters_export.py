"""T19 character to Unity: one GLB of 3D Studio (the rig and the actions) becomes one FBX.

Run: kit/run_blender.sh -b --factory-startup -P tools/characters_export.py -- <class>_animated.glb <out>.fbx <basecolor.png>

Steps:
1. Import the animated GLB (the mesh, the mixamo rig, the actions idle, walk, run).
2. Drop the stray helper mesh of the importer. Keep one mesh and one armature.
3. The material: the Principled BSDF with the base colour from the game file only, matte (roughness 1, no specular).
   The normal and the metallic-roughness maps go (they ghost the painted detail, ART_LESSONS §3).
4. Turn the figure from Blender +x to Blender -y and apply the rotation to the mesh and the armature, so Unity
   shows it along +z (the standard FBX axes). The feet stay on the origin.
5. Export the FBX with every action as its own take. Unity imports it as Humanoid.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix

args = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT, BASECOLOR = args[:3]

for obj in list(bpy.data.objects):
    bpy.data.objects.remove(obj, do_unlink=True)

bpy.ops.import_scene.gltf(filepath=GLB)

armature = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
if armature is None:
    raise SystemExit("no armature in " + GLB)

# The glTF importer adds a helper mesh for the bone shapes. Anything that is not skinned to the armature goes.
for obj in list(bpy.data.objects):
    if obj.type == "MESH" and not any(m.type == "ARMATURE" for m in obj.modifiers):
        bpy.data.objects.remove(obj, do_unlink=True)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if len(meshes) != 1:
    raise SystemExit(f"expected one skinned mesh, found {len(meshes)} in {GLB}")
mesh = meshes[0]

# The material: base colour only, matte.
image = bpy.data.images.load(BASECOLOR)
image.colorspace_settings.name = "sRGB"
for slot in mesh.material_slots:
    mat = slot.material
    if mat is None:
        continue
    mat.use_nodes = True
    tree = mat.node_tree
    bsdf = next((n for n in tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    output = next((n for n in tree.nodes if n.type == "OUTPUT_MATERIAL"), None)
    for node in list(tree.nodes):
        if node not in (bsdf, output):
            tree.nodes.remove(node)
    if bsdf is None:
        bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
        tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 1.0
    bsdf.inputs["Metallic"].default_value = 0.0
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.0

# The actions: keep every one (the exporter writes each as a take).
actions = [a for a in bpy.data.actions]
for action in actions:
    action.use_fake_user = True

# The turn: +x forward to -y forward (Blender), written into the data of both objects. The operator
# transform_apply leaves a parented child's data alone (it applies the local transform only), so the mesh and
# the armature get the same matrix on their data, and both objects end at identity. The bone keys of the actions
# are local to each bone, so a turn of the whole rest pose keeps them valid. The corner normals of the import
# (the custom split normals) turn with the mesh.
turn = Matrix.Rotation(math.radians(-90.0), 4, "Z")
mesh_world = turn @ mesh.matrix_world
arm_world = turn @ armature.matrix_world
rot3 = mesh_world.to_3x3()
corner_normals = [(rot3 @ loop.normal).normalized() for loop in mesh.data.loops]
mesh.data.transform(mesh_world)
mesh.data.normals_split_custom_set(corner_normals)
armature.data.transform(arm_world)
armature.matrix_world = Matrix.Identity(4)
mesh.matrix_parent_inverse = Matrix.Identity(4)
mesh.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()

# A report for the log: the size after the turn. The shoulders span x, the depth is along y (the front at -y).
xs = [v.co.x for v in mesh.data.vertices]
ys = [v.co.y for v in mesh.data.vertices]
zs = [v.co.z for v in mesh.data.vertices]
print(f"[characters_export] {os.path.basename(GLB)}: verts={len(mesh.data.vertices)} tris={len(mesh.data.polygons)} "
      f"x={min(xs):.3f}..{max(xs):.3f} y={min(ys):.3f}..{max(ys):.3f} z={min(zs):.3f}..{max(zs):.3f} "
      f"actions={[a.name for a in actions]} bones={len(armature.data.bones)}")

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
mesh.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=True,
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="OFF",
    use_tspace=False,
    add_leaf_bones=False,
    use_armature_deform_only=True,
    armature_nodetype="NULL",
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode="STRIP",
    embed_textures=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_NONE",
    axis_forward="-Z",
    axis_up="Y",
)
print(f"[characters_export] wrote {OUT} ({os.path.getsize(OUT) // 1024} KB)")
