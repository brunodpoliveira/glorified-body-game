#!/usr/bin/env python3
"""The travel of every clip in a T19 animated GLB, for the Unity import.

  tools/characters_clips.py <class>_animated.glb Assets/_Game/Characters/Models/<class>.clips.json

A clip that moves the figure across the floor (the hips end the cycle away from where they started) must give
its travel to the root motion in Unity, or the model runs ahead of the controller and snaps back at the loop
(Bruno, 2026-10-21: "her model slides back"). A clip that stays on the spot (an idle with its sway) keeps its
root in the pose. CharacterImportSettings reads this file next to the FBX and sets the clip that way, so the
decision comes from the data, not from the clip name.

The hips position is evaluated through the node chain (the armature, the root bone, the hips) at every key
time, in the glTF world (y up). travel = |P(last) - P(first)| on xz. reach = the largest |P(t) - P(first)|
on xz. A clip travels when travel >= TRAVEL_MIN (source units: the T19 figures are 1 m tall).
"""
import json
import os
import struct
import sys

import numpy as np

TRAVEL_MIN = 0.10

COMPONENT = {5120: ('i1', 1), 5121: ('u1', 1), 5122: ('i2', 2), 5123: ('u2', 2), 5125: ('u4', 4), 5126: ('f4', 4)}
COUNT = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def load_glb(path):
    with open(path, 'rb') as f:
        data = f.read()
    magic, _version, length = struct.unpack_from('<III', data, 0)
    if magic != 0x46546C67:
        raise SystemExit(f'[characters_clips] not a GLB: {path}')
    offset = 12
    doc = None
    blob = None
    while offset < length:
        chunk_length, chunk_type = struct.unpack_from('<II', data, offset)
        offset += 8
        chunk = data[offset:offset + chunk_length]
        offset += chunk_length
        if chunk_type == 0x4E4F534A:
            doc = json.loads(chunk)
        elif chunk_type == 0x004E4942:
            blob = chunk
    if doc is None or blob is None:
        raise SystemExit(f'[characters_clips] GLB without JSON and BIN chunks: {path}')
    return doc, blob


def accessor(doc, blob, index):
    acc = doc['accessors'][index]
    view = doc['bufferViews'][acc['bufferView']]
    base = view.get('byteOffset', 0) + acc.get('byteOffset', 0)
    dtype, size = COMPONENT[acc['componentType']]
    n = COUNT[acc['type']]
    stride = view.get('byteStride', size * n)
    out = np.empty((acc['count'], n), dtype=np.float64)
    for i in range(acc['count']):
        out[i] = np.frombuffer(blob, dtype=dtype, count=n, offset=base + i * stride)
    return out


