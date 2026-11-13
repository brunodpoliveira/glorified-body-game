#!/usr/bin/env python3
"""Lucy's canon look (Bruno's pick of 2026-10-30, T20: C3, the sunken gold veins, under the Crown) as maps for the
game's lucy_bare mesh, with no new model.

T20 painted C3 onto the T19 lucy_bare mesh (the same 2,496 faces and the same UV layout), and T21 cut the Crown out
of the head of that same mesh. So the game mesh already carries the helmet's shape: the Crown's own triangles give
its UV region. This script draws that region into a mask and puts the T19 helmet paint inside it and the C3 maps
outside it.

  python3 tools/remake_lucy_c3.py

In:  ~/MEGA/glorified/renders/T20_lucy_body/models/subtle/ (lucy_c3_basecolor_gold, _normal_gold,
     _metalrough_gold), T21_crown/crown_fixed.glb (its UVs), the T19 lucy_bare base colour (the helmet paint).
Out: Assets/_Game/Remake/Characters/lucy_c3_{base,n,ms}_{4k,1k}.{jpg,png}: 4096 for profile B, 1024 for profile A
     (the game's character size).
"""
import json
import os
import struct

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HOME = os.path.expanduser("~")
TOP = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
T20 = f"{HOME}/MEGA/glorified/renders/T20_lucy_body/models/subtle"
CROWN = f"{HOME}/MEGA/glorified/renders/T21_crown/crown_fixed.glb"
T19 = f"{HOME}/MEGA/glorified/renders/T19_characters/models/lucy_bare_game.glb"
OUT = os.path.join(TOP, "Assets/_Game/Remake/Characters")
S = 4096


def glb(path):
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    doc = json.loads(b[20:20 + n])
    blob = b[20 + n + 8:]
    return doc, blob


def accessor(doc, blob, index):
    a = doc["accessors"][index]
    v = doc["bufferViews"][a["bufferView"]]
    comps = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}[a["type"]]
    dtype = {5126: np.float32, 5125: np.uint32, 5123: np.uint16, 5121: np.uint8}[a["componentType"]]
    start = v.get("byteOffset", 0) + a.get("byteOffset", 0)
    data = np.frombuffer(blob, dtype=dtype, count=a["count"] * comps, offset=start)
    return data.reshape(a["count"], comps) if comps > 1 else data


def image_from_glb(doc, blob, index):
    im = doc["images"][index]
    v = doc["bufferViews"][im["bufferView"]]
    import io
    return Image.open(io.BytesIO(blob[v.get("byteOffset", 0):v.get("byteOffset", 0) + v["byteLength"]]))


def crown_mask():
    doc, blob = glb(CROWN)
    mask = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(mask)
    tris = 0
    for mesh in doc["meshes"]:
        for prim in mesh["primitives"]:
            uv = accessor(doc, blob, prim["attributes"]["TEXCOORD_0"])
            idx = accessor(doc, blob, prim["indices"]).reshape(-1, 3)
            for t in idx:
                pts = [(float(uv[i][0]) * S, float(uv[i][1]) * S) for i in t]
                draw.polygon(pts, fill=255)
                tris += 1
    # Grow the islands a few pixels: the mip levels and the bilinear filter must not pull skin into the helmet's edge.
    mask = mask.filter(ImageFilter.MaxFilter(9))
    cover = np.asarray(mask).mean() / 255.0
    print(f"[remake_lucy_c3] crown mask: {tris} triangles, {cover * 100:.1f} % of the texture")
    return np.asarray(mask, np.float32)[..., None] / 255.0


def main():
    os.makedirs(OUT, exist_ok=True)
    m = crown_mask()
    t19doc, t19blob = glb(T19)
    helmet = np.asarray(image_from_glb(t19doc, t19blob, 0).convert("RGB").resize((S, S), Image.LANCZOS), np.float32)
    base = np.asarray(Image.open(f"{T20}/lucy_c3_basecolor_gold.png").convert("RGB").resize((S, S), Image.LANCZOS), np.float32)
    normal = np.asarray(Image.open(f"{T20}/lucy_c3_normal_gold.png").convert("RGB").resize((S, S), Image.LANCZOS), np.float32)
    mr = np.asarray(Image.open(f"{T20}/lucy_c3_metalrough_gold.png").convert("RGB").resize((S, S), Image.LANCZOS), np.float32) / 255.0

    out_base = base * (1 - m) + helmet * m
    flat = np.array([128.0, 128.0, 255.0], np.float32)
    out_normal = normal * (1 - m) + flat * m
    metal = mr[..., 2:3] * (1 - m)                    # the helmet paint is not metal
    smooth = (1.0 - mr[..., 1:2]) * (1 - m) + 0.35 * m  # glTF G = roughness; the helmet a dull 0.35
    ms = np.concatenate([metal, metal, metal, smooth], -1)

    for size, tag in ((4096, "4k"), (1024, "1k")):
        Image.fromarray(np.clip(out_base, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS).save(f"{OUT}/lucy_c3_base_{tag}.jpg", quality=95)
        Image.fromarray(np.clip(out_normal, 0, 255).astype(np.uint8)).resize((size, size), Image.LANCZOS).save(f"{OUT}/lucy_c3_n_{tag}.png", optimize=True)
        Image.fromarray(np.clip(ms * 255 + 0.5, 0, 255).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS).save(f"{OUT}/lucy_c3_ms_{tag}.png", optimize=True)
        print(f"[remake_lucy_c3] wrote lucy_c3_*_{tag} ({size})")
    print(f"[remake_lucy_c3] gold (metal > 0.5): {(metal > 0.5).mean() * 100:.1f} % of the texture")


if __name__ == "__main__":
    main()
