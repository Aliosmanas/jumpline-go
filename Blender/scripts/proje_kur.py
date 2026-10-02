"""JUMPLINE GO — Blender başlangıç dosyasını kurar.

Çalıştırma:
  blender -b -P Blender/scripts/proje_kur.py
Sonuç: Blender/source/jumpline_assets.blend (Unity ölçeğinde: 1 birim = 1 metre)
"""
import os
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Blender", "source", "jumpline_assets.blend")
COLLECTIONS = ("Player", "Obstacles", "Environment", "Collectibles", "_Reference")

# Boş sahne
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.name = "JumplineGo"

# Birimler: metre, Unity ile birebir
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = "METERS"
scene.render.fps = 30

# Koleksiyonlar
for name in COLLECTIONS:
    col = bpy.data.collections.new(name)
    scene.collection.children.link(col)

# Ölçek referansı: 1 m genişlik, ~1.2 m boy (oyuncu boyu hedefi)
ref = bpy.data.collections["_Reference"]
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0.6))
box = bpy.context.active_object
box.name = "REF_PlayerSize_1x1.2m"
box.scale = (0.6, 0.6, 1.2)
box.display_type = "WIRE"
for c in box.users_collection:
    c.objects.unlink(box)
ref.objects.link(box)
ref.hide_render = True

# Kamera + ışık (önizleme render'ı için)
cam_data = bpy.data.cameras.new("PreviewCam")
cam = bpy.data.objects.new("PreviewCam", cam_data)
cam.location = (4.0, -4.0, 3.0)
cam.rotation_euler = (1.1, 0, 0.785)
scene.collection.objects.link(cam)
scene.camera = cam

sun_data = bpy.data.lights.new("Sun", type="SUN")
sun_data.energy = 3.0
sun = bpy.data.objects.new("Sun", sun_data)
sun.rotation_euler = (0.8, 0.2, 0.6)
scene.collection.objects.link(sun)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT)
print("KAYDEDILDI:", OUT)
