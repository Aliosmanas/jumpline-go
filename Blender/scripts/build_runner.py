"""
JUMPLINE GO koşucu karakteri — docs/references/character/design_sheet.jpg

Gerekli: Blender 5.x + MPFB eklentisi (MakeHuman) ve sistem asset paketi.
Çalıştır:  MCP ile exec (Blender açık) — `blender -b` de olur.
Çıktı:     Blender/source/runner.blend + Assets/Art/Characters/Runner/Runner.fbx

Kurgu:
1. MPFB ile atletik erkek gövdesi + oyun motoru iskeleti (tek parça, düzgün anatomi, hazır ağırlıklar).
2. Poligon azaltma + düz gölgeleme → referanstaki yüzeyli low-poly görünüm.
3. Renk bölgeleri gövdenin kendi yüzeyine boyanır (kemik ağırlığı + yüzey yönüne göre).
4. Zırh parçaları (omuzluk, dizlik, bant, göğüs plakası, bot) gövde yüzeyinden kopyalanıp dışa itilir: tam oturur,
   ağırlıkları gövdeden gelir.
5. Kask, vizör, göğüs çekirdeği, sırt ışıkları eklenir; hepsi tek mesh, tek iskelet.
Malzeme slotları (Unity kostüm sistemi adla eşler): M_Primary, M_Secondary, M_Armor, M_Glove, M_Glow, M_Visor.
"""
import importlib
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = r"D:\Repolar\junpline"
FBX_PATH = os.path.join(ROOT, "Assets", "Art", "Characters", "Runner", "Runner.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "runner.blend")

PHENOTYPE = {"age": 0.5, "cupsize": 0.5, "firmness": 0.7, "gender": 1.0, "height": 0.6,
             "muscle": 0.78, "proportions": 1.0, "weight": 0.42,
             "race": {"african": 0.0, "asian": 0.0, "caucasian": 1.0}}
FACET_ANGLE = 14.0      # derece: düzlemsel birleştirme açısı (yüzeyli low-poly görünüm)
SHELL_DECIMATE = 0.45

MAT_ORDER = ["M_Primary", "M_Secondary", "M_Armor", "M_Glove", "M_Glow", "M_Visor"]
PRIMARY, SECONDARY, ARMOR, GLOVE, GLOW, VISOR = range(6)
MATERIALS = {
    "M_Primary": ("#7A2238", 0.55, 0.15, None),
    "M_Secondary": ("#262B47", 0.6, 0.1, None),
    "M_Armor": ("#9EA9C8", 0.35, 0.6, None),
    "M_Glove": ("#1C2033", 0.7, 0.0, None),
    "M_Glow": ("#3FD8FF", 0.3, 0.0, ("#3FD8FF", 6.0)),
    "M_Visor": ("#AEB8D6", 0.15, 0.7, None),
}


def srgb(hexstr):
    h = hexstr.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def make_materials():
    for name, (color, rough, metal, emission) in MATERIALS.items():
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = srgb(color)
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Metallic"].default_value = metal
        if emission:
            bsdf.inputs["Emission Color"].default_value = srgb(emission[0])
            bsdf.inputs["Emission Strength"].default_value = emission[1]
        m.diffuse_color = srgb(color)


# ---------------------------------------------------------------------------
# 1-2. Gövde
# ---------------------------------------------------------------------------
def create_body():
    for o in list(bpy.data.objects):
        if o.name not in ("Camera", "Light"):
            bpy.data.objects.remove(o, do_unlink=True)
    for coll in (bpy.data.meshes, bpy.data.armatures):
        for d in list(coll):
            if d.users == 0:
                coll.remove(d)

    hs = importlib.import_module("bl_ext.user_default.mpfb.services.humanservice").HumanService
    info = {
        "clothes": [], "color_adjustments": {}, "expressions": [], "makeup": [], "targets": [],
        "eyebrows": "", "eyelashes": "", "eyes": "", "eyes_material_settings": {}, "eyes_material_type": "MAKESKIN",
        "hair": "", "proxy": "", "rig": "game_engine", "skin_material_settings": {}, "skin_material_type": "NONE",
        "skin_mhmat": "", "teeth": "", "tongue": "", "phenotype": PHENOTYPE,
    }
    settings = hs.get_default_deserialization_settings()
    settings["subdiv_levels"] = 0
    body = hs.deserialize_from_dict(info, settings)
    rig = body.parent
    bpy.context.view_layer.objects.active = body

    if body.data.shape_keys:  # MPFB biçim hedeflerini kalıcı yap
        body.shape_key_add(name="__mix", from_mix=True)
        co = [0.0] * (len(body.data.vertices) * 3)
        body.data.shape_keys.key_blocks["__mix"].data.foreach_get("co", co)
        body.shape_key_clear()
        body.data.vertices.foreach_set("co", co)
        body.data.update()

    gi = body.vertex_groups["body"].index
    helpers = [v.index for v in body.data.vertices if not any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in helpers], context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    for m in list(body.modifiers):
        if m.type == "MASK":
            body.modifiers.remove(m)
    deform = {b.name for b in rig.data.bones if b.use_deform}
    for g in list(body.vertex_groups):
        if g.name not in deform:
            body.vertex_groups.remove(g)

    body.data.materials.clear()
    for n in MAT_ORDER:
        body.data.materials.append(bpy.data.materials[n])
    body.name = body.data.name = "Runner"
    rig.name = rig.data.name = "RunnerRig"
    return body, rig


# ---------------------------------------------------------------------------
# Yardımcılar
# ---------------------------------------------------------------------------
class Skeleton:
    def __init__(self, rig):
        self.rig = rig

    def head(self, bone):
        return self.rig.matrix_world @ self.rig.data.bones[bone].head_local

    def tail(self, bone):
        return self.rig.matrix_world @ self.rig.data.bones[bone].tail_local

    def t_along(self, bone, p):
        a, b = self.head(bone), self.tail(bone)
        ab = b - a
        return (p - a).dot(ab) / ab.length_squared


def face_bones(obj):
    """Her yüz için en baskın deform kemiğinin adı."""
    names = {g.index: g.name for g in obj.vertex_groups}
    vdom = []
    for v in obj.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        vdom.append(names.get(best.group) if best else None)
    result = []
    for p in obj.data.polygons:
        votes = {}
        for vi in p.vertices:
            votes[vdom[vi]] = votes.get(vdom[vi], 0) + 1
        result.append(max(votes, key=votes.get))
    return result


def side_of(name):
    return 1 if name.endswith("_l") else -1 if name.endswith("_r") else 0


def is_finger(name):
    return name and name.split("_")[0] in ("thumb", "index", "middle", "ring", "pinky")


# ---------------------------------------------------------------------------
# 3. Renk bölgeleri
# ---------------------------------------------------------------------------
STRIPE_X = 0.042      # göğüs orta şeridi yarı genişliği
SIDE_X = 0.125        # bu |x|'in dışı lacivert yan panel (göğüste geniş, belde dar → V gövde)
SIDE_TOP_Z = 1.42
COLLAR_X = 0.11
BELT_Z = (1.065, 1.115)
ABDOMEN_TOP_Z = 1.2   # karın (lacivert) üst sınırı
BACK_PANEL_X = 0.09   # sırt orta paneli yarı genişliği
BACK_PANEL_Z = 1.25


def cut_planes(body, sk):
    """Renk sınırlarını mesh'e düz kenar olarak keser (merdiven görüntüsü olmasın)."""
    collar_z = sk.head("neck_01").z - 0.035
    planes = [((x, 0, 0), (1, 0, 0)) for x in (-STRIPE_X, STRIPE_X, -SIDE_X, SIDE_X, -COLLAR_X, COLLAR_X, -BACK_PANEL_X, BACK_PANEL_X)]
    planes += [((0, 0, z), (0, 0, 1)) for z in (collar_z, SIDE_TOP_Z, ABDOMEN_TOP_Z, BACK_PANEL_Z, *BELT_Z)]
    for side in ("l", "r"):
        a, b = sk.head("thigh_" + side), sk.tail("thigh_" + side)
        d = (b - a).normalized()
        lateral = d.cross(Vector((0, 1, 0))).normalized()
        planes.append((tuple(a), tuple(lateral)))
        back = lateral.cross(d).normalized()
        if back.y < 0:
            back = -back
        planes.append((tuple(a + back * 0.035), tuple(back)))
    bm = bmesh.new()
    bm.from_mesh(body.data)
    for co, no in planes:
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=co, plane_no=no, clear_inner=False, clear_outer=False)
    bm.to_mesh(body.data)
    bm.free()
    return collar_z


