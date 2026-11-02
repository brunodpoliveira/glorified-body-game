"""T26 remake look: a 3D Studio prop GLB (T15/T16, already paid) into a game FBX plus its three maps.

Run through the RAM cap: kit/run_blender.sh -b --factory-startup -P tools/remake_props.py -- <in.glb> <out_dir> <name> <height_m> <tris>
Writes <out_dir>/<name>.fbx (the mesh at its real height, the origin at the bottom centre, +z forward in Unity),
<name>_base.png, <name>_mr.png (glTF metal-roughness: G roughness, B metal), <name>_normal.png and
<name>_preview.png (a 3/4 view, EEVEE, 640 px) for the check before placement (ART_LESSONS 4.4).
The source meshes have 300,000 to 500,000 triangles: the decimate brings them to <tris>.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
GLB, OUT_DIR, NAME, HEIGHT, TRIS = argv[0], argv[1], argv[2], float(argv[3]), int(argv[4])
os.makedirs(OUT_DIR, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=GLB)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
# Apply the import transforms (the glTF y-up turn) so the mesh data is z-up in metres.
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)

before = len(obj.data.polygons)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.remove_doubles(threshold=0.0004)
bpy.ops.mesh.quads_convert_to_tris()
bpy.ops.object.mode_set(mode="OBJECT")
ratio = min(1.0, TRIS / max(1, len(obj.data.polygons)))
if ratio < 1.0:
    mod = obj.modifiers.new("decimate", "DECIMATE")
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)

# Real height, the origin at the bottom centre.
xs = [v.co.x for v in obj.data.vertices]
ys = [v.co.y for v in obj.data.vertices]
zs = [v.co.z for v in obj.data.vertices]
scale = HEIGHT / max(1e-6, max(zs) - min(zs))
centre = Vector(((min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5, min(zs)))
for v in obj.data.vertices:
    v.co = (v.co - centre) * scale
obj.data.update()
# Smooth shading with a crease at 40 degrees (the seams of the GLB carry split vertices: no triangle shades).
bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))

xs = [v.co.x for v in obj.data.vertices]
ys = [v.co.y for v in obj.data.vertices]
zs = [v.co.z for v in obj.data.vertices]
print(f"[remake_props] {NAME}: tris {before} -> {len(obj.data.polygons)}, size x {max(xs)-min(xs):.3f} y {max(ys)-min(ys):.3f} z {max(zs)-min(zs):.3f} m")

# The three maps of the glTF material.
mat = obj.data.materials[0] if obj.data.materials else None
saved = {}
if mat and mat.use_nodes:
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)

    def image_into(socket_name):
        if bsdf is None:
            return None
        stack = [bsdf.inputs[socket_name]]
        seen = set()
        while stack:
            sock = stack.pop()
            for link in sock.links:
                node = link.from_node
                if node.name in seen:
                    continue
                seen.add(node.name)
                if node.type == "TEX_IMAGE" and node.image is not None:
                    return node.image
                stack.extend(node.inputs)
        return None

    for key, socket in (("base", "Base Color"), ("mr", "Roughness"), ("normal", "Normal")):
        img = image_into(socket)
        if img is None:
            continue
        path = os.path.join(OUT_DIR, f"{NAME}_{key}.png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        saved[key] = path
print(f"[remake_props] {NAME}: maps {sorted(saved)}")

bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.ops.export_scene.fbx(
    filepath=os.path.join(OUT_DIR, f"{NAME}.fbx"),
    use_selection=True,
    object_types={"MESH"},
    mesh_smooth_type="FACE",
    use_tspace=False,
    path_mode="STRIP",
    embed_textures=False,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_NONE",
    axis_forward="-Z",
    axis_up="Y",
    bake_anim=False,
)

# A preview for the check: a 3/4 view from the front (-y in Blender is the front of a 3D Studio prop).
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.world = bpy.data.worlds.new("w")
scene.world.color = (0.35, 0.35, 0.35)
size = max(max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs))
cam_data = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
cam.location = Vector((size * 1.6, -size * 2.0, HEIGHT * 0.5 + size * 0.9))
direction = Vector((0, 0, HEIGHT * 0.45)) - cam.location
cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
scene.camera = cam
sun_data = bpy.data.lights.new("sun", "SUN")
sun_data.energy = 4.0
sun = bpy.data.objects.new("sun", sun_data)
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
scene.collection.objects.link(sun)
scene.render.filepath = os.path.join(OUT_DIR, f"{NAME}_preview.png")
bpy.ops.render.render(write_still=True)
print(f"[remake_props] {NAME}: done")
