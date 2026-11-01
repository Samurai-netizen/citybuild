"""Builds every model, exports it for Unity, checks it and renders the previews.

    blender -b --factory-startup --python art/blender/build_all.py -- [--only NAME ...] [--no-render]

Run from the repository root. Output: in `art/` (the folder above this script) textures/palette_era1.png, previews/<name>.png,
previews/lineup_era1.png and previews/city_golden_scenario.png; in the Unity project (UNITY_ART) Resources/Models/<set>/<name>.fbx and a copy of
the palette in Textures/. The models exist only there: one copy. Exits 1 if a check fails.
"""
import argparse
import math
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import assets_common  # noqa: E402
import assets_era1  # noqa: E402
import lib  # noqa: E402

ROOT = os.path.dirname(HERE)
# Where the Unity project keeps the art (client/CityBuilder/Assets/_Project/Art): the only home of the FBX files, in Resources/Models
# so a player build contains them and the client loads them by name (Resources.Load)
UNITY_ART = os.path.join(os.path.dirname(ROOT), "client", "CityBuilder", "Assets", "_Project", "Art")
# kind -> (max |x|, |y| from the pivot, triangle budget, lowest z: rocks and bushes sit slightly in the ground)
LIMITS = {"building": (0.475, 4500, -0.006), "prop": (None, 1500, -0.025), "tile": (None, 200, -0.1)}


def jobs():
    for name, build in assets_common.ASSETS.items():
        yield "common", name, build, "tile" if name.startswith("tile_") else "prop"
    for name, build in assets_era1.BUILDINGS.items():
        yield "era1", name, build, "building"
    for name, build in assets_era1.PROPS.items():
        yield "era1", name, build, "prop"


def check(name, kind, root):
    lo, hi, tris, mats = lib.stats(root)
    reach, budget, floor = LIMITS[kind]
    problems = []
    if reach is not None and max(abs(lo.x), abs(lo.y), hi.x, hi.y) > reach:
        problems.append(f"footprint {lo.x:.3f}..{hi.x:.3f} x {lo.y:.3f}..{hi.y:.3f} leaves the tile")
    if lo.z < floor - 1e-4:
        problems.append(f"goes below the ground (z = {lo.z:.3f})")
    if tris > budget:
        problems.append(f"{tris} triangles > budget {budget}")
    if mats != {lib.MATERIAL_NAME}:
        problems.append(f"materials {sorted(mats)}")
    size = hi - lo
    print(f"  {name:22} {tris:5d} tris  {size.x:.2f} x {size.y:.2f} x {size.z:.2f}  " + ("; ".join(problems) or "ok"))
    return [f"{name}: {p}" for p in problems]


def check_stockpile(name, root):
    """The largest stockpile prop, placed at the building's Anchor_Stockpile, must stay inside the tile too."""
    stock = assets_era1.PREVIEW_STOCK.get(name.rsplit("_l", 1)[0])
    a = anchors(root)
    if not stock or "Anchor_Stockpile" not in a:
        return []
    bpy.context.view_layer.update()
    big = stock[:-1] + "l"
    prop = place(assets_era1.PROPS[big], a["Anchor_Stockpile"].matrix_world.translation)
    lo, hi, _, _ = lib.stats(prop)
    for obj in lib.descendants(prop):
        bpy.data.objects.remove(obj, do_unlink=True)
    reach = LIMITS["building"][0]
    if max(abs(lo.x), abs(lo.y), hi.x, hi.y) > reach:
        print(f"  {name:22} {big} at the anchor leaves the tile: {lo.x:.3f}..{hi.x:.3f} x {lo.y:.3f}..{hi.y:.3f}")
        return [f"{name}: {big} at Anchor_Stockpile leaves the tile"]
    return []


def place(build, location, rot_z=0.0):
    root = build()
    root.location = location
    root.rotation_euler[2] = math.radians(rot_z)
    return root


def anchors(root):
    return {c.name.split(".")[0]: c for c in root.children if c.type == "EMPTY"}


def dress(root, base):
    """Preview-only extras: the medium stockpile at Anchor_Stockpile and a worker at the door."""
    a = anchors(root)
    stock = assets_era1.PREVIEW_STOCK.get(base)
    bpy.context.view_layer.update()
    if stock and "Anchor_Stockpile" in a:
        place(assets_era1.PROPS[stock], a["Anchor_Stockpile"].matrix_world.translation)
    if "Anchor_Door" in a:
        p = a["Anchor_Door"].matrix_world.translation.copy()
        p.x += 0.07
        place(assets_common.worker, p, rot_z=200)


def preview(path, size=(512, 512), direction=(1.0, 1.0, 1.25), frame=None, margin=1.12):
    lib.setup_render(*size)
    objects = frame or [o for o in bpy.context.scene.objects if o.type == "MESH"]
    lib.frame_camera(objects, direction=direction, margin=margin)
    lib.render(path)


