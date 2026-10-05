"""
Çatı haritası (gece) + portal + zipline — Desktop/Yeni klasör/üst map.mp4 ve karakter zipline animasyonu.mp4 referansı.

Referanstan: fırtınalı gece, düz taş çatılar (kesme taş döşeme), alçak korkuluklar (taş baba sırası), kare bacalar (piramit
başlık), çamaşır ipleri + beyaz çamaşırlar, ahşap direkler ve çatıların üstünden geçen mavi neon kablolar (yer yer
çaprazlanır), binalar arası derin ara sokak (sıcak sarı ışıklar), duvarlarda kırmızı ışıklı çatlaklar, uzakta kubbe/minare.
Zipline: çatı kenarında kule → karakter zıplayıp neon halata tutunur (kıvılcım) → boşluğun üstünden kayar → karşı çatıya
iner, koşmaya devam eder.

Koordinatlar bu betikte "Unity gibi" yazılır: x yanal, h yukarı, z ileri (koşu yönü). Blender'a (−x, −z, h) olarak
aktarılır (FBX: Unity x → Blender −x, Unity z → Blender −y; bkz. place_decor_in_street.py).

Modül 80 m; Unity'de art arda dizilir (StreetRecycler). Çatı yüzeyi h=0 (Unity'de RooftopWorld yüksekliği).
Rota: çatı z∈[0,45] ve [62,80] (+ sonraki modülün başı); z 45–62 arası boşluk → zipline.
Çıktılar: Assets/Art/Environment/Rooftop/RooftopModule.fbx, Portal.fbx, Blender/source/rooftop.blend
Unity sabitleri (Assets/Scripts/Level/Zipline.cs varsayılanları ile aynı): ZIP_*
"""
import importlib.util
import math
import os
import random
import shutil
import sys

import bmesh
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
spec = importlib.util.spec_from_file_location("build_street", os.path.join(ROOT, "Blender", "scripts", "build_street.py"))
bs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bs)          # yalnızca yardımcılar (malzemeler, Builder, kemer profilleri)

OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Environment", "Rooftop")
FBX_PATH = os.path.join(OUT_DIR, "RooftopModule.fbx")
PORTAL_FBX = os.path.join(OUT_DIR, "Portal.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "rooftop.blend")
DECOR_BLEND = os.path.join(ROOT, "Blender", "source", "decor.blend")
SWIRL_PNG = os.path.join(ROOT, "Blender", "textures", "portal_swirl.png")

MOD = 80.0
HALF = 5.2              # rota binasının dış yüzü |x|
INNER = 4.8             # korkuluk iç yüzü
DEPTH = 14.0            # çatıdan ara sokak zeminine
LANE_W = bs.LANE_W
ROOF_RANGES = [(0.0, 45.0), (62.0, MOD)]
ZIP_START_Z, ZIP_END_Z = 43.0, 66.0     # halat başı / sonu (modül içi z)
ZIP_START_H, ZIP_END_H = 4.65, 3.0      # halat yüksekliği (çatıdan)
ZIP_SAG = 0.25
rng = random.Random(23)

# ---------------------------------------------------------------------------
# Malzemeler: sokak malzemeleri + çatıya özel ışıklılar
# ---------------------------------------------------------------------------
EXTRA = [("R_Neon", "#55CFFF", 12.0), ("R_NeonRed", "#FF3048", 8.0), ("R_Cloth", "#E8E6E0", 0.0),
         ("R_Lamp", "#FFB45A", 6.0), ("R_PortalCore", "#7FE0FF", 6.0)]
for _n, _c, _e in EXTRA:
    if _n not in bs.ORDER:
        bs.ORDER.append(_n)
(BASALT, TRIM, PAVING, CURB, WOOD, WOODDARK, GLASS, IRON,
 NEON, NEONRED, CLOTH, LAMP, PORTALCORE) = range(13)


def make_materials():
    bs.make_materials()
    for name, col, strength in EXTRA:
        m = bs.material(name, None, col, rough=0.6)
        bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        if strength > 0:
            bsdf.inputs["Emission Color"].default_value = bs.srgb(col)
            bsdf.inputs["Emission Strength"].default_value = strength
    make_swirl()
    core = bs.MATS["R_PortalCore"]
    nt = core.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    img = bpy.data.images.load(SWIRL_PNG, check_existing=True)
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])