def paint_regions(body, sk):
    """Renk bölgeleri; sınırlar cut_planes ile kesilmiş düz kenarlara oturur."""
    collar_z = cut_planes(body, sk)
    bones = face_bones(body)
    thigh = {}
    for side in ("l", "r"):
        a, b = sk.head("thigh_" + side), sk.tail("thigh_" + side)
        d = (b - a).normalized()
        lateral = d.cross(Vector((0, 1, 0))).normalized()
        if lateral.x * (1 if side == "l" else -1) < 0:
            lateral = -lateral
        back = lateral.cross(d).normalized()
        if back.y < 0:
            back = -back
        thigh[side] = (a, lateral, back)

    for p, bone in zip(body.data.polygons, bones):
        c = p.center
        mat = PRIMARY
        torso = bone in ("pelvis", "spine_01", "spine_02", "spine_03")
        if (torso or (bone and bone.startswith("thigh"))) and c.z < BELT_Z[0]:
            # kemer altı: kalça + bacak aynı kuralla (dış/ön bordo, iç/arka lacivert)
            a, lateral, back = thigh["l" if c.x > 0 else "r"]
            inner = (c - a).dot(lateral) < 0 or (c - a).dot(back) > 0.035
            mat = SECONDARY if inner else PRIMARY
        elif torso and c.z < BELT_Z[1]:
            mat = PRIMARY                                                    # kemer
        elif torso and c.z < ABDOMEN_TOP_Z:
            mat = SECONDARY                                                  # karın
        elif torso or bone in ("neck_01", "head") or (bone and bone.startswith("clavicle") and abs(c.x) < COLLAR_X):
            if c.z > collar_z and abs(c.x) < COLLAR_X:
                mat = SECONDARY                                              # yaka / boyun
            elif bone in ("neck_01", "head"):
                mat = SECONDARY
            elif abs(c.x) < STRIPE_X and c.y < 0:
                mat = SECONDARY                                              # göğüs orta şeridi
            elif abs(c.x) < BACK_PANEL_X and c.y > 0.02 and c.z > BACK_PANEL_Z:
                mat = SECONDARY                                              # sırt paneli
            elif abs(c.x) > SIDE_X and c.z < SIDE_TOP_Z:
                mat = SECONDARY                                              # yan paneller
        elif bone and (bone.startswith("hand") or is_finger(bone)):
            mat = GLOVE
        elif bone and bone.startswith("lowerarm"):
            mat = SECONDARY if (c - sk.head(bone)).length < 0.05 else PRIMARY  # dirsek
        elif bone and bone.startswith(("upperarm", "clavicle")):
            mat = PRIMARY
        elif bone and bone.startswith("thigh"):
            a, lateral, back = thigh[bone[-1]]
            inner = (c - a).dot(lateral) < 0 or (c - a).dot(back) > 0.035
            mat = SECONDARY if inner else PRIMARY
        elif bone and bone.startswith("calf"):
            mat = SECONDARY
        p.material_index = mat
        p.use_smooth = False
    return bones


