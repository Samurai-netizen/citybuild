"""Geometry helpers, export and preview rendering for the scripted art.

Conventions (Blender space; the FBX export converts to Unity):
- 1 Blender unit = 1 m = 1 Unity unit. One grid tile is 1 x 1. The pivot is the tile centre on the ground (z = 0).
- The front of a building faces Blender +Y, which is Unity -Z: toward a camera that looks along Unity +Z.
  The second side seen by the 3/4 camera is Blender +X (Unity -X). Put the details there.
- Every face takes one palette colour (see palette.py); every model has one material.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

import palette

MATERIAL_NAME = "M_Palette_Era1"


# --- geometry ---------------------------------------------------------------------------------------------------

def _rot(rot):
    return Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4()


class Model:
    """Collects coloured parts and turns them into one flat-shaded mesh."""

    def __init__(self):
        self.verts = []
        self.faces = []
        self.colors = []

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

    def roof_bands(self, length, width, height, at, color, rows=2):
        """Raised strips across both slopes of an X-ridge gable(): thatch layers or shingle rows."""
        a = math.atan2(height, width / 2)
        slope = math.hypot(height, width / 2)
        for i in range(rows):
            t = (i + 0.6) / (rows + 0.6)
            for side in (1, -1):
                normal = Vector((0, side * math.sin(a), math.cos(a)))
                p = Vector((0, side * (width / 2) * (1 - t), height * t)) + normal * 0.006
                self.box((length + 0.014, slope * 0.16, 0.014), Vector(at) + p, color,
                         rot=(-side * math.degrees(a), 0, 0), bevel=0.006, anchor="center")

    def tris(self):
        return sum(len(f) - 2 for f in self.faces)

    def to_object(self, name, parent=None, location=(0, 0, 0)):
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
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = location
        if parent:
            obj.parent = parent
        return obj


def anchor(name, parent, location):
    """An empty the client looks up by name (status bubble, door, stockpile, smoke)."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_type = "PLAIN_AXES"
    e.empty_display_size = 0.05
    e.location = location
    e.parent = parent
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


# --- checks and export --------------------------------------------------------------------------------------------

def stats(root):
    """World-space bounds, triangle count and material count of a model and its children."""
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    tris = 0
    mats = set()
    bpy.context.view_layer.update()
    for obj in descendants(root):
        if obj.type != "MESH":
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


def setup_render(width, height, transparent=False):
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
            scene.eevee.taa_render_samples = 32
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
