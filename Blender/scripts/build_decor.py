"""
Diyarbakır sokağı dekorları — Desktop/Yeni klasör/*.mp4 dekor referansları.
Hepsi low-poly + neon ışık detayı (mavi/pembe/kırmızı emisyon). Her dekor ayrı nesne: Decor_<ad>.
Çalıştır: blender -b --factory-startup -P Blender/scripts/build_decor.py   (ya da MCP exec)
Çıktı: Assets/Art/Environment/Diyarbakir/DiyarbakirDecor.fbx + Blender/source/decor.blend
"""
import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

ROOT = r"D:\Repolar\junpline"
FBX_PATH = os.path.join(ROOT, "Assets", "Art", "Environment", "Diyarbakir", "DiyarbakirDecor.fbx")
BLEND_PATH = os.path.join(ROOT, "Blender", "source", "decor.blend")
TEX = os.path.join(ROOT, "Blender", "textures")
rng = random.Random(5)


def srgb(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


MAT_DEFS = [
    # ad, renk, pürüz, metal, emisyon gücü, doku
    ("D_Copper", "#B8653A", 0.35, 0.9, 0, None),
    ("D_Terracotta", "#A65A36", 0.8, 0.0, 0, None),
    ("D_Wood", "#8A5A34", 0.7, 0.0, 0, "wood.png"),
    ("D_WoodDark", "#3A2618", 0.75, 0.0, 0, "wood.png"),
    ("D_Iron", "#22232A", 0.45, 0.85, 0, None),
    ("D_Stone", "#4A4A50", 0.9, 0.0, 0, "basalt.png"),
    ("D_StoneLight", "#9A958C", 0.85, 0.0, 0, None),
    ("D_Wicker", "#B98A4E", 0.85, 0.0, 0, None),
    ("D_Burlap", "#B8946A", 0.95, 0.0, 0, None),
    ("D_KilimRed", "#9A2A22", 0.9, 0.0, 0, None),
    ("D_KilimGold", "#C8963A", 0.9, 0.0, 0, None),
    ("D_KilimDark", "#2A1E2A", 0.9, 0.0, 0, None),
    ("D_Melon", "#3E7A2E", 0.6, 0.0, 0, None),
    ("D_MelonDark", "#1F4A1A", 0.6, 0.0, 0, None),
    ("D_Bread", "#C99A55", 0.8, 0.0, 0, None),
    ("D_White", "#E8ECF2", 0.5, 0.0, 0, None),
    ("D_NazarBlue", "#1B3FAA", 0.3, 0.0, 0, None),
    ("D_NazarLight", "#6FB8FF", 0.3, 0.0, 0, None),
    ("D_FruitRed", "#C0262E", 0.5, 0.0, 0, None),
    ("D_FruitYellow", "#E8C23A", 0.5, 0.0, 0, None),
    ("D_FruitOrange", "#E8782A", 0.5, 0.0, 0, None),
    ("D_FruitPurple", "#4A2A6A", 0.5, 0.0, 0, None),
    ("D_GlowBlue", "#55CFFF", 0.3, 0.0, 8, None),
    ("D_GlowPink", "#FF4F9A", 0.3, 0.0, 8, None),
    ("D_GlowRed", "#FF2A3A", 0.3, 0.0, 8, None),
]
NAMES = [m[0] for m in MAT_DEFS]
M = {n: i for i, n in enumerate(NAMES)}


def make_materials():
    for name, col, rough, metal, emit, tex in MAT_DEFS:
        m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        m.use_nodes = True
        nt = m.node_tree
        b = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        b.inputs["Base Color"].default_value = srgb(col)
        b.inputs["Roughness"].default_value = rough
        b.inputs["Metallic"].default_value = metal
        if emit:
            b.inputs["Emission Color"].default_value = srgb(col)
            b.inputs["Emission Strength"].default_value = emit
        if tex and os.path.exists(os.path.join(TEX, tex)):
            node = nt.nodes.new("ShaderNodeTexImage")
            node.image = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            mix.inputs[6].default_value = srgb(col)
            nt.links.new(node.outputs["Color"], mix.inputs[7])
            nt.links.new(mix.outputs[2], b.inputs["Base Color"])
        m.diffuse_color = srgb(col)


class B:
    """Tek nesne için geometri biriktirici; xf: o anki dönüşüm."""

    def __init__(self):
        self.bm = bmesh.new()
        self.xf = Matrix.Identity(4)

    def _add(self, geom_verts, mat):
        bmesh.ops.transform(self.bm, matrix=self.xf, verts=geom_verts)
        for f in {f for v in geom_verts for f in v.link_faces}:
            f.material_index = M[mat]

    def at(self, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
        self.xf = Matrix.Translation(loc) @ (Matrix.Rotation(rot[2], 4, "Z") @ Matrix.Rotation(rot[1], 4, "Y")
                                             @ Matrix.Rotation(rot[0], 4, "X")) @ Matrix.Scale(scale, 4)
        return self

    def box(self, c, s, mat):
        r = bmesh.ops.create_cube(self.bm, size=1.0)
        v = r["verts"]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(c) @ Matrix.Diagonal((*s, 1)), verts=v)
        self._add(v, mat)

    def cyl(self, c, r, h, mat, segs=12, r2=None, axis="Z"):
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, segments=segs, radius1=r, radius2=r if r2 is None else r2, depth=h)
        v = res["verts"]
        rot = Matrix.Identity(4) if axis == "Z" else Matrix.Rotation(math.pi / 2, 4, "Y" if axis == "X" else "X")
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(c) @ rot, verts=v)
        self._add(v, mat)

    def sphere(self, c, r, mat, sub=2, scale=(1, 1, 1)):
        res = bmesh.ops.create_icosphere(self.bm, subdivisions=sub, radius=r)
        v = res["verts"]
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(c) @ Matrix.Diagonal((*scale, 1)), verts=v)
        self._add(v, mat)

    def torus(self, c, R, r, mat, segs=20, rsegs=6, axis="Z"):
        verts, rings = [], []
        for i in range(segs):
            a = 2 * math.pi * i / segs
            ring = []
            for j in range(rsegs):
                b = 2 * math.pi * j / rsegs
                p = Vector(((R + r * math.cos(b)) * math.cos(a), (R + r * math.cos(b)) * math.sin(a), r * math.sin(b)))
                ring.append(self.bm.verts.new(p))
            rings.append(ring)
        for i in range(segs):
            for j in range(rsegs):
                a, b2 = rings[i], rings[(i + 1) % segs]
                self.bm.faces.new((a[j], b2[j], b2[(j + 1) % rsegs], a[(j + 1) % rsegs]))
        allv = [v for ring in rings for v in ring]
        rot = Matrix.Identity(4) if axis == "Z" else Matrix.Rotation(math.pi / 2, 4, "Y" if axis == "X" else "X")
        bmesh.ops.transform(self.bm, matrix=Matrix.Translation(c) @ rot, verts=allv)
        self._add(allv, mat)

    def lathe(self, prof, mat, segs=14, c=(0, 0, 0), mats=None):
        """prof: [(r, z), ...] alttan üste; mats: bant başına malzeme listesi (isteğe bağlı)."""
        rings = []
        for r, z in prof:
            rings.append([self.bm.verts.new((c[0] + r * math.cos(2 * math.pi * i / segs),
                                             c[1] + r * math.sin(2 * math.pi * i / segs), c[2] + z)) for i in range(segs)])
        allv = [v for ring in rings for v in ring]
        new_faces = []
        for k in range(len(rings) - 1):
            for i in range(segs):
                j = (i + 1) % segs
                f = self.bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
                f.material_index = M[mats[k] if mats and k < len(mats) else mat]
                new_faces.append(f)
        if prof[0][0] > 0.001:
            self.bm.faces.new(list(reversed(rings[0]))).material_index = M[mat]
        if prof[-1][0] > 0.001:
            self.bm.faces.new(rings[-1]).material_index = M[mat]
        bmesh.ops.transform(self.bm, matrix=self.xf, verts=allv)

    def finish(self, name, location):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for n in NAMES:
            me.materials.append(bpy.data.materials[n])
        for p in me.polygons:
            p.use_smooth = False
        o = bpy.data.objects.new(name, me)
        bpy.context.collection.objects.link(o)
        o.location = location
        return o


