"""
Canavar kovalaması varlıkları (bölüm 1):
  • Karpuz (Karpuz): koyu/açık yeşil dalgalı çizgili, hafif uzun; sapı kahve. ~0.62 m.
  • Bakır testi (Testi): torna profili (taban → karın → boyun → ağız), kulp, emzik, koyu bakır bant. ~0.5 m.
  • Düşme işareti (LandingMarker): canavarın fırlattığı engelin düşeceği yeri gösteren parlak kırmızı halka + oklar.
  • Geçici canavar (TepegozBlock): 3.4 m kaba gövde (gerçek Tepegöz modeli gelene kadar oynanış testi için).
Ayrıca arayüz ikonları (şeffaf PNG, 256 px): heart, throw_melon, throw_jug, boss_head → Assets/Art/UI/Icons.
Çıktı: Assets/Art/Boss/Throwables.fbx (Karpuz, Testi, LandingMarker), Assets/Art/Boss/TepegozBlock.fbx,
Blender/source/throwables.blend
Çalıştır: blender -b -P Blender/scripts/build_throwables.py
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
ART = os.path.join(ROOT, "Assets", "Art", "Boss")
ICONS = os.path.join(ROOT, "Assets", "Art", "UI", "Icons")
BLEND = os.path.join(ROOT, "Blender", "source", "throwables.blend")


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, metal=0.0, rough=0.5, glow=0.0, coat=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if coat and "Coat Weight" in b.inputs:
        b.inputs["Coat Weight"].default_value = coat
    if glow:
        b.inputs["Emission Color"].default_value = srgb(col)
        b.inputs["Emission Strength"].default_value = glow
    return m


def obj_from_bm(name, bm, mats, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def apply_mods(o):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    for m in list(o.modifiers):
        bpy.ops.object.modifier_apply(modifier=m.name)


def lathe(bm, profile, segs, mat_index_fn=None, cap_bottom=True):
    """profile: [(r, z)] alttan üste. z ekseni etrafında döndürür; dörtgen yüzler. mat_index_fn(i) → malzeme."""
    rings = []
    for r, z in profile:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(segs):
            j = (k + 1) % segs
            f = bm.faces.new((a[k], a[j], b[j], b[k]))
            if mat_index_fn:
                f.material_index = mat_index_fn(i)
    if cap_bottom:
        f = bm.faces.new(list(reversed(rings[0])))
        if mat_index_fn:
            f.material_index = mat_index_fn(0)
    return rings


# ---------------------------------------------------------------------------
# Karpuz
# ---------------------------------------------------------------------------

def karpuz_texture(path, w=512, h=256):
    """Karpuz kabuğu: u = uzun eksen etrafında açı, v = boy. Koyu yeşil zemin, dalgalı açık çizgiler, ince benekler."""
    import random
    random.seed(7)
    img = bpy.data.images.new("Karpuz_Albedo", w, h, alpha=False)
    dark = srgb("#1C5A2A")[:3]
    mid = srgb("#2F7A35")[:3]
    light = srgb("#8FC266")[:3]
    px = [0.0] * (w * h * 4)
    stripes = 9
    for y in range(h):
        v = y / (h - 1)
        pole = math.sin(v * math.pi)                         # kutuplarda çizgiler birleşir
        for x in range(w):
            u = x / w
            wob = 0.012 * math.sin(v * 17.0 + u * 6.28) + 0.006 * math.sin(v * 61.0 + u * 31.0) + 0.004 * math.sin(v * 150.0)
            d = abs(((u + wob) * stripes) % 1.0 - 0.5) * 2.0   # 0 = çizgi ortası
            width = 0.42 + 0.06 * math.sin(v * 23.0 + u * 40.0)
            k = max(0.0, min(1.0, (d - width) / 0.12))          # 1 = koyu zemin
            k = 1.0 - (1.0 - k) * (0.6 + 0.4 * pole)
            n = 0.06 * math.sin(x * 1.7 + y * 2.3) * math.sin(x * 0.37 - y * 0.91)
            base = [light[i] * (1 - k) + (dark[i] * 0.75 + mid[i] * 0.25) * k + n * 0.3 for i in range(3)]
            o = (y * w + x) * 4
            px[o:o + 4] = [max(0.0, c) for c in base] + [1.0]
    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    return img


def karpuz():
    tex_path = os.path.join(ART, "Karpuz_Albedo.png")
    img = karpuz_texture(tex_path)
    rind = mat("K_Rind", "#FFFFFF", 0.0, 0.45, coat=0.25)
    b = next(n for n in rind.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    tn = rind.node_tree.nodes.new("ShaderNodeTexImage")
    tn.image = img
    rind.node_tree.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    stem = mat("K_Stem", "#6B4A2A", 0.0, 0.8)
    bm = bmesh.new()
    bm.loops.layers.uv.new("UVMap")
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=24, radius=1.0, calc_uvs=True)
    for v in bm.verts:                                   # uzun eksen z (UV: u çevre, v boy)
        x, y, z = v.co
        v.co = Vector((x * 0.27, y * 0.27, z * 0.34))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm("Karpuz", bm, [rind, stem])
    sb = bmesh.new()
    bmesh.ops.create_cone(sb, cap_ends=True, segments=8, radius1=0.022, radius2=0.013, depth=0.07)
    for v in sb.verts:
        v.co = Vector((v.co.x + v.co.z * 0.25, v.co.y, v.co.z + 0.36))
    for f in sb.faces:
        f.material_index = 1
    stem_o = obj_from_bm("KarpuzStem", sb, [rind, stem], smooth=False)
    join(o, [stem_o])
    o.rotation_euler = (math.radians(90), 0, 0)      # uzun eksen Blender y → Unity Z (koşu yönü)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(rotation=True)
    return o


# ---------------------------------------------------------------------------
# Bakır testi
# ---------------------------------------------------------------------------

def testi():
    copper = mat("T_Copper", "#C46A33", 0.95, 0.3)
    band = mat("T_CopperDark", "#6E3418", 0.9, 0.42)
    S = 1.25
    prof = [(0.0, 0.0), (0.075, 0.0), (0.085, 0.012), (0.08, 0.03), (0.11, 0.07), (0.15, 0.13), (0.168, 0.185),
            (0.165, 0.215), (0.15, 0.255), (0.115, 0.295), (0.07, 0.33), (0.048, 0.355), (0.045, 0.385), (0.055, 0.405),
            (0.066, 0.418), (0.06, 0.425), (0.042, 0.42)]
    prof = [(r * S, z * S) for r, z in prof]
    bm = bmesh.new()
    lathe(bm, prof, 40, lambda i: 1 if i in (6, 7, 12, 13) else 0, cap_bottom=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = obj_from_bm("Testi", bm, [copper, band])
    # kulp: omuzdan boyuna yarım halka (yay boyunca boru)
    hb = bmesh.new()
    pts = []
    for k in range(13):
        a = math.pi * k / 12
        pts.append(Vector((-(0.13 + 0.07 * math.sin(a)) * S, 0, (0.235 + 0.125 * (1 - math.cos(a)) * 0.5 + 0.0 * a) * S)))
    pts = [Vector((-(0.12 + 0.085 * math.sin(math.pi * k / 12)) * S, 0, (0.22 + 0.15 * k / 12) * S)) for k in range(13)]
    tube(hb, pts, 0.016 * S, 10)
    handle = obj_from_bm("TestiHandle", hb, [copper, band])
    # emzik (yan ağız): omuzdan öne eğik kısa boru
    eb = bmesh.new()
    spout = [Vector((0.13 * S, 0, 0.24 * S)), Vector((0.19 * S, 0, 0.3 * S)), Vector((0.225 * S, 0, 0.335 * S))]
    tube(eb, spout, 0.02 * S, 10, taper=0.7)
    sp = obj_from_bm("TestiSpout", eb, [copper, band])
    join(o, [handle, sp])
    o.rotation_euler = (0, 0, math.radians(-90))     # emzik koşu yönüne
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(rotation=True)
    # ağırlık merkezi ortada dönsün
    o.data.transform(__import__("mathutils").Matrix.Translation((0, 0, -0.21 * S)))
    return o


def tube(bm, pts, radius, segs, taper=1.0):
    rings = []
    n = len(pts)
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        side = d.cross(Vector((0, 1, 0)))
        if side.length < 1e-4:
            side = d.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(d).normalized()
        r = radius * (1.0 + (taper - 1.0) * i / max(1, n - 1))
        rings.append([bm.verts.new(p + (side * math.cos(2 * math.pi * k / segs) + up * math.sin(2 * math.pi * k / segs)) * r) for k in range(segs)])
    for i in range(n - 1):
        for k in range(segs):
            j = (k + 1) % segs
            bm.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


def join(target, others):
    bpy.ops.object.select_all(action="DESELECT")
    for o in others:
        o.select_set(True)
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()


# ---------------------------------------------------------------------------
# Düşme işareti
# ---------------------------------------------------------------------------

def landing_marker():
    warn = mat("M_Warn", "#FF3B30", 0.0, 0.5, glow=4.0)
    bm = bmesh.new()
    segs = 48
    r_out, r_in = 0.95, 0.8
    outer = [bm.verts.new((r_out * math.cos(2 * math.pi * k / segs), r_out * math.sin(2 * math.pi * k / segs), 0)) for k in range(segs)]
    inner = [bm.verts.new((r_in * math.cos(2 * math.pi * k / segs), r_in * math.sin(2 * math.pi * k / segs), 0)) for k in range(segs)]
    for k in range(segs):
        j = (k + 1) % segs
        bm.faces.new((inner[k], inner[j], outer[j], outer[k]))
    # içe bakan dört ok ucu
    for q in range(4):
        a = math.pi / 4 + q * math.pi / 2
        c, s = math.cos(a), math.sin(a)
        tip = Vector((c * 0.32, s * 0.32, 0))
        base = Vector((c * 0.68, s * 0.68, 0))
        perp = Vector((-s, c, 0)) * 0.17
        bm.faces.new((bm.verts.new(tip), bm.verts.new(base + perp), bm.verts.new(base - perp)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    for f in bm.faces:
        if f.normal.z < 0:
            f.normal_flip()
    return obj_from_bm("LandingMarker", bm, [warn], smooth=False)


# ---------------------------------------------------------------------------
# Geçici Tepegöz (kaba gövde)
# ---------------------------------------------------------------------------

def tepegoz_block():
    skin = mat("B_Skin", "#C9875F", 0.0, 0.6)
    cloth = mat("B_Cloth", "#5A2770", 0.0, 0.8)
    eye = mat("B_Eye", "#F4F1E8", 0.0, 0.25)
    iris = mat("B_Iris", "#C8261E", 0.0, 0.3, glow=2.5)
    rope = mat("B_Rope", "#8B6B43", 0.0, 0.9)
    mats = [skin, cloth, eye, iris, rope]
    parts = []

    def blob(name, center, size, mi, segs=16):
        b = bmesh.new()
        bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=max(8, segs // 2), radius=1.0)
        for v in b.verts:
            v.co = Vector((v.co.x * size[0] + center[0], v.co.y * size[1] + center[1], v.co.z * size[2] + center[2]))
        for f in b.faces:
            f.material_index = mi
        parts.append(obj_from_bm(name, b, mats))

    # Blender: x yan, y ileri (koşu yönü -y → Unity +Z ileri: axis_forward -Z), z yukarı
    blob("Hips", (0, 0, 1.45), (0.62, 0.42, 0.38), 1)                    # mor peştemal
    blob("Torso", (0, 0, 2.15), (0.78, 0.52, 0.62), 0)
    blob("Belly", (0, -0.12, 1.85), (0.6, 0.42, 0.42), 0)
    blob("Head", (0, -0.12, 2.95), (0.42, 0.4, 0.44), 0)
    blob("Brow", (0, -0.42, 3.08), (0.34, 0.12, 0.1), 0)
    blob("EyeWhite", (0, -0.43, 2.98), (0.2, 0.08, 0.17), 2)
    blob("EyeIris", (0, -0.5, 2.98), (0.09, 0.04, 0.09), 3)
    for side in (-1, 1):
        blob("Shoulder", (side * 0.92, 0, 2.5), (0.36, 0.34, 0.34), 0)
        blob("UpperArm", (side * 1.1, 0, 2.05), (0.24, 0.24, 0.48), 0)
        blob("Forearm", (side * 1.18, -0.08, 1.45), (0.22, 0.22, 0.42), 0)
        blob("Wrap", (side * 1.18, -0.08, 1.4), (0.235, 0.235, 0.16), 4)
        blob("Fist", (side * 1.2, -0.12, 0.98), (0.22, 0.22, 0.2), 0)
        blob("Thigh", (side * 0.36, 0, 0.98), (0.27, 0.27, 0.42), 0)
        blob("Shin", (side * 0.38, 0.02, 0.42), (0.21, 0.21, 0.38), 0)
        blob("Foot", (side * 0.38, -0.14, 0.08), (0.2, 0.32, 0.1), 0)
    o = parts[0]
    join(o, parts[1:])
    o.name = "TepegozBlock"
    o.data.name = "TepegozBlock"
    return o


# ---------------------------------------------------------------------------
# İkonlar
# ---------------------------------------------------------------------------

def heart_mesh():
    """Kabarık kalp: kalp çizgisi boyunca ve içinde metaball'lar → pürüzsüz yastık gövde."""
    red = mat("H_Red", "#D91F2E", 0.0, 0.22, coat=1.0)

    def heart(t):
        x = 16 * math.sin(t) ** 3
        z = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        return x / 17.0, z / 17.0

    outline = [heart(2 * math.pi * k / 120) for k in range(120)]

    def inside(px, pz):
        c = False
        for i in range(len(outline)):
            (x1, z1), (x2, z2) = outline[i], outline[i - 1]
            if (z1 > pz) != (z2 > pz) and px < (x2 - x1) * (pz - z1) / (z2 - z1 + 1e-9) + x1:
                c = not c
        return c

    mb = bpy.data.metaballs.new("HeartMeta")
    mb.resolution = 0.025
    mb.render_resolution = 0.025
    mb.threshold = 0.6
    for x, z in outline[::3]:
        e = mb.elements.new()
        e.co = (x * 0.82, 0, z * 0.82 + 0.02)
        e.radius = 0.3
    for gx in range(-10, 11):
        for gz in range(-12, 9):
            px, pz = gx * 0.09, gz * 0.09
            if inside(px / 0.8, pz / 0.8):
                e = mb.elements.new()
                e.co = (px, 0, pz)
                e.radius = 0.3
    mo = bpy.data.objects.new("HeartMeta", mb)
    bpy.context.collection.objects.link(mo)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mo
    mo.select_set(True)
    bpy.context.view_layer.update()
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.view_layer.objects.active
    o.name = "Heart"
    o.scale = (1.0, 0.55, 1.0)
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.clear()
    o.data.materials.append(red)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def setup_render():
    sc = bpy.context.scene
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "CYCLES"):
        if e in engines:
            try:
                sc.render.engine = e
                break
            except TypeError:
                continue
    if sc.render.engine == "CYCLES":
        sc.cycles.samples = 48
    sc.render.film_transparent = True
    sc.render.resolution_x = sc.render.resolution_y = 256
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("IconWorld")
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = (0.55, 0.58, 0.65, 1)
    bg.inputs["Strength"].default_value = 0.35
    sc.world = world
    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("IconCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam
    for name, loc, energy, size in (("Key", (-2.5, -4, 4), 900, 3), ("Rim", (3, 3, 2.5), 600, 2), ("Fill", (3, -4, -1), 250, 4)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = energy
        ld.size = size
        lo = bpy.data.objects.new(name, ld)
        lo.location = loc
        lo.rotation_euler = (Vector((0, 0, 0)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
        sc.collection.objects.link(lo)
    return cam


def render_icon(cam, target, filename, view_dir=(0.35, -1.0, 0.3), margin=1.12, center=None, size=None):
    sc = bpy.context.scene
    for o in sc.objects:
        if o.type == "MESH":
            o.hide_render = o is not target
    pts = [target.matrix_world @ Vector(c) for c in target.bound_box]
    if center is None:
        center = sum(pts, Vector()) / 8
    d = Vector(view_dir).normalized()
    cam.location = center - d * 10
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    if size is None:
        size = max((p - center).length for p in pts) * 2 * margin * 0.8
    cam.data.ortho_scale = size
    sc.render.filepath = os.path.join(ICONS, filename)
    bpy.ops.render.render(write_still=True)


def export(objs, path):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(ART, exist_ok=True)
    os.makedirs(ICONS, exist_ok=True)
    k = karpuz()
    t = testi()
    m = landing_marker()
    b = tepegoz_block()
    h = heart_mesh()
    k.location = (0, 0, 0)
    t.location = (1.2, 0, 0)
    m.location = (3.0, 0, 0)
    b.location = (6.0, 0, 0)
    h.location = (-2.0, 0, 0)
    export([k, t, m], os.path.join(ART, "Throwables.fbx"))
    export([b], os.path.join(ART, "TepegozBlock.fbx"))
    cam = setup_render()
    render_icon(cam, h, "heart.png", view_dir=(0.12, 1.0, 0.22), margin=1.0)
    render_icon(cam, k, "throw_melon.png", view_dir=(0.6, 0.9, -0.55))
    render_icon(cam, t, "throw_jug.png", view_dir=(0.3, 1.0, -0.15))
    head = bpy.data.objects.get("TepegozBlock")
    render_icon(cam, head, "boss_head.png", view_dir=(0.0, 1.0, 0.0), center=head.location + Vector((0, 0, 2.98)), size=1.25)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("THROWABLES_OK")


main()
