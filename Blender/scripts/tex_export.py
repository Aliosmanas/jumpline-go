"""Paketli (adsız) Meshy dokularını FBX'in yanına <ad>_Tex/ klasörüne adlı PNG olarak yazar (mobil: en fazla 1024 px).
Unity FBX'i içe aktarırken dokuları göreli yoldan bulur → malzeme dokusuyla gelir."""
import bpy, os

MAX = 1024


def save_images(fbx_path, prefix):
    d = os.path.splitext(fbx_path)[0] + "_Tex"
    os.makedirs(d, exist_ok=True)
    used = set()
    for o in bpy.data.objects:
        if o.type != "MESH": continue
        for sl in o.material_slots:
            m = sl.material
            if not m or not m.node_tree: continue
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image: used.add(n.image)
    for i, img in enumerate(sorted(used, key=lambda x: x.name)):
        if img.size[0] > MAX: img.scale(MAX, int(MAX * img.size[1] / img.size[0]))
        kind = "base" if img.colorspace_settings.name == "sRGB" else "data"
        path = os.path.join(d, f"{prefix}_{i}_{kind}.png")
        img.filepath_raw = path; img.file_format = "PNG"; img.save()
        if img.packed_file: img.unpack(method="REMOVE")
        img.filepath = path
    return d
