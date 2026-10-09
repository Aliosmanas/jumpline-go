"""
ON GÖZLÜ KÖPRÜ — bölüm 1 final arenası (Dicle Köprüsü, 1065; bazalt taş, sivri kemerler).
40 m'lik tekrar eden parça (StreetRecycler ile sonsuz):
  • Köprü gövdesi: kemer açıklıklı bazalt duvar (profil x boyunca kalınlaştırılır), üstte döşeme (yol yüzeyi y=0),
    iki yanda korkuluk + başlık taşı, kemer çevresinde açık renk kemer taşı bandı, ayakta su yaranlar (mahmuz).
  • Dicle: köprünün 16 m altında geniş su yüzeyi; iki yanda vadi yamaçları (z'de 40 m periyotlu → dikişsiz).
  • Sağ yakada Diyarbakır surlarının silueti (mazgallı duvar + parça başına bir burç).
Koordinatlar Unity gibi yazılır: U(x, y, z) → Blender (-x, -z, y)  (FBX axis_forward -Z, up Y).
Çıktı: Assets/Art/Environment/Bridge/OnGozluBridge.fbx, Blender/source/bridge.blend
Çalıştır: blender -b -P Blender/scripts/build_bridge.py
"""
import math
import os

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Environment", "Bridge")
FBX = os.path.join(OUT_DIR, "OnGozluBridge.fbx")
BLEND = os.path.join(ROOT, "Blender", "source", "bridge.blend")

SEG = 40.0
HALF_W = 4.8          # döşeme yarı genişliği
PARAPET_IN = 4.25     # korkuluk iç yüzü
DECK_T = 1.2
WALL_HALF = 4.6       # kemer duvarı yarı kalınlığı (x)
BOTTOM = -21.0
WATER = -16.0
ARCH_C = 20.0         # kemer merkezi (z)
ARCH_SPAN = 13.5      # yarı açıklık
SPRING = -14.2        # kemer üzengi hizası
ARCH_RISE = 11.8      # üzengiden tepeye (sivri kemer)

