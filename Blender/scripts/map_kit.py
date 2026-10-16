"""JUMPLINE GO harita tasarım kiti (16 Eki 2026) — kullanıcıyla Blender'da yapılan referans haritanın tarzında,
yalnız Meshy modelleriyle, geçilebilirliği simülasyonla kanıtlanmış haritalar üretir.

Kullanım:  blender -b -P map_kit.py -- [adet=54] [baslangic=1]
Çıktı:     Blender/Harita/Tasarim_Haritalar.blend   (her harita bir koleksiyon, yan yana; modeller paylaşılır)
           Assets/Data/Designed/Harita_NN.json        (Unity'ye aktarım: model, konum, dönüş, ölçek, tür, portal)
           D:/tmp/design_prev/Harita_NN_*.png         (önizleme: koşucu gözü + üstten)

Koordinat: Blender +Y koşu yönü (Unity +Z), x şerit (sol -2.2, orta 0, sağ +2.2), z yukarı. Yol ortası x = 0.

Tarz (kullanıcı): dolu ama orantılı · araçlar park, önü bize, yanaşık (0.3 m) · rampa bize · zıplama aralıkları 8 m ·
tavanlar arası boşluk 3 m · kamyondan otobüse kalas · kenarda evler + kule/sur + kubbe+minare, seyrek tezgâh/fener,
çatı ışıkları, 30–40 m'de bir karşıdan karşıya ışık zinciri · sadece Meshy (kalas hariç).
"""
import bpy, math, random, json, os, sys
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
OBS = ROOT + r"\Assets\Art\Environment\Obstacles"
MESHY_FBX = ROOT + r"\Assets\Art\Environment\Meshy"
PROC = ROOT + r"\Blender\meshy_processed"
OUT_BLEND = ROOT + r"\Blender\Harita\Tasarim_Haritalar.blend"
OUT_JSON = ROOT + r"\Assets\Data\Designed"
PREV = r"D:\tmp\design_prev"

LANE = 2.2
FACADE = 5.5            # ev cephesi (yol ortasından)
LANE_EDGE = 3.6         # şerit dışı bant başlangıcı (kaldırım süsü)
MAP_GAP_X = 70.0        # haritalar yan yana

# oyunla uyumlu hareket (PlayerController): basamak 0.35, zıplama tepe 1.9 m, ~5.7 m yol (8 m/sn), kafa 1.7 / kayarken 1.0
STEP = 0.25
STEP_UP = 0.35
JUMP_H = 1.9
JUMP_MIN, JUMP_MAX = 2.5, 5.5
HEAD, SLIDE_HEAD = 1.7, 1.0
LANE_CHANGE = 3.0
BAND = 1.0              # şerit çarpışma yarı genişliği (araç tavanı için)

# ---------------------------------------------------------------------------------------------- model kütüphanesi
LIB_NAME = "0_KUTUPHANE"
FBX_MODELS = {   # oyunun kullandığı FBX (ölçü oyunla aynı)
    "Taxi": OBS + r"\Taxi.fbx", "Minibus": OBS + r"\Minibus.fbx", "CityBus": OBS + r"\CityBus.fbx",
    "RampTruck": OBS + r"\RampTruck.fbx", "MelonTruck": OBS + r"\MelonTruck.fbx", "PatPat": OBS + r"\PatPat.fbx",
    "CrateStack": OBS + r"\CrateStack.fbx", "SlideBar": OBS + r"\SlideBar.fbx", "TeaCart": OBS + r"\TeaCart.fbx",
    "SimitCart": OBS + r"\SimitCart.fbx", "MelonStall": OBS + r"\MelonStall.fbx", "KebabGrill": OBS + r"\KebabGrill.fbx",
    "TeaTable": OBS + r"\TeaTable.fbx", "BusStop": OBS + r"\BusStop.fbx",
    "ClayJug": MESHY_FBX + r"\ClayJug.fbx", "Watermelon": MESHY_FBX + r"\Watermelon.fbx",
}
BLEND_MODELS = ["M03_CornerBuilding", "M09_RooftopBlock", "M07_CityWall", "M08_WallTower", "M10_Minaret", "M11_Dome",
                "M13_BridgeLantern", "M05_StringLights", "M06_StallDecor", "M04_BazaarArch", "M12_BridgeArch", "M15_Boat",
                "MelonStall", "TeaCart", "SimitCart", "KebabGrill", "TeaTable", "ClayJug", "Watermelon"]


def bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return (Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))),
            Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts))))


class Lib:
    def __init__(self):
        self.coll = bpy.data.collections.new(LIB_NAME)
        bpy.context.scene.collection.children.link(self.coll)
        self.coll.hide_viewport = True
        self.coll.hide_render = True
        self.src = {}
        for n, p in FBX_MODELS.items():
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=p)
            new = [o for o in bpy.data.objects if o not in before]
            m = next(o for o in new if o.type == "MESH")
            for o in new:
                for c in list(o.users_collection): c.objects.unlink(o)
                if o is m: self.coll.objects.link(o)
                else: bpy.data.objects.remove(o, do_unlink=True)
            m.parent = None
            self._norm(m, "G_" + n)
        for n in BLEND_MODELS:
            with bpy.data.libraries.load(os.path.join(PROC, n + ".blend")) as (s, d):
                d.objects = list(s.objects)
            m = next(o for o in d.objects if o and o.type == "MESH")
            for o in d.objects:
                if o and o is not m: bpy.data.objects.remove(o, do_unlink=True)
            self.coll.objects.link(m)
            self._norm(m, "D_" + n)
        self.prof_cache = {}

    def _norm(self, m, key):
        m.name = key
        m.rotation_mode = "XYZ"
        m.location = (0, 0, 0)
        bpy.context.view_layer.update()
        self.src[key] = m


