"""
Diyarbakır sokağı (koşu yolu modülü) — Desktop/Yeni klasör/genel map duruşu.mp4 referansı.

Siyah bazalt taş evler, ahşap cumbalar + çapraz payandalar, kemerli ahşap kapılar, parmaklıklı kemerli pencereler,
kat bantları, sokak üstünde taş kemer + kapalı ahşap köprü, kesme taş döşeme ve yüksek kaldırım şeritleri.

Blender'da sokak -Y yönünde uzanır (Unity'de +Z = koşu yönü). x: sokak genişliği (merkez 0), z: yukarı.
Çıktı: Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbx (+ Blender/source/street.blend)
Malzemeler: S_Basalt, S_BasaltTrim, S_Paving, S_Curb, S_Wood, S_WoodDark, S_Glass, S_Iron
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
TEX = os.path.join(ROOT, "Blender", "textures")
FBX_PATH = os.path.join(ROOT, "Assets", "Art", "Environment", "Diyarbakir", "DiyarbakirStreet.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "street.blend")

SEGMENT = 60.0          # modül uzunluğu (m), Unity'de art arda dizilir
STREET_HALF = 5.0       # cephe hattı |x| (3 şerit + kaldırım)
LANE_W = 2.2            # şerit genişliği; şerit merkezleri x = -LANE_W, 0, +LANE_W (Unity PlayerController.laneWidth)
CURB_W, CURB_H = 0.6, 0.18
FLOOR_H = 3.3
TEX_M = 4.0             # 1 doku = 4 m
rng = random.Random(11)

MATS = {}


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def material(name, tex=None, color="#FFFFFF", rough=0.8, metal=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Base Color"].default_value = srgb(color)
    for n in [n for n in nt.nodes if n.type == "TEX_IMAGE"]:
        nt.nodes.remove(n)
    if tex:
        img = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = img
        nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
    m.diffuse_color = srgb(color)
    MATS[name] = m
    return m


def make_materials():
    material("S_Basalt", "basalt.png", rough=0.9)
    material("S_BasaltTrim", "basalt.png", "#B8B4AC", rough=0.85)
    material("S_Paving", "paving.png", rough=0.85)
    material("S_Curb", "curb.png", rough=0.85)
    material("S_Wood", "wood.png", rough=0.7)
    material("S_WoodDark", "wood.png", "#7A6A60", rough=0.75)
    material("S_Glass", None, "#0E1218", rough=0.2, metal=0.3)
    material("S_Iron", None, "#1A1A1C", rough=0.5, metal=0.8)


ORDER = ["S_Basalt", "S_BasaltTrim", "S_Paving", "S_Curb", "S_Wood", "S_WoodDark", "S_Glass", "S_Iron"]
BASALT, TRIM, PAVING, CURB, WOOD, WOODDARK, GLASS, IRON = range(8)


# ---------------------------------------------------------------------------
# Geometri yardımcıları: tek bir bmesh'e eklenir, malzeme indeksi yüz başına
# ---------------------------------------------------------------------------
class Builder:
    def __init__(self):
        self.bm = bmesh.new()

    def box(self, x0, x1, y0, y1, z0, z1, mat):
        bm = self.bm
        v = [bm.verts.new((x, y, z)) for z in (z0, z1) for y in (y0, y1) for x in (x0, x1)]
        faces = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
        for f in faces:
            face = bm.faces.new([v[i] for i in f])
            face.material_index = mat
        return v

    def prism(self, pts2d, to3d, depth_vec, mat, cap_mat=None):
        """2B çokgeni (u,v) 3B'ye taşıyıp depth_vec yönünde kalınlık verir."""
        bm = self.bm
        front = [bm.verts.new(to3d(u, v)) for u, v in pts2d]
        back = [bm.verts.new(Vector(to3d(u, v)) + depth_vec) for u, v in pts2d]
        f1 = bm.faces.new(front)
        f2 = bm.faces.new(list(reversed(back)))
        f1.material_index = f2.material_index = cap_mat if cap_mat is not None else mat
        n = len(pts2d)
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((front[i], front[j], back[j], back[i]))
            f.material_index = mat

    def finish(self, name):
        bm = self.bm
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.normal_update()
        uv = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:  # kutu izdüşümü (dünya koordinatı / 4 m)
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                p = loop.vert.co
                if ax == 0:
                    loop[uv].uv = (p.y / TEX_M, p.z / TEX_M)
                elif ax == 1:
                    loop[uv].uv = (p.x / TEX_M, p.z / TEX_M)
                else:
                    loop[uv].uv = (p.x / TEX_M, p.y / TEX_M)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for n in ORDER:
            me.materials.append(MATS[n])
        obj = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(obj)
        return obj


