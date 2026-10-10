"""
ŞAHMERAN — bölüm 2 canavarı (Mardin). Kendi tasarımımız: taçlı kadın gövdesi + uzun yılan kuyruğu.

Kurgu (hepsi ücretsiz/CC0: MPFB/MakeHuman gövdesi):
 1. MPFB kadın gövdesi + game_engine iskeleti (Tepegöz ile aynı kemik adları → aynı poz sistemi).
 2. Bacaklar silinir; kalçadan başlayan, yere inip geriye uzanan sivrilen yılan kuyruğu (12 kemik zinciri, pullu doku).
 3. Gövdede altın-yeşil pullu korse (aynı pul dokusu), siyah saç başlığı + uzun saç, altın taç, iki göz.
 4. Animasyonlar (30 fps, Tepegöz klip adlarıyla): Idle, Run (süzülerek kaçış: kuyrukta ilerleyen dalga), Roar,
    Throw, Hit, Defeat.
Çıktı: Assets/Art/Boss/Sahmeran.fbx (+ Sahmeran_Scales.png), Blender/source/sahmeran.blend
Çalıştır: blender -b -P Blender/scripts/build_sahmeran.py
"""
import importlib
import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = r"D:\Repolar\junpline"
ART = os.path.join(ROOT, "Assets", "Art", "Boss")
FBX = os.path.join(ART, "Sahmeran.fbx")
SCALES_PNG = os.path.join(ART, "Sahmeran_Scales.png")
BLEND = os.path.join(ROOT, "Blender", "source", "sahmeran.blend")
FPS = 30

PHENOTYPE = {"age": 0.45, "cupsize": 0.5, "firmness": 0.7, "gender": 0.0, "height": 0.8,
             "muscle": 0.55, "proportions": 1.0, "weight": 0.45,
             "race": {"african": 0.05, "asian": 0.15, "caucasian": 0.8}}
LEG_BONES = ("thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r")
TAIL_BONES = 12
TAIL_LEN = 2.6          # insan ölçeğinde (Unity'de boya göre büyütülür)
TAIL_RINGS_PER_BONE = 3
TAIL_SEGS = 16


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, metal=0.0, rough=0.5, glow=0.0, image=None):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if glow:
        b.inputs["Emission Color"].default_value = srgb(col)
        b.inputs["Emission Strength"].default_value = glow
    if image is not None:
        tn = m.node_tree.nodes.new("ShaderNodeTexImage")
        tn.image = image
        m.node_tree.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    return m


# ---------------------------------------------------------------------------
# 1. Gövde
# ---------------------------------------------------------------------------

def create_body():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    hs = importlib.import_module("bl_ext.user_default.mpfb.services.humanservice").HumanService
    info = {"clothes": [], "color_adjustments": {}, "expressions": [], "makeup": [], "targets": [],
            "eyebrows": "", "eyelashes": "", "eyes": "", "eyes_material_settings": {}, "eyes_material_type": "MAKESKIN",
            "hair": "", "proxy": "", "rig": "game_engine", "skin_material_settings": {}, "skin_material_type": "NONE",
            "skin_mhmat": "", "teeth": "", "tongue": "", "phenotype": PHENOTYPE}
    st = hs.get_default_deserialization_settings()
    st["subdiv_levels"] = 0
    body = hs.deserialize_from_dict(info, st)
    rig = body.parent
    bpy.context.view_layer.objects.active = body
    if body.data.shape_keys:
        body.shape_key_add(name="__mix", from_mix=True)
        co = [0.0] * (len(body.data.vertices) * 3)
        body.data.shape_keys.key_blocks["__mix"].data.foreach_get("co", co)
        body.shape_key_clear()
        body.data.vertices.foreach_set("co", co)
        body.data.update()

    def centroid(group):
        gi = body.vertex_groups[group].index
        pts = [v.co.copy() for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
        return sum(pts, Vector()) / len(pts), max((p - sum(pts, Vector()) / len(pts)).length for p in pts)

    eyes = (centroid("helper-l-eye"), centroid("helper-r-eye"))
    gi = body.vertex_groups["body"].index
    leg = {body.vertex_groups[n].index for n in LEG_BONES if n in body.vertex_groups}
    kill = []
    for v in body.data.vertices:
        if not any(g.group == gi and g.weight > 0.5 for g in v.groups):
            kill.append(v.index)   # yardımcı (göz, diş…) mesh'leri
        elif sum(g.weight for g in v.groups if g.group in leg) > 0.45:
            kill.append(v.index)   # bacaklar: yerine kuyruk
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.verts[i] for i in kill], context="VERTS")
    bm.to_mesh(body.data)
    bm.free()
    for m in list(body.modifiers):
        if m.type == "MASK":
            body.modifiers.remove(m)
    deform = {b.name for b in rig.data.bones if b.use_deform}
    for g in list(body.vertex_groups):
        if g.name not in deform or g.name in LEG_BONES:
            body.vertex_groups.remove(g)
    body.name = body.data.name = "Sahmeran"
    rig.name = rig.data.name = "SahmeranRig"
    return body, rig, eyes


