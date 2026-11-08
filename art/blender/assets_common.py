"""Era-independent models (visual v2): nature, people, carried loads, item props and smoke; the ground tiles and road tiles come from
assets_ground.py.

Placement of small details (tufts, flowers, pebbles) comes from a fixed seed per model, so every build makes the same files.
Grid directions: the client puts tile (x, y) at Unity (x + 0.5, 0, y + 0.5). Grid +X is Unity +X, which is Blender -X; grid +Y is
Unity +Z, which is Blender -Y. Road arms are named by the grid direction they point to.
"""
import math
import random

from lib import Model, anchor, blob, group

# The ground (tiles, roads and their small details) lives in assets_ground.py since the second pass; these names stay here for the
# modules that import them from assets_common.
from assets_ground import ARMS, EDGE, ROAD_HALF, ROADS, TILES, flower, pebble, slab  # noqa: E402,F401


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


ASSETS = {
    "tree_pine": tree_pine, "tree_round": tree_round, "bush": bush, "rock": rock, "stump": stump, "flowers": flowers,
    "worker": worker, "courier": courier, "carry_logs": carry_logs, "carry_planks": carry_planks,
    "fx_smoke_puff": smoke_puff,
}