def arch_profile(w, h, segs=10, pointed=False):
    """Alt kenarı düz, üstü yarım daire (ya da sivri) kemer; (u, v) saat yönünün tersine."""
    r = w / 2
    pts = [(-r, 0.0), (r, 0.0)]
    spring = h - r
    for i in range(segs + 1):
        a = math.pi * i / segs
        if pointed:
            # iki yaydan sivri kemer
            u = r * math.cos(a)
            v = spring + r * math.sin(a) * (1.0 + 0.25 * (1 - abs(math.cos(a))))
        else:
            u, v = r * math.cos(a), spring + r * math.sin(a)
        pts.append((u, v))
    return pts


def ring_profile(w, h, t, segs=12):
    """Kemer çevresi (taş söve): dış kemer - iç kemer arası şerit, bacaklarıyla."""
    outer = arch_profile(w + 2 * t, h + t, segs)
    inner = arch_profile(w, h, segs)
    # dış: sol alt → sağ alt → yay (sağdan sola) ; iç ters sırada
    o = [outer[0], outer[1]] + outer[2:]
    i = [inner[0], inner[1]] + inner[2:]
    pts = [o[0], i[0]] + list(reversed(i[2:])) + [i[1], o[1]] + o[2:]
    return pts


# ---------------------------------------------------------------------------
# Ev cephesi
# ---------------------------------------------------------------------------
def facade_point(s, off):
    """Cephe düzlemi: x = s*(STREET_HALF - off) (off>0 sokağa doğru)."""
    return lambda yc, z0: (lambda u, v: (s * (STREET_HALF - off), yc + u, z0 + v))