# ---------------------------------------------------------------------------
# 2. Kuyruk (kemik zinciri + mesh)
# ---------------------------------------------------------------------------

def tail_path(start):
    """Kalçadan aşağı-geriye kıvrılıp yerde +y'ye (koşucunun tersine) uzanan yol. Koşu yönü -y."""
    pts = []
    n = TAIL_BONES
    for i in range(n + 1):
        t = i / n
        drop = min(1.0, t / 0.28)                       # ilk %28'de yere iner
        z = start.z + (0.14 - start.z) * (1 - (1 - drop) ** 2)
        y = start.y - 0.02 + TAIL_LEN * t
        pts.append(Vector((0.0, y, z)))
    return pts


def add_tail_bones(rig, pts):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = rig.data.edit_bones
    inv = rig.matrix_world.inverted()
    parent = eb["pelvis"]
    for name in LEG_BONES:
        if name in eb:
            eb.remove(eb[name])
    for i in range(TAIL_BONES):
        b = eb.new(f"tail_{i + 1:02d}")
        b.head = inv @ pts[i]
        b.tail = inv @ pts[i + 1]
        b.parent = parent
        b.use_connect = i > 0
        b.use_deform = True
        parent = b
    bpy.ops.object.mode_set(mode="OBJECT")


def hip_radius(body, z):
    """Kalça kesitinin yarı genişlik/derinliği — yalnız gövde (pelvis/spine_01) köşeleri; sarkan eller sayılmaz."""
    torso = {body.vertex_groups[n].index for n in ("pelvis", "spine_01") if n in body.vertex_groups}
    sel = [v.co for v in body.data.vertices
           if abs(v.co.z - z) < 0.05 and sum(g.weight for g in v.groups if g.group in torso) > 0.5]
    xs = [abs(c.x) for c in sel]
    ys = [c.y for c in sel]
    return (max(xs) if xs else 0.17), ((max(ys) - min(ys)) * 0.5 if ys else 0.12)