# ---------------------------------------------------------------------------
# 4. Gövdeye oturan zırh kabukları
# ---------------------------------------------------------------------------
def shell(body, faces, name, offset, thickness, material, sole_material=None, smooth=3, edge_smooth=8):
    """Seçili yüzleri kopyala, normal yönünde it, kalınlık ver. Ağırlıklar gövdeden gelir."""
    if not faces:
        return None
    keep = set(faces)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.normal_update()
    for v in bm.verts:
        v.co += v.normal * offset
    # kenarları düzelt, yüzeyi yuvarla (tırtık ve parmak izleri gitsin)
    edge = [v for v in bm.verts if v.is_boundary]
    for _ in range(edge_smooth):
        bmesh.ops.smooth_vert(bm, verts=edge, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for _ in range(smooth):
        bmesh.ops.smooth_vert(bm, verts=list(bm.verts), factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    if thickness > 0:
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=thickness)
    bm.normal_update()
    for f in bm.faces:
        f.material_index = sole_material if (sole_material is not None and f.normal.z < -0.5) else material
        f.smooth = False
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for g in body.vertex_groups:
        obj.vertex_groups.new(name=g.name)
    for n in MAT_ORDER:
        me.materials.append(bpy.data.materials[n])
    return obj


def add_armor(body, sk, bones):
    parts = []
    polys = body.data.polygons

    def select(pred):
        return [p.index for p, b in zip(polys, bones) if b and pred(p, b)]

    sh_z = sk.head("upperarm_l").z
    parts.append(shell(body, select(lambda p, b: b.startswith(("upperarm", "clavicle")) and p.center.z > sh_z - 0.07
                                    and abs(p.center.x) > 0.16 and p.normal.z > -0.3),
                       "ShoulderPads", 0.022, 0.014, ARMOR))
    parts.append(shell(body, select(lambda p, b: b == "spine_03" and p.normal.y < -0.35 and p.center.z > 1.43
                                    and 0.05 < abs(p.center.x) < 0.17),
                       "ChestPlates", 0.006, 0.006, ARMOR))
    parts.append(shell(body, select(lambda p, b: b.startswith(("calf", "thigh")) and p.normal.y < -0.2
                                    and (p.center - sk.head("calf_" + b[-1])).length < 0.08),
                       "KneePads", 0.014, 0.01, ARMOR))
    parts.append(shell(body, select(lambda p, b: b.startswith("lowerarm") and 0.5 < sk.t_along(b, p.center) < 0.8),
                       "Bracers", 0.008, 0.006, ARMOR))
    parts.append(shell(body, select(lambda p, b: b.startswith("calf") and 0.6 < sk.t_along(b, p.center) < 0.68),
                       "ShinBands", 0.007, 0.005, ARMOR))
    parts.append(shell(body, select(lambda p, b: b.startswith("hand") or is_finger(b)),
                       "Gloves", 0.004, 0.0, GLOVE, smooth=1, edge_smooth=2))
    return [p for p in parts if p]


def remove_feet(body, bones):
    """Bot içinde kalacak çıplak ayak yüzlerini sil (parmaklar bottan taşmasın)."""
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.faces.ensure_lookup_table()
    dead = [bm.faces[i] for i, b in enumerate(bones) if b and b.startswith(("foot", "ball"))]
    bmesh.ops.delete(bm, geom=dead, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(body.data)
    bm.free()


def add_boots(body, sk):
    """Yuvarlatılmış low-poly bot: topuk→burun gövde, bileği saran konç. Ön kısım ball, gövde foot, konç calf kemiğine."""
    parts = []
    for side in ("l", "r"):
        ankle, ball, toe = sk.head("foot_" + side), sk.head("ball_" + side), sk.tail("ball_" + side)
        x = (ankle.x + ball.x) / 2
        heel_y, toe_y = ankle.y + 0.07, toe.y - 0.03
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=2, use_grid_fill=True)
        for _ in range(3):
            bmesh.ops.smooth_vert(bm, verts=list(bm.verts), factor=0.6, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        length, width, height = heel_y - toe_y, 0.115, 0.13
        for v in bm.verts:
            u = v.co.y + 0.5            # 0 burun, 1 topuk
            w = v.co.z + 0.5            # 0 taban, 1 üst
            taper = 0.55 + 0.45 * min(1.0, u * 2.2)          # burunda incelir
            top = height * (0.45 + 0.55 * min(1.0, u * 1.6))  # burun alçak, topuk yüksek
            v.co = Vector((x + v.co.x * width * taper, toe_y + u * length, w * top))
        bm.normal_update()
        dl = bm.verts.layers.deform.verify()
        gi_foot, gi_ball = body.vertex_groups["foot_" + side].index, body.vertex_groups["ball_" + side].index
        for v in bm.verts:
            v[dl][gi_ball if v.co.y < ball.y else gi_foot] = 1.0
        for f in bm.faces:
            f.material_index = ARMOR if f.normal.z < -0.6 else PRIMARY
            f.smooth = False
        parts.append(_mesh_part(f"Boot_{side}", bm, body))

        # Konç: bileği saran kısa silindir
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=False, segments=10, radius1=0.068, radius2=0.062, depth=0.11)
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((ankle.x, ankle.y + 0.012, ankle.z + 0.06)))
        dl = bm.verts.layers.deform.verify()
        gi_calf = body.vertex_groups["calf_" + side].index
        for v in bm.verts:
            v[dl][gi_calf if v.co.z > ankle.z + 0.03 else gi_foot] = 1.0
        for f in bm.faces:
            f.material_index = PRIMARY
            f.smooth = False
        parts.append(_mesh_part(f"BootCuff_{side}", bm, body))
    return parts


