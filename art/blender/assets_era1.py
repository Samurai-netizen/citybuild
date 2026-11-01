"""Era I (Early Middle Ages) models: timber, thatch and plaster. Names follow the content ids of `server/content`.

Every building fits inside one 1 x 1 tile (|x|, |y| <= 0.47), faces +Y, and carries anchors for the client:
Anchor_Status (status bubble, progress ring), Anchor_Door (workers walk here), Anchor_Smoke (chimney top) and, for
producers, Anchor_Stockpile (where the client shows prop_logs_* / prop_planks_* by stock).
"""
import math

from mathutils import Vector

from lib import Model, anchor, scale_model

# Buildings, the construction site and stockpiles are modelled at 1.0 and exported 1.2x larger, so a building fills
# most of its tile (about 85 %) like the reference game. Workers, tiles and nature keep scale 1.
SCALE = 1.2


# --- shared parts -------------------------------------------------------------------------------------------------

def timber_walls(m, w, d, h, z, cy=0.0, wall="plaster", beam="wood_dark", brace_front=None, brace_side=None):
    """Half-timbered walls: a plaster block with corner posts, top and bottom rails and optional diagonal braces."""
    m.box((w, d, h), (0, cy, z), wall, bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.044, 0.044, h + 0.004), (sx * w / 2, cy + sy * d / 2, z), beam, bevel=0.006)
    for zz in (z, z + h - 0.034):
        for sy in (-1, 1):
            m.box((w, 0.03, 0.034), (0, cy + sy * d / 2, zz), beam, bevel=0.004)
        for sx in (-1, 1):
            m.box((0.03, d, 0.034), (sx * w / 2, cy, zz), beam, bevel=0.004)
    if brace_front is not None:
        m.box((0.024, 0.026, h * 0.72), (brace_front, cy + d / 2, z + h / 2), beam, rot=(0, 38, 0), anchor="center")
    if brace_side is not None:
        m.box((0.026, 0.024, h * 0.72), (w / 2, cy + brace_side, z + h / 2), beam, rot=(38, 0, 0), anchor="center")


def window(m, pos, facing="Y", w=0.085, h=0.085, depth=0.0):
    """A dark pane with a frame, a mullion and a sill. `pos` = wall-plane point under the pane's centre bottom."""
    x, y, z = pos
    parts = (((w + 0.03, 0.034, h + 0.03), -0.015, "wood_dark"),
             ((w, 0.04, h), 0.0, "window"),
             ((0.012, 0.046, h), 0.0, "wood_dark"),
             ((w + 0.05, 0.05, 0.016), -0.03, "wood"))
    for (sx, sy, sz), dz, color in parts:
        size = (sx, sy + depth, sz) if facing == "Y" else (sy + depth, sx, sz)
        m.box(size, (x, y, z + dz), color, bevel=0.004)


def roof(m, length, width, height, at, color, band, rows=2, cap=None):
    """Gable roof with raised bands and a ridge cap. Returns the ridge height."""
    m.gable(length, width, height, at, color, bevel=0.018)
    m.roof_bands(length, width, height, at, band, rows=rows)
    ridge = at[2] + height
    m.box((length + 0.02, 0.07, 0.045), (at[0], at[1], ridge - 0.032), cap or band, bevel=0.014)
    return ridge


def slanted(m, p1, p2, width, thick, color, plane="XZ"):
    """A board from p1 to p2 in the X-Z or Y-Z plane (lean-to roofs, trestle legs)."""
    p1, p2 = Vector(p1), Vector(p2)
    d = p2 - p1
    if plane == "XZ":
        length = math.hypot(d.x, d.z)
        size, rot = (length, width, thick), (0, math.degrees(math.atan2(-d.z, d.x)), 0)
    else:
        length = math.hypot(d.y, d.z)
        size, rot = (width, length, thick), (math.degrees(math.atan2(d.z, d.y)), 0, 0)
    m.box(size, (p1 + p2) / 2, color, rot=rot, anchor="center", bevel=0.004)


