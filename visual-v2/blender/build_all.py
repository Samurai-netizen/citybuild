"""Builds every model of visual v2, exports it for Unity, checks it and renders the previews.

    blender -b --factory-startup --python visual-v2/blender/build_all.py -- [--only NAME ...] [--no-render] [--out DIR]

Run from the repository root. `--out` is the folder that stands for the Unity project's Assets/_Project (default: visual-v2/unity,
the package's overlay); the build writes Art/Textures/palette_era1.png and Art/Resources/Models/<set>/<name>.fbx under it. Previews go
to visual-v2/previews/models/ (git-ignored) and the contact sheets to visual-v2/previews/. Exits 1 if a check fails.
"""
import argparse
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

import assets_common  # noqa: E402
import assets_era1  # noqa: E402
import lib  # noqa: E402

PACKAGE = os.path.dirname(HERE)
PREVIEWS = os.path.join(PACKAGE, "previews")
# kind -> (max |x|, |y| from the pivot or None, triangle budget, lowest z: rocks and bushes sit slightly in the ground)
LIMITS = {"building": (0.475, 6000, -0.006), "fill": (0.475, 1500, -0.006), "prop": (None, 1500, -0.03),
          "tile": (None, 1000, -0.1), "road": (None, 2000, -0.1), "fx": (None, 300, -1.0)}
REQUIRED_ANCHORS = ("Anchor_Status", "Anchor_Door")
ITEMS = ("logs", "planks", "coins")


def jobs():
    for name, build in assets_common.TILES.items():
        yield "common", name, build, "tile"
    for name, build in assets_common.ROADS.items():
        yield "common", name, build, "road"
    for name, build in assets_common.ASSETS.items():
        yield "common", name, build, "fx" if name.startswith("fx_") else "prop"
    for name, build in assets_era1.BUILDINGS.items():
        yield "era1", name, build, "building"
    for name, build in assets_era1.FILLS.items():
        yield "era1", name, build, "fill"
    for name, build in assets_era1.PROPS.items():
        yield "era1", name, build, "prop"


def footprint_problem(lo, hi, reach):
    if reach is not None and max(abs(lo.x), abs(lo.y), hi.x, hi.y) > reach:
        return f"footprint {lo.x:.3f}..{hi.x:.3f} x {lo.y:.3f}..{hi.y:.3f} leaves the tile"
    return None


def check(name, kind, root):
    """Bounds (with every moving part at rest and along its motion), floor, triangles, material, and the structure rules."""
    reach, budget, floor = LIMITS[kind]
    lo, hi, tris, mats = lib.stats(root)
    problems = []
    for t in (0.0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875):  # every moving part along its cycle
        if not lib.moving_parts(root):
            break
        lib.pose_at(root, t, 1.0, fraction=True)
        plo, phi, _, _ = lib.stats(root)
        problem = footprint_problem(plo, phi, reach)
        if problem:
            problems.append(f"in motion (t = {t:.3f} of a cycle): {problem}")
            break
        if plo.z < floor - 1e-4:
            problems.append(f"in motion (t = {t:.3f} of a cycle) goes below the ground (z = {plo.z:.3f})")
            break
    lib.pose_at(root, 0.0, 0.0)
    problem = footprint_problem(lo, hi, reach)
    if problem:
        problems.append(problem)
    if lo.z < floor - 1e-4:
        problems.append(f"goes below the ground (z = {lo.z:.3f})")
    if tris > budget:
        problems.append(f"{tris} triangles > budget {budget}")
    if mats != {lib.MATERIAL_NAME}:
        problems.append(f"materials {sorted(mats)}")
    problems += structure_problems(kind, root)
    size = hi - lo
    print(f"  {name:24} {tris:5d} tris  {size.x:.2f} x {size.y:.2f} x {size.z:.2f}  " + ("; ".join(problems) or "ok"))
    return [f"{name}: {p}" for p in problems]


