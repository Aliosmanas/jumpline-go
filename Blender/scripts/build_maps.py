"""JUMPLINE GO — Meshy kütüphanesi + bölgeli harita parçaları (15 Eki 2026, kullanıcı isteği).
Bölgeler: MAHALLE (cıvıl cıvıl, Meshy ev modelleri) → GEÇİŞ (sur kapısı) → SUR ÜSTÜ (iki yan deniz, kayıklar, kuleler)
→ GEÇİŞ → MAHALLE …  Portal evreni: gece çatılar (karanlık ama abartısız).
Rehber (docs/Harita_Tasarim_Rehberi): 30 m parça, 3 şerit (x = -2.5 / 0 / 2.5), cephe |x| = 5.5, dekor yuvası her 5 m,
engel slotu her 10 m. Koşu yönü +Y. Parçalar sınırını taşmaz → rastgele arka arkaya eklenebilir.
Çıktı (Blender/Harita/): JumplineGo_Proje.blend (kütüphane + tüm haritalar tek dosyada), JumplineGo_Kutuphane.blend,
Onizleme/<harita>.png + Haritalar_Tumu.jpg.   Çalıştır: blender -b -P Blender/scripts/build_maps.py"""
import bpy, os, math, random
import mathutils
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
PROC = os.path.join(ROOT, "Blender", "meshy_processed")
HD = os.path.join(ROOT, "Assets", "Art", "Environment", "Meshy")      # yüksek ayrıntılı binalar (40k, 2048 doku)
OUT = os.path.join(ROOT, "Blender", "Harita")
TEX = os.path.join(OUT, "Dokular")
PREV = os.path.join(OUT, "Onizleme")
os.makedirs(PREV, exist_ok=True)

SEG, LANE, ROAD_HALF, WALK_OUT, CURB_H = 30.0, 2.5, 3.75, 5.5, 0.15
WALL_TOP_HALF = 4.6      # sur üstü yürüyüş yolu yarı genişliği
SEA_Z = -9.0             # deniz seviyesi (sur üstü yolu z=0)

CATS = {
    "Binalar": ["M03_CornerBuilding", "M04_BazaarArch", "M05_StringLights", "M06_StallDecor", "M07_CityWall", "M08_WallTower",
                "M09_RooftopBlock", "M10_Minaret", "M11_Dome", "M12_BridgeArch", "M13_BridgeLantern", "M15_Boat"],
    "Engeller": ["CrateStack", "TeaCart", "SimitCart", "MelonStall", "KebabGrill", "TeaTable", "SlideBar", "LowArch", "KilimLine",
                 "PotRack", "BusStop", "Taxi", "Minibus", "CityBus", "PatPat", "MelonTruck", "RampTruck", "Tractor", "SurGate"],
    "Esyalar": ["Akce", "Gem", "Pouch", "Fish", "ClayJug", "Watermelon", "TepegozClub"],
    "Guclendirmeler": ["PU_DoubleCoin", "PU_Magnet", "PU_Shield"],
    "Karakterler": ["Baran", "Baran_SurBekcisi", "Baran_MardinUsta", "Baran_SokakKosucusu", "Baran_EfsaneAvcisi", "Baran_Altin",
                    "Thief", "Cat"],
    "Canavarlar": ["Tepegoz", "Sahmeran", "Humbaba", "StoneGuardian", "VanMonster", "Karakoncolos", "Lamassu", "Dragon"],
}
HD_NAMES = {"M03_CornerBuilding", "M07_CityWall", "M11_Dome", "M06_StallDecor", "M12_BridgeArch"}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
LIB = {}


def new_coll(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or scene.collection).children.link(c)
    return c


def load_asset(name):
    if name in HD_NAMES and os.path.exists(os.path.join(HD, name + ".fbx")):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(HD, name + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before]
        obj = next(o for o in new if o.type == "MESH")
        for o in new:
            if o is not obj: bpy.data.objects.remove(o, do_unlink=True)
        for c in obj.users_collection: c.objects.unlink(obj)
        obj.parent = None
        bpy.context.view_layer.update()
        return obj
    path = os.path.join(PROC, name + ".blend")
    if not os.path.exists(path): return None
    with bpy.data.libraries.load(path) as (src, dst):
        dst.objects = list(src.objects)
    obj = next((o for o in dst.objects if o and o.type == "MESH"), None)
    for o in dst.objects:
        if o and o is not obj: bpy.data.objects.remove(o, do_unlink=True)
    return obj


lib_root = new_coll("00_Kutuphane_Tum_Meshler")
for cat, names in CATS.items():
    cc = new_coll("Kutuphane_" + cat, lib_root)
    for n in names:
        o = load_asset(n)
        if o is None:
            print("YOK", n); continue
        o.name = n
        if o.name not in cc.objects: cc.objects.link(o)
        mw = o.matrix_world.copy()
        o.data.transform(mw); o.matrix_world = mathutils.Matrix.Identity(4)
        pts = [Vector(v) for v in o.bound_box]
        mn = Vector([min(p[i] for p in pts) for i in range(3)]); mx = Vector([max(p[i] for p in pts) for i in range(3)])
        o.data.transform(mathutils.Matrix.Translation(-Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))))
        LIB[n] = o