# Stockpiles lie along X and stay within 0.09 of their centre across (Y), so every size fits the same anchor.
def log_pile(m, at, counts, r=0.03, length=0.26):
    """Logs along X stacked in rows (bottom row first) with light cut ends; `at` is the pile's centre on the ground."""
    x, y, z = at
    for row, n in enumerate(counts):
        for i in range(n):
            zc = z + r + row * r * 1.72
            m.cyl(r, length, (x, y + (i - (n - 1) / 2) * 2 * r, zc), "wood", axis="X", seg=7, cap="wood_end")


def log_cord(m, at, rows=3, r=0.03, length=0.26):
    """A big stockpile: logs stacked straight in a block of 3 across, held by four stakes."""
    x, y, z = at
    for row in range(rows):
        for i in range(3):
            m.cyl(r, length, (x, y + (i - 1) * 2 * r, z + r + row * 2 * r), "wood", axis="X", seg=7, cap="wood_end")
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.016, 0.016, rows * 2 * r + 0.03), (x + sx * length * 0.3, y + sy * (3 * r - 0.002), z),
                  "wood_dark", bevel=0)


def plank_stack(m, at, layers, length=0.26):
    """Sawn planks along X in crossed-over layers on two spacers; `at` is the stack's centre on the ground."""
    x, y, z = at
    for s in (-1, 1):
        m.box((0.03, 0.17, 0.02), (x + s * length * 0.32, y, z), "wood_dark", bevel=0.004)
    for layer in range(layers):
        shift = 0.007 if layer % 2 else -0.007
        for k in range(3):
            m.box((length, 0.05, 0.016), (x + shift, y + (k - 1) * 0.056 + shift, z + 0.02 + layer * 0.017),
                  "wood_end", bevel=0.004)


def barrel(m, at, r=0.045, h=0.11):
    x, y, z = at
    m.cyl(r, h, (x, y, z), "wood_dark", seg=10, cap="wood")
    for zz in (z + h * 0.18, z + h * 0.72):
        m.cyl(r + 0.004, 0.012, (x, y, zz), "metal", seg=10)


def crate(m, at, s=0.08, rot=0):
    m.box((s, s, s), at, "wood", rot=(0, 0, rot), bevel=0.008)
    m.box((s + 0.006, s + 0.006, 0.014), (at[0], at[1], at[2] + s * 0.43), "wood_dark", rot=(0, 0, rot), bevel=0.003)


def chimney(m, x, y, base, top):
    m.box((0.085, 0.085, top - base), (x, y, base), "stone", bevel=0.01)
    m.box((0.105, 0.105, 0.03), (x, y, top - 0.02), "stone_dark", bevel=0.008)


# --- buildings ----------------------------------------------------------------------------------------------------

def house(level):
    """Cottage: plaster and timber under thatch. Level 2 adds an overhanging upper floor and a flower box."""
    m = Model()
    W, D, H = 0.56, 0.44, 0.26
    m.box((W + 0.07, D + 0.07, 0.05), (0, 0, 0), "stone", bevel=0.015)
    z = 0.05
    timber_walls(m, W, D, H, z, brace_front=-0.2, brace_side=-0.12)
    m.box((0.12, 0.038, 0.17), (0.13, D / 2, z), "wood_dark", bevel=0.008)
    m.box((0.014, 0.05, 0.014), (0.09, D / 2, z + 0.085), "metal", bevel=0)
    window(m, (-0.05, D / 2, z + 0.1))
    window(m, (W / 2, 0.08, z + 0.1), facing="X")
    for i in range(2):
        m.box((0.09, 0.06, 0.012), (0.13 + (0.015 if i else 0), D / 2 + 0.065 + i * 0.07, 0), "stone", bevel=0.005)
    top, cy, w_top, d_top = z + H, 0.0, W, D
    if level == 2:
        jet = 0.035
        w_top, d_top, cy = W + 0.03, D + jet, jet / 2
        m.box((w_top + 0.04, d_top + 0.04, 0.03), (0, cy, top), "wood_dark", bevel=0.006)
        z2 = top + 0.03
        timber_walls(m, w_top, d_top, 0.22, z2, cy=cy, brace_front=0.21)
        window(m, (-0.13, cy + d_top / 2, z2 + 0.07))
        window(m, (0.06, cy + d_top / 2, z2 + 0.07))
        window(m, (w_top / 2, cy, z2 + 0.07), facing="X")
        top = z2 + 0.22
        m.box((0.13, 0.05, 0.035), (-0.05, D / 2 + 0.04, z + 0.03), "wood", bevel=0.005)
        for i, c in enumerate(("red", "gold", "red")):
            m.ico(0.019, (-0.09 + i * 0.04, D / 2 + 0.045, z + 0.078), c)
        barrel(m, (0.24, D / 2 + 0.1, 0), r=0.04, h=0.1)
    length, width = w_top + 0.14, d_top + 0.18
    ridge = roof(m, length, width, 0.27 if level == 1 else 0.3, (0, cy, top - 0.02), "thatch", "thatch_dark")
    cx, cyy = -W / 2 + 0.11, cy - 0.09
    chimney(m, cx, cyy, top - 0.05, ridge + 0.1)
    root = m.to_object(f"house_l{level}")
    anchor("Anchor_Status", root, (0, 0, ridge + 0.2))
    anchor("Anchor_Door", root, (0.13, D / 2 + 0.09, 0))
    anchor("Anchor_Smoke", root, (cx, cyy, ridge + 0.11))
    return root


