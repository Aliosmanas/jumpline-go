import os, numpy as np
from PIL import Image

CUT = r"D:\Repolar\junpline\docs\flow_ui\parts\cut"
PARTS = r"D:\Repolar\junpline\docs\flow_ui\parts"
OUT = r"D:\Repolar\junpline\Assets\Art\UI\Skin"
os.makedirs(OUT, exist_ok=True)

MAP = {
    "btn_saffron": "12_00", "btn_turquoise": "12_02", "btn_red": "12_03", "btn_cobalt": "12_04", "btn_basalt": "12_05",
    "round_saffron": "12_06", "round_red": "12_07", "round_turquoise": "12_08",
    "panel_basalt": "10_00", "card_lime": "10_01", "plaque": "10_02", "ribbon": "10_03", "pill": "10_04",
    "navbar": "10_05", "parchment": "10_06",
    "frame_copper": "03_00", "frame_turquoise": "03_01", "frame_purple": "03_02", "frame_gold": "03_03",
    "medallion": "03_04", "hexbadge": "03_05", "badge_red": "03_06", "toggle_off": "03_08",
    "i_coin": "08_00", "i_gem": "08_01", "i_star": "08_02", "i_heart": "08_03", "i_gear": "08_04", "i_lock": "08_05",
    "i_chest": "08_06", "i_magnet": "08_07", "i_nazar": "08_08", "i_home": "08_09", "i_replay": "08_10", "i_music": "08_11",
    "i_scroll": "08_12", "i_book": "08_13", "i_sound": "08_14", "i_swords": "08_15",
    "i_shop": "07_00", "i_map": "07_01", "i_calendar": "07_02", "i_trophy": "07_03", "i_video": "07_04", "i_bolt": "07_05",
    "i_shield": "07_06", "i_melon": "07_07", "i_pause": "07_08", "i_back": "07_09", "i_close": "07_11", "i_globe": "07_12",
    "i_help": "07_13", "i_vibrate": "07_14", "i_bell": "07_15",
}


def load(k):
    return Image.open(os.path.join(CUT, k + ".png")).convert("RGBA")


def to_green(im, dark=False):
    """Turkuaz yüzeyi yeşile çevirir (#6CC24A → #3E8E2E); bakır/kahve kısımlar kalır."""
    a = np.asarray(im).astype(np.float32) / 255
    rgb = a[..., :3]
    mx = rgb.max(2); mn = rgb.min(2)
    teal = (rgb[..., 1] > rgb[..., 0] + 0.08) & (rgb[..., 2] > rgb[..., 0] + 0.05)
    lum = 0.3 * rgb[..., 0] + 0.59 * rgb[..., 1] + 0.11 * rgb[..., 2]
    hi = np.array([0.50, 0.85, 0.32]); lo = np.array([0.16, 0.45, 0.12])
    t = np.clip((lum - 0.15) / 0.75, 0, 1)[..., None]
    g = lo * (1 - t) + hi * t
    g = np.clip(g + np.clip(mn[..., None] - 0.6, 0, 1), 0, 1)   # beyaz parlama korunur
    rgb2 = np.where(teal[..., None], g, rgb)
    return Image.fromarray((np.dstack([rgb2, a[..., 3]]) * 255).astype(np.uint8), "RGBA")


def tint_check(im):
    a = np.asarray(im).astype(np.float32) / 255
    rgb = a[..., :3]
    lum = rgb.mean(2)
    body = lum > 0.12
    g = np.array([0.36, 0.78, 0.24])
    rgb2 = np.where(body[..., None], np.clip(g * (0.6 + lum[..., None] * 1.6), 0, 1), rgb)
    return Image.fromarray((np.dstack([rgb2, a[..., 3]]) * 255).astype(np.uint8), "RGBA")


for name, k in MAP.items():
    load(k).save(os.path.join(OUT, name + ".png"))
to_green(load("12_02")).save(os.path.join(OUT, "btn_green.png"))
to_green(load("12_08")).save(os.path.join(OUT, "round_green.png"))
on = load("03_08").transpose(Image.FLIP_LEFT_RIGHT)
a = np.asarray(on).astype(np.float32) / 255
red = (a[..., 0] > a[..., 1] + 0.15) & (a[..., 0] > a[..., 2] + 0.1)
lum = a[..., :3].mean(2)[..., None]
a[..., :3] = np.where(red[..., None], np.clip(np.array([0.30, 0.72, 0.22]) * (0.5 + lum * 1.8), 0, 1), a[..., :3])
Image.fromarray((a * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "toggle_on.png"))
tint_check(load("07_10")).save(os.path.join(OUT, "i_check.png"))

# arka planlar (opak)
Image.open(os.path.join(PARTS, "p_04.jpg")).convert("RGB").save(os.path.join(OUT, "bg_menu.png"))
Image.open(os.path.join(PARTS, "p_01.jpg")).convert("RGB").save(os.path.join(OUT, "bg_book.png"))
Image.open(r"D:\Repolar\junpline\docs\flow_ui\yukleme_1.jpg").convert("RGB").save(os.path.join(OUT, "bg_loading.png"))
print(len(os.listdir(OUT)))
