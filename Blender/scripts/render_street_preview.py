"""street.blend'i referans açısından (sokak içi, göz hizası) render eder → Logs/street_preview.png"""
import math
import sys

import bpy
from mathutils import Euler

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = argv[0] if argv else r"D:\Repolar\junpline\Logs\street_preview.png"
cam_y = float(argv[1]) if len(argv) > 1 else -2.0
pitch = float(argv[2]) if len(argv) > 2 else 86.0

bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\street.blend")
scene = bpy.context.scene
for o in list(scene.objects):
    if o.type in ("CAMERA", "LIGHT") or o.name == "Cube":
        bpy.data.objects.remove(o, do_unlink=True)

cam_data = bpy.data.cameras.new("Cam")
cam_data.lens = 18
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
cam.location = (0.0, cam_y, 1.7)
cam.rotation_euler = Euler((math.radians(pitch), 0, math.radians(180)))  # -Y'ye bak
scene.camera = cam

sun_data = bpy.data.lights.new("Sun", "SUN")
sun_data.energy = 4.0
sun_data.angle = math.radians(2)
sun = bpy.data.objects.new("Sun", sun_data)
scene.collection.objects.link(sun)
sun.rotation_euler = Euler((math.radians(55), math.radians(-25), math.radians(20)))

world = scene.world or bpy.data.worlds.new("World")
scene.world = world
world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs["Color"].default_value = (0.75, 0.8, 0.88, 1)
bg.inputs["Strength"].default_value = 0.9

try:
    scene.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    try:
        scene.render.engine = "BLENDER_EEVEE"
    except TypeError:
        pass
scene.render.resolution_x, scene.render.resolution_y = 540, 960
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("RENDER_OK", out)
