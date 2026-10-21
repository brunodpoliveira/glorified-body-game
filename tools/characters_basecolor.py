#!/usr/bin/env python3
"""Copies the base colour image out of a 3D Studio game GLB (T19) as a 1024 px PNG, with no Blender.

Usage: python3 tools/characters_basecolor.py <class>_game.glb <out_dir>/<class>_basecolor
Writes <out_dir>/<class>_basecolor.png (1024 x 1024, the game texture size, ART_LESSONS §3) and prints the path.
Without Pillow the image goes out as delivered (4096, png or jpg).
"""
import io
import json
import struct
import sys

SIZE = 1024


def main(glb_path, out_stem):
    with open(glb_path, "rb") as f:
        magic, version, length = struct.unpack("<III", f.read(12))
        if magic != 0x46546C67:
            raise SystemExit(f"not a GLB: {glb_path}")
        json_len, json_type = struct.unpack("<II", f.read(8))
        doc = json.loads(f.read(json_len))
        bin_len, bin_type = struct.unpack("<II", f.read(8))
        blob = f.read(bin_len)

    images = doc.get("images", [])
    if not images:
        raise SystemExit(f"no image in {glb_path}")
    # The game file carries one base colour. Take the image the first material's base colour texture points at.
    index = 0
    materials = doc.get("materials", [])
    if materials:
        pbr = materials[0].get("pbrMetallicRoughness", {})
        tex = pbr.get("baseColorTexture", {}).get("index")
        if tex is not None:
            index = doc["textures"][tex].get("source", 0)
    image = images[index]
    view = doc["bufferViews"][image["bufferView"]]
    start = view.get("byteOffset", 0)
    data = blob[start:start + view["byteLength"]]

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
    main(sys.argv[1], sys.argv[2])
