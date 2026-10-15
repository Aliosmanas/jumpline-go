"""Boyalı ev dokuları: Meshy taban dokusu (koyu bazalt) açılır + güçlü renkle karıştırılır (Blender'daki boya düğümünün aynısı).
Çıktı: Assets/Art/Environment/Meshy/<model>_Tex/<model>_Boya_<renk>.png (1024 px). Haritalar (Assets/Data/Maps) hangi
(model, renk) çiftini kullanıyorsa o üretilir."""
import glob, json, os
import numpy as np
from PIL import Image
ROOT = r"D:\Repolar\junpline"
PAINTS = {"Turuncu": (1.0, 0.42, 0.12), "Sari": (1.0, 0.78, 0.22), "Turkuaz": (0.1, 0.62, 0.68), "Pembe": (0.92, 0.36, 0.48),
          "Kirec_Beyazi": (0.95, 0.9, 0.78), "Yesil": (0.42, 0.75, 0.32), "Mavi": (0.26, 0.45, 0.9), "Kirectasi": (0.92, 0.78, 0.55)}
pairs = set()
for f in glob.glob(os.path.join(ROOT, "Assets", "Data", "Maps", "Harita_*.json")):
    for it in json.load(open(f, encoding="utf-8"))["items"]:
        if it["paint"] in PAINTS: pairs.add((it["a"], it["paint"]))
def to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - 0.055)
cache = {}
for model, paint in sorted(pairs):
    d = os.path.join(ROOT, "Assets", "Art", "Environment", "Meshy", model + "_Tex")
    if model not in cache:
        img = Image.open(os.path.join(d, f"{model}_0_base.png")).convert("RGB").resize((1024, 1024), Image.LANCZOS)
        cache[model] = to_lin(np.asarray(img).astype(np.float32) / 255.0)
    lin = cache[model]
    lum = (lin * np.array([0.2126, 0.7152, 0.0722])).sum(-1, keepdims=True)
    hs = np.clip((lum + (lin - lum) * 0.25) * 2.4, 0, 1)                 # doygunluk 0.25, değer x2.4
    out = hs * (1 - 0.58) + np.array(PAINTS[paint]) * 0.58
    Image.fromarray((np.clip(to_srgb(out), 0, 1) * 255).astype(np.uint8)).save(os.path.join(d, f"{model}_Boya_{paint}.png"))
    print("BOYA", model, paint)
