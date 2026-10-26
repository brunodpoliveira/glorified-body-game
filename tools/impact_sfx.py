#!/usr/bin/env python3
"""The bullet hit sounds per surface (2026-10-25, Bruno: the hit on a concrete wall sounded like metal on metal).

Concrete: synthesised here (a sharp crack, a low thud, a short crumble of grit, no ringing tone), so the file is ours.
Metal, wood, glass, soft: Kenney's "Impact Sounds" 1.0 (CC0, https://kenney.nl/assets/impact-sounds), copied from
the unpacked zip. No vendor pack has surface sounds: every Invector impact prefab plays one ricochet clip.

Writes to Assets/_Game/Audio/Impacts/.
Usage: python3 tools/impact_sfx.py <the unpacked Kenney folder (holds Audio/ and License.txt)>
"""
import os
import shutil
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import destruction_sfx as sfx  # noqa: E402

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Audio", "Impacts")

KENNEY = {
    "metal": ["impactMetal_light_000", "impactMetal_light_001", "impactMetal_light_002"],
    "wood": ["impactWood_light_000", "impactWood_light_001", "impactWood_light_002"],
    "glass": ["impactGlass_light_000", "impactGlass_light_001", "impactGlass_light_002"],
    "soft": ["impactSoft_medium_000", "impactSoft_medium_001", "impactSoft_medium_002"],
}


def concrete(rng):
    """A bullet on concrete: noise only, so no tone rings on."""
    buf = np.zeros(int(sfx.RATE * 0.45))
    sfx.place(buf, 1.0 * sfx.highpass(sfx.crack(rng, 0.04, 9000, 0.004), 1200), 0.0)
    sfx.place(buf, 0.7 * sfx.crack(rng, 0.08, 1400, 0.018), 0.0)
    sfx.place(buf, 0.45 * sfx.thump(rng.uniform(85, 120), 0.12, 0.022), 0.0)
    n = int(sfx.RATE * 0.3)
    grains = sfx.lowpass(sfx.noise(n, rng) * (rng.uniform(0, 1, n) > 0.992), 3500) * 7
    sfx.place(buf, 0.3 * grains * np.exp(-np.arange(n) / sfx.RATE / 0.09), 0.03)
    return buf


def main():
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    kenney = sys.argv[1]
    sfx.OUT = OUT
    os.makedirs(OUT, exist_ok=True)
    for i in range(4):
        sfx.write(f"concrete_hit_{i}", concrete(np.random.default_rng(100 + i)))
    for kind, names in KENNEY.items():
        for name in names:
            shutil.copyfile(os.path.join(kenney, "Audio", name + ".ogg"), os.path.join(OUT, name + ".ogg"))
            print(f"{kind}: {name}.ogg")
    shutil.copyfile(os.path.join(kenney, "License.txt"), os.path.join(OUT, "Kenney_License.txt"))


if __name__ == "__main__":
    main()