def _mesh_part(name, bm, body):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for g in body.vertex_groups:
        obj.vertex_groups.new(name=g.name)
    for n in MAT_ORDER:
        me.materials.append(bpy.data.materials[n])
    return obj


# ---------------------------------------------------------------------------
# 5. Kask, çekirdek, ışıklar
# ---------------------------------------------------------------------------
def new_part(name, bm, bone, body, material):
    for f in bm.faces:
        f.material_index = material
        f.smooth = False
    dl = bm.verts.layers.deform.verify()
    gi = body.vertex_groups[bone].index
    for v in bm.verts:
        v[dl][gi] = 1.0
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for g in body.vertex_groups:
        obj.vertex_groups.new(name=g.name)
    for n in MAT_ORDER:
        me.materials.append(bpy.data.materials[n])
    return obj


def head_bounds(body, bones):
    names = {g.index: g.name for g in body.vertex_groups}
    pts = []
    for v in body.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        if best and names.get(best.group) == "head":
            pts.append(v.co.copy())
    mn = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def add_helmet(body, bones):
    mn, mx = head_bounds(body, bones)
    c = (mn + mx) / 2
    size = mx - mn
    center = Vector((0.0, c.y + size.y * 0.04, c.z + size.z * 0.06))
    radii = Vector((size.x * 0.66, size.y * 0.66, size.z * 0.6))
    parts = []

    def sphere():
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=12, radius=1.0)
        return bm

    # Kask kabuğu (alt açık, boyun görünsün)
    bm = sphere()
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < -0.62], context="FACES")
    for v in bm.verts:
        x, y, z = v.co
        if y > 0:
            y *= 1.08  # ense
        v.co = Vector((x * radii.x, y * radii.y, z * radii.z)) + center
    bm.normal_update()
    bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.008)
    parts.append(new_part("Helmet", bm, "head", body, PRIMARY))

    # Vizör: ön bant, kaskın hemen önünde
    bm = sphere()
    keep = [f for f in bm.faces if f.calc_center_median().y < -0.66 and -0.36 < f.calc_center_median().z < 0.16]
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f not in keep], context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    for v in bm.verts:
        x, y, z = v.co
        v.co = Vector((x * radii.x * 1.03, y * radii.y * 1.03, z * radii.z * 1.03)) + center
    parts.append(new_part("Visor", bm, "head", body, VISOR))

    # Yan diskler
    for s in (-1, 1):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=radii.z * 0.32, radius2=radii.z * 0.26, depth=0.025)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90 * s), 3, "Y"))
        bmesh.ops.translate(bm, verts=bm.verts, vec=center + Vector((radii.x * 0.98 * s, radii.y * 0.05, -radii.z * 0.05)))
        parts.append(new_part(f"Ear{s}", bm, "head", body, ARMOR))
    return parts