# ---------------------------------------------------------------------------
# Dekorlar (her biri yere oturur, z=0 taban)
# ---------------------------------------------------------------------------
def samovar(b):
    b.lathe([(0.22, 0), (0.2, 0.08), (0.1, 0.12), (0.12, 0.22), (0.24, 0.3), (0.27, 0.5), (0.26, 0.7), (0.2, 0.82),
             (0.12, 0.86), (0.08, 0.95), (0.11, 1.0), (0.05, 1.08), (0.0, 1.1)], "D_Copper", 16)
    b.cyl((0, 0, 0.04), 0.24, 0.06, "D_WoodDark", 16)
    b.torus((0, 0, 0.5), 0.272, 0.012, "D_GlowBlue", 24)
    b.torus((0, 0, 0.66), 0.265, 0.01, "D_GlowBlue", 24)
    b.cyl((0.0, -0.3, 0.42), 0.025, 0.14, "D_Copper", 8, axis="Y")              # musluk
    for s in (-1, 1):
        b.torus((s * 0.3, 0, 0.62), 0.06, 0.012, "D_Copper", 10, 5, axis="Y")  # kulplar
    b.lathe([(0.04, 1.08), (0.06, 1.2), (0.09, 1.3), (0.0, 1.32)], "D_Copper", 10)  # demlik
    b.sphere((0, 0, 1.45), 0.07, "D_GlowPink", 1, (1, 1, 1.4))                       # buhar
    b.sphere((0.05, 0.02, 1.6), 0.05, "D_GlowPink", 1, (1, 1, 1.5))


