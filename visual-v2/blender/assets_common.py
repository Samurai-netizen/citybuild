"""Era-independent models (visual v2): ground tiles, road tiles, nature, people, carried loads, item props and smoke.

Placement of small details (tufts, flowers, pebbles) comes from a fixed seed per model, so every build makes the same files.
Grid directions: the client puts tile (x, y) at Unity (x + 0.5, 0, y + 0.5). Grid +X is Unity +X, which is Blender -X; grid +Y is
Unity +Z, which is Blender -Y. Road arms are named by the grid direction they point to.
"""
import math
import random

from lib import Model, anchor, blob, group

TILE_SLAB = (1.0, 1.0, 0.1)
EDGE = 0.44  # small details stay this far from the tile centre, inside the bevel that draws the grid line


# --- ground tiles -------------------------------------------------------------------------------------------------

def slab(m, color="grass"):
    """The tile block: 1 x 1, 0.1 deep, top face at z = 0; the bevel draws the grid lines between neighbours."""
    m.box(TILE_SLAB, (0, 0, -0.1), color, bevel=0.025)


def _spot(rng, keep_out=0.0):
    """A random point on the tile, inside EDGE and outside a centre square of half-size keep_out."""
    while True:
        x, y = rng.uniform(-EDGE + 0.04, EDGE - 0.04), rng.uniform(-EDGE + 0.04, EDGE - 0.04)
        if max(abs(x), abs(y)) >= keep_out:
            return x, y


def flower(m, at, petal, h=0.04):
    """A small flower: a stem with a leaf and a head of petals around a gold centre."""
    x, y, z = at
    m.blade((x, y, z), h, "grass_dark", r=0.007)
    m.ico(0.009, (x + 0.008, y, z + h * 0.35), "leaf", scale=(1.4, 0.7, 0.35), sub=0)
    m.ico(0.017, (x, y, z + h), petal, scale=(1, 1, 0.55), sub=0)
    m.ico(0.0075, (x, y, z + h + 0.006), "gold", sub=0)


def pebble(m, at, r, color="stone", turn=0):
    m.ico(r, (at[0], at[1], r * 0.25), color, scale=(1.0, 0.8, 0.55), sub=0, rot=(0, 0, turn))


def grass_tile(name, seed, patches, tufts, flowers=0, pebbles=0, clover=0):
    """A grass tile with darker and lighter patches, tufts, and optionally flowers, pebbles or clover. The middle of the tile
    keeps only flat patches, so a building's foundation covers its details and the tile still reads as free ground."""
    def build():
        rng = random.Random(seed)
        m = Model()
        slab(m)
        for k in range(patches):
            x, y = _spot(rng)
            dark = k % 3 == 1
            r = rng.uniform(0.06, 0.1) if dark else rng.uniform(0.09, 0.15)
            m.decal(blob(rng, r, n=10, stretch=(1.0, rng.uniform(0.6, 0.9)), turn=rng.uniform(0, 180)),
                    "grass_dark" if dark else "grass_light", z=0.0015 + 0.0005 * k, at=(x, y))
        for _ in range(tufts):
            x, y = _spot(rng, keep_out=0.24)
            m.tuft((x, y, 0), h=rng.uniform(0.05, 0.066), spin=rng.uniform(0, 360))
        colors = ["plaster", "flower_pink", "gold", "flower_purple"]
        for k in range(flowers):
            x, y = _spot(rng, keep_out=0.22)
            for j in range(3):
                flower(m, (x + rng.uniform(-0.035, 0.035), y + rng.uniform(-0.035, 0.035), 0), colors[(k + j) % 4],
                       h=rng.uniform(0.032, 0.046))
        for k in range(pebbles):
            x, y = _spot(rng, keep_out=0.2)
            pebble(m, (x, y), rng.uniform(0.024, 0.036), ("stone", "stone_light", "stone_dark")[k % 3], turn=rng.uniform(0, 90))
        for _ in range(clover):
            x, y = _spot(rng, keep_out=0.22)
            for j in range(3):
                a = math.radians(j * 120 + rng.uniform(0, 40))
                m.ico(0.018, (x + 0.018 * math.cos(a), y + 0.018 * math.sin(a), 0.008), "leaf", scale=(1, 1, 0.35), sub=0)
        return m.to_object(name)
    return build


