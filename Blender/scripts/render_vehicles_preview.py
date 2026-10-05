"""vehicles.blend önizleme → Logs/vehicles_preview.png (taksi, dolmuş, sur kapısı, sandıklar)"""
import math

import bpy
from mathutils import Euler

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\vehicles.blend")
scene = bpy.context.scene
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.lens = 24
scene.collection.objects.link(cam)
cam.location = (7.0, 13.0, 5.0)
cam.rotation_euler = Euler((math.radians(72), 0, math.radians(152)))
scene.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.5
scene.collection.objects.link(sun)
sun.rotation_euler = Euler((math.radians(50), math.radians(-20), math.radians(30)))
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
next(n for n in world.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (0.6, 0.65, 0.75, 1)
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1000, 600
scene.render.filepath = r"D:\Repolar\junpline\Logs\vehicles_preview.png"
bpy.ops.render.render(write_still=True)
print("RENDER_OK")
