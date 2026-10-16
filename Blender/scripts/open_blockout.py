"""Renksiz harita tasarım sahnesi (blokaj): oyundaki ölçülerde 3 şeritli yol, 10 m işaretleri, düz gri görünüm.
MCP sunucusu kendiliğinden başlar (Claude canlı çizebilsin).
blender -P open_blockout.py   (arka planda değil: pencere açık kalır)"""
import bpy, math

LANE = 2.2           # PlayerController.laneWidth
LENGTH = 150.0       # 5 harita parçası (30 m)
SIDE = 4.4           # kaldırım başlangıcı (yol ortasından)

for o in list(bpy.data.objects): bpy.data.objects.remove(o, do_unlink=True)   # varsayılan küp/ışık/kamera (eklentiler açık kalır)
sc = bpy.context.scene
sc.unit_settings.system = "METRIC"


def mat(name, gray):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (gray, gray, gray, 1)
    return m


def box(name, x, y, z, sx, sy, sz, m, coll):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z))
    o = bpy.context.active_object
    o.name = name
    o.scale = (sx, sy, sz)
    o.data.materials.append(m)
    for c in o.users_collection: c.objects.unlink(o)
    coll.objects.link(o)
    return o


ref = bpy.data.collections.new("0_REFERANS_YOL (dokunma)")
sc.collection.children.link(ref)
design = bpy.data.collections.new("1_TASARIM")
sc.collection.children.link(design)
road, line, walk, mark = mat("Yol", 0.35), mat("SeritCizgisi", 0.85), mat("Kaldirim", 0.55), mat("Isaret", 0.15)
# Blender'da koşu yönü +Y (Unity'de +Z); x = şerit (sol -2.2, orta 0, sağ +2.2)
box("Yol", 0, LENGTH / 2, -0.05, SIDE * 2, LENGTH, 0.1, road, ref)
for x in (-LANE * 1.5, -LANE * 0.5, LANE * 0.5, LANE * 1.5):
    box(f"SeritCizgisi_{x:+.1f}", x, LENGTH / 2, 0.005, 0.08, LENGTH, 0.01, line, ref)
for s in (-1, 1):
    box(f"Kaldirim_{'sol' if s < 0 else 'sag'}", s * (SIDE + 1.55), LENGTH / 2, 0.09, 3.1, LENGTH, 0.18, walk, ref)
for y in range(0, int(LENGTH) + 1, 10):
    box(f"Isaret_{y:03d}m", -SIDE - 0.3, y, 0.2, 0.3, 0.15, 0.4, mark, ref)
    bpy.ops.object.text_add(location=(-SIDE - 1.2, y, 0.25), rotation=(math.radians(90), 0, math.radians(90)))
    t = bpy.context.active_object
    t.name = f"Etiket_{y:03d}m"
    t.data.body = f"{y} m"
    t.data.size = 0.8
    for c in t.users_collection: c.objects.unlink(t)
    ref.objects.link(t)
# koşucu boyu referansı (1.7 m) — orta şerit başında
box("Kosucu_1.7m", 0, 1.0, 0.85, 0.5, 0.35, 1.7, mark, ref)
ref.hide_select = True

# düz gri görünüm, kamera koşucunun arkasından
for area in bpy.context.screen.areas if bpy.context.screen else []:
    if area.type == "VIEW_3D":
        sp = area.spaces.active
        sp.shading.type = "SOLID"
        sp.shading.color_type = "MATERIAL"
        sp.shading.light = "STUDIO"
        sp.clip_end = 1000
        r3d = sp.region_3d
        r3d.view_location = (0, 30, 0)
        r3d.view_distance = 45
        r3d.view_rotation = (0.82, 0.57, 0.0, 0.0)


def setup_view():
    for win in bpy.context.window_manager.windows:
        for area in win.screen.areas:
            if area.type == "VIEW_3D":
                sp = area.spaces.active
                sp.shading.type = "SOLID"
                sp.shading.color_type = "MATERIAL"
                sp.clip_end = 1000
                r3d = sp.region_3d
                r3d.view_location = (0, 35, 0)
                r3d.view_distance = 50
                from mathutils import Euler
                r3d.view_rotation = Euler((math.radians(62), 0, math.radians(180)), "XYZ").to_quaternion()
    try:
        bpy.ops.blendermcp.start_server()
        print("MCP_OK")
    except Exception as e:
        print("MCP_FAIL", e)
    return None


bpy.ops.wm.save_as_mainfile(filepath=r"D:\Repolar\junpline\Blender\Harita\Tasarim_Blokaj.blend")
bpy.app.timers.register(setup_view, first_interval=2.0)