def jug(b, c, scale, mat="D_Terracotta"):
    b.at(c, (0, 0, rng.uniform(0, 6)), scale)
    b.lathe([(0.12, 0), (0.2, 0.08), (0.22, 0.22), (0.17, 0.36), (0.07, 0.45), (0.06, 0.55), (0.09, 0.58)], mat, 12)
    b.torus((0, 0, 0.22), 0.222, 0.008, "D_GlowBlue", 14, 4)
    b.torus((0.12, 0, 0.42), 0.07, 0.012, mat, 8, 4, axis="Y")
    b.at()


def copper_jugs(b):
    for c, s in (((0, 0, 0), 1.0), ((0.38, 0.1, 0), 0.85), ((-0.36, 0.08, 0), 0.9), ((0.05, 0.32, 0), 0.8),
                 ((0.02, 0.05, 0.55), 0.8), ((0.3, 0.15, 0.48), 0.65)):
        jug(b, c, s)


def copper_plates(b):
    for k in range(4):
        z = k * 0.13
        b.lathe([(0.0, z), (0.32, z), (0.42, z + 0.05), (0.44, z + 0.07), (0.33, z + 0.025), (0.0, z + 0.025)], "D_Copper", 20)
        b.torus((0, 0, z + 0.04), 0.38, 0.008, "D_GlowBlue", 28, 4)
        b.torus((0, 0, z + 0.03), 0.2, 0.006, "D_KilimGold", 20, 4)


def crate(b, c, s, rot=0.0):
    b.at(c, (0, 0, rot))
    b.box((0, 0, s / 2), (s, s, s), "D_Wood")
    for x in (-1, 1):
        for y in (-1, 1):
            b.box((x * s / 2, y * s / 2, s / 2), (0.05, 0.05, s + 0.02), "D_WoodDark")   # kenar dikmeleri
    for z in (0.03, s - 0.03):
        b.box((0, -s / 2, z), (s + 0.02, 0.04, 0.06), "D_WoodDark")
        b.box((0, s / 2, z), (s + 0.02, 0.04, 0.06), "D_WoodDark")
    b.box((0, -s / 2 - 0.005, s / 2), (s * 0.55, 0.01, s * 0.3), "D_StoneLight")        # etiket
    b.box((0, -s / 2 - 0.01, s / 2 - s * 0.06), (s * 0.4, 0.01, 0.02), "D_KilimRed")
    b.at()


def crates(b):
    crate(b, (0, 0, 0), 0.7, 0.05)
    crate(b, (0.75, 0.05, 0), 0.6, -0.1)
    crate(b, (0.2, 0.05, 0.7), 0.55, 0.25)


def chest(b):
    b.box((0, 0, 0.25), (1.0, 0.6, 0.5), "D_WoodDark")
    b.cyl((0, 0, 0.5), 0.3, 1.0, "D_WoodDark", 12, axis="X")  # yarım silindir kapak
    for x in (-0.38, 0.0, 0.38):
        b.box((x, 0, 0.26), (0.06, 0.62, 0.52), "D_Iron")
    b.box((0, -0.31, 0.42), (0.14, 0.03, 0.16), "D_Iron")
    b.box((0, -0.33, 0.42), (0.06, 0.02, 0.06), "D_GlowBlue")
    b.box((0, 0, 0.5), (1.02, 0.62, 0.02), "D_GlowBlue")