def make_tail(body, rig, pts, mi_scale, mi_belly):
    rx, ry = hip_radius(body, pts[0].z)
    rings = TAIL_BONES * TAIL_RINGS_PER_BONE + 1
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    ring_verts, ring_t = [], []
    for r in range(rings):
        t = r / (rings - 1)
        f = t * TAIL_BONES
        i = min(int(f), TAIL_BONES - 1)
        a = pts[i].lerp(pts[i + 1], f - i)
        d = (pts[i + 1] - pts[i]).normalized()
        side = Vector((1, 0, 0))
        up = d.cross(side).normalized()
        if up.z < 0:
            up = -up
        k = (1 - t) ** 0.85
        sx = max(0.018, rx * 1.12 * k)
        sz = max(0.016, (ry * 1.3 if t < 0.12 else rx * 0.82) * k)
        vs = []
        for s in range(TAIL_SEGS):
            ang = 2 * math.pi * s / TAIL_SEGS
            p = a + side * math.cos(ang) * sx + up * math.sin(ang) * sz
            if t > 0.25 and math.sin(ang) < -0.3:   # yerdeki kısım: karın yassı
                p = p + up * (-math.sin(ang) - 0.3) * sz * 0.35
            vs.append(bm.verts.new(p))
        ring_verts.append(vs)
        ring_t.append(t)
    top = bm.faces.new(list(reversed(ring_verts[0])))   # üst ağız kapalı (kalça içinde kalır)
    top.material_index = mi_scale
    tip = bm.verts.new(pts[-1] + (pts[-1] - pts[-2]).normalized() * 0.08)
    for r in range(rings - 1):
        for s in range(TAIL_SEGS):
            a, b = ring_verts[r][s], ring_verts[r][(s + 1) % TAIL_SEGS]
            c, d = ring_verts[r + 1][(s + 1) % TAIL_SEGS], ring_verts[r + 1][s]
            face = bm.faces.new((a, b, c, d))
            belly = math.sin(2 * math.pi * (s + 0.5) / TAIL_SEGS) < -0.55
            face.material_index = mi_belly if belly and ring_t[r] > 0.2 else mi_scale
            for loop, (u, v) in zip(face.loops, ((s, r), (s + 1, r), (s + 1, r + 1), (s, r + 1))):
                loop[uv].uv = (u / TAIL_SEGS * 2.0, v / 3.0)
    for s in range(TAIL_SEGS):
        face = bm.faces.new((ring_verts[-1][s], ring_verts[-1][(s + 1) % TAIL_SEGS], tip))
        face.material_index = mi_scale
    me = bpy.data.meshes.new("Tail")
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Tail", me)
    bpy.context.collection.objects.link(obj)
    for m in body.data.materials:
        me.materials.append(m)
    # ağırlık: her halka iki komşu kemik arasında doğrusal; ilk halkalar kalçaya karışır
    groups = {n: obj.vertex_groups.new(name=n) for n in ["pelvis"] + [f"tail_{i + 1:02d}" for i in range(TAIL_BONES)]}
    idx = 0
    for r in range(rings):
        f = ring_t[r] * TAIL_BONES - 0.5
        i0 = max(0, min(TAIL_BONES - 1, math.floor(f)))
        i1 = min(TAIL_BONES - 1, i0 + 1)
        w1 = min(1.0, max(0.0, f - i0))
        for _ in range(TAIL_SEGS):
            if ring_t[r] < 0.04:
                groups["pelvis"].add([idx], 1.0 - ring_t[r] / 0.04, "REPLACE")
            groups[f"tail_{i0 + 1:02d}"].add([idx], 1.0 - w1, "ADD")
            if i1 != i0:
                groups[f"tail_{i1 + 1:02d}"].add([idx], w1, "ADD")
            idx += 1
    groups[f"tail_{TAIL_BONES:02d}"].add([idx], 1.0, "REPLACE")
    obj.parent = rig
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    return obj


# ---------------------------------------------------------------------------
# 3. Doku, korse, saç, taç, gözler
# ---------------------------------------------------------------------------

