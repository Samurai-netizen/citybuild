"""Era-independent models: ground tiles, nature, the worker."""
from lib import Model


def tile(name, color):
    """One grid tile, 1 x 1, top face at z = 0. The bevel draws the grid lines between neighbours."""
    def build():
        m = Model()
        m.box((1.0, 1.0, 0.1), (0, 0, -0.1), color, bevel=0.025)
        return m.to_object(name)
    return build


def tree_pine():
    m = Model()
    m.cyl(0.035, 0.12, (0, 0, 0), "wood_dark", seg=6)
    for z, r, h, c in ((0.08, 0.19, 0.24, "leaf_dark"), (0.21, 0.15, 0.21, "leaf"), (0.33, 0.105, 0.18, "leaf_dark")):
        m.cyl(r, h, (0, 0, z), c, seg=7, r2=0.0)
    return m.to_object("tree_pine")


def tree_round():
    m = Model()
    m.cyl(0.035, 0.18, (0, 0, 0), "wood_dark", seg=6)
    m.ico(0.17, (0, 0, 0.3), "leaf", scale=(1, 1, 0.9))
    m.ico(0.11, (0.09, 0.06, 0.37), "leaf", rot=(0, 0, 30))
    m.ico(0.1, (-0.08, -0.05, 0.24), "leaf_dark", rot=(20, 0, 0))
    return m.to_object("tree_round")


def bush():
    m = Model()
    m.ico(0.08, (0, 0, 0.05), "leaf_dark", scale=(1.1, 1, 0.8))
    m.ico(0.06, (0.06, 0.03, 0.06), "leaf", scale=(1, 1, 0.85))
    return m.to_object("bush")


def rock():
    m = Model()
    m.ico(0.1, (0, 0, 0.035), "stone", scale=(1.0, 0.8, 0.55), rot=(0, 0, 20))
    m.ico(0.055, (0.09, 0.05, 0.02), "stone_dark", scale=(1, 0.9, 0.7), rot=(0, 0, 50))
    return m.to_object("rock")


def worker():
    """A tiny villager in a straw hat, about 0.17 tall; origin at the feet, facing +Y like the buildings."""
    m = Model()
    for x in (-0.017, 0.017):
        m.box((0.026, 0.03, 0.05), (x, 0, 0), "wood_dark", bevel=0.006)
    m.box((0.075, 0.05, 0.065), (0, 0, 0.048), "blue", bevel=0.012)
    for x in (-0.046, 0.046):
        m.box((0.02, 0.026, 0.05), (x, 0, 0.058), "blue", bevel=0.006)
    m.ico(0.032, (0, 0, 0.138), "skin", sub=2)
    m.cyl(0.042, 0.008, (0, 0, 0.152), "thatch", seg=10)
    m.cyl(0.024, 0.022, (0, 0, 0.158), "thatch", seg=10)
    return m.to_object("worker")


ASSETS = {
    "tile_grass": tile("tile_grass", "grass"),
    "tile_dirt": tile("tile_dirt", "dirt"),
    "tree_pine": tree_pine,
    "tree_round": tree_round,
    "bush": bush,
    "rock": rock,
    "worker": worker,
}
