"""
"Sur İçi Koşusu" parkuru için yeni araçlar ve engeller (onaylanan önizleme: claude.ai/artifact/SrMVSvKMysrSdDjrvqA3RM).
build_vehicles.py ile aynı malzeme (T_*) ve pah dili; ölçüler gerçekçi, Unity Obstacle profilleriyle birebir:

  MelonTruck  karpuz kamyonu     rampa z0–4 (0.05→2.35), karpuz dolu kasa z4–10 (2.35), kabin z10–12 (1.95)   ±1.0
  CityBus     belediye otobüsü   z −4.6…4.6, tavan 2.95 (yerden çıkılmaz)                                      ±1.05
  PatPat      üç teker yük motoru z −1.3…1.3, 1.15 (zıpla)                                                     ±0.7
  Tractor     traktör + römork   rampa z0–3 (0.1→1.6), römork z3–7.2 (1.6), kabin z7.2–8.1 (2.55), kaput z8.1–9.5 (1.35)
  BusStop     otobüs durağı      duvar (şerit değiştir)
  TeaCart / SimitCart / MelonStall   seyyar tezgâhlar (duvar)
  KebabGrill  kebap mangalı      duvar
  KilimLine   kilim askısı       tavan alt kenarı 1.15, kalınlık 1.15 (yalnız KAY)
  PotRack     bakır kazan askısı tavan alt kenarı 1.15, kalınlık 0.95 (kay)
  LowArch     alçak almaşık kemer tavan alt kenarı 1.15, kalınlık 1.45 (kay)
  TeaTable    çay masası + tabureler (ayrı parçalar: çarpınca savrulur; zıpla)

Koordinatlar "Unity gibi": x yanal, h yukarı, z ileri; araçların rampası/arkası koşucuya bakar.
Çıktı: Assets/Art/Environment/Obstacles/<Ad>.fbx, Blender/source/course_assets.blend
"""
import importlib.util
import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = r"D:\Repolar\junpline"
spec = importlib.util.spec_from_file_location("bv", os.path.join(ROOT, "Blender", "scripts", "build_vehicles.py"))
bv = importlib.util.module_from_spec(spec)
spec.loader.exec_module(bv)          # yardımcılar: box, loft, wheel, finish, export, make_materials, P

box, loft, wheel, finish, export, P = bv.box, bv.loft, bv.wheel, bv.finish, bv.export, bv.P
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "course_assets.blend")


