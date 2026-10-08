"""
Toplanabilir para (Assets/Art/UI/Icons/coin.png ikonunun 3D hâli): sivri uçlu altıgen, kalın altın çerçeve (pahlı),
iki yüzde içe gömük bordo yüz ve üstünde ince altın altıgen çizgi. Unity'de Y ekseninde sürekli döner (CoinField).
Çap ~0.6 m, kalınlık 0.12 m. Çıktı: Assets/Art/Pickups/Coin.fbx, Blender/source/coin.blend
Malzemeler: C_Gold (metalik altın), C_Face (bordo #5E1634).
"""
import math
import os

import bmesh
import bpy

ROOT = r"D:\Repolar\junpline"
FBX = os.path.join(ROOT, "Assets", "Art", "Pickups", "Coin.fbx")
BLEND = os.path.join(ROOT, "Blender", "source", "coin.blend")
R_OUT, R_FACE, R_LINE, R_LINE_IN = 0.30, 0.215, 0.165, 0.148
T_RIM, T_FACE, T_LINE = 0.06, 0.035, 0.05     # yarı kalınlıklar (rim > çizgi > yüz: yüz içe gömük)


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, metal, rough, glow=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = rough
    if glow:
        b.inputs["Emission Color"].default_value = srgb(col)
        b.inputs["Emission Strength"].default_value = glow
    return m


def hexagon(r):
    # sivri uç yukarıda (ikondaki gibi): açılar 90°, 150°, …
    return [(r * math.cos(math.radians(90 + 60 * k)), r * math.sin(math.radians(90 + 60 * k))) for k in range(6)]


def ring_prism(bm, r_out, r_in, half_t, mat_index):
    """Altıgen halka (dış r_out, iç r_in), y ekseninde ±half_t kalınlık. Para düzlemi XZ (Blender), normal ±Y."""
    out, inn = hexagon(r_out), hexagon(r_in)
    layers = {}
    for side in (-1, 1):
        y = side * half_t
        layers[side] = ([bm.verts.new((x, y, z)) for x, z in out], [bm.verts.new((x, y, z)) for x, z in inn])
    for k in range(6):
        j = (k + 1) % 6
        for side in (-1, 1):
            o, i = layers[side]
            f = bm.faces.new((o[k], o[j], i[j], i[k]) if side > 0 else (o[j], o[k], i[k], i[j]))
            f.material_index = mat_index
        for ring in (0, 1):   # dış ve iç yan yüzler
            a, b = layers[-1][ring], layers[1][ring]
            f = bm.faces.new((a[k], a[j], b[j], b[k]))
            f.material_index = mat_index


def disc(bm, r, half_t, mat_index):
    pts = hexagon(r)
    for side in (-1, 1):
        vs = [bm.verts.new((x, side * half_t, z)) for x, z in pts]
        f = bm.faces.new(vs if side > 0 else list(reversed(vs)))
        f.material_index = mat_index


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    gold = mat("C_Gold", "#E8B95B", 0.9, 0.28, 0.4)
    face = mat("C_Face", "#5E1634", 0.2, 0.45)
    bm = bmesh.new()
    ring_prism(bm, R_OUT, R_FACE, T_RIM, 0)          # kalın altın çerçeve
    disc(bm, R_FACE, T_FACE, 1)                      # gömük bordo yüz
    ring_prism(bm, R_FACE + 0.002, R_FACE - 0.01, T_RIM * 0.85, 0)   # çerçeve iç kenarı (gölgeli basamak)
    ring_prism(bm, R_LINE, R_LINE_IN, T_LINE, 0)     # ince altın altıgen çizgi
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Coin")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(gold)
    me.materials.append(face)
    o = bpy.data.objects.new("Coin", me)
    bpy.context.collection.objects.link(o)
    bev = o.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.012
    bev.segments = 2
    bev.limit_method = "ANGLE"
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.modifier_apply(modifier="Bevel")
    for p in me.polygons:
        p.use_smooth = False
    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("COIN_OK", len(me.polygons))


main()
