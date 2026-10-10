"""
HUMBABA — bölüm 3 canavarı (Hasankeyf). Kendi tasarımımız: Dicle kıyısı ormanlarının dev bekçisi.

Tepegöz hattını yeniden kullanır (build_tepegoz.py: gövde, oranlar, peştemal, 6 animasyon):
 1. MPFB iri erkek gövdesi + game_engine iskeleti; daha kambur, daha uzun kollu dev oranları.
 2. Yosunlu zeytin-bronz ten (MakeHuman CC0 dokusu yeniden tonlanır), iki parlayan kehribar göz,
    pas rengi aslan yelesi (yüzü çevreleyen sivri tutamlar), kıvrık fildişi boynuzlar, alt çeneden çıkan dişler.
 3. Koyu kabuk-deri etek (Tepegöz peştemalinin kesimi, düz renk).
Çıktı: Assets/Art/Boss/Humbaba.fbx (+ Humbaba_Skin.png), Blender/source/humbaba.blend
Çalıştır: blender -b -P Blender/scripts/build_humbaba.py
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_tepegoz as tg  # noqa: E402

ART = tg.ART
FBX = os.path.join(ART, "Humbaba.fbx")
SKIN_PNG = os.path.join(ART, "Humbaba_Skin.png")
BLEND = os.path.join(tg.ROOT, "Blender", "source", "humbaba.blend")

tg.PHENOTYPE = {"age": 0.7, "cupsize": 0.5, "firmness": 0.6, "gender": 1.0, "height": 1.0,
                "muscle": 0.9, "proportions": 0.9, "weight": 0.85,
                "race": {"african": 0.35, "asian": 0.15, "caucasian": 0.5}}
tg.PROPORTIONS = dict(tg.PROPORTIONS, **{
    "head": (1.05, 1.05), "neck_01": (0.75, 1.9), "spine_03": (1.05, 1.5), "spine_02": (1.0, 1.35),
    "upperarm_l": (1.35, 1.9), "upperarm_r": (1.35, 1.9), "lowerarm_l": (1.45, 2.0), "lowerarm_r": (1.45, 2.0),
    "hand_l": (1.9, 1.9), "hand_r": (1.9, 1.9),
})


def skin_texture():
    src = bpy.data.images.load(tg.SKIN_SRC)
    w, h = src.size
    px = np.empty(w * h * 4, dtype=np.float32)
    src.pixels.foreach_get(px)
    img = px.reshape(h, w, 4)
    rgb = img[:, :, :3]
    target = np.array(tg.srgb("#7C7046")[:3], dtype=np.float32)   # yosunlu zeytin-bronz
    mean = rgb.reshape(-1, 3)[rgb.reshape(-1, 3).sum(1) > 0.1].mean(0)
    rgb[:] = np.clip((rgb / np.maximum(mean, 1e-3)) ** 1.25 * target, 0, 1)
    rng = np.random.default_rng(3)   # yosun lekeleri
    small = rng.random((h // 64 + 2, w // 64 + 2))
    ys, xs = np.linspace(0, small.shape[0] - 1.001, h), np.linspace(0, small.shape[1] - 1.001, w)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    a, b = small[y0][:, x0], small[y0][:, x0 + 1]
    c, d = small[y0 + 1][:, x0], small[y0 + 1][:, x0 + 1]
    noise = (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy   # çift doğrusal: yumuşak lekeler
    moss = np.array(tg.srgb("#4F6A2E")[:3], dtype=np.float32)
    k = np.clip((noise - 0.6) * 1.6, 0, 0.4)[..., None]
    rgb[:] = rgb * (1 - k) + moss * k
    out = bpy.data.images.new("Humbaba_Skin", w, h)
    out.pixels.foreach_set(img.ravel())
    out.filepath_raw = SKIN_PNG
    out.file_format = "PNG"
    out.save()
    return out


def part(name, bm, mi, mats):
    for f in bm.faces:
        f.material_index = mi
    me = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        me.materials.append(m)
    return obj


def sphere(center, r, segs=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=segs // 2 + 2, radius=r)
    for v in bm.verts:
        v.co += center
    return bm


def cone_along(bm, base, direction, length, r0, segs=8, bend=None, steps=6):
    """base'den direction yönünde sivrilen (isteğe göre kıvrılan) boynuz/tutam."""
    d = direction.normalized()
    side = d.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize()
    up = side.cross(d).normalized()
    rings, p, dirv = [], base.copy(), d.copy()
    for i in range(steps + 1):
        t = i / steps
        r = r0 * (1 - t) + 0.002
        ring = [bm.verts.new(p + (side * math.cos(2 * math.pi * s / segs) + up * math.sin(2 * math.pi * s / segs)) * r) for s in range(segs)]
        rings.append(ring)
        if bend is not None:
            dirv = (dirv + bend * (1.0 / steps)).normalized()
        p = p + dirv * (length / steps)
    for a, b in zip(rings, rings[1:]):
        for s in range(segs):
            bm.faces.new((a[s], a[(s + 1) % segs], b[(s + 1) % segs], b[s]))
    tip = bm.verts.new(p)
    for s in range(segs):
        bm.faces.new((rings[-1][s], rings[-1][(s + 1) % segs], tip))


