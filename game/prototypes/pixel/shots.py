"""
Builds every deliverable screenshot from the real frames.

    uv run --no-project --with pillow --with numpy python shots.py [--data DIR] [--out DIR] [only ...]

`only` picks a subset: map close region seasons sheet gif.
"""
import argparse
import os
import sys
import time
import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from world import Frame, DEFAULT_DATA  # noqa: E402
import render as R  # noqa: E402
import overview as O  # noqa: E402
import sheet as SH  # noqa: E402
from palette import RGB  # noqa: E402

# Windows (tile centre x, y) chosen on frame40: the Sky civ's river capital and its daughter villages; the Yellow
# capital with its fields and weir for the close-ups.
MAP_CENTRE = (50.5, 67.2)       # ~33x19 tiles: Sky (civ index 1) heartland: its capital (50,73), 4 more villages
CLOSE_CENTRE = (109.3, 40.9)    # Yellow (civ index 3) capital: fields, orchards, river, weir, pastures
SEASON_CENTRE = (51.0, 29.5)    # Orange (civ index 0) temperate heartland: woodland, steppe, forest


def log(*a):
    print(*a, flush=True)


def shot_map(f, out):
    sc = R.Scene(f, MAP_CENTRE, (33, 20), 24)
    sc.render()
    sc.image(2).save(os.path.join(out, "map_scale.png"))
    sc.image(1).save(os.path.join(out, "native", "map_scale_1x.png"))


def shot_close(f, out):
    sc = R.Scene(f, CLOSE_CENTRE, (11, 6.4), 24)
    sc.render()
    sc.image(6).save(os.path.join(out, "settlement_close.png"))
    sc.image(1).save(os.path.join(out, "native", "settlement_close_1x.png"))


def shot_region(f, out):
    ov = O.Overview(f, 14, 6, 132, 88, 6)
    ov.render()
    ov.image(2).save(os.path.join(out, "region_overview.png"))
    ov.image(1).save(os.path.join(out, "native", "region_overview_1x.png"))


def shot_seasons(f, out):
    for i, s in enumerate(["spring", "summer", "autumn", "winter"]):
        sc = R.Scene(f, SEASON_CENTRE, (22, 12.5), 24, season=s)
        sc.render()
        sc.image(3).save(os.path.join(out, f"season_{s}.png"))


def shot_gif(f, out, n=16, scale=4):
    frames = []
    pal_img = Image.new("P", (1, 1))
    flat = []
    for c in RGB:
        flat += list(c)
    flat += [0, 0, 0] * (256 - len(RGB))
    pal_img.putpalette(flat)
    for k in range(n):
        sc = R.Scene(f, CLOSE_CENTRE, (11, 6.4), 24, fno=k, nf=n)
        G = sc.render()
        im = Image.fromarray(G.astype(np.uint8), "P")
        im.putpalette(flat)
        im = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
        frames.append(im)
    frames[0].save(os.path.join(out, "animated.gif"), save_all=True, append_images=frames[1:], duration=110, loop=0,
                   optimize=False, disposal=1)


def shot_sheet(out, f=None):
    SH.render_sheet(os.path.join(out, "sprite_sheet.png"), 6, "summer", f)


def all_shots(data=DEFAULT_DATA, out=None, only=None):
    out = out or os.path.dirname(os.path.abspath(__file__))
    os.makedirs(os.path.join(out, "native"), exist_ok=True)
    f40 = Frame(os.path.join(data, "frame40.json"))
    jobs = [("map", lambda: shot_map(f40, out)), ("close", lambda: shot_close(f40, out)),
            ("region", lambda: shot_region(f40, out)), ("seasons", lambda: shot_seasons(f40, out)),
            ("sheet", lambda: shot_sheet(out, f40)), ("gif", lambda: shot_gif(f40, out))]
    for name, fn in jobs:
        if only and name not in only:
            continue
        t = time.time()
        fn()
        log(f"{name}: {time.time() - t:.1f}s")


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default=DEFAULT_DATA)
    ap.add_argument("--out", default=None)
    ap.add_argument("only", nargs="*")
    a = ap.parse_args()
    all_shots(a.data, a.out, a.only)