def woodcutter(level):
    """Woodcutter's Hut: a log cabin under thatch, a chopping block with an axe, the log stockpile at the front right.
    Level 2 is taller and adds a thatched lean-to over the stockpile and a firewood stack on the side."""
    m = Model()
    W, D = (0.44, 0.34) if level == 1 else (0.46, 0.34)
    rows, r = (5, 0.023) if level == 1 else (6, 0.023)
    m.box((W + 0.05, D + 0.05, 0.03), (0, 0, 0), "stone_dark", bevel=0.01)
    z = 0.03
    H = rows * 2 * r
    m.box((W - 0.01, D - 0.01, H), (0, 0, z), "wood_dark", bevel=0)
    for i in range(rows):
        zc = z + r + i * 2 * r
        for sy in (-1, 1):
            m.cyl(r, W + 0.06, (0, sy * D / 2, zc), "wood", axis="X", seg=6, cap="wood_end")
        if i < rows - 1:
            for sx in (-1, 1):
                m.cyl(r, D + 0.05, (sx * W / 2, 0, zc + r), "wood", axis="Y", seg=6, cap="wood_end")
    door = -0.07
    m.box((0.11, 0.064, 0.16), (door, D / 2, z), "wood_dark", bevel=0.008)
    m.box((0.014, 0.075, 0.014), (door + 0.035, D / 2, z + 0.08), "metal", bevel=0)
    window(m, (W / 2, 0.02, z + 0.1), facing="X", depth=0.03)
    ridge = roof(m, W + 0.16, D + 0.2, 0.2 if level == 1 else 0.22, (0, 0, z + H - 0.012), "thatch", "thatch_dark")
    cx, cyy = -W / 2 + 0.09, -0.07
    chimney(m, cx, cyy, z + H - 0.05, ridge + 0.08)
    # chopping block with an axe at the front left
    bx, by = -0.27, D / 2 + 0.12
    m.cyl(0.045, 0.05, (bx, by, 0), "wood", seg=8, cap="wood_end")
    m.box((0.012, 0.012, 0.12), (bx + 0.012, by, 0.1), "wood", rot=(0, 25, 0), anchor="center", bevel=0)
    m.box((0.016, 0.045, 0.03), (bx + 0.035, by, 0.142), "metal", rot=(0, 25, 0), anchor="center", bevel=0.003)
    pile = (0.2, D / 2 + 0.115, 0.0)
    if level == 2:
        # lean-to over the stockpile, from the front wall down toward the street
        y0, z0, y1, z1 = D / 2 + 0.01, 0.285, 0.375, 0.235
        for sx in (0.052, 0.348):
            m.box((0.024, 0.024, z1), (sx, y1 - 0.02, 0), "wood_dark", bevel=0.004)
        slanted(m, (0.2, y0, z0), (0.2, y1, z1), 0.32, 0.022, "thatch", plane="YZ")
        slanted(m, (0.2, y0 + 0.02, z0 + 0.016), (0.2, y1, z1 + 0.016), 0.33, 0.01, "thatch_dark", plane="YZ")
        # firewood stacked along the +X wall
        for i, n in enumerate((2, 1)):
            for k in range(n):
                m.cyl(0.025, 0.24, (W / 2 + 0.06 + (k - (n - 1) / 2) * 0.05, -0.04, 0.025 + i * 0.043), "wood",
                      axis="Y", seg=7, cap="wood_end")
    root = m.to_object(f"woodcutter_l{level}")
    anchor("Anchor_Status", root, (0, 0, ridge + 0.2))
    anchor("Anchor_Door", root, (door, D / 2 + 0.09, 0))
    anchor("Anchor_Smoke", root, (cx, cyy, ridge + 0.09))
    anchor("Anchor_Stockpile", root, pile)
    return root


