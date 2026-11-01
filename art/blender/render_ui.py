"""Renders the 2D assets for the UI from the same models and palette: item, building and research icons
(transparent PNG) and a portrait phone view of the golden scenario village with its building screen positions.

    blender -b --factory-startup --python art/blender/render_ui.py

Output: the icons <name>.png (256 x 256) straight into the Unity project, Resources/UI/Icons/ (the only copy, loaded by name),
and ui/city_phone.png (780 x 1688) and ui/hotspots.json here in art/.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402
from bpy_extras.object_utils import world_to_camera_view  # noqa: E402

import assets_common  # noqa: E402
import assets_era1  # noqa: E402
import build_all  # noqa: E402
import lib  # noqa: E402
from lib import Model  # noqa: E402

ROOT = os.path.dirname(HERE)
OUT = os.path.join(ROOT, "ui")
# Where the Unity project keeps the icons: under Resources, so a player build contains them and the client loads them by name (UiIcons).
ICONS_OUT = os.path.join(os.path.dirname(ROOT), "client", "CityBuilder", "Assets", "_Project", "Resources", "UI", "Icons")


# --- small models that exist only as icons ------------------------------------------------------------------------

def coins():
    m = Model()
    for i in range(4):
        m.cyl(0.08, 0.026, (0.0 + 0.004 * (i % 2), 0, i * 0.027), "thatch", seg=14, cap="gold")
    for i in range(2):
        m.cyl(0.08, 0.026, (0.14, -0.06 + 0.004 * i, i * 0.027), "thatch", seg=14, cap="gold")
    m.cyl(0.08, 0.026, (-0.06, 0.15, 0.07), "thatch", axis="Y", seg=14, cap="gold", rot=(-20, 0, 0))
    return [m.to_object("coins")]


def hammer():
    m = Model()
    m.box((0.035, 0.035, 0.34), (0, 0, 0), "wood", bevel=0.008)
    m.box((0.17, 0.06, 0.06), (0, 0, 0.3), "metal", bevel=0.012)
    m.box((0.05, 0.065, 0.065), (-0.08, 0, 0.298), "stone_dark", bevel=0.01)
    obj = m.to_object("hammer")
    obj.rotation_euler = (0, math.radians(35), math.radians(-45))
    return [obj]


def axe():
    m = Model()
    m.box((0.032, 0.032, 0.36), (0, 0, 0), "wood", bevel=0.008)
    m.box((0.11, 0.03, 0.09), (0.05, 0, 0.27), "metal", bevel=0.01)
    m.box((0.02, 0.034, 0.11), (0.105, 0, 0.26), "stone", bevel=0.004)
    obj = m.to_object("axe")
    obj.rotation_euler = (0, math.radians(-25), math.radians(-45))
    return [obj]


def frame_saw():
    m = Model()
    for x in (-0.13, 0.13):
        m.box((0.03, 0.03, 0.26), (x, 0, 0), "wood", bevel=0.008)
    m.box((0.26, 0.026, 0.026), (0, 0, 0.12), "wood_dark", bevel=0.006)
    m.box((0.28, 0.008, 0.04), (0, 0, 0.02), "metal", bevel=0)
    m.box((0.26, 0.01, 0.01), (0, 0, 0.24), "thatch_dark", bevel=0)
    obj = m.to_object("frame_saw")
    obj.rotation_euler = (0, 0, math.radians(-45))
    return [obj]


def charter():
    m = Model()
    m.box((0.28, 0.012, 0.22), (0, 0.035, -0.22), "plaster", bevel=0.004)
    m.cyl(0.05, 0.3, (0, 0, 0), "plaster", axis="X", seg=10)
    for x in (-0.17, 0.17):
        m.cyl(0.03, 0.05, (x, 0, 0), "wood_dark", axis="X", seg=8)
    for k in range(3):
        m.box((0.18 - k * 0.04, 0.004, 0.012), (-0.02, 0.043, -0.07 - k * 0.04), "wood_dark", bevel=0)
    m.cyl(0.04, 0.014, (0.07, 0.048, -0.17), "red", axis="Y", seg=10)
    obj = m.to_object("charter")
    obj.rotation_euler = (math.radians(-10), 0, math.radians(-45))
    return [obj]


def storehouse():
    m = Model()
    assets_era1.crate(m, (0, 0, 0), s=0.14)
    assets_era1.crate(m, (0.16, 0.02, 0), s=0.12, rot=10)
    assets_era1.crate(m, (0.06, 0.0, 0.14), s=0.11, rot=-8)
    m.ico(0.07, (-0.1, 0.12, 0.06), "thatch_dark", scale=(1, 1, 1.2))
    return [m.to_object("storehouse")]


def book():
    m = Model()
    m.box((0.3, 0.22, 0.03), (0, 0, 0), "red", bevel=0.008)
    m.box((0.28, 0.2, 0.05), (0.01, 0, 0.03), "plaster", bevel=0.006)
    m.box((0.3, 0.22, 0.025), (0, 0, 0.08), "red", bevel=0.008)
    m.cyl(0.045, 0.01, (0, 0, 0.105), "gold", seg=12)
    obj = m.to_object("book")
    obj.rotation_euler = (0, 0, math.radians(20))
    return [obj]


def workers():
    out = []
    for x, y, rot in ((0, 0, 200), (0.13, -0.1, 215), (-0.12, -0.08, 190)):
        w = assets_common.worker()
        w.location = (x, y, 0)
        w.rotation_euler[2] = math.radians(rot)
        out.append(w)
    return out


def one(build):
    return lambda: [build()]


ICONS = {
    "item_coins": coins, "item_logs": one(assets_era1.PROPS["prop_logs_m"]),
    "item_planks": one(assets_era1.PROPS["prop_planks_m"]),
    "workers": workers, "builders": hammer, "research": book,
    "bld_house": one(assets_era1.BUILDINGS["house_l1"]), "bld_woodcutter": one(assets_era1.BUILDINGS["woodcutter_l1"]),
    "bld_sawmill": one(assets_era1.BUILDINGS["sawmill_l1"]),
    "bld_trading_post": one(assets_era1.BUILDINGS["trading_post_l1"]),
    "bld_construction": one(assets_era1.BUILDINGS["construction_site"]),
    "res_trade_charter": charter, "res_sharp_axes": axe, "res_frame_saw": frame_saw, "res_storehouses": storehouse,
}


def meshes(objects):
    out = []
    for obj in objects:
        out += [o for o in lib.descendants(obj) if o.type == "MESH"]
    return out


def icons():
    for name, build in ICONS.items():
        lib.clear_scene()
        objects = build()
        lib.setup_render(256, 256, transparent=True)
        lib.frame_camera(meshes(objects), margin=1.08)
        lib.render(os.path.join(ICONS_OUT, name + ".png"))


def phone_city():
    """The village in a phone's portrait frame, and where each building lands on a 390 x 844 screen."""
    _, buildings = build_all.city_scene()
    lib.setup_render(780, 1688)
    cam = lib.frame_camera(meshes([root for _, root in buildings]), margin=0.92)
    lib.render(os.path.join(OUT, "city_phone.png"))
    scene = bpy.context.scene
    bpy.context.view_layer.update()
    spots = []
    for k, (name, root) in enumerate(buildings):
        a = build_all.anchors(root)
        base = world_to_camera_view(scene, cam, root.matrix_world.translation)
        top = world_to_camera_view(scene, cam, a["Anchor_Status"].matrix_world.translation)
        spots.append({"id": k + 1, "model": name, "type": name.rsplit("_l", 1)[0],
                      "x": round(base.x * 390, 1), "y": round((1 - base.y) * 844, 1),
                      "statusX": round(top.x * 390, 1), "statusY": round((1 - top.y) * 844, 1)})
    with open(os.path.join(OUT, "hotspots.json"), "w") as f:
        json.dump(spots, f, indent=1)
    print("HOTSPOTS", json.dumps(spots))


def main():
    lib.load_palette(os.path.join(ROOT, "textures", "palette_era1.png"))
    icons()
    phone_city()


main()
