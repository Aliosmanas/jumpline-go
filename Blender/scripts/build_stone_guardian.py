"""
TAŞ BEKÇİ — bölüm 4 canavarı (Şanlıurfa). Kendi tasarımımız: Göbeklitepe'nin T biçimli dikilitaşlarından esinlenen,
canlanmış kireçtaşı dev. Birebir anıt kopyası değil; yalnız "T başlıklı taş" fikri.

Tepegöz/Humbaba hattını yeniden kullanır (gövde, oranlar, 6 animasyon):
 1. MPFB iri erkek gövdesi, kalın-kısa bacaklı blok oranlar.
 2. Kireçtaşı ten (gri-bej, çatlaklı), göğüste oyma kabartma çizgileri (kol/el motifi), omuzlarda taş bloklar.
 3. Baş yerine T biçimli taş başlık: yatay blok + iki parlayan turkuaz göz yarığı.
 4. Bel kuşağı: taş halka + oyma kemer.
Çıktı: Assets/Art/Boss/StoneGuardian.fbx (+ StoneGuardian_Skin.png), Blender/source/stone_guardian.blend
Çalıştır: blender -b -P Blender/scripts/build_stone_guardian.py
"""
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_tepegoz as tg  # noqa: E402
import build_humbaba as hb  # noqa: E402

ART = tg.ART
FBX = os.path.join(ART, "StoneGuardian.fbx")
SKIN_PNG = os.path.join(ART, "StoneGuardian_Skin.png")
BLEND = os.path.join(tg.ROOT, "Blender", "source", "stone_guardian.blend")

tg.PHENOTYPE = {"age": 0.5, "cupsize": 0.5, "firmness": 0.9, "gender": 1.0, "height": 1.0,
                "muscle": 1.0, "proportions": 0.8, "weight": 0.9,
                "race": {"african": 0.2, "asian": 0.2, "caucasian": 0.6}}
tg.PROPORTIONS = dict(tg.PROPORTIONS, **{
    "head": (1.25, 1.25), "neck_01": (0.6, 2.2), "spine_03": (1.0, 1.8), "spine_02": (1.0, 1.6), "spine_01": (0.95, 1.4),
    "upperarm_l": (1.2, 2.0), "upperarm_r": (1.2, 2.0), "lowerarm_l": (1.25, 2.1), "lowerarm_r": (1.25, 2.1),
    "hand_l": (1.7, 1.8), "hand_r": (1.7, 1.8), "thigh_l": (0.8, 1.6), "thigh_r": (0.8, 1.6),
    "calf_l": (0.8, 1.7), "calf_r": (0.8, 1.7),
})


def stone_texture():
    """Kireçtaşı: gri-bej taban, yumuşak lekeler, koyu çatlak ağı (MakeHuman UV'sine bağlı değil, tekrarlanan desen)."""
    size = 1024
    rng = np.random.default_rng(7)

    def smooth_noise(cells):
        small = rng.random((cells + 2, cells + 2))
        t = np.linspace(0, cells, size, endpoint=False)
        i = t.astype(int)
        f = (t - i)
        f = f * f * (3 - 2 * f)
        a = small[i][:, i] * (1 - f[None, :]) + small[i][:, i + 1] * f[None, :]
        b = small[i + 1][:, i] * (1 - f[None, :]) + small[i + 1][:, i + 1] * f[None, :]
        return a * (1 - f[:, None]) + b * f[:, None]

    n = smooth_noise(8) * 0.5 + smooth_noise(32) * 0.35 + smooth_noise(96) * 0.15
    cracks = np.abs(smooth_noise(24) - 0.5) < 0.012
    base = np.array(tg.srgb("#BDB29C")[:3], dtype=np.float32)
    dark = np.array(tg.srgb("#7E7362")[:3], dtype=np.float32)
    rgb = base * (0.8 + 0.35 * n[..., None]) * (1 - 0.25 * (n[..., None] < 0.35)) + dark * 0.0
    rgb[cracks] = dark * 0.75
    rgba = np.concatenate([np.clip(rgb, 0, 1), np.ones((size, size, 1))], axis=2).astype(np.float32)
    im = bpy.data.images.new("StoneGuardian_Skin", size, size)
    im.pixels.foreach_set(rgba[::-1].ravel())
    im.filepath_raw = SKIN_PNG
    im.file_format = "PNG"
    im.save()
    return im


def box(bm, center, size):
    out = bmesh.ops.create_cube(bm, size=1.0)
    for v in out["verts"]:
        v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])) + center