def _wheel(name, R, parent, location):
    """The Sawmill's water wheel, a separate part with its pivot on the axle (Blender X) so the client can turn it."""
    w = Model()
    w.cyl(0.04, 0.1, (0, 0, 0), "wood_dark", axis="X", seg=8, cap="wood")
    chord = 2 * R * math.sin(math.pi / 8) + 0.012
    rm = R * math.cos(math.pi / 8)
    for side in (-0.032, 0.032):
        for k in range(8):
            th = k * 45 + 22.5
            p = (side, rm * math.cos(math.radians(th)), rm * math.sin(math.radians(th)))
            w.box((0.016, 0.026, chord), p, "wood_dark", rot=(th, 0, 0), anchor="center", bevel=0.004)
        for k in range(4):
            w.box((0.014, 2 * rm, 0.016), (side, 0, 0), "wood_dark", rot=(k * 45, 0, 0), anchor="center", bevel=0)
    for k in range(8):
        th = k * 45
        rp = R - 0.012
        p = (0, rp * math.cos(math.radians(th)), rp * math.sin(math.radians(th)))
        w.box((0.095, 0.07, 0.016), p, "wood", rot=(th, 0, 0), anchor="center", bevel=0.004)
    return w.to_object(name, parent=parent, location=location)


def sawmill(level):
    """Sawmill: a plank shed with a shingle roof and a water wheel on the +X side, a trestle with a log and saw.
    Level 2 is taller, with a loft hatch and hoist beam, a chimney and a bigger wheel."""
    m = Model()
    W, D, H = (0.54, 0.38, 0.26) if level == 1 else (0.56, 0.4, 0.33)
    cx = -0.05
    m.box((W + 0.06, D + 0.06, 0.05), (cx, 0, 0), "stone", bevel=0.015)
    z = 0.05
    m.box((W, D, H), (cx, 0, z), "wood", bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.044, 0.044, H + 0.004), (cx + sx * W / 2, sy * D / 2, z), "wood_dark", bevel=0.006)
    ox, ow, oh = cx + 0.11, 0.18, 0.17
    m.box((ow, 0.03, oh), (ox, D / 2, z), "window", bevel=0.004)
    m.box((ow + 0.05, 0.04, 0.03), (ox, D / 2, z + oh), "wood_dark", bevel=0.005)
    wx = cx - 0.14
    window(m, (wx, D / 2, z + 0.11))
    n = 6
    for i in range(1, n):
        x = cx - W / 2 + i * W / n
        if abs(x - ox) < ow / 2 + 0.03 or abs(x - wx) < 0.08:
            continue
        m.box((0.018, 0.024, H - 0.01), (x, D / 2, z), "wood_dark", bevel=0)
    for j in (-1, 1):
        m.box((0.024, 0.018, H - 0.01), (cx + W / 2, j * D / 4, z), "wood_dark", bevel=0)
    if level == 2:
        hz = z + 0.215
        m.box((0.09, 0.034, 0.085), (wx, D / 2, hz), "wood_dark", bevel=0.006)
        m.box((0.026, 0.16, 0.026), (wx, D / 2 + 0.06, hz + 0.055), "wood_dark", bevel=0.004)
        m.box((0.006, 0.006, 0.07), (wx, D / 2 + 0.125, hz - 0.012), "thatch_dark", bevel=0)
        crate(m, (wx, D / 2 + 0.125, hz - 0.06), s=0.05)
    ridge = roof(m, W + 0.08, D + 0.17, 0.22 if level == 1 else 0.24, (cx, 0, z + H - 0.02), "wood_dark", "wood",
                 rows=3, cap="wood")
    if level == 2:
        chimney(m, cx - W / 2 + 0.1, -0.08, z + H - 0.05, ridge + 0.08)
    # mill race under the wheel
    R = 0.18 if level == 1 else 0.21
    ax, az = cx + W / 2 + 0.075, R + 0.02
    m.box((0.115, 0.5, 0.02), (ax, 0, 0), "water", bevel=0.004)
    for s in (-1, 1):
        m.box((0.025, 0.56, 0.04), (ax + s * 0.07, 0, 0), "stone_dark", bevel=0.008)
        m.box((0.115, 0.03, 0.04), (ax, s * 0.265, 0), "stone_dark", bevel=0.008)
    m.cyl(0.015, 0.1, (cx + W / 2 + 0.03, 0, az), "metal", axis="X", seg=6)
    # trestle with a log and a saw, front right
    tx, ty = 0.25, D / 2 + 0.16
    for sx in (-0.08, 0.08):
        for lean in (-1, 1):
            slanted(m, (tx + sx + lean * 0.03, ty, 0), (tx + sx, ty, 0.1), 0.018, 0.018, "wood_dark")
    m.cyl(0.03, 0.24, (tx, ty, 0.125), "wood", axis="X", seg=7, cap="wood_end")
    m.box((0.08, 0.005, 0.05), (tx + 0.01, ty, 0.13), "metal", bevel=0)
    root = m.to_object(f"sawmill_l{level}")
    _wheel("Wheel", R, root, (ax, 0, az))
    anchor("Anchor_Status", root, (cx, 0, ridge + 0.2))
    anchor("Anchor_Door", root, (ox, D / 2 + 0.09, 0))
    anchor("Anchor_Stockpile", root, (-0.2, D / 2 + 0.1, 0))
    if level == 2:
        anchor("Anchor_Smoke", root, (cx - W / 2 + 0.1, -0.08, ridge + 0.09))
    return root


