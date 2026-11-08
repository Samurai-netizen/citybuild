"""The ground of visual v2, second pass (2026-11-04): grass tiles, the dirt tile and road tiles with more detail and more variety.

Variety comes in three layers, all picked by fixed hashes so every device shows the same town:
- eight grass tiles, each with its own seed and features (GroundVariants.For in the client, ground_variant in render_previews.py);
- a quarter turn per grass tile (GroundVariants.RotationFor; the tiles are square and their details have no direction);
- three variants of each road level (RoadArms.VariantFor), with the same arm widths at the tile edges so any two of them join.
Details keep inside the bevel that draws the grid line (EDGE) and tall ones keep out of the middle (CLEAR), which a foundation covers.
A grass or dirt tile is two meshes: the flat ground (top at z = 0, the one the client tints for placement hints) and its child `Details`.
Grid directions and arm names are in assets_common's docstring.
"""
import math
import random

from lib import Model, blob

TILE_SLAB = (1.0, 1.0, 0.1)
EDGE = 0.44   # small details stay this far from the tile centre, inside the bevel that draws the grid line
CLEAR = 0.24  # details taller than 0.02 stay outside this centre square
ARMS = {"Road_XPlus": (-1, 0), "Road_XMinus": (1, 0), "Road_YPlus": (0, -1), "Road_YMinus": (0, 1)}
ROAD_HALF = 0.17  # half the width of a dirt path arm at the tile edge, where neighbouring arms meet
LOG_HALF = 0.19   # half the width of a log road's bed at the tile edge
ROAD_VARIANTS = ("", "_b", "_c")
DETAILS = "Details"  # the child mesh of a ground tile that holds what rises above its flat top


# --- shared bits ------------------------------------------------------------------------------------------------------

def slab(m, color="grass"):
    """The tile block: 1 x 1, 0.1 deep, top face at z = 0; the bevel draws the grid lines between neighbours."""
    m.box(TILE_SLAB, (0, 0, -0.1), color, bevel=0.025)


def spot(rng, keep_out=0.0, edge=EDGE):
    """A random point on the tile, inside `edge` and outside a centre square of half-size keep_out."""
    while True:
        x, y = rng.uniform(-edge + 0.03, edge - 0.03), rng.uniform(-edge + 0.03, edge - 0.03)
        if max(abs(x), abs(y)) >= keep_out:
            return x, y


INSIDE = 0.465  # flat patches stop here, before the bevel, so they never paint over the grid line between two tiles


def patch(m, outline, color, z, at):
    """A flat patch at `at`, its outline pulled in to stay inside the tile's flat top."""
    pts = [(max(-INSIDE, min(INSIDE, at[0] + u)) - at[0], max(-INSIDE, min(INSIDE, at[1] + v)) - at[1]) for u, v in outline]
    m.decal(pts, color, z=z, at=at)


def mottle(m, rng, count, colors=("grass_warm", "grass_cool"), r=(0.08, 0.17), z=0.0011, keep=None):
    """Soft patches in colours close to the grass, all over the tile: the ground reads painted instead of flat."""
    for k in range(count):
        x, y = spot(rng) if keep is None else keep(rng)
        patch(m, blob(rng, rng.uniform(*r), n=10, wobble=0.28, stretch=(1.0, rng.uniform(0.55, 0.95)), turn=rng.uniform(0, 180)),
              colors[k % len(colors)], z + 0.0003 * k, (x, y))


def tuft(m, rng, at, h, colors=("grass_dark", "leaf", "grass_light"), blades=None):
    """A tuft of 4 to 7 blades in mixed greens, the middle ones taller, leaning outward at random."""
    x, y = at
    n = blades or rng.randint(4, 7)
    for k in range(n):
        a = rng.uniform(0, 2 * math.pi)
        off = 0.0 if k == 0 else rng.uniform(0.006, 0.016)
        hh = h * (1.0 if k == 0 else rng.uniform(0.55, 0.9))
        lean = 0 if k == 0 else rng.uniform(10, 24)
        m.blade((x + off * math.cos(a), y + off * math.sin(a), 0), hh, colors[k % len(colors)], r=rng.uniform(0.013, 0.019),
                lean=(lean * math.sin(a) * -1, lean * math.cos(a)))


