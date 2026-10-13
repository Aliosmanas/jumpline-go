import sys, os, numpy as np
from collections import deque
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SRC = r"D:\Repolar\junpline\docs\flow_ui\parts"
OUT = os.path.join(SRC, "cut")
os.makedirs(OUT, exist_ok=True)
S = 4  # etiketleme küçültmesi


def key(path):
    a = np.asarray(Image.open(path).convert("RGB")).astype(np.float32) / 255
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    gd = g - np.maximum(r, b)
    alpha = np.clip(1 - (gd - 0.10) / 0.22, 0, 1)
    g2 = np.where(gd > 0, np.maximum(r, b) + np.clip(gd, 0, 0.04), g)   # yeşil taşmasını sil
    return np.stack([r, g2, b], 2), alpha


def label(mask):
    h, w = mask.shape
    lab = np.zeros((h, w), np.int32)
    n = 0
    boxes = []
    for y in range(h):
        for x in range(w):
            if mask[y, x] and not lab[y, x]:
                n += 1
                q = deque([(y, x)])
                lab[y, x] = n
                y0 = y1 = y; x0 = x1 = x
                while q:
                    cy, cx = q.popleft()
                    y0 = min(y0, cy); y1 = max(y1, cy); x0 = min(x0, cx); x1 = max(x1, cx)
                    for ny, nx in ((cy + 1, cx), (cy - 1, cx), (cy, cx + 1), (cy, cx - 1)):
                        if 0 <= ny < h and 0 <= nx < w and mask[ny, nx] and not lab[ny, nx]:
                            lab[ny, nx] = n
                            q.append((ny, nx))
                boxes.append((n, y0, y1, x0, x1))
    return lab, boxes


for sheet in sys.argv[1:]:
    rgb, alpha = key(os.path.join(SRC, f"p_{sheet}.jpg"))
    H, W = alpha.shape
    small = Image.fromarray((alpha * 255).astype(np.uint8)).resize((W // S, H // S), Image.BOX)
    small = small.filter(ImageFilter.MaxFilter(3))
    lab, boxes = label(np.asarray(small) > 60)
    items = []
    for n, y0, y1, x0, x1 in boxes:
        if (y1 - y0 + 1) * (x1 - x0 + 1) < 40:
            continue
        items.append((y0 * S, y1 * S + S, x0 * S, x1 * S + S, n))
    items.sort(key=lambda t: (t[0] // 60, t[2]))
    prev = Image.new("RGB", (1200, 210 * ((len(items) + 5) // 6)), (60, 60, 70))
    d = ImageDraw.Draw(prev)
    f = ImageFont.truetype("arialbd.ttf", 18)
    for k, (y0, y1, x0, x1, n) in enumerate(items):
        y0 = max(y0 - 4, 0); x0 = max(x0 - 4, 0); y1 = min(y1 + 4, H); x1 = min(x1 + 4, W)
        m = np.asarray(Image.fromarray((lab == n).astype(np.uint8) * 255).resize((W // S * S, H // S * S), Image.NEAREST))
        m = np.pad(m, ((0, H - m.shape[0]), (0, W - m.shape[1])))[y0:y1, x0:x1] > 0
        m = np.asarray(Image.fromarray(m.astype(np.uint8) * 255).filter(ImageFilter.MaxFilter(9))) > 0
        al = alpha[y0:y1, x0:x1] * m
        im = Image.fromarray((np.dstack([rgb[y0:y1, x0:x1], al]) * 255).astype(np.uint8), "RGBA")
        im.save(os.path.join(OUT, f"{sheet}_{k:02d}.png"))
        t = im.copy(); t.thumbnail((190, 170))
        px, py = (k % 6) * 200, (k // 6) * 210
        prev.paste(t, (px + 5, py + 30), t)
        d.text((px + 5, py + 4), f"{sheet}_{k:02d} {im.size[0]}x{im.size[1]}", fill=(255, 230, 80), font=f)
    prev.save(rf"D:\tmp\cut_{sheet}.png")
    print(sheet, len(items))
