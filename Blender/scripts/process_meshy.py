"""Meshy GLB'lerini işler: adlandır, birleştir, sadeleştir (mobil), eski modelin boyuna ölçekle, ayağı/tabanı zemine oturt,
FBX (dokular gömülü) + .blend olarak Blender/meshy_processed altına yazar.
Çalıştır: blender -b -P Blender/scripts/process_meshy.py"""
import bpy, glob, os, mathutils

ROOT = r"D:\Repolar\junpline"
SRC = os.path.join(ROOT, "Blender", "meshy_raw")
OUT = os.path.join(ROOT, "Blender", "meshy_processed")
OLD_OBS = os.path.join(ROOT, "Assets", "Art", "Environment", "Obstacles")
OLD_BOSS = os.path.join(ROOT, "Assets", "Art", "Boss")
os.makedirs(OUT, exist_ok=True)

# meshy_raw dosya adı sırasıyla (görev kimliği) -> ad, tür, eski model (ölçek kaynağı) ya da hedef boy (m)
NAMES = [
    ("Baran", "char", 1.8), ("Sahmeran", "char", "Boss/Sahmeran"), ("VanMonster", "char", "Boss/VanMonster"),
    ("StoneGuardian", "char", "Boss/StoneGuardian"), ("Lamassu", "char", "Boss/Lamassu"), ("Humbaba", "char", "Boss/Humbaba"),
    ("Dragon", "char", "Boss/Dragon"), ("Karakoncolos", "char", "Boss/Karakoncolos"),
    ("LowArch", "obs", "LowArch"), ("TeaCart", "obs", "TeaCart"), ("TeaTable", "obs", "TeaTable"), ("MelonTruck", "obs", "MelonTruck"),
    ("KebabGrill", "obs", "KebabGrill"), ("SlideBar", "obs", "SlideBar"), ("PotRack", "obs", "PotRack"), ("MelonStall", "obs", "MelonStall"),
    ("CrateStack", "obs", "CrateStack"), ("KilimLine", "obs", "KilimLine"), ("CityBus", "obs", "CityBus"), ("Minibus", "obs", "Minibus"),
    ("PatPat", "obs", "PatPat"), ("RampTruck", "obs", "RampTruck"), ("SimitCart", "obs", "SimitCart"), ("SurGate", "obs", "SurGate"),
    ("Tractor", "obs", "Tractor"),
    ("M03_CornerBuilding", "mod", 9.0), ("M07_CityWall", "mod", 8.0), ("M06_StallDecor", "mod", 1.0), ("M11_Dome", "mod", 5.0),
    ("M13_BridgeLantern", "mod", 1.6), ("M12_BridgeArch", "mod", 10.0), ("M15_Boat", "mod", 1.0),
    ("Akce", "item", 0.35), ("ClayJug", "item", 0.5), ("Watermelon", "item", 0.45), ("TepegozClub", "item", 2.4),
    ("Tepegoz_Old", "char", "Boss/Tepegoz"),
]
TRIS = {"char": 40000, "obs": 25000, "mod": 20000, "item": 6000}


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def bounds(objs):
    pts = [o.matrix_world @ mathutils.Vector(c) for o in objs for c in o.bound_box]
    mn = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    mx = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return mn, mx


def old_height(ref):
    folder = OLD_BOSS if ref.startswith("Boss/") else OLD_OBS
    path = os.path.join(folder, ref.split("/")[-1] + ".fbx")
    if not os.path.exists(path):
        return None
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == "MESH"]
    h = None
    if meshes:
        mn, mx = bounds(meshes)
        h = mx.z - mn.z
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    return h


files = sorted(glob.glob(os.path.join(SRC, "*.glb")))
assert len(files) == len(NAMES), (len(files), len(NAMES))
for f, (name, kind, size) in zip(files, NAMES):
    if os.path.exists(os.path.join(OUT, name + ".fbx")):
        continue
    clear()
    target_h = size if isinstance(size, float) else old_height(size)
    bpy.ops.import_scene.gltf(filepath=f)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    for o in list(bpy.context.scene.objects):
        if o != obj:
            bpy.data.objects.remove(o, do_unlink=True)
    obj.name = name
    obj.data.name = name + "_Mesh"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # sadeleştir
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    goal = TRIS[kind]
    if tris > goal:
        m = obj.modifiers.new("Decimate", "DECIMATE")
        m.ratio = goal / tris
        bpy.ops.object.modifier_apply(modifier=m.name)
    # ölçek + zemine oturt (taban ortası orijin)
    mn, mx = bounds([obj])
    h = mx.z - mn.z
    if target_h and h > 0:
        s = target_h / h
        obj.scale = (s, s, s)
        bpy.ops.object.transform_apply(scale=True)
    mn, mx = bounds([obj])
    obj.location -= mathutils.Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    bpy.ops.object.transform_apply(location=True)
    bpy.ops.object.shade_smooth()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, name + ".blend"))
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=False, path_mode="COPY",
                             embed_textures=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y")
    final = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    print("DONE", name, kind, tris, "->", final, "h=%.2f" % (target_h or h))