def lineup():
    lib.clear_scene()
    tile = assets_common.ASSETS["tile_grass"]
    names = ["house", "woodcutter", "sawmill", "trading_post"]
    for col, base in enumerate(names):
        for row, level in enumerate((2, 1)):
            x, y = col * 1.0, row * 1.0
            place(tile, (x, y, 0))
            root = place(assets_era1.BUILDINGS[f"{base}_l{level}"], (x, y, 0))
            dress(root, base)
    place(tile, (4.0, 1.0, 0))
    place(assets_era1.construction_site, (4.0, 1.0, 0))
    place(tile, (4.0, 0.0, 0))
    for i, prop in enumerate(("prop_logs_s", "prop_logs_m", "prop_logs_l")):
        place(assets_era1.PROPS[prop], (3.75 + i * 0.25, -0.22, 0), rot_z=90)
    for i, prop in enumerate(("prop_planks_s", "prop_planks_m", "prop_planks_l")):
        place(assets_era1.PROPS[prop], (3.72 + i * 0.28, 0.2, 0), rot_z=90)
    preview(os.path.join(ROOT, "previews", "lineup_era1.png"), size=(1600, 900), margin=1.06)


# Golden scenario #1 (core-instructions/23-mvp-content.md): 2 Cottages, a Woodcutter's Hut, a Sawmill and a Trading
# Post on the 8 x 8 grid, plus one construction site, paths and nature. Tile (i, j) -> (i - 3.5, j - 3.5).
CITY = [
    ("trading_post_l1", 1, 5), ("sawmill_l1", 3, 5), ("construction_site", 5, 5), ("woodcutter_l1", 6, 5),
    ("house_l1", 2, 3), ("house_l1", 5, 3),
]
NATURE = [
    ("tree_pine", 0, 0, 0), ("tree_round", 1, 0, 30), ("tree_pine", 7, 0, 10), ("tree_pine", 7, 1, 50),
    ("tree_round", 7, 3, 0), ("bush", 0, 2, 0), ("rock", 6, 0, 40), ("bush", 3, 1, 70), ("rock", 0, 7, 0),
    ("bush", 7, 7, 20),
]


def city_scene():
    """Builds the golden scenario village. Returns its tiles by (i, j) and its buildings as (name, root)."""
    lib.clear_scene()
    tiles, buildings = {}, []
    for i in range(8):
        for j in range(8):
            path = j == 6 or (i == 4 and j <= 5)
            tiles[i, j] = place(assets_common.ASSETS["tile_dirt" if path else "tile_grass"], (i - 3.5, j - 3.5, 0))
    ground = lib.Model()
    ground.box((16, 16, 0.3), (0, 0, -0.4), "grass_dark", bevel=0)
    ground.to_object("ground")
    for name, i, j in CITY:
        root = place(assets_era1.BUILDINGS[name], (i - 3.5, j - 3.5, 0))
        dress(root, name.rsplit("_l", 1)[0])
        buildings.append((name, root))
    for name, i, j, rot in NATURE:
        place(assets_common.ASSETS[name], (i - 3.5 + 0.1, j - 3.5 - 0.05, 0), rot_z=rot)
    for k in range(14):  # a forest edge on the far (-X, -Y) sides, on the lower ground
        a = k / 13.0
        for name, x, y in (("tree_pine", -4.6 - 0.5 * (k % 2), -4.4 + 8.8 * a),
                           ("tree_pine" if k % 3 else "tree_round", -4.4 + 8.8 * a, -4.6 - 0.5 * (k % 2))):
            place(assets_common.ASSETS[name], (x, y, -0.1), rot_z=k * 37)
    for x, y, rot in ((-0.5, 2.5, 90), (1.2, 2.55, 270), (0.55, 0.3, 180), (2.0, 2.6, 80)):
        place(assets_common.worker, (x, y, 0), rot_z=rot)
    return tiles, buildings


def city():
    tiles, _ = city_scene()
    out = os.path.join(ROOT, "previews")
    preview(os.path.join(out, "city_golden_scenario.png"), size=(1600, 1100), frame=list(tiles.values()), margin=1.0)
    # what a phone shows when zoomed in on the village (a 3/4 view, about 5 x 3 tiles)
    near = [t for (i, j), t in tiles.items() if 1 <= i <= 6 and 3 <= j <= 6]
    preview(os.path.join(out, "city_closeup.png"), size=(1600, 1000), frame=near, margin=0.9)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="build_all.py")
    parser.add_argument("--only", nargs="*", help="model names to build, or 'lineup' / 'city' (default: all)")
    parser.add_argument("--no-render", action="store_true", help="skip every preview render")
    args = parser.parse_args(argv)

    palette_path = os.path.join(ROOT, "textures", "palette_era1.png")
    lib.load_palette(palette_path)
    unity_palette = os.path.join(UNITY_ART, "Textures", "palette_era1.png")
    os.makedirs(os.path.dirname(unity_palette), exist_ok=True)
    shutil.copyfile(palette_path, unity_palette)
    problems = []
    print("Models:")
    for folder, name, build, kind in jobs():
        if args.only and name not in args.only:
            continue
        lib.clear_scene()
        root = build()
        problems += check(name, kind, root)
        if kind == "building":
            problems += check_stockpile(name, root)
        lib.export_fbx(root, os.path.join(UNITY_ART, "Resources", "Models", folder, name + ".fbx"))
        if not args.no_render:
            if kind != "tile":
                place(assets_common.ASSETS["tile_grass"], (0, 0, 0))
            if kind == "building":
                dress(root, name.rsplit("_l", 1)[0])
            preview(os.path.join(ROOT, "previews", name + ".png"))
    if not args.no_render:
        for name, scene in (("lineup", lineup), ("city", city)):
            if not args.only or name in args.only:
                scene()
    if problems:
        print("FAILED:\n  " + "\n  ".join(problems))
        sys.exit(1)
    print("All checks passed.")


if __name__ == "__main__":
    main()