def pillar(b):
    b.box((0, 0, 0.9), (0.5, 0.5, 1.8), "D_Stone")
    b.box((0, 0, 1.85), (0.62, 0.62, 0.1), "D_StoneLight")
    b.box((0, 0, 2.05), (0.4, 0.4, 0.3), "D_Stone")
    b.lathe([(0.3, 2.2), (0.0, 2.45)], "D_Stone", 4)
    for side in range(4):
        a = side * math.pi / 2
        b.at()
        for k, (u, v, w, h) in enumerate(((0, 1.0, 0.03, 0.5), (0, 0.45, 0.03, 0.4), (0, 1.55, 0.03, 0.3))):
            b.at((0, 0, 0), (0, 0, a)).box((u, -0.255, v), (w, 0.012, h), "D_GlowRed")
        b.at((0, 0, 0), (0, 0, a)).box((0, -0.256, 1.0), (0.22, 0.012, 0.03), "D_GlowRed")
        b.at()


def kilim_pile(b):
    for k in range(5):
        z = k * 0.07
        w, d = 1.3 - k * 0.06, 0.9 - k * 0.05
        b.at((rng.uniform(-0.04, 0.04), rng.uniform(-0.04, 0.04), 0), (0, 0, rng.uniform(-0.12, 0.12)))
        b.box((0, 0, z + 0.03), (w, d, 0.06), "D_KilimRed")
        b.box((0, 0, z + 0.061), (w * 0.7, d * 0.6, 0.004), "D_KilimGold")
        b.box((0, 0, z + 0.064), (w * 0.35, d * 0.3, 0.004), "D_KilimDark")
        b.box((0, -d / 2 - 0.004, z + 0.03), (w, 0.01, 0.02), "D_KilimGold")
        b.at()
    b.box((0.1, 0, 0.37), (0.9, 0.012, 0.012), "D_GlowBlue")


def iron_gate(b):
    for k in range(9):
        x = -0.8 + k * 0.2
        h = 1.6 + 0.25 * math.cos((k - 4) / 4 * math.pi / 2)
        b.box((x, 0, h / 2), (0.04, 0.04, h), "D_Iron")
        b.lathe([(0.04, h), (0.0, h + 0.14)], "D_Iron", 6, c=(x, 0, 0))
        b.sphere((x, 0, h + 0.24), 0.05, "D_GlowBlue", 1, (0.7, 0.7, 1.6))
    for z in (0.15, 0.9, 1.45):
        b.box((0, 0, z), (1.7, 0.05, 0.06), "D_Iron")
    for s in (-1, 1):
        b.box((s * 0.95, 0, 1.0), (0.14, 0.14, 2.0), "D_Iron")
        b.box((s * 0.95, -0.075, 1.0), (0.02, 0.01, 1.6), "D_GlowBlue")
        for k in range(3):
            b.torus((s * 0.45, 0, 0.55 + k * 0.01), 0.18, 0.015, "D_Iron", 14, 4, axis="Y")


def ladder(b):
    tilt = math.radians(14)
    b.at((0, 0, 0), (tilt, 0, 0))
    for s in (-1, 1):
        b.box((s * 0.25, 0, 1.25), (0.07, 0.06, 2.5), "D_Wood")
    for k in range(8):
        z = 0.25 + k * 0.3
        b.cyl((0, 0, z), 0.025, 0.5, "D_WoodDark", 6, axis="X")
        b.torus((0.25, 0, z), 0.045, 0.01, "D_GlowRed", 8, 4)
    b.at()


def basket(b, fill="fruit"):
    b.lathe([(0.2, 0), (0.26, 0.04), (0.32, 0.22), (0.34, 0.3), (0.3, 0.3), (0.28, 0.24), (0.2, 0.06)], "D_Wicker", 16)
    for k in range(3):
        b.torus((0, 0, 0.07 + k * 0.08), 0.27 + k * 0.025, 0.01, "D_WoodDark", 18, 4)
    b.torus((0, 0, 0.12), 0.29, 0.007, "D_GlowBlue" if fill == "fruit" else "D_GlowPink", 20, 4)
    if fill == "fruit":
        b.torus((0, 0, 0.3), 0.3, 0.02, "D_Wicker", 18, 4, axis="X")  # sap
        for k in range(14):
            a = rng.uniform(0, 6.28)
            r = rng.uniform(0, 0.22)
            mat = rng.choice(("D_FruitRed", "D_FruitYellow", "D_FruitOrange", "D_FruitPurple", "D_Melon"))
            b.sphere((r * math.cos(a), r * math.sin(a), 0.28 + rng.uniform(0, 0.08)), rng.uniform(0.05, 0.08), mat, 1)
    else:
        for k in range(7):
            a = k / 7 * 6.28
            b.at((0.1 * math.cos(a), 0.1 * math.sin(a), 0.3), (rng.uniform(0.6, 1.0), 0, a))
            b.cyl((0, 0, 0), 0.16, 0.05, "D_Bread", 12, r2=0.14)
            b.at()