def pebble(m, at, r, color="stone", turn=0):
    m.ico(r, (at[0], at[1], r * 0.25), color, scale=(1.0, 0.8, 0.55), sub=0, rot=(0, 0, turn))


def flower(m, at, petal, h=0.04):
    """A small flower: a stem with a leaf and a head of petals around a gold centre."""
    x, y, z = at
    m.blade((x, y, z), h, "grass_dark", r=0.007)
    m.ico(0.009, (x + 0.008, y, z + h * 0.35), "leaf", scale=(1.4, 0.7, 0.35), sub=0)
    m.ico(0.017, (x, y, z + h), petal, scale=(1, 1, 0.55), sub=0)
    m.ico(0.0075, (x, y, z + h + 0.006), "gold", sub=0)


def daisy(m, rng, at):
    """A daisy: a white disc of petals with a gold heart on a short stem."""
    x, y = at
    h = rng.uniform(0.03, 0.044)
    m.blade((x, y, 0), h, "grass_dark", r=0.008)
    m.ico(0.022, (x, y, h), "plaster", scale=(1, 1, 0.28), sub=0, rot=(0, 0, rng.uniform(0, 60)))
    m.ico(0.009, (x, y, h + 0.005), "gold", sub=0)


def poppy(m, rng, at):
    """A red poppy with a dark heart, a little taller than the daisies."""
    x, y = at
    h = rng.uniform(0.046, 0.06)
    m.blade((x, y, 0), h, "grass_dark", r=0.008)
    m.ico(0.021, (x, y, h), "red", scale=(1, 1, 0.6), sub=0)
    m.ico(0.007, (x, y, h + 0.009), "window", sub=0)


def bluebell(m, rng, at):
    """A bluebell: a curved stem with three hanging bells."""
    x, y = at
    h = rng.uniform(0.058, 0.076)
    m.blade((x, y, 0), h, "leaf_dark", r=0.008, lean=(0, 8))
    for k in range(3):
        m.ico(0.012, (x + 0.005 + 0.002 * k, y + 0.005, h * (0.6 + 0.16 * k)), ("flower_purple", "blue")[k % 2], scale=(1, 1, 1.25),
              sub=0)


def dandelion(m, rng, at, seed_head=False):
    """A dandelion, or its white seed head."""
    x, y = at
    h = rng.uniform(0.04, 0.056)
    m.blade((x, y, 0), h, "grass_dark", r=0.008)
    m.ico(0.018 if seed_head else 0.016, (x, y, h), "smoke" if seed_head else "gold", sub=0)


def mushroom(m, rng, at, red):
    """A mushroom: a pale stem and a cap, red with white spots or brown."""
    x, y = at
    h = rng.uniform(0.024, 0.036)
    r = rng.uniform(0.018, 0.026)
    m.cyl(r * 0.35, h, (x, y, 0), "plaster", seg=5)
    m.cyl(r, r * 0.9, (x, y, h - 0.002), "red" if red else "wood", seg=7, r2=r * 0.25)
    if red:
        for k in range(2):
            a = rng.uniform(0, 2 * math.pi)
            m.ico(0.0045, (x + r * 0.45 * math.cos(a), y + r * 0.45 * math.sin(a), h + r * 0.45), "plaster", sub=0)


def fern(m, rng, at):
    """A fern: five fronds fanning out from one point."""
    x, y = at
    for k in range(5):
        a = math.radians(k * 72 + rng.uniform(-15, 15))
        length = rng.uniform(0.07, 0.095)
        m.box((length, 0.016, 0.004), (x + math.cos(a) * length * 0.42, y + math.sin(a) * length * 0.42, 0.018), "leaf_dark",
              rot=(0, -28, math.degrees(a)), anchor="center", bevel=0)


