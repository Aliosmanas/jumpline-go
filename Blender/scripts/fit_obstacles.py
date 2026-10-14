"""Meshy engel modellerini oyundaki eski engelin kutusuna oturtur (oynanış ayarlı boyutlar + çarpışma profili korunur).
Her engel için: eski FBX'in sınır kutusu ve koşu yönündeki yükseklik profili ölçülür; yeni model 4 yönde (0/90/180/270°)
denenir, profili en iyi uyan yön seçilir, eski kutuya (en/boy/yükseklik) ölçeklenip aynı yere konur.
Çıktı: Assets/Art/Environment/Obstacles/<ad>.fbx (eski dosyanın üzerine; eski hali git'te), malzeme <ad>_Mat (gömülü doku).
Çalıştır: blender -b -P Blender/scripts/fit_obstacles.py"""
import bpy, os, math, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tex_export
import numpy as np
from mathutils import Vector, Matrix

ROOT = r"D:\Repolar\junpline"
OBS = os.path.join(ROOT, "Assets", "Art", "Environment", "Obstacles")
PROC = os.path.join(ROOT, "Blender", "meshy_processed")
# Rampalı kamyon / karpuz kamyonu / traktör / kilim / kazan askısı / sur kapısı: eski modeller (rampa ve kayma açıklığı oynanışa uymuyor)
NAMES = ["LowArch", "TeaCart", "TeaTable", "KebabGrill", "SlideBar", "MelonStall", "CrateStack", "CityBus", "Minibus", "PatPat", "SimitCart"]
BINS = 24


def verts_world(objs):
    out = []
    for o in objs:
        M = o.matrix_world
        out += [tuple(M @ v.co) for v in o.data.vertices]
    return np.array(out)


def profile(v, mn, mx):
    """Koşu yönü (Blender Y) boyunca her dilimin en yüksek noktası, kutu yüksekliğine oranlı."""
    y = (v[:, 1] - mn[1]) / max(1e-6, mx[1] - mn[1]); z = (v[:, 2] - mn[2]) / max(1e-6, mx[2] - mn[2])
    p = np.zeros(BINS)
    idx = np.clip((y * BINS).astype(int), 0, BINS - 1)
    np.maximum.at(p, idx, z)
    return p


for name in NAMES:
    old_path = os.path.join(OBS, name + ".fbx")
    if not os.path.exists(old_path) or not os.path.exists(os.path.join(PROC, name + ".blend")):
        print("SKIP", name); continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=old_path)
    olds = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    vo = verts_world(olds); omn, omx = vo.min(0), vo.max(0)
    po = profile(vo, omn, omx)
    for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)

    with bpy.data.libraries.load(os.path.join(PROC, name + ".blend")) as (src, dst):
        dst.objects = list(src.objects)
    new = next(o for o in dst.objects if o and o.type == "MESH")
    bpy.context.scene.collection.objects.link(new)
    base = np.array([tuple(v.co) for v in new.data.vertices])
    best = None
    for k in range(4):
        R = np.array(Matrix.Rotation(k * math.pi / 2, 3, "Z"))
        v = base @ R.T
        mn, mx = v.min(0), v.max(0)
        s = (omx - omn) / np.maximum(mx - mn, 1e-6)
        distort = float(np.std(np.log(s)))
        score = float(np.corrcoef(profile(v, mn, mx), po)[0, 1]) if po.std() > 1e-3 else 0.0
        score = (0 if np.isnan(score) else score) - 1.5 * distort
        if best is None or score > best[0]: best = (score, k, mn, mx, s)
    score, k, mn, mx, s = best
    me = new.data
    R = Matrix.Rotation(k * math.pi / 2, 4, "Z")
    S = Matrix.Diagonal((s[0], s[1], s[2], 1.0))
    T0 = Matrix.Translation(Vector(-(mn + mx) / 2))
    T1 = Matrix.Translation(Vector((omn + omx) / 2))
    me.transform(T1 @ S @ T0 @ R)
    new.matrix_world = Matrix.Identity(4)
    new.name = name; me.name = name + "_Mesh"
    for i, sl in enumerate(new.material_slots):
        if sl.material: sl.material.name = f"{name}_Mat" + (f"_{i}" if i else "")
    tex_export.save_images(old_path, name)
    bpy.ops.object.select_all(action="DESELECT"); new.select_set(True); bpy.context.view_layer.objects.active = new
    bpy.ops.export_scene.fbx(filepath=old_path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="RELATIVE", embed_textures=False)
    print("FIT", name, "rot", k * 90, "scale", np.round(s, 2), "score %.2f" % score, "box", np.round(omx - omn, 2))