def dirt_tile():
    """A bare dirt tile (paths in previews): two wheel ruts, a few pebbles and grass at the corners."""
    rng = random.Random(7)
    m = Model()
    slab(m, "dirt")
    for s in (-1, 1):
        m.decal([(-0.47, s * 0.09 - 0.016), (0.47, s * 0.09 - 0.016), (0.47, s * 0.09 + 0.016), (-0.47, s * 0.09 + 0.016)],
                "dirt_dark", z=0.0015)
    for k in range(5):
        x, y = _spot(rng, keep_out=0.16)
        pebble(m, (x, y), rng.uniform(0.012, 0.02), ("stone", "stone_light")[k % 2])
    for x, y in ((0.4, 0.4), (-0.4, -0.38)):
        m.tuft((x, y, 0), h=0.032, spin=rng.uniform(0, 360))
    return m.to_object("tile_dirt")


# --- road tiles ---------------------------------------------------------------------------------------------------

# Arm name -> the direction it points in Blender (x, y). See the module docstring for the grid directions.
ARMS = {"Road_XPlus": (-1, 0), "Road_XMinus": (1, 0), "Road_YPlus": (0, -1), "Road_YMinus": (0, 1)}
ROAD_HALF = 0.17  # half the width of a road arm; arms of neighbouring tiles meet edge to edge


def _arm_box(m, dx, dy, length, width, z, h, color, start=0.0):
    """A box from `start` along the arm's direction to `start + length`, `width` across it, `h` high from z."""
    mid = start + length / 2
    size = (length, width, h) if dx else (width, length, h)
    m.box(size, (dx * mid, dy * mid, z), color, bevel=min(0.004, h * 0.3))


def road_l1():
    """Dirt path: a packed dirt patch in the middle and four arms to the tile edges, each with wheel ruts and a pebble edge."""
    rng = random.Random(11)
    m = Model()
    slab(m)
    m.prism(blob(rng, 0.2, n=12, wobble=0.05), 0.008, (0, 0, 0.004), "dirt", axis="Z", cap="dirt")
    pebble(m, (0.07, -0.05), 0.014, "stone_light")
    root = m.to_object("road_l1")
    for name, (dx, dy) in ARMS.items():
        a = Model()
        _arm_box(a, dx, dy, 0.5 - 0.12, 2 * ROAD_HALF, 0.0, 0.008, "dirt", start=0.12)
        for s in (-1, 1):  # ruts
            off = s * 0.075
            length = 0.5 - 0.2
            mid = 0.2 + length / 2
            cx, cy = dx * mid + (off if dy else 0), dy * mid + (off if dx else 0)
            w = (length, 0.026) if dx else (0.026, length)
            a.decal([(-w[0] / 2, -w[1] / 2), (w[0] / 2, -w[1] / 2), (w[0] / 2, w[1] / 2), (-w[0] / 2, w[1] / 2)], "dirt_dark",
                    z=0.0085, at=(cx, cy))
        for k in range(3):  # pebbles along both edges
            t = 0.24 + k * 0.09
            for s in (-1, 1):
                off = s * (ROAD_HALF + 0.012)
                px, py = dx * t + (off if dy else 0), dy * t + (off if dx else 0)
                pebble(a, (px, py), 0.013 + 0.004 * ((k + (s > 0)) % 2), ("stone", "stone_light")[(k + (s > 0)) % 2])
        a.to_object(name, parent=root)
    return root