def ell(parts, x, h, z, rx, ry, rz, mat, segs=12, rings=8):
    """Elipsoid (karpuz, kazan, simit yığını)."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    bmesh.ops.transform(bm, verts=bm.verts, matrix=Matrix.Diagonal((rx, rz, ry, 1.0)))
    bmesh.ops.translate(bm, verts=bm.verts, vec=P(x, h, z))
    parts.append(bv._obj_from_bm(bm, "p", mat, 0.0))


def cyl(parts, x, h0, h1, z, r, mat, segs=14):
    """Dikey silindir (direk, semaver, masa ayağı)."""
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r, radius2=r, depth=h1 - h0)
    bmesh.ops.translate(bm, verts=bm.verts, vec=P(x, (h0 + h1) / 2, z))
    parts.append(bv._obj_from_bm(bm, "p", mat, 0.0))


def cone(parts, x, h0, h1, z, r0, r1, mat, segs=16):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r0, radius2=r1, depth=h1 - h0)
    bmesh.ops.translate(bm, verts=bm.verts, vec=P(x, (h0 + h1) / 2, z))
    parts.append(bv._obj_from_bm(bm, "p", mat, 0.0))


def ramp(parts, z0, z1, h0, h1, w=0.85):
    """build_obstacles.py kamyon rampasıyla aynı dil: ahşap kalas, kaymaz çıtalar, neon kenar."""
    bv.box  # (aynı modül)
    bm_slab(parts, -w, w, z0, h0, z1, h1, 0.1, "T_Wood")
    n = max(4, int((z1 - z0) / 0.45))
    for k in range(1, n):
        z = z0 + (z1 - z0) * k / n
        h = h0 + (h1 - h0) * k / n
        box(parts, -w + 0.03, w - 0.03, h, h + 0.04, z - 0.04, z + 0.04, "T_Wood")
    for s in (-1, 1):
        bm_slab(parts, *sorted((s * w, s * (w + 0.07))), z0, h0 + 0.13, z1, h1 + 0.12, 0.16, "T_Neon")


def bm_slab(parts, x0, x1, z0, h0, z1, h1, t, mat):
    bm = bmesh.new()
    pts = [(z0, h0), (z1, h1), (z1, h1 - t), (z0, h0 - t)]
    a = [bm.verts.new(P(x0, hh, zz)) for zz, hh in pts]
    b = [bm.verts.new(P(x1, hh, zz)) for zz, hh in pts]
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(4):
        j = (i + 1) % 4
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(bv._obj_from_bm(bm, "p", mat, 0.0))


# ---------------------------------------------------------------------------
# Araçlar
# ---------------------------------------------------------------------------
def melon_truck():
    p = []
    W = 1.0
    ramp(p, 0.0, 4.0, 0.05, 2.35)
    box(p, -W + 0.1, W - 0.1, 0.42, 0.75, 3.8, 11.9, "T_Metal")                    # şasi
    box(p, -W, W, 0.75, 0.95, 4.0, 10.0, "T_Wood", 0.02)                           # kasa tabanı
    for s in (-1, 1):                                                              # yan kapaklar (yeşil boyalı tahta)
        box(p, *sorted((s * W, s * (W - 0.06))), 0.95, 2.0, 4.0, 10.0, "T_Melon", 0.02)
        for z in (5.2, 6.8, 8.4):
            box(p, *sorted((s * W, s * (W + 0.02))), 0.95, 2.0, z - 0.04, z + 0.04, "T_Metal")
    box(p, -W, W, 0.95, 2.1, 9.94, 10.0, "T_Melon", 0.02)                          # ön kapak
    # karpuzlar: alt kat + üst kat (üst yüzey ≈ 2.35 — üstünde koşulur)
    k = 0
    for layer, (hc, r) in enumerate(((1.35, 0.34), (1.98, 0.33))):
        z = 4.35 + layer * 0.27
        while z < 9.75:
            for x in (-0.66, -0.22, 0.22, 0.66):
                ell(p, x + (0.05 if k % 3 == 0 else -0.03), hc, z, 0.25, r, 0.31, "T_Melon" if k % 2 else "T_MelonDark", 10, 6)
                k += 1
            z += 0.6
    box(p, -W, W, 0.75, 1.95, 10.0, 11.95, "T_Blue", 0.1)                         # kabin
    box(p, -W - 0.02, W + 0.02, 1.95, 2.02, 10.05, 11.55, "T_White", 0.02)
    box(p, -W + 0.1, W - 0.1, 1.25, 1.85, 11.94, 11.97, "T_Glass")                # ön cam
    for s in (-1, 1):
        box(p, *sorted((s * W, s * (W + 0.01))), 1.25, 1.8, 10.25, 11.5, "T_Glass")
        box(p, s * 0.68 - 0.12, s * 0.68 + 0.12, 0.85, 1.02, 11.95, 11.99, "T_Lamp", 0.02)
        box(p, *sorted((s * (W + 0.02), s * (W + 0.16))), 1.45, 1.7, 11.4, 11.5, "T_Black")
    box(p, -W - 0.02, W + 0.02, 0.4, 0.6, 11.9, 12.1, "T_Chrome", 0.03)
    for z in (5.0, 8.6, 11.0):
        for s in (-1, 1):
            wheel(p, s * (W - 0.12), z, 0.42, 0.28)
    return finish(p, "MelonTruck")


def city_bus():
    p = []
    W, L = 1.05, 4.5
    box(p, -W, W, 0.35, 2.85, -L, L, "T_CityGreen", 0.14)
    box(p, -W - 0.01, W + 0.01, 0.35, 0.95, -L + 0.05, L - 0.05, "T_White", 0.06)   # alt etek (beyaz)
    box(p, -W + 0.08, W - 0.08, 1.05, 2.55, -L - 0.02, -L + 0.02, "T_Glass")         # ön cam
    box(p, -0.7, 0.7, 2.55, 2.78, -L - 0.03, -L + 0.0, "T_Lamp")                    # hat tabelası (LED)
    for s in (-1, 1):
        k = -L + 0.6
        while k < L - 0.7:
            box(p, *sorted((s * W, s * (W + 0.012))), 1.3, 2.3, k, k + 0.82, "T_Glass")
            k += 1.0
        box(p, *sorted((s * W, s * (W + 0.014))), 0.45, 2.3, -L + 0.75, -L + 1.65, "T_Black")   # kapı
        box(p, s * 0.72 - 0.15, s * 0.72 + 0.15, 0.55, 0.78, -L - 0.03, -L, "T_Lamp", 0.02)
        box(p, s * 0.8 - 0.1, s * 0.8 + 0.1, 0.9, 1.6, L, L + 0.03, "T_TailLight")
    box(p, -0.55, 0.55, 2.85, 3.0, -1.2, 1.2, "T_White", 0.05)                      # klima kutusu
    box(p, -W - 0.02, W + 0.02, 0.32, 0.5, -L - 0.12, -L + 0.05, "T_Metal", 0.03)
    for z in (-2.8, 2.9):
        for s in (-1, 1):
            wheel(p, s * (W - 0.14), z, 0.48, 0.3)
    return finish(p, "CityBus")


def pat_pat():
    p = []
    box(p, -0.62, 0.62, 0.45, 0.92, -0.5, 1.3, "T_PaintRed", 0.04)                 # yük kasası
    for x in (-0.3, 0.3):                                                           # çay kasaları
        for z in (-0.2, 0.45, 1.0):
            box(p, x - 0.26, x + 0.26, 0.92, 1.12, z - 0.25, z + 0.25, "T_Wood", 0.02)
    box(p, -0.32, 0.32, 0.4, 0.95, -1.3, -0.5, "T_Umbrella", 0.06)                  # motor gövdesi
    box(p, -0.18, 0.18, 0.95, 1.05, -0.95, -0.6, "T_Black", 0.02)                   # sele
    box(p, -0.38, 0.38, 1.08, 1.12, -1.25, -1.2, "T_Chrome")                        # gidon
    box(p, -0.04, 0.04, 0.9, 1.1, -1.24, -1.18, "T_Chrome")
    box(p, -0.1, 0.1, 0.72, 0.84, -1.32, -1.29, "T_Lamp")
    wheel(p, 0.0, -1.05, 0.26, 0.14)
    for s in (-1, 1):
        wheel(p, s * 0.62, 0.9, 0.28, 0.16)
    return finish(p, "PatPat")


def tractor():
    p = []
    W = 1.0
    ramp(p, 0.0, 3.0, 0.1, 1.6, 0.85)                                               # römork arka kapağı
    box(p, -W, W, 0.55, 1.6, 3.0, 6.6, "T_Wood", 0.03)                              # römork
    for s in (-1, 1):
        box(p, *sorted((s * W, s * (W - 0.05))), 1.6, 1.88, 3.0, 6.6, "T_Orange", 0.02)   # alçak yan parmaklık
        wheel(p, s * (W - 0.1), 4.8, 0.45, 0.26)
        box(p, *sorted((s * 0.42, s * 0.92)), 1.6, 2.1, 5.95, 6.55, "T_Hay", 0.06)        # saman balyaları (kenarda)
    box(p, -0.12, 0.12, 0.45, 0.6, 6.6, 7.3, "T_Metal")                             # çeki demiri
    box(p, -0.55, 0.55, 0.7, 1.35, 7.2, 9.5, "T_Orange", 0.08)                      # gövde + kaput
    box(p, -0.62, 0.62, 1.35, 1.42, 7.2, 8.1, "T_Metal")
    for x in (-0.6, 0.6):                                                           # kabin dikmeleri
        for z in (7.25, 8.05):
            box(p, x - 0.04, x + 0.04, 1.4, 2.5, z - 0.04, z + 0.04, "T_Black")
    box(p, -0.7, 0.7, 2.5, 2.6, 7.15, 8.15, "T_Orange", 0.03)                       # kabin tavanı
    box(p, -0.56, 0.56, 1.45, 2.45, 8.04, 8.06, "T_Glass")
    box(p, 0.25, 0.33, 1.35, 2.2, 8.8, 8.88, "T_Black")                             # egzoz
    box(p, -0.3, 0.3, 0.85, 1.15, 9.5, 9.53, "T_Metal")                             # ızgara
    for s in (-1, 1):
        wheel(p, s * 0.95, 7.6, 0.78, 0.42)                                         # büyük arka tekerlek
        wheel(p, s * 0.72, 9.05, 0.42, 0.24)
        box(p, s * 0.42 - 0.08, s * 0.42 + 0.08, 1.1, 1.24, 9.5, 9.53, "T_Lamp")
    return finish(p, "Tractor")


# ---------------------------------------------------------------------------
# Tezgâhlar, durak, mangal
# ---------------------------------------------------------------------------
def bus_stop():
    p = []
    for x in (-0.85, 0.85):
        for z in (-1.4, 1.4):
            box(p, x - 0.05, x + 0.05, 0.0, 2.5, z - 0.05, z + 0.05, "T_Metal")
    box(p, -0.95, 0.95, 2.5, 2.62, -1.55, 1.55, "T_CityGreen", 0.04)                # çatı
    box(p, 0.8, 0.86, 0.3, 2.4, -1.35, 1.35, "T_Glass")                             # arka cam
    box(p, -0.86, -0.8, 0.3, 2.4, -1.35, 1.35, "T_Glass")
    box(p, -0.5, 0.5, 0.45, 0.52, -1.2, 1.2, "T_Wood")                              # bank
    box(p, -0.84, -0.7, 0.4, 2.1, -0.6, 0.6, "T_Lamp")                              # ışıklı reklam panosu
    box(p, -0.5, 0.5, 2.62, 2.95, -0.04, 0.04, "T_Yellow", 0.02)                    # DURAK tabelası
    return finish(p, "BusStop")


def tea_cart():
    p = []
    box(p, -0.55, 0.55, 0.5, 1.05, -0.65, 0.65, "T_PaintRed", 0.05)
    box(p, -0.6, 0.6, 1.05, 1.1, -0.7, 0.7, "T_Wood")
    for s in (-1, 1):
        wheel(p, s * 0.6, 0.0, 0.45, 0.08)
    cyl(p, 0.2, 1.1, 1.55, 0.15, 0.18, "T_Copper")                                 # semaver
    cone(p, 0.2, 1.55, 1.68, 0.15, 0.12, 0.05, "T_Copper")
    for i in range(5):                                                              # ince belli çay bardakları
        cyl(p, -0.35 + i * 0.11, 1.1, 1.2, -0.35, 0.035, "T_TailLight", 8)
    cyl(p, -0.4, 1.1, 2.1, 0.45, 0.025, "T_Metal", 8)                               # şemsiye direği
    cone(p, -0.4, 2.0, 2.35, 0.45, 0.95, 0.05, "T_Umbrella")                       # şemsiye
    return finish(p, "TeaCart")


def simit_cart():
    p = []
    box(p, -0.5, 0.5, 0.55, 0.85, -0.6, 0.6, "T_PaintRed", 0.04)
    box(p, -0.5, 0.5, 0.85, 1.45, -0.6, 0.6, "T_Glass")                             # camlı kutu
    for i in range(3):
        for j in range(4):
            ell(p, -0.25 + i * 0.25, 1.0 + (j % 2) * 0.16, -0.4 + j * 0.27, 0.1, 0.035, 0.1, "T_Simit", 10, 4)
    box(p, -0.52, 0.52, 1.45, 1.52, -0.62, 0.62, "T_PaintRed", 0.02)
    for s in (-1, 1):
        wheel(p, s * 0.52, -0.3, 0.3, 0.07)
    box(p, -0.04, 0.04, 0.0, 0.55, 0.5, 0.56, "T_Metal")
    box(p, -0.45, 0.45, 1.52, 1.85, -0.03, 0.03, "T_Yellow", 0.02)                  # SİMİT tabelası
    return finish(p, "SimitCart")


def melon_stall():
    p = []
    box(p, -0.85, 0.85, 0.72, 0.8, -0.7, 0.7, "T_Wood", 0.02)                       # tezgâh
    for x in (-0.78, 0.78):
        for z in (-0.62, 0.62):
            box(p, x - 0.04, x + 0.04, 0.0, 0.72, z - 0.04, z + 0.04, "T_Wood")
    k = 0
    for layer, (n, hc) in enumerate(((3, 0.98), (2, 1.36), (1, 1.72))):            # karpuz piramidi
        for i in range(n):
            for j in range(n):
                off = (n - 1) / 2
                ell(p, (i - off) * 0.42, hc, (j - off) * 0.42, 0.2, 0.18, 0.24, "T_Melon" if k % 2 else "T_MelonDark", 10, 6)
                k += 1
    ell(p, 0.55, 0.92, -0.5, 0.18, 0.14, 0.18, "T_MelonRed" if "T_MelonRed" in bpy.data.materials else "T_PaintRed", 10, 6)
    cyl(p, 0.7, 0.8, 2.3, 0.55, 0.03, "T_Metal", 8)
    cone(p, 0.0, 2.15, 2.5, 0.0, 1.15, 0.06, "T_Umbrella")
    return finish(p, "MelonStall")


def kebab_grill():
    p = []
    box(p, -0.75, 0.75, 0.0, 0.75, -0.55, 0.55, "T_Metal", 0.03)                    # gövde
    box(p, -0.7, 0.7, 0.75, 0.82, -0.5, 0.5, "T_Coal")                              # kor
    for i in range(7):                                                              # şişler
        box(p, -0.72, 0.72, 0.86, 0.88, -0.42 + i * 0.14, -0.4 + i * 0.14, "T_Chrome")
        for j in range(5):
            box(p, -0.5 + j * 0.25, -0.42 + j * 0.25, 0.85, 0.92, -0.44 + i * 0.14, -0.38 + i * 0.14, "T_Rust")
    for x in (-0.75, 0.75):                                                         # davlumbaz direkleri
        box(p, x - 0.04, x + 0.04, 0.75, 2.0, 0.5, 0.58, "T_Metal")
    box(p, -0.85, 0.85, 1.95, 2.3, -0.6, 0.6, "T_Copper", 0.04)                    # bakır davlumbaz
    cyl(p, 0.0, 2.3, 3.1, 0.0, 0.14, "T_Metal")                                    # baca
    box(p, -0.7, 0.7, 2.3, 2.6, 0.55, 0.6, "T_Board" if "T_Board" in bpy.data.materials else "T_Black", 0.02)
    return finish(p, "KebabGrill")


# ---------------------------------------------------------------------------
# Kayılacak askılar ve kemer
# ---------------------------------------------------------------------------
def kilim_line():
    p = []
    for x in (-1.05, 1.05):
        cyl(p, x, 0.0, 2.55, 0.0, 0.06, "T_Wood", 10)
    box(p, -1.08, 1.08, 2.3, 2.33, -0.015, 0.015, "T_Rust")                        # ip
    box(p, -0.92, 0.92, 1.2, 2.28, -0.02, 0.02, "T_KilimRed")                       # kilim
    for i, (mat, h) in enumerate((("T_KilimGold", 2.1), ("T_KilimBlue", 1.85), ("T_KilimGold", 1.6), ("T_KilimBlue", 1.38))):
        box(p, -0.8, 0.8, h, h + 0.1, -0.025, 0.025, mat)
    for k in range(9):                                                              # göbek motifleri
        x = -0.72 + k * 0.18
        box(p, x - 0.05, x + 0.05, 1.7, 1.8, -0.028, 0.028, "T_Canvas")
    for k in range(14):                                                             # saçak
        x = -0.88 + k * 0.135
        box(p, x - 0.01, x + 0.01, 1.12, 1.2, -0.01, 0.01, "T_Canvas")
    return finish(p, "KilimLine")


def pot_rack():
    p = []
    for x in (-1.05, 1.05):
        box(p, x - 0.05, x + 0.05, 0.0, 2.3, -0.05, 0.05, "T_Wood")
    box(p, -1.1, 1.1, 2.1, 2.18, -0.04, 0.04, "T_Metal")
    for i, x in enumerate((-0.75, -0.25, 0.25, 0.75)):                               # asılı bakır kazanlar
        box(p, x - 0.01, x + 0.01, 1.8, 2.1, -0.01, 0.01, "T_Metal")
        r = 0.22 if i % 2 == 0 else 0.18
        ell(p, x, 1.55, 0.0, r, 0.28, r, "T_Copper", 12, 8)
        box(p, x - r - 0.02, x + r + 0.02, 1.76, 1.8, -0.02, 0.02, "T_Copper")
    return finish(p, "PotRack")


def low_arch():
    """Tek şeritlik alçak kemer: bazalt + kireç taşı dönüşümlü taşlar (almaşık), altından kayılır."""
    p = []
    for s in (-1, 1):
        for k in range(6):
            mat = "T_Basalt" if k % 2 == 0 else "T_Lime"
            box(p, *sorted((s * 0.95, s * 1.25)), k * 0.43, k * 0.43 + 0.43, -0.45, 0.45, mat, 0.01)
    for k in range(8):                                                              # kemer üstü taş sırası
        x0 = -1.25 + k * (2.5 / 8)
        box(p, x0, x0 + 2.5 / 8, 1.15, 2.6, -0.45, 0.45, "T_Basalt" if k % 2 == 0 else "T_Lime", 0.01)
    box(p, -1.32, 1.32, 2.6, 2.75, -0.5, 0.5, "T_Trim", 0.02)                       # söve
    box(p, -0.95, 0.95, 1.12, 1.16, -0.47, -0.43, "T_NeonRed")                      # alt kenar uyarı
    return finish(p, "LowArch")


def tea_table():
    """Ayrı parçalar (Unity Breakable savurur): masa, iki tabure."""
    objs = []
    p = []
    cyl(p, 0.0, 0.0, 0.7, 0.0, 0.04, "T_Metal", 8)
    cyl(p, 0.0, 0.68, 0.73, 0.0, 0.38, "T_Wood", 16)
    for i in range(3):
        cyl(p, -0.15 + i * 0.15, 0.73, 0.83, 0.05 * (i - 1), 0.03, "T_TailLight", 8)
    objs.append(finish(p, "TeaTable_0"))
    for k, x in enumerate((-0.6, 0.6)):
        q = []
        cyl(q, 0.0, 0.0, 0.42, 0.0, 0.03, "T_Metal", 6)
        cyl(q, 0.0, 0.42, 0.46, 0.0, 0.18, "T_PaintRed", 12)
        o = finish(q, f"TeaTable_{k + 1}")
        o.location = P(x, 0.0, 0.1)
        objs.append(o)
    return objs


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bv.make_materials()
    os.makedirs(bv.OUT, exist_ok=True)
    made = []
    for fn, name in ((melon_truck, "MelonTruck"), (city_bus, "CityBus"), (pat_pat, "PatPat"), (tractor, "Tractor"),
                     (bus_stop, "BusStop"), (tea_cart, "TeaCart"), (simit_cart, "SimitCart"), (melon_stall, "MelonStall"),
                     (kebab_grill, "KebabGrill"), (kilim_line, "KilimLine"), (pot_rack, "PotRack"), (low_arch, "LowArch")):
        o = fn()
        export([o], name)
        made.append(o)
    tt = tea_table()
    export(tt, "TeaTable")
    for i, o in enumerate(made + tt):     # blend dosyasında yan yana
        o.location.x += -4.0 * (i + 1)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print("COURSE_ASSETS_OK", [(o.name, len(o.data.polygons)) for o in made + tt])


main()