def quat_matrix(q):
    x, y, z, w = q / np.linalg.norm(q)
    return np.array([
        [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
        [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
        [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)],
    ])


def trs_matrix(t, r, s):
    m = np.eye(4)
    m[:3, :3] = quat_matrix(np.asarray(r, dtype=np.float64)) * np.asarray(s, dtype=np.float64)
    m[:3, 3] = t
    return m


class Track:
    """One animated property of one node: the key times and the values, with the glTF interpolation."""

    def __init__(self, doc, blob, sampler):
        self.times = accessor(doc, blob, sampler['input'])[:, 0]
        values = accessor(doc, blob, sampler['output'])
        self.mode = sampler.get('interpolation', 'LINEAR')
        if self.mode == 'CUBICSPLINE':
            values = values[1::3]  # in-tangent, value, out-tangent: keep the value
        self.values = values

    def at(self, t):
        times = self.times
        if t <= times[0]:
            return self.values[0]
        if t >= times[-1]:
            return self.values[-1]
        i = int(np.searchsorted(times, t, side='right')) - 1
        if self.mode == 'STEP':
            return self.values[i]
        span = times[i + 1] - times[i]
        f = 0.0 if span <= 0 else (t - times[i]) / span
        a, b = self.values[i], self.values[i + 1]
        if len(a) == 4 and np.dot(a, b) < 0:
            b = -b
        return a + (b - a) * f


def node_trs(node):
    if 'matrix' in node:
        m = np.array(node['matrix'], dtype=np.float64).reshape(4, 4).T
        return m
    return trs_matrix(node.get('translation', [0, 0, 0]), node.get('rotation', [0, 0, 0, 1]), node.get('scale', [1, 1, 1]))


def parents(doc):
    parent = {}
    for i, node in enumerate(doc['nodes']):
        for child in node.get('children', []):
            parent[child] = i
    return parent


def find_hips(doc):
    names = [n.get('name', '') for n in doc['nodes']]
    joints = doc['skins'][0]['joints'] if doc.get('skins') else list(range(len(names)))
    for j in joints:
        if 'hips' in names[j].lower():
            return j
    parent = parents(doc)
    joint_set = set(joints)
    for j in joints:
        if parent.get(j) not in joint_set:
            return j
    return joints[0]


def world_position(doc, chain, tracks, t):
    """The world position of the last node of the chain (root first) at time t."""
    m = np.eye(4)
    for node_index in chain:
        node = doc['nodes'][node_index]
        animated = tracks.get(node_index, {})
        if animated:
            t_ = animated['translation'].at(t) if 'translation' in animated else np.array(node.get('translation', [0, 0, 0]), dtype=np.float64)
            r_ = animated['rotation'].at(t) if 'rotation' in animated else np.array(node.get('rotation', [0, 0, 0, 1]), dtype=np.float64)
            s_ = animated['scale'].at(t) if 'scale' in animated else np.array(node.get('scale', [1, 1, 1]), dtype=np.float64)
            local = trs_matrix(t_, r_, s_)
        else:
            local = node_trs(node)
        m = m @ local
    return m[:3, 3]


def measure(doc, blob):
    hips = find_hips(doc)
    parent = parents(doc)
    chain = [hips]
    while chain[-1] in parent:
        chain.append(parent[chain[-1]])
    chain.reverse()
    chain_set = set(chain)
    names = [n.get('name', '?') for n in doc['nodes']]
    clips = []
    for anim in doc.get('animations', []):
        tracks = {}
        times = set()
        for channel in anim['channels']:
            target = channel['target']
            node = target.get('node')
            if node not in chain_set or target['path'] not in ('translation', 'rotation', 'scale'):
                continue
            track = Track(doc, blob, anim['samplers'][channel['sampler']])
            tracks.setdefault(node, {})[target['path']] = track
            times.update(float(x) for x in track.times)
        if not times:
            # No key on the chain: the clip cannot travel.
            clips.append({'name': anim.get('name', f'anim{len(clips)}'), 'duration': 0.0, 'keys': 0, 'travel': 0.0, 'reach': 0.0, 'travels': False})
            continue
        ordered = sorted(times)
        positions = np.array([world_position(doc, chain, tracks, t) for t in ordered])
        flat = positions[:, [0, 2]]  # x and z: the floor in the glTF world (y up)
        travel = float(np.linalg.norm(flat[-1] - flat[0]))
        reach = float(np.max(np.linalg.norm(flat - flat[0], axis=1)))
        clips.append({
            'name': anim.get('name', f'anim{len(clips)}'),
            'duration': round(ordered[-1] - ordered[0], 4),
            'keys': len(ordered),
            'travel': round(travel, 4),
            'reach': round(reach, 4),
            'travels': travel >= TRAVEL_MIN,
        })
    return names[hips], chain, clips


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    source, out = argv[1], argv[2]
    doc, blob = load_glb(source)
    hips_name, chain, clips = measure(doc, blob)
    names = [doc['nodes'][i].get('name', '?') for i in chain]
    report = {
        'source': os.path.basename(source),
        'hips': hips_name,
        'chain': names,
        'travelMin': TRAVEL_MIN,
        'clips': clips,
    }
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, 'w') as f:
        json.dump(report, f, indent=2)
        f.write('\n')
    for clip in clips:
        verdict = 'travels' if clip['travels'] else 'stays'
        print(f"[characters_clips] {os.path.basename(source)} {clip['name']}: {clip['duration']:.2f} s, travel {clip['travel']:.3f}, reach {clip['reach']:.3f} -> {verdict}")
    if not clips:
        print(f'[characters_clips] {os.path.basename(source)}: no animation in the file')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
