"""runner.blend: seçili action/karelerden yan görünüş şeridi → Logs/anim_preview.png"""
import math
import os

import bpy
from mathutils import Euler

SHOTS = [("Death", 0), ("Death", 3), ("Death", 11), ("Death", 20), ("Death", 25), ("Death", 48)]
bpy.ops.wm.open_mainfile(filepath=r"D:\Repolar\junpline\Blender\source\runner.blend")
sc = bpy.context.scene
rig = bpy.data.objects["RunnerRig"]
cam = bpy.data.objects.new("PCam", bpy.data.cameras.new("PCam"))
sc.collection.objects.link(cam)
cam.location = (4.2, -1.2, 1.0)
cam.rotation_euler = Euler((math.radians(88), 0, math.radians(75)))
cam.data.lens = 40
sc.camera = cam
sun = bpy.data.objects.new("PSun", bpy.data.lights.new("PSun", "SUN"))
sun.data.energy = 3
sc.collection.objects.link(sun)
sun.rotation_euler = Euler((0.8, 0, 0.8))
try:
    sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError:
    pass
sc.render.resolution_x, sc.render.resolution_y = 220, 300
tmp = r"D:\Repolar\junpline\Logs\anim_tmp"
os.makedirs(tmp, exist_ok=True)
files = []
for i, (act, fr) in enumerate(SHOTS):
    rig.animation_data.action = bpy.data.actions[act]
    sc.frame_set(fr)
    p = os.path.join(tmp, f"{i}.png")
    sc.render.filepath = p
    bpy.ops.render.render(write_still=True)
    files.append(p)
imgs = [bpy.data.images.load(f) for f in files]
w, h = 220, 300
out = bpy.data.images.new("strip", w * len(imgs), h)
px = [0.0] * (w * len(imgs) * h * 4)
for k, im in enumerate(imgs):
    src = list(im.pixels)
    for y in range(h):
        row = (y * w * len(imgs) + k * w) * 4
        px[row:row + w * 4] = src[y * w * 4:(y + 1) * w * 4]
out.pixels = px
out.filepath_raw = r"D:\Repolar\junpline\Logs\anim_preview.png"
out.file_format = "PNG"
out.save()
print("PREVIEW_OK")