def clover(m, rng, at):
    """A clover: three round leaves, sometimes a white flower ball above them."""
    x, y = at
    turn = rng.uniform(0, 120)
    for j in range(3):
        a = math.radians(turn + j * 120)
        m.ico(0.015, (x + 0.015 * math.cos(a), y + 0.015 * math.sin(a), 0.009), ("leaf", "grass_light")[j % 2], scale=(1, 1, 0.3),
              sub=0)
    if rng.random() < 0.5:
        m.blade((x + 0.014, y, 0), 0.04, "grass_dark", r=0.007)
        m.ico(0.013, (x + 0.014, y, 0.041), "plaster", sub=0)


def stone_cluster(m, rng, at):
    """Two or three stones, the big one mossy on top."""
    x, y = at
    m.ico(0.045, (x, y, 0.012), "stone", scale=(1.0, 0.8, 0.6), sub=1, rot=(0, 0, rng.uniform(0, 90)))
    m.ico(0.03, (x - 0.004, y + 0.002, 0.034), "grass_dark", scale=(1.0, 0.8, 0.25), sub=0)
    m.ico(0.024, (x + 0.05, y - 0.02, 0.006), "stone_dark", scale=(1, 0.9, 0.7), sub=0, rot=(0, 0, rng.uniform(0, 90)))
    if rng.random() < 0.6:
        m.ico(0.016, (x - 0.04, y + 0.035, 0.004), "stone_light", scale=(1, 0.9, 0.65), sub=0)


def grass_tile(name, seed, tufts=5, lush=False, daisies=0, poppies=0, stones=0, pebbles=0, clovers=0, dry=False,
               dandelions=0, mushrooms=0, ferns=0, molehill=False, bluebells=0):
    """A grass tile: the slab, soft mottling, tufts, and this variant's features, placed from its own seed. The flat ground (the slab and
    its patches, top at z = 0) is the model's own mesh, the one the client tints for placement hints and whose top a test checks; everything
    that rises above it is the child mesh `Details`."""
    def build():
        rng = random.Random(seed)
        m = Model()
        d = Model()
        slab(m)
        mottle(m, rng, 7 if lush else 5)
        for k in range(2):  # one or two small accents in stronger greens
            x, y = spot(rng)
            patch(m, blob(rng, rng.uniform(0.045, 0.08), n=9, wobble=0.3), ("grass_light", "grass_dark")[k], 0.0035 + 0.0003 * k, (x, y))
        if dry:
            x, y = spot(rng, keep_out=0.1)
            patch(m, blob(rng, 0.14, n=11, wobble=0.25, stretch=(1, 0.75)), "grass_dry", 0.0042, (x, y))
            for _ in range(5):
                tuft(d, rng, (x + rng.uniform(-0.09, 0.09), y + rng.uniform(-0.07, 0.07)), rng.uniform(0.04, 0.06),
                     colors=("grass_dry", "thatch", "thatch_dark"))
        if molehill:
            x, y = spot(rng, keep_out=CLEAR + 0.04)
            patch(m, blob(rng, 0.075, n=10, wobble=0.3), "dirt_dark", 0.0045, (x, y))
            d.ico(0.05, (x, y, 0.004), "dirt", scale=(1.0, 0.9, 0.45), sub=1)
            for _ in range(5):
                a = rng.uniform(0, 2 * math.pi)
                r = rng.uniform(0.05, 0.08)
                d.ico(rng.uniform(0.006, 0.01), (x + r * math.cos(a), y + r * math.sin(a), 0.003), "dirt_dark", sub=0)
        for _ in range(tufts + (3 if lush else 0)):
            tuft(d, rng, spot(rng, keep_out=CLEAR), rng.uniform(0.05, 0.085))
        for _ in range(14):  # speckles: pairs of short blades all over, low enough for a foundation to hide
            x, y = spot(rng)
            for j in range(2):
                d.blade((x + 0.008 * j, y + 0.004 * j, 0), rng.uniform(0.014, 0.02), ("grass_light", "grass_dark", "grass_cool")[(j + _) % 3],
                        r=0.01, lean=(rng.uniform(-15, 15), rng.uniform(-15, 15)))
        groups = [(daisies, daisy), (poppies, poppy), (bluebells, bluebell), (clovers, clover), (ferns, fern)]
        for count, make in groups:
            centre = spot(rng, keep_out=CLEAR + 0.02)
            for k in range(count):
                if k % 3 == 0 and k:
                    centre = spot(rng, keep_out=CLEAR + 0.02)
                p = (centre[0] + rng.uniform(-0.045, 0.045), centre[1] + rng.uniform(-0.045, 0.045))
                if max(abs(p[0]), abs(p[1])) > EDGE - 0.02:
                    continue
                make(d, rng, p)
        for k in range(dandelions):
            dandelion(d, rng, spot(rng, keep_out=CLEAR), seed_head=k % 3 == 2)
        if mushrooms:  # a little ring of mushrooms
            cx, cy = spot(rng, keep_out=CLEAR + 0.06)
            for k in range(mushrooms):
                a = 2 * math.pi * k / mushrooms + rng.uniform(-0.3, 0.3)
                mushroom(d, rng, (cx + 0.045 * math.cos(a), cy + 0.045 * math.sin(a)), red=k % 2 == 0)
        for _ in range(stones):
            stone_cluster(d, rng, spot(rng, keep_out=CLEAR + 0.04))
        for k in range(pebbles):
            pebble(d, spot(rng, keep_out=0.18), rng.uniform(0.012, 0.024), ("stone", "stone_light", "stone_dark")[k % 3],
                   turn=rng.uniform(0, 90))
        root = m.to_object(name)
        d.to_object(DETAILS, parent=root)
        return root
    return build


