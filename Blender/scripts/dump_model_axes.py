"""Model yön imzası: her kütüphane modelinin (varsayılan duruşunda) köşe ağırlık merkezinin kutu merkezinden kayması ve
kutu boyu → Assets/Data/Designed/_model_axes.json. Unity aynı imzayı FBX'ten ölçüp Blender ile arasındaki taban dönüşünü
(0/90/180/270°) kendisi bulur (eksen dönüşümü tahmin edilmez).  blender -b -P dump_model_axes.py"""
import bpy, sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import map_kit as K
from mathutils import Vector
bpy.ops.wm.read_factory_settings(use_empty=True)
lib = K.Lib()
out = {}
for key, o in lib.src.items():
    bpy.context.view_layer.update()
    mn, mx = K.bounds(o)
    c = (mn + mx) / 2
    s = Vector((0, 0, 0)); n = 0
    for v in o.data.vertices:
        w = o.matrix_world @ v.co
        s += w; n += 1
    g = s / max(1, n)
    out[key] = {"size": [round(mx.x - mn.x, 4), round(mx.y - mn.y, 4), round(mx.z - mn.z, 4)],
                "off": [round((g.x - c.x) / max(1e-3, mx.x - mn.x), 4), round((g.y - c.y) / max(1e-3, mx.y - mn.y), 4)]}
with open(os.path.join(K.OUT_JSON, "_model_axes.json"), "w", encoding="utf-8") as f:
    json.dump(out, f, indent=1)
print("AXES", len(out))
