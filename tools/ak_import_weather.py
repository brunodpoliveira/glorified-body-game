#!/usr/bin/env python3
"""T38: bring the Shader Graph sample "Production Ready Shaders" (the Weather / Rain part) into the project.

  python3 -I tools/ak_import_weather.py

The sample sits in the package cache (Library/PackageCache/com.unity.shadergraph@*/Samples~). The Package Manager
imports a whole sample (Common is 106 MB of textures and meshes). This script copies only the rain floor's subgraphs and
the files they need, found by their GUIDs, with the .meta files (the GUIDs stay the same), into
Assets/Samples/Shader Graph/<version>/. It then writes Assets/_Game/Arkham/AK_WetFloor.shadergraph: a copy of the
sample's RainFloorExample.shadergraph with our black granite maps in place of the cobblestones, tiling 1.
The sample is under the Unity Companion License: Assets/Samples/ stays out of git (.gitignore).
"""
import glob
import json
import os
import re
import shutil

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CACHE = os.path.join(ROOT, "Library", "PackageCache")
SG = sorted(glob.glob(os.path.join(CACHE, "com.unity.shadergraph@*")))[0]
VERSION = json.load(open(os.path.join(SG, "package.json")))["version"]
SOURCES = [os.path.join(SG, "Samples~")] + [os.path.join(p, "Samples~") for p in glob.glob(os.path.join(CACHE, "com.unity.render-pipelines.core@*"))]
DEST = os.path.join(ROOT, "Assets", "Samples", "Shader Graph", VERSION)
GUID = re.compile(r'guid\\?"?\s*[:=]\s*\\?"?\s*([0-9a-f]{32})')

guid_to_file = {}
for src in SOURCES:
    for meta in glob.glob(os.path.join(src, "**", "*.meta"), recursive=True):
        m = re.search(r"^guid: ([0-9a-f]{32})", open(meta, errors="ignore").read(), re.M)
        if m and not os.path.isdir(meta[:-5]):
            guid_to_file[m.group(1)] = (src, meta[:-5])

example = os.path.join(SG, "Samples~", "ProductionReady", "Weather", "RainFloorExample.shadergraph")
todo = [example]
needed = set()
while todo:
    f = todo.pop()
    if not f.endswith((".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".mat")):
        continue
    for g in GUID.findall(open(f, errors="ignore").read()):
        if g in guid_to_file and guid_to_file[g][1] not in needed:
            needed.add(guid_to_file[g][1])
            todo.append(guid_to_file[g][1])

copied = 0
for f in sorted(needed):
    src = next(s for s in SOURCES if f.startswith(s))
    rel = os.path.relpath(f, src)
    out = os.path.join(DEST, rel)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    for a, b in ((f, out), (f + ".meta", out + ".meta")):
        if not os.path.exists(b) or os.path.getmtime(b) < os.path.getmtime(a):
            shutil.copy2(a, b)
    copied += 1
    print("[ak_import_weather]", rel)
# folder metas are written by Unity on import

# Our wet floor: the example graph with the granite maps (the sample's cobblestones out).
def guid_of(asset):
    return re.search(r"^guid: ([0-9a-f]{32})", open(asset + ".meta").read(), re.M).group(1)

tex = os.path.join(ROOT, "Assets", "_Game", "Remake", "Textures")
ak = os.path.join(ROOT, "Assets", "_Game", "Arkham")
swap = {
    "b356831ef4f363d48989d117c6ea79e3": guid_of(os.path.join(tex, "blk_granite_2k.png")),      # colour
    "7be741f9449557c488d643ae307fe1ab": guid_of(os.path.join(ak, "Textures", "ak_granite_n_soft.png")),  # normal at 0.25
    "8c31c20ac2129c3428084be388816642": guid_of(os.path.join(tex, "blk_granite_2k_ms.png")),   # R metal, A smoothness
}
graph = open(example).read()
for a, b in swap.items():
    graph = graph.replace(a, b)
# tiling 2 -> 1 (the granite is laid at 1, as RB_Granite)
graph = re.sub(r'("m_DisplayName": "Tiling",[\s\S]*?"m_Value": \{\s*"x": )2\.0(,\s*"y": )2\.0', r"\g<1>1.0\g<2>1.0", graph, count=1)
open(os.path.join(ak, "AK_WetFloor.shadergraph"), "w").write(graph)
print(f"[ak_import_weather] {copied} files into {os.path.relpath(DEST, ROOT)}; wrote Assets/_Game/Arkham/AK_WetFloor.shadergraph")
