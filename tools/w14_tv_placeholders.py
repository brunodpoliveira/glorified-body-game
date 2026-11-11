#!/usr/bin/env python3
"""The two placeholder stills of the W14 TV's watch view (2026-11-11, Bruno: she watches the show as a cutscene).

- Assets/_Game/Cutscenes/n2_speech.png: the last speech of Things to Come (the writer's game/lines/N1_news.md, N2).
- Assets/_Game/Cutscenes/n2_credits.png: the end credits.

The render of the speech replaces n2_speech.png later. Run: python3 tools/w14_tv_placeholders.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from christmas_placeholders import CUTSCENES, card  # noqa: E402


def main():
    card(os.path.join(CUTSCENES, "n2_speech.png"),
         "PLACEHOLDER (N2, the render of Things to Come): an anime of the early 2000s, clean lines. Two men under the "
         "night sky, the great mirror of the observatory, the ship a point of light among the stars.")
    card(os.path.join(CUTSCENES, "n2_credits.png"),
         "PLACEHOLDER (N2): the end credits roll, the names of an animation staff, fast and small, white on black.")


if __name__ == "__main__":
    main()
