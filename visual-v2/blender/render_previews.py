"""Renders the package's preview pictures: lineups, a town, and the moving parts with their store filling.

    blender -b --factory-startup --python visual-v2/blender/render_previews.py -- [--only lineups town motion] [--frames 16]

Output in visual-v2/previews/: lineup_buildings.jpg, lineup_ground.jpg, lineup_props.jpg, town.jpg, town_closeup.jpg, and in
previews/motion/ one GIF per building with moving parts (every part runs one cycle per loop while the store fills step by step)
plus motion_sheet.jpg (eight frames of each). Renders come out as PNG and are kept as JPEG (git keeps every version of a picture);
that and the GIFs need ImageMagick's `convert` on the PATH.
"""
import argparse
import math
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import assets_common as ac  # noqa: E402
import assets_era1 as e1  # noqa: E402
import build_all as ba  # noqa: E402
import lib  # noqa: E402

OUT = os.path.join(ba.PACKAGE, "previews")


def ground_variant(x, y):
    """The grass tile of grid tile (x, y): the client's GroundVariants rule (a fixed hash; plain 40 %, flowers, pebbles and clover
    20 % each). Index into GRASS."""
    h = (x * 374761393 + y * 668265263) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    h ^= h >> 16
    bucket = h % 10
    return 0 if bucket < 4 else 1 if bucket < 6 else 2 if bucket < 8 else 3


GRASS = ["tile_grass", "tile_grass_b", "tile_grass_c", "tile_grass_d"]


def at_grid(i, j):
    """Blender position of grid tile (i, j) on an 8 x 8 town: grid +X is Blender -X and grid +Y is Blender -Y (see assets_common)."""
    return (-(i - 3.5), -(j - 3.5), 0)


def show_arms(root, neighbours):
    """Hides the road arms that point at no road: the client's RoadArms rule. `neighbours` = set of arm names to keep."""
    for child in root.children:
        if child.name.split(".")[0] in ac.ARMS and child.name.split(".")[0] not in neighbours:
            child.hide_render = True


def show_fill(fill_root, steps):
    """Shows the first `steps[item]` steps of each item's fill and hides the rest: the client's StorageFill rule."""
    for child in fill_root.children:
        bits = child.name.split(".")[0].split("_")
        child.hide_render = int(bits[2]) > steps.get(bits[1], 0)


def add_fill(root, name, steps):
    base, level = ba.split_name(name)
    fill_name = f"{base}_l{level}_fill"
    if fill_name not in e1.FILLS:
        return None
    f = e1.FILLS[fill_name]()
    f.parent = root
    show_fill(f, steps)
    return f


def slot(c, r, gap=1.0):
    """A lineup position: column c runs across the picture (Blender (1, -1)), row r goes back (Blender (-1, -1))."""
    return (c * gap - r * 1.15, -c * gap - r * 1.15, 0)


def to_jpeg(path):
    """Converts a rendered PNG to a JPEG beside it and removes the PNG (smaller in git)."""
    subprocess.run(["convert", path, "-quality", "86", path[:-4] + ".jpg"], check=True)
    os.remove(path)


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.hide_render]


# --- lineups --------------------------------------------------------------------------------------------------------

def lineup_buildings():
    lib.clear_scene()
    rows = [["house_l1", "woodcutter_l1", "sawmill_l1", "trading_post_l1", "scriptorium_l1", "courier_station_l1",
             "warehouse_l1"],
            ["house_l2", "woodcutter_l2", "sawmill_l2", "trading_post_l2", "construction_site", "courier_station_l2",
             "warehouse_l2"]]
    for r, row in enumerate(rows):
        for c, name in enumerate(row):
            pos = slot(c, r)
            ba.place(ac.TILES[GRASS[(c + r) % 4]], pos)
            root = ba.place(e1.BUILDINGS[name], pos)
            add_fill(root, name, {"logs": 3, "planks": 3})
            a = ba.anchors(root)
            if "Anchor_Door" in a:
                bpy.context.view_layer.update()
                p = a["Anchor_Door"].matrix_world.translation.copy()
                p.x += 0.07
                ba.place(ac.worker, p, rot_z=200)
    lib.setup_render(2400, 1000)
    lib.frame_camera(meshes(), margin=1.02)
    lib.render(os.path.join(OUT, "lineup_buildings.png"))
    to_jpeg(os.path.join(OUT, "lineup_buildings.png"))


def lineup_ground():
    lib.clear_scene()
    for c, name in enumerate(list(ac.TILES)):
        ba.place(ac.TILES[name], slot(c, 0))
    # roads: alone, a straight, a corner, a T and a cross, for both levels
    shapes = [set(), {"Road_XPlus", "Road_XMinus"}, {"Road_XPlus", "Road_YPlus"}, {"Road_XPlus", "Road_XMinus", "Road_YPlus"},
              set(ac.ARMS)]
    for r, name in enumerate(("road_l1", "road_l2")):
        for c, arms in enumerate(shapes):
            root = ba.place(ac.ROADS[name], slot(c, r + 1))
            show_arms(root, arms)
    lib.setup_render(2000, 1100)
    lib.frame_camera(meshes(), margin=1.03)
    lib.render(os.path.join(OUT, "lineup_ground.png"))
    to_jpeg(os.path.join(OUT, "lineup_ground.png"))