def structure_problems(kind, root):
    names = {o.name for o in lib.descendants(root)}
    out = []
    poses = {n for n in names if n.endswith(lib.POSE_SUFFIX)}
    parts = {p.name for p, _, _ in lib.moving_parts(root)}
    for pose in poses:
        part = pose[: -len(lib.POSE_SUFFIX)]
        if part not in parts:
            out.append(f"{pose} has no part {part}")
        elif lib.motion_of(part) is None:
            out.append(f"part {part} has no MOTIONS row")
    if kind == "building":
        for a in REQUIRED_ANCHORS:
            if a not in names:
                out.append(f"no {a}")
        for obj in lib.descendants(root)[1:]:
            if obj.type == "MESH" and obj.name not in parts:
                out.append(f"mesh child {obj.name} is not a moving part (no pose)")
    if kind == "fill":
        if root.type != "EMPTY":
            out.append("a fill model's root must be an empty")
        steps = {}
        for obj in root.children:
            bits = obj.name.split("_")
            if obj.type != "MESH" or len(bits) != 3 or bits[0] != "Fill" or bits[1] not in ITEMS or not bits[2].isdigit():
                out.append(f"child {obj.name} is not Fill_<item>_<step>")
                continue
            steps.setdefault(bits[1], []).append(int(bits[2]))
        for item, ks in steps.items():
            if sorted(ks) != list(range(1, len(ks) + 1)) or not 1 <= len(ks) <= 3:
                out.append(f"{item} steps {sorted(ks)} are not 1..n with n in 1..3")
    if kind == "tile":
        top = max((v.co.z for v in root.data.vertices), default=0.0) if root.type == "MESH" else 1.0
        if top > 0.01:
            out.append(f"the tile's own mesh reaches z = {top:.3f}: what rises above the ground belongs in its Details child")
    if kind == "road":
        arms = {o.name for o in root.children}
        if arms != set(assets_common.ARMS):
            out.append(f"arms {sorted(arms)} are not {sorted(assets_common.ARMS)}")
    return out


def place(build, location, rot_z=0.0):
    root = build()
    root.location = location
    root.rotation_euler[2] = math.radians(rot_z)
    return root


def anchors(root):
    return {c.name.split(".")[0]: c for c in root.children if c.type == "EMPTY"}


def dress(root, base, level, fill=True):
    """Preview-only extras: the building's full store (every fill step) and a worker at the door."""
    a = anchors(root)
    bpy.context.view_layer.update()
    fill_name = f"{base}_l{level}_fill"
    if fill and fill_name in assets_era1.FILLS:
        f = assets_era1.FILLS[fill_name]()
        f.parent = root
    if "Anchor_Door" in a:
        p = a["Anchor_Door"].matrix_world.translation.copy()
        p.x += 0.07
        place(assets_common.worker, p, rot_z=200)


def preview(path, size=(512, 512), direction=(1.0, 1.0, 1.25), frame=None, margin=1.12):
    lib.setup_render(*size)
    objects = frame or [o for o in bpy.context.scene.objects if o.type == "MESH"]
    lib.frame_camera(objects, direction=direction, margin=margin)
    lib.render(path)


def split_name(name):
    base, _, level = name.rpartition("_l")
    return (base, int(level)) if level.isdigit() else (name, 1)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(prog="build_all.py")
    parser.add_argument("--only", nargs="*", help="model names to build (default: all)")
    parser.add_argument("--no-render", action="store_true", help="skip every preview render")
    parser.add_argument("--out", default=os.path.join(PACKAGE, "unity"), help="the folder that stands for Assets/_Project")
    args = parser.parse_args(argv)

    art = os.path.join(args.out, "Art")
    lib.load_palette(os.path.join(art, "Textures", "palette_era1.png"))
    problems = []
    print("Models:")
    for folder, name, build, kind in jobs():
        if args.only and name not in args.only:
            continue
        lib.clear_scene()
        root = build()
        problems += check(name, kind, root)
        lib.export_fbx(root, os.path.join(art, "Resources", "Models", folder, name + ".fbx"))
        if not args.no_render:
            if kind in ("building", "fill", "prop"):
                place(assets_common.TILES["tile_grass"], (0, 0, 0))
            if kind == "building":
                dress(root, *split_name(name))
            preview(os.path.join(PREVIEWS, "models", name + ".png"))
    if problems:
        print("FAILED:\n  " + "\n  ".join(problems))
        sys.exit(1)
    print("All checks passed.")


if __name__ == "__main__":
    main()
