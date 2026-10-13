"""
Efsaneler Haritası arka planı — kullanıcı referansları (eski Türkiye haritası + hazine haritası) harmanı, kendi çizimimiz.
Doğu/Güneydoğu Anadolu, üslup haritası (ölçekli değil; şehirler oyundaki durak düzenine göre yerleşir):
  parşömen + yanık kenar, çiçekli süslü çerçeve, Karadeniz (dalga, yelkenli), komşular (Gürcistan, Ermenistan, İran,
  Irak, Suriye) kesik sınırlarla, Fırat/Dicle/Aras/Murat, Van Gölü + Akdamar, Kaçkar/Toros dağları, Ağrı Dağı,
  simge yapılar (Sur surları, Mardin evleri, Hasankeyf köprüsü, Göbeklitepe, Nemrut başları, Ani, İshak Paşa),
  şehir noktaları, sisli "kayıp topraklar" (??? durakları), pusula gülü, ölçek çubuğu,
  hazine haritası gibi fırça darbeli kıvrımlı yol + her durağın önünde açık uçlu kalem oku.
Durak konumları MainMenuBuilder.Map.cs ile AYNI (1116×2000 arayüz koordinatı); resim 1.5× çözünürlükte.
Çıktı: Assets/Art/UI/Map/LegendsMap.png      Çalıştır: python Tools/make_map_art.py
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = r"D:\Repolar\junpline"
OUT = os.path.join(ROOT, "Assets", "Art", "UI", "Map", "LegendsMap.png")
FONT_X = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Montserrat-ExtraBold.ttf")
FONT_B = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Montserrat-Bold.ttf")
FONT_M = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Montserrat-Medium.ttf")
S = 1.5
W, H = int(1116 * S), int(2000 * S)
INK = (74, 46, 22, 255)
INK_SOFT = (110, 76, 40, 255)
WATER = (106, 160, 182, 255)
SEA = (62, 92, 132, 255)
LAND_SHADE = (214, 186, 140, 255)
# Map.cs ile aynı: (x, y) arayüz koordinatı — Diyarbakır, Mardin, Hasankeyf, Şanlıurfa, Van, Kars, ???, ???
STOPS = [(500, 1250), (230, 1450), (790, 1420), (190, 1070), (860, 960), (800, 640), (300, 760), (540, 455)]   # alt menünün üstünde kalır
NODE_R = 92
ROUTE_MIDS = {1: (510, 1590), 2: (660, 1090)}   # Hasankeyf → Şanlıurfa: Diyarbakır'ın kuzeyinden dolaşır   # yol, durak çerçevesinin bu kadar dışında biter (arayüz px)
random.seed(7)
rng = np.random.default_rng(7)


def P(x, y):
    return (x * S, y * S)


def F(path, size):
    return ImageFont.truetype(path, int(size * S))


# ---------------------------------------------------------------- yardımcılar

def smooth_noise(cells, size):
    small = rng.random((cells + 3, cells + 3))
    ys = np.linspace(0, cells, size[1], endpoint=False)
    xs = np.linspace(0, cells, size[0], endpoint=False)
    y0, x0 = ys.astype(int), xs.astype(int)
    fy, fx = (ys - y0)[:, None], (xs - x0)[None, :]
    fy, fx = fy * fy * (3 - 2 * fy), fx * fx * (3 - 2 * fx)
    a, b = small[y0][:, x0], small[y0][:, x0 + 1]
    c, d = small[y0 + 1][:, x0], small[y0 + 1][:, x0 + 1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def catmull(pts, steps=24):
    out = []
    p = [pts[0]] + pts + [pts[-1]]
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        for k in range(steps):
            t = k / steps
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2
                                     + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    out.append(pts[-1])
    return out


def text(img, xy, s, font, fill, angle=0, spacing=0):
    """Döndürülebilir, harf aralıklı harita yazısı (merkezden)."""
    if spacing:
        s = (" " * spacing).join(s)
    tmp = Image.new("RGBA", (int(len(s) * font.size * 0.9) + 40, int(font.size * 1.8)), (0, 0, 0, 0))
    ImageDraw.Draw(tmp).text((tmp.width / 2, tmp.height / 2), s, font=font, fill=fill, anchor="mm")
    if angle:
        tmp = tmp.rotate(angle, expand=True, resample=Image.BICUBIC)
    x, y = P(*xy)
    img.alpha_composite(tmp, (int(x - tmp.width / 2), int(y - tmp.height / 2)))


def blob(cx, cy, rx, ry, n=40, wob=0.16, seed=0):
    r = random.Random(seed)
    ph = [r.uniform(0, 6.28) for _ in range(3)]
    pts = []
    for k in range(n):
        a = k / n * math.tau
        rr = 1 + wob * math.sin(a * 3 + ph[0]) + wob * 0.6 * math.sin(a * 5 + ph[1]) + wob * 0.3 * math.sin(a * 9 + ph[2])
        pts.append(P(cx + math.cos(a) * rx * rr, cy + math.sin(a) * ry * rr))
    return pts


# ---------------------------------------------------------------- zemin

def parchment():
    n = smooth_noise(5, (W, H)) * 0.5 + smooth_noise(20, (W, H)) * 0.3 + smooth_noise(140, (W, H)) * 0.2
    base = np.array([243, 228, 194], dtype=np.float32)
    img = base * (0.95 + 0.07 * n[..., None])
    yy, xx = np.mgrid[0:H, 0:W]
    dx, dy = (xx - W / 2) / (W / 2), (yy - H / 2) / (H / 2)
    edge = np.clip((np.maximum(np.abs(dx), np.abs(dy)) - 0.8) / 0.2, 0, 1) ** 1.5
    img = img * (1 - edge[..., None] * 0.5)
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def neighbours(img, d):
    """Komşu ülkeler: hafif farklı ton + kesik sınır çizgisi + harf aralıklı adlar."""
    regions = [
        ([(560, 300), (1116, 300), (1116, 560), (980, 520), (860, 470), (720, 420)], (226, 222, 196, 255)),   # Gürcistan / Ermenistan
        ([(1116, 560), (1116, 1520), (1060, 1400), (1050, 1150), (1070, 900), (1040, 700)], (232, 214, 186, 255)),   # İran
        ([(0, 1700), (620, 1690), (760, 1660), (1116, 1560), (1116, 2000), (0, 2000)], (232, 214, 186, 255)),   # Suriye / Irak
    ]
    for poly, col in regions:
        d.polygon([P(*p) for p in poly], fill=col)
    for line in ([(720, 420), (860, 470), (980, 520), (1116, 560)], [(1040, 700), (1070, 900), (1050, 1150), (1060, 1400), (1116, 1520)],
                 [(0, 1700), (620, 1690), (760, 1660), (1116, 1560)]):
        pts = catmull([P(*p) for p in line], 20)
        for a, b in list(zip(pts, pts[1:]))[::2]:
            d.line([a, b], fill=(140, 90, 60, 230), width=int(4 * S))
    f = F(FONT_B, 24)
    text(img, (880, 360), "GÜRCİSTAN", f, (120, 96, 70, 255), spacing=1)
    text(img, (1050, 610), "ERMENİSTAN", F(FONT_B, 20), (120, 96, 70, 255), angle=-80)
    text(img, (1088, 1180), "İ R A N", f, (120, 96, 70, 255), angle=-90)
    text(img, (930, 1760), "I R A K", f, (120, 96, 70, 255))
    text(img, (210, 1770), "S U R İ Y E", f, (120, 96, 70, 255))


def black_sea(img, d):
    poly = [(0, 300), (560, 300), (520, 380), (430, 470), (330, 520), (200, 560), (80, 600), (0, 640)]
    pts = catmull([P(*p) for p in poly], 14)
    d.polygon(pts, fill=SEA)
    d.line(pts[1:-1], fill=INK, width=int(4 * S))
    for k in range(18):   # dalgalar
        x, y = random.uniform(30, 470), random.uniform(320, 540)
        if y > 610 - x * 0.55:
            continue
        cx, cy = P(x, y)
        d.arc((cx - 16 * S, cy - 6 * S, cx + 16 * S, cy + 6 * S), 200, 340, fill=(190, 210, 230, 255), width=int(3 * S))
    ship(d, 150, 430, 1.0)
    text(img, (280, 360), "KARADENİZ", F(FONT_B, 28), (225, 232, 240, 255), spacing=1)


def ship(d, x, y, s):
    cx, cy = P(x, y)
    k = S * s
    d.polygon([(cx - 40 * k, cy), (cx + 40 * k, cy), (cx + 28 * k, cy + 16 * k), (cx - 30 * k, cy + 16 * k)], fill=(140, 92, 52, 255), outline=INK)
    d.line([(cx, cy), (cx, cy - 64 * k)], fill=INK, width=int(4 * S))
    d.polygon([(cx + 3 * k, cy - 60 * k), (cx + 36 * k, cy - 12 * k), (cx + 3 * k, cy - 12 * k)], fill=(250, 244, 226, 255), outline=INK)
    d.polygon([(cx - 3 * k, cy - 50 * k), (cx - 30 * k, cy - 14 * k), (cx - 3 * k, cy - 14 * k)], fill=(250, 244, 226, 255), outline=INK)
    d.polygon([(cx, cy - 64 * k), (cx + 16 * k, cy - 58 * k), (cx, cy - 52 * k)], fill=(190, 50, 40, 255))


def river(d, pts, width):
    line = catmull([P(*p) for p in pts], 18)
    d.line(line, fill=(84, 140, 162, 255), width=int((width + 4) * S), joint="curve")
    d.line(line, fill=WATER, width=int(width * S), joint="curve")
    d.line(line, fill=(170, 210, 222, 200), width=max(1, int(width * S * 0.3)), joint="curve")


def lake(img, d):
    pts = blob(1010, 1200, 92, 58, seed=3)
    d.polygon(pts, fill=WATER)
    d.line(pts + [pts[0]], fill=INK, width=int(3 * S))
    for k in range(5):
        x, y = P(960 + k * 26, 1190 + (k % 2) * 16)
        d.arc((x - 13 * S, y - 5 * S, x + 13 * S, y + 5 * S), 200, 340, fill=(230, 245, 250, 255), width=int(3 * S))
    isl = blob(1050, 1218, 14, 9, seed=9)   # Akdamar adası + kilise
    d.polygon(isl, fill=LAND_SHADE, outline=INK)
    cx, cy = P(1050, 1210)
    d.rectangle((cx - 7 * S, cy - 10 * S, cx + 7 * S, cy), fill=(200, 170, 128, 255), outline=INK)
    d.polygon([(cx - 9 * S, cy - 10 * S), (cx, cy - 22 * S), (cx + 9 * S, cy - 10 * S)], fill=(170, 120, 80, 255), outline=INK)
    text(img, (1010, 1285), "VAN GÖLÜ", F(FONT_B, 20), (60, 100, 122, 255))


def mountain(d, x, y, s, snow=True):
    x, y = P(x, y)
    h, w = 46 * S * s, 40 * S * s
    d.polygon([(x - w, y), (x, y - h), (x + w, y)], fill=LAND_SHADE)
    d.polygon([(x, y - h), (x + w, y), (x + w * 0.2, y)], fill=(178, 142, 98, 255))
    if snow:
        d.polygon([(x - w * 0.32, y - h * 0.64), (x, y - h), (x + w * 0.32, y - h * 0.64), (x + w * 0.1, y - h * 0.58), (x - w * 0.08, y - h * 0.66)],
                  fill=(255, 252, 244, 255))
    d.line([(x - w, y), (x, y - h), (x + w, y)], fill=INK, width=int(3 * S))
    for k in range(3):   # yamaç tarama
        t = 0.35 + k * 0.18
        d.line([(x + w * t * 0.6, y - h * (1 - t)), (x + w * t * 0.25, y - h * (1 - t) * 0.6)], fill=INK_SOFT, width=int(2 * S))


def range_(d, cx, cy, n, spread, s):
    r = random.Random(int(cx * 7 + cy))
    for k in range(n):
        mountain(d, cx + (k - n / 2) * spread + r.uniform(-8, 8), cy + r.uniform(-14, 14), s * r.uniform(0.75, 1.15))


def tree(d, x, y):
    cx, cy = P(x, y)
    d.line([(cx, cy), (cx, cy + 16 * S)], fill=INK, width=int(4 * S))
    d.ellipse((cx - 15 * S, cy - 22 * S, cx + 15 * S, cy + 6 * S), fill=(120, 150, 80, 255), outline=INK, width=int(3 * S))
    d.arc((cx - 9 * S, cy - 17 * S, cx + 5 * S, cy - 4 * S), 200, 300, fill=(180, 205, 130, 255), width=int(3 * S))


def city(img, d, x, y, name, big=False):
    cx, cy = P(x, y)
    r = (9 if big else 7) * S
    d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=INK, outline=(250, 240, 220, 255), width=int(2 * S))
    text(img, (x, y + 24), name, F(FONT_B if big else FONT_M, 21 if big else 18), INK)


# ---------------------------------------------------------------- simge yapılar

def walls(d, x, y, s):
    cx, cy = P(x, y)
    w, h = 80 * S * s, 26 * S * s
    d.rectangle((cx - w, cy - h, cx + w, cy), fill=(70, 62, 58, 255), outline=INK, width=int(3 * S))   # bazalt
    for k in range(9):
        if k % 2 == 0:
            x0 = cx - w + (k + 0.5) * (2 * w / 9)
            d.rectangle((x0 - 7 * S * s, cy - h - 11 * S * s, x0 + 7 * S * s, cy - h), fill=(70, 62, 58, 255), outline=INK, width=int(2 * S))
    for bx in (cx - w, cx + w):
        d.rectangle((bx - 16 * S * s, cy - h * 1.9, bx + 16 * S * s, cy), fill=(56, 50, 48, 255), outline=INK, width=int(3 * S))


def houses(d, x, y):
    """Mardin: yamaçta basamaklanan bal rengi taş evler."""
    for row in range(3):
        for k in range(4 - row):
            cx, cy = P(x + k * 30 + row * 15, y - row * 24)
            d.rectangle((cx - 13 * S, cy - 18 * S, cx + 13 * S, cy), fill=(222, 182, 112, 255), outline=INK, width=int(2 * S))
            d.rectangle((cx - 4 * S, cy - 12 * S, cx + 4 * S, cy - 2 * S), fill=(120, 80, 40, 255))


def bridge(d, x, y):
    """Hasankeyf: yıkık köprü ayakları."""
    cx, cy = P(x, y)
    d.arc((cx - 70 * S, cy - 40 * S, cx + 10 * S, cy + 30 * S), 180, 360, fill=INK, width=int(6 * S))
    for px in (-70, 10, 50):
        d.rectangle((cx + (px - 8) * S, cy - 4 * S, cx + (px + 8) * S, cy + 26 * S), fill=(200, 170, 120, 255), outline=INK, width=int(2 * S))


def gobeklitepe(d, x, y):
    for k in range(3):
        cx, cy = P(x + k * 30, y + (k % 2) * 8)
        d.rectangle((cx - 5 * S, cy - 34 * S, cx + 5 * S, cy), fill=(214, 200, 168, 255), outline=INK, width=int(2 * S))
        d.rectangle((cx - 14 * S, cy - 44 * S, cx + 14 * S, cy - 34 * S), fill=(214, 200, 168, 255), outline=INK, width=int(2 * S))


def nemrut(d, x, y):
    """Nemrut Dağı: zirvede taş heykel başları."""
    mountain(d, x, y + 30, 1.4, snow=False)
    for k in range(3):
        cx, cy = P(x - 30 + k * 30, y + 36)
        d.ellipse((cx - 11 * S, cy - 26 * S, cx + 11 * S, cy - 4 * S), fill=(222, 206, 172, 255), outline=INK, width=int(2 * S))
        d.polygon([(cx - 10 * S, cy - 22 * S), (cx, cy - 38 * S), (cx + 10 * S, cy - 22 * S)], fill=(200, 180, 140, 255), outline=INK)


def ani(d, x, y):
    cx, cy = P(x, y)
    d.rectangle((cx - 30 * S, cy - 30 * S, cx + 30 * S, cy), fill=(196, 150, 110, 255), outline=INK, width=int(2 * S))
    d.polygon([(cx - 34 * S, cy - 30 * S), (cx, cy - 56 * S), (cx + 34 * S, cy - 30 * S)], fill=(170, 120, 82, 255), outline=INK)
    d.line([(cx + 12 * S, cy - 30 * S), (cx + 20 * S, cy - 4 * S)], fill=INK, width=int(3 * S))   # çatlak (harabe)


def palace(d, x, y):
    """İshak Paşa Sarayı: kubbe + minare."""
    cx, cy = P(x, y)
    d.rectangle((cx - 34 * S, cy - 24 * S, cx + 34 * S, cy), fill=(222, 196, 150, 255), outline=INK, width=int(2 * S))
    d.pieslice((cx - 18 * S, cy - 46 * S, cx + 18 * S, cy - 10 * S), 180, 360, fill=(200, 170, 120, 255), outline=INK)
    d.rectangle((cx + 26 * S, cy - 60 * S, cx + 34 * S, cy - 24 * S), fill=(222, 196, 150, 255), outline=INK, width=int(2 * S))


def mist(img, centers):
    m = Image.new("L", (W, H), 0)
    md = ImageDraw.Draw(m)
    for (sx, sy) in centers:
        for _ in range(16):
            x = (sx + random.uniform(-130, 130)) * S
            y = (sy + random.uniform(-90, 90)) * S
            r = random.uniform(45, 95) * S
            md.ellipse((x - r * 1.4, y - r, x + r * 1.4, y + r), fill=170)
    m = m.filter(ImageFilter.GaussianBlur(34 * S))
    cloud = Image.new("RGBA", (W, H), (250, 245, 232, 0))
    cloud.putalpha(m)
    img.alpha_composite(cloud)
    for (sx, sy) in centers:   # soru işaretleri
        for dx, dy, sz in ((-130, -40, 42), (140, 40, 34)):
            text(img, (sx + dx, sy + dy), "?", F(FONT_X, sz), (150, 120, 90, 180))


def compass(img, d, cx, cy, r):
    cx, cy, r = cx * S, cy * S, r * S
    for rr, w in ((1.0, 4), (0.86, 2), (0.36, 2)):
        d.ellipse((cx - r * rr, cy - r * rr, cx + r * rr, cy + r * rr), outline=INK, width=int(w * S))
    for k in range(16):
        a = k * math.pi / 8 - math.pi / 2
        L = r * (1.0 if k % 4 == 0 else 0.72 if k % 2 == 0 else 0.5)
        side = r * (0.14 if k % 4 == 0 else 0.09)
        tip = (cx + math.cos(a) * L, cy + math.sin(a) * L)
        l = (cx + math.cos(a - math.pi / 2) * side, cy + math.sin(a - math.pi / 2) * side)
        rr = (cx + math.cos(a + math.pi / 2) * side, cy + math.sin(a + math.pi / 2) * side)
        d.polygon([l, tip, (cx, cy)], fill=(178, 52, 40, 255) if k == 0 else (96, 64, 34, 255))
        d.polygon([rr, tip, (cx, cy)], fill=(242, 214, 150, 255))
        d.line([l, tip, rr], fill=INK, width=int(2 * S))
    f = F(FONT_X, 28)
    for t, a in (("K", -90), ("D", 0), ("G", 90), ("B", 180)):
        ax, ay = cx + math.cos(math.radians(a)) * r * 1.3, cy + math.sin(math.radians(a)) * r * 1.3
        d.text((ax, ay), t, font=f, fill=INK, anchor="mm")


def scale_bar(img, d, x, y):
    cx, cy = P(x, y)
    seg = 40 * S
    for k in range(4):
        d.rectangle((cx + k * seg, cy, cx + (k + 1) * seg, cy + 10 * S), fill=INK if k % 2 == 0 else (250, 240, 220, 255), outline=INK, width=int(2 * S))
    for k, t in enumerate(("0", "50", "100", "150", "200 km")):
        d.text((cx + k * seg, cy + 22 * S), t, font=F(FONT_M, 14), fill=INK, anchor="mm")


def floral_border(img):
    d = ImageDraw.Draw(img)
    m = 16 * S
    d.rectangle((m, m, W - m, H - m), outline=(104, 64, 30, 255), width=int(12 * S))
    d.rectangle((m + 22 * S, m + 22 * S, W - m - 22 * S, H - m - 22 * S), outline=(150, 104, 52, 255), width=int(3 * S))
    # sarmaşık: kenar boyunca dalgalı dal + yaprak + kırmızı çiçek
    inner = m + 11 * S
    def vine(p0, p1, horizontal):
        n = int(math.dist(p0, p1) / (46 * S))
        for k in range(n):
            t = (k + 0.5) / n
            x = p0[0] + (p1[0] - p0[0]) * t
            y = p0[1] + (p1[1] - p0[1]) * t
            o = math.sin(k * 1.3) * 3 * S
            if horizontal:
                y += o
            else:
                x += o
            if k % 3 == 0:
                d.ellipse((x - 6 * S, y - 6 * S, x + 6 * S, y + 6 * S), fill=(196, 64, 50, 255), outline=(250, 220, 160, 255), width=int(2 * S))
            else:
                ang = k * 1.1
                lx, ly = x + math.cos(ang) * 8 * S, y + math.sin(ang) * 8 * S
                d.ellipse((lx - 6 * S, ly - 3.5 * S, lx + 6 * S, ly + 3.5 * S), fill=(110, 140, 70, 255))
    vine((inner, inner), (W - inner, inner), True)
    vine((inner, H - inner), (W - inner, H - inner), True)
    vine((inner, inner), (inner, H - inner), False)
    vine((W - inner, inner), (W - inner, H - inner), False)
    gold, teal = (226, 178, 74, 255), (70, 190, 180, 255)
    for cx, cy in ((m, m), (W - m, m), (m, H - m), (W - m, H - m)):   # köşe madalyonları
        d.ellipse((cx - 34 * S, cy - 34 * S, cx + 34 * S, cy + 34 * S), fill=teal, outline=gold, width=int(7 * S))
        d.ellipse((cx - 12 * S, cy - 12 * S, cx + 12 * S, cy + 12 * S), fill=gold)


# ---------------------------------------------------------------- yol + oklar

def route(img):
    """Hazine haritası gibi: koyu kahve fırça darbesi (kalın, kenarı titrek) + her durağın önünde açık uçlu kalem oku."""
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(len(STOPS) - 1):
        a, b = STOPS[i], STOPS[i + 1]
        mid = ((a[0] + b[0]) / 2 + (b[1] - a[1]) * 0.18, (a[1] + b[1]) / 2 - (b[0] - a[0]) * 0.18)   # yay
        mid = ROUTE_MIDS.get(i, mid)   # yolların kesişmemesi için elle verilen ara noktalar
        curve = catmull([P(*a), P(*mid), P(*b)], 40)
        # duraklardan çerçeve yarıçapı kadar kırp
        def trim(pts, r):
            return [p for p in pts if math.dist(p, pts[0]) > r * S and math.dist(p, pts[-1]) > r * S]
        c = trim(curve, NODE_R)
        if len(c) < 4:
            continue
        for off, w, col in ((0, 13, (66, 36, 16, 255)), (1.5, 6, (96, 56, 26, 255))):   # ana iz + iç ton
            pts = [(x + random.uniform(-off, off), y + random.uniform(-off, off)) for x, y in c]
            d.line(pts, fill=col, width=int(w * S), joint="curve")
        # açık uçlu ok (V): yolun ucunda, sonraki durağa bakar
        tip, back = c[-1], c[-6]
        ang = math.atan2(tip[1] - back[1], tip[0] - back[0])
        L = 40 * S
        for da in (math.radians(150), math.radians(-150)):
            end = (tip[0] + math.cos(ang + da) * L, tip[1] + math.sin(ang + da) * L)
            d.line([tip, end], fill=(66, 36, 16, 255), width=int(13 * S))
            d.ellipse((end[0] - 6.5 * S, end[1] - 6.5 * S, end[0] + 6.5 * S, end[1] + 6.5 * S), fill=(66, 36, 16, 255))
        d.ellipse((tip[0] - 6.5 * S, tip[1] - 6.5 * S, tip[0] + 6.5 * S, tip[1] + 6.5 * S), fill=(66, 36, 16, 255))
    img.alpha_composite(layer.filter(ImageFilter.GaussianBlur(0.6 * S)))


# ---------------------------------------------------------------- ana

if __name__ == "__main__":
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img = parchment()
    d = ImageDraw.Draw(img, "RGBA")
    neighbours(img, d)
    black_sea(img, d)
    # nehirler
    river(d, [(560, 820), (480, 900), (380, 1000), (290, 1060), (180, 1240), (140, 1420), (120, 1560), (90, 1720), (60, 1900)], 12)   # Fırat
    river(d, [(740, 1000), (620, 1020), (500, 1040), (380, 1010)], 8)   # Murat
    river(d, [(380, 1250), (430, 1340), (520, 1440), (650, 1480), (780, 1500), (900, 1590), (1020, 1690), (1116, 1760)], 13)   # Dicle
    river(d, [(680, 560), (800, 600), (920, 640), (1030, 690), (1116, 720)], 8)   # Aras
    text(img, (150, 1350), "FIRAT", F(FONT_B, 22), (60, 100, 122, 255), angle=-80)
    text(img, (660, 1450), "DİCLE", F(FONT_B, 22), (60, 100, 122, 255), angle=-8)
    text(img, (620, 980), "MURAT", F(FONT_M, 16), (60, 100, 122, 255), angle=8)
    text(img, (930, 610), "ARAS", F(FONT_M, 16), (60, 100, 122, 255), angle=-18)
    lake(img, d)
    # dağlar
    range_(d, 770, 505, 4, 50, 1.1)    # Kaçkar
    text(img, (770, 440), "KAÇKAR DAĞLARI", F(FONT_B, 17), INK_SOFT, spacing=0)
    text(img, (960, 1370), "TOROS DAĞLARI", F(FONT_B, 17), INK_SOFT)
    range_(d, 960, 1340, 4, 60, 1.0)   # Güneydoğu Toroslar
    mountain(d, 1010, 790, 2.6)        # Ağrı Dağı
    text(img, (1010, 815), "AĞRI DAĞI", F(FONT_B, 18), INK)
    range_(d, 640, 880, 3, 60, 1.0)
    for x, y in ((420, 1460), (450, 1480), (1060, 1440), (1080, 1460), (700, 1730), (560, 1080), (600, 1090),
                 (690, 1380), (90, 860), (130, 880)):
        tree(d, x, y)
    # simge yapılar
    walls(d, 700, 1295, 0.6)
    houses(d, 40, 1420)
    bridge(d, 1010, 1470)
    gobeklitepe(d, 290, 1300)
    nemrut(d, 420, 1010)
    ani(d, 470, 705)
    palace(d, 1050, 920)
    # şehir noktaları
    for x, y, n in ((600, 780, "Erzurum"), (330, 600, "Trabzon"), (930, 860, "Ağrı"), (720, 1080, "Muş"), (800, 1180, "Bitlis"),
                    (470, 1000, "Elazığ"), (70, 930, "Malatya"), (250, 1310, "Adıyaman"), (60, 1290, "Gaziantep")):
        city(img, d, x, y, n)
    text(img, (640, 1700), "MEZOPOTAMYA OVASI", F(FONT_B, 18), INK_SOFT, spacing=0)
    compass(img, d, 110, 740, 70)
    scale_bar(img, d, 830, 1700)
    mist(img, [STOPS[6], STOPS[7]])   # kayıp topraklar (??? durakları)
    route(img)
    floral_border(img)
    img.convert("RGB").save(OUT, optimize=True)
    print("MAP_OK", OUT, img.size)
