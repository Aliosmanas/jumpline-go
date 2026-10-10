"""
LAMASSU — bölüm 7 canavarı. Kendi tasarımımız: Mezopotamya kapı bekçisinden esinli kanatlı dev (anıt kopyası değil).

Tepegöz/Humbaba hattını yeniden kullanır (gövde, oranlar, 6 animasyon):
 1. MPFB iri erkek gövdesi; geniş göğüs, kalın boyun.
 2. Örgülü uzun sakal (sıralı koniler), yüksek silindir taç + iki çift boynuz, lacivert-altın.
 3. Sırtta geniş tüy kanatları (ortak `wing` yapıcısı — Ejderha da kullanır), ayaklarda boğa toynakları, bel kuşağı.
Çıktı: Assets/Art/Boss/Lamassu.fbx, Blender/source/lamassu.blend
Çalıştır: blender -b -P Blender/scripts/build_lamassu.py
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_tepegoz as tg  # noqa: E402
import build_humbaba as hb  # noqa: E402

ART = tg.ART
FBX = os.path.join(ART, "Lamassu.fbx")
BLEND = os.path.join(tg.ROOT, "Blender", "source", "lamassu.blend")

tg.PHENOTYPE = {"age": 0.6, "cupsize": 0.5, "firmness": 0.8, "gender": 1.0, "height": 1.0,
                "muscle": 0.9, "proportions": 1.0, "weight": 0.7,
                "race": {"african": 0.2, "asian": 0.1, "caucasian": 0.7}}
tg.PROPORTIONS = dict(tg.PROPORTIONS, **{
    "head": (1.35, 1.35), "neck_01": (0.6, 2.2), "spine_03": (1.05, 1.6), "spine_02": (1.0, 1.4),
    "upperarm_l": (1.2, 1.6), "upperarm_r": (1.2, 1.6), "lowerarm_l": (1.25, 1.6), "lowerarm_r": (1.25, 1.6),
    "thigh_l": (0.85, 1.4), "thigh_r": (0.85, 1.4), "calf_l": (0.85, 1.5), "calf_r": (0.85, 1.5),
})


def wing(bm, root, side, span, height, feathers=16, droop=0.25):
    """Sırttan yana-yukarı açılan kanat: üst kenar kemiği + aşağı sarkan sivri tüy dilimleri (çift yüzlü düz)."""
    out = Vector((side * 1.0, 0.35, 0.55)).normalized()
    tip = root + out * span
    for i in range(feathers):
        t = (i + 0.5) / feathers
        top = root.lerp(tip, t) + Vector((0, 0, 0.05 * math.sin(t * math.pi)))
        length = height * (1.0 - 0.55 * t) * (0.85 + 0.15 * math.cos(i * 1.7))
        w = span / feathers * 1.5   # bindirmeli tüyler
        a = bm.verts.new(top + Vector((-side * w * 0.5, 0, 0)))
        b = bm.verts.new(top + Vector((side * w * 0.5, 0, 0)))
        c = bm.verts.new(top + Vector((side * w * 0.35, 0.06 + droop * t, -length)))
        d = bm.verts.new(top + Vector((side * w * 0.05, 0.06 + droop * t, -length * 1.08)))
        bm.faces.new((a, b, c, d))
        if i % 2 == 0:   # ikinci (kısa) sıra: kanadı dolgun gösterir
            e = bm.verts.new(top + Vector((-side * w * 0.4, -0.015, -0.02)))
            f2 = bm.verts.new(top + Vector((side * w * 0.6, -0.015, -0.02)))
            g = bm.verts.new(top + Vector((side * w * 0.3, 0.03, -length * 0.55)))
            bm.faces.new((e, f2, g))
    hb.cone_along(bm, root, out, span * 1.02, 0.03, segs=6, steps=3, bend=Vector((0, 0, 0.3)))   # kanat kemiği


def head_parts(body, rig, eyes, mats, mi):
    head = tg.bone_head(rig, "head")
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    half = max(abs(p.x) for p in pts)
    front = min(p.y for p in pts)
    back = max(p.y for p in pts)
    cy = (front + back) * 0.5
    eye_z = (eyes[0].z + eyes[1].z) * 0.5
    parts = []
    for c in eyes:
        parts.append(hb.part("Eye", hb.sphere(c, 0.0145), mi["eye"], mats))
        parts.append(hb.part("Iris", hb.sphere(c + Vector((0, -0.011, 0)), 0.0065, 10), mi["lapis"], mats))
    # örgülü sakal: çeneden göğse sıralı koniler
    bm = bmesh.new()
    for row in range(5):
        for k in range(7):
            x = (k - 3) * half * 0.24
            base = Vector((x * (1 - row * 0.12), front + 0.02 + row * 0.012, eye_z - 0.07 - row * 0.045))
            hb.cone_along(bm, base, Vector((0, -0.25, -1)), 0.08, 0.02, segs=6, steps=2)
    parts.append(hb.part("Beard", bm, mi["hair"], mats))
    # saç: başlığın altından omuza dökülen bukleler
    bm = bmesh.new()
    for k in range(9):
        a = -math.pi * 0.55 + k * (math.pi * 1.1 / 8)
        base = Vector((math.sin(a) * half * 0.95, cy + math.cos(a) * (back - cy) * 0.9, eye_z + 0.02))
        hb.cone_along(bm, base, Vector((math.sin(a) * 0.2, 0.25, -1)), 0.13, 0.02, segs=6, steps=2)
    parts.append(hb.part("Hair", bm, mi["hair"], mats))
    # yüksek silindir taç + iki çift boynuz
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=half * 1.08, radius2=half * 1.18, depth=0.16)
    for v in bm.verts:
        v.co += Vector((0, cy + 0.005, top + 0.03))
    parts.append(hb.part("Crown", bm, mi["lapis"], mats))
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=half * 1.2, radius2=half * 1.2, depth=0.025)
    for v in bm.verts:
        v.co += Vector((0, cy + 0.005, top + 0.115))
    for row in range(2):
        z = top + 0.02 + row * 0.06
        for sx in (-1, 1):
            hb.cone_along(bm, Vector((sx * half * 1.05, cy - 0.02, z)), Vector((sx * 0.3, -1, 0.15)), 0.09, 0.012, segs=6, steps=4,
                          bend=Vector((-sx * 0.8, 0.2, 0.6)))
    parts.append(hb.part("CrownGold", bm, mi["gold"], mats))
    return parts


def body_parts(body, rig, mats, mi):
    parts = []
    # kanatlar: kürek kemikleri arasından
    sp = tg.bone_head(rig, "spine_03")
    back = max(v.co.y for v in body.data.vertices if abs(v.co.z - sp.z) < 0.04 and abs(v.co.x) < 0.12)
    bm = bmesh.new()
    for side in (-1, 1):
        wing(bm, Vector((side * 0.08, back - 0.01, sp.z + 0.08)), side, 1.25, 0.75)
    o = hb.part("Wings", bm, mi["wing"], mats)
    o.vertex_groups.new(name="spine_03").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    parts.append(o)
    # toynaklar
    bm = bmesh.new()
    groups = []
    for side in ("l", "r"):
        foot = tg.bone_head(rig, f"foot_{side}")
        s0 = len(bm.verts)
        fz = min(v.co.z for v in body.data.vertices if abs(v.co.x - foot.x) < 0.12 and v.co.z < foot.z + 0.05)
        bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.13, radius2=0.095, depth=0.16)
        for v in bm.verts[s0:] if False else list(bm.verts)[s0:]:
            v.co += Vector((foot.x, foot.y - 0.06, fz + 0.075))   # ayağı tabandan bileğe sarar
        groups.append((f"foot_{side}", range(s0, len(bm.verts))))
    o = hb.part("Hooves", bm, mi["hoof"], mats)
    for n, idx in groups:
        o.vertex_groups.new(name=n).add(list(idx), 1.0, "REPLACE")
    parts.append(o)
    # bel kuşağı
    pelvis = tg.bone_head(rig, "pelvis")
    bz = pelvis.z + 0.1
    sel = [v.co for v in body.data.vertices if abs(v.co.z - bz) < 0.03 and abs(v.co.x) < 0.35]
    hw = max(abs(c.x) for c in sel)
    fy, by = min(c.y for c in sel), max(c.y for c in sel)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=1.0, radius2=1.0, depth=0.1)
    for v in bm.verts:
        v.co = Vector((v.co.x * hw * 1.08, v.co.y * (by - fy) * 0.56, v.co.z)) + Vector((0, (fy + by) * 0.5, bz))
    o = hb.part("Belt", bm, mi["gold"], mats)
    tg.copy_weights(o, body)
    parts.append(o)
    return parts


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = tg.create_body()
    mats = {
        "skin": tg.mat("LM_Skin", "#B98A62", 0.0, 0.6),
        "eye": tg.mat("LM_Eye", "#F2EEE4", 0.0, 0.2),
        "lapis": tg.mat("LM_Lapis", "#1F3E8C", 0.2, 0.35, glow=0.3),
        "gold": tg.mat("LM_Gold", "#E2B24A", 1.0, 0.25),
        "hair": tg.mat("LM_Hair", "#1A1210", 0.0, 0.7),
        "wing": tg.mat("LM_Wing", "#D9C7A0", 0.0, 0.6),
        "hoof": tg.mat("LM_Hoof", "#2A2420", 0.0, 0.5),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    face = head_parts(body, rig, eyes, mats_list, mi)
    tg.stylize(body, rig, face)
    extra = body_parts(body, rig, mats_list, mi)
    kilt = tg.make_pestemal(body, rig, mats_list, mi["lapis"])   # saçaklı lacivert etek
    extra.append(kilt)
    for o in extra:
        o.parent = rig
        o.modifiers.new("Armature", "ARMATURE").object = rig
    bpy.ops.object.select_all(action="DESELECT")
    for o in face + extra:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = p.material_index not in (mi["wing"],)
    body.name = body.data.name = "Lamassu"
    rig.name = rig.data.name = "LamassuRig"
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
    print("LAMASSU_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


if __name__ == "__main__":
    main()