# ---------------------------------------------------------------------------------------------- harita kurucu
class MapBuilder:
    def __init__(self, lib, index, name, theme_plan):
        self.lib, self.i, self.name = lib, index, name
        self.coll = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(self.coll)
        self.ox = (index - 1) * MAP_GAP_X           # haritalar yan yana (x)
        self.items, self.portals, self.sections = [], [], []
        self.solids = []      # geçilebilirlik: (lane, y0, y1, kind, data)
        self.objs = {}
        self.theme_plan = theme_plan

    # ---- yerleştirme temeli
    def _copy(self, key, name, rot_z=0.0, scale=(1, 1, 1)):
        src = self.lib.src[key]
        o = src.copy()
        self.coll.objects.link(o)
        o.name = f"{self.name}_{name}"
        o.rotation_euler = (src.rotation_euler.x, src.rotation_euler.y, src.rotation_euler.z + rot_z)
        o.scale = tuple(a * b for a, b in zip(src.scale, scale))
        o.location = (0, 0, 0)
        bpy.context.view_layer.update()
        return o

    def _put(self, o, cx, y_front, z_base):
        mn, mx = bounds(o)
        o.location = (self.ox + cx - (mn.x + mx.x) / 2, y_front - mn.y, z_base - mn.z)
        bpy.context.view_layer.update()
        return bounds(o)

    def _record(self, o, kind, model, lane=None, extra=None):
        mn, mx = bounds(o)
        d = {"m": model, "kind": kind, "lane": lane, "x": round(o.location.x - self.ox, 3), "y": round(o.location.y, 3),
             "z": round(o.location.z, 3), "rz": round(math.degrees(o.rotation_euler.z), 2),
             "s": [round(v, 4) for v in o.scale], "y0": round(mn.y, 2), "y1": round(mx.y, 2), "top": round(mx.z, 2)}
        d["obj"] = o.name
        src = next((v for v in self.lib.src.values() if v.data == o.data), None)
        d["yaw"] = round(math.degrees(o.rotation_euler.z - (src.rotation_euler.z if src else 0.0)), 2)
        d["src"] = "G" if src is not None and src.name.startswith("G_") else "D"
        d["min"] = [round(mn.x - self.ox, 3), round(mn.y, 3), round(mn.z, 3)]
        d["max"] = [round(mx.x - self.ox, 3), round(mx.y, 3), round(mx.z, 3)]
        if extra: d.update(extra)
        self.items.append(d)

    def lx(self, lane):
        return lane * LANE

    # ---- tavan profili (araç / kalas): şerit bandındaki en yüksek nokta, 0.25 m dilimlerle, ön kenardan
    def _profile(self, o, key):
        if key in self.lib.prof_cache:
            return self.lib.prof_cache[key]
        mn, mx = bounds(o)
        cx = (mn.x + mx.x) / 2
        n = int(math.ceil((mx.y - mn.y) / STEP))
        h = [0.0] * n
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            if abs(w.x - cx) > BAND: continue
            i = min(n - 1, max(0, int((w.y - mn.y) / STEP)))
            if w.z - mn.z > h[i]: h[i] = w.z - mn.z
        self.lib.prof_cache[key] = h
        return h

    # ---- engeller (oyundaki tanımlarla aynı ölçü)
    def vehicle(self, model, lane, y, walk=True, toward_us=True, tag=""):
        """Park araç: önü bize (180°). walk=False: üstüne çıkılmaz (oyunda duvar)."""
        o = self._copy("G_" + model, f"{y:05.1f}_{model}_{lane:+d}{tag}", math.pi if toward_us else 0.0)
        mn, mx = self._put(o, self.lx(lane), y, 0.0)
        prof = list(self._profile(o, ("veh", model, toward_us)))
        # rampasız araç: ön yüz (tampon, kaput, cam) tırmanılmaz — tavan başlayana kadar tavan yüksekliğinde dik blok
        top = max(prof)
        i0 = next(i for i, h in enumerate(prof) if h >= 0.9 * top)
        for i in range(i0): prof[i] = prof[i0]
        self.solids.append((lane, mn.y, mx.y, "profile" if walk else "wall", prof))
        self._record(o, "vehicle" if walk else "vehicle_wall", model, lane)
        return o, mn.y, mx.y, max(prof)

    def ramp(self, model, lane, y, tag=""):
        """Rampalı araç (kamyon, karpuz kamyonu): rampası bize — iki yönden hangisinde ön uç alçaksa o."""
        best = None
        for rz in (math.pi, 0.0):
            o = self._copy("G_" + model, "tmp", rz)
            self._put(o, self.lx(lane), y, 0.0)
            prof = self._profile(o, ("ramp", model, rz))
            if best is None or prof[2] < best[1][2]: best = (rz, prof)
            bpy.data.objects.remove(o, do_unlink=True)
        o = self._copy("G_" + model, f"{y:05.1f}_{model}_Rampa_{lane:+d}{tag}", best[0])
        mn, mx = self._put(o, self.lx(lane), y, 0.0)
        self.solids.append((lane, mn.y, mx.y, "profile", best[1]))
        self._record(o, "ramp_vehicle", model, lane)
        return o, mn.y, mx.y, max(best[1])

    JUMPS = {"CrateStack": 1.25, "ClayJug": 1.0, "Watermelon": 0.85, "TeaTable": 0.85, "PatPat": None}

    def jump(self, model, lane, y, z=0.0, h=None):
        """Zıplanan engel (z > 0: araç tavanında)."""
        if model == "PatPat":
            o = self._copy("G_PatPat", f"{y:05.1f}_PatPat_{lane:+d}", math.pi)
        else:
            o = self._copy("G_" + model, f"{y:05.1f}_{model}_{lane:+d}")
            mn, mx = bounds(o)
            hh = h or self.JUMPS[model]
            k = hh / max(0.01, mx.z - mn.z)
            o.scale = tuple(s * k for s in o.scale)
            bpy.context.view_layer.update()
        mn, mx = self._put(o, self.lx(lane), y, z)
        self.solids.append((lane, mn.y, mx.y, "box", mx.z))
        self._record(o, "jump", model, lane)
        return o, mn.y, mx.y

    def slide(self, lane, y, z=0.0):
        """Kayma bariyeri (tek kayılabilir Meshy model): alt kenar z+1.25, üst z+1.63 (üstünden de zıplanır)."""
        o = self._copy("G_SlideBar", f"{y:05.1f}_SlideBar_{lane:+d}")
        mn, mx = bounds(o)
        o.scale = (o.scale.x * 2.30 / (mx.x - mn.x), o.scale.y, o.scale.z * 1.25 / 1.15)
        bpy.context.view_layer.update()
        mn, mx = self._put(o, self.lx(lane), y, z)
        self.solids.append((lane, mn.y, mx.y, "ceiling", (z + 1.25, z + 1.63)))
        self._record(o, "slide", "SlideBar", lane)
        return o, mn.y, mx.y

    DODGE_TURN = {"KebabGrill", "BusStop"}

    def dodge(self, model, lane, y):
        """Kaçılan engel (tezgâh, araba, durak): yerden aşılmaz. Genişse uzun kenarı yol boyunca."""
        o = self._copy("G_" + model, f"{y:05.1f}_{model}_{lane:+d}", math.pi / 2 if model in self.DODGE_TURN else math.pi)
        mn, mx = bounds(o)
        if mx.x - mn.x > 2.3:
            k = 2.3 / (mx.x - mn.x)
            o.scale = tuple(s * k for s in o.scale)
            bpy.context.view_layer.update()
        mn, mx = self._put(o, self.lx(lane), y, 0.0)
        self.solids.append((lane, mn.y, mx.y, "wall", None))
        self._record(o, "dodge", model, lane)
        return o, mn.y, mx.y

    def plank(self, lane, y0, z0, y1, z1, tag=""):
        """Kalas (kullanıcı onayı): bir tavandan ötekine düz geçiş."""
        L = math.hypot(y1 - y0, z1 - z0)
        bpy.ops.mesh.primitive_cube_add(size=1)
        p = bpy.context.active_object
        for c in list(p.users_collection): c.objects.unlink(p)
        self.coll.objects.link(p)
        p.name = f"{self.name}_{y0:05.1f}_Kalas_{lane:+d}{tag}"
        mat = bpy.data.materials.get("Kalas") or bpy.data.materials.new("Kalas")
        mat.diffuse_color = (0.62, 0.5, 0.36, 1)
        p.data.materials.append(mat)
        p.scale = (1.2, L, 0.1)
        p.rotation_euler = (math.atan2(z1 - z0, y1 - y0), 0, 0)
        p.location = (self.ox + self.lx(lane), (y0 + y1) / 2, (z0 + z1) / 2 - 0.05)
        n = int(math.ceil((y1 - y0) / STEP))
        prof = [z0 + (z1 - z0) * (i + 0.5) / n for i in range(n)]
        self.solids.append((lane, y0, y1, "plank", prof))
        self.items.append({"m": "Kalas", "kind": "plank", "lane": lane, "y0": round(y0, 2), "z0": round(z0, 2),
                           "y1": round(y1, 2), "z1": round(z1, 2)})
        return p

    def portal(self, lane, y, z=0.0):
        """Portal yeri (oyunun kendi portalı konur; burada yalnız işaret)."""
        e = bpy.data.objects.new(f"{self.name}_{y:05.1f}_PORTAL_{lane:+d}", None)
        e.empty_display_type = "CIRCLE"
        e.empty_display_size = 1.4
        e.rotation_euler = (math.pi / 2, 0, 0)
        e.location = (self.ox + self.lx(lane), y, z + 1.5)
        self.coll.objects.link(e)
        self.portals.append({"lane": lane, "y": round(y, 2), "z": round(z, 2)})

    def roof_at(self, lane, y):
        """Şeritte y'deki tavan yüksekliği (araç / kalas), yoksa 0."""
        h = 0.0
        for (l, y0, y1, kind, data) in self.solids:
            if l != lane or not (y0 <= y <= y1): continue
            if kind in ("profile", "plank"):
                i = min(len(data) - 1, max(0, int((y - y0) / STEP)))
                h = max(h, data[i])
        return h

    # ---------------------------------------------------------------------------------- zemin (yalnız önizleme; oyunda harita parçasının yolu var)
    def ground(self, y0, y1, theme):
        def mat(name, rgb):
            m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
            m.diffuse_color = (*rgb, 1)
            return m
        def slab(name, x, w, z, m, h=0.1):
            bpy.ops.mesh.primitive_cube_add(size=1)
            p = bpy.context.active_object
            for c in list(p.users_collection): c.objects.unlink(p)
            self.coll.objects.link(p)
            p.name = f"{self.name}_Zemin_{name}_{y0:05.1f}"
            p.scale = (w, y1 - y0, h)
            p.location = (self.ox + x, (y0 + y1) / 2, z - h / 2)
            p.data.materials.append(m)
        road = mat("Z_Yol", (0.30, 0.29, 0.28) if theme != "sur" else (0.42, 0.38, 0.33))
        slab("Yol", 0, 8.8, 0.0, road)
        for x in (-LANE * 1.5, -LANE * 0.5, LANE * 0.5, LANE * 1.5):
            slab(f"Cizgi{x:+.1f}", x, 0.08, 0.012, mat("Z_Cizgi", (0.8, 0.78, 0.72)), 0.02)
        if theme != "sur":
            for sx in (-1, 1):
                slab(f"Kaldirim{sx:+d}", sx * 5.0, 1.2, 0.12, mat("Z_Kaldirim", (0.5, 0.48, 0.45)), 0.14)

    # ---------------------------------------------------------------------------------- kenar dekoru
    def _side(self, key, side, y, x_inner, scale=1.0, name="", z=0.0):
        """Kenar modeli: ön yüzü (model -Y) yola döner, yola bakan kenarı yol ortasından x_inner'de, tabanı z'de."""
        o = self._copy(key, name, math.radians(90 if side < 0 else -90), (scale, scale, scale))
        mn, mx = bounds(o)
        near = mn.x if side > 0 else mx.x
        o.location = ((self.ox + side * x_inner) - near, y - mn.y, z - mn.z)
        bpy.context.view_layer.update()
        return o

    def dress_mahalle(self, rng, y0, y1, mosque=False):
        """Mahalle: cephe (ev/kule/sur, karışık) + isteğe bağlı kubbe+minare + seyrek tezgâh + fener + çatı ışığı + zincir."""
        pool = [("D_M03_CornerBuilding", 0.36), ("D_M09_RooftopBlock", 0.36), ("D_M08_WallTower", 0.18), ("D_M07_CityWall", 0.10)]
        mosque_side = rng.choice((-1, 1)) if mosque else 0
        mosque_y = rng.uniform(y0 + 30, max(y0 + 31, y1 - 30)) if mosque else 1e9
        facade = []
        for side in (-1, 1):
            y = y0 - (2.0 if side < 0 else 0.0) + rng.uniform(-1.5, 1.5)
            last = None
            done = side != mosque_side
            while y < y1:
                if not done and y >= mosque_y - 4:
                    d = self._side("D_M11_Dome", side, y, FACADE + 0.5, 2.6, f"Cami_Kubbe_{side:+d}")
                    _, dmx = bounds(d)
                    m = self._side("D_M10_Minaret", side, dmx.y + 0.3, FACADE + 0.3, 1.0, f"Cami_Minare_{side:+d}")
                    _, mmx = bounds(m)
                    y = mmx.y + 0.5
                    done = True
                    last = None
                    continue
                opts = [(k, w) for k, w in pool if k != last]
                k = rng.choices([a for a, _ in opts], [w for _, w in opts])[0]
                last = k
                o = self._side(k, side, y, FACADE, 1.0, f"Cephe_{side:+d}_{y:05.1f}")
                facade.append(o)
                _, mx = bounds(o)
                y = mx.y - 0.3
        # kaldırım: tezgâhlar sırayla, 14–22 m arayla; bazen yanında tek testi/karpuz; fener 24 m'de bir
        props = ["D_MelonStall", "D_TeaCart", "D_SimitCart", "D_KebabGrill", "D_M06_StallDecor", "D_TeaTable"]
        smalls = {"D_ClayJug": 2.2, "D_Watermelon": 1.5}
        for side in (-1, 1):
            bag = []
            y = y0 + rng.uniform(6, 14) + (0 if side < 0 else 7)
            while y < y1 - 4:
                if not bag:
                    bag = props[:]
                    rng.shuffle(bag)
                k = bag.pop()
                o = self._side(k, side, y, LANE_EDGE, 1.0, f"Kaldirim_{side:+d}_{y:05.1f}")
                mn, mx = bounds(o)
                if mx.x - mn.x > FACADE - LANE_EDGE:
                    s = (FACADE - LANE_EDGE) / (mx.x - mn.x)
                    o.scale = tuple(a * s for a in o.scale)
                    bpy.context.view_layer.update()
                    mn, mx = bounds(o)
                    near = mn.x if side > 0 else mx.x
                    o.location.x += (self.ox + side * LANE_EDGE) - near
                    o.location.z -= mn.z
                    bpy.context.view_layer.update()
                    mn, mx = bounds(o)
                if rng.random() < 0.35:
                    s = rng.choice(list(smalls))
                    self._side(s, side, mx.y + 0.5, LANE_EDGE + 0.6, smalls[s], f"Kucuk_{side:+d}_{y:05.1f}")
                y = mx.y + rng.uniform(14, 22)
            for yy in range(int(y0) + (8 if side < 0 else 20), int(y1), 24):
                self._side("D_M13_BridgeLantern", side, yy, LANE_EDGE + 0.9, 1.2, f"Fener_{side:+d}_{yy:03d}")
        # çatı ışığı (her binada değil), karşıdan karşıya zincir 30–40 m'de bir
        sc = bpy.context.scene
        dg = bpy.context.evaluated_depsgraph_get()
        for b in facade:
            if "CityWall" in b.name or rng.random() < 0.25: continue
            mn, mx = bounds(b)
            side = 1 if mn.x > self.ox else -1
            cy = (mn.y + mx.y) / 2
            xf = (mn.x + 1.6) if side > 0 else (mx.x - 1.6)
            hit, loc, nor, idx, obj, _ = sc.ray_cast(dg, Vector((xf, cy, mx.z + 2)), Vector((0, 0, -1)))
            if not hit or obj != b or loc.z < 3: continue
            o = self._copy("D_M05_StringLights", f"CatiIsik_{side:+d}_{cy:05.1f}", math.radians(90))
            omn, omx = bounds(o)
            s = min(1.0, (mx.y - mn.y) * 0.8 / max(0.1, omx.y - omn.y))
            o.scale = tuple(a * s for a in o.scale)
            bpy.context.view_layer.update()
            omn, omx = bounds(o)
            o.location += Vector((xf - (omn.x + omx.x) / 2, cy - (omn.y + omx.y) / 2, loc.z - omn.z))
        y = y0 + rng.uniform(10, 18)
        while y < y1 - 6:
            for sx in (-1, 1):
                o = self._copy("D_M05_StringLights", f"Zincir_{y:05.1f}_{sx:+d}")
                mn, mx = bounds(o)
                w = mx.x - mn.x
                o.location += Vector((self.ox + sx * (w / 2 + 0.1) - (mn.x + mx.x) / 2, y - (mn.y + mx.y) / 2, 7.8 - mn.z))
            y += rng.uniform(30, 40)
        bpy.context.view_layer.update()
        # arka plan siluetleri
        for side in (-1, 1):
            y = y0 + rng.uniform(5, 20)
            while y < y1:
                k = rng.choice(("D_M11_Dome", "D_M10_Minaret", "D_M11_Dome"))
                self._side(k, side, y, rng.uniform(14, 20), 2.4 if "Dome" in k else 1.0, f"Arka_{side:+d}_{y:05.1f}")
                y += rng.uniform(22, 34)

    def dress_sur(self, rng, y0, y1):
        """Sur üstü: yol surun tepesinde; iki yanda mazgallı sur gövdesi (aşağıda), deniz, kuleler, kayıklar, köprü."""
        WALL_TOP = 1.1     # mazgal yolun 1.1 m üstünde (korkuluk)
        sea = bpy.data.materials.get("Deniz") or bpy.data.materials.new("Deniz")
        sea.diffuse_color = (0.12, 0.42, 0.55, 1)
        for side in (-1, 1):
            y = y0 - 3 + rng.uniform(0, 3)
            while y < y1:
                o = self._copy("D_M07_CityWall", f"Sur_{side:+d}_{y:05.1f}", math.radians(90 if side < 0 else -90))
                mn, mx = bounds(o)
                near = mn.x if side > 0 else mx.x
                o.location += Vector(((self.ox + side * 4.6) - near, y - mn.y, WALL_TOP - mx.z))
                bpy.context.view_layer.update()
                _, mx = bounds(o)
                y = mx.y - 0.4
            yy = y0 + rng.uniform(10, 30)
            while yy < y1:
                t = self._copy("D_M08_WallTower", f"Kule_{side:+d}_{yy:05.1f}")
                mn, mx = bounds(t)
                t.location += Vector((self.ox + side * 8.0 - (mn.x + mx.x) / 2, yy - mn.y, -7.0 - mn.z))
                yy += rng.uniform(38, 55)
            yb = y0 + rng.uniform(5, 25)
            while yb < y1:
                b = self._copy("D_M15_Boat", f"Kayik_{side:+d}_{yb:05.1f}", rng.uniform(0, 6.28), (1.6, 1.6, 1.6))
                mn, mx = bounds(b)
                b.location += Vector((self.ox + side * rng.uniform(16, 30) - (mn.x + mx.x) / 2, yb - mn.y, -7.2 - mn.z))
                yb += rng.uniform(18, 35)
        bpy.ops.mesh.primitive_plane_add(size=1)
        p = bpy.context.active_object
        for c in list(p.users_collection): c.objects.unlink(p)
        self.coll.objects.link(p)
        p.name = f"{self.name}_Deniz_{y0:05.1f}"
        p.scale = (90, y1 - y0 + 4, 1)
        p.location = (self.ox, (y0 + y1) / 2, -7.2)
        p.data.materials.append(sea)
        a = self._copy("D_M12_BridgeArch", f"Kopru_{y0:05.1f}", math.radians(90), (1.1, 1.1, 1.1))
        mn, mx = bounds(a)
        a.location += Vector((self.ox + rng.choice((-1, 1)) * 34 - (mn.x + mx.x) / 2, (y0 + y1) / 2 - (mn.y + mx.y) / 2, -8 - mn.z))
        bpy.context.view_layer.update()

    def dress_gecis(self, rng, y0, y1, to_sur=False):
        """Geçiş: yolu aşan çarşı kemeri (döşemesi yolun altında) + iki yanda kule."""
        a = self._copy("D_M04_BazaarArch", f"Gecis_Kemer_{y0:05.1f}", math.pi)
        mn, mx = bounds(a)
        k = 9.5 / (mx.x - mn.x)
        a.scale = tuple(s * k for s in a.scale)
        bpy.context.view_layer.update()
        mn, mx = bounds(a)
        a.location += Vector((self.ox - (mn.x + mx.x) / 2, (y0 + y1) / 2 - (mn.y + mx.y) / 2, -mn.z - (mx.z - mn.z) * 0.42 / 9.0))
        for side in (-1, 1):
            t = self._copy("D_M08_WallTower", f"Gecis_Kule_{side:+d}_{y0:05.1f}")
            mn, mx = bounds(t)
            t.location += Vector((self.ox + side * 8.2 - (mn.x + mx.x) / 2, (y0 + y1) / 2 - (mn.y + mx.y) / 2, -mn.z))
            for yy in (y0 + 3, y1 - 6):
                self._side("D_M13_BridgeLantern", side, yy, LANE_EDGE + 0.9, 1.2, f"Gecis_Fener_{side:+d}_{yy:05.1f}")
        bpy.context.view_layer.update()

    # ---------------------------------------------------------------------------------- geçilebilirlik simülasyonu
    def solve(self, length):
        n = int(math.ceil(length / STEP)) + 1
        surf = [[0.0] * n for _ in range(3)]
        ceil_lo = [[1e9] * n for _ in range(3)]
        ceil_hi = [[0.0] * n for _ in range(3)]
        for (lane, y0, y1, kind, data) in self.solids:
            li = lane + 1
            i0, i1 = max(0, int(y0 / STEP)), min(n - 1, int(math.ceil(y1 / STEP)))
            for i in range(i0, i1 + 1):
                if kind == "wall":
                    surf[li][i] = max(surf[li][i], 4.0)
                elif kind == "box":
                    surf[li][i] = max(surf[li][i], data)
                elif kind in ("profile", "plank"):
                    k = min(len(data) - 1, max(0, int((i * STEP - y0) / STEP)))
                    surf[li][i] = max(surf[li][i], data[k])
                elif kind == "ceiling":
                    ceil_lo[li][i] = min(ceil_lo[li][i], data[0])
                    ceil_hi[li][i] = max(ceil_hi[li][i], data[1])
        W = int(LANE_CHANGE / STEP)

        def can_walk(li, i, h):
            s = surf[li][i]
            if s > h + STEP_UP: return False                       # duvar / yüksek basamak
            return ceil_lo[li][i] > 1e8 or ceil_lo[li][i] - s > SLIDE_HEAD + 0.05   # tavan varsa altından kayılır

        def ok_air(li, i, z):
            if surf[li][i] > z - 0.02: return False
            lo, hi = ceil_lo[li][i], ceil_hi[li][i]
            if lo < 1e8 and not (z >= hi or z + 1.0 <= lo): return False
            return True

        best = {}   # (i, li) -> yükseklik kümesi
        from collections import deque

        def start(li):
            seen = set()
            q = deque([(0, li, 0.0)])
            seen.add((0, li, 0.0))
            reach_end = False
            while q:
                i, l, h = q.popleft()
                if i >= n - 1:
                    if h <= STEP_UP: reach_end = True
                    continue
                cands = []
                # yürü / kay / tırman / düş
                s = surf[l][i + 1]
                if can_walk(l, i + 1, h):
                    cands.append((i + 1, l, s))
                # şerit değiştir (W adımda; ilk yarı kendi şeridi, ikinci yarı hedef)
                for d in (-1, 1):
                    tl = l + d
                    if tl < 0 or tl > 2 or i + W >= n: continue
                    hh, ok = h, True
                    for k in range(1, W + 1):
                        lane_k = l if k <= W // 2 else tl
                        sk = surf[lane_k][i + k]
                        if sk > hh + STEP_UP: ok = False; break
                        if ceil_lo[lane_k][i + k] < 1e8 and ceil_lo[lane_k][i + k] - max(sk, 0) <= SLIDE_HEAD + 0.05: ok = False; break
                        hh = sk
                    if ok: cands.append((i + W, tl, hh))
                # zıpla (aynı şeritte ya da çapraz), yay: z(t) = h + 4·H·t(1-t)
                for tl in (l - 1, l, l + 1):
                    if tl < 0 or tl > 2: continue
                    for dist in (JUMP_MIN, 3.5, 4.5, JUMP_MAX):
                        steps = int(dist / STEP)
                        j = i + steps
                        if j >= n: continue
                        okj = True
                        landed = None
                        zprev = h
                        for k in range(1, steps + 1):
                            t = k / steps
                            z = h + 4 * JUMP_H * t * (1 - t)
                            lane_k = l if (tl == l or k <= steps // 2) else tl
                            sk = surf[lane_k][i + k]
                            if t >= 0.5 and sk >= z - 0.02:
                                # iniyor: bir önceki adımda yüzeyin üstündeydik → üstüne iner; değilse yüzüne çarpar
                                if sk <= zprev + 0.02 and lane_k == tl:
                                    landed = (i + k, tl, sk); break
                                okj = False; break
                            if not ok_air(lane_k, i + k, z): okj = False; break
                            zprev = z
                        if okj:
                            cands.append(landed or (j, tl, surf[tl][j]))
                for c in cands:
                    key = (c[0], c[1], round(c[2], 1))
                    if key in seen: continue
                    seen.add(key)
                    q.append((c[0], c[1], c[2]))
            return reach_end, seen

        results = [start(li) for li in range(3)]
        ok_all = all(r[0] for r in results)
        portal_ok = True
        for p in self.portals:
            pi = int(p["y"] / STEP)
            got = any(any(k[0] in range(pi - 2, pi + 3) and k[1] == p["lane"] + 1 and abs(k[2] - p["z"]) < 0.5 for k in r[1]) for r in results)
            portal_ok = portal_ok and got
        return ok_all, portal_ok, [r[0] for r in results]

    def save_json(self, length, theme, regions=None):
        """Engeller (anlamlı kayıt) + kenar dekoru (kütüphane modeli, konum, dönüş, ölçek) → Unity birebir kurar."""
        os.makedirs(OUT_JSON, exist_ok=True)
        data_key = {src.data.name: k for k, src in self.lib.src.items()}
        obstacle_names = {it.get("obj") for it in self.items}
        decor = []
        for o in self.coll.objects:
            if o.type != "MESH" or o.data.name not in data_key or o.name in obstacle_names: continue
            if "_Zemin_" in o.name or "_Deniz_" in o.name: continue
            k = data_key[o.data.name]
            src = self.lib.src[k]
            mn, mx = bounds(o)
            decor.append({"m": k[2:], "src": k[:1], "yaw": round(math.degrees(o.rotation_euler.z - src.rotation_euler.z), 2),
                          "min": [round(mn.x - self.ox, 3), round(mn.y, 3), round(mn.z, 3)],
                          "max": [round(mx.x - self.ox, 3), round(mx.y, 3), round(mx.z, 3)]})
        with open(os.path.join(OUT_JSON, self.name + ".json"), "w", encoding="utf-8") as f:
            json.dump({"name": self.name, "length": round(length, 2), "theme": theme, "sections": self.sections,
                       "regions": [{"theme": t, "y0": round(a, 2), "y1": round(b, 2)} for t, a, b in (regions or [])],
                       "portals": self.portals, "items": self.items, "decor": decor}, f, ensure_ascii=False, indent=1)
