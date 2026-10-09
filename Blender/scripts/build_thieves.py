"""
Günlük görev hırsızları ve topladıkları (kendi tasarımımız):
  • Kapkaççı (Thief.fbx): MPFB genç ince erkek (CC0) + koyu kapüşonlu üst, siyah göz bandı maskesi, bere, omzunda çuval.
    İskelet koşucuyla aynı (game_engine) → Unity'de Humanoid, koşucunun koşu klibini kullanır.
  • Sur kedisi (Cat.fbx): turuncu tekir, parçalı (Body, Head, Tail, LegFL/FR/BL/BR) — Unity'de ThiefRunner bacak/kuyruğu sallar.
  • Kese (Pouch) ve balık (Fish): Collectibles.fbx — süzülen toplanabilirler.
  • Portreler: Assets/Art/UI/Portraits/thief.png, cat.png (görev konuşması için).
Çalıştır: blender -b -P Blender/scripts/build_thieves.py
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
ART = os.path.join(ROOT, "Assets", "Art", "Thieves")
PORTRAITS = os.path.join(ROOT, "Assets", "Art", "UI", "Portraits")
BLEND = os.path.join(ROOT, "Blender", "source")
MPFB = os.path.join(os.environ["APPDATA"], "Blender Foundation", "Blender", "5.2", "extensions", ".user", "user_default", "mpfb", "data")


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, rough=0.7, metal=0.0, image=None, glow=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    if glow:
        b.inputs["Emission Color"].default_value = srgb(col)
        b.inputs["Emission Strength"].default_value = glow
    if image is not None:
        tn = m.node_tree.nodes.new("ShaderNodeTexImage")
        tn.image = image
        m.node_tree.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    return m


def mesh_obj(name, bm, materials, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(m)
    for p in me.polygons:
        p.use_smooth = smooth
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    return o


def ellipsoid(name, center, size, material, segs=20):
    b = bmesh.new()
    bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=max(8, segs // 2), radius=1.0)
    for v in b.verts:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + Vector(center)
    return mesh_obj(name, b, [material])


def export(objs, path, armature=False):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    types = {"ARMATURE", "MESH"} if armature else {"MESH", "EMPTY"}
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types=types, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False, path_mode="RELATIVE")


# ---------------------------------------------------------------------------
# Kapkaççı
# ---------------------------------------------------------------------------

def thief():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hs = importlib.import_module("bl_ext.user_default.mpfb.services.humanservice").HumanService
    info = {"clothes": ["male_casualsuit02/male_casualsuit02.mhclo"], "color_adjustments": {}, "expressions": [], "makeup": [],
            "targets": [], "alternative_materials": {}, "eyebrows": "", "eyelashes": "", "eyes": "", "eyes_material_settings": {},
            "eyes_material_type": "MAKESKIN", "hair": "", "proxy": "", "rig": "game_engine", "skin_material_settings": {},
            "skin_material_type": "NONE", "skin_mhmat": "", "teeth": "", "tongue": "",
            "phenotype": {"age": 0.4, "cupsize": 0.5, "firmness": 0.6, "gender": 1.0, "height": 0.5, "muscle": 0.55,
                          "proportions": 0.8, "weight": 0.3, "race": {"african": 0.1, "asian": 0.1, "caucasian": 0.8}}}
    st = hs.get_default_deserialization_settings()
    st["subdiv_levels"] = 0
    body = hs.deserialize_from_dict(info, st)
    rig = body.parent
    bpy.context.view_layer.update()
    skin_img = bpy.data.images.load(os.path.join(MPFB, "skins", "young_caucasian_male", "young_lightskinned_male_diffuse.png"))
    skin = mat("TH_Skin", "#FFFFFF", 0.6, image=skin_img)
    cloth = mat("TH_Cloth", "#30343C", 0.85)
    mask = mat("TH_Mask", "#101010", 0.6)
    hat = mat("TH_Hat", "#1A1A1E", 0.9)
    sack = mat("TH_Sack", "#8C6A42", 0.95)
    body.data.materials.clear()
    body.data.materials.append(skin)
    for o in rig.children:
        if o is not body and o.type == "MESH":
            o.data.materials.clear()
            o.data.materials.append(cloth)
            o.name = "ThiefClothes"
    # baş ölçüleri (bel düz duruş)
    pts = [v.co for v in body.data.vertices]
    if body.data.shape_keys:
        kb = body.data.shape_keys.key_blocks
        mix = [0.0] * (len(body.data.vertices) * 3)
        body.shape_key_add(name="__mix", from_mix=True)
        body.data.shape_keys.key_blocks["__mix"].data.foreach_get("co", mix)
        pts = [Vector(mix[i * 3:i * 3 + 3]) for i in range(len(body.data.vertices))]
    top = max(p.z for p in pts)
    hp = [p for p in pts if p.z > top - 0.24]
    hc = sum(hp, Vector()) / len(hp)
    front = min(p.y for p in hp)
    extras = []
    # göz bandı maskesi: başın etrafında halka (göz hizası)
    eye_z = hc.z - 0.03
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=False, segments=40, radius1=0.092, radius2=0.09, depth=0.04)
    for v in b.verts:
        v.co.y *= 1.12
    m_o = mesh_obj("Mask", b, [mask])
    m_o.location = (hc.x, hc.y - 0.004, eye_z)
    extras.append(m_o)
    # bere
    # bere: başı saran yarım kubbe (alt yarısı silinir) + kıvrık kenar
    bc = Vector((hc.x, hc.y + 0.006, hc.z + 0.012))
    b = bmesh.new()
    bmesh.ops.create_uvsphere(b, u_segments=28, v_segments=16, radius=1.0)
    for v in b.verts:
        v.co = Vector((v.co.x * 0.1, v.co.y * 0.11, v.co.z * 0.118)) + bc
    bmesh.ops.delete(b, geom=[f for f in b.faces if f.calc_center_median().z < bc.z], context="FACES")
    bere = mesh_obj("Beanie", b, [hat])
    extras.append(bere)
    b = bmesh.new()
    bmesh.ops.create_cone(b, cap_ends=False, segments=28, radius1=0.103, radius2=0.103, depth=0.03)
    for v in b.verts:
        v.co.y *= 1.1
    cuff = mesh_obj("BeanieCuff", b, [hat])
    cuff.location = (bc.x, bc.y, bc.z + 0.012)
    cuff.name = "Beanie2"
    extras.append(cuff)
    # omuzda çuval (sırtta)
    sp = rig.matrix_world @ rig.data.bones["spine_03"].head_local
    sk = ellipsoid("Sack", (sp.x + 0.08, sp.y + 0.2, sp.z + 0.08), (0.17, 0.14, 0.2), sack, 18)
    extras.append(sk)
    for o in extras:
        o.parent = rig
        bone = "head" if o.name.startswith(("Mask", "Beanie")) else "spine_03"
        vg = o.vertex_groups.new(name=bone)
        vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    body.name = body.data.name = "Thief"
    rig.name = rig.data.name = "ThiefRig"
    os.makedirs(ART, exist_ok=True)
    meshes = [o for o in rig.children if o.type == "MESH"]
    export([rig] + meshes, os.path.join(ART, "Thief.fbx"), armature=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, "thief.blend"))
    portrait_from_scene(hc + Vector((0, 0, -0.06)), "thief", (0.4, 1.0, -0.05), 1.7, "#9F7BFF")


# ---------------------------------------------------------------------------
# Sur kedisi (parçalı)
# ---------------------------------------------------------------------------

def cat():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fur = mat("CAT_Fur", "#D9822B", 0.85)
    stripe = mat("CAT_Stripe", "#8A4A16", 0.85)
    white = mat("CAT_White", "#F2E9DA", 0.85)
    eye = mat("CAT_Eye", "#7CD34B", 0.2, glow=1.2)
    nose = mat("CAT_Nose", "#D46A7A", 0.5)
    S = 1.6   # oyunda görünür boy (gerçek kediden iri, çizgi film oranı)
    root = bpy.data.objects.new("Cat", None)
    bpy.context.collection.objects.link(root)
    parts = []

    def part(name, objs, pivot):
        """Parçayı pivotu (eklem) kökü olacak şekilde birleştirir — Unity'de bu nokta etrafında döner."""
        bpy.ops.object.select_all(action="DESELECT")
        for o in objs:
            o.select_set(True)
        bpy.context.view_layer.objects.active = objs[0]
        if len(objs) > 1:
            bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        o.name = name
        o.data.name = name
        piv = Vector(pivot)
        o.data.transform(__import__("mathutils").Matrix.Translation(-piv))
        o.location = piv
        o.parent = root
        parts.append(o)
        return o

    # gövde (+ çizgiler + beyaz göğüs): koşu yönü -y (Blender) = Unity +Z
    bodyp = [ellipsoid("B", (0, 0, 0.17 * S), (0.085 * S, 0.17 * S, 0.075 * S), fur, 24)]
    for k in range(3):
        bodyp.append(ellipsoid("St", (0, (-0.06 + k * 0.07) * S, 0.235 * S), (0.07 * S, 0.012 * S, 0.02 * S), stripe, 12))
    bodyp.append(ellipsoid("Ch", (0, -0.13 * S, 0.15 * S), (0.055 * S, 0.05 * S, 0.06 * S), white, 14))
    part("Body", bodyp, (0, 0, 0.17 * S))
    hz, hy = 0.27 * S, -0.2 * S
    headp = [ellipsoid("H", (0, hy, hz), (0.07 * S, 0.065 * S, 0.06 * S), fur, 22),
             ellipsoid("Mz", (0, hy - 0.05 * S, hz - 0.015 * S), (0.035 * S, 0.03 * S, 0.025 * S), white, 14),
             ellipsoid("N", (0, hy - 0.078 * S, hz - 0.005 * S), (0.009 * S, 0.006 * S, 0.006 * S), nose, 8)]
    for s in (-1, 1):
        headp.append(ellipsoid("E", (s * 0.028 * S, hy - 0.055 * S, hz + 0.012 * S), (0.012 * S, 0.008 * S, 0.014 * S), eye, 10))
        b = bmesh.new()
        bmesh.ops.create_cone(b, cap_ends=True, segments=4, radius1=0.025 * S, radius2=0.0, depth=0.05 * S)
        ear = mesh_obj("Ear", b, [fur], smooth=False)
        ear.location = (s * 0.04 * S, hy + 0.005 * S, hz + 0.065 * S)
        ear.rotation_euler = (0.2, s * -0.35, 0)
        bpy.context.view_layer.objects.active = ear
        ear.select_set(True)
        bpy.ops.object.transform_apply(location=True, rotation=True)
        headp.append(ear)
    part("Head", headp, (0, hy + 0.04 * S, hz - 0.02 * S))
    # kuyruk: kalkık, kıvrık
    b = bmesh.new()
    pts = [Vector((0, (0.16 + 0.04 * t) * S, (0.2 + 0.18 * t + 0.05 * math.sin(t * 3)) * S)) for t in [i / 10 for i in range(11)]]
    rings = []
    for i, p in enumerate(pts):
        r = (0.022 - 0.012 * i / 10) * S
        rings.append([b.verts.new(p + Vector((r * math.cos(a), 0, r * math.sin(a))) if False else p + Vector((r * math.cos(2 * math.pi * k / 8), r * math.sin(2 * math.pi * k / 8) * 0.0 + 0, 0)) + Vector((0, r * math.sin(2 * math.pi * k / 8), 0))) for k in range(8)])
    for i in range(len(rings) - 1):
        for k in range(8):
            j = (k + 1) % 8
            b.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
    tail = mesh_obj("T", b, [fur])
    part("Tail", [tail], (0, 0.16 * S, 0.2 * S))
    for name, x, y in (("LegFL", 0.045, -0.11), ("LegFR", -0.045, -0.11), ("LegBL", 0.05, 0.11), ("LegBR", -0.05, 0.11)):
        leg = ellipsoid("L", (x * S, y * S, 0.075 * S), (0.022 * S, 0.024 * S, 0.075 * S), fur, 12)
        paw = ellipsoid("P", (x * S, (y - 0.01) * S, 0.012 * S), (0.026 * S, 0.032 * S, 0.014 * S), white, 10)
        part(name, [leg, paw], (x * S, y * S, 0.14 * S))
    os.makedirs(ART, exist_ok=True)
    export([root] + parts, os.path.join(ART, "Cat.fbx"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND, "cat.blend"))
    portrait_from_scene(Vector((0, -0.12 * S, 0.22 * S)), "cat", (0.55, 1.0, -0.25), 1.15, "#FFB35C")


