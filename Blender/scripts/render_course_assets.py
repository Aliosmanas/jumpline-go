"""course_assets.blend → Logs/course_assets_a.png / _b.png (yan yana dizili yeni araç ve engeller)"""
import math

import bpy
from mathutils import Vector

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\course_assets.blend")
scene = bpy.context.scene
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
sun.data.energy = 3.2
scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(50), math.radians(-15), math.radians(35))
world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
next(n for n in world.node_tree.nodes if n.type == "BACKGROUND").inputs["Color"].default_value = (0.62, 0.66, 0.74, 1)
try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 1400, 700
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
cam.data.lens = 24
scene.collection.objects.link(cam)
scene.camera = cam
for tag, cx in (("a", -14.0), ("b", -40.0)):
    target = Vector((cx, -4.0, 0.8))
    cam.location = target + Vector((9.0, 13.0, 8.0))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = rf"D:\Repolar\junpline\Logs\course_assets_{tag}.png"
    bpy.ops.render.render(write_still=True)
print("RENDER_OK")