def road_l2():
    """Log road: a plank platform in the middle and four arms of logs laid across the way on a dirt bed, with a rail log on
    each side."""
    m = Model()
    slab(m)
    m.box((0.4, 0.4, 0.008), (0, 0, 0), "dirt_dark", bevel=0.004)
    for k in range(5):
        m.box((0.075, 0.38, 0.018), (-0.152 + k * 0.076, 0, 0.008), "wood_end", bevel=0.005)
    for s in (-1, 1):
        m.box((0.38, 0.02, 0.022), (0, s * 0.19, 0.008), "wood_dark", bevel=0.005)
    root = m.to_object("road_l2")
    for name, (dx, dy) in ARMS.items():
        a = Model()
        _arm_box(a, dx, dy, 0.3, 2 * ROAD_HALF + 0.02, 0.0, 0.008, "dirt_dark", start=0.2)
        for k in range(5):
            t = 0.235 + k * 0.057
            p = (dx * t, dy * t, 0.026)
            a.cyl(0.021, 2 * ROAD_HALF - 0.02, p, "wood" if k % 2 else "wood_dark", axis="Y" if dx else "X", seg=6,
                  cap="wood_end")
        for s in (-1, 1):
            off = s * (ROAD_HALF + 0.004)
            mid = 0.35
            p = (dx * mid + (off if dy else 0), dy * mid + (off if dx else 0), 0.018)
            a.cyl(0.016, 0.3, p, "wood_dark", axis="X" if dx else "Y", seg=6, cap="wood_end")
        a.to_object(name, parent=root)
    return root


# --- nature -------------------------------------------------------------------------------------------------------

def tree_pine():
    """A pine: a trunk with a root flare and four tiers, each with a darker skirt under it."""
    m = Model()
    m.cyl(0.05, 0.03, (0, 0, 0), "wood_dark", seg=6, r2=0.035)
    m.cyl(0.033, 0.14, (0, 0, 0), "wood_dark", seg=6)
    for z, r, h, c in ((0.09, 0.2, 0.2, "leaf_dark"), (0.19, 0.165, 0.18, "leaf"), (0.28, 0.13, 0.16, "leaf_dark"),
                       (0.37, 0.09, 0.15, "leaf")):
        m.cyl(r + 0.012, 0.03, (0, 0, z - 0.012), "leaf_dark", seg=8, r2=r - 0.01, rot=(0, 0, 22.5))
        m.cyl(r, h, (0, 0, z), c, seg=8, r2=0.0)
    return m.to_object("tree_pine")


def tree_round():
    """A round leafy tree: a trunk that forks, a crown of five blobs in three greens, and a few red apples."""
    m = Model()
    m.cyl(0.045, 0.03, (0, 0, 0), "wood_dark", seg=6, r2=0.032)
    m.cyl(0.032, 0.2, (0, 0, 0), "wood_dark", seg=6)
    m.cyl(0.016, 0.12, (0.02, 0, 0.16), "wood_dark", seg=5, rot=(0, 35, 0))
    for (x, y, z), r, c in (((0, 0, 0.31), 0.16, "leaf"), ((0.09, 0.06, 0.37), 0.11, "leaf"),
                            ((-0.09, -0.05, 0.25), 0.1, "leaf_dark"), ((0.07, -0.08, 0.27), 0.095, "leaf_dark"),
                            ((-0.04, 0.08, 0.41), 0.085, "grass_light")):
        m.ico(r, (x, y, z), c, scale=(1, 1, 0.9))
    for x, y, z in ((0.12, 0.09, 0.3), (-0.06, 0.13, 0.33), (0.15, -0.03, 0.36)):
        m.ico(0.016, (x, y, z), "red", sub=1)
    return m.to_object("tree_round")


def bush():
    """A bush of three blobs with a few pink blossoms."""
    m = Model()
    m.ico(0.08, (0, 0, 0.05), "leaf_dark", scale=(1.1, 1, 0.8))
    m.ico(0.06, (0.06, 0.03, 0.06), "leaf", scale=(1, 1, 0.85))
    m.ico(0.05, (-0.05, 0.05, 0.055), "grass_light", scale=(1, 1, 0.8))
    for x, y, z in ((0.04, 0.08, 0.09), (-0.07, 0.0, 0.1), (0.09, -0.03, 0.08)):
        m.ico(0.012, (x, y, z), "flower_pink", sub=0)
    return m.to_object("bush")


