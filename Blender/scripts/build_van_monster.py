"""
VAN GÖLÜ CANAVARI — bölüm 5 canavarı (Van). Kendi tasarımımız: gölden çıkan kambur, pullu sürüngen dev.

Şahmeran hattını yeniden kullanır (build_sahmeran.py: bacaksız gövde + 12 kemikli kuyruk + süzülerek kaçış animasyonları):
 1. MPFB iri erkek gövdesi, bacaklar yerine kalın kuyruk; tüm ten koyu deniz yeşili pullu (gölün sodalı suyu).
 2. Baş: uzun sürüngen burnu + alt çene, sarı-yeşil parlayan gözler, dişler.
 3. Sırtta ensen kuyruk ucuna kadar sivrilen dikenler, ellerde pençeler.
Çıktı: Assets/Art/Boss/VanMonster.fbx (+ VanMonster_Scales.png), Blender/source/van_monster.blend
Çalıştır: blender -b -P Blender/scripts/build_van_monster.py
"""
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_sahmeran as sm  # noqa: E402
import build_humbaba as hb  # noqa: E402

ART = sm.ART
FBX = os.path.join(ART, "VanMonster.fbx")
SCALES_PNG = os.path.join(ART, "VanMonster_Scales.png")
BLEND = os.path.join(sm.ROOT, "Blender", "source", "van_monster.blend")

sm.PHENOTYPE = {"age": 0.5, "cupsize": 0.5, "firmness": 0.8, "gender": 1.0, "height": 1.0,
                "muscle": 0.95, "proportions": 0.9, "weight": 0.75,
                "race": {"african": 0.3, "asian": 0.2, "caucasian": 0.5}}
sm.TAIL_LEN = 3.0


