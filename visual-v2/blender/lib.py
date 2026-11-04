"""Geometry helpers, moving parts, store fill, export and preview rendering for the scripted art (visual v2).

Conventions (Blender space; the FBX export converts to Unity), as in art/blender/lib.py:
- 1 Blender unit = 1 m = 1 Unity unit. One grid tile is 1 x 1. The pivot is the tile centre on the ground (z = 0).
- The front of a building faces Blender +Y, which is Unity -Z: toward a camera that looks along Unity +Z.
  The second side seen by the 3/4 camera is Blender +X (Unity -X). Put the details there.
- Every face takes one palette colour (see palette.py); every model has one material.
New in v2:
- A moving part is its own mesh object, a child of the model's root, with its origin on its joint (axle, hinge, hip). Beside it the
  root carries an empty "<Part>_Pose": where the part is at the far end of its motion. The client moves the part between its own
  transform and the pose, or spins it around the axis between them, by the part's row in MOTIONS. Part and pose go through the same
  FBX axis conversion, so neither the client nor this file writes a Unity axis.
- Store fill is a model of its own: an empty root with one mesh child per item and step, "Fill_<item>_<step>", in building space.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

import palette

MATERIAL_NAME = "M_Palette_Era1"
POSE_SUFFIX = "_Pose"

# Moving parts by base name: (kind, seconds per cycle, phase as a fraction of a cycle). A part may carry a number after its base name
# ("Flag_2"). The client keeps the same rows (PartMotions in the overlay); MOTIONS.md says what each kind does.
#   spin      turns all the way round the axis from the part to its pose (the pose is a quarter turn), one turn per cycle
#   chop      rises slowly to the pose and drops back fast, then rests a moment (an axe, a hammer)
#   pingpong  glides to the pose and back (a saw stroke, a hoist)
#   wave      swings to the pose, back through rest to the mirror of the pose, and back (a flag, legs and arms)
MOTIONS = {
    "Wheel": ("spin", 4.0, 0.0),
    "Sign": ("spin", 3.0, 0.0),
    "Vane": ("spin", 7.0, 0.0),
    "Axe": ("chop", 1.4, 0.0),
    "Hammer": ("chop", 0.8, 0.0),
    "Saw": ("pingpong", 0.9, 0.0),
    "Hoist": ("pingpong", 5.0, 0.0),
    "Quill": ("wave", 1.1, 0.0),
    "Flag": ("wave", 1.6, 0.0),
    "Leg_L": ("wave", 0.6, 0.0), "Leg_R": ("wave", 0.6, 0.5),
    "Arm_L": ("wave", 0.6, 0.5), "Arm_R": ("wave", 0.6, 0.0),
}


def motion_of(part_name):
    """The MOTIONS row of a part name ("Flag_2" -> the "Flag" row), or None."""
    if part_name in MOTIONS:
        return MOTIONS[part_name]
    base, _, tail = part_name.rpartition("_")
    return MOTIONS.get(base) if tail.isdigit() else None


def motion_weight(kind, t):
    """How far a part is along its motion at cycle position t (any real number): 1 = at the pose; spin returns turns."""
    f = t - math.floor(t)
    if kind == "spin":
        return t
    if kind == "chop":
        if f < 0.62:  # rise, easing in and out
            u = f / 0.62
            return u * u * (3 - 2 * u)
        if f < 0.74:  # the blow: accelerating back to rest
            u = (f - 0.62) / 0.12
            return 1 - u * u
        return 0.0
    if kind == "pingpong":
        return 0.5 - 0.5 * math.cos(2 * math.pi * f)
    if kind == "wave":
        return math.sin(2 * math.pi * f)
    raise ValueError(kind)


# --- geometry ---------------------------------------------------------------------------------------------------

def _rot(rot):
    return Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()


class Model:
    """Collects coloured parts and turns them into one flat-shaded mesh, plus moving parts with their poses."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.colors = []
        self.parts = []  # (name, Model, pivot, pose rotation in degrees, pose offset)

    # Each shape builds a small bmesh around the origin, colours its faces, then moves it into place.
    def _commit(self, bm, colors, matrix):
        bmesh.ops.transform(bm, matrix=matrix, verts=bm.verts)
        bm.verts.index_update()
        base = len(self.verts)
        self.verts += [tuple(v.co) for v in bm.verts]
        for f, c in zip(bm.faces, colors):
            self.faces.append([base + v.index for v in f.verts])
            self.colors.append(c)
        bm.free()

    @staticmethod
    def _bevel(bm, width):
        if width > 0:
            bmesh.ops.bevel(bm, geom=list(bm.edges) + list(bm.verts), offset=width, offset_type="OFFSET",
                            segments=1, profile=0.5, affect="EDGES", clamp_overlap=True)
        bm.normal_update()

    def box(self, size, at, color, rot=(0, 0, 0), bevel=0.01, anchor="bottom"):
        """A box of `size` (x, y, z). `at` is the centre of its bottom face (anchor 'bottom') or its centre."""
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((*size, 1.0)))
        self._bevel(bm, 0.0 if min(size) < 0.018 else min(bevel, min(size) * 0.3))
        lift = size[2] / 2 if anchor == "bottom" else 0.0
        m = Matrix.Translation(Vector(at) + Vector((0, 0, lift))) @ _rot(rot)
        self._commit(bm, [color] * len(bm.faces), m)

    def cyl(self, r, length, at, color, axis="Z", seg=8, cap=None, r2=None, rot=(0, 0, 0), anchor=None, bevel=0.0):
        """A cylinder (or cone with r2) along `axis`. Z cylinders sit on `at`; X and Y ones are centred on it.
        `cap` colours the end faces (cut log ends)."""
        bm = bmesh.new()
        top = r if r2 is None else r2
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=max(top, 1e-4),
                              depth=length)
        if top <= 1e-4:
            bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=2e-4)
        self._bevel(bm, bevel)
        z = Vector((0, 0, 1))
        colors = [cap if cap and abs(f.normal.dot(z)) > 0.9 else color for f in bm.faces]
        orient = {"Z": (0, 0, 0), "X": (0, 90, 0), "Y": (90, 0, 0)}[axis]
        anchor = anchor or ("bottom" if axis == "Z" else "center")
        lift = length / 2 if anchor == "bottom" else 0.0
        m = Matrix.Translation(Vector(at) + Vector((0, 0, lift))) @ _rot(rot) @ _rot(orient)
        self._commit(bm, colors, m)

    def ico(self, r, at, color, scale=(1, 1, 1), sub=1, rot=(0, 0, 0)):
        """A low-poly blob (foliage, rocks, sacks), centred on `at`."""
        bm = bmesh.new()
        bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=r)
        bm.normal_update()
        m = Matrix.Translation(at) @ _rot(rot) @ Matrix.Diagonal((*scale, 1.0))
        self._commit(bm, [color] * len(bm.faces), m)

    def prism(self, outline, depth, at, color, axis="Y", rot=(0, 0, 0), bevel=0.0, cap=None):
        """A flat shape extruded `depth` along `axis`, centred on `at`. `outline` lists (u, v) points counter-clockwise: for
        axis Y, u is X and v is Z (a gable wall, a sign); for axis X, u is Y and v is Z; for axis Z, u is X and v is Y. `cap`
        colours the two flat faces."""
        bm = bmesh.new()
        h = depth / 2
        lo = [bm.verts.new((u, v, -h)) for u, v in outline]
        hi = [bm.verts.new((u, v, h)) for u, v in outline]
        bm.faces.new(list(reversed(lo)))
        bm.faces.new(hi)
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self._bevel(bm, bevel)
        flat = Vector((0, 0, 1))
        colors = [cap if cap and abs(f.normal.dot(flat)) > 0.95 else color for f in bm.faces]
        orient = {"Z": Matrix.Identity(4), "Y": _rot((90, 0, 0)), "X": _rot((90, 0, 90))}[axis]
        self._commit(bm, colors, Matrix.Translation(at) @ _rot(rot) @ orient)

    def gable(self, length, width, height, at, color, axis="X", bevel=0.012):
        """A solid gable roof: a triangular prism with its ridge along `axis`, base centred on `at`."""
        bm = bmesh.new()
        l2, w2 = length / 2, width / 2
        v = [bm.verts.new(p) for p in ((-l2, -w2, 0), (l2, -w2, 0), (l2, w2, 0), (-l2, w2, 0),
                                       (-l2, 0, height), (l2, 0, height))]
        for idx in ((0, 1, 2, 3), (0, 1, 5, 4), (2, 3, 4, 5), (0, 4, 3), (1, 2, 5)):
            bm.faces.new([v[i] for i in idx])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        self._bevel(bm, bevel)
        rot = (0, 0, 0) if axis == "X" else (0, 0, 90)
        self._commit(bm, [color] * len(bm.faces), Matrix.Translation(at) @ _rot(rot))

    def slope_strip(self, length, width, height, at, side, t0, t1, color, lift=0.006, thick=0.014, axis="X"):
        """A board lying on one slope of a gable(): from t0 to t1 of the way up (0 = eave, 1 = ridge), on side +1 (+Y) or -1."""
        a = math.atan2(height, width / 2)
        slope = math.hypot(height, width / 2)
        tm = (t0 + t1) / 2
        normal = Vector((0, side * math.sin(a), math.cos(a)))
        p = Vector((0, side * (width / 2) * (1 - tm), height * tm)) + normal * (lift + thick / 2)
        m = Matrix.Translation(at) @ _rot((0, 0, 0) if axis == "X" else (0, 0, 90))
        self._strip(length, slope * (t1 - t0), thick, m @ Matrix.Translation(p) @ _rot((-side * math.degrees(a), 0, 0)), color)

    def _strip(self, sx, sy, sz, matrix, color):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((sx, sy, sz, 1.0)))
        self._bevel(bm, min(0.005, sz * 0.3))
        self._commit(bm, [color] * len(bm.faces), matrix)

    def roof_bands(self, length, width, height, at, color, rows=2, axis="X"):
        """Raised strips across both slopes of a gable(): thatch layers or shingle rows."""
        for i in range(rows):
            t = (i + 0.6) / (rows + 0.6)
            for side in (1, -1):
                self.slope_strip(length + 0.014, width, height, at, side, t - 0.08, t + 0.08, color, lift=0.0, axis=axis)

    def decal(self, outline, color, z=0.002, at=(0, 0)):
        """A flat shape lying on the ground (a patch of darker grass, a rut), one face looking up."""
        bm = bmesh.new()
        vs = [bm.verts.new((at[0] + u, at[1] + v, z)) for u, v in outline]
        face = bm.faces.new(vs)
        bm.normal_update()
        if face.normal.z < 0:
            face.normal_flip()
        self._commit(bm, [color], Matrix.Identity(4))

    def blade(self, at, h, color, r=0.012, lean=(0, 0), seg=3):
        """A grass blade or a reed: a thin three-sided cone, leaning by `lean` degrees (x, y)."""
        self.cyl(r, h, at, color, seg=seg, r2=0.0, rot=(lean[0], lean[1], 0))

    def tuft(self, at, color="grass_dark", tip="grass_light", h=0.05, spin=0.0):
        """A tuft of five blades fanning out from one point, the middle ones taller and lighter."""
        x, y, z = at
        for k, (dx, dy, hh, c) in enumerate(((0, 0, 1.0, tip), (0.012, 0.004, 0.8, color), (-0.011, 0.006, 0.75, color),
                                               (0.004, -0.012, 0.7, color), (-0.004, 0.012, 0.85, tip))):
            ang = math.radians(spin + k * 72)
            lean = (16 * math.cos(ang) if k else 0, 16 * math.sin(ang) if k else 0)
            self.blade((x + dx * 1.3, y + dy * 1.3, z), h * hh, c, r=0.015, lean=lean)

    def part(self, name, pivot, pose_rot=(0, 0, 0), pose_offset=(0, 0, 0)):
        """A moving part: returns a new Model drawn around its own joint (local coordinates, joint at the origin), placed at
        `pivot` in this model. Its pose is the part turned by `pose_rot` degrees (x, y, z, in the part's frame) and moved by
        `pose_offset`. Requires a MOTIONS row for `name`."""
        if motion_of(name) is None:
            raise ValueError(f"no MOTIONS row for part {name}")
        sub = Model()
        self.parts.append((name, sub, Vector(pivot), pose_rot, Vector(pose_offset)))
        return sub

    def tris(self):
        return sum(len(f) - 2 for f in self.faces) + sum(p[1].tris() for p in self.parts)

    def mesh(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.validate(clean_customdata=False)
        uv = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            u, v = palette.uv_of(self.colors[poly.index])
            for li in poly.loop_indices:
                uv.data[li].uv = (u, v)
            poly.use_smooth = False
        mesh.materials.append(material())
        return mesh

    def to_object(self, name, parent=None, location=(0, 0, 0)):
        """The model as a mesh object; each moving part becomes a mesh child at its pivot with a "<part>_Pose" empty beside it."""
        obj = bpy.data.objects.new(name, self.mesh(name))
        bpy.context.scene.collection.objects.link(obj)
        obj.location = location
        if parent:
            obj.parent = parent
        for pname, sub, pivot, pose_rot, pose_offset in self.parts:
            child = sub.to_object(pname, parent=obj, location=pivot)
            pose = anchor(pname + POSE_SUFFIX, obj, pivot + pose_offset)
            pose.rotation_euler = Euler([math.radians(a) for a in pose_rot], "XYZ")
        return obj


def blob(rng, r, n=9, wobble=0.22, stretch=(1.0, 1.0), turn=0.0):
    """The outline of an irregular round patch: n points counter-clockwise around the origin, each radius r +- wobble."""
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n + math.radians(turn)
        rr = r * (1 + rng.uniform(-wobble, wobble))
        pts.append((rr * math.cos(a) * stretch[0], rr * math.sin(a) * stretch[1]))
    return pts


def anchor(name, parent, location):
    """An empty the client looks up by name (status bubble, door, stockpile, smoke, a part's pose)."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.05
    e.location = location
    e.parent = parent
    bpy.context.scene.collection.objects.link(e)
    return e


def group(name, location=(0, 0, 0)):
    """An empty root for a model made of several meshes (store fill, road arms)."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.1
    e.location = location
    bpy.context.scene.collection.objects.link(e)
    return e


# --- scene, material ----------------------------------------------------------------------------------------------

_palette_image = None


def load_palette(path):
    global _palette_image
    palette.write_png(path)
    _palette_image = bpy.data.images.load(path, check_existing=True)
    return _palette_image


def material():
    mat = bpy.data.materials.get(MATERIAL_NAME)
    if mat:
        return mat
    mat = bpy.data.materials.new(MATERIAL_NAME)
    try:
        mat.use_nodes = True
    except (AttributeError, TypeError):
        pass
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = _palette_image
    tex.interpolation = "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def clear_scene():
    _REST.clear()
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for data in (bpy.data.cameras, bpy.data.lights):
        for item in list(data):
            data.remove(item)


def scale_model(root, s):
    """Scales a finished model in place around its pivot: the meshes' vertices and the children's positions,
    so every transform stays at scale 1 for Unity."""
    for obj in descendants(root):
        if obj.type == "MESH":
            obj.data.transform(Matrix.Scale(s, 4))
        if obj is not root:
            obj.location = obj.location * s
    return root


def descendants(root):
    out = [root]
    for child in root.children:
        out += descendants(child)
    return out


def moving_parts(root):
    """(part, pose empty, MOTIONS row) for every moving part under `root`. A part and its pose are siblings; Blender's ".001"
    suffixes on copies are ignored, so a scene may hold several instances of one model."""
    out = []
    for obj in descendants(root):
        if obj.type != "MESH" or obj.parent is None:
            continue
        base = obj.name.split(".")[0]
        row = motion_of(base)
        if row is None:
            continue
        for sib in obj.parent.children:
            if sib.type == "EMPTY" and sib.name.split(".")[0] == base + POSE_SUFFIX:
                out.append((obj, sib, row))
                break
    return out


_REST = {}


def pose_at(root, t, intensity=1.0, fraction=False):
    """Puts every moving part where the client would draw it at time t seconds (preview only): the same formula as the client.
    With fraction=True, t is a fraction of each part's own cycle instead (the checks sample every part along its motion)."""
    for part, pose, row in moving_parts(root):
        kind, period, phase = row
        key = part.as_pointer()
        if key not in _REST:
            _REST[key] = part.matrix_basis.copy()
        r_loc, r_rot, _ = _REST[key].decompose()
        p_loc, p_rot, _ = pose.matrix_basis.decompose()
        delta = r_rot.inverted() @ p_rot
        axis, angle = delta.to_axis_angle()
        w = motion_weight(kind, (t if fraction else t / period) + phase)
        if kind == "spin":
            turn = 2 * math.pi * w * intensity
            rot = r_rot @ Quaternion(axis, turn)
            loc = r_loc
        else:
            rot = r_rot @ Quaternion(axis, angle * w * intensity)
            loc = r_loc.lerp(p_loc, w * intensity) if w >= 0 else r_loc + (p_loc - r_loc) * (w * intensity)
        part.matrix_basis = Matrix.LocRotScale(loc, rot, Vector((1, 1, 1)))


# --- checks and export --------------------------------------------------------------------------------------------

def stats(root, meshes_only=None):
    """World-space bounds, triangle count and material count of a model and its children."""
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    tris = 0
    mats = set()
    bpy.context.view_layer.update()
    for obj in descendants(root):
        if obj.type != "MESH" or (meshes_only and obj.name not in meshes_only):
            continue
        for v in obj.data.vertices:
            w = obj.matrix_world @ v.co
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
        tris += sum(len(p.vertices) - 2 for p in obj.data.polygons)
        mats |= {m.name for m in obj.data.materials if m}
    return lo, hi, tris, mats


def export_fbx(root, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False,
        bake_anim=False, path_mode="STRIP", embed_textures=False, use_custom_props=False)


# --- preview rendering --------------------------------------------------------------------------------------------

BACKGROUND = (0.80, 0.88, 0.93)


def setup_render(width, height, transparent=False, samples=24):
    scene = bpy.context.scene
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = transparent
    scene.render.image_settings.color_mode = "RGBA" if transparent else "RGB"
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    if hasattr(scene, "eevee"):
        try:
            scene.eevee.taa_render_samples = samples
        except AttributeError:
            pass
    world = scene.world or bpy.data.worlds.new("World")
    scene.world = world
    try:
        world.use_nodes = True
    except (AttributeError, TypeError):
        pass
    # The camera sees a flat sky colour; the models get a softer, neutral ambient light.
    nt = world.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputWorld")
    sky, fill = nt.nodes.new("ShaderNodeBackground"), nt.nodes.new("ShaderNodeBackground")
    sky.inputs["Color"].default_value = (*BACKGROUND, 1.0)
    fill.inputs["Color"].default_value = (0.9, 0.93, 1.0, 1.0)
    fill.inputs["Strength"].default_value = 0.6
    path, mix = nt.nodes.new("ShaderNodeLightPath"), nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(path.outputs["Is Camera Ray"], mix.inputs[0])
    nt.links.new(fill.outputs[0], mix.inputs[1])
    nt.links.new(sky.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])

    # Warm key light from the camera's left (+X) and above; shadows fall away from the viewer. One per scene.
    for obj in [o for o in scene.objects if o.type in ("LIGHT", "CAMERA")]:
        bpy.data.objects.remove(obj, do_unlink=True)
    sun_data = bpy.data.lights.new("Sun", type="SUN")
    sun_data.energy = 3.6
    sun_data.angle = math.radians(6)
    sun_data.color = (1.0, 0.94, 0.84)
    sun = bpy.data.objects.new("Sun", sun_data)
    sun.rotation_euler = Vector((-1.0, -0.25, -1.35)).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(sun)