def rock():
    """Two stones with a mossy top and a pebble."""
    m = Model()
    m.ico(0.1, (0, 0, 0.035), "stone", scale=(1.0, 0.8, 0.55), rot=(0, 0, 20))
    m.ico(0.07, (-0.01, 0.0, 0.07), "grass_dark", scale=(1.0, 0.75, 0.25), rot=(0, 0, 20), sub=1)
    m.ico(0.055, (0.09, 0.05, 0.02), "stone_dark", scale=(1, 0.9, 0.7), rot=(0, 0, 50))
    pebble(m, (-0.1, 0.06), 0.02, "stone_light")
    return m.to_object("rock")


def stump():
    """A tree stump with its rings, three roots and a mushroom."""
    m = Model()
    m.cyl(0.06, 0.07, (0, 0, 0), "wood_dark", seg=8, cap="wood_end")
    m.cyl(0.035, 0.002, (0, 0, 0.07), "wood", seg=8)
    for k in range(3):
        a = k * 120 + 15
        m.box((0.07, 0.022, 0.022), (0.06 * math.cos(math.radians(a)), 0.06 * math.sin(math.radians(a)), 0.0), "wood_dark",
              rot=(0, -20, a), bevel=0.006)
    m.cyl(0.006, 0.02, (0.075, -0.03, 0), "plaster", seg=5)
    m.cyl(0.016, 0.01, (0.075, -0.03, 0.018), "red", seg=8, r2=0.004)
    return m.to_object("stump")


def flowers():
    """A patch of flowers in four colours, for the edge of the town."""
    rng = random.Random(5)
    m = Model()
    m.decal(blob(rng, 0.1, n=9), "grass_dark", z=0.001)
    colors = ["plaster", "flower_pink", "gold", "flower_purple"]
    for k in range(8):
        a = rng.uniform(0, 2 * math.pi)
        r = rng.uniform(0, 0.08)
        flower(m, (r * math.cos(a), r * math.sin(a), 0), colors[k % 4], h=rng.uniform(0.026, 0.042))
    return m.to_object("flowers")


# --- people -------------------------------------------------------------------------------------------------------

def _person(name, tunic, hat, pack=False):
    """A villager about 0.17 tall, facing +Y, origin at the feet. Legs and arms are moving parts that swing about the hips and
    shoulders while it walks (MOTIONS: Leg_L, Leg_R, Arm_L, Arm_R)."""
    m = Model()
    hip, shoulder = 0.052, 0.104
    m.box((0.07, 0.048, 0.03), (0, 0, hip - 0.006), "wood_dark", bevel=0.008)  # breeches
    m.box((0.074, 0.05, 0.056), (0, 0, hip + 0.012), tunic, bevel=0.012)  # tunic
    m.box((0.078, 0.054, 0.012), (0, 0, hip + 0.016), "wood_dark", bevel=0.004)  # belt
    m.box((0.014, 0.006, 0.01), (0.012, 0.027, hip + 0.017), "gold", bevel=0)  # buckle
    m.box((0.05, 0.034, 0.014), (0, 0, shoulder - 0.004), tunic, bevel=0.005)  # collar
    m.ico(0.032, (0, 0, 0.138), "skin", sub=2)
    m.ico(0.0045, (-0.011, 0.029, 0.142), "window", sub=0)
    m.ico(0.0045, (0.011, 0.029, 0.142), "window", sub=0)
    m.ico(0.03, (0, -0.006, 0.147), "wood_dark", scale=(1.02, 1.0, 0.75), sub=1)  # hair
    if hat == "straw":
        m.cyl(0.043, 0.007, (0, 0, 0.154), "thatch", seg=12)
        m.cyl(0.025, 0.022, (0, 0, 0.158), "thatch", seg=10, r2=0.021)
        m.cyl(0.026, 0.005, (0, 0, 0.162), "red", seg=10)
    else:  # a hood
        m.ico(0.036, (0, -0.008, 0.146), hat, scale=(1.0, 1.05, 0.95), sub=1)
        m.ico(0.03, (0, 0.012, 0.139), "skin", sub=2)
    for side, x in (("L", 0.018), ("R", -0.018)):
        leg = m.part("Leg_" + side, (x, 0, hip), pose_rot=(28, 0, 0))
        leg.box((0.024, 0.028, hip - 0.008), (0, 0, -hip + 0.008), "wood_dark", bevel=0.006)
        leg.box((0.026, 0.036, 0.014), (0, 0.004, -hip), "wood_dark" if tunic != "wood_dark" else "stone_dark", bevel=0.005)
        arm = m.part("Arm_" + side, (2.65 * x, 0, shoulder), pose_rot=(24, 0, 0))
        arm.box((0.019, 0.024, 0.05), (0, 0, -0.05), tunic, bevel=0.006)
        arm.ico(0.011, (0, 0.002, -0.054), "skin", sub=1)
    root = None
    if pack:
        # a carrying frame with a basket on the back (-Y), the load sits on top of it
        for x in (-0.026, 0.026):
            m.box((0.008, 0.008, 0.11), (x, -0.036, 0.04), "wood_dark", bevel=0)
        m.cyl(0.03, 0.05, (0, -0.052, 0.08), "thatch_dark", seg=8, r2=0.034)
        m.cyl(0.035, 0.008, (0, -0.052, 0.128), "wood_dark", seg=8)
        root = m.to_object(name)
        anchor("Anchor_Load", root, (0, -0.052, 0.136))
    return root or m.to_object(name)


