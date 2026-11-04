"""Renders the UI icons of visual v2 from the same models and palette (transparent PNG, 256 x 256).

    blender -b --factory-startup --python visual-v2/blender/render_ui.py -- [--only NAME ...]

Output: <name>.png in visual-v2/unity/Resources/UI/Icons/ (the overlay of the Unity project's Resources/UI/Icons/, which the client
loads by name through UiIcons). The 15 names of v1 keep their names; the new ones are for CR-0008. A render is not byte-identical
from run to run, so run it only when an icon should change.
"""
import argparse
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import assets_common as ac  # noqa: E402
import assets_era1 as e1  # noqa: E402
import build_all as ba  # noqa: E402
import lib  # noqa: E402
from lib import Model  # noqa: E402

ICONS_OUT = os.path.join(ba.PACKAGE, "unity", "Resources", "UI", "Icons")


# --- small models that exist only as icons ------------------------------------------------------------------------

def tool(name, build, rot):
    m = Model()
    build(m)
    obj = m.to_object(name)
    obj.rotation_euler = tuple(math.radians(a) for a in rot)
    return [obj]


def hammer():
    def b(m):
        m.box((0.035, 0.035, 0.34), (0, 0, 0), "wood", bevel=0.008)
        m.box((0.036, 0.04, 0.04), (0, 0, 0.12), "wood_dark", bevel=0.006)
        m.box((0.17, 0.06, 0.06), (0, 0, 0.3), "iron", bevel=0.012)
        m.box((0.05, 0.065, 0.065), (-0.08, 0, 0.298), "metal", bevel=0.01)
    return tool("hammer", b, (0, 35, -45))


def axe():
    def b(m):
        m.box((0.032, 0.032, 0.36), (0, 0, 0), "wood", bevel=0.008)
        m.box((0.11, 0.03, 0.09), (0.05, 0, 0.27), "iron", bevel=0.01)
        m.box((0.02, 0.034, 0.11), (0.105, 0, 0.26), "metal", bevel=0.004)
        m.box((0.04, 0.036, 0.03), (0, 0, 0.05), "wood_dark", bevel=0.006)
    return tool("axe", b, (0, -25, -45))


def frame_saw():
    def b(m):
        for x in (-0.13, 0.13):
            m.box((0.03, 0.03, 0.26), (x, 0, 0), "wood", bevel=0.008)
        m.box((0.26, 0.026, 0.026), (0, 0, 0.12), "wood_dark", bevel=0.006)
        m.box((0.28, 0.008, 0.04), (0, 0, 0.02), "metal", bevel=0)
        for k in range(7):
            m.prism([(0, 0), (0.02, 0), (0.01, -0.016)], 0.008, (-0.13 + k * 0.04, 0, 0.022), "metal", axis="Y")
        m.box((0.26, 0.01, 0.01), (0, 0, 0.24), "thatch_dark", bevel=0)
    return tool("frame_saw", b, (0, 0, -45))


def charter():
    def b(m):
        m.box((0.28, 0.012, 0.22), (0, 0.035, -0.22), "plaster", bevel=0.004)
        m.cyl(0.05, 0.3, (0, 0, 0), "plaster", axis="X", seg=10)
        for x in (-0.17, 0.17):
            m.cyl(0.03, 0.05, (x, 0, 0), "wood_dark", axis="X", seg=8)
        for k in range(3):
            m.box((0.18 - k * 0.04, 0.004, 0.012), (-0.02, 0.043, -0.07 - k * 0.04), "wood_dark", bevel=0)
        m.cyl(0.04, 0.014, (0.07, 0.048, -0.17), "red", axis="Y", seg=10)
    return tool("charter", b, (-10, 0, -45))


def storehouse():
    m = Model()
    e1.crate(m, (0, 0, 0), s=0.14)
    e1.crate(m, (0.16, 0.02, 0), s=0.12, rot=10)
    e1.crate(m, (0.06, 0.0, 0.14), s=0.11, rot=-8)
    e1.sack(m, (-0.11, 0.13, 0), s=1.6)
    return [m.to_object("storehouse")]


def book():
    def b(m):
        m.box((0.3, 0.22, 0.03), (0, 0, 0), "red", bevel=0.008)
        m.box((0.28, 0.2, 0.05), (0.01, 0, 0.03), "plaster", bevel=0.006)
        m.box((0.3, 0.22, 0.025), (0, 0, 0.08), "red", bevel=0.008)
        m.cyl(0.045, 0.01, (0, 0, 0.105), "gold", seg=12)
        m.box((0.02, 0.06, 0.004), (0.06, -0.11, 0.06), "blue", bevel=0)
    return tool("book", b, (0, 0, 20))


def tally_sticks():
    """Notched tally sticks tied in a bundle (the research that unlocks any percentage)."""
    def b(m):
        for k in range(4):
            x = (k - 1.5) * 0.045
            m.box((0.034, 0.03, 0.3 - 0.03 * (k % 2)), (x, 0, 0), "wood_end", rot=(0, (k - 1.5) * 5, 0), bevel=0.006)
            for n in range(3 + k % 2):
                m.box((0.036, 0.034, 0.008), (x, 0.002, 0.06 + n * 0.05), "wood_dark", bevel=0)
        m.box((0.2, 0.04, 0.03), (0, 0, 0.12), "red", bevel=0.006)
    return tool("tally_sticks", b, (0, 0, -40))


