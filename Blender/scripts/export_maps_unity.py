"""JumplineGo_Proje.blend → Unity.
  • Haritalarda kullanılan her kütüphane modeli: Assets/Art/Environment/Meshy/<ad>.fbx (+_Tex) — orijinde, birim dönüşüm.
  • Her harita: Assets/Art/Environment/Maps/<harita>_Ozel.fbx (zemin, deniz, sur gövdesi, tente, bayrak, çamaşır —
    harita kökenine göre), Assets/Data/Maps/<harita>.json (model yerleşimi: ad, konum, Z dönüşü, ölçek, boya; ışıklar).
  Koordinatlar Blender'da (x sağ, y koşu yönü, z yukarı), harita kökenine göre; Unity tarafı çevirir.
Çalıştır: blender -b -P Blender/scripts/export_maps_unity.py"""
import bpy, os, sys, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tex_export
from mathutils import Vector, Matrix

ROOT = r"D:\Repolar\junpline"
SRC = os.path.join(ROOT, "Blender", "Harita", "JumplineGo_Proje.blend")
MESHY = os.path.join(ROOT, "Assets", "Art", "Environment", "Meshy")
MAPS = os.path.join(ROOT, "Assets", "Art", "Environment", "Maps")
DATA = os.path.join(ROOT, "Assets", "Data", "Maps")
for d in (MESHY, MAPS, DATA): os.makedirs(d, exist_ok=True)
GRID = {"1_MAHALLE": (70.0, 60.0, 0.0), }

bpy.ops.wm.open_mainfile(filepath=SRC)
lib_coll = bpy.data.collections["00_Kutuphane_Tum_Meshler"]
LIB = {}
for c in lib_coll.children:
    for o in c.objects:
        if o.type == "MESH": LIB[o.data.name] = o.name   # mesh adı → model adı

maps = None
ZONE_OF = {}
for z in ("1_MAHALLE", "2_GECIS", "3_SUR_USTU_DENIZ", "4_PORTAL_EVRENI"):
    zc = bpy.data.collections.get(z)
    if zc:
        for ch in zc.children: ZONE_OF[ch.name] = z
maps = sorted([bpy.data.collections[n] for n in ZONE_OF], key=lambda c: c.name)
# zemin/deniz dokuları bir kez Unity klasörüne; FBX'ler göreli yolla bulur (her haritaya kopya yok)
import shutil
TEXD = os.path.join(MAPS, "Dokular"); os.makedirs(TEXD, exist_ok=True)
for img in bpy.data.images:
    fp = bpy.path.abspath(img.filepath)
    if "Harita" in fp and "Dokular" in fp and os.path.exists(fp):
        dst = os.path.join(TEXD, os.path.basename(fp))
        if not os.path.exists(dst): shutil.copy2(fp, dst)
        img.filepath = dst