# --- the dirt tile ------------------------------------------------------------------------------------------------------

def dirt_tile():
    """A bare dirt tile: mottled dirt, wheel ruts with a puddle (the flat model), stones and grass at the corners (its Details)."""
    rng = random.Random(7)
    m = Model()
    d = Model()
    slab(m, "dirt")
    mottle(m, rng, 6, colors=("dirt_light", "dirt_dark"), r=(0.07, 0.13))
    for s in (-1, 1):
        m.decal(_strip_outline(rng, (1, 0), s * 0.09, 0.016, -0.47, 0.47, wobble=0.006), "dirt_dark", z=0.0042)
    m.decal(blob(rng, 0.04, n=9, wobble=0.2, stretch=(1.6, 1.0)), "water", z=0.0048, at=(0.12, 0.09))
    m.decal(blob(rng, 0.018, n=7, wobble=0.2, stretch=(1.6, 1.0)), "foam", z=0.005, at=(0.105, 0.095))
    for k in range(6):
        pebble(d, spot(rng, keep_out=0.12), rng.uniform(0.01, 0.02), ("stone", "stone_light")[k % 2], turn=rng.uniform(0, 90))
    for x, y in ((0.4, 0.4), (-0.4, -0.38), (0.38, -0.41)):
        tuft(d, rng, (x, y), rng.uniform(0.03, 0.045))
    root = m.to_object("tile_dirt")
    d.to_object(DETAILS, parent=root)
    return root


# --- road tiles -----------------------------------------------------------------------------------------------------------

