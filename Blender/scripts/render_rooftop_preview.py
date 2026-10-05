"""rooftop.blend'i koşucu kamerasından (gece) render eder → Logs/rooftop_preview.png
Kullanım: blender -b -P render_rooftop_preview.py -- [çıktı] [kamera_z] [yükseklik] [eğim]"""
import math
import sys

import bpy
from mathutils import Euler

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = argv[0] if argv else r"D:\Repolar\junpline\Logs\rooftop_preview.png"
cam_z = float(argv[1]) if len(argv) > 1 else 28.0      # Unity z (koşu yönü)
cam_h = float(argv[2]) if len(argv) > 2 else 4.0
pitch = float(argv[3]) if len(argv) > 3 else 78.0

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\rooftop.blend")
scene = bpy.context.scene
cam_data = bpy.data.cameras.new("Cam")
cam_data.lens = 16
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
cam.location = (0.0, -cam_z, cam_h)
cam.rotation_euler = Euler((math.radians(pitch), 0, math.radians(180)))
scene.camera = cam

moon = bpy.data.lights.new("Moon", "SUN")
moon.energy = 0.6
moon.color = (0.6, 0.7, 1.0)
mo = bpy.data.objects.new("Moon", moon)
scene.collection.objects.link(mo)
mo.rotation_euler = Euler((math.radians(50), math.radians(-30), math.radians(30)))

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.03, 0.045, 0.07, 1)
bg.inputs["Strength"].default_value = 1.0
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        pass
try:
    scene.eevee.use_bloom = True
except AttributeError:
    pass
scene.render.resolution_x, scene.render.resolution_y = 540, 960
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("RENDER_OK", out)