def wall_lantern(b):
    b.box((0, 0.3, 0.0), (0.12, 0.04, 0.3), "D_Iron")
    b.box((0, 0.15, 0.1), (0.03, 0.3, 0.03), "D_Iron")
    b.lathe([(0.08, -0.3), (0.12, -0.18), (0.12, 0.02), (0.06, 0.1), (0.0, 0.16)], "D_Iron", 6, c=(0, 0, 0))
    b.cyl((0, 0, -0.08), 0.1, 0.18, "D_GlowRed", 6)
    b.torus((0, 0, 0.15), 0.03, 0.008, "D_Iron", 8, 4, axis="X")


def sack(b):
    b.lathe([(0.0, 0), (0.28, 0.02), (0.36, 0.25), (0.33, 0.5), (0.18, 0.66), (0.12, 0.72), (0.16, 0.82), (0.08, 0.86), (0.0, 0.86)],
            "D_Burlap", 12)
    b.torus((0, 0, 0.71), 0.13, 0.025, "D_WoodDark", 12, 4)
    b.box((0, -0.34, 0.33), (0.36, 0.01, 0.18), "D_White")
    b.torus((0, 0, 0.02), 0.45, 0.015, "D_GlowRed", 28, 4)


def watermelon(b):
    # sehpa
    b.box((0, 0, 0.32), (0.6, 0.6, 0.05), "D_Wood")
    for x in (-1, 1):
        for y in (-1, 1):
            b.box((x * 0.25, y * 0.25, 0.15), (0.05, 0.05, 0.3), "D_WoodDark")
    b.sphere((0, 0, 0.72), 0.42, "D_Melon", 1)
    for k in range(6):
        b.at((0, 0, 0.72), (0, 0, k * math.pi / 6))
        b.torus((0, 0, 0), 0.425, 0.012, "D_MelonDark", 20, 4, axis="X")
        b.at()
    b.torus((0, 0, 0.72), 0.43, 0.006, "D_GlowPink", 24, 4)
    b.cyl((0, 0, 1.16), 0.015, 0.06, "D_WoodDark", 5)


def rolled_carpet(b):
    b.cyl((0, 0, 0.5), 0.32, 1.0, "D_KilimRed", 16)
    for z in (0.2, 0.8):
        b.cyl((0, 0, z), 0.322, 0.1, "D_KilimGold", 16)
    b.cyl((0, 0, 0.5), 0.33, 0.14, "D_KilimDark", 16)
    b.torus((0, 0, 0.44), 0.335, 0.008, "D_GlowBlue", 24, 4)
    b.torus((0, 0, 0.56), 0.335, 0.008, "D_GlowBlue", 24, 4)
    b.cyl((0, 0, 1.0), 0.18, 0.01, "D_KilimDark", 16)


def birdhouse(b):
    b.box((0, 0, 0.3), (0.5, 0.45, 0.6), "D_StoneLight")
    for s in (-1, 1):
        b.at((0, 0, 0.6), (0, s * math.radians(38), 0))
        b.box((s * 0.18, 0, 0.12), (0.4, 0.55, 0.04), "D_NazarBlue")
        b.at()
    for x, z in ((-0.12, 0.35), (0.12, 0.35), (0, 0.15)):
        b.box((x, -0.23, z), (0.09, 0.01, 0.14), "D_GlowBlue")
    b.box((0, 0, 0.02), (0.6, 0.55, 0.04), "D_Stone")


def nazar(b):
    b.at((0, 0, 0.45), (math.pi / 2, 0, 0))
    b.cyl((0, 0, 0), 0.42, 0.08, "D_NazarBlue", 24)
    b.cyl((0, 0, -0.045), 0.3, 0.02, "D_White", 24)
    b.cyl((0, 0, -0.055), 0.2, 0.02, "D_NazarLight", 24)
    b.cyl((0, 0, -0.065), 0.1, 0.02, "D_KilimDark", 20)
    b.cyl((0, 0, -0.072), 0.035, 0.01, "D_GlowBlue", 12)
    b.torus((0, 0, 0), 0.43, 0.012, "D_GlowBlue", 32, 4)
    b.at()


