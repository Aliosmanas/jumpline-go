"""
KARAKONCOLOS — bölüm 6 canavarı (Kars). Kendi tasarımımız: zemheri gecelerinin kambur, tüylü kış cini.

Tepegöz/Humbaba hattını yeniden kullanır (gövde, oranlar, 6 animasyon):
 1. MPFB uzun-ince erkek gövdesi; çok uzun kollar, kocaman eller, kambur sırt.
 2. Koyu kömür-kahve kürk: gövdeye dağılmış sivri tutamlar (omuz, sırt, kol, bacak), açık gri yüz maskesi.
 3. Buz mavisi parlayan gözler, kıvrık kara boynuzlar, uzun buzlu pençeler, sırtta kırağı lekeleri.
Çıktı: Assets/Art/Boss/Karakoncolos.fbx, Blender/source/karakoncolos.blend
Çalıştır: blender -b -P Blender/scripts/build_karakoncolos.py
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_tepegoz as tg  # noqa: E402
import build_humbaba as hb  # noqa: E402

ART = tg.ART
FBX = os.path.join(ART, "Karakoncolos.fbx")
BLEND = os.path.join(tg.ROOT, "Blender", "source", "karakoncolos.blend")
random.seed(23)

tg.PHENOTYPE = {"age": 0.8, "cupsize": 0.5, "firmness": 0.4, "gender": 1.0, "height": 1.0,
                "muscle": 0.6, "proportions": 0.7, "weight": 0.35,
                "race": {"african": 0.2, "asian": 0.3, "caucasian": 0.5}}
tg.PROPORTIONS = dict(tg.PROPORTIONS, **{
    "head": (1.2, 1.2), "neck_01": (0.7, 1.6), "spine_03": (1.1, 1.3), "spine_02": (1.05, 1.15), "spine_01": (1.0, 1.0),
    "upperarm_l": (1.45, 1.3), "upperarm_r": (1.45, 1.3), "lowerarm_l": (1.6, 1.25), "lowerarm_r": (1.6, 1.25),
    "hand_l": (2.1, 1.7), "hand_r": (2.1, 1.7), "thigh_l": (0.9, 1.1), "thigh_r": (0.9, 1.1),
    "calf_l": (0.95, 1.1), "calf_r": (0.95, 1.1), "foot_l": (1.5, 1.3), "foot_r": (1.5, 1.3),
})


def fur(body, rig, mats, mi):
    """Gövde yüzeyine dağılmış, aşağı-dışa eğik sivri tutamlar; her tutam en yakın gövde köşesinin ağırlıklarını alır."""
    me = body.data
    head_g = body.vertex_groups["head"].index
    hand_g = {body.vertex_groups[n].index for n in ("hand_l", "hand_r", "foot_l", "foot_r", "ball_l", "ball_r") if n in body.vertex_groups}
    cand = []
    for v in me.vertices:
        ws = {g.group: g.weight for g in v.groups}
        if ws.get(head_g, 0) > 0.4 or any(ws.get(h, 0) > 0.3 for h in hand_g):
            continue
        cand.append(v)
    bm = bmesh.new()
    picks = random.sample(cand, min(1500, len(cand)))
    for v in picks:
        n = v.normal.normalized()
        d = (n + Vector((0, 0.15, -0.9))).normalized()
        hb.cone_along(bm, v.co - n * 0.004, d, random.uniform(0.035, 0.065), random.uniform(0.01, 0.017), segs=4, steps=1)
    o = hb.part("Fur", bm, mi["fur"], mats)
    tg.copy_weights(o, body)
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


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
    for c in eyes:
        parts.append(hb.part("Eye", hb.sphere(c + Vector((0, -0.002, 0)), 0.015), mi["glow"], mats))
    # kürk başlık: başın arkasını ve yanlarını saran tüylü kapşon (yüz açık)
    cap = hb.sphere(Vector((0, cy + 0.012, cz + 0.008)), half * 1.18, 18)
    for v in cap.verts:
        v.co.y = cy + 0.012 + (v.co.y - cy - 0.012) * ((back - front) * 0.62) / (half * 1.18)
    bmesh.ops.delete(cap, geom=[v for v in cap.verts if v.co.y < cy - (back - front) * 0.18 and v.co.z < top - 0.02], context="VERTS")
    parts.append(hb.part("Hood", cap, mi["fur"], mats))
    bm = bmesh.new()
    for i in range(30):   # başlıktan dışa taşan tutamlar
        a = -math.pi * 0.7 + i * (math.pi * 1.4 / 29)
        base = Vector((math.sin(a) * half * 1.1, cy + math.cos(a) * (back - cy) * 0.95, cz + random.uniform(-0.05, 0.06)))
        out = (base - Vector((0, cy, cz))).normalized() + Vector((0, 0.2, -0.6))
        hb.cone_along(bm, base, out, random.uniform(0.07, 0.12), 0.022, segs=5, steps=2, bend=Vector((0, 0.1, -0.5)))
    parts.append(hb.part("HoodTufts", bm, mi["fur"], mats))
    bm = bmesh.new()   # kıvrık kara boynuzlar
    for sx in (-1, 1):
        hb.cone_along(bm, Vector((sx * half * 0.6, cy - 0.02, top - 0.02)), Vector((sx * 0.9, 0.3, 0.6)), 0.22, 0.024, segs=8,
                      bend=Vector((-sx * 0.6, 0.4, 0.9)), steps=8)
    parts.append(hb.part("Horns", bm, mi["horn"], mats))
    return parts


def claws(body, rig, mats, mi):
    bm = bmesh.new()
    groups = []
    for side in ("l", "r"):
        for f in ("index", "middle", "ring", "pinky"):
            name = f"{f}_03_{side}"
            if name not in rig.data.bones:
                continue
            b = rig.data.bones[name]
            tip = rig.matrix_world @ b.tail_local
            d = (tip - rig.matrix_world @ b.head_local).normalized()
            s0 = len(bm.verts)
            hb.cone_along(bm, tip - d * 0.005, d, 0.075, 0.008, segs=5, steps=3, bend=Vector((0, 0, -0.9)))
            groups.append((name, range(s0, len(bm.verts))))
    o = hb.part("Claws", bm, mi["ice"], mats)
    for n, idx in groups:
        g = o.vertex_groups.get(n) or o.vertex_groups.new(name=n)
        g.add(list(idx), 1.0, "REPLACE")
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = tg.create_body()
    mats = {
        "skin": tg.mat("KK_Skin", "#5A5560", 0.0, 0.8),
        "fur": tg.mat("KK_Fur", "#2A211C", 0.0, 0.95),
        "glow": tg.mat("KK_Eye", "#7FE8FF", 0.0, 0.2, glow=3.5),
        "horn": tg.mat("KK_Horn", "#141012", 0.0, 0.35),
        "ice": tg.mat("KK_Ice", "#CFF4FF", 0.0, 0.1, glow=0.6),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    head_g = body.vertex_groups["head"].index   # ten koyu kürk; yalnız yüz/baş gri deri
    for p in body.data.polygons:
        hw = sum(g.weight for vi in p.vertices for g in body.data.vertices[vi].groups if g.group == head_g) / len(p.vertices)
        p.material_index = mi["skin"] if hw > 0.5 else mi["fur"]
    face = head_parts(body, rig, eyes, mats_list, mi)
    tg.stylize(body, rig, face)
    extra = [fur(body, rig, mats_list, mi), claws(body, rig, mats_list, mi)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in face + extra:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = True
    body.name = body.data.name = "Karakoncolos"
    rig.name = rig.data.name = "KarakoncolosRig"
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
    print("KARA_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


if __name__ == "__main__":
    main()