def surface_point(body, bones, bone_names, x, z, front=True, window=0.03):
    """Gövde yüzeyinde (x, z) civarındaki en öndeki / en arkadaki noktanın y'si."""
    names = {g.index: g.name for g in body.vertex_groups}
    ys = []
    for v in body.data.vertices:
        if abs(v.co.x - x) < window and abs(v.co.z - z) < window:
            best = max(v.groups, key=lambda g: g.weight, default=None)
            if best and names.get(best.group) in bone_names:
                ys.append(v.co.y)
    return (min(ys) if front else max(ys)) if ys else 0.0


def add_glows(body, bones, plates_offset):
    parts = []
    chest = ("spine_03", "spine_02")
    z_core = 1.42
    y = surface_point(body, bones, chest, 0.0, z_core, front=True) - 0.006
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.032, radius2=0.032, depth=0.016)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0.0, y, z_core)))
    parts.append(new_part("ChestCore", bm, "spine_03", body, GLOW))

    for s in (-1, 1):
        x, z = 0.045 * s, 1.43
        yb = surface_point(body, bones, chest, x, z, front=False, window=0.04) + plates_offset + 0.012
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        bmesh.ops.scale(bm, vec=(0.016, 0.01, 0.15), verts=bm.verts)
        bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(-28 * s), 3, "Y"))
        bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((x, yb, z)))
        parts.append(new_part(f"BackGlow{s}", bm, "spine_03", body, GLOW))
    return parts