def kebab_cart(b):
    b.box((0, 0, 0.75), (1.2, 0.6, 0.5), "D_White")
    b.box((0, -0.305, 0.75), (1.1, 0.01, 0.35), "D_KilimRed")
    b.box((0, 0, 1.03), (1.24, 0.64, 0.06), "D_Iron")
    b.box((0, 0, 1.1), (1.0, 0.45, 0.08), "D_WoodDark")              # ızgara tabanı
    for k in range(5):
        b.cyl((-0.4 + k * 0.2, 0, 1.17), 0.012, 0.5, "D_Iron", 5, axis="Y")
        for j in range(4):
            b.box((-0.4 + k * 0.2, -0.15 + j * 0.1, 1.19), (0.05, 0.05, 0.05), "D_FruitRed")
    for s in (-1, 1):
        b.torus((s * 0.45, -0.32, 0.3), 0.26, 0.03, "D_WoodDark", 14, 4, axis="Y")
        b.cyl((s * 0.45, -0.32, 0.3), 0.04, 0.05, "D_Iron", 8, axis="Y")
        b.torus((s * 0.45, -0.34, 0.3), 0.27, 0.008, "D_GlowBlue", 16, 4, axis="Y")
    b.cyl((0.58, 0.32, 0.5), 0.03, 0.3, "D_Iron", 6)
    b.cyl((-0.75, 0, 0.95), 0.02, 0.5, "D_Iron", 6, axis="X")         # kol
    b.box((0, 0, 0.5), (1.2, 0.6, 0.01), "D_GlowBlue")
    for k in range(3):
        b.sphere((-0.2 + k * 0.2, 0, 1.45 + k * 0.08), 0.1, "D_White", 1, (1, 1, 1.4))  # duman


def fountain(b):
    b.lathe([(0.0, 0), (0.95, 0), (0.95, 0.35), (0.85, 0.35), (0.85, 0.1), (0.0, 0.1)], "D_Stone", 8)
    b.cyl((0, 0, 0.27), 0.84, 0.02, "D_GlowBlue", 8)                   # su yüzeyi
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        x, y = 0.6 * math.cos(a), 0.6 * math.sin(a)
        b.box((x, y, 0.9), (0.18, 0.18, 1.2), "D_StoneLight")
    for k in range(4):
        a = k * math.pi / 2
        b.at((0, 0, 1.5), (0, 0, a))
        b.torus((0, -0.6, 0), 0.42, 0.06, "D_Stone", 12, 4, axis="Y")
        b.at()
    b.box((0, 0, 1.6), (1.1, 1.1, 0.12), "D_Stone")
    b.lathe([(0.55, 1.66), (0.0, 2.2)], "D_Stone", 8)
    b.lathe([(0.12, 0.3), (0.08, 0.7), (0.03, 1.3), (0.0, 1.35)], "D_GlowBlue", 10)  # su sütunu
    b.lathe([(0.0, 0.7), (0.3, 0.75), (0.25, 0.8), (0.0, 0.8)], "D_StoneLight", 10)


DECOR = [
    ("Samovar", samovar), ("CopperJugs", copper_jugs), ("CopperPlates", copper_plates), ("Crates", crates),
    ("Chest", chest), ("CarvedPillar", pillar), ("KilimPile", kilim_pile), ("IronGate", iron_gate),
    ("Ladder", ladder), ("FruitBasket", lambda b: basket(b, "fruit")), ("WallLantern", wall_lantern), ("Sack", sack),
    ("Watermelon", watermelon), ("BreadBasket", lambda b: basket(b, "bread")), ("RolledCarpet", rolled_carpet),
    ("Birdhouse", birdhouse), ("Nazar", nazar), ("KebabCart", kebab_cart), ("Fountain", fountain),
]


def build():
    for o in list(bpy.data.objects):
        if o.name.startswith("Decor_"):
            bpy.data.objects.remove(o, do_unlink=True)
    make_materials()
    objs = []
    for i, (name, fn) in enumerate(DECOR):
        b = B()
        fn(b)
        x, y = (i % 5) * 2.6, -(i // 5) * 2.6
        o = b.finish(f"Decor_{name}", (x, y, 0.0 if name != "WallLantern" else 1.2))
        objs.append(o)
    return objs


def export(objs):
    os.makedirs(os.path.dirname(FBX_PATH), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="COPY")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH, copy=True)


_objs = build()
export(_objs)
print("DECOR_OK", [(o.name, len(o.data.polygons)) for o in _objs])