def boot():
    """A sturdy leather boot (the research that makes walkers faster)."""
    def b(m):
        m.box((0.09, 0.07, 0.2), (0, 0, 0.03), "wood_dark", bevel=0.02)
        m.box((0.2, 0.08, 0.06), (0.05, 0, 0.0), "wood_dark", bevel=0.02)
        m.box((0.21, 0.085, 0.03), (0.05, 0, 0.0), "iron", bevel=0.008)
        m.box((0.1, 0.08, 0.03), (0, 0, 0.2), "wood", bevel=0.008)
        for k in range(3):
            m.box((0.012, 0.075, 0.012), (0.02 + k * 0.004, 0.0, 0.08 + k * 0.04), "gold", bevel=0)
    return tool("boot", b, (0, 0, -30))


def bread_mug():
    """Bread and a mug: the couriers' rest (re-energize)."""
    m = Model()
    m.ico(0.11, (0, 0.03, 0.07), "thatch", scale=(1.4, 0.9, 0.7))
    for k in range(3):
        m.box((0.012, 0.13, 0.012), (-0.06 + k * 0.06, 0.03, 0.12), "thatch_dark", rot=(0, 0, 20), anchor="center", bevel=0)
    m.cyl(0.06, 0.16, (0.13, -0.08, 0), "wood", seg=10, cap="foam")
    for zz in (0.03, 0.12):
        m.cyl(0.063, 0.014, (0.13, -0.08, zz), "iron", seg=10)
    m.box((0.03, 0.02, 0.09), (0.2, -0.08, 0.035), "wood_dark", bevel=0.008)
    m.ico(0.05, (0.13, -0.08, 0.17), "foam", scale=(1.1, 1.1, 0.5))
    return [m.to_object("bread_mug")]


def workers():
    out = []
    for x, y, rot in ((0, 0, 200), (0.13, -0.1, 215), (-0.12, -0.08, 190)):
        out.append(ba.place(ac.worker, (x, y, 0), rot_z=rot))
    return out


def couriers():
    out = []
    for x, y, rot, load in ((0, 0, 200, ac.carry_logs), (0.14, -0.1, 215, ac.carry_planks)):
        c = ba.place(ac.courier, (x, y, 0), rot_z=rot)
        bpy.context.view_layer.update()
        ba.place(load, ba.anchors(c)["Anchor_Load"].matrix_world.translation, rot_z=rot)
        out.append(c)
    return out


def trips():
    """A worker mid-stride carrying a bundle of logs on the head, footprints behind (a one-time trip)."""
    w = ba.place(ac.worker, (0, 0, 0), rot_z=230)
    lib.pose_at(w, 0.25, 1.0, fraction=True)
    load = ba.place(ac.carry_logs, (0.0, 0.0, 0.166), rot_z=230 + 90)
    m = Model()
    for k in range(4):
        side = 0.018 if k % 2 else -0.018
        m.box((0.022, 0.012, 0.004), (0.05 + k * 0.035 + side * 0.3, 0.04 + k * 0.03 + side, 0), "dirt_dark", rot=(0, 0, 40))
    steps = m.to_object("steps")
    return [w, load, steps]


def road(level):
    def build():
        r = ba.place(ac.ROADS[f"road_l{level}"], (0, 0, 0))
        for child in r.children:
            if child.name.split(".")[0] in ("Road_XPlus", "Road_XMinus"):
                child.hide_render = False
            else:
                child.hide_render = True
        return [r]
    return build


def one(build):
    return lambda: [build()]


def with_fill(name, steps):
    def build():
        root = e1.BUILDINGS[name]()
        base, level = ba.split_name(name)
        fill_name = f"{base}_l{level}_fill"
        if fill_name in e1.FILLS:
            f = e1.FILLS[fill_name]()
            f.parent = root
            for child in f.children:
                bits = child.name.split(".")[0].split("_")
                child.hide_render = int(bits[2]) > steps
        return [root]
    return build


ICONS = {
    # v1 names, drawn again from the v2 models
    "item_coins": one(e1.PROPS["prop_coins_m"]), "item_logs": one(e1.PROPS["prop_logs_m"]),
    "item_planks": one(e1.PROPS["prop_planks_m"]),
    "workers": workers, "builders": hammer, "research": book,
    "bld_house": one(e1.BUILDINGS["house_l1"]), "bld_woodcutter": with_fill("woodcutter_l1", 2),
    "bld_sawmill": with_fill("sawmill_l1", 2), "bld_trading_post": one(e1.BUILDINGS["trading_post_l1"]),
    "bld_construction": one(e1.BUILDINGS["construction_site"]),
    "res_trade_charter": charter, "res_sharp_axes": axe, "res_frame_saw": frame_saw, "res_storehouses": storehouse,
    # new for CR-0008
    "bld_scriptorium": one(e1.BUILDINGS["scriptorium_l1"]), "bld_courier_station": one(e1.BUILDINGS["courier_station_l1"]),
    "bld_warehouse": with_fill("warehouse_l1", 2), "road_l1": road(1), "road_l2": road(2),
    "couriers": couriers, "trips": trips, "reenergize": bread_mug,
    "res_tally_sticks": tally_sticks, "res_sturdy_boots": boot,
}


def meshes(objects):
    out = []
    for obj in objects:
        out += [o for o in lib.descendants(obj) if o.type == "MESH" and not o.hide_render]
    return out


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="render_ui.py")
    parser.add_argument("--only", nargs="*")
    args = parser.parse_args(argv)
    lib.load_palette(os.path.join(ba.PACKAGE, "unity", "Art", "Textures", "palette_era1.png"))
    for name, build in ICONS.items():
        if args.only and name not in args.only:
            continue
        lib.clear_scene()
        objects = build()
        lib.setup_render(256, 256, transparent=True)
        lib.frame_camera(meshes(objects), margin=1.06)
        lib.render(os.path.join(ICONS_OUT, name + ".png"))


main()