MATS = {}


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def mat(name, col, rough=0.8, metal=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = srgb(col)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    MATS[name] = len(MATS)
    return m


def U(x, y, z):
    return Vector((-x, -z, y))


class Builder:
    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def quad(self, pts, mi, uv_scale=0.25):
        vs = [self.bm.verts.new(U(*p)) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = mi
        self._uv(f, uv_scale)
        return f

    def poly(self, pts, mi, uv_scale=0.25):
        return self.quad(pts, mi, uv_scale)

    def _uv(self, f, s):
        f.normal_update()
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for loop in f.loops:
            c = loop.vert.co
            if ax == 0:
                loop[self.uv].uv = (c.y * s, c.z * s)
            elif ax == 1:
                loop[self.uv].uv = (c.x * s, c.z * s)
            else:
                loop[self.uv].uv = (c.x * s, c.y * s)

    def box(self, x0, x1, y0, y1, z0, z1, mi, uv_scale=0.25, skip=()):
        P = lambda x, y, z: (x, y, z)
        faces = {
            "top": [P(x0, y1, z0), P(x1, y1, z0), P(x1, y1, z1), P(x0, y1, z1)],
            "bottom": [P(x0, y0, z1), P(x1, y0, z1), P(x1, y0, z0), P(x0, y0, z0)],
            "left": [P(x0, y0, z0), P(x0, y1, z0), P(x0, y1, z1), P(x0, y0, z1)],
            "right": [P(x1, y0, z1), P(x1, y1, z1), P(x1, y1, z0), P(x1, y0, z0)],
            "back": [P(x0, y0, z0), P(x1, y0, z0), P(x1, y1, z0), P(x0, y1, z0)],
            "front": [P(x0, y1, z1), P(x1, y1, z1), P(x1, y0, z1), P(x0, y0, z1)],
        }
        for k, pts in faces.items():
            if k not in skip:
                self.quad(pts, mi, uv_scale)


def arch_curve(n=28):
    """Basık eliptik kemer (soldan sağa z, y): Dicle Köprüsü'nün geniş açıklıklı kemerlerine yakın."""
    pts = []
    for i in range(n + 1):
        phi = math.pi * i / n
        pts.append((ARCH_C - ARCH_SPAN * math.cos(phi), SPRING + ARCH_RISE * math.sin(phi) ** 0.9))
    return pts


def build_bridge(b):
    stone, light, deck, cap = MATS["BR_Stone"], MATS["BR_Light"], MATS["BR_Deck"], MATS["BR_Cap"]
    # döşeme (yol): üst yüz koyu kaldırım, yanlar taş
    b.box(-HALF_W, HALF_W, -DECK_T, 0.0, 0.0, SEG, stone, skip=("top", "back", "front"))
    b.quad([(-PARAPET_IN, 0, 0), (PARAPET_IN, 0, 0), (PARAPET_IN, 0, SEG), (-PARAPET_IN, 0, SEG)], deck, 0.5)
    # korkuluklar + başlık taşları
    for s in (-1, 1):
        x0, x1 = sorted((s * PARAPET_IN, s * HALF_W))
        b.box(x0, x1, 0.0, 0.95, 0.0, SEG, stone, skip=("back", "front"))
        b.box(x0 - 0.06, x1 + 0.06, 0.95, 1.12, 0.0, SEG, cap, 0.5, skip=("back", "front"))
        # korkulukta parça başına iki taş babası (ritim)
        for zb in (8.0, 28.0):
            b.box(x0 - 0.1, x1 + 0.1, 0.0, 1.35, zb - 0.35, zb + 0.35, cap, 0.5)
    # kemer duvarı: profil (z, y) — dış kenar + kemer açıklığı; iki yüz + kemer içi tonozu
    arch = arch_curve()
    right_leg = [(ARCH_C + ARCH_SPAN, BOTTOM), (ARCH_C + ARCH_SPAN, SPRING)]
    left_leg = [(ARCH_C - ARCH_SPAN, SPRING), (ARCH_C - ARCH_SPAN, BOTTOM)]
    prof = [(0.0, BOTTOM), (0.0, -DECK_T), (SEG, -DECK_T), (SEG, BOTTOM)] + right_leg + list(reversed(arch))[1:-1] + left_leg
    for s in (-1, 1):
        x = s * WALL_HALF
        pts = [(x, y, z) for z, y in prof]
        if s < 0:
            pts = list(reversed(pts))
        vs = [b.bm.verts.new(U(*p)) for p in pts]
        f = b.bm.faces.new(vs)
        f.material_index = stone
        b._uv(f, 0.25)
    # tonoz (kemer içi) + ayak iç yüzleri
    inner = [(ARCH_C - ARCH_SPAN, BOTTOM)] + arch + [(ARCH_C + ARCH_SPAN, BOTTOM)]
    for i in range(len(inner) - 1):
        (za, ya), (zb_, yb) = inner[i], inner[i + 1]
        b.quad([(-WALL_HALF, ya, za), (WALL_HALF, ya, za), (WALL_HALF, yb, zb_), (-WALL_HALF, yb, zb_)], stone)
    # kemer taşı bandı (her iki yüzde, hafif çıkıntılı, açık taş)
    for s in (-1, 1):
        x = s * (WALL_HALF + 0.12)
        xi = s * WALL_HALF
        for i in range(len(arch) - 1):
            (za, ya), (zc, yc) = arch[i], arch[i + 1]
            na = Vector(((za - ARCH_C) / ARCH_SPAN, (ya - SPRING) / ARCH_RISE)).normalized()
            nc = Vector(((zc - ARCH_C) / ARCH_SPAN, (yc - SPRING) / ARCH_RISE)).normalized()
            oa = (za + na.x * 1.1, ya + na.y * 1.1)
            oc = (zc + nc.x * 1.1, yc + nc.y * 1.1)
            face = [(x, ya, za), (x, oa[1], oa[0]), (x, oc[1], oc[0]), (x, yc, zc)]
            if s > 0:
                face = list(reversed(face))
            b.quad(face, light, 0.6)
            # bant kenarları (çıkıntı)
            b.quad([(xi, oa[1], oa[0]), (x, oa[1], oa[0]), (x, oc[1], oc[0]), (xi, oc[1], oc[0])][:: (1 if s > 0 else -1)], light, 0.6)
    # ayak su yaranları (mahmuz): z=0'daki ayağın iki yanında üçgen prizma, su seviyesinin üstüne kadar
    for s in (-1, 1):
        x0 = s * WALL_HALF
        tip = s * (WALL_HALF + 4.0)
        top = WATER + 5.5
        for (za, zb_) in ((-3.2, 3.2),):
            base = [(x0, BOTTOM, za), (tip, BOTTOM, 0.0), (x0, BOTTOM, zb_)]
            up = [(x0, top, za), (tip, top, 0.0), (x0, top, zb_)]
            b.quad([base[0], up[0], up[1], base[1]][:: (1 if s > 0 else -1)], stone)
            b.quad([base[1], up[1], up[2], base[2]][:: (1 if s > 0 else -1)], stone)
            # piramit başlık
            peak = (x0 + s * 1.0, top + 2.6, 0.0)
            b.quad([up[0], peak, up[1]][:: (1 if s > 0 else -1)], light, 0.6)
            b.quad([up[1], peak, up[2]][:: (1 if s > 0 else -1)], light, 0.6)


def build_valley(b):
    water, earth, grass, wall = MATS["BR_Water"], MATS["BR_Earth"], MATS["BR_Grass"], MATS["BR_Wall"]
    b.quad([(-260, WATER, 0), (260, WATER, 0), (260, WATER, SEG), (-260, WATER, SEG)], water, 0.05)

    def bank_h(x, z, side):
        d = abs(x)
        base = WATER + 0.8 + (d - 40.0) * 0.21
        ripple = 2.2 * math.sin(2 * math.pi * z / SEG + side * 1.3) + 1.1 * math.sin(4 * math.pi * z / SEG + d * 0.05)
        return min(16.0, base + ripple * min(1.0, (d - 40.0) / 30.0))

    xs = [40, 55, 72, 92, 115, 140, 170, 210]
    zs = [SEG * k / 8 for k in range(9)]
    for side in (-1, 1):
        for i in range(len(xs) - 1):
            for j in range(len(zs) - 1):
                p = []
                for (xi, zj) in ((xs[i], zs[j]), (xs[i + 1], zs[j]), (xs[i + 1], zs[j + 1]), (xs[i], zs[j + 1])):
                    x = side * xi
                    p.append((x, max(WATER - 1.5, bank_h(x, zj, side)), zj))
                if side < 0:
                    p = list(reversed(p))
                avg = sum(q[1] for q in p) / 4
                b.quad(p, grass if avg > -9.0 else earth, 0.08)
    # sağ yakada sur silueti: mazgallı duvar + burç
    x0, x1 = 118.0, 124.0
    y0, y1 = -1.0, 9.0
    b.box(x0, x1, y0, y1, 0.0, SEG, wall, 0.15, skip=("back", "front"))
    n = 10
    for k in range(n):
        za = SEG * k / n + 0.6
        b.box(x0 - 0.2, x0 + 1.2, y1, y1 + 1.6, za, za + SEG / n * 0.55, wall, 0.15)
    b.box(x0 - 3.5, x1 + 3.5, y0, y1 + 6.5, 14.0, 24.0, wall, 0.15)       # burç
    for k in range(4):
        za = 14.0 + k * 2.6
        b.box(x0 - 3.7, x0 - 2.2, y1 + 6.5, y1 + 8.0, za, za + 1.4, wall, 0.15)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    ms = [mat("BR_Stone", "#5E5853", 0.85), mat("BR_Light", "#9A8E80", 0.8), mat("BR_Deck", "#4E4A46", 0.7),
          mat("BR_Cap", "#7B736A", 0.75), mat("BR_Water", "#2E6A6E", 0.08), mat("BR_Earth", "#8E6E4A", 0.95),
          mat("BR_Grass", "#6A7F3A", 0.95), mat("BR_Wall", "#3F3A36", 0.9)]
    b = Builder()
    build_bridge(b)
    build_valley(b)
    bmesh.ops.remove_doubles(b.bm, verts=b.bm.verts, dist=1e-4)
    bmesh.ops.triangulate(b.bm, faces=[f for f in b.bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="EAR_CLIP")
    me = bpy.data.meshes.new("BridgeSegment")
    b.bm.to_mesh(me)
    b.bm.free()
    for m in ms:
        me.materials.append(m)
    o = bpy.data.objects.new("BridgeSegment", me)
    bpy.context.collection.objects.link(o)
    for p in me.polygons:
        p.use_smooth = False
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.export_scene.fbx(filepath=FBX, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND)
    print("BRIDGE_OK faces=%d" % len(me.polygons))


main()
