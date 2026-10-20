"""
Sprite sheet: every sprite, labelled, enlarged x6 (labels in the 3x5 pixel font at x3), plus season variants and the
palette. Usage: python sheet.py OUT.png [frame.json]
"""
import sys
import numpy as np
from PIL import Image
import sprites as S
from palette import RGB, IDX, NAMES, WORLD, SEASONS
from font import G as GLYPHS

LUT = np.array(RGB + [(255, 0, 255)] * (256 - len(RGB)), np.uint8)


def to_rgba(px):
    h, w = px.shape
    out = np.zeros((h, w, 4), np.uint8)
    m = px != S.T
    out[..., :3] = LUT[px]
    out[..., 3] = np.where(m, 255, 0)
    return out


def build(frame=None):
    se = "summer"
    sec = []

    def add(section, label, sp):
        if not sec or sec[-1][0] != section:
            sec.append((section, []))
        sec[-1][1].append((label, sp))

    for civ, kind, pose, extra, lab in [
        (0, "adult", "stand", {}, "adult"), (0, "adult", "walk1", {}, "walk 1"), (0, "adult", "walk2", {}, "walk 2"),
        (0, "adult", "work", {}, "work (swing)"), (0, "adult", "bend", {}, "bend (gather)"),
        (1, "adult", "stand", {"female": True, "hair": 2, "cloth": 1}, "woman"),
        (2, "adult", "stand", {"warrior": True}, "warrior"), (3, "elder", "stand", {}, "elder"),
        (3, "elder", "walk1", {}, "elder walk"), (1, "child", "stand", {"hair": 3}, "child"),
        (1, "child", "walk1", {"hair": 3}, "child walk"), (2, "resting", "stand", {}, "sick / resting"),
        (0, "adult", "stand", {"pale": True}, "ill (pale)"), (5, "adult", "stand", {"cloth": 2, "skin": 1}, "woven"),
        (6, "adult", "stand", {"cloth": 3, "hair": 1}, "furs")]:
        add("People (7x11; civ sash)", lab, S.person(se, civ, kind, pose, **extra))
    for t in S.TOOLS:
        add("Tools and carried goods", t, S.tool(t))
    for c in S.CARRY:
        add("Tools and carried goods", "carry " + c, S.carry(c, se))
    names = {"boar": "wild boar", "sheep": "goat/sheep", "gazelle": "gazelle", "llama": "camel/llama",
             "rabbit": "small game"}
    for a in S.HERD_SPECIES:
        add("Herd animals", names.get(a, a), S.animal(a, se))
    add("Herd animals", "deer grazing", S.animal_grazing("deer", se))
    add("Herd animals", "bison grazing", S.animal_grazing("bison", se))
    add("Predators", "wolf", S.animal("wolf", se))
    add("Predators", "bear", S.animal("bear", se))
    for n in ["hearth", "lean_to", "hide_tent", "pit_house", "wattle_house", "mudbrick_house", "longhouse",
              "storage_pit", "drying_rack", "granary", "kiln", "quern", "thorn", "palisade", "lookout", "totem",
              "weir", "fence_h"]:
        lab = {"thorn": "thorn barrier", "totem": "shrine/totem", "fence_h": "pasture fence"}.get(n, n.replace("_", " "))
        add("Structures", lab, S.structure(n, se, 0))
    for f in range(4):
        hs, fs = S.structure("hearth", se), S.fire(f)
        px = np.full((hs.h + 4, max(hs.w, fs.w)), S.T, np.uint8)
        px[4:4 + hs.h, :hs.w][hs.px != S.T] = hs.px[hs.px != S.T]
        X0, Y0 = hs.ax - fs.ax, 4 + hs.ay - 2 - fs.ay
        px[Y0:Y0 + fs.h, X0:X0 + fs.w][fs.px != S.T] = fs.px[fs.px != S.T]
        add("Structures", f"fire frame {f + 1}", S.Spr(px))
    for civ in range(8):
        from palette import CIV_EMBLEMS, CIV_NAMES
        add("Civ banners (emblem shape + colour)", f"{CIV_NAMES[civ]} {CIV_EMBLEMS[civ]}", S.banner(civ))
    for i, sz in enumerate([3, 4, 5, 6]):
        add("Trees and terrain", f"broadleaf r{sz}", S.deciduous(11 + i, sz, se))
    add("Trees and terrain", "broadleaf dark", S.deciduous(17, 5, se, "leafdk"))
    for i, sz in enumerate([4, 5, 6, 7]):
        add("Trees and terrain", f"conifer {sz}", S.conifer(21 + i, sz, se))
    add("Trees and terrain", "acacia", S.acacia(3, 4, se))
    add("Trees and terrain", "palm", S.palm(3, 5, se))
    add("Trees and terrain", "bush", S.bush(3, 3, se))
    add("Trees and terrain", "rock", S.rock(3, 4, se))
    add("Trees and terrain", "reeds", S.reeds(3, se))
    add("Trees and terrain", "mountain", S.mountain(7, 22, se))
    add("Trees and terrain", "peak", S.mountain(9, 30, se, peak=True))
    for s in SEASONS:
        add("Seasons (palette role swaps)", f"broadleaf {s}", S.deciduous(13, 5, s))
    add("Seasons (palette role swaps)", "red maple autumn", S.deciduous(14, 5, "autumn", "leafalt"))
    add("Seasons (palette role swaps)", "blossom spring", S.deciduous(15, 5, "spring", "leafalt"))
    add("Seasons (palette role swaps)", "conifer winter", S.conifer(22, 6, "winter"))
    add("Seasons (palette role swaps)", "tent winter", S.structure("hide_tent", "winter", 0))
    add("Seasons (palette role swaps)", "wattle winter", S.structure("wattle_house", "winter", 0))
    add("Seasons (palette role swaps)", "mountain winter", S.mountain(7, 22, "winter"))
    if frame is not None:
        import render as R
        t = next(iter(t for t, k in frame.plots.items() if k == 0))
        cx, cy = frame.centre(t)
        for s in SEASONS:
            sc = R.Scene(frame, (cx, cy), (1.0, 1.0), 24, season=s, actors=False)
            sc.render()
            add("Fields by season (furrows + crop)", f"field {s}", S.Spr(sc.G.copy()))
    return sec