def make_swirl():
    """Portal girdabı dokusu (mavi-beyaz spiral), 256x256."""
    if os.path.exists(SWIRL_PNG):
        return
    import numpy as np
    n = 256
    y, x = np.mgrid[0:n, 0:n]
    u, v = (x - n / 2) / (n / 2), (y - n / 2) / (n / 2)
    r = np.sqrt(u * u + v * v)
    a = np.arctan2(v, u)
    s = 0.5 + 0.5 * np.sin(a * 4 + r * 14)
    glow = np.clip(1.15 - r, 0, 1)
    core = np.clip(1 - r * 3.2, 0, 1)
    val = np.clip(s * glow * 0.9 + core, 0, 1)
    rgb = np.stack([0.20 + 0.8 * val ** 2, 0.55 + 0.45 * val, np.full_like(val, 1.0)], -1) * glow[..., None]
    rgba = np.concatenate([np.clip(rgb, 0, 1), (r < 1).astype(float)[..., None]], -1)
    img = bpy.data.images.new("portal_swirl", n, n, alpha=True)
    img.pixels = rgba.astype(np.float32).ravel()
    img.filepath_raw = SWIRL_PNG
    img.file_format = "PNG"
    img.save()


# ---------------------------------------------------------------------------
# Geometri (Unity koordinatında yazılır)
# ---------------------------------------------------------------------------
def P(x, h, z):
    return (-x, -z, h)


def box(b, x0, x1, h0, h1, z0, z1, mat):
    xa, xb = sorted((-x0, -x1))
    ya, yb = sorted((-z0, -z1))
    b.box(xa, xb, ya, yb, h0, h1, mat)


def tube(b, pts, r, mat):
    """Nokta dizisi boyunca 4 kenarlı ince boru (neon kablo, ip)."""
    bm = b.bm
    vs = [Vector(P(*p)) for p in pts]
    rings = []
    for i, p in enumerate(vs):
        t = (vs[min(i + 1, len(vs) - 1)] - vs[max(i - 1, 0)]).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.95 else Vector((1, 0, 0))
        s1 = t.cross(up).normalized() * r
        s2 = s1.cross(t).normalized() * r
        rings.append([bm.verts.new(p + s1), bm.verts.new(p + s2), bm.verts.new(p - s1), bm.verts.new(p - s2)])
    for a, c in zip(rings, rings[1:]):
        for k in range(4):
            f = bm.faces.new((a[k], a[(k + 1) % 4], c[(k + 1) % 4], c[k]))
            f.material_index = mat


def cable(b, a, c, sag, r=0.035, mat=NEON, n=14):
    pts = []
    for i in range(n + 1):
        t = i / n
        p = [a[k] + (c[k] - a[k]) * t for k in range(3)]
        p[1] -= sag * 4 * t * (1 - t)
        pts.append(tuple(p))
    tube(b, pts, r, mat)


def pyramid(b, x, z, h0, w, hh, mat):
    bm = b.bm
    w2 = w / 2
    base = [bm.verts.new(P(x + dx, h0, z + dz)) for dx, dz in ((-w2, -w2), (w2, -w2), (w2, w2), (-w2, w2))]
    apex = bm.verts.new(P(x, h0 + hh, z))
    for i in range(4):
        f = bm.faces.new((base[i], base[(i + 1) % 4], apex))
        f.material_index = mat
    f = bm.faces.new(list(reversed(base)))
    f.material_index = mat


def lathe(b, x, z, h0, prof, mat, segs=14):
    """(yarıçap, yükseklik) profilini dikey eksende döndürür (kubbe, minare)."""
    bm = b.bm
    rings = []
    for r, h in prof:
        if r < 1e-4:
            rings.append([bm.verts.new(P(x, h0 + h, z))] * segs)
        else:
            rings.append([bm.verts.new(P(x + r * math.cos(2 * math.pi * k / segs), h0 + h,
                                         z + r * math.sin(2 * math.pi * k / segs))) for k in range(segs)])
    for a, c in zip(rings, rings[1:]):
        for k in range(segs):
            q = [a[k], a[(k + 1) % segs], c[(k + 1) % segs], c[k]]
            uniq = []
            for v in q:
                if v not in uniq:
                    uniq.append(v)
            if len(uniq) >= 3:
                f = bm.faces.new(uniq)
                f.material_index = mat