print("LIB", len(LIB))
# kütüphane ızgarası: haritaların solunda (x < 0), kategori başına bir sütun
gx = -60.0 - 22.0 * len(CATS)
for cat, names in CATS.items():
    gy = 0.0
    for n in names:
        o = LIB.get(n)
        if not o: continue
        o.location = (gx, gy, 0)
        gy += max(o.dimensions.y, 2.0) + 2.5
    gx += 22.0


# ------------------------------------------------------------------ malzemeler
def pbr(name, base, tile, tint=None, rough=None):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (tile, tile, tile)
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    def img(suffix, non_color=False):
        n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(os.path.join(TEX, f"{base}_{suffix}.jpg"), check_existing=True)
        if non_color: n.image.colorspace_settings.name = "Non-Color"
        nt.links.new(mp.outputs["Vector"], n.inputs["Vector"]); return n
    d = img("diff")
    if tint:
        mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs["Fac"].default_value = 1.0
        mix.inputs["Color2"].default_value = (*tint, 1)
        nt.links.new(d.outputs["Color"], mix.inputs["Color1"]); nt.links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        nt.links.new(d.outputs["Color"], bsdf.inputs["Base Color"])
    r = img("rough", True); nt.links.new(r.outputs["Color"], bsdf.inputs["Roughness"])
    nm = img("nor_gl", True); nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.inputs["Strength"].default_value = 0.8
    nt.links.new(nm.outputs["Color"], nmap.inputs["Color"]); nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return m


ROAD = pbr("Yol_Bazalt", "cobblestone_floor_08", 1.0, tint=(0.62, 0.55, 0.5))       # sıcak, koyu bazalt
WALK = pbr("Kaldirim_Tas", "floor_pattern_01", 1.0, tint=(0.95, 0.85, 0.7))
WALLTOP = pbr("Sur_Ustu_Tas", "cobblestone_floor_08", 0.7, tint=(0.45, 0.42, 0.42))
ROOF = pbr("Cati_Kiremit_Gece", "floor_pattern_01", 0.8, tint=(0.35, 0.3, 0.33))


