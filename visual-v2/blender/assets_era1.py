"""Era I (Early Middle Ages) models, visual v2: timber, thatch, plaster and stone. Names follow the content ids of
`server/content` (`mvp-2` adds scriptorium, courier_station and warehouse).

Every building fits inside one 1 x 1 tile together with its full store and its moving parts in motion (|x|, |y| <= 0.475 after
SCALE), faces +Y, and carries anchors for the client: Anchor_Status (status bubble, progress ring), Anchor_Door (workers walk here),
Anchor_Smoke (chimney top, where the client loops fx_smoke_puff) and, for producers, Anchor_Stockpile (kept from v1). Moving parts
and their *_Pose empties are described in lib.py (MOTIONS). Store fill is a separate model per level, <type>_l<level>_fill.
"""
import math

from mathutils import Vector

import assets_common as ac
from lib import Model, anchor, group, scale_model

# Buildings, their fill, the construction site and the item props are modelled at 1.0 and exported 1.2x larger, so a building
# fills most of its tile (about 85 %). Workers, tiles and nature keep scale 1. In model space a building stays within +-0.395.
SCALE = 1.2
FIGURE = 1 / SCALE  # people drawn inside a building model come out as tall as the walking ones


# --- shared parts -------------------------------------------------------------------------------------------------

def stone_course(m, x0, x1, y, z, h, facing="Y", depth=0.012, size=0.075, seed=0):
    """A row of protruding stones on a wall face (a plinth's texture): from x0 to x1 along the face, `h` high, at z."""
    n = max(2, round((x1 - x0) / size))
    step = (x1 - x0) / n
    colors = ("stone_light", "stone", "stone_dark", "stone")
    for i in range(n):
        c = colors[(i + seed) % 4]
        cx = x0 + (i + 0.5) * step
        w = step - 0.008
        if facing == "Y":
            m.box((w, depth, h * (0.82 if i % 2 else 0.9)), (cx, y, z + 0.004), c, bevel=0.004)
        else:
            m.box((depth, w, h * (0.82 if i % 2 else 0.9)), (y, cx, z + 0.004), c, bevel=0.004)


def plinth(m, w, d, h=0.05, cx=0.0, cy=0.0):
    """A stone foundation with a course of stones on the front (+Y) and the seen side (+X)."""
    m.box((w, d, h), (cx, cy, 0), "stone", bevel=0.012)
    stone_course(m, cx - w / 2 + 0.01, cx + w / 2 - 0.01, cy + d / 2, 0, h - 0.008)
    stone_course(m, cy - d / 2 + 0.01, cy + d / 2 - 0.01, cx + w / 2, 0, h - 0.008, facing="X", seed=1)


def timber_walls(m, w, d, h, z, cx=0.0, cy=0.0, wall="plaster", beam="wood_dark", braces=True):
    """Half-timbered walls: a plaster block with corner posts, sill, mid and top rails, studs and braces on the seen faces."""
    m.box((w, d, h), (cx, cy, z), wall, bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.044, 0.044, h + 0.004), (cx + sx * w / 2, cy + sy * d / 2, z), beam, bevel=0.006)
    for zz in (z, z + h - 0.034):
        for sy in (-1, 1):
            m.box((w, 0.03, 0.034), (cx, cy + sy * d / 2, zz), beam, bevel=0.004)
        for sx in (-1, 1):
            m.box((0.03, d, 0.034), (cx + sx * w / 2, cy, zz), beam, bevel=0.004)
    m.box((w, 0.024, 0.022), (cx, cy + d / 2, z + h * 0.52), beam, bevel=0.004)  # mid rail, front
    m.box((0.024, d, 0.022), (cx + w / 2, cy, z + h * 0.52), beam, bevel=0.004)  # mid rail, side
    if braces:
        m.box((0.022, 0.026, h * 0.5), (cx - w / 2 + 0.07, cy + d / 2, z + h * 0.27), beam, rot=(0, 34, 0), anchor="center")
        m.box((0.026, 0.022, h * 0.5), (cx + w / 2, cy - d / 2 + 0.07, z + h * 0.27), beam, rot=(34, 0, 0), anchor="center")


def window(m, pos, facing="Y", w=0.085, h=0.085, shutters="blue", lit=False, box=None):
    """A pane with a frame, a cross mullion, a sill, two open shutters and optionally a flower box. `pos` = the wall-plane
    point under the pane's centre bottom."""
    x, y, z = pos

    def put(size, off, color, bevel=0.003):
        sx, sy, sz = size
        ox, oy, oz = off
        if facing == "Y":
            m.box((sx, sy, sz), (x + ox, y + oy, z + oz), color, bevel=bevel)
        else:
            m.box((sy, sx, sz), (x + oy, y + ox, z + oz), color, bevel=bevel)
    put((w + 0.03, 0.03, h + 0.03), (0, 0, -0.015), "wood_dark")
    put((w, 0.036, h), (0, 0, 0), "lamp" if lit else "window")
    put((0.01, 0.042, h), (0, 0, 0), "wood_dark", bevel=0)
    put((w, 0.042, 0.01), (0, 0, h / 2 - 0.005), "wood_dark", bevel=0)
    put((w + 0.05, 0.05, 0.014), (0, 0.006, -0.03), "wood")
    if shutters:
        for s in (-1, 1):
            put((w * 0.48, 0.012, h + 0.012), (s * (w * 0.74 + 0.012), 0.03, -0.006), shutters)
            put((w * 0.3, 0.014, 0.008), (s * (w * 0.74 + 0.012), 0.032, h * 0.25), "wood_dark", bevel=0)
    if box:
        put((w + 0.04, 0.045, 0.03), (0, 0.04, -0.06), "wood_dark")
        for i, c in enumerate(box):
            off = (-w / 2 + (i + 0.5) * w / len(box), 0.04, -0.028)
            if facing == "Y":
                m.ico(0.016, (x + off[0], y + off[1], z + off[2]), "leaf", scale=(1, 1, 0.8), sub=0)
                m.ico(0.011, (x + off[0], y + off[1] + 0.008, z + off[2] + 0.014), c, sub=0)
            else:
                m.ico(0.016, (x + off[1], y + off[0], z + off[2]), "leaf", scale=(1, 1, 0.8), sub=0)
                m.ico(0.011, (x + off[1] + 0.008, y + off[0], z + off[2] + 0.014), c, sub=0)


def door(m, pos, facing="Y", w=0.11, h=0.17, color="wood", step=True):
    """A plank door in a dark frame with iron hinges, a ring handle and a step stone. `pos` = the wall-plane point under its
    centre."""
    x, y, z = pos

    def put(size, off, c, bevel=0.004):
        sx, sy, sz = size
        ox, oy, oz = off
        if facing == "Y":
            m.box((sx, sy, sz), (x + ox, y + oy, z + oz), c, bevel=bevel)
        else:
            m.box((sy, sx, sz), (x + oy, y + ox, z + oz), c, bevel=bevel)
    put((w + 0.03, 0.03, h + 0.02), (0, 0, 0), "wood_dark")
    put((w, 0.04, h), (0, 0, 0), color)
    for k in (-1, 0, 1):
        put((0.006, 0.044, h), (k * w / 3.2, 0, 0), "wood_dark", bevel=0)
    for zz in (0.25, 0.75):
        put((w * 0.7, 0.046, 0.012), (-w * 0.12, 0, h * zz), "iron", bevel=0)
    put((0.014, 0.05, 0.014), (w * 0.32, 0, h * 0.48), "iron", bevel=0)
    if step:
        put((w + 0.04, 0.06, 0.02), (0, 0.05, -z), "stone_light", bevel=0.005)


def thatch_roof(m, length, width, height, at, rows=3, axis="X"):
    """A thatched gable: thick layers, a rounded ridge and a fringe along both eaves. Returns the ridge height."""
    m.gable(length, width, height, at, "thatch", axis=axis, bevel=0.022)
    for i in range(rows):
        t = (i + 0.55) / (rows + 0.5)
        for side in (1, -1):
            m.slope_strip(length + 0.016, width, height, at, side, t - 0.07, t + 0.07, "thatch_dark", lift=-0.002,
                          thick=0.016, axis=axis)
    for side in (1, -1):
        m.slope_strip(length + 0.022, width, height, at, side, -0.02, 0.05, "thatch_dark", lift=0.0, thick=0.02, axis=axis)
    ridge = at[2] + height
    m.cyl(0.027, length + 0.028, (at[0], at[1], ridge - 0.008), "thatch_dark", axis=axis, seg=7)
    return ridge