def chimney(b, x, z, h0=0.0, s=0.7):
    box(b, x - s / 2, x + s / 2, h0, h0 + 1.7, z - s / 2, z + s / 2, BASALT)
    box(b, x - s / 2 - 0.08, x + s / 2 + 0.08, h0 + 1.7, h0 + 1.82, z - s / 2 - 0.08, z + s / 2 + 0.08, TRIM)
    for dx in (-0.2, 0.2):  # duman delikleri
        box(b, x + dx - 0.06, x + dx + 0.06, h0 + 1.25, h0 + 1.45, z - s / 2 - 0.01, z - s / 2 + 0.02, GLASS)
    pyramid(b, x, z, h0 + 1.82, s + 0.1, 0.55, TRIM)


def balustrade(b, side, z0, z1):
    """Korkuluk: taban + baba sırası + küpeşte (referans: üst map ilk kare)."""
    x0, x1 = sorted((side * INNER, side * HALF))
    box(b, x0, x1, 0.0, 0.18, z0, z1, TRIM)
    box(b, x0 - 0.03, x1 + 0.03, 0.82, 0.95, z0, z1, TRIM)
    z = z0 + 0.25
    while z < z1 - 0.2:
        box(b, x0 + 0.1, x1 - 0.1, 0.18, 0.82, z - 0.07, z + 0.07, BASALT)
        z += 0.42