def flat(name, color, rough=0.6, metal=0.0, emit=None, emit_strength=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*color, 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if emit:
        b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = emit_strength
    return m


# deniz: koyu turkuaz, dalga kabartması, parlak
SEA = bpy.data.materials.new("Deniz"); SEA.use_nodes = True
nt = SEA.node_tree; b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
b.inputs["Base Color"].default_value = (0.02, 0.22, 0.27, 1); b.inputs["Roughness"].default_value = 0.08
noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 0.35; noise.inputs["Detail"].default_value = 6
bump = nt.nodes.new("ShaderNodeBump"); bump.inputs["Strength"].default_value = 0.35
nt.links.new(noise.outputs["Fac"], bump.inputs["Height"]); nt.links.new(bump.outputs["Normal"], b.inputs["Normal"])

# ev boyaları (cıvıl cıvıl): Meshy'nin koyu bazalt dokusu önce açılır, sonra güçlü renkle karıştırılır — taş/pencere
# ayrıntısı kalır ama ev gerçekten boyalı görünür (çarpma koyu dokuda işe yaramıyordu). Nesne başına malzeme, mesh paylaşılır.
PAINTS = {"Turuncu": (1.0, 0.42, 0.12), "Sari": (1.0, 0.78, 0.22), "Turkuaz": (0.1, 0.62, 0.68), "Pembe": (0.92, 0.36, 0.48),
          "Kirec_Beyazi": (0.95, 0.9, 0.78), "Yesil": (0.42, 0.75, 0.32), "Mavi": (0.26, 0.45, 0.9), "Kirectasi": (0.92, 0.78, 0.55),
          "Bazalt": None}
PAINTED = {}


def painted(src_obj, paint):
    key = (src_obj.name, paint)
    if key in PAINTED: return PAINTED[key]
    mats = []
    for sl in src_obj.material_slots:
        m = sl.material.copy(); m.name = f"{src_obj.name}_{paint}"
        nt = m.node_tree; bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        link = next((l for l in nt.links if l.to_socket == bsdf.inputs["Base Color"]), None)
        if link:
            hs = nt.nodes.new("ShaderNodeHueSaturation"); hs.inputs["Value"].default_value = 2.4; hs.inputs["Saturation"].default_value = 0.25
            mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MIX"; mix.inputs["Fac"].default_value = 0.58
            mix.inputs["Color2"].default_value = (*PAINTS[paint], 1)
            nt.links.new(link.from_socket, hs.inputs["Color"]); nt.links.new(hs.outputs["Color"], mix.inputs["Color1"])
            nt.links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
        mats.append(m)
    PAINTED[key] = mats
    return mats


def stripes_mat(name, c1, c2, width=0.35):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = 1.0 / width
    fl = nt.nodes.new("ShaderNodeMath"); fl.operation = "PINGPONG"; fl.inputs[1].default_value = 1.0
    gt = nt.nodes.new("ShaderNodeMath"); gt.operation = "GREATER_THAN"; gt.inputs[1].default_value = 0.5
    mix = nt.nodes.new("ShaderNodeMixRGB"); mix.inputs["Color1"].default_value = (*c1, 1); mix.inputs["Color2"].default_value = (*c2, 1)
    nt.links.new(tc.outputs["Object"], sep.inputs["Vector"]); nt.links.new(sep.outputs["Y"], mul.inputs[0])
    nt.links.new(mul.outputs[0], fl.inputs[0]); nt.links.new(fl.outputs[0], gt.inputs[0]); nt.links.new(gt.outputs[0], mix.inputs["Fac"])
    nt.links.new(mix.outputs["Color"], b.inputs["Base Color"]); b.inputs["Roughness"].default_value = 0.75
    return m


AWNING_COLORS = [((0.85, 0.08, 0.08), (0.95, 0.93, 0.88)), ((0.05, 0.5, 0.25), (0.95, 0.93, 0.88)), ((1.0, 0.7, 0.05), (0.95, 0.35, 0.05)),
                 ((0.05, 0.35, 0.8), (0.95, 0.93, 0.88)), ((0.9, 0.3, 0.55), (0.98, 0.85, 0.3)), ((0.1, 0.6, 0.65), (0.98, 0.9, 0.75))]
AWNINGS = [stripes_mat(f"Tente_{i}", a, b) for i, (a, b) in enumerate(AWNING_COLORS)]
FLAG_COLS = [(0.9, 0.1, 0.1), (1.0, 0.75, 0.05), (0.1, 0.55, 0.9), (0.15, 0.7, 0.3), (0.95, 0.4, 0.6), (1.0, 0.5, 0.05), (0.95, 0.95, 0.95)]
FLAGS = []
for i, c in enumerate(FLAG_COLS):
    m = bpy.data.materials.new(f"Bayrak_{i}"); m.use_nodes = True
    bb = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bb.inputs["Base Color"].default_value = (*c, 1); bb.inputs["Roughness"].default_value = 0.8
    FLAGS.append(m)


def mesh_obj(name, verts, faces, mats, coll, mat_idx=None):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    for m in mats: me.materials.append(m)
    if mat_idx:
        for p, mi in zip(me.polygons, mat_idx): p.material_index = mi
    o = bpy.data.objects.new(name, me); coll.objects.link(o)
    return o


def awning(coll, origin, side, y, width, z=3.2, depth=1.3, mat=None):
    """Kavisli çizgili tente: cepheden (|x|=5.5) sokağa doğru eğimli, ucunda saçak."""
    xw = side * WALK_OUT; verts = []; faces = []; n = 6
    for i in range(n + 1):
        t = i / n; dx = depth * t; dz = -0.55 * t * t
        for yy in (-width / 2, width / 2):
            verts.append((origin.x + xw - side * dx, origin.y + y + yy, z + dz + 0.35 * (1 - t)))
    for i in range(n): a = i * 2; faces.append((a, a + 1, a + 3, a + 2))
    base = len(verts); xe = xw - side * depth; ze = z - 0.55
    for yy in (-width / 2, width / 2):
        verts.append((origin.x + xe, origin.y + y + yy, ze)); verts.append((origin.x + xe, origin.y + y + yy, ze - 0.3))
    faces.append((base, base + 2, base + 3, base + 1))
    o = mesh_obj("Tente", verts, faces, [mat], coll)
    o.data.polygons.foreach_set("use_smooth", [True] * len(o.data.polygons))
    return o


def bunting(coll, origin, rng, y, z=6.5, sag=1.0):
    """Sokağın üstünde evden eve renkli üçgen bayrak dizisi."""
    x0, x1 = -WALK_OUT + 0.2, WALK_OUT - 0.2; n = 16
    verts, faces, mi = [], [], []
    pts = [(x0 + (x1 - x0) * i / n, z - sag * 4 * (i / n) * (1 - i / n)) for i in range(n + 1)]
    for i in range(n):
        (xa, za), (xb, zb) = pts[i], pts[i + 1]
        k = len(verts)
        verts += [(origin.x + xa, origin.y + y, za), (origin.x + xb, origin.y + y, zb),
                  (origin.x + (xa + xb) / 2, origin.y + y + 0.02, (za + zb) / 2 - 0.55)]
        faces.append((k, k + 1, k + 2)); mi.append(rng.randrange(len(FLAGS)))
    mesh_obj("Bayraklar", verts, faces, FLAGS, coll, mi)


def laundry(coll, origin, rng, y, z=7.8):
    """Evler arası çamaşır ipi: renkli çamaşırlar."""
    x0, x1 = -WALK_OUT + 0.2, WALK_OUT - 0.2
    verts, faces, mi = [], [], []
    x = x0 + 0.6
    while x < x1 - 0.8:
        w = rng.uniform(0.5, 1.1); h = rng.uniform(0.5, 1.0)
        zz = z - 0.6 * (1 - ((x - x0) / (x1 - x0) * 2 - 1) ** 2)
        k = len(verts)
        verts += [(origin.x + x, origin.y + y, zz), (origin.x + x + w, origin.y + y, zz),
                  (origin.x + x + w, origin.y + y, zz - h), (origin.x + x, origin.y + y, zz - h)]
        faces.append((k, k + 1, k + 2, k + 3)); mi.append(rng.randrange(len(FLAGS)))
        x += w + rng.uniform(0.2, 0.6)
    mesh_obj("Camasirlar", verts, faces, FLAGS, coll, mi)


def box_uv(name, x0, x1, y0, y1, z0, z1, mat, coll, origin, uvscale=2.0):
    bpy.ops.mesh.primitive_cube_add(size=1)
    o = bpy.context.active_object; o.name = name
    o.scale = ((x1 - x0), (y1 - y0), (z1 - z0)); o.location = (origin.x + (x0 + x1) / 2, origin.y + (y0 + y1) / 2, (z0 + z1) / 2)
    bpy.ops.object.transform_apply(scale=True)
    me = o.data; uv = me.uv_layers.new(name="UV")
    for poly in me.polygons:
        nrm = poly.normal
        for li in poly.loop_indices:
            v = me.vertices[me.loops[li].vertex_index].co + o.location
            if abs(nrm.z) > 0.5: uv.data[li].uv = (v.x / uvscale, v.y / uvscale)
            elif abs(nrm.x) > 0.5: uv.data[li].uv = (v.y / uvscale, v.z / uvscale)
            else: uv.data[li].uv = (v.x / uvscale, v.z / uvscale)
    me.materials.append(mat)
    for c in o.users_collection: c.objects.unlink(o)
    coll.objects.link(o)
    return o


def footprint(name, rot_deg, s):
    d = LIB[name].dimensions * s
    return (d.y, d.x) if abs(math.sin(math.radians(rot_deg))) > 0.7 else (d.x, d.y)


def place(name, coll, origin, x, y, rot=0.0, height=None, scale=None, z=0.0, paint=None):
    src = LIB.get(name)
    if src is None: return None
    o = src.copy(); coll.objects.link(o)
    s = scale if scale else (height / max(src.dimensions.z, 1e-3) if height else 1.0)
    o.scale = (s, s, s)
    o.rotation_euler = (0, 0, math.radians(rot))
    o.location = (origin.x + x, origin.y + y, z)
    if paint and PAINTS.get(paint):
        for i, m in enumerate(painted(src, paint)):
            o.material_slots[i].link = "OBJECT"; o.material_slots[i].material = m
    return o


def facade_rot(side): return 90.0 if side < 0 else -90.0   # model önü -Y → sokağa


def lane_obstacles(coll, origin, rng, low, big, coin_z=0.9, base_z=0.0):
    """Her 10 m bir engel satırı (en az bir şerit açık) + açık şeritte akçe dizisi — Subway Surfers akışı."""
    for k in range(3):
        yy = 5 + k * 10
        lanes = [-1, 0, 1]; rng.shuffle(lanes)
        blocked = lanes[:rng.choice((1, 2, 2))]
        for ln in blocked:
            if big and rng.random() < 0.35:
                v = rng.choice(big); d = LIB[v].dimensions
                place(v, coll, origin, ln * LANE, yy, 90.0 if d.x > d.y else 0.0, z=base_z)
            else:
                place(rng.choice(low), coll, origin, ln * LANE, yy, 0, z=base_z)
        free = [ln for ln in (-1, 0, 1) if ln not in blocked][0]
        for c in range(5):
            place("Akce", coll, origin, free * LANE, yy - 3 + c * 1.5, 90, z=base_z + coin_z)


# ------------------------------------------------------------------ MAHALLE
HOUSES = [("M09_RooftopBlock", 8.0), ("M03_CornerBuilding", 9.0), ("M09_RooftopBlock", 9.5)]
MAHALLE = [
    # ad, dekor havuzu, üst, arka plan, park araçları, yoğunluk
    ("Carsi_Girisi", ["M06_StallDecor", "TeaCart", "SimitCart", "ClayJug", "Watermelon"], ["M04_BazaarArch", "M05_StringLights"], ["M10_Minaret"], [], 0.85),
    ("Kapali_Carsi", ["M06_StallDecor", "MelonStall", "KebabGrill", "ClayJug", "PotRack"], ["M04_BazaarArch", "M05_StringLights", "M04_BazaarArch"], [], [], 0.95),
    ("Bakircilar", ["PotRack", "ClayJug", "M06_StallDecor", "CrateStack"], ["M05_StringLights", "M05_StringLights"], ["M11_Dome"], [], 0.85),
    ("Karpuz_Pazari", ["MelonStall", "Watermelon", "CrateStack", "MelonStall"], ["M05_StringLights"], ["M10_Minaret"], ["PatPat"], 0.9),
    ("Cami_Meydani", ["TeaTable", "SimitCart", "M13_BridgeLantern", "M06_StallDecor"], ["M05_StringLights"], ["M11_Dome", "M10_Minaret"], [], 0.7),
    ("Dort_Ayakli_Minare", ["TeaCart", "ClayJug", "M06_StallDecor", "KilimLine"], ["M05_StringLights", "M04_BazaarArch"], ["M10_Minaret", "M10_Minaret"], [], 0.8),
    ("Cay_Bahcesi", ["TeaTable", "TeaTable", "TeaCart", "M13_BridgeLantern"], ["M05_StringLights", "M05_StringLights"], ["M11_Dome"], [], 0.9),
    ("Simitci_Sokagi", ["SimitCart", "SimitCart", "CrateStack", "ClayJug"], ["M04_BazaarArch", "M05_StringLights"], [], ["PatPat"], 0.85),
    ("Kebapcilar", ["KebabGrill", "KebabGrill", "TeaTable", "CrateStack"], ["M05_StringLights", "M05_StringLights"], ["M10_Minaret"], [], 0.9),
    ("Kilimciler", ["KilimLine", "KilimLine", "M06_StallDecor", "PotRack"], ["M04_BazaarArch", "M05_StringLights"], [], [], 0.9),
    ("Dolmus_Duragi", ["BusStop", "SimitCart", "M13_BridgeLantern", "TeaCart"], ["M05_StringLights"], ["M10_Minaret"], ["Taxi"], 0.75),
    ("Bayram_Sokagi", ["M06_StallDecor", "SimitCart", "TeaCart", "MelonStall", "TeaTable"], ["M05_StringLights", "M04_BazaarArch", "M05_StringLights"], ["M11_Dome", "M10_Minaret"], ["Taxi", "PatPat"], 1.0),
]
LANE_LOW = ["CrateStack", "TeaTable", "SlideBar", "LowArch", "MelonStall"]
LANE_BIG = ["Minibus", "CityBus", "Taxi", "PatPat"]


def street_ground(env, origin, tag):
    box_uv(f"{tag}_Yol", -ROAD_HALF, ROAD_HALF, 0, SEG, -0.2, 0, ROAD, env, origin)
    for s in (-1, 1):
        x0, x1 = (ROAD_HALF, WALK_OUT + 0.5) if s > 0 else (-WALK_OUT - 0.5, -ROAD_HALF)
        box_uv(f"{tag}_Kaldirim_{'R' if s > 0 else 'L'}", x0, x1, 0, SEG, -0.2, CURB_H, WALK, env, origin)


def house_row(env, origin, rng, y_from=0.0, y_to=SEG, sides=(-1, 1)):
    paints = list(PAINTS.keys())
    for s in sides:
        y = y_from
        while y < y_to - 0.5:
            fname, fh = rng.choice(HOUSES)
            h = fh * rng.uniform(0.9, 1.12)
            sc = h / LIB[fname].dimensions.z
            rot = facade_rot(s)
            fx, fy = footprint(fname, rot, sc)
            if y + fy > y_to + 0.3:            # sığmıyor: kalan boşluğa küçültülmüş ev
                sc *= max(0.45, (y_to - y) / fy); h = sc * LIB[fname].dimensions.z
                fx, fy = footprint(fname, rot, sc)
            paint = rng.choice(paints[:-1]) if rng.random() < 0.92 else "Bazalt"
            place(fname, env, origin, s * (WALK_OUT + fx / 2), y + fy / 2, rot, height=h, paint=paint)
            y += fy - 0.15


def sidewalk_props(env, origin, rng, props, density, y_from=0.0, y_to=SEG):
    for k in range(int((y_to - y_from) // 5)):
        for s in (-1, 1):
            if rng.random() > density: continue
            pname = rng.choice(props)
            yy = y_from + 2.5 + k * 5 + (1.2 if s > 0 else -1.2)
            if pname in ("KilimLine", "PotRack"):
                place(pname, env, origin, s * (WALK_OUT - 0.4), yy, 0.0, z=CURB_H)
            else:
                fx, fy = footprint(pname, facade_rot(s), 1.0)
                place(pname, env, origin, s * (WALK_OUT - 0.25 - fx / 2), yy, facade_rot(s) + rng.uniform(-8, 8), z=CURB_H)
    for k in range(int((y_to - y_from) // 10)):
        for s in (-1, 1):
            place("M13_BridgeLantern", env, origin, s * (ROAD_HALF + 0.35), y_from + 5 + k * 10 + (5 if s > 0 else 0), facade_rot(s), height=2.1, z=CURB_H)


def overhead(env, origin, items, y_from=0.0, y_to=SEG):
    for k, ov in enumerate(items):
        yy = y_from + (y_to - y_from) * (k + 1) / (len(items) + 1)
        if ov == "M04_BazaarArch":
            place(ov, env, origin, 0, yy, 0, scale=(2 * WALK_OUT + 1.0) / LIB[ov].dimensions.x)
        else:
            o = place(ov, env, origin, 0, yy, 0, z=5.0)
            if o: s = (2 * WALK_OUT) / LIB[ov].dimensions.x; o.scale = (s, s, s)


def build_mahalle(i, theme, origin, coll):
    name, props, over, background, vehicles, density = theme
    rng = random.Random(1000 + i * 37)
    env = new_coll(f"{coll.name}_Cevre", coll); obs = new_coll(f"{coll.name}_Engel_Ornek", coll)
    street_ground(env, origin, coll.name[:9])
    house_row(env, origin, rng)
    sidewalk_props(env, origin, rng, props, density)
    deco = new_coll(f"{coll.name}_Renkli_Suslemeler", coll)
    for sd in (-1, 1):   # dükkân tenteleri
        y = rng.uniform(1.0, 3.0)
        while y < SEG - 2.0:
            w = rng.uniform(2.0, 3.4)
            awning(deco, origin, sd, y + w / 2, w, z=rng.uniform(3.0, 3.6), mat=rng.choice(AWNINGS))
            y += w + rng.uniform(1.0, 3.5)
    for yy in (rng.uniform(4, 9), rng.uniform(14, 18), rng.uniform(24, 28)):
        bunting(deco, origin, rng, yy, z=rng.uniform(6.2, 7.2), sag=rng.uniform(0.6, 1.2))
    if rng.random() < 0.8: laundry(deco, origin, rng, rng.uniform(10, 22), z=rng.uniform(7.5, 8.5))
    for v in vehicles:
        s = rng.choice((-1, 1)); d = LIB[v].dimensions
        place(v, env, origin, s * (ROAD_HALF + 0.9), rng.uniform(6, 24), (90.0 if d.x > d.y else 0.0) + (180 if s > 0 else 0), z=CURB_H)
    overhead(env, origin, over)
    for b in background:
        s = rng.choice((-1, 1)); yy = rng.uniform(5, 25)
        if b == "M10_Minaret": place(b, env, origin, s * rng.uniform(17, 24), yy, rng.uniform(0, 360), height=rng.uniform(17, 21))
        else: place(b, env, origin, s * rng.uniform(15, 22), yy, rng.uniform(0, 360), height=rng.uniform(8, 11), paint="Kirectasi")
    lane_obstacles(obs, origin, rng, LANE_LOW, LANE_BIG)


# ------------------------------------------------------------------ SUR ÜSTÜ (iki yan deniz)
def sea_and_wall_body(env, origin, tag, y0=0.0, y1=SEG):
    box_uv(f"{tag}_Sur_Ustu_Yol", -WALL_TOP_HALF, WALL_TOP_HALF, y0, y1, -0.3, 0, WALLTOP, env, origin)
    # sur gövdesi: yolun altında denize inen bazalt kütle (Meshy sur duvarı iki yanda dış yüz olarak)
    box_uv(f"{tag}_Sur_Govde", -WALL_TOP_HALF + 0.2, WALL_TOP_HALF - 0.2, y0, y1, SEA_Z - 1.0, -0.3, WALLTOP, env, origin)
    bpy.ops.mesh.primitive_plane_add(size=1)
    sea = bpy.context.active_object; sea.name = f"{tag}_Deniz"
    sea.scale = (240, y1 - y0, 1); sea.location = (origin.x, origin.y + (y0 + y1) / 2, SEA_Z)
    bpy.ops.object.transform_apply(scale=True); sea.data.materials.append(SEA)
    for c in sea.users_collection: c.objects.unlink(sea)
    env.objects.link(sea)


def wall_faces(env, origin, rng, y0=0.0, y1=SEG):
    """İki yanda Meshy sur duvarı: üst mazgalları yolun kenarında korkuluk olur, gövdesi denize iner."""
    for s in (-1, 1):
        y = y0
        while y < y1 - 0.5:
            name = "M07_CityWall"
            h = abs(SEA_Z) + 1.3                       # mazgal 1.3 m korkuluk
            sc = h / LIB[name].dimensions.z
            rot = facade_rot(-s)                        # dış yüz denize baksın
            fx, fy = footprint(name, rot, sc)
            if y + fy > y1 + 0.3:
                o = place(name, env, origin, s * (WALL_TOP_HALF + fx / 2 - 0.6), y + (y1 - y) / 2, rot, height=h, z=SEA_Z)
                o.scale = (o.scale.x * (y1 - y) / fy, o.scale.y, o.scale.z)
                break
            place(name, env, origin, s * (WALL_TOP_HALF + fx / 2 - 0.6), y + fy / 2, rot, height=h, z=SEA_Z)
            y += fy - 0.2


SUR = [
    # ad, kule aralığı, kayık sayısı, uzak köprü, fener
    ("Sur_Ustu_Liman", 15, 6, False, True),
    ("Sur_Ustu_Kuleler", 10, 3, False, True),
    ("Sur_Ustu_Kopru_Manzarasi", 30, 4, True, True),
    ("Sur_Ustu_Balikcilar", 30, 9, False, True),
    ("Sur_Ustu_Minareli_Kiyi", 15, 4, False, False),
    ("Sur_Ustu_Gun_Batimi", 20, 5, True, True),
]
SUR_LOW = ["CrateStack", "SlideBar", "LowArch", "ClayJug"]


def build_sur(i, theme, origin, coll):
    name, tower_gap, boats, bridge, lanterns = theme
    rng = random.Random(5000 + i * 53)
    env = new_coll(f"{coll.name}_Cevre", coll); obs = new_coll(f"{coll.name}_Engel_Ornek", coll)
    sea_and_wall_body(env, origin, coll.name[:9])
    wall_faces(env, origin, rng)
    # denizden yükselen kuleler (yolun dışında, korkuluğa yaslı)
    y = tower_gap / 2
    while y < SEG:
        for s in (-1, 1):
            if rng.random() < 0.7:
                place("M08_WallTower", env, origin, s * (WALL_TOP_HALF + 3.2), y, facade_rot(-s), height=abs(SEA_Z) + 8.5, z=SEA_Z)
        y += tower_gap
    if lanterns:
        for k in range(3):
            for s in (-1, 1):
                place("M13_BridgeLantern", env, origin, s * (WALL_TOP_HALF - 0.5), 5 + k * 10 + (5 if s > 0 else 0), facade_rot(s), height=2.0)
    for b in range(boats):
        s = rng.choice((-1, 1))
        place("M15_Boat", env, origin, s * rng.uniform(12, 45), rng.uniform(0, SEG), rng.uniform(0, 360), height=rng.uniform(1.1, 1.6), z=SEA_Z - 0.35)
    if bridge:
        place("M12_BridgeArch", env, origin, rng.choice((-1, 1)) * rng.uniform(55, 75), SEG / 2, 90, height=16, z=SEA_Z - 1)
    for k in range(2):   # karşı kıyı silüeti
        s = rng.choice((-1, 1))
        place("M10_Minaret", env, origin, s * rng.uniform(70, 95), rng.uniform(0, SEG), 0, height=26, z=SEA_Z)
        place("M11_Dome", env, origin, s * rng.uniform(70, 95), rng.uniform(0, SEG), 0, height=13, z=SEA_Z, paint="Kirectasi")
    lane_obstacles(obs, origin, rng, SUR_LOW, [])


# ------------------------------------------------------------------ GEÇİŞLER
def build_gecis(i, origin, coll, to_sur):
    """Mahalle → sur (sokak sonunda kuleli sur kapısı, ardından sur üstü) ya da tersi. Kapı y=15'te."""
    rng = random.Random(9000 + i)
    env = new_coll(f"{coll.name}_Cevre", coll); obs = new_coll(f"{coll.name}_Engel_Ornek", coll)
    tag = coll.name[:9]
    a0, a1, b0, b1 = (0, 15, 15, SEG)
    street_y = (a0, a1) if to_sur else (b0, b1)
    sur_y = (b0, b1) if to_sur else (a0, a1)
    # mahalle yarısı
    box_uv(f"{tag}_Yol", -ROAD_HALF, ROAD_HALF, street_y[0], street_y[1], -0.2, 0, ROAD, env, origin)
    for s in (-1, 1):
        x0, x1 = (ROAD_HALF, WALK_OUT + 0.5) if s > 0 else (-WALK_OUT - 0.5, -ROAD_HALF)
        box_uv(f"{tag}_Kaldirim_{s}", x0, x1, street_y[0], street_y[1], -0.2, CURB_H, WALK, env, origin)
    house_row(env, origin, rng, street_y[0], street_y[1])
    sidewalk_props(env, origin, rng, ["TeaCart", "M06_StallDecor", "SimitCart"], 0.8, street_y[0], street_y[1])
    for sd in (-1, 1):
        awning(env, origin, sd, street_y[0] + 4, 2.8, mat=rng.choice(AWNINGS)); awning(env, origin, sd, street_y[0] + 10, 2.4, mat=rng.choice(AWNINGS))
    bunting(env, origin, rng, street_y[0] + 7, z=6.8)
    # sur yarısı
    sea_and_wall_body(env, origin, tag, sur_y[0], sur_y[1])
    wall_faces(env, origin, rng, sur_y[0], sur_y[1])
    # kapı: iki kule + üstte çarşı kemeri (sur kapısı)
    for s in (-1, 1):
        place("M08_WallTower", env, origin, s * (WALK_OUT + 2.2), 15, facade_rot(s), height=14)
    place("M04_BazaarArch", env, origin, 0, 15, 0, scale=(2 * WALK_OUT + 1.0) / LIB["M04_BazaarArch"].dimensions.x, paint="Bazalt")
    for b in range(4):
        s = rng.choice((-1, 1))
        place("M15_Boat", env, origin, s * rng.uniform(14, 40), rng.uniform(sur_y[0], sur_y[1]), rng.uniform(0, 360), height=1.3, z=SEA_Z - 0.35)
    lane_obstacles(obs, origin, rng, ["CrateStack", "SlideBar", "LowArch"], [])


# ------------------------------------------------------------------ PORTAL EVRENİ (gece çatılar)
PORTAL = [("Portal_Ay_Isigi", 0.5), ("Portal_Fenerli_Catilar", 0.9), ("Portal_Kubbeler", 0.6),
          ("Portal_Minare_Siluetleri", 0.5), ("Portal_Isik_Zincirleri", 1.0), ("Portal_Sisli_Catilar", 0.6)]
NIGHT_LAMPS = []


def build_portal(i, theme, origin, coll):
    name, lights = theme
    rng = random.Random(7000 + i * 41)
    env = new_coll(f"{coll.name}_Cevre", coll); obs = new_coll(f"{coll.name}_Engel_Ornek", coll)
    tag = coll.name[:9]
    box_uv(f"{tag}_Cati_Yolu", -ROAD_HALF - 0.6, ROAD_HALF + 0.6, 0, SEG, -0.4, 0, ROOF, env, origin)
    # yolun iki yanında aşağıda kalan çatı blokları (çatıların üstünde koşuyoruz) — koyu bazalt
    for s in (-1, 1):
        y = 0.0
        while y < SEG - 0.5:
            h = rng.uniform(6.5, 9.0)
            sc = h / LIB["M09_RooftopBlock"].dimensions.z
            fx, fy = footprint("M09_RooftopBlock", facade_rot(s), sc)
            if y + fy > SEG + 0.3: sc *= (SEG - y) / fy; fx, fy = footprint("M09_RooftopBlock", facade_rot(s), sc); h = sc * LIB["M09_RooftopBlock"].dimensions.z
            place("M09_RooftopBlock", env, origin, s * (ROAD_HALF + 0.6 + fx / 2), y + fy / 2, facade_rot(s), height=h, z=-h + rng.uniform(1.0, 2.5))
            y += fy - 0.1
    for k in range(3):
        s = rng.choice((-1, 1))
        place("M11_Dome", env, origin, s * rng.uniform(13, 22), rng.uniform(0, SEG), rng.uniform(0, 360), height=rng.uniform(7, 10), z=-3)
        place("M10_Minaret", env, origin, -s * rng.uniform(16, 30), rng.uniform(0, SEG), 0, height=rng.uniform(16, 22), z=-5)
    if lights > 0.7:
        for k in range(2):
            o = place("M05_StringLights", env, origin, 0, 8 + k * 14, 0, z=4.0)
            if o: sc = (2 * ROAD_HALF + 2.5) / LIB["M05_StringLights"].dimensions.x; o.scale = (sc, sc, sc)
    for k in range(int(4 * lights) + 2):   # sıcak fener ışıkları
        s = (-1) ** k
        yy = 3 + k * (SEG - 6) / max(1, int(4 * lights) + 1)
        place("M13_BridgeLantern", env, origin, s * (ROAD_HALF + 0.3), yy, facade_rot(s), height=2.0)
        L = bpy.data.objects.new(f"{tag}_Fener_Isik_{k}", bpy.data.lights.new(f"{tag}_Fener_{k}", "POINT"))
        L.data.energy = 220; L.data.color = (1.0, 0.65, 0.3); L.data.shadow_soft_size = 0.3
        L.location = (origin.x + s * (ROAD_HALF + 0.3), origin.y + yy, 2.1); env.objects.link(L); NIGHT_LAMPS.append(L)
    lane_obstacles(obs, origin, rng, ["CrateStack", "SlideBar", "LowArch", "ClayJug"], [])


# ------------------------------------------------------------------ yerleşim: bölge başına bir satır
MAPS = []   # (koleksiyon, köken, bölge)
col_gap, row_gap = 70.0, 60.0
zones_root = {z: new_coll(z) for z in ("1_MAHALLE", "2_GECIS", "3_SUR_USTU_DENIZ", "4_PORTAL_EVRENI")}
n = 1
for k, th in enumerate(MAHALLE):
    o = Vector(((k % 6) * col_gap, (k // 6) * row_gap, 0))
    c = new_coll(f"Harita_{n:02d}_Mahalle_{th[0]}", zones_root["1_MAHALLE"]); build_mahalle(k, th, o, c); MAPS.append((c, o, "gun")); n += 1
for k, to_sur in enumerate((True, False)):
    o = Vector((k * col_gap, 2 * row_gap, 0))
    c = new_coll(f"Harita_{n:02d}_Gecis_{'Mahalle_Sur' if to_sur else 'Sur_Mahalle'}", zones_root["2_GECIS"]); build_gecis(k, o, c, to_sur); MAPS.append((c, o, "gun")); n += 1
for k, th in enumerate(SUR):
    o = Vector(((k % 6) * col_gap * 1.6, 3 * row_gap + 40, 0))
    c = new_coll(f"Harita_{n:02d}_{th[0]}", zones_root["3_SUR_USTU_DENIZ"]); build_sur(k, th, o, c); MAPS.append((c, o, "deniz")); n += 1
for k, th in enumerate(PORTAL):
    o = Vector(((k % 6) * col_gap, 5 * row_gap + 60, 0))
    c = new_coll(f"Harita_{n:02d}_{th[0]}", zones_root["4_PORTAL_EVRENI"]); build_portal(k, th, o, c); MAPS.append((c, o, "gece")); n += 1
print("MAPS", len(MAPS))

# ------------------------------------------------------------------ ışık + render
world = bpy.data.worlds.new("Gok"); scene.world = world; world.use_nodes = True
wnt = world.node_tree; bg = wnt.nodes.get("Background")
sky = wnt.nodes.new("ShaderNodeTexSky"); wnt.links.new(sky.outputs["Color"], bg.inputs["Color"])
sun = bpy.data.objects.new("Gunes", bpy.data.lights.new("Gunes", "SUN")); scene.collection.objects.link(sun)
try: scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError: scene.render.engine = "BLENDER_EEVEE"
try: scene.view_settings.view_transform = "AgX"; scene.view_settings.look = "AgX - Medium High Contrast"
except Exception: pass
cam = bpy.data.objects.new("Kamera", bpy.data.cameras.new("Kamera")); scene.collection.objects.link(cam); scene.camera = cam
cam.data.lens = 24; cam.data.clip_end = 1000


def mood(kind):
    if kind == "gece":   # karanlık ama abartısız: lacivert gök, mavi ay ışığı, sıcak fenerler
        wnt.links.clear() if False else None
        bg.inputs["Color"].default_value = (0.05, 0.08, 0.18, 1)
        for l in list(wnt.links):
            if l.to_socket == bg.inputs["Color"]: wnt.links.remove(l)
        bg.inputs["Strength"].default_value = 0.5
        sun.data.energy = 1.1; sun.data.color = (0.6, 0.72, 1.0); sun.rotation_euler = (math.radians(55), 0, math.radians(40))
        scene.view_settings.exposure = -0.2
        for L in NIGHT_LAMPS: L.hide_render = False
    else:
        if not any(l.to_socket == bg.inputs["Color"] for l in wnt.links): wnt.links.new(sky.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 0.35
        sun.data.energy = 3.2 if kind == "gun" else 3.6; sun.data.color = (1.0, 0.9, 0.75)
        sun.rotation_euler = (math.radians(52), math.radians(8), math.radians(-35))
        scene.view_settings.exposure = -1.0
        for L in NIGHT_LAMPS: L.hide_render = True


scene.render.resolution_x, scene.render.resolution_y = 960, 540
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "JumplineGo_Proje.blend"))
for c, o, kind in MAPS:
    mood(kind)
    if kind == "deniz":   # sur üstü: denizi ve kayıkları görecek kadar yüksek, hafif yandan
        cam.location = o + Vector((5, -9, 7)); target = o + Vector((-1, SEG * 0.6, -1))
    else:
        cam.location = o + Vector((0, -6, 6.5)); target = o + Vector((0, SEG * 0.6, 1.5))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(PREV, c.name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDER", c.name)
mood("gun")
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "JumplineGo_Proje.blend"))
print("MAPS_DONE")