def shingle_roof(m, length, width, height, at, rows=4, axis="X", colors=("wood_dark", "wood")):
    """A gable of shingles: rows of separate shingles, each row a shade, every other row shifted by half a shingle (with half
    shingles at the ends), and a ridge board. Returns the ridge height."""
    m.gable(length, width, height, at, colors[0], axis=axis, bevel=0.016)
    n = max(4, round(length / 0.09))
    w = length / n
    for i in range(rows):
        t0, t1 = i / rows, (i + 1) / rows + 0.05
        edges = [k * w for k in range(n + 1)] if i % 2 == 0 else [0.0] + [(k + 0.5) * w for k in range(n)] + [length]
        for side in (1, -1):
            for a, b in zip(edges, edges[1:]):
                off = (a + b) / 2 - length / 2
                piece_at = (at[0] + (off if axis == "X" else 0), at[1] + (0 if axis == "X" else off), at[2])
                m.slope_strip(b - a - 0.008, width, height, piece_at, side, t0, t1, colors[i % 2], lift=0.002 + 0.004 * (i % 2),
                              thick=0.012, axis=axis)
    ridge = at[2] + height
    size = (length + 0.02, 0.06, 0.04) if axis == "X" else (0.06, length + 0.02, 0.04)
    m.box(size, (at[0], at[1], ridge - 0.028), "wood", bevel=0.012)
    return ridge


def chimney(m, x, y, base, top, w=0.085):
    """A chimney of stone courses with a cap and a clay pot."""
    z = base
    k = 0
    while z < top - 0.03:
        hh = min(0.045, top - 0.03 - z)
        m.box((w + (0.006 if k % 2 else 0), w + (0.006 if k % 2 else 0), hh), (x, y, z),
              ("stone", "stone_light")[k % 2], bevel=0.006)
        z += hh
        k += 1
    m.box((w + 0.026, w + 0.026, 0.026), (x, y, top - 0.03), "stone_dark", bevel=0.007)
    m.cyl(0.022, 0.03, (x, y, top - 0.006), "red", seg=8, r2=0.018)
    return top + 0.024


def barrel(m, at, r=0.045, h=0.11):
    x, y, z = at
    m.cyl(r, h, (x, y, z), "wood", seg=10, cap="wood_end")
    m.cyl(r * 0.98, h * 0.6, (x, y, z + h * 0.2), "wood", seg=10, r2=r * 0.98)
    for zz in (z + h * 0.16, z + h * 0.76):
        m.cyl(r + 0.004, 0.012, (x, y, zz), "iron", seg=10)
    m.cyl(r * 0.7, 0.004, (x, y, z + h), "wood_dark", seg=10)


def crate(m, at, s=0.08, rot=0):
    x, y, z = at
    m.box((s, s, s), at, "wood", rot=(0, 0, rot), bevel=0.008)
    m.box((s + 0.006, s + 0.006, 0.014), (x, y, z + s * 0.43), "wood_dark", rot=(0, 0, rot), bevel=0.003)
    m.box((s + 0.004, s + 0.004, 0.01), (x, y, z), "wood_dark", rot=(0, 0, rot), bevel=0.003)


def sack(m, at, color="canvas", s=1.0):
    x, y, z = at
    m.ico(0.04 * s, (x, y, z + 0.035 * s), color, scale=(1, 0.85, 0.95))
    m.cyl(0.014 * s, 0.022 * s, (x, y, z + 0.066 * s), color, seg=6, r2=0.02 * s)
    m.cyl(0.016 * s, 0.006 * s, (x, y, z + 0.068 * s), "thatch_dark", seg=6)


def lantern(m, at, h=0.2):
    """A lantern on a post, its glass lit."""
    x, y, z = at
    m.box((0.02, 0.02, h), (x, y, z), "wood_dark", bevel=0.004)
    m.box((0.06, 0.012, 0.012), (x + 0.025, y, z + h - 0.01), "wood_dark", bevel=0)
    m.cyl(0.002, 0.02, (x + 0.05, y, z + h - 0.03), "iron", seg=4)
    m.box((0.03, 0.03, 0.035), (x + 0.05, y, z + h - 0.068), "lamp", bevel=0.004)
    m.cyl(0.022, 0.016, (x + 0.05, y, z + h - 0.036), "iron", seg=4, r2=0.004, rot=(0, 0, 45))


def bench(m, at, length=0.16, rot=0):
    x, y, z = at
    m.box((length, 0.04, 0.012), (x, y, z + 0.04), "wood", rot=(0, 0, rot), bevel=0.003)
    for s in (-1, 1):
        dx, dy = s * (length / 2 - 0.02) * math.cos(math.radians(rot)), s * (length / 2 - 0.02) * math.sin(math.radians(rot))
        m.box((0.014, 0.036, 0.04), (x + dx, y + dy, z), "wood_dark", rot=(0, 0, rot), bevel=0.002)


def fence(m, p0, p1, posts=3, h=0.075):
    """A low wattle fence from p0 to p1 on the ground."""
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    length = d.length
    ang = math.degrees(math.atan2(d.y, d.x))
    for k in range(posts):
        p = p0 + d * (k / (posts - 1))
        m.box((0.016, 0.016, h + 0.012), (p.x, p.y, 0), "wood_dark", bevel=0.003)
    for zz in (h * 0.35, h * 0.8):
        mid = (p0 + p1) / 2
        m.box((length, 0.01, 0.012), (mid.x, mid.y, zz), "wood", rot=(0, 0, ang), bevel=0)


def figure(m, at, facing=0.0, tunic="blue", hat="straw"):
    """A seated-in villager body for a building model (no legs that move): at `at`, facing the angle `facing` (degrees from +Y
    toward -X). Drawn small by FIGURE, so it matches the walking workers after SCALE. Returns the shoulder point and the facing."""
    x, y, z = at
    k = FIGURE
    a = math.radians(facing)

    def p(dx, dy, dz):
        return (x + (dx * math.cos(a) - dy * math.sin(a)) * k, y + (dx * math.sin(a) + dy * math.cos(a)) * k, z + dz * k)
    for side in (-1, 1):
        m.box((0.024 * k, 0.028 * k, 0.05 * k), p(side * 0.018, 0, 0), "wood_dark", rot=(0, 0, facing), bevel=0.005 * k)
    m.box((0.072 * k, 0.05 * k, 0.03 * k), p(0, 0, 0.046), "wood_dark", rot=(0, 0, facing), bevel=0.008 * k)
    m.box((0.074 * k, 0.05 * k, 0.056 * k), p(0, 0, 0.064), tunic, rot=(0, 0, facing), bevel=0.012 * k)
    m.box((0.078 * k, 0.054 * k, 0.012 * k), p(0, 0, 0.068), "wood_dark", rot=(0, 0, facing), bevel=0.004 * k)
    m.ico(0.032 * k, p(0, 0, 0.138), "skin", sub=2)
    m.ico(0.03 * k, p(0, -0.006, 0.147), "wood_dark", scale=(1.02, 1.0, 0.75), sub=1)
    if hat == "straw":
        m.cyl(0.043 * k, 0.007 * k, p(0, 0, 0.154), "thatch", seg=12)
        m.cyl(0.025 * k, 0.022 * k, p(0, 0, 0.158), "thatch", seg=10, r2=0.021 * k)
    return Vector(p(0, 0, 0.104)), facing


def arms_with(part, facing, tool, target, tunic="blue"):
    """Two arms from the shoulders to the hands in front of the belly, holding a tool whose head is at `target`: a moving part
    drawn around the shoulder point, so `target` is relative to the shoulder. `tool` is "axe" or "hammer"."""
    k = FIGURE
    a = math.radians(facing)
    fwd = Vector((-math.sin(a), math.cos(a), 0))
    side = Vector((math.cos(a), math.sin(a), 0))
    hands = fwd * 0.05 * k + Vector((0, 0, -0.04 * k))
    for s in (-1, 1):
        sh = side * s * 0.044 * k
        mid = (sh + hands) / 2
        d = hands - sh
        pitch = math.degrees(math.atan2(-d.z, math.hypot(d.x, d.y)))
        yaw = math.degrees(math.atan2(d.y, d.x))
        part.box((d.length, 0.02 * k, 0.02 * k), tuple(mid), tunic, rot=(0, pitch, yaw), anchor="center", bevel=0.004 * k)
    part.ico(0.012 * k, tuple(hands), "skin", sub=1)
    tip = Vector(target)
    d = tip - hands
    pitch = math.degrees(math.atan2(-d.z, math.hypot(d.x, d.y)))
    yaw = math.degrees(math.atan2(d.y, d.x))
    part.box((d.length + 0.016, 0.011, 0.011), tuple((hands + tip) / 2), "wood", rot=(0, pitch, yaw), anchor="center", bevel=0)
    head = tip + Vector((0, 0, 0.012))
    if tool == "axe":
        part.box((0.016, 0.044, 0.036), tuple(head), "iron", rot=(0, 0, yaw + 90), anchor="center", bevel=0.004)
        part.box((0.008, 0.05, 0.03), tuple(head + d.normalized() * 0.01), "metal", rot=(0, 0, yaw + 90), anchor="center",
                 bevel=0)
    else:
        part.box((0.022, 0.046, 0.022), tuple(head), "iron", rot=(0, 0, yaw + 90), anchor="center", bevel=0.004)


# --- buildings ----------------------------------------------------------------------------------------------------