def scale_texture(size=512):
    """Üst üste binen yarım daire pullar: altın → zeytin yeşili, kenarı koyu."""
    y, x = np.mgrid[0:size, 0:size] / size
    cells = 8
    cx = x * cells
    cy = y * cells * 1.4
    row = np.floor(cy)
    cx = cx + (row % 2) * 0.5
    fx = cx - np.floor(cx) - 0.5
    fy = cy - row
    d = np.sqrt(fx ** 2 + (fy * 0.9) ** 2)
    edge = np.clip((d - 0.38) / 0.14, 0, 1)
    shade = np.clip(1.0 - fy * 0.55, 0, 1)
    gold = np.array([0.78, 0.6, 0.18])
    olive = np.array([0.3, 0.42, 0.14])
    base = gold * shade[..., None] + olive * (1 - shade[..., None])
    img = base * (1 - edge[..., None] * 0.65)
    rgba = np.concatenate([img, np.ones((size, size, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new("Sahmeran_Scales", size, size)
    im.pixels.foreach_set(rgba[::-1].ravel())
    im.filepath_raw = SCALES_PNG
    im.file_format = "PNG"
    im.save()
    return im


def assign_corset(body, mi_corset, waist_z, chest_z):
    """Gövde kemiklerine bağlı, bel ile koltuk altı arası yüzler pullu korse (kollar/boyun ten)."""
    torso = {body.vertex_groups[n].index for n in ("pelvis", "spine_01", "spine_02", "spine_03") if n in body.vertex_groups}
    me = body.data
    uv = me.uv_layers.active
    for p in me.polygons:
        c = p.center
        if not (waist_z - 0.25 < c.z < chest_z):
            continue
        ws = [sum(g.weight for g in me.vertices[v].groups if g.group in torso) for v in p.vertices]
        if min(ws) > 0.6:
            p.material_index = mi_corset
    if uv is not None:   # pul dokusu gövdede silindirik eşlensin
        for p in me.polygons:
            if p.material_index != mi_corset:
                continue
            for li in p.loop_indices:
                co = me.vertices[me.loops[li].vertex_index].co
                uv.data[li].uv = (math.atan2(co.x, co.y) / math.pi + 1.0, co.z * 6.0)


def add_sphere(name, center, radius, mi, bone, rig, mats, squash=1.0, seg=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=seg // 2 + 2, radius=radius)
    for v in bm.verts:
        v.co.z *= squash
        v.co += center
    for f in bm.faces:
        f.material_index = mi
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        me.materials.append(m)
    g = obj.vertex_groups.new(name=bone)
    g.add(list(range(len(me.vertices))), 1.0, "REPLACE")
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    return obj


def head_frame(body, rig):
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    back = max(p.y for p in pts)
    half = max(abs(p.x) for p in pts)
    return head, top, back, half


def make_eyes(eyes, rig, mats, mi):
    parts = []
    for i, (c, r) in enumerate(eyes):
        r = max(0.011, min(r, 0.016))
        parts.append(add_sphere(f"EyeWhite{i}", c, r, mi["eye"], "head", rig, mats))
        parts.append(add_sphere(f"Iris{i}", c + Vector((0, -r * 0.82, 0)), r * 0.45, mi["iris"], "head", rig, mats, squash=1.0, seg=12))
        brow = add_sphere(f"Brow{i}", c + Vector((0, -r * 0.95, r * 1.3)), r * 1.25, mi["hair"], "head", rig, mats, seg=10)
        for v in brow.data.vertices:   # ince kemer kaş
            d = v.co - (c + Vector((0, -r * 0.95, r * 1.3)))
            v.co = c + Vector((0, -r * 0.95, r * 1.3)) + Vector((d.x * 1.3, d.y * 0.4, d.z * 0.25 - abs(d.x) * 0.25))
        parts.append(brow)
    return parts


def make_hair(body, rig, mats, mi):
    head, top, back, half = head_frame(body, rig)
    gi = body.vertex_groups["head"].index
    hy = [v.co.y for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    depth = (max(hy) - min(hy)) * 0.5
    c = Vector((0, (max(hy) + min(hy)) * 0.5 + 0.004, (head.z + top) * 0.5 + 0.01))
    cap = add_sphere("HairCap", c, half * 1.18, mi["hair"], "head", rig, mats, squash=1.1, seg=24)
    for v in cap.data.vertices:   # başın derinliğine uy (baş genişliğinden uzun)
        v.co.y = c.y + (v.co.y - c.y) * (depth * 1.2) / (half * 1.18)
    # yüzü açmak için ön-alt yarıyı kes
    me = cap.data
    bm = bmesh.new()
    bm.from_mesh(me)
    kill = [v for v in bm.verts if v.co.y < c.y - depth * 0.3 and v.co.z < c.z + half * 0.12]   # alın saçla örtülü, yüz açık
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(me)
    bm.free()
    # sırta dökülen uzun saç: arkada yassı, uçta sivri bir perde (başa ve göğüs kemiklerine bağlı)
    bm = bmesh.new()
    rows, cols = 8, 7
    grid = []
    for r in range(rows + 1):
        t = r / rows
        z = c.z - t * 0.55
        w = half * (1.5 - 0.7 * t)
        y = back + 0.02 + t * 0.06
        grid.append([bm.verts.new((-w + 2 * w * k / cols, y + 0.02 * math.sin(math.pi * k / cols), z)) for k in range(cols + 1)])
    for r in range(rows):
        for k in range(cols):
            f = bm.faces.new((grid[r][k], grid[r][k + 1], grid[r + 1][k + 1], grid[r + 1][k]))
            f.material_index = mi["hair"]
    me = bpy.data.meshes.new("HairLong")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("HairLong", me)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        me.materials.append(m)
    gh, gs = obj.vertex_groups.new(name="head"), obj.vertex_groups.new(name="spine_03")
    for v in me.vertices:
        t = min(1.0, max(0.0, (c.z - v.co.z) / 0.55))
        gh.add([v.index], 1 - t, "REPLACE")
        gs.add([v.index], t, "REPLACE")
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    obj.modifiers.new("Solid", "SOLIDIFY").thickness = 0.012
    return [cap, obj], (c, half, top)


def make_crown(rig, mats, mi, frame):
    c, half, top = frame
    bm = bmesh.new()
    segs, r, z0 = 24, half * 1.02, top - 0.015
    lower, upper = [], []
    for s in range(segs):
        a = 2 * math.pi * s / segs
        x, y = math.cos(a) * r, math.sin(a) * r * 1.08
        lower.append(bm.verts.new((x, c.y + y, z0)))
        spike = 0.055 if s % 2 == 0 else 0.022
        upper.append(bm.verts.new((x * 0.97, c.y + y * 0.97, z0 + spike)))
    for s in range(segs):
        f = bm.faces.new((lower[s], lower[(s + 1) % segs], upper[(s + 1) % segs], upper[s]))
        f.material_index = mi["gold"]
    me = bpy.data.meshes.new("Crown")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Crown", me)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        me.materials.append(m)
    obj.vertex_groups.new(name="head").add(list(range(len(me.vertices))), 1.0, "REPLACE")
    obj.parent = rig
    obj.modifiers.new("Armature", "ARMATURE").object = rig
    obj.modifiers.new("Solid", "SOLIDIFY").thickness = 0.008
    gem = add_sphere("CrownGem", Vector((0, c.y - r * 1.08, z0 + 0.03)), 0.012, mi["gem"], "head", rig, mats, seg=10)
    return [obj, gem]


# ---------------------------------------------------------------------------
# 4. Animasyonlar (Tepegöz poz sistemi; bacak yerine kuyruk dalgası)
# ---------------------------------------------------------------------------

ORDER = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
         "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r"]


def V(x, y, z):
    return Vector((x, y, z)).normalized()


STAND = {
    "spine_01": V(0, -0.02, 1), "spine_02": V(0, 0.0, 1), "spine_03": V(0, 0.02, 1), "neck_01": V(0, -0.2, 1), "head": V(0, -0.05, 1),
    "upperarm_l": V(0.4, 0.05, -1), "lowerarm_l": V(0.25, -0.3, -1), "upperarm_r": V(-0.4, 0.05, -1), "lowerarm_r": V(-0.25, -0.3, -1),
}


def P(base=STAND, **kw):
    d = dict(base)
    d.update(kw)
    return d


def mirror(pose):
    out = {}
    for k, v in pose.items():
        if not isinstance(v, Vector):
            out[k] = v
            continue
        nk = k[:-2] + ("_r" if k.endswith("_l") else "_l") if k[-2:] in ("_l", "_r") else k
        out[nk] = Vector((-v.x, v.y, v.z))
    return out


SLITHER_A = P(spine_01=V(0.08, -0.15, 1), spine_02=V(0.06, -0.2, 1), spine_03=V(0.04, -0.18, 1), neck_01=V(0, -0.35, 1),
              upperarm_l=V(0.55, -0.45, -0.7), lowerarm_l=V(0.3, -0.8, -0.3), upperarm_r=V(-0.55, 0.35, -0.75), lowerarm_r=V(-0.3, -0.4, -0.85))
ROAR_LOAD = P(spine_02=V(0, -0.35, 1), spine_03=V(0, -0.4, 0.9), neck_01=V(0, -0.5, 0.85), head=V(0, -0.3, 0.9),
              upperarm_l=V(0.6, -0.4, -0.7), upperarm_r=V(-0.6, -0.4, -0.7))
ROAR = P(spine_02=V(0, 0.2, 1), spine_03=V(0, 0.3, 1), neck_01=V(0, 0.15, 1), head=V(0, 0.45, 0.9),
         upperarm_l=V(0.9, 0.1, 0.5), lowerarm_l=V(0.4, 0.0, 1), upperarm_r=V(-0.9, 0.1, 0.5), lowerarm_r=V(-0.4, 0.0, 1))
ROAR_SHAKE = P(ROAR, head=V(0.15, 0.45, 0.9), upperarm_l=V(0.95, 0.2, 0.55))
THROW_WIND = P(SLITHER_A, spine_03=V(-0.15, -0.45, 0.9), upperarm_r=V(-0.35, -0.75, -0.55), lowerarm_r=V(-0.1, -0.9, -0.4))
THROW_UP = P(SLITHER_A, spine_03=V(-0.1, 0.05, 1), upperarm_r=V(-0.25, -0.2, 1), lowerarm_r=V(-0.1, 0.25, 1))
THROW_REL = P(SLITHER_A, spine_03=V(0.12, 0.2, 1), upperarm_r=V(-0.3, 0.8, 0.5), lowerarm_r=V(-0.2, 0.95, -0.1))
HIT = P(SLITHER_A, spine_01=V(0, -0.5, 0.85), spine_02=V(0, -0.6, 0.75), spine_03=V(0, -0.5, 0.8), head=V(0, 0.35, 0.9),
        upperarm_l=V(0.85, 0.35, 0.35), upperarm_r=V(-0.85, 0.35, 0.35))
SLUMP = P(spine_01=V(0, -0.8, 0.5), spine_02=V(0, -0.95, 0.3), spine_03=V(0, -1, 0.1), neck_01=V(0, -1, 0.0), head=V(0, -1, -0.1),
          upperarm_l=V(0.5, -0.6, -0.6), upperarm_r=V(-0.5, -0.6, -0.6), lowerarm_l=V(0.3, -0.9, -0.2), lowerarm_r=V(-0.3, -0.9, -0.2))
DOWN = P(SLUMP, pelvis_drop=0.45, spine_01=V(0, -1, 0.15), spine_02=V(0, -1, 0.05), spine_03=V(0, -1, 0.0),
         upperarm_l=V(0.95, -0.2, 0.0), upperarm_r=V(-0.95, -0.2, 0.0))


def tail_wave(amp, phase, lift=0.0, curl=0.0):
    """Kuyruk kemiği başına (yanal açı, dikey açı): uca doğru büyüyen ilerleyen dalga."""
    out = []
    for i in range(TAIL_BONES):
        t = i / (TAIL_BONES - 1)
        side = amp * (0.25 + t) * math.sin(phase - i * 0.75)
        up = lift * (t ** 2) + curl * math.sin(phase * 0.5 + t * 3)
        out.append((side, up))
    return out


def apply_pose(rig, pose, tail):
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    if pose.get("pelvis_drop"):
        p = pb["pelvis"]
        p.matrix = Matrix.Translation((0, -0.15, -pose["pelvis_drop"])) @ p.matrix
        bpy.context.view_layer.update()
    for name in ORDER:
        if name not in pose or name not in pb:
            continue
        b = pb[name]
        cur = (b.tail - b.head).normalized()
        target = (rig.matrix_world.to_3x3().inverted() @ pose[name]).normalized()
        q = cur.rotation_difference(target)
        m = b.matrix.copy()
        loc = m.to_translation()
        b.matrix = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m
        bpy.context.view_layer.update()
    for i, (side, up) in enumerate(tail):
        b = pb[f"tail_{i + 1:02d}"]
        rest = rig.data.bones[b.name].matrix_local.to_3x3()
        zl = (rest.inverted() @ Vector((0, 0, 1))).normalized()
        xl = (rest.inverted() @ Vector((1, 0, 0))).normalized()
        b.rotation_quaternion = Quaternion(zl, side) @ Quaternion(xl, up)


def build_actions(rig):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    rig.animation_data_create()
    tau = 2 * math.pi
    run_keys = [(f, mirror(SLITHER_A) if f % 12 >= 6 else SLITHER_A, tail_wave(0.32, tau * f / 24)) for f in range(0, 25, 3)]
    idle_keys = [(f, P(STAND, spine_03=V(0.03 * math.sin(tau * f / 80), 0.02, 1)), tail_wave(0.12, tau * f / 80)) for f in range(0, 81, 10)]
    actions = {
        "Idle": (idle_keys, True),
        "Run": (run_keys, True),
        "Roar": ([(0, STAND, tail_wave(0.1, 0)), (8, ROAR_LOAD, tail_wave(0.15, 1, lift=0.2)), (16, ROAR, tail_wave(0.2, 2, lift=0.9)),
                  (22, ROAR_SHAKE, tail_wave(0.3, 3, lift=0.9)), (30, ROAR, tail_wave(0.2, 4, lift=0.8)),
                  (38, ROAR_SHAKE, tail_wave(0.3, 5, lift=0.8)), (52, STAND, tail_wave(0.1, 6))], False),
        "Throw": ([(0, SLITHER_A, tail_wave(0.3, 0)), (6, THROW_WIND, tail_wave(0.3, 1.6)), (13, THROW_UP, tail_wave(0.3, 3.2)),
                   (18, THROW_REL, tail_wave(0.3, 4.4)), (26, mirror(SLITHER_A), tail_wave(0.3, 6.3))], False),
        "Hit": ([(0, SLITHER_A, tail_wave(0.3, 0)), (3, HIT, tail_wave(0.5, 0.8, lift=0.4)), (12, P(HIT, spine_02=V(0, -0.45, 0.9)), tail_wave(0.4, 2)),
                 (20, SLITHER_A, tail_wave(0.3, 3.2))], False),
        "Defeat": ([(0, HIT, tail_wave(0.4, 0)), (8, SLUMP, tail_wave(0.5, 1.5, lift=0.3)), (18, DOWN, tail_wave(0.2, 3)),
                    (23, P(DOWN, pelvis_drop=0.4), tail_wave(0.15, 3.4)), (30, DOWN, tail_wave(0.05, 3.6))], False),
    }
    made = []
    for name, (keys, loop) in actions.items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for frame, pose, tail in keys:
            apply_pose(rig, pose, tail)
            for b in rig.pose.bones:
                b.keyframe_insert("rotation_quaternion", frame=frame)
                b.keyframe_insert("location", frame=frame)
        act["loop"] = loop
        made.append(name)
    rig.animation_data.action = None
    for b in rig.pose.bones:
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.scene.render.fps = FPS
    return made


# ---------------------------------------------------------------------------

def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = create_body()
    scales = scale_texture()
    mats = {
        "skin": mat("SM_Skin", "#D9A47E", 0.0, 0.55),
        "corset": mat("SM_Scales", "#FFFFFF", 0.35, 0.45, image=scales),
        "belly": mat("SM_Belly", "#E3C77A", 0.0, 0.6),
        "hair": mat("SM_Hair", "#16100C", 0.0, 0.7),
        "gold": mat("SM_Gold", "#E2B24A", 1.0, 0.25),
        "gem": mat("SM_Gem", "#2ED18A", 0.0, 0.1, glow=2.0),
        "eye": mat("SM_Eye", "#F2EEE4", 0.0, 0.15),
        "iris": mat("SM_Iris", "#C9B12A", 0.0, 0.2, glow=1.2),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    pelvis = rig.matrix_world @ rig.data.bones["pelvis"].head_local
    chest = rig.matrix_world @ rig.data.bones["spine_03"].tail_local
    assign_corset(body, mi["corset"], pelvis.z, chest.z - 0.03)
    pts = tail_path(Vector((0, pelvis.y, pelvis.z + 0.1)))   # kesik kalçayı örtecek kadar yukarıdan başlar
    tail = make_tail(body, rig, pts, mi["corset"], mi["belly"])
    add_tail_bones(rig, pts)
    parts = make_eyes(eyes, rig, mats_list, mi)
    hair, frame = make_hair(body, rig, mats_list, mi)
    crown = make_crown(rig, mats_list, mi, frame)
    for o in hair + crown:   # solidify'ı uygula (tek mesh'e katmadan önce)
        for m in list(o.modifiers):
            if m.type == "SOLIDIFY":
                bpy.context.view_layer.objects.active = o
                bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.select_all(action="DESELECT")
    for o in [tail] + parts + hair + crown:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = True
    made = build_actions(rig)
    height = max((rig.matrix_world @ v.co).z for v in body.data.vertices)
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False,
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False,
                             use_armature_deform_only=True, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             path_mode="RELATIVE")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("SAHMERAN_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


main()
