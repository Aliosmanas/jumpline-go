"""Canavar portresi + HUD yüz ikonu, iskeletli Meshy modelinden (rigged/<ad>.blend, Idle pozu), şeffaf PNG.
blender -b -P render_boss_portrait.py -- <ad> <portre_png> [ikon_png]
  portre: 512 px baş+omuz, hafif alttan (tehditkâr).  ikon: 256 px yalnız baş (canavar madalyonu / mesafe çubuğu)."""
import bpy, os, sys, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
NAME, PORTRAIT = args[0], args[1]
ICON = args[2] if len(args) > 2 else None
ROOT = r"D:\Repolar\junpline"
bpy.ops.wm.open_mainfile(filepath=os.path.join(ROOT, "Blender", "rigged", NAME + ".blend"))
sc = bpy.context.scene
arm = next(o for o in sc.objects if o.type == "ARMATURE")
idle = bpy.data.actions.get("Idle")
if idle:
    arm.animation_data.action = idle
    try: arm.animation_data.action_slot = idle.slots[0]
    except Exception: pass
sc.frame_set(int(idle.frame_range[0]) if idle else 0)
bpy.context.view_layer.update()
head = next(b for b in arm.pose.bones if b.name.endswith("Head"))
neck = next((b for b in arm.pose.bones if b.name.endswith("Neck")), head)
H = arm.matrix_world @ head.head
N = arm.matrix_world @ neck.head
hs = (H - N).length * 2.2  # baş boyu ölçeği

try: sc.render.engine = "BLENDER_EEVEE_NEXT"
except TypeError: sc.render.engine = "BLENDER_EEVEE"
sc.render.film_transparent = True
sc.view_settings.view_transform = "Standard"
world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
bg.inputs[0].default_value = (0.55, 0.5, 0.48, 1); bg.inputs[1].default_value = 0.6


def light(name, kind, loc, energy, color):
    l = bpy.data.objects.new(name, bpy.data.lights.new(name, kind)); sc.collection.objects.link(l)
    l.data.energy = energy; l.data.color = color; l.location = loc
    l.rotation_euler = (H - Vector(loc)).to_track_quat("-Z", "Y").to_euler()
    if kind == "AREA": l.data.size = hs * 3
    return l


light("key", "AREA", H + Vector((-1.2, -2.0, 0.8)) * hs * 2, 900 * hs * hs, (1.0, 0.86, 0.72))
light("rim", "AREA", H + Vector((1.6, 1.4, 0.6)) * hs * 2, 1400 * hs * hs, (1.0, 0.45, 0.25))
light("fill", "AREA", H + Vector((1.5, -2.0, -0.6)) * hs * 2, 250 * hs * hs, (0.55, 0.65, 1.0))

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 70


def shoot(path, size, target, dist, lift):
    sc.render.resolution_x = sc.render.resolution_y = size
    cam.location = target + Vector((0.18, -1.0, lift)).normalized() * dist
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print("PORTRAIT", path)


# portre: baş + omuzlar, hafif alttan
shoot(PORTRAIT, 512, H + Vector((0, 0, -0.25 * hs)), hs * 6.2, -0.12)
if ICON:
    shoot(ICON, 256, H + Vector((0, 0, -0.05 * hs)), hs * 4.8, 0.0)