# ---------------------------------------------------------------------------
# Toplanabilirler
# ---------------------------------------------------------------------------

def collectibles():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    leather = mat("CL_Leather", "#8A5A2B", 0.7)
    cord = mat("CL_Cord", "#D9B65A", 0.5, 0.3)
    gold = mat("CL_Gold", "#E8B95B", 0.25, 0.9, glow=0.4)
    fishm = mat("CL_Fish", "#8FB8C8", 0.25, 0.4)
    fishd = mat("CL_FishBack", "#3E6C86", 0.35, 0.3)
    # kese: torna profili + boğaz bağı + iki altın
    prof = [(0.0, 0.0), (0.09, 0.01), (0.15, 0.06), (0.165, 0.13), (0.14, 0.2), (0.07, 0.25), (0.045, 0.27), (0.07, 0.31), (0.05, 0.33), (0.0, 0.33)]
    b = bmesh.new()
    rings = [[b.verts.new((r * math.cos(2 * math.pi * k / 24), r * math.sin(2 * math.pi * k / 24), z)) for k in range(24)] for r, z in prof]
    for i in range(len(rings) - 1):
        for k in range(24):
            j = (k + 1) % 24
            b.faces.new((rings[i][k], rings[i][j], rings[i + 1][j], rings[i + 1][k]))
    pouch = mesh_obj("Pouch", b, [leather])
    tie = ellipsoid("Tie", (0, 0, 0.255), (0.06, 0.06, 0.018), cord, 16)
    c1 = ellipsoid("Coin1", (0.02, -0.03, 0.32), (0.04, 0.012, 0.04), gold, 16)
    c2 = ellipsoid("Coin2", (-0.03, 0.01, 0.31), (0.035, 0.035, 0.01), gold, 16)
    for o in (tie, c1, c2):
        o.select_set(True)
    pouch.select_set(True)
    bpy.context.view_layer.objects.active = pouch
    bpy.ops.object.join()
    pouch.data.transform(__import__("mathutils").Matrix.Translation((0, 0, -0.16)))
    pouch.scale = (1.6, 1.6, 1.6)
    bpy.ops.object.transform_apply(scale=True)
    # balık: gövde + kuyruk yüzgeci
    fb = ellipsoid("Fish", (0, 0, 0), (0.05, 0.2, 0.075), fishm, 22)
    back = ellipsoid("FishBack", (0, 0.0, 0.035), (0.04, 0.17, 0.035), fishd, 18)
    b = bmesh.new()
    vs = [b.verts.new(p) for p in ((0, 0.18, 0), (0.01, 0.3, 0.09), (0.01, 0.3, -0.09), (-0.01, 0.3, 0.09), (-0.01, 0.3, -0.09))]
    b.faces.new((vs[0], vs[1], vs[2]))
    b.faces.new((vs[0], vs[4], vs[3]))
    fin = mesh_obj("Fin", b, [fishd], smooth=False)
    bpy.ops.object.select_all(action="DESELECT")   # kese seçili kalmasın (balığa katılmasın)
    for s in (-1, 1):
        ellipsoid("FEye", (s * 0.035, -0.13, 0.025), (0.012, 0.012, 0.012), fishd, 8).select_set(True)
    back.select_set(True)
    fin.select_set(True)
    fb.select_set(True)
    bpy.context.view_layer.objects.active = fb
    bpy.ops.object.join()
    fb.scale = (1.7, 1.7, 1.7)
    bpy.ops.object.transform_apply(scale=True)
    fb.location = (0.8, 0, 0)
    os.makedirs(ART, exist_ok=True)
    export([pouch, fb], os.path.join(ART, "Collectibles.fbx"))
    # arayüz ikonları
    for name, o, d in (("mission_pouch", pouch, (0.3, 1.0, -0.25)), ("mission_fish", fb, (0.9, 0.6, -0.3))):
        icon_render(o, name, d)