def trading_post(level):
    """Trading Post: a timber shop with a striped awning, a counter with coins and goods, barrels and a flag.
    Level 2 is wider, with a longer awning, a second flag and a hand cart of planks."""
    m = Model()
    W, D, H = (0.5, 0.3, 0.28) if level == 1 else (0.56, 0.3, 0.3)
    cy = -0.13
    m.box((0.74 if level == 1 else 0.78, 0.7, 0.035), (0, 0, 0), "wood", bevel=0.01)
    z = 0.035
    timber_walls(m, W, D, H, z, cy=cy, brace_side=-0.07)
    front = cy + D / 2
    door = -0.1
    m.box((0.11, 0.038, 0.17), (door, front, z), "wood_dark", bevel=0.008)
    window(m, (0.12, front, z + 0.1))
    ridge = roof(m, W + 0.12, D + 0.16, 0.24, (0, cy, z + H - 0.02), "wood_dark", "wood", rows=2, cap="wood")
    # striped awning sloping down toward the street
    aw = W + 0.04
    stripes = 6 if level == 1 else 8
    y0, z0, y1, z1 = front + 0.01, z + H - 0.035, 0.25, z + 0.205
    a = math.atan2(z0 - z1, y1 - y0)
    length = math.hypot(y1 - y0, z0 - z1)
    for i in range(stripes):
        x = -aw / 2 + (i + 0.5) * aw / stripes
        c = "red" if i % 2 == 0 else "plaster"
        m.box((aw / stripes, length, 0.014), (x, (y0 + y1) / 2, (z0 + z1) / 2), c, rot=(-math.degrees(a), 0, 0),
              anchor="center", bevel=0)
        m.box((aw / stripes, 0.012, 0.045), (x, y1 + 0.002, z1 - 0.04), c, bevel=0)
    for sx in (-1, 1):
        m.box((0.028, 0.028, z1 - z), (sx * (aw / 2 - 0.02), y1 - 0.012, z), "wood_dark", bevel=0.004)
    # counter with goods, right of the door
    m.box((0.26, 0.08, 0.1), (0.1, 0.13, z), "wood", bevel=0.008)
    m.box((0.29, 0.1, 0.016), (0.1, 0.13, z + 0.1), "wood_dark", bevel=0.004)
    top = z + 0.116
    for i, dx in enumerate((0.0, 0.035)):
        m.cyl(0.02, 0.022 + 0.012 * i, (0.03 + dx, 0.13, top), "gold", seg=10)
    m.box((0.1, 0.05, 0.014), (0.15, 0.13, top), "wood_end", bevel=0.003)
    m.box((0.1, 0.05, 0.014), (0.155, 0.13, top + 0.014), "wood_end", rot=(0, 0, 8), bevel=0.003)
    m.ico(0.03, (0.215, 0.13, top + 0.025), "thatch_dark", scale=(1, 1, 1.15))
    # barrels, crates
    for bx, by in ((0.32, 0.22),) if level == 1 else ((0.34, 0.25), (0.34, 0.15)):
        barrel(m, (bx, by, z))
    kx, ky = (-0.3, 0.24) if level == 1 else (-0.33, 0.29)
    crate(m, (kx, ky, z))
    crate(m, (kx, ky, z + 0.08), s=0.06, rot=20)
    # gold coin sign on the +X wall
    m.box((0.05, 0.012, 0.012), (W / 2 + 0.03, cy + 0.03, z + 0.24), "wood_dark", bevel=0)
    m.cyl(0.045, 0.014, (W / 2 + 0.05, cy + 0.03, z + 0.19), "gold", axis="X", seg=12)
    # flag
    flags = [(W / 2 + 0.05, cy - 0.1, "blue")] + ([(-W / 2 - 0.05, cy - 0.1, "red")] if level == 2 else [])
    for fx, fy, c in flags:
        m.cyl(0.012, 0.72, (fx, fy, z), "wood_dark", seg=6)
        m.ico(0.02, (fx, fy, z + 0.73), "gold")
        side = -1 if fx > 0 else 1
        m.box((0.15, 0.008, 0.09), (fx + side * 0.08, fy, z + 0.58), c, bevel=0)
    if level == 2:
        # hand cart with planks, front left
        cxx, cyy = -0.23, 0.15
        m.box((0.14, 0.11, 0.04), (cxx, cyy, z + 0.06), "wood", bevel=0.006)
        for s in (-1, 1):
            m.cyl(0.042, 0.016, (cxx + 0.02, cyy + s * 0.065, z + 0.042), "wood_dark", axis="Y", seg=10, cap="wood")
            m.box((0.1, 0.012, 0.012), (cxx - 0.115, cyy + s * 0.045, z + 0.085), "wood_dark", rot=(0, -15, 0),
                  anchor="center", bevel=0)
        for k in range(3):
            m.box((0.14, 0.09, 0.014), (cxx, cyy, z + 0.1 + k * 0.015), "wood_end", rot=(0, 0, k * 4), bevel=0.003)
    root = m.to_object(f"trading_post_l{level}")
    anchor("Anchor_Status", root, (0, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (door, front + 0.1, z))
    return root


def construction_site():
    """Any building while it is being built or upgraded: foundation, timber frame, scaffold, ladder, planks."""
    m = Model()
    m.box((0.76, 0.76, 0.014), (0, 0, 0), "dirt", bevel=0.01)
    W, D = 0.54, 0.42
    g = 0.014
    for sy in (-1, 1):
        m.box((W + 0.05, 0.05, 0.05), (0, sy * D / 2, g), "stone", bevel=0.01)
    for sx in (-1, 1):
        m.box((0.05, D - 0.05, 0.05), (sx * W / 2, 0, g), "stone", bevel=0.01)
    z = g + 0.05
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.04, 0.04, 0.3), (sx * W / 2, sy * D / 2, z), "wood", bevel=0.005)
    m.box((W, 0.035, 0.035), (0, -D / 2, z + 0.265), "wood", bevel=0.004)
    m.box((0.035, D, 0.035), (-W / 2, 0, z + 0.265), "wood", bevel=0.004)
    m.box((0.035, D, 0.035), (W / 2, 0, z + 0.265), "wood", bevel=0.004)
    m.box((W - 0.04, 0.03, 0.14), (0, -D / 2, z), "plaster", bevel=0.004)
    m.box((0.03, 0.03, 0.3), (-0.1, -D / 2, z), "wood", bevel=0.004)
    m.box((0.024, 0.026, 0.3 * 0.75), (0.15, -D / 2 + 0.02, z + 0.15), "wood", rot=(0, 38, 0), anchor="center")
    # scaffold on the +X side
    for sy in (-0.18, 0.18):
        m.cyl(0.012, 0.44, (W / 2 + 0.07, sy, g), "wood_dark", seg=6)
    m.box((0.07, 0.46, 0.014), (W / 2 + 0.045, 0, z + 0.17), "wood_end", bevel=0.003)
    m.box((0.012, 0.42, 0.012), (W / 2 + 0.07, 0, z + 0.3), "wood_dark", bevel=0)
    # ladder against the front
    lx, length, th = 0.12, 0.33, 22
    centre = Vector((lx, D / 2 + 0.09, 0.153))
    up = Vector((0, -math.sin(math.radians(th)), math.cos(math.radians(th))))
    for s in (-1, 1):
        m.box((0.016, 0.016, length), centre + Vector((s * 0.04, 0, 0)), "wood", rot=(th, 0, 0), anchor="center", bevel=0)
    for t in (-0.6, -0.2, 0.2, 0.6):
        m.box((0.08, 0.012, 0.012), centre + up * (t * length / 2), "wood", bevel=0)
    plank_stack(m, (-0.2, D / 2 + 0.085, g), 2, length=0.24)
    m.box((0.06, 0.006, 0.04), (W / 2 + 0.035, D / 2, z + 0.25), "red", bevel=0)
    root = m.to_object("construction_site")
    anchor("Anchor_Status", root, (0, 0, 0.62))
    anchor("Anchor_Door", root, (0, D / 2 + 0.1, 0))
    return root


