"""
Rampalı kamyonet (engel) — üstüne rampadan çıkılır, kasa tavanında koşulur, kabin tavanına inilir, önden yere atlanır.
Diyarbakır sokak teması: eski usul kasalı kamyonet, solgun petrol yeşili boya + bordo şerit, arkada ahşap rampa,
kenarlarda mavi neon şeritler (dekorlardaki neon detay dili).

Koordinatlar "Unity gibi": x yanal, h yukarı, z ileri (rampa başı z=0, koşucu -z'den gelir). Blender'a (−x, −z, h).
Yüzey profili Unity Obstacle segmentleriyle aynı (GameplaySceneBuilder.RampTruckSegments):
  z 0–4    rampa 0.05 → 2.35
  z 4–9.5  kasa tavanı 2.35
  z 9.5–10.8 kabin tavanı 1.9
  z 10.8–11.6 kaput 1.25
Genişlik ±0.95. Çıktı: Assets/Art/Environment/Obstacles/RampTruck.fbx, Blender/source/obstacles.blend
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
TEX = os.path.join(ROOT, "Blender", "textures")
FBX_PATH = os.path.join(ROOT, "Assets", "Art", "Environment", "Obstacles", "RampTruck.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "obstacles.blend")

HW = 0.95
RAMP_L, BOX_END, CAB_END, HOOD_END = 4.0, 9.5, 10.8, 11.6
ROOF_H, CAB_H, HOOD_H, CHASSIS = 2.35, 1.9, 1.25, 0.55

MATS = [("T_Paint", "#3F7F7A", None, 0.0), ("T_PaintRed", "#8E1748", None, 0.0), ("T_Wood", "#FFFFFF", "wood.png", 0.0),
        ("T_Metal", "#2A2C30", None, 0.0), ("T_Glass", "#0E1620", None, 0.0), ("T_Neon", "#55CFFF", None, 10.0),
        ("T_Tire", "#141414", None, 0.0), ("T_Lamp", "#FFE6A0", None, 8.0), ("T_Rust", "#7A4A32", None, 0.0)]
PAINT, RED, WOOD, METAL, GLASS, NEON, TIRE, LAMP, RUST = range(len(MATS))


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def make_materials():
    out = []
    for name, col, tex, glow in MATS:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        bsdf.inputs["Base Color"].default_value = srgb(col)
        bsdf.inputs["Roughness"].default_value = 0.6
        if tex:
            node = nt.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
            nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        if glow:
            bsdf.inputs["Emission Color"].default_value = srgb(col)
            bsdf.inputs["Emission Strength"].default_value = glow
        m.diffuse_color = srgb(col)
        out.append(m)
    return out


bm = bmesh.new()


def P(x, h, z):
    return Vector((-x, -z, h))


def face(vs, mat):
    f = bm.faces.new(vs)
    f.material_index = mat
    return f


def box(x0, x1, h0, h1, z0, z1, mat):
    v = [bm.verts.new(P(x, h, z)) for h in (h0, h1) for z in (z0, z1) for x in (x0, x1)]
    for idx in ((0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        face([v[i] for i in idx], mat)


def slab(x0, x1, z0, h0, z1, h1, t, mat):
    """Eğik levha (rampa): z0'da h0, z1'de h1 yüksekliğinde, t kalınlığında."""
    pts = [(z0, h0), (z1, h1), (z1, h1 - t), (z0, h0 - t)]
    a = [bm.verts.new(P(x0, h, z)) for z, h in pts]
    b = [bm.verts.new(P(x1, h, z)) for z, h in pts]
    face(a, mat)
    face(list(reversed(b)), mat)
    for i in range(4):
        j = (i + 1) % 4
        face((a[i], a[j], b[j], b[i]), mat)


def wheel(x, z, r=0.42, w=0.28, segs=14):
    ring_a, ring_b = [], []
    for k in range(segs):
        a = 2 * math.pi * k / segs
        h, zz = r + r * math.sin(a), z + r * math.cos(a)
        ring_a.append(bm.verts.new(P(x - w / 2, h, zz)))
        ring_b.append(bm.verts.new(P(x + w / 2, h, zz)))
    face(ring_a, TIRE)
    face(list(reversed(ring_b)), TIRE)
    for k in range(segs):
        j = (k + 1) % segs
        face((ring_a[k], ring_a[j], ring_b[j], ring_b[k]), TIRE)
    hub_x = x + (w / 2 + 0.01) * (1 if x > 0 else -1)
    box(min(hub_x, hub_x + 0.02 * (1 if x > 0 else -1)), max(hub_x, hub_x + 0.02 * (1 if x > 0 else -1)),
        r - 0.18, r + 0.18, z - 0.18, z + 0.18, METAL)


def build():
    # şasi + tamponlar
    box(-HW + 0.1, HW - 0.1, 0.35, CHASSIS, RAMP_L - 0.2, HOOD_END - 0.1, METAL)
    box(-HW, HW, 0.3, 0.5, HOOD_END - 0.15, HOOD_END + 0.1, METAL)       # ön tampon
    # kasa (kutu): yan duvarlar boyalı, tavan ahşap döşeme (üstünde koşulur)
    box(-HW, HW, CHASSIS, ROOF_H - 0.08, RAMP_L, BOX_END, PAINT)
    box(-HW - 0.02, HW + 0.02, ROOF_H - 0.08, ROOF_H, RAMP_L, BOX_END, WOOD)
    for s in (-1, 1):
        x0, x1 = sorted((s * HW, s * (HW + 0.02)))
        box(x0, x1, 1.2, 1.45, RAMP_L + 0.2, BOX_END - 0.2, RED)            # bordo şerit
        box(x0, x1, ROOF_H - 0.05, ROOF_H + 0.04, RAMP_L, BOX_END, NEON)    # tavan kenarı neon
        for z in (RAMP_L + 1.5, RAMP_L + 3.5):                                # perçin kuşakları
            box(x0, x1, CHASSIS, ROOF_H - 0.1, z - 0.04, z + 0.04, METAL)
    # açık arka kapılar (iki yana açılmış)
    for s in (-1, 1):
        box(*sorted((s * HW, s * (HW + 0.05))), CHASSIS, ROOF_H - 0.1, RAMP_L - 0.9, RAMP_L, PAINT)
    # rampa: ahşap kalaslar + yan korkuluk + neon kenar
    slab(-0.85, 0.85, 0.0, 0.05, RAMP_L, ROOF_H, 0.1, WOOD)
    n = 9
    for k in range(1, n):   # kaymaz çıtalar
        z = RAMP_L * k / n
        h = 0.05 + (ROOF_H - 0.05) * k / n
        box(-0.82, 0.82, h, h + 0.04, z - 0.04, z + 0.04, WOOD)
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.85, s * 0.92))
        slab(x0, x1, 0.0, 0.18, RAMP_L, ROOF_H + 0.12, 0.16, NEON)
        # rampa ayağı
        box(*sorted((s * 0.7, s * 0.78)), 0.0, ROOF_H * 0.5, RAMP_L * 0.5 - 0.05, RAMP_L * 0.5 + 0.05, RUST)
    # kabin + kaput + camlar + farlar
    box(-HW, HW, CHASSIS, CAB_H - 0.06, BOX_END, CAB_END, PAINT)
    box(-HW - 0.02, HW + 0.02, CAB_H - 0.06, CAB_H, BOX_END, CAB_END, METAL)
    box(-HW + 0.08, HW - 0.08, 1.15, CAB_H - 0.12, CAB_END - 0.02, CAB_END + 0.01, GLASS)    # ön cam
    for s in (-1, 1):
        x0, x1 = sorted((s * HW, s * (HW + 0.01)))
        box(x0, x1, 1.15, CAB_H - 0.15, BOX_END + 0.2, CAB_END - 0.15, GLASS)               # yan cam
    box(-HW, HW, CHASSIS, HOOD_H, CAB_END, HOOD_END, PAINT)
    box(-HW + 0.15, HW - 0.15, 0.62, 1.05, HOOD_END - 0.01, HOOD_END + 0.01, METAL)          # ızgara
    for s in (-1, 1):
        box(s * 0.68 - 0.13, s * 0.68 + 0.13, 0.8, 1.02, HOOD_END - 0.005, HOOD_END + 0.03, LAMP)
    # tekerlekler
    for z in (RAMP_L + 1.0, BOX_END - 0.8, HOOD_END - 0.65):
        for s in (-1, 1):
            wheel(s * (HW - 0.12), z)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = make_materials()
    build()
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            p = loop.vert.co
            loop[uv].uv = (p.y / 2, p.z / 2) if ax == 0 else (p.x / 2, p.z / 2) if ax == 1 else (p.x / 2, p.y / 2)
    me = bpy.data.meshes.new("RampTruck")
    bm.to_mesh(me)
    for m in mats:
        me.materials.append(m)
    obj = bpy.data.objects.new("RampTruck", me)
    bpy.context.collection.objects.link(obj)
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("TRUCK_OK", len(me.polygons))


main()
