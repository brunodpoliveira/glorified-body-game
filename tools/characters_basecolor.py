#!/usr/bin/env python3
"""Copies the maps out of a 3D Studio game GLB, with no Blender.

Usage: python3 tools/characters_basecolor.py <class>_game.glb <out_dir>/<class>_basecolor [--maps]

Without --maps (the T19 classes): writes <out_dir>/<class>_basecolor.png at 1024 x 1024 (the T19 game texture size,
ART_LESSONS §3) and prints the path. The normal and the metal-rough maps stay out: on the T19 models they ghost the
painted detail.

With --maps (a passed T49 game file, rounds 23, 24, 175, 178: the normal map carries the relief, the metal-rough map the
gloss by material): the maps stay at their delivered size (2048). For each mesh node, three files: the largest mesh (the
body) as <class>, any other mesh as <class>_<node> (Lucy's Crown: lucy_bare_crown_t27):
  <stem>_basecolor.png  the base colour (sRGB)
  <stem>_normal.png     the normal map as delivered (glTF and Unity both read +Y up: no flip)
  <stem>_ms.png         Unity's metallic-smoothness map (linear): R = metal (glTF B), A = smoothness (1 - glTF G)
The import (CharacterImportSettings) reads the names; CreateCharacterAssets makes one material for each stem and gives a
part's material to the renderer of the same name.
Without Pillow the base colour goes out as delivered (png or jpg) and --maps fails.
"""
import io
import json
import struct
import sys

SIZE = 1024


def read_glb(glb_path):
    with open(glb_path, "rb") as f:
        magic, _version, _length = struct.unpack("<III", f.read(12))
        if magic != 0x46546C67:
            raise SystemExit(f"not a GLB: {glb_path}")
        json_len, _json_type = struct.unpack("<II", f.read(8))
        doc = json.loads(f.read(json_len))
        bin_len, _bin_type = struct.unpack("<II", f.read(8))
        blob = f.read(bin_len)
    return doc, blob


def image_of(doc, blob, texture_ref):
    """The bytes and the record of the image a material texture slot points at, or (None, None)."""
    if not texture_ref or texture_ref.get("index") is None:
        return None, None
    image = doc["images"][doc["textures"][texture_ref["index"]].get("source", 0)]
    view = doc["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    return blob[start:start + view["byteLength"]], image


def part_name(name):
    """A mesh node name as a file and object name: letters, digits and _ (the same rule as CreateCharacterAssets.PartName)."""
    return "".join(c if c.isalnum() else "_" for c in name)


def mesh_parts(doc):
    """(node name, material index, triangles) for every mesh node, the largest first: the body, then the parts."""
    parts = []
    for node in doc.get("nodes", []):
        if "mesh" not in node:
            continue
        for prim in doc["meshes"][node["mesh"]]["primitives"]:
            idx = prim.get("indices")
            tris = doc["accessors"][idx]["count"] // 3 if idx is not None else 0
            parts.append((node.get("name", "mesh"), prim.get("material", 0), tris))
    parts.sort(key=lambda p: -p[2])
    return parts


def write_maps(doc, blob, material, stem, Image):
    """The three maps of one material: <stem>_basecolor.png, <stem>_normal.png, <stem>_ms.png."""
    import numpy as np
    pbr = material.get("pbrMetallicRoughness", {})
    base_data, _ = image_of(doc, blob, pbr.get("baseColorTexture"))
    normal_data, _ = image_of(doc, blob, material.get("normalTexture"))
    mr_data, _ = image_of(doc, blob, pbr.get("metallicRoughnessTexture"))
    if base_data is None or normal_data is None or mr_data is None:
        raise SystemExit(f"--maps: the material {material.get('name')} lacks a base colour, a normal or a metal-rough map")
    picture = Image.open(io.BytesIO(base_data)).convert("RGB")
    picture.save(stem + "_basecolor.png", optimize=True)
    Image.open(io.BytesIO(normal_data)).convert("RGB").save(stem + "_normal.png", optimize=True)
    mr = np.asarray(Image.open(io.BytesIO(mr_data)).convert("RGB"), np.uint8)
    ms = np.zeros(mr.shape[:2] + (4,), np.uint8)
    ms[..., 0] = mr[..., 2]          # metal
    ms[..., 3] = 255 - mr[..., 1]    # smoothness = 1 - roughness
    Image.fromarray(ms, "RGBA").save(stem + "_ms.png", optimize=True)
    metal = mr[..., 2] / 255.0
    smooth = 1.0 - mr[..., 1] / 255.0
    hard = metal >= 0.5
    print(f"[characters_basecolor] {stem.rsplit('/', 1)[-1]} ({material.get('name')}): {picture.size[0]} px, "
          f"metal >= 0.5 on {hard.mean() * 100:.1f} % (smoothness median {np.median(smooth[hard]) if hard.any() else 0:.2f}), "
          f"the rest smoothness median {np.median(smooth[~hard]):.2f}, p95 {np.percentile(smooth[~hard], 95):.2f}", file=sys.stderr)


def main(glb_path, out_stem, maps):
    doc, blob = read_glb(glb_path)
    images = doc.get("images", [])
    if not images:
        raise SystemExit(f"no image in {glb_path}")

    if maps:
        # One map set for each mesh node: the largest mesh (the body) is <class>, a part is <class>_<node>
        # (T49 Lucy: the Crown crown_t27 with its own material, round 178).
        try:
            from PIL import Image
        except ImportError:
            raise SystemExit("--maps needs Pillow")
        stem = out_stem[:-len("_basecolor")] if out_stem.endswith("_basecolor") else out_stem
        for i, (name, mat, _tris) in enumerate(mesh_parts(doc)):
            write_maps(doc, blob, doc["materials"][mat], stem if i == 0 else f"{stem}_{part_name(name)}", Image)
        print(stem + "_basecolor.png")
        return

    material = (doc.get("materials") or [{}])[0]
    pbr = material.get("pbrMetallicRoughness", {})
    data, image = image_of(doc, blob, pbr.get("baseColorTexture"))
    if data is None:
        # No base colour slot: the first image.
        view = doc["bufferViews"][images[0]["bufferView"]]
        start = view.get("byteOffset", 0)
        data, image = blob[start:start + view["byteLength"]], images[0]

    try:
        from PIL import Image
    except ImportError:
        ext = ".jpg" if image.get("mimeType") == "image/jpeg" else ".png"
        out = out_stem + ext
        with open(out, "wb") as f:
            f.write(data)
        print(out)
        return

    picture = Image.open(io.BytesIO(data)).convert("RGB")
    if picture.size != (SIZE, SIZE):
        picture = picture.resize((SIZE, SIZE), Image.LANCZOS)
    out = out_stem + ".png"
    picture.save(out, optimize=True)
    print(out)


if __name__ == "__main__":
    flags = [a for a in sys.argv[1:] if a.startswith("--")]
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    main(args[0], args[1], "--maps" in flags)
