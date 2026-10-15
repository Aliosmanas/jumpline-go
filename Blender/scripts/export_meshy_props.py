"""Meshy bina/eşya modellerini Unity'ye aktarır: Assets/Art/Environment/Meshy/<ad>.fbx (+ <ad>_Tex dokular, 1024 px).
Akçe: eski Coin.fbx kutusuna (boyut + duruş) oturtulup Assets/Art/Pickups/Coin.fbx olarak yazılır (CoinField şablonu)."""
import bpy, os, sys, math
import numpy as np
from mathutils import Matrix, Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tex_export

ROOT = r"D:\Repolar\junpline"
PROC = os.path.join(ROOT, "Blender", "meshy_processed")
OUT = os.path.join(ROOT, "Assets", "Art", "Environment", "Meshy")
os.makedirs(OUT, exist_ok=True)


# sokak binaları ham Meshy GLB'den yüksek ayrıntıyla (ara dosya 20k → parçalı görünüyordu)
import glob
RAW = sorted(glob.glob(os.path.join(ROOT, "Blender", "meshy_raw", "*.glb")))
RAW_INDEX = {"M03_CornerBuilding": 25, "M07_CityWall": 26, "M11_Dome": 28, "M06_StallDecor": 27, "M12_BridgeArch": 30}
RAW_TRIS = {"M03_CornerBuilding": 40000, "M07_CityWall": 40000, "M11_Dome": 25000, "M06_StallDecor": 20000, "M12_BridgeArch": 30000}


def load(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(os.path.join(PROC, name + ".blend")) as (src, dst):
        dst.objects = list(src.objects)
    o = next(x for x in dst.objects if x and x.type == "MESH")
    bpy.context.scene.collection.objects.link(o)
    if name in RAW_INDEX:   # aynı boyut/konumda, ham modelden daha ayrıntılı gövde
        ref_dims = o.dimensions.copy(); ref_mats = [sl.material for sl in o.material_slots]
        bpy.data.objects.remove(o, do_unlink=True)
        bpy.ops.import_scene.gltf(filepath=RAW[RAW_INDEX[name]])
        ms = [x for x in bpy.context.scene.objects if x.type == "MESH"]
        bpy.ops.object.select_all(action="DESELECT")
        for x in ms: x.select_set(True)
        bpy.context.view_layer.objects.active = ms[0]
        if len(ms) > 1: bpy.ops.object.join()
        o = bpy.context.view_layer.objects.active
        bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
        for x in list(bpy.context.scene.objects):
            if x != o: bpy.data.objects.remove(x, do_unlink=True)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        m = o.modifiers.new("d", "DECIMATE"); m.ratio = RAW_TRIS[name] / tris; m.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="d")
        sc = ref_dims.z / o.dimensions.z
        o.scale = (sc, sc, sc); bpy.ops.object.transform_apply(scale=True)
        import mathutils
        pts = [o.matrix_world @ mathutils.Vector(c) for c in o.bound_box]
        mn = mathutils.Vector([min(p[i] for p in pts) for i in range(3)]); mx = mathutils.Vector([max(p[i] for p in pts) for i in range(3)])
        o.data.transform(mathutils.Matrix.Translation(-mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))
        bpy.ops.object.shade_smooth()
    for i, sl in enumerate(o.material_slots):
        if sl.material: sl.material.name = f"{name}_Mat" + (f"_{i}" if i else "")
    o.name = name
    return o


# sokakta çok kopya: mobil için üçgen sınırı
TRIS = {"M13_BridgeLantern": 4000, "M15_Boat": 6000}   # binalar ham modelden (RAW_TRIS)


def export(o, path, prefix):
    goal = TRIS.get(prefix)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if goal and tris > goal:
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
        m = o.modifiers.new("d", "DECIMATE"); m.ratio = goal / tris; bpy.ops.object.modifier_apply(modifier="d")
    tex_export.save_images(path, prefix, 2048 if prefix in RAW_INDEX else 1024)
    bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="RELATIVE", embed_textures=False)
    print("PROP", prefix, [round(x, 2) for x in o.dimensions])


for n in ["M03_CornerBuilding", "M07_CityWall", "M06_StallDecor", "M11_Dome", "M12_BridgeArch", "M13_BridgeLantern",
          "M15_Boat", "ClayJug", "Watermelon", "TepegozClub"]:
    o = load(n); export(o, os.path.join(OUT, n + ".fbx"), n)

# Akçe → eski para kutusu
coin_path = os.path.join(ROOT, "Assets", "Art", "Pickups", "Coin.fbx")
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=coin_path)
v = np.array([tuple(o.matrix_world @ x.co) for o in bpy.context.scene.objects if o.type == "MESH" for x in o.data.vertices])
omn, omx = v.min(0), v.max(0)
o = load("Akce")
b = np.array([tuple(x.co) for x in o.data.vertices])
# para düz yüzü: eski paranın en ince ekseni ile yeninin en ince ekseni eşlenir
thin_old = int(np.argmin(omx - omn)); thin_new = int(np.argmin(b.max(0) - b.min(0)))
if thin_old != thin_new:
    ax = "XYZ"[3 - thin_old - thin_new] if {thin_old, thin_new} != {0, 0} else "Z"
    o.data.transform(Matrix.Rotation(math.pi / 2, 4, ax))
b = np.array([tuple(x.co) for x in o.data.vertices]); mn, mx = b.min(0), b.max(0)
s = float(np.max(omx - omn) / np.max(mx - mn))
o.data.transform(Matrix.Translation(Vector((omn + omx) / 2)) @ Matrix.Scale(s, 4) @ Matrix.Translation(Vector(-(mn + mx) / 2)))
o.name = "Coin"
export(o, coin_path, "Coin")