def house(b, s, y0, y1, floors, has_cumba, door_pos):
    xi, xo = s * STREET_HALF, s * (STREET_HALF + 7.0)
    H = floors * FLOOR_H + 0.7
    b.box(min(xi, xo), max(xi, xo), min(y0, y1), max(y0, y1), 0.0, H, BASALT)
    width = abs(y1 - y0)
    ymid = (y0 + y1) / 2
    toward = Vector((-s, 0, 0))

    # Kat bantları ve saçak (açık taş)
    for f in range(1, floors + 1):
        z = f * FLOOR_H if f < floors else H - 0.25
        x0, x1 = sorted((s * (STREET_HALF - 0.08), s * (STREET_HALF + 0.05)))
        b.box(x0, x1, min(y0, y1), max(y0, y1), z - 0.12, z + 0.06 if f < floors else z + 0.25, TRIM)

    # Zemin kat: kemerli kapı + taş söve + basamak
    yd = ymid + door_pos * width * 0.25
    to3 = facade_point(s, 0.03)(yd, 0.0)
    b.prism(arch_profile(1.15, 2.45), to3, toward * 0.06, WOOD)
    b.prism(ring_profile(1.15, 2.45, 0.18), facade_point(s, 0.0)(yd, 0.0), toward * 0.14, TRIM)
    sx0, sx1 = sorted((s * STREET_HALF, s * (STREET_HALF - 0.45)))
    b.box(sx0, sx1, yd - 0.85, yd + 0.85, 0.0, CURB_H + 0.14, TRIM)

    # Zemin kat penceresi (kapının diğer tarafında)
    yw = ymid - door_pos * width * 0.27
    if abs(yw - yd) > 1.6:
        window(b, s, yw, 1.0, 0.7, 1.25)

    # Üst katlar: kemerli pencereler ya da cumba
    for f in range(1, floors):
        z0 = f * FLOOR_H + 0.5
        if has_cumba and f == 1:
            cumba(b, s, ymid, min(width - 1.2, 3.2), f * FLOOR_H)
        else:
            n = max(1, int(width // 2.4))
            for k in range(n):
                yk = min(y0, y1) + width * (k + 0.5) / n
                window(b, s, yk, z0, 0.75, 1.35)


def window(b, s, yc, z0, w, h):
    toward = Vector((-s, 0, 0))
    b.prism(arch_profile(w, h), facade_point(s, 0.02)(yc, z0), toward * 0.03, GLASS)
    b.prism(ring_profile(w, h, 0.12), facade_point(s, 0.0)(yc, z0), toward * 0.1, TRIM)
    # demir parmaklık
    xa, xb = sorted((s * (STREET_HALF - 0.06), s * (STREET_HALF - 0.09)))
    for k in range(3):
        u = -w / 2 + w * (k + 1) / 4
        b.box(xa, xb, yc + u - 0.015, yc + u + 0.015, z0, z0 + h - w / 2 * 0.3, IRON)
    b.box(xa, xb, yc - w / 2, yc + w / 2, z0 + h * 0.45, z0 + h * 0.45 + 0.03, IRON)
    # denizlik
    xc, xd = sorted((s * (STREET_HALF + 0.0), s * (STREET_HALF - 0.16)))
    b.box(xc, xd, yc - w / 2 - 0.12, yc + w / 2 + 0.12, z0 - 0.08, z0, TRIM)


def cumba(b, s, yc, w, zf):
    """Ahşap cumba: sokağa 0.9 m taşar, pencere şeritleri, altında çapraz payandalar, üstünde saçak."""
    d = 0.95
    h = FLOOR_H - 0.35
    xa, xb = sorted((s * STREET_HALF, s * (STREET_HALF - d)))
    z0 = zf + 0.15
    b.box(xa, xb, yc - w / 2, yc + w / 2, z0, z0 + 0.22, WOODDARK)          # taban kirişi
    b.box(xa, xb, yc - w / 2, yc + w / 2, z0 + 0.22, z0 + 0.95, WOOD)        # alt panel
    xf = s * (STREET_HALF - d - 0.005)
    # gövde (ahşap) + önde kemerli açıklıklar (koyu cam) ve oymalı çerçeveler
    b.box(xa, xb, yc - w / 2, yc + w / 2, z0 + 0.95, z0 + h - 0.35, WOOD)
    n = max(3, int(w / 0.6))
    ow = w / n - 0.14
    oh = h - 1.45
    toward = Vector((-s, 0, 0))
    for k in range(n):
        u = -w / 2 + w * (k + 0.5) / n
        face = lambda uu, v, u=u: (s * (STREET_HALF - d - 0.005), yc + u + uu, z0 + 1.02 + v)
        b.prism(arch_profile(ow, oh, 8), face, toward * 0.02, GLASS)
        ring = lambda uu, v, u=u: (s * (STREET_HALF - d - 0.01), yc + u + uu, z0 + 1.02 + v)
        b.prism(ring_profile(ow, oh, 0.05, 8), ring, toward * 0.05, WOODDARK)
    # alt panel üzerinde oyma kutucuklar
    for k in range(n):
        u = -w / 2 + w * (k + 0.5) / n
        pa, pb = sorted((s * (STREET_HALF - d - 0.005), s * (STREET_HALF - d - 0.04)))
        b.box(pa, pb, yc + u - ow / 2 + 0.05, yc + u + ow / 2 - 0.05, z0 + 0.38, z0 + 0.82, WOODDARK)
    # kemerli üst süs paneli (oyma hissi: koyu çerçeve)
    b.box(xa, xb, yc - w / 2, yc + w / 2, z0 + h - 0.35, z0 + h, WOOD)
    # yan yüzler pencere
    for side in (-1, 1):
        ys = yc + side * w / 2
        b.box(xa, xb, ys - 0.03, ys + 0.03, z0 + 1.0, z0 + h - 0.35, GLASS)
    # saçak (eğimli tahta)
    roof_d = d + 0.25
    xr0, xr1 = sorted((s * (STREET_HALF + 0.05), s * (STREET_HALF - roof_d)))
    b.box(xr0, xr1, yc - w / 2 - 0.15, yc + w / 2 + 0.15, z0 + h, z0 + h + 0.12, WOODDARK)
    # çapraz payandalar (duvardan cumba altına)
    for side in (-1, 1):
        yb = yc + side * (w / 2 - 0.2)
        to3 = lambda u, v, yb=yb: (s * (STREET_HALF - u), yb - 0.06, z0 - v)
        b.prism([(0.0, 1.1), (0.12, 1.1), (d - 0.05, 0.05), (d - 0.05, -0.05), (d - 0.2, -0.05)],
                to3, Vector((0, 0.12, 0)), WOODDARK)
        b.box(*sorted((s * STREET_HALF, s * (STREET_HALF - 0.12))), yb - 0.06, yb + 0.06, z0 - 1.25, z0, WOODDARK)


# ---------------------------------------------------------------------------
# Sokak üstü taş kemer + kapalı ahşap köprü
# ---------------------------------------------------------------------------
def arch_bridge(b, yc):
    t = 1.4                       # kemer kalınlığı (y)
    span = 2 * STREET_HALF
    open_h = 7.0                  # yan şeritte zıplayan karakter kemere değmesin
    top = 8.6
    # kemer duvarı: dış dikdörtgen - kemer açıklığı (iki yan + üst)
    arch = arch_profile(span - 0.6, open_h, 16)
    r = (span - 0.6) / 2
    outline = [(-STREET_HALF, 0.0), (-r, 0.0)] + [(arch[i][0], arch[i][1]) for i in range(len(arch) - 1, 1, -1)] + \
              [(r, 0.0), (STREET_HALF, 0.0), (STREET_HALF, top), (-STREET_HALF, top)]
    to3 = lambda u, v: (u, yc - t / 2, v)
    b.prism(outline, to3, Vector((0, t, 0)), BASALT)
    # kemer taşı (açık söve) ön ve arka yüzde
    ring = ring_profile(span - 0.6, open_h, 0.3, 16)
    for yy, dv in ((yc - t / 2 - 0.06, 0.07), (yc + t / 2 - 0.01, 0.07)):
        b.prism(ring, lambda u, v, yy=yy: (u, yy, v), Vector((0, dv, 0)), TRIM)
    # üst köprü: kapalı ahşap geçit
    z0 = top
    hb = 2.3
    b.box(-STREET_HALF, STREET_HALF, yc - t / 2 - 0.1, yc + t / 2 + 0.1, z0, z0 + 0.25, TRIM)
    b.box(-STREET_HALF, STREET_HALF, yc - t / 2, yc + t / 2, z0 + 0.25, z0 + 0.9, WOOD)
    # kemerli ahşap pencere sırası (iki yüz)
    n = 6
    for k in range(n):
        u = -STREET_HALF + span * (k + 0.5) / n
        for yy, dv in ((yc - t / 2 - 0.02, 0.04), (yc + t / 2 - 0.02, 0.04)):
            b.prism(arch_profile(0.6, 1.05), lambda uu, v, yy=yy, u=u: (u + uu, yy, z0 + 0.95 + v), Vector((0, dv, 0)), GLASS)
    b.box(-STREET_HALF, STREET_HALF, yc - t / 2, yc + t / 2, z0 + 0.9, z0 + hb, WOODDARK)
    b.box(-STREET_HALF - 0.2, STREET_HALF + 0.2, yc - t / 2 - 0.35, yc + t / 2 + 0.35, z0 + hb, z0 + hb + 0.15, WOODDARK)


# ---------------------------------------------------------------------------
def street_floor(b, y0, y1):
    b.box(-STREET_HALF + CURB_W, STREET_HALF - CURB_W, y0, y1, -0.2, 0.0, PAVING)
    # 3 şerit: koyu bazalt şerit çizgileri (aralar ±LANE_W/2, kenarlar ±1.5*LANE_W)
    for x in (-1.5 * LANE_W, -0.5 * LANE_W, 0.5 * LANE_W, 1.5 * LANE_W):
        b.box(x - 0.08, x + 0.08, y0, y1, -0.2, 0.015, CURB)
    for s in (-1, 1):
        xa, xb = sorted((s * STREET_HALF, s * (STREET_HALF - CURB_W)))
        b.box(xa, xb, y0, y1, -0.2, CURB_H, CURB)


def build():
    for o in list(bpy.data.objects):
        if o.name.startswith(("DiyarbakirStreet", "Street_")):
            bpy.data.objects.remove(o, do_unlink=True)
    make_materials()
    walls, floor = Builder(), Builder()
    street_floor(floor, -SEGMENT, 0.0)

    for s in (-1, 1):
        y = 0.0
        while y > -SEGMENT + 0.5:
            w = min(rng.uniform(4.5, 7.5), y + SEGMENT)
            floors = rng.choice((2, 3, 3))
            house(walls, s, y, y - w, floors, has_cumba=rng.random() < 0.8 and floors >= 2, door_pos=rng.choice((-1, 1)))
            y -= w

    bridges = Builder()
    for yc in (-18.0, -46.0):
        arch_bridge(bridges, yc)

    objs = [floor.finish("Street_Floor"), walls.finish("Street_Houses"), bridges.finish("Street_Arches")]
    for o in objs:
        for p in o.data.polygons:
            p.use_smooth = False
    return objs


def export(objs):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, copy=True)


_objs = build()
export(_objs)
print("STREET_OK", [(o.name, len(o.data.polygons)) for o in _objs])
