"""Köprü önizlemesi: 3 parça yan yana; yandan (vadi) ve yol üstünden → Logs/Captures/bridge_*.png"""
import os
import bpy
from mathutils import Vector

ROOT = r"D:\Repolar\junpline"
OUT = os.path.join(ROOT, "Logs", "Captures")
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "Blender", "source", "bridge.blend"))
sc = bpy.context.scene
seg = bpy.data.objects["BridgeSegment"]
for k in (1, 2, -1):
    c = seg.copy()
    c.location = (0, -40.0 * k, 0)   # Unity +z = Blender -y
    sc.collection.objects.link(c)
engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
sc.render.resolution_x, sc.render.resolution_y = 1200, 700
w = bpy.data.worlds.new("W"); w.use_nodes = True
bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND"); bg.inputs["Color"].default_value = (0.95, 0.7, 0.45, 1); bg.inputs["Strength"].default_value = 0.8
sc.world = w
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); sun.data.energy = 4; sun.rotation_euler = (0.9, 0.3, 2.4); sc.collection.objects.link(sun)
cam = bpy.data.objects.new("C", bpy.data.cameras.new("C")); sc.collection.objects.link(cam); sc.camera = cam
def shot(name, loc, target, lens=35):
    cam.data.lens = lens
    cam.location = loc
    cam.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
shot("bridge_side", (-70, -40, -8), (0, -40, -8), 30)
shot("bridge_deck", (0, 12, 3.5), (0, -40, 0.5), 30)
print("BRIDGE_PREVIEW_OK")