def facade(b, z, face, x0, x1, h_top, h_bot, hero=False):
    """Boşluğa bakan cephe: kemerli pencereler, bazıları sıcak ışıklı; kahraman cephede kırmızı çatlaklar."""
    d = -0.06 * face   # cepheden dışarı
    floors = int((h_top - h_bot) // 3.3)
    for f in range(floors):
        hb = h_top - 2.6 - f * 3.3
        xs = [x0 + (x1 - x0) * (k + 0.5) / 3 for k in range(3)]
        for x in xs:
            lit = rng.random() < (0.35 if hero else 0.2)
            mat = LAMP if lit else GLASS
            zz0, zz1 = sorted((z, z + d))
            box(b, x - 0.45, x + 0.45, hb, hb + 1.3, zz0, zz1, mat)
            box(b, x - 0.55, x + 0.55, hb - 0.12, hb, *sorted((z, z + d * 2)), TRIM)
    if hero:
        # kırmızı ışıklı zikzak çatlak (referans: üst map kırmızı yarıklar)
        for _ in range(2):
            x = rng.uniform(x0 + 1, x1 - 1)
            h = rng.uniform(h_bot + 3, h_top - 2)
            pts = []
            for k in range(7):
                pts.append((x + rng.uniform(-0.3, 0.3), h - k * 0.55, z + d * 0.5))
            tube(b, pts, 0.04, NEONRED)


def laundry(b, side_x, z0, z1, h=2.2):
    """İki direk + ip + asılı çamaşırlar (x sabit düzlemde)."""
    for z in (z0, z1):
        box(b, side_x - 0.04, side_x + 0.04, 0.0, h + 0.1, z - 0.04, z + 0.04, WOODDARK)
    cable(b, (side_x, h, z0), (side_x, h, z1), 0.18, r=0.012, mat=IRON, n=8)
    z = z0 + 0.4
    while z < z1 - 0.5:
        w = rng.uniform(0.45, 0.75)
        hh = rng.uniform(0.5, 0.95)
        t = (z + w / 2 - z0) / (z1 - z0)
        top = h - 0.18 * 4 * t * (1 - t)
        box(b, side_x - 0.01, side_x + 0.01, top - hh, top, z, z + w, CLOTH)
        z += w + rng.uniform(0.15, 0.4)


def pole(b, x, z, h=7.5):
    box(b, x - 0.09, x + 0.09, 0.0, h, z - 0.09, z + 0.09, WOODDARK)
    box(b, x - 0.75, x + 0.75, h - 0.35, h - 0.22, z - 0.06, z + 0.06, WOODDARK)
    for dx in (-0.65, 0.65):
        box(b, x + dx - 0.04, x + dx + 0.04, h - 0.22, h - 0.12, z - 0.04, z + 0.04, CURB)  # izolatör


def gantry(b, z, beam_h, cable_h):
    """Zipline kulesi: iki taş ayak + ahşap kiriş + şerit başına demir askı."""
    for s in (-1, 1):
        box(b, s * 5.25 - 0.3, s * 5.25 + 0.3, 0.0, beam_h + 0.45, z - 0.3, z + 0.3, BASALT)
        box(b, s * 5.25 - 0.38, s * 5.25 + 0.38, beam_h + 0.45, beam_h + 0.55, z - 0.38, z + 0.38, TRIM)
    box(b, -5.3, 5.3, beam_h, beam_h + 0.35, z - 0.18, z + 0.18, WOODDARK)
    for k in (-1, 0, 1):
        x = k * LANE_W
        box(b, x - 0.06, x + 0.06, cable_h, beam_h, z - 0.06, z + 0.06, IRON)
        box(b, x - 0.15, x + 0.15, cable_h - 0.05, cable_h + 0.08, z - 0.15, z + 0.15, NEON)  # ışıklı makara


def route(b, neon):
    for (z0, z1) in ROOF_RANGES:
        box(b, -HALF, HALF, -DEPTH, -0.05, z0, z1, BASALT)              # bina gövdesi
        box(b, -INNER, INNER, -0.06, 0.0, z0, z1, PAVING)               # çatı döşemesi
        for s in (-1, 1):
            balustrade(b, s, z0, z1)
        # şerit izleri (çok hafif, taş derz bandı)
        for x in (-1.5 * LANE_W, -0.5 * LANE_W, 0.5 * LANE_W, 1.5 * LANE_W):
            box(b, x - 0.05, x + 0.05, -0.06, 0.006, z0, z1, TRIM)
    # bina ekleri (çatı üstünde ince derz + kat bandı)
    box(b, -INNER, INNER, -0.06, 0.012, 21.9, 22.1, TRIM)
    # boşluk kenarları: alçak bordür + cephe
    box(b, -INNER, INNER, 0.0, 0.22, 44.75, 45.0, TRIM)
    box(b, -INNER, INNER, 0.0, 0.22, 62.0, 62.25, TRIM)
    facade(b, 45.0, +1, -HALF, HALF, 0.0, -DEPTH)
    facade(b, 62.0, -1, -HALF, HALF, 0.0, -DEPTH, hero=True)
    # ara sokak zemini + sıcak lambalar + karşıdan karşıya çamaşır ipi
    box(b, -HALF, HALF, -DEPTH - 0.2, -DEPTH, 45.0, 62.0, PAVING)
    for z in (49.0, 57.0):
        for s in (-1, 1):
            box(b, s * HALF - 0.15, s * HALF + 0.15, -DEPTH + 3.0, -DEPTH + 3.35, z - 0.12, z + 0.12, LAMP)
    for z in (50.5, 55.0):
        cable(b, (-HALF, -6.0, z), (HALF, -6.0, z + 0.5), 0.5, r=0.012, mat=IRON, n=8)
        for k in range(5):
            x = -3.6 + k * 1.8 + rng.uniform(-0.2, 0.2)
            box(b, x, x + 0.6, -6.9 - rng.uniform(0, 0.3), -6.35, z + 0.25 - 0.01, z + 0.25 + 0.01, CLOTH)
    # bacalar (korkuluk hizasında, şerit dışı)
    for z, s in ((8.0, -1), (30.0, 1), (36.0, -1), (71.0, 1)):
        chimney(b, s * 4.45, z)
    # çamaşır ipleri
    laundry(b, 4.05, 11.0, 18.0)
    laundry(b, -4.05, 24.0, 29.5)
    laundry(b, -4.05, 68.0, 76.0)
    # direkler + yan neon kablolar (bir sonraki modülün ilk direğine bağlanır) + çapraz kablolar
    poles = [2.0, 22.0, 40.0, 64.0]
    for z in poles:
        for s in (-1, 1):
            pole(b, s * 5.0, z)
    nxt = poles + [MOD + poles[0]]
    for z0, z1 in zip(nxt, nxt[1:]):
        for s in (-1, 1):
            for dx in (-0.65, 0.65):
                cable(neon, (s * 5.0 + dx, 7.38, z0), (s * 5.0 + dx, 7.38, z1), 0.8 if z1 - z0 > 20 else 0.5)
    for z0, z1 in ((2.0, 22.0), (64.0, MOD + 2.0)):
        cable(neon, (-5.6, 7.38, z0), (5.6, 7.38, z1), 0.6)
        cable(neon, (5.6, 7.38, z0), (-5.6, 7.38, z1), 0.6)
    # zipline: iki kule + şerit başına neon halat
    gantry(b, ZIP_START_Z, ZIP_START_H + 0.55, ZIP_START_H)
    gantry(b, ZIP_END_Z, ZIP_END_H + 0.6, ZIP_END_H)
    for k in (-1, 0, 1):
        x = k * LANE_W
        cable(neon, (x, ZIP_START_H, ZIP_START_Z), (x, ZIP_END_H, ZIP_END_Z), ZIP_SAG, r=0.045, n=20)


def city(b):
    """Rota dışı: rastgele yükseklikte taş çatılar, bacalar, kubbeler, çamaşırlar; uzakta minare."""
    for s in (-1, 1):
        for col in range(3):
            x0 = HALF + 0.6 + col * 9.5
            x1 = x0 + rng.uniform(7.5, 9.0)
            z = rng.uniform(-2, 2)
            while z < MOD:
                ln = rng.uniform(8, 17)
                top = rng.uniform(-3.5, 1.5) if col == 0 else rng.uniform(-5, 3)
                xa, xb = sorted((s * x0, s * x1))
                za, zb = z, min(z + ln, MOD + 2)
                box(b, xa, xb, -DEPTH, top, za, zb, BASALT)
                box(b, xa + 0.3, xb - 0.3, top, top + 0.02, za + 0.3, zb - 0.3, PAVING)
                for xx0, xx1, zz0, zz1 in ((xa, xb, za, za + 0.35), (xa, xb, zb - 0.35, zb),
                                           (xa, xa + 0.35, za, zb), (xb - 0.35, xb, za, zb)):
                    box(b, xx0, xx1, top, top + 0.7, zz0, zz1, TRIM)
                if rng.random() < 0.7:
                    chimney(b, rng.uniform(xa + 1, xb - 1), rng.uniform(za + 1, zb - 1), top)
                if rng.random() < 0.25:
                    cx, cz = (xa + xb) / 2, (za + zb) / 2
                    lathe(b, cx, cz, top, [(2.2, 0), (2.2, 0.5)] + [(2.2 * math.cos(a), 0.5 + 2.2 * math.sin(a))
                                                                     for a in [i * math.pi / 12 for i in range(1, 7)]], TRIM)
                if rng.random() < 0.35:
                    lz = rng.uniform(za + 1, max(za + 1.5, zb - 4))
                    laundry(b, (xa + xb) / 2, lz, min(lz + 3.5, zb - 0.5), h=top + 2.0)
                z = zb + rng.uniform(1.5, 3.5)
    # uzak siluet: alçak kütleler + bir minare + büyük kubbe
    for s in (-1, 1):
        for k in range(4):
            x0 = 36 + k * 12
            box(b, *sorted((s * x0, s * (x0 + 10))), -DEPTH, rng.uniform(-2, 6), k * 20, k * 20 + 18, BASALT)
    lathe(b, -44.0, 50.0, -DEPTH, [(1.4, 0), (1.4, 30), (2.0, 30.2), (2.0, 30.8), (1.1, 31), (1.1, 36), (0.0, 39)], TRIM, 10)
    lathe(b, 46.0, 20.0, 4.0, [(7, 0)] + [(7 * math.cos(a), 7 * math.sin(a)) for a in [i * math.pi / 12 for i in range(1, 7)]],
          BASALT, 16)
    box(b, 39, 53, -DEPTH, 4.0, 13, 27, BASALT)


def portal():
    """Sivri kemerli taş çerçeve + neon iç kenar + dönen girdap diski (tek şerit genişliğinde)."""
    frame = bs.Builder()
    wo, ho, wi, hi, dep = 2.3, 3.4, 1.6, 2.85, 0.45
    outer = bs.arch_profile(wo, ho, 16, pointed=True)
    inner = bs.arch_profile(wi, hi, 16, pointed=True)
    outline = [(-wo / 2, 0.0), (-wi / 2, 0.0)] + list(reversed(inner[2:])) + [(wi / 2, 0.0), (wo / 2, 0.0)] + outer[2:]
    frame.prism(outline, lambda u, v: (u, dep / 2, v), Vector((0, -dep, 0)), BASALT)
    ring = bs.ring_profile(wi, hi, 0.09, 16)
    for yy in (dep / 2 + 0.02, -dep / 2 - 0.02):
        frame.prism(ring, lambda u, v, yy=yy: (u, yy, v), Vector((0, -0.03 if yy > 0 else 0.03, 0)), NEON)
    frame.box(-wo / 2 - 0.2, wo / 2 + 0.2, -dep / 2 - 0.2, dep / 2 + 0.2, -0.05, 0.12, TRIM)
    # arka dolgu (koyu mavi parıltı)
    frame.prism(inner, lambda u, v: (u, 0.01, v), Vector((0, -0.02, 0)), PORTALCORE)
    f = frame.finish("Portal_Frame")

    # girdap diski: merkez (0, 1.45), 0..1 UV — Unity'de döner (Portal.cs)
    bm = bmesh.new()
    segs, r, hc = 32, 0.78, 1.45
    c = bm.verts.new((0, 0.03, 0))
    ring_v = [bm.verts.new((r * math.cos(2 * math.pi * k / segs), 0.03, r * math.sin(2 * math.pi * k / segs)))
              for k in range(segs)]
    for k in range(segs):
        bm.faces.new((c, ring_v[(k + 1) % segs], ring_v[k]))
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        face.material_index = 0
        for loop in face.loops:
            p = loop.vert.co
            loop[uv].uv = (0.5 + p.x / (2 * r), 0.5 + p.z / (2 * r))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new("Portal_Core")
    bm.to_mesh(me)
    bm.free()
    me.materials.append(bs.MATS["R_PortalCore"])
    core = bpy.data.objects.new("Portal_Core", me)
    bpy.context.collection.objects.link(core)
    core.location = (0, 0, hc)
    core.rotation_euler = (0, 0, 0)
    # her iki yüzden görünsün: kopya yüz ters normal
    core2 = core.copy()
    core2.data = me.copy()
    core2.name = "Portal_CoreBack"
    bpy.context.collection.objects.link(core2)
    core2.location = (0, -0.06, hc)
    core2.rotation_euler = (0, 0, math.pi)
    core2.parent = None
    return [f, core, core2]


ROOF_DECOR = [  # GameplaySceneBuilder.RoofDecorLayout ile aynı (ad, x, h, z, açı) — çatıdaki dosya dekorları
    ("Crates", 4.0, 0, 5.0, -90), ("Sack", -4.1, 0, 4.0, 90), ("CopperJugs", -4.0, 0, 13.0, 90),
    ("KilimPile", 4.0, 0, 20.0, -90), ("Birdhouse", 4.15, 0.95, 26.0, -90), ("RolledCarpet", -4.1, 0, 19.0, 90),
    ("WallLantern", -4.95, 1.6, 33.0, 90), ("Chest", 4.0, 0, 34.0, -90), ("Nazar", 5.0, 5.2, 40.0, -90),
    ("CopperPlates", -4.0, 0, 39.5, 90), ("Watermelon", 4.0, 0, 69.0, -90), ("FruitBasket", -4.0, 0, 64.5, 90),
    ("Samovar", 4.0, 0, 76.0, -90), ("WallLantern", 4.95, 1.6, 73.0, -90), ("BreadBasket", -4.05, 0, 78.0, 90),
]


def place_decor():
    if not os.path.exists(DECOR_BLEND):
        return 0
    with bpy.data.libraries.load(DECOR_BLEND, link=False) as (src, dst):
        dst.objects = [n for n in src.objects if n.startswith("Decor_")]
    coll = bpy.data.collections.new("RoofDecor")
    bpy.context.scene.collection.children.link(coll)
    lib = {o.name: o for o in dst.objects if o}
    count = 0
    for i, (name, x, h, z, rot) in enumerate(ROOF_DECOR):
        src_obj = lib.get("Decor_" + name)
        if src_obj is None:
            continue
        o = src_obj.copy()
        o.name = f"Placed_{name}_{i}"
        coll.objects.link(o)
        o.location = P(x, h, z)
        o.rotation_euler = (0, 0, math.radians(-rot))
        count += 1
    return count


def export(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    make_materials()
    b, neon, town = bs.Builder(), bs.Builder(), bs.Builder()
    route(b, neon)
    city(town)
    objs = [b.finish("Roof_Route"), neon.finish("Roof_Neon"), town.finish("Roof_City")]
    for o in objs:
        for p in o.data.polygons:
            p.use_smooth = False
    export(objs, FBX_PATH)

    portal_objs = portal()
    export(portal_objs, PORTAL_FBX)
    for o in portal_objs:
        o.location.x += 8.0   # blend dosyasında rotanın yanında dursun (FBX'e dokunmaz, dışa aktarımdan sonra)
    shutil.copyfile(SWIRL_PNG, os.path.join(OUT_DIR, "portal_swirl.png"))

    n = place_decor()
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("ROOFTOP_OK", [(o.name, len(o.data.polygons)) for o in objs], "decor", n)


main()