used = set()
index = []
for mc in maps:
    objs = [o for c in mc.children for o in c.all_objects] if mc.children else list(mc.all_objects)
    obs_names = {o.name for c in mc.children if "_Engel_Ornek" in c.name for o in c.all_objects}
    env = [o for o in objs if o.name not in obs_names]
    # harita kökeni: yol/zemin nesnelerinin x merkezi ve y başlangıcı
    grounds = [o for o in env if o.type == "MESH" and o.data.name not in LIB and ("Yol" in o.name or "Cati_Yolu" in o.name)]
    gpts = [o.matrix_world @ Vector(v) for o in grounds for v in o.bound_box]
    ox = (min(p.x for p in gpts) + max(p.x for p in gpts)) / 2 if gpts else 0.0
    oy = min(p.y for p in gpts) if gpts else 0.0
    origin = Vector((ox, oy, 0.0))
    items, lights, custom = [], [], []
    for o in env:
        if o.type == "LIGHT":
            lights.append({"pos": list(o.location - origin), "color": list(o.data.color), "energy": o.data.energy})
        elif o.type == "MESH" and o.data.name in LIB:
            name = LIB[o.data.name]; used.add(name)
            paint = None
            for sl in o.material_slots:
                if sl.link == "OBJECT" and sl.material and "_" in sl.material.name:
                    paint = sl.material.name[len(name) + 1:]; break
            items.append({"a": name, "p": [round(v, 4) for v in (o.location - origin)], "r": round(math.degrees(o.rotation_euler.z), 3),
                          "s": [round(v, 4) for v in o.scale], "paint": paint})
        elif o.type == "MESH":
            custom.append(o)
    zone = ZONE_OF.get(mc.name, "")
    data = {"name": mc.name, "zone": zone, "length": 30.0, "items": items, "lights": lights}
    with open(os.path.join(DATA, mc.name + ".json"), "w", encoding="utf-8") as f: json.dump(data, f, ensure_ascii=False)
    # özel geometri: kökene taşınmış kopyalar, tek FBX
    bpy.ops.object.select_all(action="DESELECT")
    tmp = []
    for o in custom:
        c = o.copy(); c.data = o.data.copy(); bpy.context.scene.collection.objects.link(c)
        c.matrix_world = Matrix.Translation(-origin) @ o.matrix_world
        if c.name.startswith("Tente"):   # çizgiler için UV: u = cephe boyunca konum / 0.7 m (Unity'de 2 renkli şerit dokusu)
            me = c.data; uvl = me.uv_layers.new(name="UV")
            for poly in me.polygons:
                for li in poly.loop_indices:
                    v = me.vertices[me.loops[li].vertex_index].co
                    uvl.data[li].uv = (v.y / 0.7, 0.5)
        c.select_set(True); tmp.append(c)
    if tmp:
        bpy.context.view_layer.objects.active = tmp[0]
        out = os.path.join(MAPS, mc.name + "_Ozel.fbx")
        tex_export.save_images(out, mc.name[:9], 1024) if False else None
        bpy.ops.export_scene.fbx(filepath=out, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                                 path_mode="RELATIVE", embed_textures=False)
    for c in tmp: bpy.data.objects.remove(c, do_unlink=True)
    index.append({"name": mc.name, "zone": zone, "items": len(items), "custom": len(custom), "lights": len(lights)})
    print("MAP", mc.name, zone, len(items), len(custom), len(lights))

# kullanılan modeller: orijinde tek tek dışa aktarılır (yüksek ayrıntılı binalar zaten var)
for name in sorted(used):
    path = os.path.join(MESHY, name + ".fbx")
    src = bpy.data.objects[name]
    c = src.copy(); bpy.context.scene.collection.objects.link(c)
    c.matrix_world = Matrix.Identity(4)
    bpy.ops.object.select_all(action="DESELECT"); c.select_set(True); bpy.context.view_layer.objects.active = c
    # bu modelin malzemeleri benzersiz ad (Unity malzeme klasöründe çakışmasın)
    for i, sl in enumerate(c.material_slots):
        if sl.material and not sl.material.name.startswith(name): sl.material.name = f"{name}_Mat" + (f"_{i}" if i else "")
    hide = [o for o in bpy.context.scene.objects if o is not c]
    for o in hide: o.hide_set(True) if o.name in bpy.context.view_layer.objects else None
    # yalnız bu nesnenin dokuları yazılsın
    keep = {n.image for sl in c.material_slots if sl.material and sl.material.node_tree for n in sl.material.node_tree.nodes if n.type == "TEX_IMAGE" and n.image}
    d = os.path.splitext(path)[0] + "_Tex"; os.makedirs(d, exist_ok=True)
    for k, img in enumerate(sorted(keep, key=lambda x: x.name)):
        if img.filepath and os.path.exists(bpy.path.abspath(img.filepath)) and "_Tex" in img.filepath: continue
        if img.size[0] > 2048: img.scale(2048, 2048)
        kind = "base" if img.colorspace_settings.name == "sRGB" else "data"
        fp = os.path.join(d, f"{name}_{k}_{kind}.png"); img.filepath_raw = fp; img.file_format = "PNG"; img.save()
        if img.packed_file: img.unpack(method="REMOVE")
        img.filepath = fp
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="RELATIVE", embed_textures=False)
    for o in hide:
        try: o.hide_set(False)
        except Exception: pass
    bpy.data.objects.remove(c, do_unlink=True)
    print("ASSET", name)
with open(os.path.join(DATA, "_index.json"), "w", encoding="utf-8") as f: json.dump(index, f, ensure_ascii=False, indent=1)
print("EXPORT_DONE", len(maps), len(used))
