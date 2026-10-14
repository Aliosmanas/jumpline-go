"""Kontrol: rigged/<ad>.blend içindeki her klipten bir kare (orta kare) render eder, tek görselde birleştirir.
blender -b -P render_rig_check.py -- <ad>"""
import bpy, os, sys, math, mathutils
NAME = sys.argv[sys.argv.index("--") + 1]
ROOT = r"D:\Repolar\junpline"; OUT = r"D:\tmp\rigcheck"; os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "Blender", "rigged", NAME + ".blend"))
sc = bpy.context.scene; sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"
sc.render.resolution_x = 300; sc.render.resolution_y = 380
arm = next(o for o in sc.objects if o.type == "ARMATURE"); body = next(o for o in sc.objects if o.type == "MESH")
H = max(body.dimensions) if max(body.dimensions) > 0.1 else 1.8
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = H * 1.5
c = mathutils.Vector((0, 0, body.dimensions.z * 0.5)); d = mathutils.Vector(tuple(float(x) for x in os.environ.get("VIEW", "0.7,-1.0,0.25").split(","))).normalized()
cam.location = c + d * H * 4; cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
files = []
for act in [a for a in bpy.data.actions]:
    arm.animation_data.action = act
    try: arm.animation_data.action_slot = act.slots[0]
    except Exception: pass
    f0, f1 = act.frame_range; sc.frame_set(int((f0 + f1) / 2))
    sc.render.filepath = os.path.join(OUT, f"{NAME}_{act.name}.png"); bpy.ops.render.render(write_still=True)
    files.append(sc.render.filepath)
print("RCHECK", files)
