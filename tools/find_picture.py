#!/usr/bin/env python3
"""Find a picture by its look: the pictures you give are matched against every picture under the render and
design folders by a 24 x 14 grey thumbnail (a squared distance, 0 = the same picture, under 100 = a close copy
or a frame of it). Bruno could not find his corridor plate on 2026-10-22; this found it in a minute (RFAB_GAPS 12).
Usage: tools/find_picture.py <picture> [<picture> ...]
"""
import os
import sys

from PIL import Image

refs = {}
def thumb(p):
    im = Image.open(p).convert('L').resize((24, 14), Image.BILINEAR)
    px = list(im.get_flattened_data()) if hasattr(im, 'get_flattened_data') else list(im.getdata())
    m = sum(px) / len(px)
    return [v - m for v in px]
def dist(a,b): return sum((x-y)**2 for x,y in zip(a,b))/len(a)
for n in sys.argv[1:]: refs[n] = thumb(n)
roots = [os.path.expanduser(r) for r in ('~/MEGA/glorified-body-renders', '~/MEGA/code/reality-fabricator/the-glorified-body', '~/Pictures', '~/Downloads')]
best={n:[] for n in refs}
count=0
for root in roots:
    for dp,dn,fn in os.walk(root):
        if '/node_modules' in dp or '/.git' in dp: continue
        for f in fn:
            if f.lower().rsplit('.',1)[-1] not in ('png','jpg','jpeg','webp','gif'): continue
            p=os.path.join(dp,f)
            try:
                if os.path.getsize(p) < 20000: continue
                t=thumb(p)
            except Exception: continue
            count+=1
            for n,r in refs.items():
                best[n].append((dist(t,r),p))
print('scanned',count)
for n in refs:
    best[n].sort()
    print('== ref',n)
    for d,p in best[n][:6]: print('  %8.1f %s'%(d,p))
