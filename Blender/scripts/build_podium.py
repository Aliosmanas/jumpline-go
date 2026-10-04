"""
Karakter platformu (podyum) — docs/references/menu/podium.jpg

Üç katlı koyu metal disk: alt geniş disk (yan yüzünde ışık bandı), orta basamak, üst disk (üstünde ışıklı halka,
ortası parlak iç yüzey). Üstten yukarı yayılan şeffaf ışık hüzmesi ayrı mesh (Podium_Beam).
Çalıştır: MCP ile exec ya da `blender -b -P Blender/scripts/build_podium.py`
Çıktı:    Assets/Art/Environment/Podium/Podium.fbx (+ Blender/source/podium.blend)
Malzemeler: P_Metal, P_MetalDark, P_Top, P_Glow, P_Beam (Unity'de adla ayarlanır)
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
FBX_PATH = os.path.join(ROOT, "Assets", "Art", "Environment", "Podium", "Podium.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "podium.blend")

SEG = 96                  # daire çözünürlüğü
BASE_R, BASE_H = 0.78, 0.075
MID_R, MID_H = 0.70, 0.045
TOP_R, TOP_H = 0.62, 0.045
RING_IN, RING_OUT = 0.42, 0.50    # üstteki ışıklı halka
BAND_Z = (0.028, 0.042)           # alt diskin yan ışık bandı (alt kenardan)
BEAM_H = 1.6


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def material(name, color, metal, rough, emission=None, alpha=1.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(color)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if emission:
        b.inputs["Emission Color"].default_value = srgb(emission[0])
        b.inputs["Emission Strength"].default_value = emission[1]
    b.inputs["Alpha"].default_value = alpha
    m.diffuse_color = srgb(color)
    return m


MATS = ["P_Metal", "P_MetalDark", "P_Top", "P_Glow", "P_Beam"]
METAL, DARK, TOP, GLOW, BEAM = range(5)


def make_materials():
    material("P_Metal", "#3A4052", 0.9, 0.28)
    material("P_MetalDark", "#1C2030", 0.85, 0.35)
    material("P_Top", "#4A5470", 0.6, 0.12)
    material("P_Glow", "#8CC8FF", 0.0, 0.3, ("#7FC0FF", 8.0))
    material("P_Beam", "#7FB4FF", 0.0, 1.0, ("#7FB4FF", 2.0), alpha=0.25)


def ring_verts(bm, r, z):
    return [bm.verts.new((r * math.cos(2 * math.pi * i / SEG), r * math.sin(2 * math.pi * i / SEG), z)) for i in range(SEG)]


def bridge(bm, a, b, mat):
    faces = []
    for i in range(SEG):
        j = (i + 1) % SEG
        f = bm.faces.new((a[i], a[j], b[j], b[i]))
        f.material_index = mat
        faces.append(f)
    return faces


def cap(bm, ring, z, mat, flip=False):
    c = bm.verts.new((0, 0, z))
    for i in range(SEG):
        j = (i + 1) % SEG
        f = bm.faces.new((c, ring[j], ring[i]) if not flip else (c, ring[i], ring[j]))
        f.material_index = mat


def profile_solid(bm, profile, mat_of):
    """profile: [(r, z), ...] dıştan içe/aşağıdan yukarı sıralı yüzey; her segment ayrı malzeme."""
    rings = [ring_verts(bm, r, z) for r, z in profile]
    for k in range(len(rings) - 1):
        bridge(bm, rings[k], rings[k + 1], mat_of(k))
    return rings


def build_podium():
    bm = bmesh.new()
    eps = 0.006  # pah
    z0, z1 = 0.0, BASE_H
    z2, z3 = z1 + MID_H, z1 + MID_H + TOP_H
    band0, band1 = BAND_Z
    # Dış profil: alttan üste (r, z). Her basamakta küçük pah; alt diskin yanında ışık bandı girintisi.
    prof = [
        (BASE_R - 0.03, z0),                 # taban altı kenarı
        (BASE_R, z0 + eps),
        (BASE_R, band0),
        (BASE_R - 0.004, band0),             # bant girintisi
        (BASE_R - 0.004, band1),
        (BASE_R, band1),
        (BASE_R, z1 - eps),
        (BASE_R - eps, z1),                  # alt disk üst pahı
        (MID_R + 0.01, z1),                  # alt disk üst yüzeyi (basamak)
        (MID_R, z1 + eps * 0.5),
        (MID_R, z2 - eps),
        (MID_R - eps, z2),
        (TOP_R + 0.01, z2),                  # orta basamak üst yüzeyi
        (TOP_R, z2 + eps * 0.5),
        (TOP_R, z3 - eps),
        (TOP_R - eps, z3),
        (RING_OUT + 0.012, z3),              # üst yüzey dış kısmı
        (RING_OUT, z3 - 0.004),              # ışıklı halka (hafif gömük)
        (RING_IN, z3 - 0.004),
        (RING_IN - 0.012, z3),
        (RING_IN - 0.03, z3 - 0.008),        # iç yüzey hafif çukur
    ]
    glow_segments = {3, 17}                  # bant + halka
    dark_segments = {0, 8, 12}               # alt kenar ve basamak üstleri koyu
    top_segments = {19}

    def mat_of(k):
        if k in glow_segments:
            return GLOW
        if k in dark_segments:
            return DARK
        if k in top_segments:
            return TOP
        return METAL

    rings = profile_solid(bm, prof, mat_of)
    cap(bm, rings[-1], z3 - 0.008, TOP)       # iç yüzey
    cap(bm, rings[0], z0, DARK, flip=True)    # taban

    # Orta basamağın yanında ince ışık şeridi
    add_band(bm, MID_R + 0.001, z1 + MID_H * 0.45, 0.006, GLOW)
    # Üst diskin yanında kesik ışık noktaları (detay)
    add_dots(bm, TOP_R + 0.002, z2 + TOP_H * 0.5, 36, 0.035, 0.008, GLOW)
    # Alt diskin üst yüzeyinde radyal panel çizgileri
    add_grooves(bm, (MID_R + 0.02, BASE_R - 0.015), z1 + 0.0008, 24, 0.004, DARK)

    bm.normal_update()
    me = bpy.data.meshes.new("Podium")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Podium", me)
    bpy.context.collection.objects.link(obj)
    for n in MATS:
        me.materials.append(bpy.data.materials[n])
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(35))
    return obj


def add_band(bm, r, z, h, mat):
    a = ring_verts(bm, r, z - h / 2)
    b = ring_verts(bm, r, z + h / 2)
    bridge(bm, a, b, mat)


def add_dots(bm, r, z, count, w, h, mat):
    for i in range(count):
        a = 2 * math.pi * i / count
        d = Vector((math.cos(a), math.sin(a), 0))
        t = Vector((-math.sin(a), math.cos(a), 0))
        c = d * r + Vector((0, 0, z))
        vs = [bm.verts.new(c + t * sx * w / 2 + Vector((0, 0, sz * h / 2))) for sx, sz in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        bm.faces.new(vs).material_index = mat


def add_grooves(bm, r_range, z, count, w, mat):
    for i in range(count):
        a = 2 * math.pi * (i + 0.5) / count
        d = Vector((math.cos(a), math.sin(a), 0))
        t = Vector((-math.sin(a), math.cos(a), 0))
        p0, p1 = d * r_range[0], d * r_range[1]
        vs = [bm.verts.new(p + t * s * w / 2 + Vector((0, 0, z))) for p, s in ((p0, -1), (p1, -1), (p1, 1), (p0, 1))]
        bm.faces.new(vs).material_index = mat


def build_beam(top_z):
    """Yukarı doğru genişleyen açık koni; UV.y: 0 altta → 1 üstte (Unity'de alfa geçişi)."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    rb, rt = RING_OUT * 0.98, RING_OUT * 1.55
    bottom = ring_verts(bm, rb, top_z)
    top = ring_verts(bm, rt, top_z + BEAM_H)
    for i in range(SEG):
        j = (i + 1) % SEG
        f = bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        f.material_index = 0
        u0, u1 = i / SEG, (i + 1) / SEG
        for loop, (u, v) in zip(f.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1))):
            loop[uv].uv = (u, v)
    me = bpy.data.meshes.new("Podium_Beam")
    bm.to_mesh(me)
    bm.free()
    obj = bpy.data.objects.new("Podium_Beam", me)
    bpy.context.collection.objects.link(obj)
    me.materials.append(bpy.data.materials["P_Beam"])
    return obj


def run():
    for n in ("Podium", "Podium_Beam"):
        o = bpy.data.objects.get(n)
        if o:
            bpy.data.objects.remove(o, do_unlink=True)
    make_materials()
    podium = build_podium()
    beam = build_beam(BASE_H + MID_H + TOP_H)
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    podium.select_set(True)
    beam.select_set(True)
    bpy.context.view_layer.objects.active = podium
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
    return podium, beam


_p, _b = run()
print("PODIUM_OK", len(_p.data.polygons), "top_z", BASE_H + MID_H + TOP_H)
