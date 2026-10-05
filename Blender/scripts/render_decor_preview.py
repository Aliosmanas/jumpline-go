"""decor.blend önizlemesi → Logs/decor_preview.png"""
import math

import bpy
from mathutils import Euler

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\decor.blend")
sc = bpy.context.scene
for o in list(sc.objects):
    if o.type in ("CAMERA", "LIGHT") or o.name == "Cube":
        bpy.data.objects.remove(o, do_unlink=True)
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.lens = 28
sc.collection.objects.link(cam)
cam.location = (5.2, -14.5, 7.5)
cam.rotation_euler = Euler((math.radians(62), 0, 0))
sc.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.0
sc.collection.objects.link(sun)
sun.rotation_euler = Euler((math.radians(50), 0, math.radians(30)))
w = sc.world or bpy.data.worlds.new("W")
sc.world = w
w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.05, 0.06, 0.09, 1)
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    pass
sc.render.resolution_x, sc.render.resolution_y = 1000, 700
sc.render.filepath = r"D:\Repolar\junpline\Logs\decor_preview.png"
bpy.ops.render.render(write_still=True)
print("RENDER_OK")
