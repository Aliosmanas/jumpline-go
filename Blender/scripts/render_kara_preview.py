"""Humbaba önizleme: dinlenme pozu önden + 3/4 arkadan, Roar ve Run kareleri → Logs/Captures/tepegoz_*.png"""
import math
import os

import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
OUT = os.path.join(ROOT, "Logs", "Captures")
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "Blender", "source", "karakoncolos.blend"))
sc = bpy.context.scene
engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
for e in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    if e in engines:
        sc.render.engine = e
        break
sc.render.resolution_x, sc.render.resolution_y = 900, 1100
sc.render.film_transparent = False
sc.view_settings.view_transform = "AgX" if "AgX" in [i.identifier for i in bpy.types.ColorManagedViewSettings.bl_rna.properties["view_transform"].enum_items] else "Filmic"
w = bpy.data.worlds.new("W"); w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND"); bg.inputs["Color"].default_value = (0.05, 0.05, 0.06, 1); bg.inputs["Strength"].default_value = 1.0
sc.world = w
rig = bpy.data.objects["KarakoncolosRig"]
body = bpy.data.objects["Karakoncolos"]
h = 2.4
for name, loc, energy, size in (("Key", (-2.5, -3.5, 3.5), 1200, 3), ("Rim", (2.5, 3, 3), 900, 2), ("Fill", (3, -3, 1), 300, 4)):
    ld = bpy.data.lights.new(name, "AREA"); ld.energy = energy; ld.size = size
    lo = bpy.data.objects.new(name, ld); lo.location = loc
    lo.rotation_euler = (Vector((0, 0, 1.2)) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.collection.objects.link(lo)
cd = bpy.data.cameras.new("C"); cd.lens = 50
cam = bpy.data.objects.new("C", cd); sc.collection.objects.link(cam); sc.camera = cam
floor = bpy.data.meshes.new("F"); import bmesh; bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6); bm.to_mesh(floor)
fo = bpy.data.objects.new("Floor", floor); sc.collection.objects.link(fo)

def shot(name, cam_dir, action=None, frame=0, target_z=1.15, dist=6.2):
    rig.animation_data_create()
    rig.animation_data.action = bpy.data.actions.get(action) if action else None
    sc.frame_set(frame)
    d = Vector(cam_dir).normalized()
    t = Vector((0, 0, target_z))
    cam.location = t - d * dist
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)

os.makedirs(OUT, exist_ok=True)
shot("kara_front", (0.0, 1.0, -0.08), target_z=1.2, dist=6.0)
shot("kara_face", (0.25, 1.0, -0.05), target_z=1.9, dist=1.6)
shot("kara_side", (1.0, 0.0, -0.25), target_z=0.8, dist=5.5)
shot("kara_run", (0.6, 1.0, -0.6), "Run", 6, target_z=0.6, dist=5.5)