def head_parts(body, rig, eyes, mats, mi):
    head = tg.bone_head(rig, "head")
    gi = body.vertex_groups["head"].index
    pts = [v.co for v in body.data.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    top = max(p.z for p in pts)
    half = max(abs(p.x) for p in pts)
    front = min(p.y for p in pts)
    back = max(p.y for p in pts)
    cy = (front + back) * 0.5
    parts = []
    # T başlık: dikey gövde başı örter, üstte geniş yatay blok
    bm = bmesh.new()
    box(bm, Vector((0, cy, head.z + (top - head.z) * 0.42 - 0.03)), (half * 2.5, (back - front) * 1.3, (top - head.z) * 1.4))   # çeneyi de örter
    box(bm, Vector((0, cy, top + 0.035)), (half * 5.2, (back - front) * 1.55, 0.1))
    parts.append(hb.part("TCapital", bm, mi["stone"], mats))
    # göz yarıkları (parlayan)
    bm = bmesh.new()
    for c in eyes:
        box(bm, Vector((c.x * 1.3, cy - (back - front) * 0.65 - 0.004, c.z + 0.01)), (0.04, 0.012, 0.013))   # başlığın ön yüzünde
    parts.append(hb.part("EyeSlits", bm, mi["glow"], mats))
    return parts


def section(body, z, band=0.03):
    """Gövdenin z yüksekliğindeki kesiti: (yarı genişlik, ön y) — kollar hariç (|x| < 0.35)."""
    sel = [v.co for v in body.data.vertices if abs(v.co.z - z) < band and abs(v.co.x) < 0.35]
    return max(abs(c.x) for c in sel), min(c.y for c in sel), max(c.y for c in sel)


def body_parts(body, rig, mats, mi):
    parts = []
    # omuz blokları
    bm = bmesh.new()
    for side in ("l", "r"):
        sh = tg.bone_head(rig, f"upperarm_{side}")
        box(bm, sh + Vector((0.04 if side == "l" else -0.04, 0, 0.07)), (0.24, 0.24, 0.13))
    o = hb.part("ShoulderBlocks", bm, mi["stone_dark"], mats)
    tg.copy_weights(o, body)
    parts.append(o)
    # bel kuşağı
    pelvis = tg.bone_head(rig, "pelvis")
    bm = bmesh.new()
    bz = pelvis.z + 0.12
    hw, fy, by = section(body, bz)
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=1.0, radius2=0.97, depth=0.11)
    for v in bm.verts:
        v.co = Vector((v.co.x * hw * 1.1, v.co.y * (by - fy) * 0.58, v.co.z)) + Vector((0, (fy + by) * 0.5, bz))
    o = hb.part("Belt", bm, mi["stone_dark"], mats)
    tg.copy_weights(o, body)
    parts.append(o)
    # göğüs kabartması: iki kol motifi (ince çıkıntı çizgiler)
    chest = tg.bone_head(rig, "spine_03")
    bm = bmesh.new()
    for k in range(4):
        z = chest.z - 0.03 - 0.06 * k
        _, fy, _ = section(body, z, 0.02)
        for sx in (-1, 1):
            box(bm, Vector((sx * (0.07 + 0.03 * k), fy - 0.004, z)), (0.016, 0.016, 0.1))
    o = hb.part("Relief", bm, mi["glow"], mats)
    tg.copy_weights(o, body)
    parts.append(o)
    return parts


def main():
    os.makedirs(ART, exist_ok=True)
    body, rig, eyes = tg.create_body()
    stone = stone_texture()
    mats = {
        "skin": tg.mat("SG_Skin", "#FFFFFF", 0.0, 0.85, image=stone),
        "stone": tg.mat("SG_Capital", "#FFFFFF", 0.0, 0.9, image=stone),
        "stone_dark": tg.mat("SG_Dark", "#8C8170", 0.0, 0.9),
        "glow": tg.mat("SG_Glow", "#38E0D0", 0.0, 0.3, glow=3.0),
    }
    order = list(mats.keys())
    mats_list = [mats[k] for k in order]
    mi = {k: i for i, k in enumerate(order)}
    body.data.materials.clear()
    for m in mats_list:
        body.data.materials.append(m)
    tg.close_eye_sockets(body, eyes)
    face = head_parts(body, rig, eyes, mats_list, mi)
    tg.stylize(body, rig, face)
    extra = body_parts(body, rig, mats_list, mi)
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
        p.use_smooth = p.material_index == mi["skin"]
    body.name = body.data.name = "StoneGuardian"
    rig.name = rig.data.name = "StoneGuardianRig"
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
    print("STONE_OK height=%.2f verts=%d actions=%s" % (height, len(body.data.vertices), made))


if __name__ == "__main__":
    main()