def house(level):
    """Cottage: plaster and timber under thatch on a stone plinth, shuttered windows, a garden with cabbages, a bench and a
    barrel. Level 2 adds an overhanging upper floor, a dormer, a woodpile and a lantern."""
    m = Model()
    W, D, H = 0.54, 0.4, 0.25
    cy = -0.04
    plinth(m, W + 0.07, D + 0.07, h=0.05, cy=cy)
    z = 0.05
    timber_walls(m, W, D, H, z, cy=cy)
    front = cy + D / 2
    door(m, (0.12, front, z), w=0.11, h=0.17)
    window(m, (-0.11, front, z + 0.095), shutters="blue", box=("red", "gold", "flower_pink"))
    window(m, (W / 2, cy + 0.06, z + 0.095), facing="X", shutters="blue")
    top, ccy, w_top, d_top = z + H, cy, W, D
    if level == 2:
        jet = 0.035
        w_top, d_top, ccy = W + 0.03, D + jet, cy + jet / 2
        m.box((w_top + 0.04, d_top + 0.04, 0.03), (0, ccy, top), "wood_dark", bevel=0.006)
        for k in range(5):  # joist ends under the jetty
            m.box((0.02, 0.03, 0.02), (-0.2 + k * 0.1, ccy + d_top / 2 + 0.012, top - 0.012), "wood_dark", bevel=0.003)
        z2 = top + 0.03
        timber_walls(m, w_top, d_top, 0.22, z2, cy=ccy)
        window(m, (-0.13, ccy + d_top / 2, z2 + 0.07), shutters="red")
        window(m, (0.08, ccy + d_top / 2, z2 + 0.07), shutters="red", box=("plaster", "flower_purple"))
        window(m, (w_top / 2, ccy, z2 + 0.07), facing="X", shutters="red")
        top = z2 + 0.22
    length, width = w_top + 0.12, d_top + 0.16
    rh = 0.26 if level == 1 else 0.29
    ridge = thatch_roof(m, length, width, rh, (0, ccy, top - 0.02))
    if level == 2:  # a small dormer on the front slope
        dz = top - 0.02 + rh * 0.38
        dy = ccy + width / 2 * (1 - 0.38) - 0.02
        m.box((0.11, 0.08, 0.08), (-0.1, dy, dz), "plaster", bevel=0.005)
        window(m, (-0.1, dy + 0.04, dz + 0.018), w=0.05, h=0.045, shutters=None)
        m.gable(0.15, 0.13, 0.06, (-0.1, dy, dz + 0.075), "thatch", axis="Y", bevel=0.01)
    cx, cyy = -W / 2 + 0.11, ccy - 0.09
    smoke = chimney(m, cx, cyy, top - 0.06, ridge + 0.1)
    # garden: dirt bed with cabbages on the front left, a wattle fence
    m.decal([(-0.37, front + 0.07), (-0.2, front + 0.07), (-0.2, front + 0.16), (-0.37, front + 0.16)], "dirt", z=0.003)
    for i in range(3):
        m.ico(0.022, (-0.345 + i * 0.06, front + 0.115, 0.016), ("leaf", "grass_light", "leaf")[i], scale=(1, 1, 0.75), sub=1)
    fence(m, (-0.385, front + 0.055), (-0.385, front + 0.175), posts=2)
    fence(m, (-0.385, front + 0.175), (-0.19, front + 0.175), posts=3)
    bench(m, (-0.02, front + 0.045, 0.0), length=0.13)
    barrel(m, (0.31, front + 0.03, 0), r=0.035, h=0.09)
    if level == 2:
        for i, n in enumerate((3, 2)):  # a woodpile against the side wall
            for k in range(n):
                ac.log(m, (W / 2 + 0.05, cy - 0.12 + (k - (n - 1) / 2) * 0.05 + 0.02, 0.024 + i * 0.042), 0.024, 0.13, axis="X",
                       seg=7)
        lantern(m, (0.25, front + 0.09, 0), h=0.2)
    root = m.to_object(f"house_l{level}")
    anchor("Anchor_Status", root, (0, ccy, ridge + 0.2))
    anchor("Anchor_Door", root, (0.12, front + 0.09, 0))
    anchor("Anchor_Smoke", root, (cx, cyy, smoke))
    return root


def log_cabin(m, W, D, rows, r, z, cy=0.0):
    """Log walls with crossed corners: logs along X on the front and back, along Y on the sides, alternating rows."""
    H = rows * 2 * r
    m.box((W - 0.01, D - 0.01, H), (0, cy, z), "wood_dark", bevel=0)
    for i in range(rows):
        zc = z + r + i * 2 * r
        for sy in (-1, 1):
            ac.log(m, (0, cy + sy * D / 2, zc), r, W + 0.06, axis="X", seg=6)
        if i < rows - 1:
            for sx in (-1, 1):
                ac.log(m, (sx * W / 2, cy, zc + r), r, D + 0.05, axis="Y", seg=6)
    return H


def woodcutter(level):
    """Woodcutter's Hut: a log cabin under thatch with a chimney, a chopper at the block (the moving part Axe), a saw-horse and
    a tool rack. The store (logs) is the fill model. Level 2 is taller with a lean-to over the store and a firewood rack."""
    m = Model()
    W, D = (0.42, 0.32) if level == 1 else (0.44, 0.32)
    rows, r = (5, 0.022) if level == 1 else (6, 0.022)
    cy = -0.07
    plinth(m, W + 0.05, D + 0.05, h=0.03, cy=cy)
    z = 0.03
    H = log_cabin(m, W, D, rows, r, z, cy=cy)
    front = cy + D / 2
    dx = -0.08
    door(m, (dx, front, z), w=0.1, h=0.15, color="wood_dark")
    window(m, (W / 2 + 0.01, cy + 0.02, z + 0.09), facing="X", shutters="red")
    ridge = thatch_roof(m, W + 0.14, D + 0.18, 0.2 if level == 1 else 0.22, (0, cy, z + H - 0.012))
    cx, cyy = -W / 2 + 0.09, cy - 0.07
    smoke = chimney(m, cx, cyy, z + H - 0.05, ridge + 0.08)
    # antlers over the door, a tool rack (saw, rake) on the side wall
    m.box((0.05, 0.014, 0.012), (dx, front + 0.012, z + 0.17), "wood_end", bevel=0)
    for s in (-1, 1):
        m.box((0.008, 0.008, 0.03), (dx + s * 0.024, front + 0.012, z + 0.176), "wood_end", rot=(0, s * 25, 0), bevel=0)
    m.box((0.012, 0.13, 0.1), (W / 2 + 0.02, cy - 0.09, z + 0.03), "metal", bevel=0)
    m.box((0.016, 0.135, 0.016), (W / 2 + 0.022, cy - 0.09, z + 0.13), "wood_dark", bevel=0)
    # chopping block with a log on it at the front left, and the chopper next to it
    bx, by = -0.25, front + 0.14
    m.cyl(0.05, 0.034, (bx, by, 0), "wood", seg=9, cap="wood_end")
    m.cyl(0.025, 0.003, (bx, by, 0.034), "wood", seg=9)
    m.cyl(0.024, 0.04, (bx + 0.004, by, 0.034), "wood", seg=7, cap="wood_end")
    for k in range(4):  # chips
        a = math.radians(k * 95 + 20)
        m.box((0.016, 0.01, 0.004), (bx + 0.075 * math.cos(a), by + 0.065 * math.sin(a), 0), "wood_end", rot=(0, 0, k * 40))
    fx, fy = bx + 0.12, by + 0.0
    shoulder, facing = figure(m, (fx, fy, 0), facing=90, tunic="blue")
    # the arms and the axe swing about the shoulders: at rest the axe is in the log, the pose has it raised overhead
    axe = m.part("Axe", tuple(shoulder), pose_rot=(0, 140, 0))
    arms_with(axe, facing, "axe", Vector((bx + 0.012, by, 0.078)) - shoulder)
    # saw-horse with a log, front middle
    sx, sy = 0.02, front + 0.16
    for ox in (-0.06, 0.06):
        for lean in (-1, 1):
            m.box((0.012, 0.012, 0.09), (sx + ox, sy + lean * 0.016, 0.042), "wood_dark", rot=(lean * 20, 0, 0),
                  anchor="center", bevel=0)
    ac.log(m, (sx, sy, 0.093), 0.024, 0.2, axis="X", seg=7)
    if level == 2:
        # a lean-to over the store, front right
        y0, z0, y1, z1 = front + 0.01, 0.27, 0.33, 0.21
        for px in (0.13, 0.34):
            m.box((0.022, 0.022, z1), (px, y1 - 0.02, 0), "wood_dark", bevel=0.004)
        m.box((0.24, abs(y1 - y0) + 0.04, 0.02), (0.235, (y0 + y1) / 2, (z0 + z1) / 2), "thatch",
              rot=(math.degrees(math.atan2(z0 - z1, y1 - y0)), 0, 0), anchor="center", bevel=0.006)
        m.box((0.25, 0.03, 0.026), (0.235, y1 + 0.005, z1 - 0.014), "thatch_dark", bevel=0.006)
        # a firewood rack along the -X wall's front corner
        for i, n in enumerate((3, 3, 2)):
            for k in range(n):
                ac.log(m, (-W / 2 - 0.045, cy - 0.04 + (k - (n - 1) / 2) * 0.048, 0.024 + i * 0.042), 0.023, 0.13, axis="X",
                       seg=6)
    root = m.to_object(f"woodcutter_l{level}")
    anchor("Anchor_Status", root, (0, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (dx, front + 0.09, 0))
    anchor("Anchor_Smoke", root, (cx, cyy, smoke))
    anchor("Anchor_Stockpile", root, (0.24, front + 0.12, 0))
    return root


def woodcutter_fill(level):
    """The Woodcutter's store in three steps, a row of logs each: 4, 3 and 2 logs (level 2: 5, 4, 3, under its lean-to)."""
    def build():
        root = group(f"woodcutter_l{level}_fill")
        front = -0.07 + 0.32 / 2
        x, y = 0.24, front + 0.12
        rows = (4, 3, 2) if level == 1 else (5, 4, 3)
        length = 0.19 if level == 1 else 0.2
        rr = 0.025 if level == 1 else 0.022
        for row, n in enumerate(rows):
            m = Model()
            for i in range(n):
                ac.log(m, (x, y + (i - (n - 1) / 2) * 2 * rr, rr + row * rr * 1.72), rr, length, axis="X", seg=7)
            m.to_object(f"Fill_logs_{row + 1}", parent=root)
        return root
    return build


def plank_walls(m, w, d, h, z, cx=0.0, cy=0.0, boards=7):
    """Walls of vertical boards: a wood block, corner posts, a sill and board seams on the front and the seen side."""
    m.box((w, d, h), (cx, cy, z), "wood", bevel=0.006)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.044, 0.044, h + 0.004), (cx + sx * w / 2, cy + sy * d / 2, z), "wood_dark", bevel=0.006)
    for sy in (-1, 1):
        m.box((w, 0.03, 0.03), (cx, cy + sy * d / 2, z), "wood_dark", bevel=0.004)
        m.box((w, 0.03, 0.03), (cx, cy + sy * d / 2, z + h - 0.03), "wood_dark", bevel=0.004)
    for sx in (-1, 1):
        m.box((0.03, d, 0.03), (cx + sx * w / 2, cy, z + h - 0.03), "wood_dark", bevel=0.004)
    return [cx - w / 2 + i * w / boards for i in range(1, boards)]