def _strip_outline(rng, d, offset, half, t0, t1, wobble=0.0, steps=6, exact_end=True):
    """The outline of a strip along direction d (unit grid vector) from t0 to t1, `offset` across it and `half` wide each side; the sides
    wobble by up to `wobble`, except at t1 when exact_end (the tile edge, where neighbours must meet)."""
    dx, dy = d
    px, py = -dy, dx
    left, right = [], []
    for i in range(steps + 1):
        t = t0 + (t1 - t0) * i / steps
        last = exact_end and i >= steps - 1
        wl = 0.0 if last else rng.uniform(-wobble, wobble)
        wr = 0.0 if last else rng.uniform(-wobble, wobble)
        left.append((dx * t + px * (offset + half + wl), dy * t + py * (offset + half + wl)))
        right.append((dx * t + px * (offset - half - wr), dy * t + py * (offset - half - wr)))
    return left + right[::-1]


def _edge_tufts(m, rng, d, half, count):
    """Grass tufts along both sides of an arm, just outside the road."""
    dx, dy = d
    px, py = -dy, dx
    for _ in range(count):
        t = rng.uniform(0.22, 0.44)
        s = rng.choice((-1, 1)) * (half + rng.uniform(0.015, 0.04))
        tuft(m, rng, (dx * t + px * s, dy * t + py * s), rng.uniform(0.028, 0.05))


def _corner_grass(m, rng):
    """Mottling and a few tufts in the four grass corners between the arms."""
    def corner(r):
        # a point in one of the four corners, outside every arm
        while True:
            x, y = r.uniform(0.24, EDGE - 0.03) * r.choice((-1, 1)), r.uniform(0.24, EDGE - 0.03) * r.choice((-1, 1))
            return x, y
    mottle(m, rng, 4, keep=corner, r=(0.05, 0.09))
    for _ in range(4):
        tuft(m, rng, corner(rng), rng.uniform(0.03, 0.055))


def road_l1(variant):
    """Dirt path: a packed dirt patch in the middle and four arms to the tile edges with uneven sides, wheel ruts, pebbles and grass at
    the edges; variant b has a puddle and hoof prints, variant c stones in the track and grass in the middle between the ruts."""
    suffix = ROAD_VARIANTS[variant]
    rng = random.Random(110 + variant)
    m = Model()
    slab(m)
    _corner_grass(m, rng)
    m.prism(blob(rng, 0.215, n=14, wobble=0.07), 0.006, (0, 0, 0.003), "dirt", axis="Z", cap="dirt")
    mottle(m, rng, 3, colors=("dirt_light", "dirt_dark"), r=(0.035, 0.06), z=0.0062, keep=lambda r: (r.uniform(-0.1, 0.1),
                                                                                                      r.uniform(-0.1, 0.1)))
    pebble(m, (rng.uniform(-0.08, 0.08), rng.uniform(-0.08, 0.08)), 0.013, "stone_light", turn=rng.uniform(0, 90))
    root = m.to_object("road_l1" + suffix)
    for name, d in ARMS.items():
        a = Model()
        a.prism(_strip_outline(rng, d, 0.0, ROAD_HALF, 0.12, 0.5, wobble=0.022), 0.006, (0, 0, 0.003), "dirt", axis="Z", cap="dirt")
        for s in (-1, 1):  # ruts
            a.decal(_strip_outline(rng, d, s * 0.075, 0.014, 0.17, 0.5, wobble=0.005), "dirt_dark", z=0.0064)
        for _ in range(2):
            t = rng.uniform(0.2, 0.44)
            o = rng.uniform(-0.12, 0.12)
            a.decal(blob(rng, rng.uniform(0.02, 0.035), n=8, wobble=0.3), rng.choice(("dirt_light", "dirt_dark")), z=0.0066,
                    at=(d[0] * t - d[1] * o, d[1] * t + d[0] * o))
        _edge_tufts(a, rng, d, ROAD_HALF, 3)
        for k in range(2):  # pebbles along the edges
            t = rng.uniform(0.22, 0.46)
            s = rng.choice((-1, 1)) * (ROAD_HALF - 0.01)
            pebble(a, (d[0] * t - d[1] * s, d[1] * t + d[0] * s), rng.uniform(0.011, 0.017), ("stone", "stone_light")[k % 2],
                   turn=rng.uniform(0, 90))
        if variant == 1:
            if name in ("Road_XPlus", "Road_YMinus"):  # a puddle in a rut
                t = rng.uniform(0.28, 0.4)
                s = rng.choice((-1, 1)) * 0.075
                c = (d[0] * t - d[1] * s, d[1] * t + d[0] * s)
                a.decal(blob(rng, 0.03, n=9, wobble=0.2, stretch=(1.5, 0.9) if d[0] else (0.9, 1.5)), "water", z=0.0068, at=c)
                a.decal(blob(rng, 0.012, n=7, wobble=0.2), "foam", z=0.007, at=(c[0] + 0.006, c[1] + 0.004))
            for k in range(3):  # hoof prints down the middle
                t = 0.22 + k * 0.08 + rng.uniform(-0.01, 0.01)
                o = rng.uniform(-0.02, 0.02)
                a.decal(blob(rng, 0.01, n=6, wobble=0.15, stretch=(1.0, 1.3)), "dirt_dark", z=0.0067,
                        at=(d[0] * t - d[1] * o, d[1] * t + d[0] * o))
        if variant == 2:
            for k in range(3):  # grass in the middle, between the ruts
                t = 0.24 + k * 0.08 + rng.uniform(-0.015, 0.015)
                tuft(a, rng, (d[0] * t, d[1] * t), rng.uniform(0.016, 0.024), blades=4)
            for k in range(2):  # stones pressed into the track
                t = rng.uniform(0.2, 0.46)
                o = rng.uniform(-0.12, 0.12)
                a.ico(rng.uniform(0.014, 0.02), (d[0] * t - d[1] * o, d[1] * t + d[0] * o, 0.006), "stone_light", scale=(1, 0.85, 0.4),
                      sub=0, rot=(0, 0, rng.uniform(0, 90)))
        a.to_object(name, parent=root)
    return root