def scale_texture(size=512):
    y, x = np.mgrid[0:size, 0:size] / size
    cells = 10
    cy = y * cells * 1.4
    row = np.floor(cy)
    cx = x * cells + (row % 2) * 0.5
    fx = cx - np.floor(cx) - 0.5
    fy = cy - row
    d = np.sqrt(fx ** 2 + (fy * 0.9) ** 2)
    edge = np.clip((d - 0.36) / 0.16, 0, 1)
    shade = np.clip(1.0 - fy * 0.6, 0, 1)
    teal = np.array([0.08, 0.32, 0.3])
    deep = np.array([0.03, 0.12, 0.14])
    base = teal * shade[..., None] + deep * (1 - shade[..., None])
    img = base * (1 - edge[..., None] * 0.7)
    rgba = np.concatenate([img, np.ones((size, size, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new("VanMonster_Scales", size, size)
    im.pixels.foreach_set(rgba[::-1].ravel())
    im.filepath_raw = SCALES_PNG
    im.file_format = "PNG"
    im.save()
    return im


def cylinder_uv(body):
    me = body.data
    uv = me.uv_layers.active
    if uv is None:
        return
    for p in me.polygons:
        for li in p.loop_indices:
            co = me.vertices[me.loops[li].vertex_index].co
            uv.data[li].uv = (math.atan2(co.x, co.y) / math.pi + 1.0, co.z * 5.0)


def head_parts(body, rig, eyes, mats, mi):
    head = rig.matrix_world @ rig.data.bones["head"].head_local
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    half = max(abs(p.x) for p in pts)
    front = min(p.y for p in pts)
    eye_z = (eyes[0][0].z + eyes[1][0].z) * 0.5
    parts = []
    # burun (üst çene) ve alt çene: öne uzanan yuvarlak hatlı elipsoitler
    def ellipsoid(name, center, rx, ry, rz):
        bm = hb.sphere(Vector((0, 0, 0)), 1.0, 20)
        for v in bm.verts:
            v.co = Vector((v.co.x * rx, v.co.y * ry, v.co.z * rz)) + center
        return hb.part(name, bm, mi["scales"], mats)
    parts.append(ellipsoid("Snout", Vector((0, front - 0.09, eye_z - 0.035)), half * 0.78, 0.15, 0.05))
    parts.append(ellipsoid("Jaw", Vector((0, front - 0.07, eye_z - 0.092)), half * 0.66, 0.13, 0.032))
    # dişler
    bm = bmesh.new()
    for k in range(6):
        for sx in (-1, 1):
            base = Vector((sx * half * 0.55 * (1 - k * 0.13), front - 0.02 - k * 0.035, eye_z - 0.07))
            hb.cone_along(bm, base, Vector((0, 0, -1)), 0.022, 0.006, segs=5, steps=2)
    parts.append(hb.part("Teeth", bm, mi["bone"], mats))
    # parlayan gözler (yuvanın üstünde, yanlara bakan)
    for c, _ in eyes:
        parts.append(sm.add_sphere("Eye", Vector((half * 0.55 if c.x > 0 else -half * 0.55, front - 0.015, eye_z + 0.012)), 0.02, mi["glow"], "head", rig, mats))
    for o in parts:
        if not o.vertex_groups:
            o.vertex_groups.new(name="head").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
            o.parent = rig
            o.modifiers.new("Armature", "ARMATURE").object = rig
    return parts


def spines(body, rig, pts, mats, mi):
    """Enseden kuyruk ucuna sırt dikenleri: kemik zincirine bağlı (gövde: spine_*, kuyruk: tail_*)."""
    bm = bmesh.new()
    chain = [(rig.matrix_world @ rig.data.bones[n].head_local, n) for n in ("neck_01", "spine_03", "spine_02", "spine_01")]
    groups = []
    for p, n in chain:
        back = max(v.co.y for v in body.data.vertices if abs(v.co.z - p.z) < 0.03 and abs(v.co.x) < 0.1)
        s0 = len(bm.verts)
        hb.cone_along(bm, Vector((0, back - 0.01, p.z)), Vector((0, 0.6, 0.8)), 0.13, 0.03, segs=6, steps=2)
        groups.append((n, range(s0, len(bm.verts))))
    for i in range(1, len(pts) - 2):
        t = i / (len(pts) - 1)
        p = pts[i]
        s0 = len(bm.verts)
        hb.cone_along(bm, p + Vector((0, 0, 0.11 * (1 - t) + 0.03)), Vector((0, 0.35, 1)), 0.1 * (1 - t) + 0.04, 0.025 * (1 - t) + 0.008, segs=6, steps=2)
        groups.append((f"tail_{i:02d}", range(s0, len(bm.verts))))
    o = hb.part("Spines", bm, mi["glow_dark"], mats)
    for n, idx in groups:
        g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n)
        g.add(list(idx), 1.0, "REPLACE")
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


def claws(body, rig, mats, mi):
    bm = bmesh.new()
    groups = []
    for side in ("l", "r"):
        hand = rig.matrix_world @ rig.data.bones[f"hand_{side}"].tail_local
        d = (hand - rig.matrix_world @ rig.data.bones[f"hand_{side}"].head_local).normalized()
        for k in range(4):
            s0 = len(bm.verts)
            off = Vector(((k - 1.5) * 0.018, -0.01, 0))
            hb.cone_along(bm, hand + off + d * 0.06, d + Vector((0, -0.4, 0)), 0.05, 0.007, segs=5, steps=3, bend=Vector((0, 0, -0.8)))
            groups.append((f"hand_{side}", range(s0, len(bm.verts))))
    o = hb.part("Claws", bm, mi["bone"], mats)
    for n, idx in groups:
        g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n)
        g.add(list(idx), 1.0, "REPLACE")
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = sm.create_body()
    scales = scale_texture()
    mats = {
        "scales": sm.mat("VM_Scales", "#FFFFFF", 0.0, 0.6, image=scales),
        "belly": sm.mat("VM_Belly", "#5E8C7A", 0.0, 0.55),
        "bone": sm.mat("VM_Bone", "#E8E0C6", 0.0, 0.4),
        "glow": sm.mat("VM_Eye", "#C8FF3A", 0.0, 0.2, glow=3.0),
        "glow_dark": sm.mat("VM_Spine", "#1E7A6E", 0.0, 0.35, glow=0.6),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    for p in body.data.polygons:
        p.material_index = mi["scales"]
    cylinder_uv(body)
    pelvis = rig.matrix_world @ rig.data.bones["pelvis"].head_local
    pts = sm.tail_path(Vector((0, pelvis.y, pelvis.z + 0.1)))
    tail = sm.make_tail(body, rig, pts, mi["scales"], mi["belly"])
    sm.add_tail_bones(rig, pts)
    parts = head_parts(body, rig, eyes, mats_list, mi) + [spines(body, rig, pts, mats_list, mi), claws(body, rig, mats_list, mi)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in [tail] + parts:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = True
    body.name = body.data.name = "VanMonster"
    rig.name = rig.data.name = "VanMonsterRig"
    made = sm.build_actions(rig)
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
    print("VAN_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


if __name__ == "__main__":
    main()