def lights_at(target, rim="#FF9A5A"):
    t = Vector(target)
    for name, off, e, col, sz in (("Key", (-1.6, -2.2, 1.4), 400, "#FFE6CC", 1.4), ("Rim", (1.8, 1.6, 1.2), 480, rim, 1.0),
                                  ("Fill", (1.6, -2.0, -0.4), 110, "#BFD3FF", 2.0)):
        ld = bpy.data.lights.new(name, "AREA")
        ld.energy, ld.size = e, sz
        ld.color = srgb(col)[:3]
        lo = bpy.data.objects.new(name, ld)
        lo.location = t + Vector(off)
        lo.rotation_euler = (t - lo.location).to_track_quat("-Z", "Y").to_euler()
        bpy.context.scene.collection.objects.link(lo)


def render_setup(size):
    sc = bpy.context.scene
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = size
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    w = bpy.data.worlds.new("W")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs["Color"].default_value = srgb("#6E5A4A")
    bg.inputs["Strength"].default_value = 0.35
    sc.world = w
    return sc


def camera_at(target, view_dir, dist, lens=85, ortho=None):
    sc = bpy.context.scene
    cd = bpy.data.cameras.new("C")
    cd.lens = lens
    if ortho:
        cd.type = "ORTHO"
        cd.ortho_scale = ortho
    cam = bpy.data.objects.new("C", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    d = Vector(view_dir).normalized()
    cam.location = Vector(target) - d * dist
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()


def portrait_from_scene(target, name, view_dir, dist, rim):
    sc = render_setup(512)
    lights_at(target, rim)
    camera_at(target, view_dir, dist)
    os.makedirs(PORTRAITS, exist_ok=True)
    sc.render.filepath = os.path.join(PORTRAITS, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("PORTRAIT", name)


def icon_render(o, name, view_dir):
    sc = render_setup(256)
    for x in bpy.data.objects:
        if x.type == "MESH":
            x.hide_render = x is not o
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    c = sum(pts, Vector()) / 8
    size = max((p - c).length for p in pts) * 1.9
    lights_at(c)
    camera_at(c, view_dir, 6, ortho=size)
    sc.render.filepath = os.path.join(ROOT, "Assets", "Art", "UI", "Icons", name + ".png")
    bpy.ops.render.render(write_still=True)
    print("ICON", name)


thief()
cat()
collectibles()
print("THIEVES_OK")