def head_parts(body, rig, eyes, mats, mi):
    head = tg.bone_head(rig, "head")
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    half = max(abs(p.x) for p in pts)
    front = min(p.y for p in pts)
    back = max(p.y for p in pts)
    cy = (front + back) * 0.5
    cz = (head.z + top) * 0.5
    parts = []
    # gözler: yuvaya oturan göz akı + parlayan kehribar iris
    for c in eyes:
        parts.append(part("Eye", sphere(c, 0.0145), mi["eye"], mats))
        parts.append(part("Iris", sphere(c + Vector((0, -0.011, 0)), 0.0068, 10), mi["iris"], mats))
    # yele: yüzün çevresinde ve ensede sivri tutamlar
    cap = sphere(Vector((0, cy + (back - cy) * 0.25, cz + 0.01)), half * 1.08, 18)
    kill = [v for v in cap.verts if v.co.y < cy - (back - cy) * 0.1 and v.co.z < top - 0.01]   # yüz ve alın açık
    bmesh.ops.delete(cap, geom=kill, context="VERTS")
    parts.append(part("ManeCap", cap, mi["mane"], mats))
    bm = bmesh.new()
    for i in range(46):
        a = -math.pi * 0.6 + (i % 23) * (math.pi * 1.2 / 22)   # yanlar + arka (yüz açık); a=0 ense
        ring = 0 if i < 23 else 1
        z = cz + (0.035 if ring == 0 else -0.045) + 0.03 * math.cos(a * 0.5)
        base = Vector((math.sin(a) * half * 0.92, cy + math.cos(a + math.pi) * -0.0 + (back - cy) * (0.3 + 0.6 * abs(math.cos(a * 0.5))), z))
        base.y = cy + math.cos(a) * (back - cy) * 0.85 + 0.01
        out = (base - Vector((0, cy, cz))).normalized() + Vector((0, 0.35, -0.25 if ring else 0.15))
        cone_along(bm, base, out, 0.11 + 0.04 * (i % 3), 0.024, segs=6, bend=Vector((0, 0.2, -0.6)), steps=3)
    parts.append(part("Mane", bm, mi["mane"], mats))
    # boynuzlar: şakaklardan yukarı-geriye kıvrılır
    bm = bmesh.new()
    for sx in (-1, 1):
        base = Vector((sx * half * 0.75, cy - 0.01, top - 0.035))
        cone_along(bm, base, Vector((sx * 0.6, 0.1, 1)), 0.2, 0.026, segs=10, bend=Vector((sx * 0.4, 0.9, -0.6)), steps=8)
    parts.append(part("Horns", bm, mi["horn"], mats))
    # dişler: alt çeneden yukarı
    bm = bmesh.new()
    mouth_z = eyes[0].z - 0.088
    for sx in (-1, 1):
        cone_along(bm, Vector((sx * 0.02, front + 0.02, mouth_z)), Vector((sx * 0.15, -0.3, 1)), 0.04, 0.007, segs=6, steps=3)
    parts.append(part("Tusks", bm, mi["horn"], mats))
    return parts


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = tg.create_body()
    mats = {
        "skin": tg.mat("HB_Skin", "#FFFFFF", 0.0, 0.7, image=skin_texture()),
        "eye": tg.mat("HB_Eye", "#E9E2C8", 0.0, 0.2),
        "iris": tg.mat("HB_Iris", "#FFB21E", 0.0, 0.2, glow=2.2),
        "mane": tg.mat("HB_Mane", "#7A3A16", 0.0, 0.85),
        "horn": tg.mat("HB_Horn", "#E6DABB", 0.0, 0.45),
        "bark": tg.mat("HB_Bark", "#3E2E1C", 0.0, 0.9),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    face = head_parts(body, rig, eyes, mats_list, mi)
    tg.stylize(body, rig, face)
    kilt = tg.make_pestemal(body, rig, mats_list, mi["bark"])
    kilt.modifiers.new("Armature", "ARMATURE").object = rig
    bpy.ops.object.select_all(action="DESELECT")
    for o in face + [kilt]:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = True
    body.name = body.data.name = "Humbaba"
    rig.name = rig.data.name = "HumbabaRig"
    tg.snap_to_ground(body, rig)
    made = tg.build_actions(rig)
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
    print("HUMBABA_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


main()
