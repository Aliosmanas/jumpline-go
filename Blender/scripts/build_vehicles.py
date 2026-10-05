"""
Yeni engeller (Diyarbakır sokağı): araçlar, sur kapısı, dağılan kasa yığını. Kenarlar pahlı (Bevel) — oyunda düzgün
ve dolu görünsün diye.

  Taxi.fbx      sarı Şahin taksi (üstüne zıplanabilir: kaput 0.95, tavan 1.45, bagaj 0.95)
  Minibus.fbx   beyaz-mavi dolmuş (yüksek, üstüne çıkılmaz → şerit değiştir)
  SurGate.fbx   bazalt sur kapısı: iki burç + yolun üstünde alçak geçit (alt kenar 3.7 m, kırmızı neon uyarı şeridi)
                → kamyon tavanındaki koşucu kafasını çarpar
  CrateStack.fbx üç ayrı sandık (Crate_0..2) — çarpınca ayrı ayrı yere savrulur (Unity Breakable)

Koordinatlar "Unity gibi": x yanal, h yukarı, z ileri (koşucu -z'den gelir). Blender'a (−x, −z, h).
Malzeme adları RampTruck ile ortak (T_*); Unity hepsini Obstacles/Materials'a bağlar.
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
TEX = os.path.join(ROOT, "Blender", "textures")
OUT = os.path.join(ROOT, "Assets", "Art", "Environment", "Obstacles")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "vehicles.blend")

MATS = {
    "T_Paint": ("#3F7F7A", None, 0), "T_PaintRed": ("#8E1748", None, 0), "T_Wood": ("#FFFFFF", "wood.png", 0),
    "T_Metal": ("#2A2C30", None, 0), "T_Glass": ("#0E1620", None, 0), "T_Neon": ("#55CFFF", None, 10),
    "T_Tire": ("#141414", None, 0), "T_Lamp": ("#FFE6A0", None, 8), "T_Rust": ("#7A4A32", None, 0),
    "T_Yellow": ("#F2B705", None, 0), "T_White": ("#E9E6DF", None, 0), "T_Blue": ("#1F4E9C", None, 0),
    "T_Black": ("#111111", None, 0), "T_Basalt": ("#FFFFFF", "basalt.png", 0), "T_Trim": ("#B8B4AC", "basalt.png", 0),
    "T_NeonRed": ("#FF3048", None, 8), "T_TailLight": ("#D01020", None, 4), "T_Chrome": ("#C8CCD0", None, 0),
}


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def make_materials():
    for name, (col, tex, glow) in MATS.items():
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = srgb(col)
        bsdf.inputs["Roughness"].default_value = 0.35 if name in ("T_Yellow", "T_White", "T_Blue", "T_Chrome") else 0.7
        if name == "T_Chrome":
            bsdf.inputs["Metallic"].default_value = 0.9
        if tex:
            node = nt.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
            nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        if glow:
            bsdf.inputs["Emission Color"].default_value = srgb(col)
            bsdf.inputs["Emission Strength"].default_value = glow
        m.diffuse_color = srgb(col)


def P(x, h, z):
    return Vector((-x, -z, h))


def _obj_from_bm(bm, name, mat, bevel):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bpy.data.materials[mat])
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    if bevel > 0:
        md = o.modifiers.new("Bevel", "BEVEL")
        md.width = bevel
        md.segments = 2
        md.limit_method = "ANGLE"
    return o


def box(parts, x0, x1, h0, h1, z0, z1, mat, bevel=0.0):
    bm = bmesh.new()
    xa, xb = sorted((x0, x1))
    za, zb = sorted((z0, z1))
    v = [bm.verts.new(P(x, h, z)) for h in (h0, h1) for z in (za, zb) for x in (xa, xb)]
    for idx in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        bm.faces.new([v[i] for i in idx])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(_obj_from_bm(bm, "p", mat, bevel))


def loft(parts, x_half_bottom, x_half_top, profile_bottom, profile_top, mat, bevel=0.0):
    """Alt ve üst dörtgenden (z0,z1,h) kesik piramit (kabin camı, ön cam eğimi)."""
    bm = bmesh.new()
    (bz0, bz1, bh), (tz0, tz1, th) = profile_bottom, profile_top
    b = [bm.verts.new(P(x, bh, z)) for z, x in ((bz0, -x_half_bottom), (bz0, x_half_bottom), (bz1, x_half_bottom), (bz1, -x_half_bottom))]
    t = [bm.verts.new(P(x, th, z)) for z, x in ((tz0, -x_half_top), (tz0, x_half_top), (tz1, x_half_top), (tz1, -x_half_top))]
    bm.faces.new(list(reversed(b)))
    bm.faces.new(t)
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((b[i], b[j], t[j], t[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(_obj_from_bm(bm, "p", mat, bevel))


def wheel(parts, x, z, r, w):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=18, radius1=r, radius2=r, depth=w)
    bmesh.ops.rotate(bm, verts=bm.verts, cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(math.pi / 2, 3, "Y"))
    bmesh.ops.translate(bm, verts=bm.verts, vec=P(x, r, z))
    parts.append(_obj_from_bm(bm, "p", "T_Tire", 0.03))
    s = 1 if x > 0 else -1
    box(parts, x + s * (w / 2), x + s * (w / 2 + 0.02), r - r * 0.55, r + r * 0.55, z - r * 0.55, z + r * 0.55, "T_Chrome")


def finish(parts, name):
    """Parçaları değiştiricileri uygulayarak tek objede birleştirir."""
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    o.data.name = name
    for p in o.data.polygons:
        p.use_smooth = False
    # kutu izdüşümü UV (dokulu malzemeler için)
    bm = bmesh.new()
    bm.from_mesh(o.data)
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            q = loop.vert.co
            loop[uv].uv = (q.y / 2, q.z / 2) if ax == 0 else (q.x / 2, q.z / 2) if ax == 1 else (q.x / 2, q.y / 2)
    bm.to_mesh(o.data)
    bm.free()
    return o


# ---------------------------------------------------------------------------
def taxi():
    p = []
    L, W = 2.15, 0.84          # yarı boy / yarı en; ön -z'de (farlar koşucuya bakar)
    box(p, -W, W, 0.32, 0.92, -L, L, "T_Yellow", 0.09)                         # gövde
    loft(p, 0.76, 0.66, (-0.95, 1.30, 0.92), (-0.42, 0.86, 1.40), "T_Glass", 0.02)   # kabin camı
    box(p, -0.67, 0.67, 1.38, 1.45, -0.44, 0.88, "T_Yellow", 0.03)             # tavan
    for s in (-1, 1):                                                          # kapı direkleri
        box(p, s * 0.70, s * 0.74, 0.92, 1.40, 0.22, 0.30, "T_Yellow")
    for k in range(14):                                                         # dama şerit
        z = -1.9 + k * 0.27
        for s in (-1, 1):
            box(p, s * W, s * (W + 0.012), 0.60, 0.70, z, z + 0.135, "T_Black" if k % 2 == 0 else "T_White")
    box(p, -0.26, 0.26, 1.45, 1.62, 0.05, 0.30, "T_Lamp", 0.02)               # TAKSİ lambası
    for z0, z1 in ((-L - 0.06, -L + 0.12), (L - 0.12, L + 0.06)):              # tamponlar
        box(p, -W - 0.02, W + 0.02, 0.28, 0.46, z0, z1, "T_Chrome", 0.03)
    box(p, -0.5, 0.5, 0.52, 0.78, -L - 0.01, -L + 0.02, "T_Metal")             # ızgara
    for s in (-1, 1):
        box(p, s * 0.55 - 0.14, s * 0.55 + 0.14, 0.56, 0.76, -L - 0.02, -L + 0.02, "T_Lamp", 0.02)    # farlar
        box(p, s * 0.58 - 0.16, s * 0.58 + 0.16, 0.62, 0.82, L - 0.02, L + 0.02, "T_TailLight", 0.02)  # stoplar
        box(p, s * 0.80, s * 0.92, 0.95, 1.05, -0.95, -0.82, "T_Yellow", 0.01)                         # ayna
    box(p, -0.28, 0.28, 0.40, 0.52, -L - 0.07, -L - 0.05, "T_White")           # plaka
    for z in (-1.35, 1.35):
        for s in (-1, 1):
            wheel(p, s * 0.76, z, 0.32, 0.22)
    return finish(p, "Taxi")


def minibus():
    p = []
    W = 0.98
    box(p, -W, W, 0.35, 2.35, -2.2, 2.8, "T_White", 0.12)                      # kasa
    box(p, -W, W, 0.35, 1.15, -2.85, -2.2, "T_White", 0.10)                    # burun
    loft(p, W - 0.04, W - 0.08, (-2.75, -2.2, 1.15), (-2.25, -2.2, 2.25), "T_Glass")   # ön cam (eğik)
    for s in (-1, 1):
        box(p, s * W, s * (W + 0.012), 1.0, 1.14, -2.8, 2.75, "T_Blue")       # mavi şerit
        for k in range(5):                                                     # yan camlar
            z = -1.95 + k * 0.92
            box(p, s * W, s * (W + 0.015), 1.35, 2.02, z, z + 0.78, "T_Glass")
        box(p, s * (W + 0.02), s * (W + 0.18), 1.35, 1.6, -2.3, -2.15, "T_Black")    # ayna
    box(p, -0.62, 0.62, 2.35, 2.62, -2.1, -1.6, "T_Lamp", 0.03)               # DOLMUŞ tabelası
    for s in (-1, 1):                                                          # tavan bagajı
        box(p, s * 0.82, s * 0.88, 2.35, 2.5, -1.2, 2.5, "T_Chrome")
    for z, c, h in ((-0.8, "T_PaintRed", 0.35), (0.2, "T_Blue", 0.45), (1.3, "T_Rust", 0.3)):
        box(p, -0.6, 0.55, 2.38, 2.38 + h, z, z + 0.8, c, 0.06)               # yükler
    box(p, -W - 0.02, W + 0.02, 0.3, 0.5, -2.95, -2.82, "T_Chrome", 0.03)
    box(p, -W - 0.02, W + 0.02, 0.3, 0.5, 2.78, 2.9, "T_Chrome", 0.03)
    box(p, -0.55, 0.55, 0.6, 0.95, -2.86, -2.84, "T_Metal")                    # ızgara
    for s in (-1, 1):
        box(p, s * 0.72 - 0.15, s * 0.72 + 0.15, 0.7, 0.92, -2.87, -2.84, "T_Lamp", 0.02)
        box(p, s * 0.8 - 0.1, s * 0.8 + 0.1, 0.9, 1.4, 2.8, 2.83, "T_TailLight")
    for z in (-1.9, 1.9):
        for s in (-1, 1):
            wheel(p, s * 0.86, z, 0.36, 0.26)
    return finish(p, "Minibus")


def sur_gate():
    p = []
    for s in (-1, 1):                                                          # burçlar
        x0, x1 = s * 3.65, s * 5.2
        box(p, x0, x1, 0.0, 7.4, -1.0, 1.0, "T_Basalt", 0.04)
        box(p, x0 - s * 0.1, x1 + s * 0.1, 0.0, 0.5, -1.1, 1.1, "T_Trim")      # taban
        box(p, x0 - s * 0.08, x1 + s * 0.08, 4.4, 4.55, -1.08, 1.08, "T_Trim")  # kat bandı
        for k in range(3):                                                     # burç mazgalları
            xx = x0 + s * (0.2 + k * 0.5)
            box(p, xx, xx + s * 0.32, 7.4, 7.95, -1.0, 1.0, "T_Basalt")
        box(p, s * 4.3, s * 4.55, 5.6, 6.3, -1.02, -0.98, "T_Black")          # okçu deliği
    box(p, -3.65, 3.65, 3.7, 6.6, -0.75, 0.75, "T_Basalt", 0.03)              # geçit duvarı (alt kenar 3.7)
    box(p, -3.7, 3.7, 3.62, 3.72, -0.8, 0.8, "T_Trim")                         # alt söve
    box(p, -3.6, 3.6, 3.72, 3.8, -0.81, -0.77, "T_NeonRed")                    # uyarı şeridi (ön)
    box(p, -3.6, 3.6, 3.72, 3.8, 0.77, 0.81, "T_NeonRed")                      # (arka)
    for k in range(-3, 4):                                                     # kemer taşları (ön yüz)
        x = k * 1.0
        box(p, x - 0.42, x + 0.42, 3.8, 4.5, -0.8, -0.74, "T_Trim" if k % 2 == 0 else "T_Basalt")
    box(p, -1.0, 1.0, 5.0, 5.8, -0.8, -0.76, "T_Trim", 0.02)                   # kitabe
    k = -3.5
    while k < 3.6:                                                             # mazgallar
        box(p, k, k + 0.45, 6.6, 7.1, -0.75, 0.75, "T_Basalt")
        k += 0.8
    return finish(p, "SurGate")


def crate_stack():
    """Üç ayrı sandık: Crate_0, Crate_1 (alt), Crate_2 (üst, hafif dönük)."""
    objs = []
    specs = [(-0.41, 0.0, 0.0), (0.41, 0.0, 0.0), (0.0, 0.8, 12.0)]
    for i, (x, h0, rot) in enumerate(specs):
        p = []
        s = 0.39
        box(p, -s, s, 0.0, 2 * s, -s, s, "T_Wood", 0.025)
        for hh in (0.18, 0.4, 0.62):                                          # tahta aralıkları
            for zz in (-s - 0.005, s + 0.005):
                box(p, -s + 0.04, s - 0.04, hh, hh + 0.025, zz - 0.004, zz + 0.004, "T_Rust")
        for cx in (-s, s):                                                     # köşe demirleri
            for cz in (-s, s):
                box(p, cx - 0.04, cx + 0.04, 0.0, 2 * s, cz - 0.04, cz + 0.04, "T_Metal")
        if i == 2:
            box(p, -0.25, 0.25, 0.32, 0.42, -s - 0.012, -s - 0.004, "T_Neon")   # JUMPLINE GO etiket şeridi
        o = finish(p, f"Crate_{i}")
        o.location = P(x, h0, 0.0)
        o.rotation_euler = (0, 0, math.radians(rot))
        objs.append(o)
    return objs


def export(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + ".fbx"), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_materials()
    os.makedirs(OUT, exist_ok=True)
    t = taxi()
    export([t], "Taxi")
    m = minibus()
    export([m], "Minibus")
    g = sur_gate()
    export([g], "SurGate")
    c = crate_stack()
    export(c, "CrateStack")
    # blend dosyasında yan yana dursunlar (dışa aktarımdan sonra)
    t.location.x -= 4
    m.location.x -= 8
    for o in c:
        o.location.x -= 12
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("VEHICLES_OK", [(o.name, len(o.data.polygons)) for o in [t, m, g] + c])


main()