def _log_across(m, rng, d, t, half, color):
    """One log laid across an arm at distance t, a little out of line, with light cut ends."""
    dx, dy = d
    r = rng.uniform(0.018, 0.024)
    length = 2 * half - rng.uniform(0.02, 0.05)
    jitter = rng.uniform(-0.004, 0.004)
    shift = rng.uniform(-0.012, 0.012)
    yaw = rng.uniform(-5, 5)
    x, y = dx * (t + jitter) - dy * shift, dy * (t + jitter) + dx * shift
    m.cyl(r, length, (x, y, r + 0.004), color, axis="Y" if dx else "X", seg=6, cap="wood_end", rot=(0, 0, yaw))
    return r


def road_l2(variant):
    """Log road: a plank deck in the middle and four arms of logs laid across the way on a muddy bed, each log a little different, rail
    logs held by stakes on both sides; variant b has a missing log with a puddle, variant c moss on the logs and a loose plank."""
    suffix = ROAD_VARIANTS[variant]
    rng = random.Random(220 + variant)
    m = Model()
    slab(m)
    _corner_grass(m, rng)
    m.prism(blob(rng, 0.25, n=12, wobble=0.04, turn=15), 0.008, (0, 0, 0.004), "dirt_dark", axis="Z", cap="dirt_dark")
    for k in range(5):
        length = rng.uniform(0.34, 0.39)
        color = rng.choice(("wood_end", "wood_end", "wood"))
        x = -0.152 + k * 0.076
        y = rng.uniform(-0.01, 0.01)
        m.box((0.072, length, 0.016), (x, y, 0.008), color, rot=(0, 0, rng.uniform(-2, 2)), bevel=0.005)
        for s in (-1, 1):  # nails
            m.box((0.008, 0.008, 0.003), (x, y + s * (length / 2 - 0.02), 0.024), "iron", bevel=0)
    root = m.to_object("road_l2" + suffix)
    for name, d in ARMS.items():
        a = Model()
        a.prism(_strip_outline(rng, d, 0.0, LOG_HALF, 0.19, 0.5, wobble=0.016), 0.008, (0, 0, 0.004), "dirt_dark", axis="Z",
                cap="dirt_dark")
        missing = variant == 1 and name in ("Road_XMinus", "Road_YPlus")
        gap = rng.randint(1, 3) if missing else -1
        for k in range(5):
            t = 0.23 + k * 0.058
            if k == gap:
                c = (d[0] * t, d[1] * t)
                a.decal(blob(rng, 0.045, n=9, wobble=0.2, stretch=(1.0, 2.2) if d[0] else (2.2, 1.0)), "dirt", z=0.0084, at=c)
                a.decal(blob(rng, 0.024, n=8, wobble=0.25), "water", z=0.0088, at=(c[0] + 0.01, c[1] - 0.012))
                continue
            r = _log_across(a, rng, d, t, LOG_HALF - 0.01, rng.choice(("wood", "wood", "wood_dark")))
            if variant == 2 and rng.random() < 0.45:  # moss on top of the log
                a.ico(0.02, (d[0] * t + d[1] * rng.uniform(-0.08, 0.08), d[1] * t - d[0] * rng.uniform(-0.08, 0.08), 2 * r + 0.003),
                      "grass_dark", scale=(1.2, 1.2, 0.25), sub=0)
        for s in (-1, 1):  # rail logs and their stakes
            off = s * (LOG_HALF + 0.002)
            mid = 0.35
            a.cyl(0.014, 0.3, (d[0] * mid - d[1] * off, d[1] * mid + d[0] * off, 0.018), "wood_dark", axis="X" if d[0] else "Y", seg=6,
                  cap="wood_end")
            for t in (0.26, 0.44):
                so = off + s * 0.02
                a.box((0.012, 0.012, 0.05), (d[0] * t - d[1] * so, d[1] * t + d[0] * so, 0), "wood_dark", bevel=0)
        _edge_tufts(a, rng, d, LOG_HALF + 0.02, 3)
        if variant == 2 and name == "Road_YMinus":  # a loose plank by the road
            t = 0.33
            o = LOG_HALF + 0.07
            a.box((0.13, 0.04, 0.012), (d[0] * t - d[1] * o, d[1] * t + d[0] * o, 0), "wood_end", rot=(0, 0, 27), bevel=0.004)
        a.to_object(name, parent=root)
    return root


