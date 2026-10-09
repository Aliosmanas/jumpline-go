"""
TEPEGÖZ — bölüm 1 canavarı (docs/references/boss/tepegoz_ref.webp; kendi tasarımımız, birebir kopya değil).

Kurgu (hepsi ücretsiz/CC0: MPFB/MakeHuman gövdesi + cilt dokusu):
 1. MPFB atletik dev erkek gövdesi + game_engine iskeleti (koşucuyla aynı kemik adları → aynı poz sistemi).
 2. İki göz yuvası kapatılır, yüz dokusundaki göz/kaş boyası düz cilde boyanır; alnın ortasına TEK GÖZ
    (göz akı + kehribar-kırmızı iris + gözbebeği), etrafında göz kapağı halkası ve öfkeli kalın kaş çıkıntısı.
 3. Stilize dev oranları: poz ölçekleriyle (kemik boyu/kalınlığı) küçük kafa, kısa kalın boyun, geniş omuz ve göğüs,
    uzun kalın kollar, kocaman eller ve ayaklar, kalın kısa bacaklar → poz dinlenme pozu olarak uygulanır.
 4. Cilt dokusu: MakeHuman orta yaş erkek dokusu bronz-bej tona çekilir; göğse yeşil-mavi dövme (dikey çizgiler +
    oklar) piksel piksel işlenir.
 5. Giysi: çizgili yırtık mor peştemal (dokulu, testere dişli etek ucu), bel ipi, kollarda ip sargılar, kulak halkaları.
 6. Animasyonlar (30 fps): Run (ağır, geniş kol salınımlı koşu), Roar (kükreme), Throw (omzunun üstünden arkaya fırlatma),
    Hit (sırtından vurulup öne sendeleme), Defeat (yüzüstü devrilme), Idle.
Çıktı: Assets/Art/Boss/Tepegoz.fbx (+ Tepegoz_Skin.png, Pestemal.png), Blender/source/tepegoz.blend
Çalıştır: blender -b -P Blender/scripts/build_tepegoz.py
"""
import importlib
import math
import os
import random

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

ROOT = r"D:\Repolar\junpline"
ART = os.path.join(ROOT, "Assets", "Art", "Boss")
FBX = os.path.join(ART, "Tepegoz.fbx")
SKIN_PNG = os.path.join(ART, "Tepegoz_Skin.png")
CLOTH_PNG = os.path.join(ART, "Pestemal.png")
BLEND = os.path.join(ROOT, "Blender", "source", "tepegoz.blend")
MPFB_DATA = os.path.join(os.environ["APPDATA"], "Blender Foundation", "Blender", "5.2", "extensions", ".user", "user_default", "mpfb", "data")
SKIN_SRC = os.path.join(MPFB_DATA, "skins", "middleage_caucasian_male", "middleage_lightskinned_male_diffuse.png")
FPS = 30
random.seed(11)

PHENOTYPE = {"age": 0.6, "cupsize": 0.5, "firmness": 0.85, "gender": 1.0, "height": 1.0,
             "muscle": 1.0, "proportions": 1.0, "weight": 0.55,
             "race": {"african": 0.15, "asian": 0.0, "caucasian": 0.85}}

# kemik: (boy çarpanı, kalınlık çarpanı) — kemiğin kendi y ekseni boy
PROPORTIONS = {
    "head": (0.75, 0.75), "neck_01": (0.45, 2.3),
    "spine_03": (1.05, 1.62), "spine_02": (1.0, 1.38), "spine_01": (0.95, 1.16), "pelvis": (1.0, 1.12),
    "clavicle_l": (1.5, 2.1), "clavicle_r": (1.5, 2.1),
    "upperarm_l": (1.25, 1.78), "upperarm_r": (1.25, 1.78),
    "lowerarm_l": (1.35, 1.9), "lowerarm_r": (1.35, 1.9),
    "hand_l": (1.8, 1.8), "hand_r": (1.8, 1.8),
    "thigh_l": (0.8, 1.3), "thigh_r": (0.8, 1.3),
    "calf_l": (0.78, 1.38), "calf_r": (0.78, 1.38),
    "foot_l": (1.45, 1.45), "foot_r": (1.45, 1.45),
}
FINGER_SCALE = 1.0   # el ölçeği parmaklara da geçer (inherit)


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
        return sum(pts, Vector()) / len(pts)

    eyes = (centroid("helper-l-eye"), centroid("helper-r-eye"))
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
    body.name = body.data.name = "Tepegoz"
    rig.name = rig.data.name = "TepegozRig"
    return body, rig, eyes


def bone_head(rig, name):
    return rig.matrix_world @ rig.data.bones[name].head_local


def bone_tail(rig, name):
    return rig.matrix_world @ rig.data.bones[name].tail_local


# ---------------------------------------------------------------------------
# 2. Yüz: iki göz kapanır, alna tek göz
# ---------------------------------------------------------------------------