def lineup_props():
    lib.clear_scene()
    names = list(ac.ASSETS) + list(e1.PROPS)
    for k, name in enumerate(names):
        build = ac.ASSETS.get(name) or e1.PROPS[name]
        x, y, _ = slot(k % 7, k // 7, gap=0.42)
        x, y = x * 1.0, y * 1.0
        root = ba.place(build, (x, y, 0.08 if name.startswith("fx_") else 0), rot_z=200 if name in ("worker", "courier") else 0)
        if name == "courier":
            bpy.context.view_layer.update()
            load = ba.place(ac.carry_logs, ba.anchors(root)["Anchor_Load"].matrix_world.translation)
            load.rotation_euler[2] = math.radians(200)
    lib.setup_render(1800, 1100)
    lib.frame_camera(meshes(), margin=1.04)
    lib.render(os.path.join(OUT, "lineup_props.png"))
    to_jpeg(os.path.join(OUT, "lineup_props.png"))


# --- the town -------------------------------------------------------------------------------------------------------

# A CR-0008 town on the 8 x 8 grid: (model, grid x, grid y, store steps). Fronts face grid -Y, onto the road along y = 2.
TOWN = [
    ("house_l1", 0, 3, {}), ("house_l2", 1, 3, {}), ("woodcutter_l1", 2, 3, {"logs": 3}), ("sawmill_l1", 3, 3, {"logs": 1, "planks": 2}),
    ("trading_post_l1", 5, 3, {"planks": 1}), ("warehouse_l1", 6, 3, {"logs": 2, "planks": 1}), ("courier_station_l1", 7, 3, {}),
    ("scriptorium_l1", 2, 5, {}), ("house_l1", 3, 5, {}), ("construction_site", 6, 5, {}), ("woodcutter_l2", 0, 0, {"logs": 2}),
    ("sawmill_l2", 6, 0, {"logs": 2, "planks": 3}), ("warehouse_l2", 7, 6, {"logs": 3, "planks": 2}),
]
ROADS = {(i, 2): "road_l2" if 2 <= i <= 5 else "road_l1" for i in range(8)}
ROADS.update({(4, j): "road_l1" for j in range(3, 8)})
ROADS.update({(5, 4): "road_l1", (6, 4): "road_l1", (1, 1): "road_l1", (1, 0): "road_l1", (5, 1): "road_l1", (5, 0): "road_l1"})
NATURE = [("tree_pine", 0, 7, 0), ("tree_round", 1, 7, 30), ("tree_pine", 0, 6, 50), ("bush", 1, 6, 0), ("flowers", 0, 5, 0),
          ("rock", 7, 7, 40), ("stump", 3, 7, 0), ("tree_round", 2, 7, 70), ("flowers", 5, 6, 10), ("bush", 3, 0, 20),
          ("tree_pine", 2, 0, 15), ("rock", 3, 1, 0), ("stump", 7, 1, 0), ("flowers", 6, 7, 0)]
WALKERS = [("courier", 3.1, 2.0, 90, "carry_planks"), ("courier", 4.0, 5.3, 0, "carry_logs"), ("worker", 0.6, 2.05, 270, None),
           ("courier", 6.3, 2.0, 270, None)]


def town_scene():
    lib.clear_scene()
    tiles = {}
    for i in range(8):
        for j in range(8):
            if (i, j) in ROADS:
                root = ba.place(ac.ROADS[ROADS[(i, j)]], at_grid(i, j))
                keep = {name for name, (dx, dy) in (("Road_XPlus", (1, 0)), ("Road_XMinus", (-1, 0)), ("Road_YPlus", (0, 1)),
                                                     ("Road_YMinus", (0, -1))) if (i + dx, j + dy) in ROADS}
                show_arms(root, keep)
                tiles[i, j] = root
            else:
                tiles[i, j] = ba.place(ac.TILES[GRASS[ground_variant(i, j)]], at_grid(i, j))
    ground = lib.Model()
    ground.box((16, 16, 0.3), (0, 0, -0.4), "grass_dark", bevel=0)
    ground.to_object("ground")
    for name, i, j, steps in TOWN:
        root = ba.place(e1.BUILDINGS[name], at_grid(i, j))
        add_fill(root, name, steps)
        lib.pose_at(root, 0.3 + 0.17 * i, 1.0)
    for name, i, j, rot in NATURE:
        p = at_grid(i, j)
        ba.place(ac.ASSETS[name], (p[0] + 0.12, p[1] - 0.08, 0), rot_z=rot)
    for k in range(16):  # a forest edge beyond the grid's far sides
        a = k / 15.0
        for name, x, y in (("tree_pine", 4.6 + 0.5 * (k % 2), -4.4 + 8.8 * a),
                           ("tree_pine" if k % 3 else "tree_round", -4.4 + 8.8 * a, 4.6 + 0.5 * (k % 2))):
            ba.place(ac.ASSETS[name], (x, y, -0.1), rot_z=k * 37)
    for name, gx, gy, rot, load in WALKERS:
        w = ba.place(ac.ASSETS[name], at_grid(gx, gy), rot_z=rot)
        lib.pose_at(w, 0.15 * gx, 1.0)
        if load:
            bpy.context.view_layer.update()
            ld = ba.place(ac.ASSETS[load], ba.anchors(w)["Anchor_Load"].matrix_world.translation)
            ld.rotation_euler[2] = math.radians(rot)
    return tiles


def town():
    tiles = town_scene()
    lib.setup_render(2000, 1400)
    lib.frame_camera([t for t in tiles.values()], margin=0.98)
    lib.render(os.path.join(OUT, "town.png"))
    to_jpeg(os.path.join(OUT, "town.png"))
    near = [t for (i, j), t in tiles.items() if 1 <= i <= 6 and 1 <= j <= 5]
    lib.setup_render(1800, 1100)
    lib.frame_camera(near, margin=0.86)
    lib.render(os.path.join(OUT, "town_closeup.png"))
    to_jpeg(os.path.join(OUT, "town_closeup.png"))


# --- motion ----------------------------------------------------------------------------------------------------------

MOTION = ["woodcutter_l1", "sawmill_l1", "trading_post_l2", "scriptorium_l1", "courier_station_l2", "warehouse_l1",
          "construction_site"]


def motion(frames):
    out = os.path.join(OUT, "motion")
    tmp = os.path.join(out, "_frames")
    os.makedirs(tmp, exist_ok=True)
    sheet_rows = []
    for name in MOTION + ["walkers"]:
        lib.clear_scene()
        if name == "walkers":
            roots = [ba.place(ac.ROADS["road_l1"], (0, 0, 0))]
            show_arms(roots[0], {"Road_XPlus", "Road_XMinus"})
            people = [ba.place(ac.worker, (0.18, -0.08, 0), rot_z=270), ba.place(ac.courier, (-0.18, 0.08, 0), rot_z=90)]
            bpy.context.view_layer.update()
            ba.place(ac.carry_planks, ba.anchors(people[1])["Anchor_Load"].matrix_world.translation, rot_z=90)
            animated, fill, steps_total = people, None, 0
        else:
            ba.place(ac.TILES["tile_grass"], (0, 0, 0))
            root = ba.place(e1.BUILDINGS[name], (0, 0, 0))
            fill = add_fill(root, name, {})
            animated = [root]
            steps_total = 0 if fill is None else max(int(c.name.split(".")[0].split("_")[2]) for c in fill.children)
        lib.setup_render(360, 360, samples=12)
        lib.frame_camera(meshes() + ([c for c in fill.children] if fill else []), margin=1.08)
        paths = []
        for f in range(frames):
            t = f / frames
            for r in animated:
                lib.pose_at(r, t, 1.0, fraction=True)
            if fill is not None:
                show_fill(fill, {item: min(steps_total, f * (steps_total + 1) // frames) for item in ("logs", "planks")})
            path = os.path.join(tmp, f"{name}_{f:02d}.png")
            lib.render(path)
            paths.append(path)
        if shutil.which("convert"):
            subprocess.run(["convert", "-delay", "12", "-loop", "0"] + paths + ["-resize", "80%", "-fuzz", "3%", "-layers", "Optimize",
                            os.path.join(out, name + ".gif")], check=True)
            pick = [paths[k * frames // 8] for k in range(8)]
            row = os.path.join(tmp, f"row_{name}.png")
            subprocess.run(["convert"] + pick + ["+append", "-resize", "50%", row], check=True)
            sheet_rows.append(row)
    if sheet_rows:
        subprocess.run(["convert"] + sheet_rows + ["-append", "-quality", "86", os.path.join(OUT, "motion_sheet.jpg")], check=True)
    shutil.rmtree(tmp, ignore_errors=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="render_previews.py")
    parser.add_argument("--only", nargs="*", default=["lineups", "town", "motion"])
    parser.add_argument("--frames", type=int, default=16)
    args = parser.parse_args(argv)
    lib.load_palette(os.path.join(ba.PACKAGE, "unity", "Art", "Textures", "palette_era1.png"))
    if "lineups" in args.only:
        lineup_buildings()
        lineup_ground()
        lineup_props()
    if "town" in args.only:
        town()
    if "motion" in args.only:
        motion(args.frames)


main()