def text(canvas, s, x, y, colour, k):
    """Draw text with the 3x5 font, each font pixel k x k screen pixels."""
    for i, ch in enumerate(s.upper()):
        g = GLYPHS.get(ch, GLYPHS[" "])
        for j, bit in enumerate(g):
            if bit == "1":
                X, Y = x + (i * 4 + j % 3) * k, y + (j // 3) * k
                canvas[Y:Y + k, X:X + k] = colour


def render_sheet(path, scale=6, season="summer", frame=None, width=1800):
    secs = build(frame)
    k = 2                      # font pixel size
    pad = 14
    bg, panel, ink, sub = np.array(RGB[IDX["n2"]]), np.array(RGB[IDX["r4"]]), np.array(RGB[IDX["r0"]]), \
        np.array(RGB[IDX["r1"]])
    # layout pass
    placements = []
    y = pad
    for title, items in secs:
        placements.append(("title", title, pad, y))
        y += 5 * 3 + 12
        x, row_h = pad, 0
        for lab, sp in items:
            lw = (len(lab) * 4 - 1) * k
            cw = max(sp.w * scale, lw) + 18
            if x + cw > width - pad:
                x = pad
                y += row_h + 5 * k + 22
                row_h = 0
            placements.append(("spr", (lab, sp), x, y))
            x += cw
            row_h = max(row_h, sp.h * scale)
        y += row_h + 5 * k + 30
    # palette strip
    placements.append(("title", f"Palette: {len(WORLD)} world colours + 8x3 civ colours", pad, y))
    y += 27
    sw = 34
    cols = (width - 2 * pad) // sw
    for i, n in enumerate(NAMES):
        placements.append(("swatch", n, pad + (i % cols) * sw, y + (i // cols) * (sw + 14)))
    y += ((len(NAMES) + cols - 1) // cols) * (sw + 14) + pad
    H = y
    canvas = np.zeros((H, width, 3), np.uint8)
    canvas[:] = bg
    for kind, data, x, yy in placements:
        if kind == "title":
            text(canvas, data, x, yy, ink, 3)
            canvas[yy + 19:yy + 21, x:width - pad] = sub
        elif kind == "swatch":
            canvas[yy - 1:yy + sw - 3, x - 1:x + sw - 3] = sub
            canvas[yy:yy + sw - 4, x:x + sw - 4] = RGB[IDX[data]]
            text(canvas, data, x, yy + sw - 1, sub, 2)
        else:
            lab, sp = data
            w, h = sp.w * scale, sp.h * scale
            canvas[yy - 4:yy + h + 4, x - 4:x + w + 4] = panel
            rgba = to_rgba(sp.px)
            big = np.repeat(np.repeat(rgba, scale, 0), scale, 1)
            m = big[..., 3] > 0
            region = canvas[yy:yy + h, x:x + w]
            region[m] = big[..., :3][m]
            text(canvas, lab, x, yy + h + 9, sub, k)
    Image.fromarray(canvas, "RGB").save(path)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "sprite_sheet.png"
    fr = None
    if len(sys.argv) > 2:
        from world import Frame
        fr = Frame(sys.argv[2])
    render_sheet(out, 6, "summer", fr)
