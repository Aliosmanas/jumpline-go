"""decor.blend'deki dekorları street.blend'e ekler ve Unity'deki yerleşimle aynı yerlere koyar (Unity z → Blender -y)."""
import math

import bpy

STREET = r"D:\Repolar\junpline\Blender\source\street.blend"
DECOR = r"D:\Repolar\junpline\Blender\source\decor.blend"
LAYOUT = [  # GameplaySceneBuilder.DecorLayout ile aynı (ad, x, y(yukarı), z(ileri), açı)
    ("Samovar", -2.45, 0, 6, 90), ("Crates", 2.2, 0, 9, -90), ("WallLantern", -2.9, 2.6, 4, 90),
    ("WallLantern", 2.9, 2.6, 13, -90), ("KilimPile", -2.3, 0, 14, 90), ("FruitBasket", 2.45, 0, 16.5, -90),
    ("IronGate", 2.95, 0, 21, -90), ("Sack", -2.45, 0, 22, 90), ("Watermelon", 2.35, 0, 25, -90),
    ("Nazar", -2.92, 2.0, 26, 90), ("CopperJugs", -2.3, 0, 30, 90), ("BreadBasket", 2.45, 0, 33, -90),
    ("CarvedPillar", -2.6, 0, 35.5, 90), ("Chest", 2.35, 0, 38, -90), ("RolledCarpet", -2.5, 0, 41, 90),
    ("WallLantern", -2.9, 2.6, 44, 90), ("Ladder", 2.7, 0, 47, -90), ("CopperPlates", -2.45, 0, 50, 90),
    ("Birdhouse", 2.45, 0, 52.5, -90), ("KebabCart", -2.1, 0, 56, 90), ("WallLantern", 2.9, 2.6, 58, -90),
]

bpy.ops.wm.open_mainfile(filepath=STREET)
for o in list(bpy.data.objects):
    if o.name.startswith("Decor_") or o.name.startswith("Placed_"):
        bpy.data.objects.remove(o, do_unlink=True)
with bpy.data.libraries.load(DECOR, link=False) as (src, dst):
    dst.objects = [n for n in src.objects if n.startswith("Decor_")]
coll = bpy.data.collections.get("Decor") or bpy.data.collections.new("Decor")
if coll.name not in bpy.context.scene.collection.children:
    bpy.context.scene.collection.children.link(coll)
lib = {o.name: o for o in dst.objects if o}
for i, (name, x, y, z, rot) in enumerate(LAYOUT):
    src_obj = lib.get("Decor_" + name)
    if src_obj is None:
        continue
    o = src_obj.copy()
    o.name = f"Placed_{name}_{i}"
    coll.objects.link(o)
    o.location = (-x, -z, y)                       # Unity x → Blender -x (FBX ekseni), Unity z → Blender -y
    o.rotation_euler = (0, 0, math.radians(-rot))
bpy.ops.wm.save_mainfile()
print("PLACED_OK", len(coll.objects))