def water_wheel(part, R, width=0.1, paddles=10):
    """An undershot water wheel drawn around its axle (Blender X): hub, two rims of segments, spokes and paddles."""
    part.cyl(0.035, width + 0.02, (0, 0, 0), "wood_dark", axis="X", seg=8, cap="wood")
    part.cyl(0.012, width + 0.07, (0, 0, 0), "iron", axis="X", seg=6)
    n = paddles
    chord = 2 * R * math.sin(math.pi / n) + 0.012
    rm = R * math.cos(math.pi / n)
    for side in (-width / 2 + 0.008, width / 2 - 0.008):
        for k in range(n):
            th = (k + 0.5) * 360 / n
            p = (side, rm * math.cos(math.radians(th)), rm * math.sin(math.radians(th)))
            part.box((0.014, 0.024, chord), p, "wood_dark", rot=(th, 0, 0), anchor="center", bevel=0.004)
        for k in range(n // 2):
            part.box((0.012, 2 * rm - 0.02, 0.014), (side, 0, 0), "wood", rot=(k * 360 / n, 0, 0), anchor="center", bevel=0)
    for k in range(n):
        th = k * 360 / n
        rp = R - 0.014
        p = (0, rp * math.cos(math.radians(th)), rp * math.sin(math.radians(th)))
        part.box((width - 0.006, 0.066, 0.014), p, "wood", rot=(th, 0, 0), anchor="center", bevel=0.004)


def sawmill(level):
    """Sawmill: a board shed with a shingle roof; a water wheel (moving part Wheel) on the +X side in a stone mill race; in the
    open front bay a frame saw (moving part Saw) cuts a log; a sawdust heap. The store is the fill model (planks out front,
    logs waiting by the bay). Level 2 is taller, with a loft hoist, a chimney and a bigger wheel."""
    m = Model()
    W, D, H = (0.5, 0.36, 0.25) if level == 1 else (0.52, 0.38, 0.32)
    cx, cy = -0.07, -0.08
    plinth(m, W + 0.06, D + 0.06, h=0.045, cx=cx, cy=cy)
    z = 0.045
    seams = plank_walls(m, W, D, H, z, cx=cx, cy=cy)
    front = cy + D / 2
    # the open bay, front right, with its lintel and the saw frame inside
    ox, ow, oh = cx + 0.1, 0.17, 0.18
    m.box((ow, 0.03, oh), (ox, front, z), "window", bevel=0.003)
    m.box((ow + 0.06, 0.04, 0.032), (ox, front, z + oh), "wood_dark", bevel=0.005)
    for s_ in (-1, 1):
        m.box((0.03, 0.04, oh), (ox + s_ * (ow / 2 + 0.012), front, z), "wood_dark", bevel=0.004)
    wx = cx - 0.13
    window(m, (wx, front, z + 0.1), shutters="red")
    for x in seams:
        if abs(x - ox) < ow / 2 + 0.04 or abs(x - wx) < 0.09:
            continue
        m.box((0.01, 0.034, H - 0.06), (x, front, z + 0.03), "wood_dark", bevel=0)
    for j in (-1, 0, 1):
        m.box((0.034, 0.01, H - 0.06), (cx + W / 2, cy + j * D / 4, z + 0.03), "wood_dark", bevel=0)
    # the log through the bay on a carriage, and the frame saw that moves up and down across it
    m.box((0.1, 0.24, 0.02), (ox, front + 0.04, 0.045), "wood_dark", bevel=0.004)
    ac.log(m, (ox, front + 0.06, 0.09), 0.026, 0.2, axis="Y", seg=7)
    for k in range(2):
        m.box((0.08, 0.17 - k * 0.04, 0.012), (ox + 0.06 + k * 0.006, front + 0.03, 0.065 + k * 0.012), "wood_end", bevel=0.003)
    for s_ in (-1, 1):  # the guide posts the frame runs in
        m.box((0.02, 0.02, 0.26), (ox + s_ * 0.075, front + 0.05, z), "wood_dark", bevel=0.004)
    m.box((0.18, 0.024, 0.02), (ox, front + 0.05, z + 0.26), "wood_dark", bevel=0.004)
    saw = m.part("Saw", (ox, front + 0.05, z + 0.08), pose_offset=(0, 0, 0.05))
    for s_ in (-1, 1):
        saw.box((0.016, 0.016, 0.15), (s_ * 0.055, 0, -0.05), "wood_end", bevel=0.003)
    for zz in (-0.05, 0.085):
        saw.box((0.13, 0.02, 0.018), (0, 0, zz), "wood_end", bevel=0.004)
    saw.box((0.008, 0.008, 0.14), (0, 0, -0.05), "metal", bevel=0)
    m.ico(0.05, (ox + 0.11, front + 0.13, 0.02), "wood_end", scale=(1.1, 1.0, 0.45), sub=1)  # sawdust
    rh = 0.22 if level == 1 else 0.24
    ridge = shingle_roof(m, W + 0.08, D + 0.15, rh, (cx, cy, z + H - 0.02), rows=3)
    smoke = None
    if level == 2:
        hz = z + 0.22
        m.box((0.09, 0.034, 0.085), (wx, front, hz), "wood_dark", bevel=0.006)
        m.box((0.026, 0.16, 0.026), (wx, front + 0.06, hz + 0.055), "wood_dark", bevel=0.004)
        m.box((0.006, 0.006, 0.07), (wx, front + 0.125, hz - 0.012), "thatch_dark", bevel=0)
        crate(m, (wx, front + 0.125, hz - 0.06), s=0.05)
        smoke = chimney(m, cx - W / 2 + 0.1, cy - 0.06, z + H - 0.05, ridge + 0.08)
    # the mill race along the +X side, its water and the foam at the wheel; a flume would be Era II
    R = 0.17 if level == 1 else 0.2
    ax, az = cx + W / 2 + 0.075, R + 0.04
    y0, y1 = -0.39, 0.2
    m.box((0.11, y1 - y0, 0.016), (ax, (y0 + y1) / 2, 0), "water", bevel=0.004)
    for k in range(3):
        m.decal([(-0.03, -0.012), (0.03, -0.012), (0.03, 0.012), (-0.03, 0.012)], "foam", z=0.0172,
                at=(ax + (k - 1) * 0.025, 0.05 + 0.03 * k))
    for s_ in (-1, 1):
        m.box((0.026, y1 - y0, 0.04), (ax + s_ * 0.068, (y0 + y1) / 2, 0), "stone_dark", bevel=0.008)
    m.box((0.16, 0.03, 0.04), (ax, y1, 0), "stone_dark", bevel=0.008)
    m.cyl(0.015, 0.09, (cx + W / 2 + 0.03, 0, az), "iron", axis="X", seg=6)
    for s_ in (-1, 1):  # bearing posts
        m.box((0.024, 0.03, az), (ax + s_ * 0.068, 0, 0), "wood_dark", bevel=0.004)
    wheel = m.part("Wheel", (ax, 0, az), pose_rot=(-90, 0, 0))
    water_wheel(wheel, R, width=0.09, paddles=10 if level == 1 else 12)
    root = m.to_object(f"sawmill_l{level}")
    anchor("Anchor_Status", root, (cx, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (ox, front + 0.1, 0))
    anchor("Anchor_Stockpile", root, (-0.24, front + 0.14, 0))
    if smoke:
        anchor("Anchor_Smoke", root, (cx - W / 2 + 0.1, cy - 0.06, smoke))
    return root


def sawmill_fill(level):
    """The Sawmill's store: planks out front in three steps (two layers each) and the logs waiting by the bay in two steps."""
    def build():
        root = group(f"sawmill_l{level}_fill")
        D = 0.36 if level == 1 else 0.38
        front = -0.08 + D / 2
        px, py = -0.25, front + 0.14
        for step in range(3):
            m = Model()
            if step == 0:
                for sx in (-1, 1):
                    m.box((0.03, 0.17, 0.02), (px + sx * 0.08, py, 0), "wood_dark", bevel=0.004)
            for layer in range(2):
                lv = step * 2 + layer
                shift = 0.006 if lv % 2 else -0.006
                for k in range(3):
                    m.box((0.24, 0.05, 0.016), (px + shift, py + (k - 1) * 0.056 + shift, 0.02 + lv * 0.017),
                          "wood_end" if (lv + k) % 3 else "wood", bevel=0.004)
            m.to_object(f"Fill_planks_{step + 1}", parent=root)
        lx, ly = 0.07, front + 0.15
        for step, n in enumerate((3, 2)):
            m = Model()
            r = 0.022
            for i in range(n):
                ac.log(m, (lx, ly + (i - (n - 1) / 2) * 2 * r, r + step * r * 1.72), r, 0.15, axis="X", seg=7)
            m.to_object(f"Fill_logs_{step + 1}", parent=root)
        return root
    return build


def striped_awning(m, x0, x1, y0, z0, y1, z1, stripes):
    """A striped cloth awning from the wall (y0, z0) down to its front edge (y1, z1), with a scalloped valance."""
    a = math.atan2(z0 - z1, y1 - y0)
    length = math.hypot(y1 - y0, z0 - z1)
    w = (x1 - x0) / stripes
    for i in range(stripes):
        x = x0 + (i + 0.5) * w
        c = "red" if i % 2 == 0 else "plaster"
        m.box((w, length, 0.012), (x, (y0 + y1) / 2, (z0 + z1) / 2), c, rot=(-math.degrees(a), 0, 0), anchor="center",
              bevel=0)
        m.prism([(-w / 2, 0), (w / 2, 0), (0, -0.04)], 0.008, (x, y1 + 0.003, z1 - 0.004), c, axis="Y")


def trading_post(level):
    """Trading Post: a timber shop on a plank deck with a striped awning, a counter with coins, scales and goods, a spinning coin
    sign (moving part Sign) and a flag (moving part Flag; level 2 adds Flag_2), barrels and crates. The store (planks for sale) is
    the fill model. Level 2 is wider, with a longer awning, a hand cart and a lantern."""
    m = Model()
    W, D, H = (0.48, 0.28, 0.27) if level == 1 else (0.54, 0.28, 0.29)
    cy = -0.15
    deck = 0.72 if level == 1 else 0.76
    m.box((deck, 0.7, 0.032), (0, -0.02, 0), "wood", bevel=0.008)
    for k in range(9):
        m.box((deck - 0.01, 0.006, 0.004), (0, -0.33 + k * 0.078, 0.031), "wood_dark", bevel=0)
    z = 0.032
    timber_walls(m, W, D, H, z, cy=cy)
    front = cy + D / 2
    dx = -0.11
    door(m, (dx, front, z), w=0.1, h=0.17, color="red", step=False)
    window(m, (0.11, front, z + 0.1), shutters="gold")
    window(m, (W / 2, cy, z + 0.1), facing="X", shutters="gold")
    ridge = shingle_roof(m, W + 0.1, D + 0.15, 0.22, (0, cy, z + H - 0.02), rows=3)
    aw = W + 0.04
    striped_awning(m, -aw / 2, aw / 2, front + 0.01, z + H - 0.03, 0.22, z + 0.235, 7 if level == 1 else 9)
    for sx in (-1, 1):
        m.box((0.026, 0.026, 0.235), (sx * (aw / 2 - 0.02), 0.21, z), "wood_dark", bevel=0.004)
    # the counter, right of the door: coins, a balance, a ledger and a sack
    m.box((0.26, 0.08, 0.1), (0.1, 0.1, z), "wood", bevel=0.008)
    m.box((0.29, 0.1, 0.016), (0.1, 0.1, z + 0.1), "wood_dark", bevel=0.004)
    top = z + 0.116
    ac.coin_stack(m, (0.0, 0.1, top), 3, r=0.018)
    ac.coin_stack(m, (0.035, 0.11, top), 2, r=0.018)
    m.box((0.006, 0.006, 0.07), (0.1, 0.1, top), "iron", bevel=0)
    m.box((0.09, 0.006, 0.006), (0.1, 0.1, top + 0.066), "iron", bevel=0)
    for s_ in (-1, 1):
        m.cyl(0.018, 0.006, (0.1 + s_ * 0.04, 0.1, top + 0.03), "gold", seg=10)
        m.box((0.002, 0.002, 0.034), (0.1 + s_ * 0.04, 0.1, top + 0.034), "iron", bevel=0)
    m.box((0.05, 0.036, 0.012), (0.17, 0.1, top), "red", bevel=0.002)
    m.box((0.046, 0.032, 0.006), (0.172, 0.1, top + 0.012), "plaster", bevel=0)
    sack(m, (0.22, 0.12, top - 0.002), s=0.75)
    # the coin sign on a bracket of the +X wall; it spins about its hanging point
    m.box((0.07, 0.012, 0.012), (W / 2 + 0.035, cy + 0.05, z + 0.25), "wood_dark", bevel=0)
    m.box((0.004, 0.004, 0.02), (W / 2 + 0.06, cy + 0.05, z + 0.232), "iron", bevel=0)
    sign = m.part("Sign", (W / 2 + 0.06, cy + 0.05, z + 0.19), pose_rot=(0, 0, 90))
    sign.cyl(0.042, 0.012, (0, 0, 0), "thatch_dark", axis="X", seg=14, cap="gold")
    sign.cyl(0.026, 0.016, (0, 0, 0), "thatch_dark", axis="X", seg=10, cap="gold")
    # flags at the back corners
    flags = [("Flag", W / 2 + 0.04, cy - 0.1, "blue")] + ([("Flag_2", -W / 2 - 0.04, cy - 0.1, "red")] if level == 2 else [])
    for name, fx, fy, c in flags:
        m.cyl(0.012, 0.68, (fx, fy, z), "wood_dark", seg=6)
        m.ico(0.02, (fx, fy, z + 0.69), "gold")
        side = -1 if fx > 0 else 1
        flag = m.part(name, (fx, fy, z + 0.6), pose_rot=(0, 0, 22))
        flag.prism([(0, 0), (side * 0.15, 0.0), (side * 0.12, -0.045), (side * 0.15, -0.09), (0, -0.09)], 0.008,
                   (0, 0, 0), c, axis="Y")
        flag.cyl(0.012, 0.012, (side * 0.045, 0, -0.045), "gold", axis="Y", seg=8)
    # barrels and crates
    for bx, by in ((0.31, 0.24),) if level == 1 else ((0.33, 0.26), (0.33, 0.16)):
        barrel(m, (bx, by, z))
    kx, ky = (-0.29, 0.25) if level == 1 else (-0.31, 0.27)
    crate(m, (kx, ky, z))
    crate(m, (kx + 0.004, ky, z + 0.08), s=0.06, rot=20)
    if level == 2:
        cxx, cyy = 0.3, -0.05
        m.box((0.1, 0.13, 0.04), (cxx, cyy, z + 0.055), "wood", bevel=0.006)
        for s_ in (-1, 1):
            m.cyl(0.04, 0.014, (cxx + s_ * 0.06, cyy + 0.02, z + 0.04), "wood_dark", axis="X", seg=10, cap="wood")
        m.box((0.012, 0.11, 0.012), (cxx - 0.04, cyy - 0.11, z + 0.08), "wood_dark", rot=(15, 0, 0), anchor="center", bevel=0)
        m.box((0.012, 0.11, 0.012), (cxx + 0.04, cyy - 0.11, z + 0.08), "wood_dark", rot=(15, 0, 0), anchor="center", bevel=0)
        for k in range(2):
            sack(m, (cxx - 0.02 + 0.04 * k, cyy + 0.01, z + 0.095), s=0.7)
        lantern(m, (-W / 2 + 0.02, front + 0.06, z), h=0.21)
    root = m.to_object(f"trading_post_l{level}")
    anchor("Anchor_Status", root, (0, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (dx, front + 0.1, z))
    return root


def trading_post_fill(level):
    """The Trading Post's store, the planks it sells, in two steps: a stack by the steps, then a second stack beside it."""
    def build():
        root = group(f"trading_post_l{level}_fill")
        z = 0.032
        x0 = 0.3 if level == 1 else 0.335
        for step in range(2):
            m = Model()
            x = x0 - step * 0.115
            y = 0.06 if level == 1 else -0.0
            for lv in range(3):
                shift = 0.005 if lv % 2 else -0.005
                for k in range(2):
                    m.box((0.05, 0.17, 0.016), (x + (k - 0.5) * 0.054 + shift, y + shift, z + lv * 0.017),
                          "wood_end" if (lv + k) % 2 else "wood", bevel=0.004)
            m.to_object(f"Fill_planks_{step + 1}", parent=root)
        return root
    return build


def construction_site():
    """Any building while it is built or upgraded: a stone foundation, a timber frame with part of its walls, scaffolding with a
    ladder, a pulley with a bucket, planks and stones, and a builder hammering (moving part Hammer)."""
    m = Model()
    m.box((0.76, 0.76, 0.012), (0, 0, 0), "dirt", bevel=0.008)
    m.decal([(-0.3, -0.2), (0.1, -0.3), (0.32, -0.05), (0.2, 0.25)], "dirt_dark", z=0.0135)
    W, D = 0.52, 0.4
    g = 0.012
    for sy in (-1, 1):
        m.box((W + 0.05, 0.05, 0.05), (0, sy * D / 2, g), "stone", bevel=0.01)
        if sy > 0:
            stone_course(m, -W / 2, W / 2, sy * D / 2 + 0.026, g, 0.042)
    for sx in (-1, 1):
        m.box((0.05, D - 0.05, 0.05), (sx * W / 2, 0, g), "stone", bevel=0.01)
    z = g + 0.05
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.box((0.04, 0.04, 0.3), (sx * W / 2, sy * D / 2, z), "wood", bevel=0.005)
    m.box((W, 0.035, 0.035), (0, -D / 2, z + 0.265), "wood", bevel=0.004)
    m.box((0.035, D, 0.035), (-W / 2, 0, z + 0.265), "wood", bevel=0.004)
    m.box((0.035, D, 0.035), (W / 2, 0, z + 0.265), "wood", bevel=0.004)
    m.box((W - 0.04, 0.03, 0.15), (0, -D / 2, z), "plaster", bevel=0.004)
    m.box((0.03, D - 0.04, 0.09), (-W / 2, 0, z), "plaster", bevel=0.004)
    m.box((0.03, 0.03, 0.3), (-0.1, -D / 2, z), "wood", bevel=0.004)
    m.box((0.024, 0.026, 0.22), (0.15, -D / 2 + 0.02, z + 0.15), "wood", rot=(0, 38, 0), anchor="center")
    for k in range(4):  # rafters waiting on top
        m.box((0.022, D + 0.06, 0.022), (-0.18 + k * 0.12, 0, z + 0.3), "wood_end", bevel=0.003)
    # scaffold on the +X side with a plank deck, a ladder against it
    for sy in (-0.18, 0.18):
        m.cyl(0.012, 0.44, (W / 2 + 0.07, sy, g), "wood_dark", seg=6)
    m.box((0.08, 0.46, 0.014), (W / 2 + 0.05, 0, z + 0.17), "wood_end", bevel=0.003)
    m.box((0.012, 0.42, 0.012), (W / 2 + 0.085, 0, z + 0.3), "wood_dark", bevel=0)
    for s_ in (-1, 1):
        m.box((0.012, 0.012, 0.22), (W / 2 + 0.085, s_ * 0.09, z + 0.07), "wood_dark", rot=(s_ * 40, 0, 0), anchor="center",
              bevel=0)
    lx, length, th = 0.12, 0.33, 22
    centre = Vector((lx, D / 2 + 0.075, 0.153))
    up = Vector((0, -math.sin(math.radians(th)), math.cos(math.radians(th))))
    for s_ in (-1, 1):
        m.box((0.016, 0.016, length), centre + Vector((s_ * 0.04, 0, 0)), "wood", rot=(th, 0, 0), anchor="center", bevel=0)
    for t in (-0.6, -0.2, 0.2, 0.6):
        m.box((0.08, 0.012, 0.012), centre + up * (t * length / 2), "wood", bevel=0)
    # a pulley on a pole with a bucket, front left
    m.box((0.02, 0.02, 0.4), (-0.3, D / 2 + 0.08, 0), "wood_dark", bevel=0.004)
    m.box((0.1, 0.016, 0.016), (-0.26, D / 2 + 0.08, 0.39), "wood_dark", bevel=0)
    m.cyl(0.018, 0.012, (-0.22, D / 2 + 0.08, 0.37), "iron", axis="Y", seg=8)
    m.box((0.003, 0.003, 0.2), (-0.205, D / 2 + 0.08, 0.17), "thatch_dark", bevel=0)
    m.cyl(0.024, 0.04, (-0.205, D / 2 + 0.08, 0.13), "wood", seg=8, r2=0.03, cap="stone")
    ac.plank_stack(m, (-0.08, D / 2 + 0.095, g), 2, length=0.24)
    for k in range(3):
        m.box((0.05, 0.04, 0.035), (-0.32 + k * 0.03, -D / 2 + 0.02 + (k % 2) * 0.045, g), ("stone", "stone_light")[k % 2],
              bevel=0.008)
    m.box((0.06, 0.006, 0.04), (W / 2 + 0.035, D / 2, z + 0.25), "red", bevel=0)
    # the builder on the scaffold deck hammering at the frame
    fx, fy = W / 2 + 0.05, 0.06
    shoulder, facing = figure(m, (fx, fy, z + 0.184), facing=-90, tunic="red", hat="straw")
    hammer = m.part("Hammer", tuple(shoulder), pose_rot=(0, -95, 0))
    arms_with(hammer, facing, "hammer", Vector((W / 2 + 0.012, fy, shoulder.z - 0.02)) - shoulder, tunic="red")
    root = m.to_object("construction_site")
    anchor("Anchor_Status", root, (0, 0, 0.62))
    anchor("Anchor_Door", root, (0, D / 2 + 0.1, 0))
    return root


def ring(part, r, at, color, axis="Z", n=10, thick=0.012):
    """A thin ring of n short boxes around `at`, in the plane normal to `axis` (an armillary's hoop)."""
    chord = 2 * r * math.sin(math.pi / n) + 0.004
    for k in range(n):
        th = k * 360 / n
        c, s_ = math.cos(math.radians(th)), math.sin(math.radians(th))
        if axis == "Z":
            p, rot = (at[0] + r * c, at[1] + r * s_, at[2]), (0, 0, th + 90)
            size = (chord, thick, thick)
        elif axis == "X":
            p, rot = (at[0], at[1] + r * c, at[2] + r * s_), (th + 90, 0, 0)
            size = (thick, chord, thick)
        else:
            p, rot = (at[0] + r * c, at[1], at[2] + r * s_), (0, -(th + 90), 0)
            size = (chord, thick, thick)
        part.box(size, p, color, rot=rot, anchor="center", bevel=0)


def scriptorium():
    """Scriptorium (the science building, CR-0008): a stone hall under red shingles with lit windows, a round tower with a
    conical roof crowned by an armillary sphere that turns while a research runs (moving part Vane), a sundial and a crate of
    scrolls. One level in M0."""
    m = Model()
    W, D, H = 0.42, 0.3, 0.2
    cx, cy = -0.06, -0.08
    plinth(m, W + 0.06, D + 0.06, h=0.04, cx=cx, cy=cy)
    z = 0.04
    m.box((W, D, 0.1), (cx, cy, z), "stone", bevel=0.008)
    stone_course(m, cx - W / 2 + 0.02, cx + W / 2 - 0.02, cy + D / 2, z + 0.02, 0.05, seed=2)
    stone_course(m, cy - D / 2 + 0.02, cy + D / 2 - 0.02, cx + W / 2, z + 0.02, 0.05, facing="X", seed=3)
    timber_walls(m, W, D, H - 0.1, z + 0.1, cx=cx, cy=cy, braces=False)
    front = cy + D / 2
    # an arched door of stone and a tall lit window
    dxp = cx - 0.1
    m.prism([(-0.065, 0), (0.065, 0), (0.065, 0.16), (0.04, 0.2), (0, 0.212), (-0.04, 0.2), (-0.065, 0.16)], 0.03,
            (dxp, front + 0.004, z), "stone_light", axis="Y")
    m.prism([(-0.048, 0), (0.048, 0), (0.048, 0.15), (0.028, 0.18), (0, 0.19), (-0.028, 0.18), (-0.048, 0.15)], 0.036,
            (dxp, front + 0.008, z), "wood_dark", axis="Y")
    m.box((0.012, 0.042, 0.012), (dxp + 0.03, front + 0.008, z + 0.085), "iron", bevel=0)
    m.box((0.14, 0.06, 0.02), (dxp, front + 0.04, 0), "stone_light", bevel=0.005)
    window(m, (cx + 0.07, front, z + 0.11), w=0.08, h=0.09, shutters="red", lit=True)
    window(m, (cx + W / 2, cy - 0.05, z + 0.11), facing="X", w=0.08, h=0.09, shutters="red", lit=True)
    ridge = shingle_roof(m, W + 0.1, D + 0.15, 0.24, (cx, cy, z + H - 0.02), rows=3, colors=("red", "red"))
    smoke = chimney(m, cx - W / 2 + 0.08, cy - 0.06, z + H - 0.05, ridge + 0.07)
    # the round tower at the front right corner (+X, +Y)
    tx, ty, tr = cx + W / 2 + 0.02, front + 0.0, 0.1
    m.cyl(tr + 0.012, 0.05, (tx, ty, 0), "stone_dark", seg=10)
    m.cyl(tr, 0.42, (tx, ty, 0.05), "stone", seg=10)
    for k in range(3):  # stone bands
        m.cyl(tr + 0.006, 0.018, (tx, ty, 0.12 + k * 0.12), ("stone_light", "stone_dark")[k % 2], seg=10)
    for zz, a in ((0.16, 60), (0.32, 20)):
        wx_, wy_ = tx + tr * math.cos(math.radians(a)), ty + tr * math.sin(math.radians(a))
        m.box((0.035, 0.035, 0.06), (wx_, wy_, zz), "lamp", rot=(0, 0, a), bevel=0.004)
        m.box((0.045, 0.03, 0.07), (wx_ - 0.004 * math.cos(math.radians(a)), wy_ - 0.004 * math.sin(math.radians(a)), zz - 0.005),
              "wood_dark", rot=(0, 0, a), bevel=0.004)
    m.cyl(tr + 0.03, 0.03, (tx, ty, 0.47), "wood_dark", seg=10)
    m.cyl(tr + 0.04, 0.16, (tx, ty, 0.5), "red", seg=10, r2=0.012)
    m.cyl(0.006, 0.1, (tx, ty, 0.64), "iron", seg=4)
    vane = m.part("Vane", (tx, ty, 0.72), pose_rot=(0, 0, 90))
    ring(vane, 0.045, (0, 0, 0), "gold", axis="Z", n=9)
    ring(vane, 0.045, (0, 0, 0), "gold", axis="X", n=9)
    ring(vane, 0.035, (0, 0, 0), "metal", axis="Y", n=8, thick=0.009)
    vane.ico(0.014, (0, 0, 0), "blue", sub=1)
    # a sundial on a pedestal and a crate of scrolls in the front yard
    sx_, sy_ = cx - 0.25, front + 0.14
    m.cyl(0.03, 0.08, (sx_, sy_, 0), "stone_light", seg=8)
    m.cyl(0.05, 0.014, (sx_, sy_, 0.08), "stone", seg=10)
    m.prism([(0, 0), (0.04, 0), (0, 0.035)], 0.006, (sx_ - 0.02, sy_, 0.094), "iron", axis="Y")
    crate(m, (0.12, front + 0.15, 0), s=0.075, rot=10)
    for k in range(3):
        m.cyl(0.011, 0.07, (0.11 + k * 0.016, front + 0.15, 0.085 + 0.004 * (k % 2)), "plaster", axis="X", seg=6, cap="wood_end")
    root = m.to_object("scriptorium_l1")
    anchor("Anchor_Status", root, (cx, cy, max(ridge, 0.8) + 0.12))
    anchor("Anchor_Door", root, (dxp, front + 0.1, 0))
    anchor("Anchor_Smoke", root, (cx - W / 2 + 0.08, cy - 0.06, smoke))
    return root


def signpost(m, at):
    """A signpost with three arrow boards pointing different ways."""
    x, y, z = at
    m.box((0.022, 0.022, 0.26), (x, y, z), "wood_dark", bevel=0.004)
    for k, (zz, a, c) in enumerate(((0.23, 20, "wood_end"), (0.19, 140, "wood"), (0.15, 260, "wood_end"))):
        dx, dy = 0.04 * math.cos(math.radians(a)), 0.04 * math.sin(math.radians(a))
        m.prism([(-0.045, -0.012), (0.035, -0.012), (0.05, 0), (0.035, 0.012), (-0.045, 0.012)], 0.008,
                (x + dx, y + dy, z + zz), c, axis="Z", rot=(90, 0, a))


def courier_station(level):
    """Courier Station (CR-0008): a timber lodge with a wide open door, a signpost, a notice board with route sheets, parcels by
    the door, a bench and a pennant that waves while its couriers work (moving part Flag). Level 2 adds a loft with a hoist, a
    second pennant (Flag_2) and a hand cart of parcels."""
    m = Model()
    W, D, H = (0.46, 0.3, 0.22) if level == 1 else (0.5, 0.32, 0.22)
    cx, cy = -0.04, -0.09
    plinth(m, W + 0.06, D + 0.06, h=0.04, cx=cx, cy=cy)
    z = 0.04
    timber_walls(m, W, D, H, z, cx=cx, cy=cy, wall="wood_end")
    front = cy + D / 2
    # the wide open door with the parcels' shelves inside
    ox, ow, oh = cx - 0.06, 0.16, 0.17
    m.box((ow, 0.03, oh), (ox, front, z), "window", bevel=0.003)
    m.box((ow + 0.05, 0.04, 0.03), (ox, front, z + oh), "wood_dark", bevel=0.005)
    for s_ in (-1, 1):
        m.box((0.016, 0.06, oh), (ox + s_ * (ow / 2 + 0.03), front + 0.03, z), "wood", rot=(0, 0, s_ * 30), bevel=0.003)
    m.box((ow - 0.02, 0.012, 0.012), (ox, front - 0.004, z + 0.09), "wood", bevel=0)
    for k in range(3):
        m.box((0.03, 0.02, 0.025), (ox - 0.05 + k * 0.05, front - 0.004, z + 0.102), ("canvas", "wood", "red")[k], bevel=0.003)
    window(m, (cx + 0.15, front, z + 0.1), shutters="red")
    window(m, (cx + W / 2, cy, z + 0.1), facing="X", shutters="red")
    # the notice board with route sheets on the +X wall
    m.box((0.012, 0.13, 0.09), (cx + W / 2 + 0.008, cy + 0.07, z + 0.06), "wood_dark", bevel=0.003)
    for k, (dy, dz) in enumerate(((-0.03, 0.06), (0.02, 0.07), (0.035, 0.1), (-0.02, 0.11))):
        m.box((0.004, 0.035, 0.03), (cx + W / 2 + 0.016, cy + 0.07 + dy, z + dz), ("plaster", "lamp", "plaster", "canvas")[k],
              bevel=0)
    top = z + H
    if level == 2:  # a loft under a jettied gable with a hoist
        m.box((W + 0.03, D + 0.03, 0.03), (cx, cy, top), "wood_dark", bevel=0.006)
        timber_walls(m, W, D, 0.16, top + 0.03, cx=cx, cy=cy, wall="wood_end", braces=False)
        m.box((0.08, 0.034, 0.08), (cx + 0.12, front + 0.006, top + 0.06), "wood_dark", bevel=0.005)
        m.box((0.022, 0.15, 0.022), (cx + 0.12, front + 0.06, top + 0.14), "wood_dark", bevel=0.004)
        m.box((0.004, 0.004, 0.06), (cx + 0.12, front + 0.125, top + 0.08), "thatch_dark", bevel=0)
        sack(m, (cx + 0.12, front + 0.125, top + 0.02), s=0.65)
        top += 0.19
    ridge = shingle_roof(m, W + 0.1, D + 0.16, 0.2, (cx, cy, top - 0.02), rows=3)
    smoke = chimney(m, cx - W / 2 + 0.09, cy - 0.05, top - 0.05, ridge + 0.07)
    # the pennant pole at the front left corner and, at level 2, a second one at the right
    poles = [("Flag", cx - W / 2 - 0.035, front + 0.03, "red")]
    if level == 2:
        poles.append(("Flag_2", cx + W / 2 + 0.035, cy - D / 2 - 0.02, "blue"))
    for name, fx, fy, c in poles:
        m.cyl(0.011, 0.6, (fx, fy, 0), "wood_dark", seg=6)
        m.ico(0.018, (fx, fy, 0.61), "gold")
        flag = m.part(name, (fx, fy, 0.54), pose_rot=(0, 0, -24))
        flag.prism([(0, 0), (0.13, -0.025), (0, -0.07)], 0.008, (0, 0, 0), c, axis="X")
    signpost(m, (cx + W / 2 + 0.07, front + 0.12, 0))
    # parcels by the door, a bench
    crate(m, (ox - 0.13, front + 0.08, 0), s=0.07)
    crate(m, (ox - 0.125, front + 0.08, 0.07), s=0.055, rot=15)
    sack(m, (ox - 0.2, front + 0.12, 0))
    sack(m, (ox + 0.12, front + 0.1, 0), color="thatch_dark", s=0.9)
    bench(m, (cx + 0.16, front + 0.06, 0), length=0.14)
    if level == 2:
        hx, hy = cx + 0.02, front + 0.17
        m.box((0.13, 0.09, 0.035), (hx, hy, 0.045), "wood", bevel=0.006)
        for s_ in (-1, 1):
            m.cyl(0.035, 0.012, (hx - 0.02, hy + s_ * 0.052, 0.035), "wood_dark", axis="Y", seg=10, cap="wood")
        m.box((0.1, 0.01, 0.01), (hx + 0.1, hy - 0.035, 0.06), "wood_dark", rot=(0, 12, 0), anchor="center", bevel=0)
        m.box((0.1, 0.01, 0.01), (hx + 0.1, hy + 0.035, 0.06), "wood_dark", rot=(0, 12, 0), anchor="center", bevel=0)
        crate(m, (hx - 0.025, hy, 0.08), s=0.05)
        sack(m, (hx + 0.03, hy, 0.08), color="canvas", s=0.7)
    root = m.to_object(f"courier_station_l{level}")
    anchor("Anchor_Status", root, (cx, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (ox, front + 0.1, 0))
    anchor("Anchor_Smoke", root, (cx - W / 2 + 0.09, cy - 0.05, smoke))
    return root


def warehouse(level):
    """Warehouse (CR-0008): a plank barn with braced double doors under a shingle roof, a hoist over the loft door whose crate
    goes up and down while goods come and go (moving part Hoist), and an open yard in front where the store shows (the fill
    model: log racks and plank stacks). Level 2 is longer, with a lean-to along its side."""
    m = Model()
    W, D, H = (0.46, 0.3, 0.26) if level == 1 else (0.52, 0.3, 0.3)
    cx, cy = -0.02, -0.15
    plinth(m, W + 0.05, D + 0.05, h=0.035, cx=cx, cy=cy)
    z = 0.035
    seams = plank_walls(m, W, D, H, z, cx=cx, cy=cy, boards=8)
    front = cy + D / 2
    for x in seams:
        if abs(x - cx) < 0.11:
            continue
        m.box((0.01, 0.034, H - 0.06), (x, front, z + 0.03), "wood_dark", bevel=0)
    # the double door with Z braces
    for s_ in (-1, 1):
        dxp = cx + s_ * 0.05
        m.box((0.095, 0.036, 0.19), (dxp, front, z), "wood_dark" if s_ < 0 else "wood", bevel=0.004)
        for zz in (0.03, 0.15):
            m.box((0.09, 0.042, 0.016), (dxp, front, z + zz), "wood_end", bevel=0.002)
        m.box((0.016, 0.042, 0.14), (dxp, front, z + 0.1), "wood_end", rot=(0, s_ * 38, 0), anchor="center", bevel=0)
    m.box((0.22, 0.04, 0.026), (cx, front, z + 0.19), "wood_dark", bevel=0.004)
    side = cx + W / 2
    for k in range(1, 6):  # board seams, a rail and a small window on the seen side
        y_ = cy - D / 2 + k * D / 6
        if abs(y_ - cy) > 0.05:
            m.box((0.034, 0.01, H - 0.06), (side, y_, z + 0.03), "wood_dark", bevel=0)
    m.box((0.03, D, 0.022), (side + 0.004, cy, z + 0.12), "wood_dark", bevel=0.003)
    window(m, (side, cy, z + 0.155), facing="X", w=0.07, h=0.06, shutters=None)
    for k in range(2):
        m.box((0.03, 0.05, 0.05), (side + 0.04, cy - 0.06 + k * 0.012, 0.0 + k * 0.05), "wood", rot=(0, 0, 10 * k),
              bevel=0.006)
    # the hoist beam and the crate that rides it
    rh = 0.24 if level == 1 else 0.26
    ridge = shingle_roof(m, W + 0.08, D + 0.15, rh, (cx, cy, z + H - 0.02), rows=3)
    m.box((0.026, 0.19, 0.026), (cx, front + 0.06, z + H + 0.03), "wood_dark", bevel=0.004)
    m.cyl(0.014, 0.03, (cx, front + 0.14, z + H + 0.005), "iron", axis="X", seg=8)
    hoist = m.part("Hoist", (cx, front + 0.14, z + H - 0.08), pose_offset=(0, 0, 0.1))
    hoist.box((0.004, 0.004, 0.08), (0, 0, 0.0), "thatch_dark", bevel=0)
    crate(hoist, (0, 0, -0.06), s=0.06)
    # sacks and a barrel by the door
    sack(m, (cx + 0.15, front + 0.05, 0))
    barrel(m, (cx - 0.16, front + 0.05, 0), r=0.035, h=0.09)
    if level == 2:  # a lean-to along the -X side for more racks
        for k in range(3):
            m.box((0.02, 0.02, 0.19), (cx - W / 2 - 0.1, cy - 0.12 + k * 0.12, 0), "wood_dark", bevel=0.004)
        m.box((0.13, D + 0.06, 0.016), (cx - W / 2 - 0.05, cy, 0.205), "wood", rot=(0, -14, 0), anchor="center", bevel=0.004)
    root = m.to_object(f"warehouse_l{level}")
    anchor("Anchor_Status", root, (cx, cy, ridge + 0.2))
    anchor("Anchor_Door", root, (cx, front + 0.1, 0))
    return root


def warehouse_fill(level):
    """The Warehouse's store in its yard: logs on a rack at the right (+X) in three rows, planks at the left in three double
    layers (level 2: longer logs and wider stacks)."""
    def build():
        root = group(f"warehouse_l{level}_fill")
        front = -0.15 + 0.3 / 2
        lx, ly = 0.24, front + 0.17
        n = 4 if level == 1 else 5
        r = 0.023
        for row in range(3):
            m = Model()
            if row == 0:
                for s_ in (-1, 1):
                    m.box((0.02, 0.02, 0.14), (lx + s_ * 0.085, ly + (n * r + 0.012), 0), "wood_dark", bevel=0)
                    m.box((0.02, 0.02, 0.14), (lx + s_ * 0.085, ly - (n * r + 0.012), 0), "wood_dark", bevel=0)
            for i in range(n - row % 2):
                ac.log(m, (lx, ly + (i - (n - row % 2 - 1) / 2) * 2 * r, r + row * r * 1.75), r, 0.22, axis="X", seg=7)
            m.to_object(f"Fill_logs_{row + 1}", parent=root)
        px, py = -0.21 if level == 1 else -0.23, front + 0.17
        width = 3 if level == 1 else 4
        for step in range(3):
            m = Model()
            if step == 0:
                for s_ in (-1, 1):
                    m.box((0.03, 0.06 * width, 0.02), (px + s_ * 0.075, py, 0), "wood_dark", bevel=0.004)
            for layer in range(2):
                lv = step * 2 + layer
                shift = 0.005 if lv % 2 else -0.005
                for k in range(width):
                    m.box((0.22, 0.05, 0.016), (px + shift, py + (k - (width - 1) / 2) * 0.056 + shift, 0.02 + lv * 0.017),
                          "wood_end" if (lv + k) % 3 else "wood", bevel=0.004)
            m.to_object(f"Fill_planks_{step + 1}", parent=root)
        return root
    return build


BUILDINGS_RAW = {
    "house_l1": lambda: house(1), "house_l2": lambda: house(2),
    "woodcutter_l1": lambda: woodcutter(1), "woodcutter_l2": lambda: woodcutter(2),
    "sawmill_l1": lambda: sawmill(1), "sawmill_l2": lambda: sawmill(2),
    "trading_post_l1": lambda: trading_post(1), "trading_post_l2": lambda: trading_post(2),
    "construction_site": construction_site,
    "scriptorium_l1": scriptorium,
    "courier_station_l1": lambda: courier_station(1), "courier_station_l2": lambda: courier_station(2),
    "warehouse_l1": lambda: warehouse(1), "warehouse_l2": lambda: warehouse(2),
}

FILLS_RAW = {
    "woodcutter_l1_fill": woodcutter_fill(1), "woodcutter_l2_fill": woodcutter_fill(2),
    "sawmill_l1_fill": sawmill_fill(1), "sawmill_l2_fill": sawmill_fill(2),
    "trading_post_l1_fill": trading_post_fill(1), "trading_post_l2_fill": trading_post_fill(2),
    "warehouse_l1_fill": warehouse_fill(1), "warehouse_l2_fill": warehouse_fill(2),
}

PROPS_RAW = {
    "prop_logs_s": ac.logs_prop("prop_logs_s", (2, 1)),
    "prop_logs_m": ac.logs_prop("prop_logs_m", (3, 2, 1)),
    "prop_logs_l": ac.logs_prop("prop_logs_l", (4, 3, 2)),
    "prop_planks_s": ac.planks_prop("prop_planks_s", 2),
    "prop_planks_m": ac.planks_prop("prop_planks_m", 4),
    "prop_planks_l": ac.planks_prop("prop_planks_l", 6),
    "prop_coins_s": ac.coins_prop("prop_coins_s", [((0, 0), 3), ((0.07, 0.02), 2)]),
    "prop_coins_m": ac.coins_prop("prop_coins_m", [((0, 0), 5), ((0.08, 0.02), 3), ((0.03, -0.07), 4)], pouch=True),
    "prop_coins_l": ac.coins_prop("prop_coins_l", [((0.11, 0.06), 6), ((0.15, -0.02), 4)], pouch=True, chest=True),
}


def _scaled(build):
    return lambda: scale_model(build(), SCALE)


BUILDINGS = {name: _scaled(build) for name, build in BUILDINGS_RAW.items()}
FILLS = {name: _scaled(build) for name, build in FILLS_RAW.items()}
PROPS = {name: _scaled(build) for name, build in PROPS_RAW.items()}