def close_eye_sockets(body, eyes):
    bm = bmesh.new()
    bm.from_mesh(body.data)
    boundary = [e for e in bm.edges if e.is_boundary and min((e.verts[0].co - c).length for c in eyes) < 0.05]
    if boundary:
        bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    region = [v for v in bm.verts if min((v.co - c).length for c in eyes) < 0.034]
    for _ in range(3):
        bmesh.ops.smooth_vert(bm, verts=region, factor=0.9, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    # kapanan yuvalar hafif öne (düz yanak/kaş kası); tek gözün kaşı bunların üstüne çöker
    for v in region:
        c = min(eyes, key=lambda e: (v.co - e).length)
        d = (v.co - c).length
        v.co.y -= 0.006 * max(0.0, 1 - d / 0.034)
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()


def add_cyclops_eye(body, rig, eyes, mats):
    """Alnın ortasına tek göz + göz kapağı halkası + öfkeli kaş. Hepsi head kemiğine bağlı."""
    mid = (eyes[0] + eyes[1]) * 0.5
    eye_z = mid.z + 0.052
    bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    hit, normal, _, _ = bvh.ray_cast(Vector((0, mid.y - 0.5, eye_z)), Vector((0, 1, 0)))
    if hit is None:
        hit, normal = Vector((0, mid.y - 0.03, eye_z)), Vector((0, -1, 0))
    r = 0.03
    center = hit + Vector((0, r * 0.35, 0))           # yarısı alna gömülü
    parts = []

    def sphere(name, c, size, mi, segs=24, rot=None):
        b = bmesh.new()
        bmesh.ops.create_uvsphere(b, u_segments=segs, v_segments=segs // 2, radius=1.0)
        for v in b.verts:
            p = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
            if rot:
                p = rot @ p
            v.co = p + c
        for f in b.faces:
            f.material_index = mi
            f.smooth = True
        return b

    # göz akı (biraz bombeli), iris, gözbebeği (hepsi -y'ye bakar)
    parts.append(("Eye", sphere("Eye", center, (r, r * 0.9, r), mats["eye"])))
    iris_c = center + Vector((0, -r * 0.82, 0))
    parts.append(("Iris", sphere("Iris", iris_c, (r * 0.52, r * 0.22, r * 0.52), mats["iris"])))
    parts.append(("Pupil", sphere("Pupil", iris_c + Vector((0, -r * 0.12, 0)), (r * 0.2, r * 0.12, r * 0.24), mats["pupil"])))
    # göz kapağı: gözün etrafında kalın cilt halkası (torus, alnın eğimine yaslı)
    lid = bmesh.new()
    bmesh.ops.create_circle(lid, cap_ends=False, segments=28, radius=1.0)
    ring = list(lid.verts)
    lid.free()
    lb = bmesh.new()
    seg, tube = 28, 10
    R, rr = r * 1.02, r * 0.32
    grid = []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        row = []
        for j in range(tube):
            t = 2 * math.pi * j / tube
            x = (R + rr * math.cos(t)) * math.cos(a)
            z = (R + rr * math.cos(t)) * math.sin(a) * 0.82
            y = rr * math.sin(t) - r * 0.45
            row.append(lb.verts.new(center + Vector((x, y, z))))
        grid.append(row)
    for i in range(seg):
        for j in range(tube):
            f = lb.faces.new((grid[i][j], grid[(i + 1) % seg][j], grid[(i + 1) % seg][(j + 1) % tube], grid[i][(j + 1) % tube]))
            f.material_index = mats["skin"]
            f.smooth = True
    parts.append(("Lid", lb))
    # öfkeli kaş: iki eğik kalın çıkıntı ortada V çizer
    for side in (-1, 1):
        rot = Matrix.Rotation(math.radians(-22 * side), 3, "Y")
        c = center + Vector((side * r * 0.95, -r * 0.55, r * 1.25))
        parts.append(("Brow", sphere("Brow", c, (r * 1.25, r * 0.55, r * 0.42), mats["skin"], 16, rot)))
    # burun kökü kası (iki kapalı yuvanın üstünü yumuşak örter)

    objs = []
    for name, b in parts:
        me = bpy.data.meshes.new(name)
        b.to_mesh(me)
        b.free()
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        for m in body.data.materials:
            o.data.materials.append(m)
        objs.append(o)
    return objs


# ---------------------------------------------------------------------------
# 3. Stilize oranlar (poz ölçeği → dinlenme pozu)
# ---------------------------------------------------------------------------

def stylize(body, rig, extras):
    for o in extras:
        o.parent = rig
        vg = o.vertex_groups.new(name="head")
        vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    for b in rig.data.bones:
        b.inherit_scale = "NONE"
    for name, (length, thick) in PROPORTIONS.items():
        if name in rig.pose.bones:
            rig.pose.bones[name].scale = (thick, length, thick)
    # el ölçeği parmaklara da: parmak kemikleri el kadar büyür
    for pb in rig.pose.bones:
        if pb.name.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb"):
            s = PROPORTIONS["hand_l"][0]
            pb.scale = (s, s, s)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    for o in [body] + list(extras):
        bpy.ops.object.select_all(action="DESELECT")
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        arm = next(m for m in o.modifiers if m.type == "ARMATURE")
        bpy.ops.object.modifier_apply(modifier=arm.name)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in rig.data.bones:
        b.inherit_scale = "FULL"
    for o in [body] + list(extras):
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        o.parent = rig


# ---------------------------------------------------------------------------
# 4. Cilt dokusu (ton + dövme + yüz temizliği)
# ---------------------------------------------------------------------------

def tattoo(x, z, chest):
    """Göğüs dövmesi: her göğüste dışta iki dikey çizgi, içe doğru ok (<  >). x, z dünya; chest = (z0, z1, half)."""
    z0, z1, half = chest
    if z < z0 or z > z1:
        return 0.0
    u = abs(x) / half
    v = (z - z0) / (z1 - z0)
    if u > 1.05:
        return 0.0
    k = 0.0
    for lx, top in ((0.86, 0.95), (0.72, 0.85)):
        d = abs(u - lx - 0.05 * (v - 0.5))
        if v > 0.12 and v < top:
            k = max(k, 1.0 - smooth(d, 0.022, 0.04))
    chev = abs((u - 0.38) - 0.32 * abs(v - 0.52))
    if 0.22 < v < 0.82:
        k = max(k, 1.0 - smooth(chev, 0.026, 0.045))
    return k


def smooth(d, a, b):
    if d <= a:
        return 0.0
    if d >= b:
        return 1.0
    t = (d - a) / (b - a)
    return t * t * (3 - 2 * t)


def bake_skin(body, rig, eyes):
    src = bpy.data.images.load(SKIN_SRC)
    w, h = src.size
    px = np.empty(w * h * 4, dtype=np.float32)
    src.pixels.foreach_get(px)
    img = px.reshape(h, w, 4)
    rgb = img[:, :, :3]
    # bronz-bej tona çek (referans: sıcak, hafif soluk ten), kontrastı biraz artır
    target = np.array(srgb("#D2A783")[:3], dtype=np.float32)
    mean = rgb.reshape(-1, 3)[rgb.reshape(-1, 3).sum(1) > 0.1].mean(0)
    rgb[:] = np.clip((rgb / np.maximum(mean, 1e-3)) ** 1.15 * target, 0, 1)
    skin_tone = target.copy()
    ink = np.array(srgb("#1F6B66")[:3], dtype=np.float32)

    me = body.data
    uv = me.uv_layers.active.data
    head_y = bone_head(rig, "head").y
    sp = bone_head(rig, "spine_03")
    neck = bone_head(rig, "neck_01")
    chest = (sp.z - 0.02, neck.z - 0.07, 0.2)
    mid_eye = (eyes[0] + eyes[1]) * 0.5

    def paint_tri(p3, puv, fn):
        xs = [int(u * (w - 1)) for u, _ in puv]
        ys = [int(v * (h - 1)) for _, v in puv]
        x0, x1, y0, y1 = max(min(xs), 0), min(max(xs), w - 1), max(min(ys), 0), min(max(ys), h - 1)
        if x1 < x0 or y1 < y0 or (x1 - x0) * (y1 - y0) > 40000:
            return
        (ax, ay), (bx, by), (cx, cy) = [(u * (w - 1), v * (h - 1)) for u, v in puv]
        den = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy)
        if abs(den) < 1e-9:
            return
        gx, gy = np.meshgrid(np.arange(x0, x1 + 1), np.arange(y0, y1 + 1))
        l1 = ((by - cy) * (gx - cx) + (cx - bx) * (gy - cy)) / den
        l2 = ((cy - ay) * (gx - cx) + (ax - cx) * (gy - cy)) / den
        l3 = 1 - l1 - l2
        inside = (l1 >= -0.02) & (l2 >= -0.02) & (l3 >= -0.02)
        if not inside.any():
            return
        pos = [l1[..., None] * np.array(p3[0]) + l2[..., None] * np.array(p3[1]) + l3[..., None] * np.array(p3[2])][0]
        fn(gy[inside], gx[inside], pos[inside])

    def chest_fn(yy, xx, pos):
        k = np.array([tattoo(p[0], p[2], chest) for p in pos], dtype=np.float32)
        a = (k * 0.78)[:, None]
        rgb[yy, xx] = rgb[yy, xx] * (1 - a) + ink * a

    def face_fn(yy, xx, pos):
        d = np.minimum(np.linalg.norm(pos - np.array(eyes[0]), axis=1), np.linalg.norm(pos - np.array(eyes[1]), axis=1))
        brow = np.abs(pos[:, 2] - (mid_eye.z + 0.028)) < 0.02
        k = np.clip((0.045 - d) / 0.02, 0, 1)
        k = np.maximum(k, brow * np.clip((0.07 - np.abs(pos[:, 0])) / 0.03, 0, 1) * 0.9)
        a = k[:, None]
        rgb[yy, xx] = rgb[yy, xx] * (1 - a) + (rgb[yy, xx] * 0.25 + skin_tone * 0.75) * a

    for poly in me.polygons:
        c = poly.center
        front = poly.normal.y < -0.15
        is_chest = front and chest[0] - 0.02 < c.z < chest[1] + 0.02 and abs(c.x) < 0.24
        is_face = front and c.y < head_y and min((c - e).length for e in eyes) < 0.07
        if not (is_chest or is_face):
            continue
        idx = list(poly.loop_indices)
        for t in range(1, len(idx) - 1):
            tri = (idx[0], idx[t], idx[t + 1])
            p3 = [tuple(me.vertices[me.loops[i].vertex_index].co) for i in tri]
            puv = [tuple(uv[i].uv) for i in tri]
            if is_chest:
                paint_tri(p3, puv, chest_fn)
            if is_face:
                paint_tri(p3, puv, face_fn)
    out_w = min(w, 2048)
    res = bpy.data.images.new("Tepegoz_Skin", w, h, alpha=False)
    res.pixels.foreach_set(img.ravel())
    if out_w < w:
        res.scale(out_w, int(h * out_w / w))
    res.filepath_raw = SKIN_PNG
    res.file_format = "PNG"
    res.save()
    return res


def cloth_texture():
    """Peştemal: mor zemin, koyu ve açık enine çizgiler, hafif dokuma (u: çevre, v: boy)."""
    w, h = 512, 256
    base = np.array(srgb("#8A45A6")[:3], dtype=np.float32)
    dark = np.array(srgb("#4E2060")[:3], dtype=np.float32)
    light = np.array(srgb("#C89BD8")[:3], dtype=np.float32)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    v = yy / h
    img = np.tile(base, (h, w, 1))
    bands = [(0.14, 0.035, dark), (0.2, 0.012, light), (0.82, 0.05, dark), (0.9, 0.014, light), (0.95, 0.01, light)]
    for c, half, col in bands:
        k = np.clip(1 - np.abs(v - c) / half, 0, 1)[..., None] ** 0.5
        img = img * (1 - k) + col * k
    weave = (np.sin(xx * 1.9) * np.sin(yy * 2.3) * 0.035 + np.sin(xx * 0.21 + yy * 0.05) * 0.02)[..., None]
    img = np.clip(img * (1 + weave), 0, 1)
    out = np.concatenate([img, np.ones((h, w, 1), np.float32)], axis=2)
    im = bpy.data.images.new("Pestemal", w, h, alpha=False)
    im.pixels.foreach_set(out.ravel())
    im.filepath_raw = CLOTH_PNG
    im.file_format = "PNG"
    im.save()
    return im


# ---------------------------------------------------------------------------
# 5. Giysi ve aksesuar
# ---------------------------------------------------------------------------

def copy_weights(obj, body, blend_bone=None, blend_of=None):
    """Her köşeye en yakın gövde köşesinin ağırlıkları (blend_of(v) oranında blend_bone'a çekilir)."""
    kd = KDTree(len(body.data.vertices))
    for v in body.data.vertices:
        kd.insert(v.co, v.index)
    kd.balance()
    names = {g.index: g.name for g in body.vertex_groups}
    groups = {}
    for v in obj.data.vertices:
        _, idx, _ = kd.find(v.co)
        ws = {names[g.group]: g.weight for g in body.data.vertices[idx].groups if g.group in names}
        tot = sum(ws.values()) or 1.0
        ws = {k: w / tot for k, w in ws.items()}
        if blend_bone and blend_of:
            k = blend_of(v.co)
            ws = {n: w * (1 - k) for n, w in ws.items()}
            ws[blend_bone] = ws.get(blend_bone, 0.0) + k
        for n, w in ws.items():
            if w < 0.01:
                continue
            if n not in groups:
                groups[n] = obj.vertex_groups.new(name=n)
            groups[n].add([v.index], w, "REPLACE")


def surface_radius(bvh, center, direction, far=0.45):
    """İçten dışa: merkezden yöne ilk yüzey (kol/bacak gibi tek uzvun kesiti)."""
    hit, _, _, _ = bvh.ray_cast(center, direction, far)
    return (hit - center).length if hit is not None else None


def dominant_bones(body):
    names = {g.index: g.name for g in body.vertex_groups}
    out = []
    for v in body.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        out.append(names.get(best.group) if best else None)
    return out


def hull_radius(body, dom, bones, center, z, d, band=0.035):
    """Gövdenin (yalnız verilen kemiklere ait köşeler) z kesitinin d yönündeki dış sınırı (destek fonksiyonu)."""
    best = None
    for v, b in zip(body.data.vertices, dom):
        if b not in bones or abs(v.co.z - z) > band:
            continue
        s = (v.co.x - center.x) * d.x + (v.co.y - center.y) * d.y
        if best is None or s > best:
            best = s
    return best


def finish_part(name, bm, body, rig, mats_list):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    for m in mats_list:
        o.data.materials.append(m)
    o.parent = rig
    return o


def make_pestemal(body, rig, mats_list, cloth_mi):
    dom = dominant_bones(body)
    hips = {"pelvis", "spine_01", "thigh_l", "thigh_r"}
    pelvis = bone_head(rig, "pelvis")
    thigh = bone_head(rig, "thigh_l")
    knee = bone_tail(rig, "thigh_l")
    waist_z = pelvis.z + 0.16
    hem_z = thigh.z + (knee.z - thigh.z) * 0.55
    cols, rows = 72, 9
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new("UVMap")
    grid = []
    hem_jag = [random.uniform(-0.09, 0.05) + (0.07 if (i % 9) in (0, 1) else 0.0) for i in range(cols)]
    for r_ in range(rows):
        f = r_ / (rows - 1)
        row = []
        for i in range(cols):
            a = 2 * math.pi * i / cols
            d = Vector((math.sin(a), -math.cos(a), 0))
            front = max(0.0, -d.y)
            z = waist_z + (hem_z - waist_z - 0.05 * front) * f
            if r_ == rows - 1:
                z += hem_jag[i] * (0.6 + 0.4 * front)
            c = Vector((0, pelvis.y, z))
            rad = hull_radius(body, dom, hips, c, z, d) or 0.25
            rad += 0.03 + 0.07 * f * f + 0.02 * front * f      # etek açılır, önü bol
            row.append(bm.verts.new(c + d * rad))
        grid.append(row)
    for r_ in range(rows - 1):
        for i in range(cols):
            j = (i + 1) % cols
            fc = bm.faces.new((grid[r_][i], grid[r_][j], grid[r_ + 1][j], grid[r_ + 1][i]))
            fc.material_index = cloth_mi
            fc.smooth = True
            for loop, (uu, vv) in zip(fc.loops, ((i / cols, 1 - r_ / (rows - 1)), ((i + 1) / cols, 1 - r_ / (rows - 1)),
                                                 ((i + 1) / cols, 1 - (r_ + 1) / (rows - 1)), (i / cols, 1 - (r_ + 1) / (rows - 1)))):
                loop[uvl].uv = (uu * 3.0, vv)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    o = finish_part("Pestemal", bm, body, rig, mats_list)
    sol = o.modifiers.new("Solid", "SOLIDIFY")
    sol.thickness = 0.014
    sol.offset = 1.0
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.modifier_apply(modifier=sol.name)
    span = max(1e-3, waist_z - hem_z)
    copy_weights(o, body, "pelvis", lambda co: 0.55 * max(0.0, min(1.0, 1 - (waist_z - co.z) / span)) + 0.25)
    return o


def rope_ring(bm, center, axis, radius, thick, mi, tilt=0.0, segs=20, tube=6):
    axis = axis.normalized()
    side = axis.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = axis.cross(Vector((1, 0, 0)))
    side.normalize()
    up = axis.cross(side).normalized()
    grid = []
    for i in range(segs):
        a = 2 * math.pi * i / segs
        ring_dir = side * math.cos(a) + up * math.sin(a)
        c = center + ring_dir * radius + axis * (math.sin(a) * tilt)
        row = []
        for j in range(tube):
            t = 2 * math.pi * j / tube
            row.append(bm.verts.new(c + (ring_dir * math.cos(t) + axis * math.sin(t)) * thick))
        grid.append(row)
    for i in range(segs):
        for j in range(tube):
            f = bm.faces.new((grid[i][j], grid[(i + 1) % segs][j], grid[(i + 1) % segs][(j + 1) % tube], grid[i][(j + 1) % tube]))
            f.material_index = mi
            f.smooth = True


def make_ropes(body, rig, mats_list, rope_mi, metal_mi):
    bvh = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get())
    out = []
    for side in ("l", "r"):
        for bone, ts in (("upperarm_" + side, (0.62, 0.7, 0.78)), ("lowerarm_" + side, (0.2, 0.3, 0.72, 0.8, 0.87))):
            a, b = bone_head(rig, bone), bone_tail(rig, bone)
            axis = (b - a).normalized()
            bm = bmesh.new()
            for k, t in enumerate(ts):
                c = a + (b - a) * t
                perp = axis.cross(Vector((0, 1, 0))).normalized()
                rs = sorted(r for r in (surface_radius(bvh, c, (perp * math.cos(q) + axis.cross(perp) * math.sin(q)).normalized())
                                        for q in np.linspace(0, 6.0, 8)) if r)
                rad = (rs[len(rs) // 2] if rs else 0.08) + 0.008
                rope_ring(bm, c, axis, rad, 0.011, rope_mi, tilt=0.025 * (1 if k % 2 else -1))
            o = finish_part("Rope_" + bone, bm, body, rig, mats_list)
            vg = o.vertex_groups.new(name=bone)
            vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
            out.append(o)
    # bel ipi
    pelvis = bone_head(rig, "pelvis")
    c = Vector((0, pelvis.y, pelvis.z + 0.17))
    dom = dominant_bones(body)
    rs = [hull_radius(body, dom, {"pelvis", "spine_01", "spine_02"}, c, c.z, Vector((math.sin(q), -math.cos(q), 0))) for q in np.linspace(0, 6.28, 12)]
    rs = [r for r in rs if r]
    bm = bmesh.new()
    rope_ring(bm, c + Vector((0, 0.01, 0)), Vector((0, 0, 1)), (sum(rs) / len(rs)) + 0.035, 0.016, rope_mi, tilt=0.02, segs=36)
    rope_ring(bm, c + Vector((0, 0.01, -0.03)), Vector((0, 0, 1)), (sum(rs) / len(rs)) + 0.04, 0.014, rope_mi, tilt=-0.015, segs=36)
    o = finish_part("Rope_belt", bm, body, rig, mats_list)
    vg = o.vertex_groups.new(name="pelvis")
    vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    out.append(o)
    for o in out:
        mod = o.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
    return out


def snap_to_ground(body, rig):
    """Kısalan bacaklar ayağı yerden kaldırır: tüm kemikler (Root hariç) ve mesh aşağı, ayak tabanı z=0."""
    minz = min(v.co.z for v in body.data.vertices)
    if abs(minz) < 1e-4:
        return
    for v in body.data.vertices:
        v.co.z -= minz
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in rig.data.edit_bones:
        if eb.name == "Root":
            continue
        eb.head.z -= minz
        eb.tail.z -= minz
    bpy.ops.object.mode_set(mode="OBJECT")


# ---------------------------------------------------------------------------
# 6. Animasyonlar (bkz. build_anims.py — aynı poz sistemi)
# ---------------------------------------------------------------------------

ORDER = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "head",
         "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l", "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
         "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r"]
FINGERS = [f"{f}_0{i}_{s}" for f in ("index", "middle", "ring", "pinky") for i in (1, 2, 3) for s in ("l", "r")]


def V(x, y, z):
    return Vector((x, y, z)).normalized()


def mirror(pose):
    out = {}
    for k, v in pose.items():
        if k in ("pelvis_z", "root", "fist"):
            out[k] = v
            continue
        nk = k[:-2] + ("_r" if k.endswith("_l") else "_l") if k[-2:] in ("_l", "_r") else k
        out[nk] = Vector((-v.x, v.y, v.z))
    return out


STAND = {
    "spine_01": V(0, -0.05, 1), "spine_02": V(0, -0.06, 1), "spine_03": V(0, -0.02, 1), "neck_01": V(0, -0.35, 1), "head": V(0, -0.1, 1),
    "upperarm_l": V(0.42, 0.0, -1), "lowerarm_l": V(0.2, -0.22, -1), "upperarm_r": V(-0.42, 0.0, -1), "lowerarm_r": V(-0.2, -0.22, -1),
    "thigh_l": V(0.16, 0, -1), "calf_l": V(0.08, 0.04, -1), "foot_l": V(0.1, -1, -0.45),
    "thigh_r": V(-0.16, 0, -1), "calf_r": V(-0.08, 0.04, -1), "foot_r": V(-0.1, -1, -0.45),
}


def P(base=STAND, **kw):
    d = dict(base)
    d.update(kw)
    return d


# ağır koşu: gövde öne eğik, omuzlar sallanır, kollar geniş ve iri salınır, adımlar güçlü
RUN_A = P(spine_01=V(0.04, -0.3, 1), spine_02=V(0.06, -0.35, 1), spine_03=V(0.08, -0.3, 1), neck_01=V(0, -0.55, 1), head=V(0, -0.15, 1),
          thigh_r=V(-0.12, -0.75, -0.66), calf_r=V(-0.06, -0.1, -1), foot_r=V(-0.1, -1, -0.2),
          thigh_l=V(0.14, 0.45, -0.88), calf_l=V(0.06, 0.9, -0.42), foot_l=V(0.05, 0.4, -1),
          upperarm_l=V(0.45, -0.65, -0.6), lowerarm_l=V(0.2, -1, 0.15),
          upperarm_r=V(-0.45, 0.6, -0.65), lowerarm_r=V(-0.3, -0.1, -1), pelvis_z=-0.06)
RUN_B = P(spine_01=V(0, -0.3, 1), spine_02=V(0, -0.36, 1), spine_03=V(0, -0.3, 1), neck_01=V(0, -0.55, 1), head=V(0, -0.15, 1),
          thigh_r=V(-0.1, 0.05, -1), calf_r=V(-0.05, 0.1, -1), foot_r=V(-0.1, -1, -0.5),
          thigh_l=V(0.14, -0.35, -0.94), calf_l=V(0.06, 0.85, -0.5), foot_l=V(0.05, 0.2, -1),
          upperarm_l=V(0.5, -0.1, -0.9), lowerarm_l=V(0.3, -0.7, -0.6),
          upperarm_r=V(-0.5, 0.1, -0.9), lowerarm_r=V(-0.3, -0.7, -0.6), pelvis_z=0.06)
ROAR_LOAD = P(spine_01=V(0, -0.35, 1), spine_02=V(0, -0.45, 1), spine_03=V(0, -0.45, 0.9), neck_01=V(0, -0.6, 0.8), head=V(0, -0.4, 0.9),
              upperarm_l=V(0.6, -0.3, -0.75), lowerarm_l=V(0.2, -0.6, -0.8), upperarm_r=V(-0.6, -0.3, -0.75), lowerarm_r=V(-0.2, -0.6, -0.8),
              thigh_l=V(0.3, -0.3, -0.9), calf_l=V(0.15, 0.3, -0.95), thigh_r=V(-0.3, -0.3, -0.9), calf_r=V(-0.15, 0.3, -0.95), pelvis_z=-0.18)
ROAR = P(spine_01=V(0, 0.12, 1), spine_02=V(0, 0.22, 1), spine_03=V(0, 0.3, 1), neck_01=V(0, 0.1, 1), head=V(0, 0.45, 0.9),
         upperarm_l=V(0.95, 0.15, 0.3), lowerarm_l=V(0.35, 0.1, 1), upperarm_r=V(-0.95, 0.15, 0.3), lowerarm_r=V(-0.35, 0.1, 1),
         thigh_l=V(0.32, 0, -0.95), calf_l=V(0.16, 0.1, -1), thigh_r=V(-0.32, 0, -0.95), calf_r=V(-0.16, 0.1, -1), pelvis_z=-0.08)
ROAR_SHAKE = P(ROAR, spine_03=V(0.08, 0.32, 1), head=V(0.15, 0.45, 0.9), upperarm_l=V(0.95, 0.25, 0.4), upperarm_r=V(-0.95, 0.05, 0.25))
# arkaya fırlatma: sağ kol önden aşağıdan alır, omzun üstünden arkaya (koşucuya) savurur
THROW_WIND = P(RUN_B, spine_02=V(-0.1, -0.5, 0.9), spine_03=V(-0.15, -0.5, 0.85),
               upperarm_r=V(-0.35, -0.75, -0.55), lowerarm_r=V(-0.1, -0.9, -0.4), upperarm_l=V(0.6, 0.1, -0.8))
THROW_UP = P(RUN_A, spine_02=V(-0.05, -0.1, 1), spine_03=V(-0.1, 0.05, 1),
             upperarm_r=V(-0.25, -0.2, 1), lowerarm_r=V(-0.1, 0.25, 1))
THROW_REL = P(RUN_B, spine_02=V(0.05, 0.05, 1), spine_03=V(0.12, 0.2, 1), head=V(0.2, 0.1, 1),
              upperarm_r=V(-0.3, 0.8, 0.5), lowerarm_r=V(-0.2, 0.95, -0.1))
# sırtından vuruldu: öne kamburlaşıp kolları savrulur
HIT = P(RUN_B, spine_01=V(0, -0.55, 0.85), spine_02=V(0, -0.65, 0.75), spine_03=V(0, -0.55, 0.8), neck_01=V(0, -0.2, 1), head=V(0, 0.35, 0.9),
        upperarm_l=V(0.85, 0.35, 0.35), lowerarm_l=V(0.7, 0.2, 0.7), upperarm_r=V(-0.85, 0.35, 0.35), lowerarm_r=V(-0.7, 0.2, 0.7), pelvis_z=-0.1)
KNEEL_F = P(spine_01=V(0, -0.5, 0.85), spine_02=V(0, -0.6, 0.8), spine_03=V(0, -0.6, 0.8), head=V(0, -0.5, 0.85),
            thigh_l=V(0.2, -0.98, -0.15), calf_l=V(0.08, 0.98, -0.1), thigh_r=V(-0.2, -0.98, -0.15), calf_r=V(-0.08, 0.98, -0.1),
            upperarm_l=V(0.4, -0.5, -0.75), upperarm_r=V(-0.4, -0.5, -0.75), lowerarm_l=V(0.2, -0.6, -0.8), lowerarm_r=V(-0.2, -0.6, -0.8),
            pelvis_z=-0.75)
DOWN_F = P(STAND, root=(1.5, -0.9, 0.18),   # yüzüstü: baş -y'ye (koşu yönüne) devrilir
           thigh_l=V(0.2, 0.0, -1), calf_l=V(0.1, 0.25, -1), thigh_r=V(-0.25, 0.1, -1), calf_r=V(-0.1, 0.3, -1),
           upperarm_l=V(0.95, 0.0, 0.3), lowerarm_l=V(0.8, -0.4, 0.4), upperarm_r=V(-0.9, 0.2, 0.4), lowerarm_r=V(-0.9, -0.2, 0.3),
           head=V(0.5, -0.1, 1), pelvis_z=0.0)
DOWN_BOUNCE = P(DOWN_F, root=(1.38, -0.92, 0.3))

ACTIONS = {
    "Idle": ([(0, STAND), (40, P(STAND, spine_03=V(0, 0.03, 1), pelvis_z=0.01)), (80, STAND)], True),
    "Run": ([(0, RUN_A), (6, RUN_B), (12, mirror(RUN_A)), (18, mirror(RUN_B)), (24, RUN_A)], True),
    "Roar": ([(0, STAND), (8, ROAR_LOAD), (16, ROAR), (22, ROAR_SHAKE), (28, ROAR), (34, ROAR_SHAKE), (40, ROAR), (52, STAND)], False),
    "Throw": ([(0, RUN_A), (6, THROW_WIND), (13, THROW_UP), (18, THROW_REL), (26, RUN_B)], False),
    "Hit": ([(0, RUN_B), (3, HIT), (12, P(HIT, spine_02=V(0, -0.45, 0.9))), (20, RUN_A)], False),
    "Defeat": ([(0, HIT), (8, KNEEL_F), (18, DOWN_F), (23, DOWN_BOUNCE), (30, DOWN_F)], False),
}


def apply_pose(rig, pose):
    pb = rig.pose.bones
    for b in pb:
        b.rotation_mode = "QUATERNION"
        b.rotation_quaternion = (1, 0, 0, 0)
        b.location = (0, 0, 0)
    bpy.context.view_layer.update()
    if "root" in pose:
        ax, dy, dz = pose["root"]
        r = pb["Root"]
        r.matrix = Matrix.Translation((0, dy, dz)) @ Matrix.Rotation(ax, 4, "X") @ r.matrix
        bpy.context.view_layer.update()
    if pose.get("pelvis_z"):
        p = pb["pelvis"]
        p.matrix = Matrix.Translation((0, 0, pose["pelvis_z"])) @ p.matrix
        bpy.context.view_layer.update()
    for name in ORDER:
        if name not in pose or name not in pb:
            continue
        b = pb[name]
        cur = (b.tail - b.head).normalized()
        target = (rig.matrix_world.to_3x3().inverted() @ pose[name]).normalized()
        if "root" in pose:
            target = (Matrix.Rotation(pose["root"][0], 3, "X") @ target).normalized()
        q = cur.rotation_difference(target)
        m = b.matrix.copy()
        loc = m.to_translation()
        b.matrix = Matrix.Translation(loc) @ q.to_matrix().to_4x4() @ Matrix.Translation(-loc) @ m
        bpy.context.view_layer.update()
    for f in FINGERS:   # yarı yumruk (pençe)
        if f in pb:
            pb[f].rotation_quaternion = Matrix.Rotation(math.radians(55), 3, "X").to_quaternion()


def build_actions(rig):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    rig.animation_data_create()
    made = []
    for name, (keys, loop) in ACTIONS.items():
        act = bpy.data.actions.new(name)
        act.use_fake_user = True
        rig.animation_data.action = act
        for frame, pose in keys:
            apply_pose(rig, pose)
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
    skin_img = None
    cloth_img = cloth_texture()
    mats = {
        "skin": mat("TG_Skin", "#FFFFFF", 0.0, 0.62),
        "eye": mat("TG_Eye", "#F2EEE4", 0.0, 0.15),
        "iris": mat("TG_Iris", "#D9541E", 0.0, 0.2, glow=1.6),
        "pupil": mat("TG_Pupil", "#120808", 0.0, 0.1),
        "cloth": mat("TG_Cloth", "#FFFFFF", 0.0, 0.85, image=cloth_img),
        "rope": mat("TG_Rope", "#6E5236", 0.0, 0.9),
        "metal": mat("TG_Brass", "#C9A04A", 0.9, 0.35),
    }
    order = ["skin", "eye", "iris", "pupil", "cloth", "rope", "metal"]
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    close_eye_sockets(body, eyes)
    skin_img = bake_skin(body, rig, eyes)   # yüz/göğüs koordinatları stilizasyondan önce (insan ölçeği)
    tn = mats["skin"].node_tree.nodes.new("ShaderNodeTexImage")
    tn.image = skin_img
    b = next(n for n in mats["skin"].node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    mats["skin"].node_tree.links.new(tn.outputs["Color"], b.inputs["Base Color"])
    face_parts = add_cyclops_eye(body, rig, eyes, mi)
    stylize(body, rig, face_parts)
    cloth = make_pestemal(body, rig, mats_list, mi["cloth"])
    mod = cloth.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    ropes = make_ropes(body, rig, mats_list, mi["rope"], mi["metal"])
    # yüz parçalarını gövdeye kat (tek mesh: mobilde az çizim çağrısı)
    bpy.ops.object.select_all(action="DESELECT")
    for o in face_parts + [cloth] + ropes:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = True
    snap_to_ground(body, rig)
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
    print("TEPEGOZ_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


main()
