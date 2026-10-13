"""
OYUN İÇİ TEMA ÖNİZLEMESİ (yalnız tasarım; oyunu değiştirmez). Arayüzsüz kare (ConceptCapture testi) + renk düzenlemesi +
"Efsane" temasında yeniden çizilmiş gösterge. İki yön:
  A "Sur — Efsane": Diyarbakır korunur; gölgeler turkuaz, ışıklar altın, yumuşak parlama.
  B "Masal": daha canlı/büyülü; mor-turkuaz gölge, altın ışık, uçuşan ışık zerreleri, sis, toon kontur.
Çıktı: docs/concept/ingame_A.png, ingame_B.png, ingame_AB.png      Çalıştır: python Tools/make_concept.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = r"D:\Repolar\junpline"
SRC = os.path.join(ROOT, "Logs", "Concept")
OUT = os.path.join(ROOT, "docs", "concept")
FX = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Montserrat-ExtraBold.ttf")
FT = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Exo2-BlackItalic.ttf")
ICONS = os.path.join(ROOT, "Assets", "Art", "UI", "Icons")
PORTRAITS = os.path.join(ROOT, "Assets", "Art", "UI", "Portraits")
W, H = 1080, 1920
GOLD, GOLD_D, TEAL_D, TEAL_L, BROWN = (242, 194, 78), (168, 112, 30), (14, 69, 85), (46, 147, 174), (74, 36, 8)
random.seed(4)


# ------------------------------------------------------------------ renk düzenlemesi

def grade(img, shadow, highlight, lift, sat, bright):
    a = np.asarray(img.convert("RGB")).astype(np.float32) / 255.0
    lum = (0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2])[..., None]
    a = a * bright + lift                                  # karanlığı aç
    g = np.clip(lum * bright, 0, 1)
    tint = np.array(shadow) / 255.0 * (1 - g) + np.array(highlight) / 255.0 * g   # bölünmüş tonlama
    a = a * 0.72 + a * tint * 0.55
    gray = (0.299 * a[..., 0] + 0.587 * a[..., 1] + 0.114 * a[..., 2])[..., None]
    a = gray + (a - gray) * sat
    return Image.fromarray(np.clip(a * 255, 0, 255).astype(np.uint8), "RGB")


def bloom(img, threshold=180, radius=26, strength=0.55):
    bright = img.point(lambda v: 255 if v > threshold else int(v * 0.25)).filter(ImageFilter.GaussianBlur(radius))
    return Image.blend(img, Image.fromarray(np.clip(np.asarray(img).astype(np.int32) + np.asarray(bright).astype(np.int32) * strength, 0, 255).astype(np.uint8)), 1.0)


def vignette(img, color, strength):
    yy, xx = np.mgrid[0:H, 0:W]
    d = np.sqrt(((xx - W / 2) / (W / 2)) ** 2 + ((yy - H * 0.45) / (H / 2)) ** 2)
    k = np.clip((d - 0.55) / 0.7, 0, 1)[..., None] * strength
    a = np.asarray(img).astype(np.float32)
    a = a * (1 - k) + np.array(color, dtype=np.float32) * k
    return Image.fromarray(a.astype(np.uint8))


def toon_edges(img):
    e = img.convert("L").filter(ImageFilter.FIND_EDGES).point(lambda v: 255 if v > 38 else 0).filter(ImageFilter.MaxFilter(3))
    dark = Image.new("RGB", img.size, (30, 18, 30))
    return Image.composite(dark, img, e.point(lambda v: int(v * 0.55)))


def posterize_soft(img):
    return Image.blend(img, img.quantize(48, method=Image.Quantize.MEDIANCUT).convert("RGB"), 0.35)


def motes(img, n, colors):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(n):
        x, y = random.uniform(0, W), random.uniform(250, H * 0.75)
        r = random.uniform(3, 10)
        c = random.choice(colors)
        d.ellipse((x - r * 3, y - r * 3, x + r * 3, y + r * 3), fill=c + (40,))
        d.ellipse((x - r, y - r, x + r, y + r), fill=c + (220,))
    layer = layer.filter(ImageFilter.GaussianBlur(1.5))
    out = img.convert("RGBA")
    out.alpha_composite(layer)
    return out.convert("RGB")


def mist(img, color, top, bottom, alpha):
    m = Image.new("L", img.size, 0)
    d = ImageDraw.Draw(m)
    for _ in range(26):
        x, y = random.uniform(-100, W + 100), random.uniform(top, bottom)
        r = random.uniform(120, 260)
        d.ellipse((x - r * 1.6, y - r * 0.5, x + r * 1.6, y + r * 0.5), fill=alpha)
    m = m.filter(ImageFilter.GaussianBlur(60))
    return Image.composite(Image.new("RGB", img.size, color), img, m)


# ------------------------------------------------------------------ gösterge parçaları

def font(path, size):
    return ImageFont.truetype(path, size)


def outlined(d, xy, text, f, fill=(255, 255, 255), stroke=BROWN, sw=6, anchor="mm"):
    d.text(xy, text, font=f, fill=fill, stroke_width=sw, stroke_fill=stroke, anchor=anchor)


def vgrad(w, h, top, bottom):
    g = Image.new("RGB", (w, h))
    gd = ImageDraw.Draw(g)
    for y in range(h):
        t = y / max(1, h - 1)
        gd.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    return g


def pill(canvas, box, top, bottom, border=GOLD, bw=5, radius=None, shadow=True, gloss=True):
    x0, y0, x1, y1 = [int(v) for v in box]
    w, h = x1 - x0, y1 - y0
    r = radius if radius is not None else h // 2
    if shadow:
        sh = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).rounded_rectangle((x0, y0 + 7, x1, y1 + 7), r, fill=(40, 20, 4, 150))
        canvas.alpha_composite(sh.filter(ImageFilter.GaussianBlur(3)))
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), r, fill=255)
    canvas.paste(vgrad(w, h, top, bottom), (x0, y0), mask)
    d = ImageDraw.Draw(canvas)
    if gloss:
        gl = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(gl).rounded_rectangle((w * 0.06, h * 0.08, w * 0.94, h * 0.46), int(r * 0.6), fill=(255, 255, 255, 70))
        canvas.alpha_composite(gl, (x0, y0))
    d.rounded_rectangle((x0, y0, x1, y1), r, outline=border, width=bw)


def gem(d, cx, cy, s):
    d.polygon([(cx, cy - s * 1.35), (cx + s * 1.35, cy), (cx, cy + s * 1.35), (cx - s * 1.35, cy)], fill=GOLD)
    d.polygon([(cx, cy - s), (cx + s, cy), (cx, cy + s), (cx - s, cy)], fill=(127, 240, 228))


def ribbon_curl(d, x, y, s, flip=False):
    sx = -1 if flip else 1
    d.arc((x - 60 * s, y - 40 * s, x + 60 * s, y + 40 * s), 200 if not flip else 300, 320 if not flip else 60, fill=GOLD, width=int(10 * s))
    d.arc((x - 40 * s + sx * 30 * s, y - 20 * s, x + 40 * s + sx * 30 * s, y + 40 * s), 20, 140, fill=(95, 224, 208), width=int(8 * s))


def heart(d, cx, cy, s, fill):
    d.ellipse((cx - s, cy - s * 0.7, cx, cy + s * 0.3), fill=fill)
    d.ellipse((cx, cy - s * 0.7, cx + s, cy + s * 0.3), fill=fill)
    d.polygon([(cx - s * 0.98, cy - s * 0.1), (cx + s * 0.98, cy - s * 0.1), (cx, cy + s)], fill=fill)
    d.ellipse((cx - s * 0.6, cy - s * 0.45, cx - s * 0.25, cy - s * 0.15), fill=(255, 200, 200))


def icon(canvas, name, box):
    p = os.path.join(ICONS, name + ".png")
    if not os.path.exists(p):
        return
    im = Image.open(p).convert("RGBA").resize((int(box[2] - box[0]), int(box[3] - box[1])), Image.LANCZOS)
    canvas.alpha_composite(im, (int(box[0]), int(box[1])))


def round_portrait(canvas, path, cx, cy, r):
    d = ImageDraw.Draw(canvas)
    d.ellipse((cx - r - 9, cy - r - 9, cx + r + 9, cy + r + 9), fill=GOLD, outline=GOLD_D, width=3)
    d.ellipse((cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3), fill=TEAL_L)
    if os.path.exists(path):
        p = Image.open(path).convert("RGBA").resize((r * 2, r * 2), Image.LANCZOS)
        m = Image.new("L", (r * 2, r * 2), 0)
        ImageDraw.Draw(m).ellipse((0, 0, r * 2 - 1, r * 2 - 1), fill=255)
        bg = Image.new("RGBA", (r * 2, r * 2), (245, 227, 190, 255))
        bg.alpha_composite(p)
        canvas.paste(bg, (cx - r, cy - r), m)


def sparkle(d, cx, cy, s, a=255):
    pts = []
    for k in range(8):
        ang = k * math.pi / 4
        rr = s if k % 2 == 0 else s * 0.35
        pts.append((cx + math.cos(ang) * rr, cy + math.sin(ang) * rr))
    d.polygon(pts, fill=(255, 238, 150, a))


def hud(base, legend_portrait, level_text, city):
    c = base.convert("RGBA")
    d = ImageDraw.Draw(c)
    # üst bant: bölüm adı (turkuaz, altın kenar, kurdeleler, mücevher)
    pill(c, (210, 46, 870, 150), TEAL_L, TEAL_D, bw=6)
    d = ImageDraw.Draw(c)
    ribbon_curl(d, 230, 98, 0.9, flip=True)
    ribbon_curl(d, 850, 98, 0.9)
    outlined(d, (540, 88), level_text, font(FX, 46), sw=5)
    d.text((540, 128), city, font=font(FX, 24), fill=(255, 230, 150), anchor="mm")
    gem(d, 540, 152, 13)
    # duraklat (altın yuvarlak)
    pill(c, (938, 44, 1048, 154), (255, 217, 87), (232, 144, 26), border=(255, 244, 204), bw=5)
    d = ImageDraw.Draw(c)
    d.rounded_rectangle((972, 74, 986, 124), 4, fill=(255, 255, 255))
    d.rounded_rectangle((1000, 74, 1014, 124), 4, fill=(255, 255, 255))
    # canavar: portre + kalpler + mesafe çubuğu
    round_portrait(c, legend_portrait, 112, 250, 62)
    d = ImageDraw.Draw(c)
    pill(c, (186, 206, 560, 270), (10, 42, 58), (18, 60, 80), bw=4, gloss=False)
    d = ImageDraw.Draw(c)
    for k in range(4):
        heart(d, 236 + k * 84, 236, 26, (230, 50, 60) if k < 3 else (70, 60, 72))
    pill(c, (186, 286, 640, 318), (8, 35, 49), (12, 48, 66), bw=3, gloss=False, shadow=False)
    pill(c, (190, 290, 470, 314), (255, 224, 122), (242, 154, 29), border=(255, 244, 204), bw=2, gloss=False, shadow=False)
    d = ImageDraw.Draw(c)
    # para + puan
    pill(c, (690, 188, 1048, 258), TEAL_L, TEAL_D, bw=5)
    d = ImageDraw.Draw(c)
    d.ellipse((990, 196, 1044, 250), fill=(255, 205, 64), outline=(180, 110, 20), width=4)
    d.ellipse((1004, 210, 1030, 236), outline=(200, 130, 30), width=4)
    outlined(d, (900, 222), "1,248", font(FX, 40), fill=(255, 226, 120), sw=4)
    pill(c, (750, 272, 1048, 330), (16, 70, 90), (10, 46, 62), bw=4)
    d = ImageDraw.Draw(c)
    d.text((790, 301), "PUAN", font=font(FX, 24), fill=(127, 240, 228), anchor="lm")
    outlined(d, (1020, 301), "3,960", font(FX, 32), sw=3, anchor="rm")
    # atma düğmesi (tombul altın, karpuz, sayaç, yıldızlar)
    cx, cy, r = 920, 1700, 118
    d.ellipse((cx - r - 16, cy - r - 16 + 8, cx + r + 16, cy + r + 16 + 8), fill=(60, 30, 6, 160))
    d.ellipse((cx - r - 16, cy - r - 16, cx + r + 16, cy + r + 16), fill=GOLD_D)
    face = vgrad(2 * r, 2 * r, (255, 226, 110), (232, 140, 26))
    m = Image.new("L", (2 * r, 2 * r), 0)
    ImageDraw.Draw(m).ellipse((0, 0, 2 * r - 1, 2 * r - 1), fill=255)
    c.paste(face, (cx - r, cy - r), m)
    d = ImageDraw.Draw(c)
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(255, 246, 210), width=7)
    d.ellipse((cx - r * 0.7, cy - r * 0.85, cx + r * 0.7, cy - r * 0.15), fill=(255, 255, 255, 60))
    icon(c, "throw_melon", (cx - 78, cy - 92, cx + 78, cy + 64))
    d = ImageDraw.Draw(c)
    outlined(d, (cx, cy + 72), "AT", font(FX, 46), sw=6)
    d.ellipse((cx + 70, cy - 132, cx + 140, cy - 62), fill=(214, 50, 46), outline=(255, 255, 255), width=5)
    outlined(d, (cx + 105, cy - 97), "x2", font(FX, 30), sw=3)
    for k in range(9):
        a = k / 9 * math.tau + 0.3
        rr = r + random.uniform(30, 60)
        sparkle(d, cx + math.cos(a) * rr, cy + math.sin(a) * rr, random.uniform(10, 20), random.randint(140, 255))
    # kontrol ipucu şeridi (altın kenarlı yarı saydam)
    tip = Image.new("RGBA", c.size, (0, 0, 0, 0))
    td = ImageDraw.Draw(tip)
    td.rounded_rectangle((90, 1500, 700, 1570), 35, fill=(10, 40, 55, 170), outline=GOLD, width=3)
    c.alpha_composite(tip)
    d = ImageDraw.Draw(c)
    d.text((395, 1535), "▲ ZIPLA   ▼ KAY   ◀ ▶ ŞERİT", font=font(FX, 30), fill=(255, 240, 200), anchor="mm")
    return c.convert("RGB")


def label(img, text):
    c = img.convert("RGBA")
    d = ImageDraw.Draw(c)
    pill(c, (40, 1820, 640, 1890), (40, 30, 20), (20, 14, 10), border=GOLD, bw=3, gloss=False, shadow=False)
    d = ImageDraw.Draw(c)
    d.text((340, 1855), text, font=font(FX, 30), fill=(255, 230, 160), anchor="mm")
    return c.convert("RGB")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    src1 = Image.open(os.path.join(SRC, "concept_lv1.png")).convert("RGB").resize((W, H))
    # A: Sur — Efsane
    a = grade(src1, shadow=(40, 150, 170), highlight=(255, 200, 120), lift=0.07, sat=1.25, bright=1.45)
    a = bloom(a, 170, 22, 0.5)
    a = vignette(a, (8, 40, 54), 0.55)
    a = hud(a, os.path.join(PORTRAITS, "tepegoz.png"), "BÖLÜM 1", "DİYARBAKIR · TEPEGÖZ")
    a = label(a, "A · SUR — EFSANE")
    a.save(os.path.join(OUT, "ingame_A.png"))
    # B: Masal
    src2 = Image.open(os.path.join(SRC, "concept_lv2.png")).convert("RGB").resize((W, H))
    b = grade(src2, shadow=(90, 70, 170), highlight=(255, 190, 110), lift=0.09, sat=1.5, bright=1.55)
    b = posterize_soft(b)
    b = toon_edges(b)
    b = mist(b, (170, 220, 230), 300, 900, 120)
    b = bloom(b, 160, 30, 0.7)
    b = motes(b, 60, [(255, 230, 140), (130, 240, 230), (255, 170, 210)])
    b = vignette(b, (40, 20, 70), 0.6)
    b = hud(b, os.path.join(PORTRAITS, "sahmeran.png"), "BÖLÜM 2", "MARDİN · ŞAHMERAN")
    b = label(b, "B · MASAL")
    b.save(os.path.join(OUT, "ingame_B.png"))
    ab = Image.new("RGB", (W * 2 + 40, H), (12, 30, 40))
    ab.paste(a, (0, 0))
    ab.paste(b, (W + 40, 0))
    ab.resize(((W * 2 + 40) // 2, H // 2), Image.LANCZOS).save(os.path.join(OUT, "ingame_AB.png"))
    print("CONCEPT_OK")
