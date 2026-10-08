"""coin.blend önizleme → Logs/coin_preview.png"""
import math

import bpy

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\coin.blend")
sc = bpy.context.scene
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.lens = 60
sc.collection.objects.link(cam)
cam.location = (0.35, -1.6, 0.2)
cam.rotation_euler = (math.radians(84), 0, math.radians(12))
sc.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 4
sc.collection.objects.link(sun)
sun.rotation_euler = (math.radians(60), 0, math.radians(30))
w = sc.world or bpy.data.worlds.new("W")
sc.world = w
w.use_nodes = True
next(n for n in w.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (0.25, 0.25, 0.3, 1)
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = 420
sc.render.filepath = r"D:\Repolar\junpline\Logs\coin_preview.png"
bpy.ops.render.render(write_still=True)
print("RENDER_OK")
