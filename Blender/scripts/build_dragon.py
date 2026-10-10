"""
EJDERHA — bölüm 8 (final) canavarı. Kendi tasarımımız: kanatlı, yılan gövdeli kızıl-altın ejderha.

Mevcut hatların birleşimi:
 • Şahmeran: bacaksız gövde + 12 kemikli kuyruk + süzülerek kaçış animasyonları
 • Van Gölü Canavarı: sürüngen ağzı/çenesi, dişler, sırt dikenleri, pençeler
 • Lamassu: kanat yapıcısı (burada zarlı ejderha kanadı tonunda)
Ek: geriye kıvrık altın boynuzlar, kızıl pul dokusu, parlayan turuncu gözler/dikenler.
Çıktı: Assets/Art/Boss/Dragon.fbx (+ Dragon_Scales.png), Blender/source/dragon.blend
Çalıştır: blender -b -P Blender/scripts/build_dragon.py
"""
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_sahmeran as sm  # noqa: E402
import build_humbaba as hb  # noqa: E402
import build_van_monster as vm  # noqa: E402
import build_lamassu as lm  # noqa: E402

ART = sm.ART
FBX = os.path.join(ART, "Dragon.fbx")
SCALES_PNG = os.path.join(ART, "Dragon_Scales.png")
BLEND = os.path.join(sm.ROOT, "Blender", "source", "dragon.blend")

sm.PHENOTYPE = {"age": 0.5, "cupsize": 0.5, "firmness": 0.85, "gender": 1.0, "height": 1.0,
                "muscle": 1.0, "proportions": 0.9, "weight": 0.7,
                "race": {"african": 0.3, "asian": 0.3, "caucasian": 0.4}}
sm.TAIL_LEN = 3.4


def scale_texture(size=512):
    """vm.scale_texture'ın kızıl-altın sürümü."""
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
    red = np.array([0.55, 0.08, 0.04])
    gold = np.array([0.85, 0.5, 0.08])
    base = gold * (shade[..., None] ** 3) + red * (1 - shade[..., None] ** 3)
    img = base * (1 - edge[..., None] * 0.75)
    rgba = np.concatenate([img, np.ones((size, size, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new("Dragon_Scales", size, size)
    im.pixels.foreach_set(rgba[::-1].ravel())
    im.filepath_raw = SCALES_PNG
    im.file_format = "PNG"
    im.save()
    return im


def horns(body, rig, mats, mi):
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    half = max(abs(p.x) for p in pts)
    front, back = min(p.y for p in pts), max(p.y for p in pts)
    bm = bmesh.new()
    for sx in (-1, 1):
        hb.cone_along(bm, Vector((sx * half * 0.55, (front + back) * 0.5, top - 0.02)), Vector((sx * 0.35, 0.8, 0.6)), 0.26, 0.026,
                      segs=8, steps=8, bend=Vector((0, 0.5, -0.5)))
    o = hb.part("Horns", bm, mi["gold"], mats)
    o.vertex_groups.new(name="head").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


def wings(body, rig, mats, mi):
    sp = rig.matrix_world @ rig.data.bones["spine_03"].head_local
    back = max(v.co.y for v in body.data.vertices if abs(v.co.z - sp.z) < 0.04 and abs(v.co.x) < 0.12)
    bm = bmesh.new()
    for side in (-1, 1):
        lm.wing(bm, Vector((side * 0.08, back - 0.01, sp.z + 0.06)), side, 1.5, 0.95, feathers=12, droop=0.15)
    o = hb.part("Wings", bm, mi["wing"], mats)
    o.vertex_groups.new(name="spine_03").add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    o.parent = rig
    o.modifiers.new("Armature", "ARMATURE").object = rig
    return o


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = sm.create_body()
    scales = scale_texture()
    mats = {
        "scales": sm.mat("DR_Scales", "#FFFFFF", 0.1, 0.45, image=scales),
        "belly": sm.mat("DR_Belly", "#E0A23A", 0.0, 0.5),
        "bone": sm.mat("DR_Bone", "#F0E6CC", 0.0, 0.4),
        "glow": sm.mat("DR_Eye", "#FF8A1E", 0.0, 0.2, glow=4.0),
        "glow_dark": sm.mat("DR_Spine", "#FF5A1E", 0.0, 0.35, glow=1.2),
        "gold": sm.mat("DR_Gold", "#E2B24A", 1.0, 0.25),
        "wing": sm.mat("DR_Wing", "#7A1A12", 0.0, 0.6),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    for p in body.data.polygons:
        p.material_index = mi["scales"]
    vm.cylinder_uv(body)
    pelvis = rig.matrix_world @ rig.data.bones["pelvis"].head_local
    pts = sm.tail_path(Vector((0, pelvis.y, pelvis.z + 0.1)))
    tail = sm.make_tail(body, rig, pts, mi["scales"], mi["belly"])
    sm.add_tail_bones(rig, pts)
    parts = vm.head_parts(body, rig, eyes, mats_list, mi)
    parts += [vm.spines(body, rig, pts, mats_list, mi), vm.claws(body, rig, mats_list, mi), horns(body, rig, mats_list, mi),
              wings(body, rig, mats_list, mi)]
    bpy.ops.object.select_all(action="DESELECT")
    for o in [tail] + parts:
        o.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    for p in body.data.polygons:
        p.use_smooth = p.material_index != mi["wing"]
    body.name = body.data.name = "Dragon"
    rig.name = rig.data.name = "DragonRig"
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
    print("DRAGON_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


if __name__ == "__main__":
    main()