def logs_prop(name, counts=None):
    def build():
        m = Model()
        if counts:
            log_pile(m, (0, 0, 0), counts)
        else:
            log_cord(m, (0, 0, 0))
        return m.to_object(name)
    return build


def planks_prop(name, layers):
    def build():
        m = Model()
        plank_stack(m, (0, 0, 0), layers)
        return m.to_object(name)
    return build


def _scaled(build):
    return lambda: scale_model(build(), SCALE)


BUILDINGS = {name: _scaled(build) for name, build in {
    "house_l1": lambda: house(1), "house_l2": lambda: house(2),
    "woodcutter_l1": lambda: woodcutter(1), "woodcutter_l2": lambda: woodcutter(2),
    "sawmill_l1": lambda: sawmill(1), "sawmill_l2": lambda: sawmill(2),
    "trading_post_l1": lambda: trading_post(1), "trading_post_l2": lambda: trading_post(2),
    "construction_site": construction_site,
}.items()}

PROPS = {name: _scaled(build) for name, build in {
    "prop_logs_s": logs_prop("prop_logs_s", (2, 1)),
    "prop_logs_m": logs_prop("prop_logs_m", (3, 2, 1)),
    "prop_logs_l": logs_prop("prop_logs_l"),
    "prop_planks_s": planks_prop("prop_planks_s", 2),
    "prop_planks_m": planks_prop("prop_planks_m", 4),
    "prop_planks_l": planks_prop("prop_planks_l", 6),
}.items()}

# The stockpile prop each producer shows in previews (the client picks s / m / l by stock).
PREVIEW_STOCK = {"woodcutter": "prop_logs_m", "sawmill": "prop_planks_m"}