def frame_camera(objects, direction=(1.0, 1.0, 1.25), margin=1.12, name="Camera"):
    """An orthographic 3/4 camera from `direction` that fits every vertex of `objects`."""
    scene = bpy.context.scene
    bpy.context.view_layer.update()
    pts = []
    for obj in objects:
        if obj.type == "MESH":
            pts += [obj.matrix_world @ v.co for v in obj.data.vertices]
    centre = sum(pts, Vector()) / len(pts)
    d = Vector(direction).normalized()
    rot = (-d).to_track_quat("-Z", "Y")
    right = rot @ Vector((1, 0, 0))
    up = rot @ Vector((0, 1, 0))
    xs = [(p - centre).dot(right) for p in pts]
    ys = [(p - centre).dot(up) for p in pts]
    mid = centre + right * (max(xs) + min(xs)) / 2 + up * (max(ys) + min(ys)) / 2
    aspect = scene.render.resolution_x / scene.render.resolution_y
    w, h = max(xs) - min(xs), max(ys) - min(ys)
    # ortho_scale spans the image's longer side
    span = max(w, h * aspect) if aspect >= 1 else max(w / aspect, h)
    cam_data = bpy.data.cameras.new(name)
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = span * margin
    cam_data.clip_start = 0.01
    cam_data.clip_end = 200
    cam = bpy.data.objects.new(name, cam_data)
    cam.location = mid + d * 30
    cam.rotation_euler = rot.to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam
    return cam


def render(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
