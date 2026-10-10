"""
Mağaza ve uygulama görselleri (aşama 17 hazırlığı) — Pillow ile, oyunun kendi çıktılarından:
  • Assets/Art/UI/AppIcon.png (1024): Efsaneler Kitabı portresi, menü renk geçişi (bordo → lacivert), altın çerçeve
  • docs/store/feature_graphic.png (1024x500): ejderha bölümü karesi + başlık
  • docs/store/screenshots/*.png (1080x1920): oyun kareleri + üst şerit başlık (TR)
Kaynaklar: Assets/Art/UI/Portraits/book.png, Logs/Captures/*_01_intro.png, Logs/MenuCaptures/map.png
Çalıştır: python Tools/make_store_assets.py   (önce GameplayCapture testleri ve menü yakalaması)
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = r"D:\Repolar\junpline"
FONT_X = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Montserrat-ExtraBold.ttf")
FONT_T = os.path.join(ROOT, "Assets", "Art", "UI", "Fonts", "Exo2-BlackItalic.ttf")
CAP = os.path.join(ROOT, "Logs", "Captures")
MENU = os.path.join(ROOT, "Logs", "MenuCaptures")
STORE = os.path.join(ROOT, "docs", "store")
SHOTS = os.path.join(STORE, "screenshots")


def gradient(w, h, top, bottom):
    img = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(img)
    for y in range(h):
        t = y / max(1, h - 1)
        d.line([(0, y), (w, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    return img


def app_icon():
    size = 1024
    img = gradient(size, size, (90, 26, 48), (31, 42, 71))
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((170, 170, 854, 854), fill=(255, 190, 90, 120))
    img.paste(glow.filter(ImageFilter.GaussianBlur(90)), (0, 0), glow.filter(ImageFilter.GaussianBlur(90)))
    book = Image.open(os.path.join(ROOT, "Assets", "Art", "UI", "Portraits", "book.png")).convert("RGBA")
    book = book.resize((820, 820), Image.LANCZOS)
    img.paste(book, (102, 80), book)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((28, 28, size - 28, size - 28), radius=180, outline=(232, 185, 91), width=22)
    f = ImageFont.truetype(FONT_T, 150)
    text = "GO!"
    tw = d.textlength(text, font=f)
    d.text(((size - tw) / 2 + 6, 812 + 6), text, font=f, fill=(20, 10, 10))
    d.text(((size - tw) / 2, 812), text, font=f, fill=(255, 224, 138))
    out = os.path.join(ROOT, "Assets", "Art", "UI", "AppIcon.png")
    img.save(out)
    return out


def banner(img, title, sub=None):
    w, h = img.size
    band = 300
    canvas = Image.new("RGB", (w, h), (0, 0, 0))
    canvas.paste(img.resize((w, h)), (0, 0))
    top = gradient(w, band, (90, 26, 48), (31, 42, 71)).convert("RGBA")
    top.putalpha(235)
    canvas.paste(top, (0, 0), top)
    d = ImageDraw.Draw(canvas)
    f = ImageFont.truetype(FONT_X, 74)
    tw = d.textlength(title, font=f)
    d.text(((w - tw) / 2, 70), title, font=f, fill=(255, 255, 255))
    if sub:
        fs = ImageFont.truetype(FONT_X, 44)
        sw = d.textlength(sub, font=fs)
        d.text(((w - sw) / 2, 172), sub, font=fs, fill=(232, 185, 91))
    return canvas


def screenshots():
    os.makedirs(SHOTS, exist_ok=True)
    plan = [
        ("chase_02_holding.png", CAP, "EFSANELERİ KOVALA!", "Diyarbakır Sur'unda Tepegöz"),
        ("sahmeran_01_intro.png", CAP, "8 ŞEHİR · 8 EFSANE", "Mardin'de Şahmeran"),
        ("map.png", MENU, "EFSANELER KİTABI", "Her durakta yeni bir canavar"),
        ("kara_01_intro.png", CAP, "KAYDIR, ZIPLA, FIRLAT", "Kars'ta Karakoncolos"),
        ("humbaba_01_intro.png", CAP, "KARPUZU KAP, ATEŞLE!", "Hasankeyf'te Humbaba"),
        ("dragon_01_intro.png", CAP, "SON SAYFA: EJDERHA", "Kitabı sonsuza dek kapat"),
    ]
    made = []
    for i, (name, folder, title, sub) in enumerate(plan, 1):
        src = os.path.join(folder, name)
        if not os.path.exists(src):
            continue
        img = Image.open(src).convert("RGB").resize((1080, 1920), Image.LANCZOS)
        out = os.path.join(SHOTS, f"{i:02d}_{os.path.splitext(name)[0]}.png")
        banner(img, title, sub).save(out)
        made.append(out)
    return made


def feature_graphic():
    w, h = 1024, 500
    src = Image.open(os.path.join(CAP, "dragon_01_intro.png")).convert("RGB")
    sw, sh = src.size
    crop = src.crop((0, int(sh * 0.22), sw, int(sh * 0.22) + int(sw * h / w))).resize((w, h), Image.LANCZOS)
    shade = gradient(w, h, (20, 10, 20), (20, 10, 20)).convert("RGBA")
    mask = Image.linear_gradient("L").rotate(90).resize((w, h))
    shade.putalpha(mask.point(lambda v: int(v * 0.85)))
    crop.paste(shade, (0, 0), shade)
    d = ImageDraw.Draw(crop)
    f = ImageFont.truetype(FONT_T, 108)
    d.text((54, 120), "JUMPLINE GO", font=f, fill=(20, 10, 10))
    d.text((48, 114), "JUMPLINE GO", font=f, fill=(255, 255, 255))
    fs = ImageFont.truetype(FONT_X, 40)
    d.text((52, 262), "EFSANELER KİTABI", font=fs, fill=(232, 185, 91))
    d.text((52, 318), "8 şehir · 8 efsane · sonsuz koşu", font=ImageFont.truetype(FONT_X, 30), fill=(230, 230, 236))
    out = os.path.join(STORE, "feature_graphic.png")
    crop.save(out)
    return out


if __name__ == "__main__":
    os.makedirs(STORE, exist_ok=True)
    print("icon", app_icon())
    print("feature", feature_graphic())
    for s in screenshots():
        print("shot", s)