def worker():
    """A worker in a blue tunic and a straw hat."""
    return _person("worker", "blue", "straw")


def courier():
    """A courier in a red tunic and a brown hood, with a basket on a carrying frame (Anchor_Load: where a load sits)."""
    return _person("courier", "red", "wood_dark", pack=True)


def carry_logs():
    """A load of three short logs tied with a rope, for Anchor_Load."""
    m = Model()
    for i, (y, z) in enumerate(((-0.014, 0.012), (0.014, 0.012), (0.0, 0.034))):
        m.cyl(0.013, 0.07, (0, y, z), "wood", axis="X", seg=7, cap="wood_end")
    m.cyl(0.03, 0.006, (0, 0, 0.022), "thatch_dark", axis="X", seg=8)
    return m.to_object("carry_logs")


def carry_planks():
    """A load of four short planks, for Anchor_Load."""
    m = Model()
    for k in range(4):
        m.box((0.075, 0.022, 0.008), (0.003 * (k % 2), -0.012 + 0.024 * (k % 2), 0.004 + 0.008 * (k // 2) * 2 + 0.008 * (k % 2)),
              "wood_end", bevel=0.002)
    return m.to_object("carry_planks")


# --- item props ---------------------------------------------------------------------------------------------------

def log(m, at, r, length, axis="X", seg=8):
    """One log: bark sides, light cut ends with a darker heart."""
    m.cyl(r, length, at, "wood", axis=axis, seg=seg, cap="wood_end")
    m.cyl(r * 0.45, length + 0.004, at, "wood", axis=axis, seg=6, cap="thatch_dark")


def log_rows(m, at, counts, r=0.03, length=0.26, axis="X"):
    """Logs stacked in rows (bottom row first) along `axis`; `at` is the pile's centre on the ground."""
    x, y, z = at
    for row, n in enumerate(counts):
        for i in range(n):
            off = (i - (n - 1) / 2) * 2 * r
            zc = z + r + row * r * 1.72
            p = (x, y + off, zc) if axis == "X" else (x + off, y, zc)
            log(m, p, r, length, axis=axis)


def plank_stack(m, at, layers, length=0.26, axis="X", spacers=True):
    """Sawn planks in crossed-over layers on two spacers; `at` is the stack's centre on the ground."""
    x, y, z = at
    if spacers:
        for s in (-1, 1):
            p = (x + s * length * 0.32, y, z) if axis == "X" else (x, y + s * length * 0.32, z)
            m.box((0.03, 0.17, 0.02) if axis == "X" else (0.17, 0.03, 0.02), p, "wood_dark", bevel=0.004)
    base = z + (0.02 if spacers else 0.0)
    for layer in range(layers):
        shift = 0.007 if layer % 2 else -0.007
        for k in range(3):
            off = (k - 1) * 0.056 + shift
            p = (x + shift, y + off, base + layer * 0.017) if axis == "X" else (x + off, y + shift, base + layer * 0.017)
            m.box((length, 0.05, 0.016) if axis == "X" else (0.05, length, 0.016), p,
                  "wood_end" if (layer + k) % 3 else "wood", bevel=0.004)


def coin_stack(m, at, n, r=0.04):
    """A stack of n gold coins, each slightly off-centre."""
    x, y, z = at
    for i in range(n):
        dx = 0.003 * ((i * 7) % 3 - 1)
        m.cyl(r, 0.013, (x + dx, y, z + i * 0.0135), "thatch_dark", seg=12, cap="gold")


def logs_prop(name, counts):
    def build():
        m = Model()
        log_rows(m, (0, 0, 0), counts)
        if len(counts) >= 3:
            for sx in (-1, 1):
                for sy in (-1, 1):
                    m.box((0.016, 0.016, 0.16), (sx * 0.08, sy * (len(counts) * 0.03 + 0.012), 0), "wood_dark", bevel=0)
        return m.to_object(name)
    return build


def planks_prop(name, layers):
    def build():
        m = Model()
        plank_stack(m, (0, 0, 0), layers)
        if layers >= 6:
            m.box((0.012, 0.18, 0.012), (0.06, 0, 0.02 + layers * 0.017), "thatch_dark", bevel=0)
        return m.to_object(name)
    return build


def coins_prop(name, stacks, pouch=False, chest=False):
    def build():
        m = Model()
        for (x, y), n in stacks:
            coin_stack(m, (x, y, 0), n)
        if pouch:
            m.ico(0.05, (-0.07, 0.05, 0.045), "canvas", scale=(1, 1, 0.9))
            m.cyl(0.018, 0.02, (-0.07, 0.05, 0.085), "canvas", seg=8, r2=0.026)
            m.cyl(0.02, 0.006, (-0.07, 0.05, 0.088), "red", seg=8)
        if chest:
            m.box((0.16, 0.11, 0.07), (0.0, -0.08, 0), "wood_dark", bevel=0.01)
            m.box((0.17, 0.12, 0.012), (0.0, -0.08, 0.064), "metal", bevel=0.003)
            m.box((0.15, 0.1, 0.01), (0.0, -0.08, 0.07), "gold", bevel=0.003)
            for x in (-0.03, 0.03):
                m.cyl(0.026, 0.009, (x, -0.08, 0.078), "thatch_dark", seg=10, cap="gold")
        return m.to_object(name)
    return build


# --- effects ------------------------------------------------------------------------------------------------------

def smoke_puff():
    """One puff of chimney smoke; the client loops a few of them at Anchor_Smoke, rising, growing and shrinking away."""
    m = Model()
    m.ico(0.05, (0, 0, 0), "smoke", scale=(1.0, 1.0, 0.85), sub=1)
    m.ico(0.03, (0.03, 0.01, 0.02), "smoke", sub=1)
    return m.to_object("fx_smoke_puff")


TILES = {
    "tile_grass": grass_tile("tile_grass", 101, patches=3, tufts=3),
    "tile_grass_b": grass_tile("tile_grass_b", 202, patches=2, tufts=2, flowers=3),
    "tile_grass_c": grass_tile("tile_grass_c", 303, patches=2, tufts=2, pebbles=4),
    "tile_grass_d": grass_tile("tile_grass_d", 404, patches=3, tufts=5, clover=2),
    "tile_dirt": dirt_tile,
}

ROADS = {"road_l1": road_l1, "road_l2": road_l2}

ASSETS = {
    "tree_pine": tree_pine, "tree_round": tree_round, "bush": bush, "rock": rock, "stump": stump, "flowers": flowers,
    "worker": worker, "courier": courier, "carry_logs": carry_logs, "carry_planks": carry_planks,
    "fx_smoke_puff": smoke_puff,
}
