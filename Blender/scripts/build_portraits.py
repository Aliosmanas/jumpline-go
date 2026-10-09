"""
Konuşma portreleri (DialogueBox) — 512 px şeffaf PNG → Assets/Art/UI/Portraits/{cayci,tepegoz,hero,book}.png
  • cayci  : ÇAYCI AMCA (kendi tasarımımız, bilge dayı arketipi; kimseye benzetilmez): MPFB yaşlı erkek (CC0) +
             kıvırcık kalpak, gri sakal/bıyık, kürk yakalı ceket, elinde pirinç tepsi ve iki ince belli çay bardağı.
  • tepegoz: tepegoz.blend (alt açıdan, tehditkâr).   • hero: runner.blend (şimdiki koşucu).
  • book   : EFSANELER KİTABI — deri kapak, altın köşelikler, kapakta parlayan tek göz amblemi.
Çalıştır: blender -b -P Blender/scripts/build_portraits.py
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = r"D:\Repolar\junpline"
OUT = os.path.join(ROOT, "Assets", "Art", "UI", "Portraits")
BLEND = os.path.join(ROOT, "Blender", "source")
SIZE = 512


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, rough=0.6, metal=0.0, glow=0.0, transmission=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if glow:
        b.inputs["Emission Color"].default_value = srgb(col)
        b.inputs["Emission Strength"].default_value = glow
    if transmission and "Transmission Weight" in b.inputs:
        b.inputs["Transmission Weight"].default_value = transmission
    return m


def obj(name, bm, material, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(material)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def blob(name, center, size, material, segs=24, noise=0.0, seed=0):
    b = bmesh.new()
    bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=segs // 2, radius=1.0)
    import random
    rnd = random.Random(seed)
    for v in b.verts:
        n = 1.0 + (rnd.uniform(-noise, noise) if noise else 0.0)
        v.co = Vector((v.co.x * size[0] * n, v.co.y * size[1] * n, v.co.z * size[2] * n)) + Vector(center)
    return obj(name, b, material)


def lathe(name, profile, material, segs=32):
    b = bmesh.new()
    rings = [[b.verts.new((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)) for k in range(segs)] for r, z in profile]
    for i in range(len(rings) - 1):
        for k in range(segs):
            j = (k + 1) % segs
            b.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
    b.faces.new(list(reversed(rings[0])))
    return obj(name, b, material)


# ---------------------------------------------------------------------------
# Ortak: ışık / kamera / render
# ---------------------------------------------------------------------------

def setup_scene(bg_strength=0.35):
    sc = bpy.context.scene
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = SIZE
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    vt = [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items]
    sc.view_settings.view_transform = "AgX" if "AgX" in vt else "Filmic"
    w = bpy.data.worlds.new("PW")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = srgb("#6E5A4A")
    bg.inputs["Strength"].default_value = bg_strength
    sc.world = w
    return sc


def lights(target, key=(-1.6, -2.2, 1.4), rim_color="#FF9A5A", key_e=420, rim_e=520):
    t = Vector(target)
    for name, off, energy, col, size in (("Key", key, key_e, "#FFE6CC", 1.4), ("Rim", (1.8, 1.6, 1.2), rim_e, rim_color, 1.0),
                                          ("Fill", (1.6, -2.0, -0.4), 120, "#BFD3FF", 2.0)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy = energy
        ld.size = size
        ld.color = srgb(col)[:3]
        lo = bpy.data.objects.new(name, ld)
        lo.location = t + Vector(off)
        lo.rotation_euler = (t - lo.location).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.collection.objects.link(lo)


def render(name, target, view_dir, dist, lens=85, hide=()):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("PC")
    cd.lens = lens
    cam = bpy.data.objects.new("PC", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    d = Vector(view_dir).normalized()
    cam.location = Vector(target) - d * dist
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    for o in sc.objects:
        if o.name in hide:
            o.hide_render = True
    os.makedirs(OUT, exist_ok=True)
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("PORTRAIT", name)


def reset_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
    bpy.context.view_layer.update()


def aim_bone(rig, name, world_dir):
    pb = rig.pose.bones.get(name)
    if pb is None:
        return
    cur = (pb.tail - pb.head).normalized()
    q = cur.rotation_difference((rig.matrix_world.to_3x3().inverted() @ Vector(world_dir)).normalized())
    m = pb.matrix.copy()
    loc = m.to_translation()
    pb.matrix = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m
    bpy.context.view_layer.update()


# ---------------------------------------------------------------------------
# Çaycı amca
# ---------------------------------------------------------------------------

def cayci():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hs = importlib.import_module("bl_ext.user_default.mpfb.services.humanservice").HumanService
    info = {"clothes": ["male_casualsuit03/male_casualsuit03.mhclo"], "color_adjustments": {}, "expressions": [], "makeup": [],
            "targets": [], "alternative_materials": {},
            "eyebrows": "eyebrow010/eyebrow010.mhclo", "eyelashes": "eyelashes01/eyelashes01.mhclo", "eyes": "high-poly/high-poly.mhclo",
            "eyes_material_settings": {}, "eyes_material_type": "MAKESKIN", "hair": "", "proxy": "", "rig": "game_engine",
            "skin_material_settings": {}, "skin_material_type": "MAKESKIN", "skin_mhmat": "old_caucasian_male/old_caucasian_male.mhmat",
            "teeth": "", "tongue": "",
            "phenotype": {"age": 0.95, "cupsize": 0.5, "firmness": 0.4, "gender": 1.0, "height": 0.4, "muscle": 0.35,
                          "proportions": 0.6, "weight": 0.62, "race": {"african": 0.0, "asian": 0.05, "caucasian": 0.95}}}
    st = hs.get_default_deserialization_settings()
    st["subdiv_levels"] = 1
    body = hs.deserialize_from_dict(info, st)
    rig = body.parent
    bpy.context.view_layer.update()
    # kollar: tepsiyi göğüs önünde taşır
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")
    reset_pose(rig)
    for side, sx in (("l", 1), ("r", -1)):
        aim_bone(rig, "upperarm_" + side, (sx * 0.35, -0.45, -0.82))
        aim_bone(rig, "lowerarm_" + side, (sx * -0.25, -0.97, 0.05))
        aim_bone(rig, "hand_" + side, (sx * -0.1, -0.9, 0.42))
    aim_bone(rig, "head", (0.08, -0.25, 1.0))
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    ev = body.evaluated_get(deps)
    pts = [ev.matrix_world @ v.co for v in ev.data.vertices]
    top = max(p.z for p in pts)
    head_pts = [p for p in pts if p.z > top - 0.26]
    hc = sum(head_pts, Vector()) / len(head_pts)
    front_y = min(p.y for p in head_pts)
    hl = rig.matrix_world @ rig.pose.bones["hand_l"].head
    hr = rig.matrix_world @ rig.pose.bones["hand_r"].head

    fur = mat("Fur", "#2B2522", 1.0)
    beard = mat("Beard", "#CFCAC2", 0.9)
    collar = mat("Collar", "#7A5A3C", 1.0)
    brass = mat("Brass", "#C9A04A", 0.28, 0.95)
    glass = mat("Glass", "#F6F1E8", 0.05, 0.0)
    gb = next(n for n in glass.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    gb.inputs["Alpha"].default_value = 0.28   # ince belli bardak: içindeki demli çay görünsün
    if hasattr(glass, "surface_render_method"):
        glass.surface_render_method = "BLENDED"
    tea = mat("Tea", "#6E1606", 0.12, glow=0.25)
    # kalpak: kıvırcık kuzu postu — tepeye oturan basık silindir, yüzeyi tümsekli
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=True, segments=64, radius1=0.126, radius2=0.118, depth=0.14)
    bmesh.ops.subdivide_edges(b, edges=b.edges[:], cuts=3, use_grid_fill=True)
    import random
    rnd = random.Random(3)
    for v in b.verts:   # kuzu postu kıvırcığı: her köşe yüzey yönünde rastgele kabarır
        n = Vector((v.co.x, v.co.y, 0)).normalized() if abs(v.co.z) < 0.064 else Vector((0, 0, 1 if v.co.z > 0 else -1))
        v.co += n * rnd.uniform(0.002, 0.012)
    kal = obj("Kalpak", b, fur)
    kal.location = (hc.x, hc.y + 0.01, top - 0.025)   # alt kenar alnın ortasına iner (kalpak başa geçer)
    bev = kal.modifiers.new("B", "BEVEL")
    bev.width = 0.015
    bev.segments = 3
    # sakal + bıyık (gri, dolgun) — çene önünde
    chin_z = hc.z - 0.115
    blob("Beard", (hc.x, front_y + 0.035, chin_z), (0.07, 0.05, 0.075), beard, 28, 0.06, 1)
    blob("BeardSideL", (hc.x + 0.055, front_y + 0.055, chin_z + 0.03), (0.035, 0.04, 0.055), beard, 20, 0.08, 2)
    blob("BeardSideR", (hc.x - 0.055, front_y + 0.055, chin_z + 0.03), (0.035, 0.04, 0.055), beard, 20, 0.08, 3)
    for s in (-1, 1):   # bıyık: önce kökte kurulur, sonra yerine (dönüş kendi merkezinde)
        mo = blob("Mustache", (0, 0, 0), (0.032, 0.014, 0.012), beard, 16, 0.05, 4 + s)
        mo.rotation_euler = (0, s * 0.35, 0)
        mo.location = (hc.x + s * 0.024, front_y + 0.004, hc.z - 0.064)
    # tepsi + ince belli bardaklar
    tray_c = (hl + hr) * 0.5 + Vector((0, -0.04, 0.035))
    tray = lathe("Tray", [(0.0, 0.0), (0.2, 0.0), (0.205, 0.012), (0.198, 0.016), (0.19, 0.006), (0.0, 0.006)], brass, 48)
    tray.location = tray_c
    for i, dx in enumerate((-0.055, 0.055)):
        prof = [(0.0, 0.0), (0.024, 0.0), (0.026, 0.006), (0.02, 0.03), (0.018, 0.045), (0.026, 0.075), (0.028, 0.09), (0.0, 0.09)]
        g = lathe("Glass%d" % i, prof, glass, 28)
        g.location = tray_c + Vector((dx, -0.02, 0.012))
        t = lathe("Tea%d" % i, [(0.0, 0.0), (0.021, 0.0), (0.017, 0.028), (0.016, 0.044), (0.023, 0.07), (0.0, 0.07)], tea, 24)
        t.location = g.location + Vector((0, 0, 0.004))
        saucer = lathe("Saucer%d" % i, [(0.0, 0.0), (0.04, 0.0), (0.045, 0.006), (0.0, 0.004)], brass, 28)
        saucer.location = g.location - Vector((0, 0, 0.002))
    setup_scene()
    target = hc + Vector((0, 0, -0.16))
    lights(target)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, "cayci.blend"))
    render("cayci", target, (0.42, 1.0, -0.1), 2.15, lens=85)


# ---------------------------------------------------------------------------
# Tepegöz / kahraman (mevcut modellerden)
# ---------------------------------------------------------------------------

def from_blend(path, name, view_dir, dist, rim, action=None, frame=0, lift=0.0, key_e=420, head_bone=False):
    bpy.ops.wm.open_mainfile(filepath=path)
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = next(o for o in bpy.data.objects if o.type == "MESH" and o.parent == rig)
    if action and bpy.data.actions.get(action):
        rig.animation_data_create()
        rig.animation_data.action = bpy.data.actions[action]
        bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    ev = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    pts = [ev.matrix_world @ v.co for v in ev.data.vertices]
    top = max(p.z for p in pts)
    hp = [p for p in pts if p.z > top - 0.3 * (top / 2.0)]
    hc = sum(hp, Vector()) / len(hp)
    if head_bone:
        pb = rig.pose.bones["head"]
        hc = rig.matrix_world @ ((pb.head + pb.tail) * 0.5)
    for o in list(bpy.data.objects):
        if o.type in ("LIGHT", "CAMERA"):
            bpy.data.objects.remove(o)
    setup_scene()
    target = hc + Vector((0, 0, -0.05 + lift))
    lights(target, rim_color=rim, key_e=key_e)
    render(name, target, view_dir, dist)


# ---------------------------------------------------------------------------
# Efsaneler Kitabı
# ---------------------------------------------------------------------------

def book():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    leather = mat("Leather", "#5A1A1E", 0.55)
    pages = mat("Pages", "#EADBB8", 0.9)
    gold = mat("Gold", "#D9A441", 0.25, 1.0)
    eye = mat("EyeW", "#F4EEE0", 0.3, glow=0.6)
    iris = mat("EyeI", "#FF5A1E", 0.2, glow=6.0)

    def box(name, x, y, z, sx, sy, sz, m):
        b = bmesh.new()
        bmesh.ops.create_cube(b, size=1.0)
        for v in b.verts:
            v.co = Vector((v.co.x * sx + x, v.co.y * sy + y, v.co.z * sz + z))
        o = obj(name, b, m, smooth=False)
        bev = o.modifiers.new("B", "BEVEL")
        bev.width = 0.012
        bev.segments = 3
        return o

    box("CoverBack", 0, 0, 0.0, 1.0, 1.36, 0.06, leather)
    box("Pages", 0.02, 0, 0.08, 0.94, 1.3, 0.12, pages)
    box("CoverFront", 0, 0, 0.16, 1.0, 1.36, 0.06, leather)
    box("Spine", -0.5, 0, 0.08, 0.08, 1.36, 0.22, leather)
    for sx in (-1, 1):
        for sy in (-1, 1):
            box("Corner", sx * 0.44, sy * 0.62, 0.195, 0.14, 0.14, 0.02, gold)
    ring = lathe("Ring", [(0.17, 0.0), (0.2, 0.0), (0.2, 0.02), (0.17, 0.02)], gold, 64)
    ring.location = (0.02, 0, 0.19)
    e = blob("Eye", (0.02, 0, 0.2), (0.15, 0.09, 0.02), eye, 32)
    blob("Iris", (0.02, 0, 0.215), (0.055, 0.055, 0.012), iris, 32)
    for k in range(8):
        a = k * math.pi / 4
        r = box("Ray", 0.02 + math.cos(a) * 0.28, math.sin(a) * 0.28, 0.195, 0.05, 0.05, 0.015, gold)
        r.rotation_euler = (0, 0, a)
    setup_scene(0.45)
    lights((0, 0, 0.1), key=(-1.2, -1.6, 2.2), rim_color="#FFB060")
    render("book", (0.0, 0.0, 0.1), (0.25, 0.55, -0.8), 4.2, lens=60)


import sys
ONLY = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if not ONLY or "cayci" in ONLY:
    cayci()
if not ONLY or "tepegoz" in ONLY:
    from_blend(os.path.join(BLEND, "tepegoz.blend"), "tepegoz", (0.3, 1.0, 0.28), 1.2, "#FF4A2A", action="Idle", frame=0, lift=-0.05, key_e=380, head_bone=True)
if not ONLY or "hero" in ONLY:
    from_blend(os.path.join(BLEND, "runner.blend"), "hero", (-0.4, 1.0, -0.05), 1.9, "#55CFFF")
if not ONLY or "book" in ONLY:
    book()
print("PORTRAITS_OK")