# The eight grass tiles: (name, seed, features). The client's weights per 20 tiles: plain 5, meadow 2, stones 2, clover 3, dry 2,
# mushrooms 2, molehill 2, bluebells 2 (GroundVariants).
GRASS = [
    ("tile_grass", 11, dict(tufts=6, lush=True)),
    ("tile_grass_b", 23, dict(tufts=4, daisies=9, poppies=4)),
    ("tile_grass_c", 37, dict(tufts=4, stones=2, pebbles=7)),
    ("tile_grass_d", 41, dict(tufts=4, clovers=9)),
    ("tile_grass_e", 53, dict(tufts=3, dry=True, dandelions=7)),
    ("tile_grass_f", 67, dict(tufts=4, mushrooms=6, ferns=2)),
    ("tile_grass_g", 71, dict(tufts=6, molehill=True, pebbles=3)),
    ("tile_grass_h", 83, dict(tufts=5, bluebells=9)),
]

TILES = {name: grass_tile(name, seed, **features) for name, seed, features in GRASS}
TILES["tile_dirt"] = dirt_tile

ROADS = {}
for _level, _make in ((1, road_l1), (2, road_l2)):
    for _v, _suffix in enumerate(ROAD_VARIANTS):
        ROADS[f"road_l{_level}{_suffix}"] = (lambda make, v: lambda: make(v))(_make, _v)
