"""obstacles.blend'deki rampalı kamyoneti çapraz arkadan render eder → Logs/truck_preview.png"""
import math

import bpy
from mathutils import Euler

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\obstacles.blend")
scene = bpy.context.scene
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.lens = 28
scene.collection.objects.link(cam)
cam.location = (-5.5, 5.0, 3.6)            # Unity (5.5, h, -5): arkadan-yandan
cam.rotation_euler = Euler((math.radians(72), 0, math.radians(-140)))
scene.camera = cam
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.5
scene.collection.objects.link(sun)
sun.rotation_euler = Euler((math.radians(50), math.radians(-20), math.radians(30)))
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.6, 0.65, 0.75, 1)
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 800, 600
scene.render.filepath = r"D:\Repolar\junpline\Logs\truck_preview.png"
bpy.ops.render.render(write_still=True)
print("RENDER_OK")
