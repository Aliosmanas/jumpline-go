"""
Diyarbakır sokağı dokuları (sistem python'u: pip install pillow numpy).
Çıktı: Blender/textures/*.png  (her doku 1024px = 4 m x 4 m)
  basalt.png   : siyah bazalt duvar (sıralı kesme taş + açık harç)
  paving.png   : büyük kesme taş döşeme
  curb.png     : kaldırım taşı (uzun bloklar)
  wood.png     : koyu ahşap (dikey tahta + damar)
"""
import os
import random

import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "textures")
S = 1024
PX_PER_M = S / 4.0
rng = random.Random(7)
np_rng = np.random.default_rng(7)


def noise(scale, octaves=4):
    """Basit değer gürültüsü (0..1)."""
    out = np.zeros((S, S))
    amp, total = 1.0, 0.0
    for o in range(octaves):
        n = max(2, int(S / scale * (2 ** o)))
        small = np_rng.random((n, n))
        img = Image.fromarray((small * 255).astype(np.uint8)).resize((S, S), Image.BICUBIC)
        out += np.asarray(img, dtype=float) / 255 * amp
        total += amp
        amp *= 0.5
    return out / total


def blocks(rows_h, len_range, base, var, mortar, mortar_w, offset_rows=True):
    img = np.zeros((S, S, 3))
    img[:] = mortar
    grain = noise(40, 5)
    y = 0
    row = 0
    while y < S:
        h = int(rows_h * PX_PER_M * rng.uniform(0.85, 1.15))
        x = -rng.randint(0, int(len_range[1] * PX_PER_M)) if offset_rows else 0
        while x < S:
            w = int(rng.uniform(*len_range) * PX_PER_M)
            shade = rng.uniform(-var, var)
            col = np.clip(np.array(base) + shade, 0, 1)
            x0, x1 = max(0, x + mortar_w), min(S, x + w - mortar_w)
            y0, y1 = y + mortar_w, min(S, y + h - mortar_w)
            if x1 > x0 and y1 > y0:
                g = grain[y0:y1, x0:x1, None]
                img[y0:y1, x0:x1] = col * (0.8 + 0.4 * g)
                # kenar gölgesi (taş hafif kabarık)
                img[y0:y0 + 2, x0:x1] *= 1.12
                img[y1 - 3:y1, x0:x1] *= 0.75
            x += w
        y += h
        row += 1
    return img


def save(name, arr, blur=0.6):
    os.makedirs(OUT, exist_ok=True)
    im = Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8))
    if blur:
        im = im.filter(ImageFilter.GaussianBlur(blur))
    im.save(os.path.join(OUT, name))
    print("yazıldı", name)


# Bazalt duvar: koyu gri-siyah, sıralar ~35 cm, açık gri harç
save("basalt.png", blocks(0.36, (0.45, 0.95), (0.20, 0.20, 0.215), 0.05, (0.47, 0.45, 0.42), 3))
# Döşeme: büyük düzensiz plakalar, koyu gri
save("paving.png", blocks(0.55, (0.45, 1.25), (0.27, 0.27, 0.285), 0.07, (0.44, 0.43, 0.41), 2))
# Kaldırım: uzun açık gri bloklar
save("curb.png", blocks(1.0, (1.0, 1.8), (0.46, 0.45, 0.43), 0.04, (0.30, 0.29, 0.28), 4, offset_rows=False))
# Ahşap: dikey tahtalar, damarlı
wood = np.zeros((S, S, 3))
streak = noise(8, 3)
x = 0
while x < S:
    w = int(rng.uniform(0.12, 0.2) * PX_PER_M)
    base = np.array((0.30, 0.19, 0.11)) * rng.uniform(0.8, 1.15)
    lines = np.sin(np.linspace(0, rng.uniform(20, 40), S))[:, None] * 0.06
    wood[:, x:x + w] = base * (0.85 + 0.3 * streak[:, x:x + w, None]) + lines[:, :, None] * 0.3
    wood[:, x:x + 2] *= 0.5
    x += w
save("wood.png", wood, blur=0.4)