# ---------------------------------------------------------------------------
def facet(obj):
    """Düzlemsel birleştirme: renk sınırlarını koruyarak yakın düzlemdeki yüzleri birleştirir → yüzeyli görünüm."""
    bpy.context.view_layer.objects.active = obj
    dec = obj.modifiers.new("Facet", "DECIMATE")
    dec.decimate_type = "DISSOLVE"
    dec.angle_limit = math.radians(FACET_ANGLE)
    dec.delimit = {"MATERIAL"}
    bpy.ops.object.modifier_move_to_index(modifier="Facet", index=0)
    bpy.ops.object.modifier_apply(modifier="Facet")
    tri = obj.modifiers.new("Tri", "TRIANGULATE")
    bpy.ops.object.modifier_move_to_index(modifier="Tri", index=0)
    bpy.ops.object.modifier_apply(modifier="Tri")


def reduce(obj, ratio):
    bpy.context.view_layer.objects.active = obj
    dec = obj.modifiers.new("Reduce", "DECIMATE")
    dec.ratio = ratio
    bpy.ops.object.modifier_apply(modifier="Reduce")


def build():
    make_materials()
    body, rig = create_body()
    sk = Skeleton(rig)
    bones = paint_regions(body, sk)
    parts = add_armor(body, sk, bones)
    remove_feet(body, bones)
    parts += add_boots(body, sk)
    parts += add_helmet(body, bones)
    parts += add_glows(body, bones, 0.0)
    facet(body)
    for part in parts:
        if part.name.startswith(("ShoulderPads", "ChestPlates", "KneePads", "Bracers", "ShinBands", "Boots", "BackPlate", "Gloves")):
            reduce(part, SHELL_DECIMATE)

    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = False
    return rig, body


def export(rig, body):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=True, object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False,
        use_armature_deform_only=True, bake_anim=False,
    )
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, copy=True)
    bpy.ops.object.select_all(action="DESELECT")


_rig, _body = build()
export(_rig, _body)
print("RUNNER_OK", len(_body.data.polygons), [m.name for m in _body.data.materials])
