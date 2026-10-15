"""Sokak parkuru (Assets/Data/Course_SurIci.json) — kurallı üretici (15 Eki 2026, Subway Surfers akışı).
Kurallar:
  • Her an en az bir şerit yerden geçilebilir (üç şerit aynı anda aşılamaz engelle kapanmaz) — doğrulanır.
  • Tren: rampalı kamyon + önünde 2–3 minibüs (2 m ara: tavandan tavana zıpla / koş); yan şeritte minibüs dizisi
    (tavanda şerit değiştir), üçüncü şerit yerden kaçış.
  • Aynı engel üç şeride yan yana dizilmez; engeller şaşırtmalı (zikzak) konur.
  • Bölümler ilerledikçe trenler ve zikzaklar artar; son bölüm Portal Meydanı (portal satırı) değişmez.
Satır: z = bölüm başından metre (engelin koşucuya bakan ön kenarı), lane = -1 / 0 / 1, id = engel kümesi (GameplaySceneBuilder.BuildCourse)."""
import json, os, random

OUT = r"D:\Repolar\junpline\Assets\Data\Course_SurIci.json"
# engel boyu (koşu yönünde, m) ve türü: free=yerden geçilmez değil (zıpla/kay), solid=şerit değiştir / üstüne çık
DEPTH = {"low": 1.0, "crates": 1.1, "copper": 1.0, "teatable": 1.0, "bar": 0.6, "kilim": 0.6, "gate": 1.6,
         "wall": 1.6, "busstop": 3.1, "grill": 1.2, "stall": 1.5, "teacart": 1.5, "simitcart": 1.4, "melonstall": 1.5,
         "bus": 5.9, "citybus": 9.2, "taxi": 4.5, "patpat": 2.7, "truck": 6.6, "melon": 6.6, "tractor": 7.4}
PASSABLE = {"low", "crates", "copper", "teatable", "bar", "kilim", "gate"}   # yerden zıplanır / kayılır
LOW = ["low", "crates", "copper", "teatable"]
SIDE = ["wall", "stall", "grill", "teacart", "simitcart", "melonstall", "busstop"]
GAP = 2.0          # tren içi araç arası (zincir: ObstacleSpawner.chainGap 3.5'ten küçük → sabit kalır)


def lanes(rng):
    a = [-1, 0, 1]; rng.shuffle(a); return a


def p_jump(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, rng.choice(LOW))], 1.0


def p_jump_stagger(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, rng.choice(LOW)), (z + 8, b, rng.choice(LOW))], 9.0


def p_slide(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, "bar"), (z, b, rng.choice(SIDE[:4]))], 1.6


def p_weave(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, rng.choice(LOW)), (z + 8, b, "bar"), (z + 16, c, rng.choice(LOW))], 17.0


def p_wall_turn(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, rng.choice(SIDE)), (z, b, rng.choice(SIDE)), (z + 12, c, rng.choice(LOW + ["bar"]))], 13.0


def p_hop(rng, z):
    a, b, c = lanes(rng)
    return [(z, a, "taxi"), (z + 6, b, "patpat")], 8.7


def p_citybus(rng, z):
    a, b, c = lanes(rng)
    rows = [(z, a, "citybus"), (z + 2, b, "bus")]
    rows.append((z + 12, c, rng.choice(LOW)))
    return rows, 13.0


def p_gate(rng, z):
    a, b, c = lanes(rng)
    return [(z, 0, "gate"), (z, a, rng.choice(LOW)), (z + 7, b, "bar")], 8.0


def p_train(rng, z):
    a, b, c = lanes(rng)
    ramp = rng.choice(["truck", "truck", "melon", "tractor"])
    rows = [(z, a, ramp)]
    y = z + DEPTH[ramp] + GAP
    for i in range(rng.randint(2, 3)):                      # kamyon → minibüs → minibüs (tavandan tavana)
        rows.append((y, a, "bus")); y += DEPTH["bus"] + GAP
    # yan şerit: kamyon tavanından geçilebilen minibüs dizisi (yerden kapalı, tavandan şerit değiştirilir)
    y2 = z + 4.0
    for i in range(rng.randint(1, 3)):
        rows.append((y2, b, "bus")); y2 += DEPTH["bus"] + GAP
    # üçüncü şerit yerden kaçış: arada bir zıplanacak/kayılacak engel
    if rng.random() < 0.6: rows.append((z + rng.uniform(6, 12), c, rng.choice(LOW + ["bar"])))
    return rows, max(y, y2) - z


PATTERNS = {"jump": p_jump, "stagger": p_jump_stagger, "slide": p_slide, "weave": p_weave, "wallturn": p_wall_turn,
            "hop": p_hop, "citybus": p_citybus, "gate": p_gate, "train": p_train}
SECTIONS = [  # ad, uzunluk, desen ağırlıkları
    ("Dağkapı Girişi", 140, {"jump": 3, "stagger": 2, "slide": 2, "wallturn": 1}),
    ("Gazi Caddesi Çarşısı", 170, {"stagger": 2, "slide": 2, "weave": 2, "wallturn": 2, "hop": 1}),
    ("Dolmuş Durağı", 170, {"hop": 2, "citybus": 2, "train": 2, "slide": 1, "weave": 1}),
    ("Karpuz Kamyonu Treni", 190, {"train": 4, "weave": 1, "stagger": 1, "gate": 1}),
    ("Hasan Paşa Hanı Önü", 160, {"weave": 3, "gate": 2, "wallturn": 2, "jump": 1}),
    ("Bakırcılar Çarşısı", 160, {"stagger": 2, "slide": 2, "wallturn": 2, "hop": 1, "weave": 1}),
    ("Belediye Otobüsü Kavşağı", 190, {"train": 3, "citybus": 2, "hop": 1, "gate": 1}),
    ("Surdibi Koşusu", 180, {"weave": 2, "train": 2, "slide": 1, "stagger": 1, "wallturn": 1}),
]
PORTAL = ("Portal Meydanı", 130, [(26, -1, "low"), (26, 1, "low"), (48, -1, "wall"), (48, 1, "wall"), (66, 0, "portal")])


def validate(rows, length, name):
    """Her 0.5 m'de en az bir şerit yerden geçilebilir mi (aşılamaz engel üç şeridi birden kapatmıyor)."""
    z = 0.0
    while z < length:
        blocked = set()
        for rz, ln, i in rows:
            if i in PASSABLE or i == "portal": continue
            if rz <= z <= rz + DEPTH[i]: blocked.add(ln)
        if len(blocked) >= 3: raise SystemExit(f"GEÇİLEMEZ: {name} z={z:.1f}")
        z += 0.5


def main():
    rng = random.Random(20261015)
    data = {"sections": []}
    for name, length, weights in SECTIONS:
        rows = []; z = 14.0
        keys = list(weights); w = [weights[k] for k in keys]
        last = None
        while True:
            k = rng.choices(keys, w)[0]
            if k == last and len(keys) > 1: k = rng.choices(keys, w)[0]
            pr, plen = PATTERNS[k](rng, z)
            if z + plen > length - 10: break
            rows += pr; z += plen + rng.uniform(13.0, 18.0); last = k
        validate(rows, length, name)
        data["sections"].append({"name": name, "length": length,
                                 "rows": [{"z": round(rz, 2), "lane": ln, "id": i} for rz, ln, i in sorted(rows, key=lambda r: r[0])]})
    name, length, rows = PORTAL
    data["sections"].append({"name": name, "length": length, "rows": [{"z": z, "lane": l, "id": i} for z, l, i in rows]})
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh: json.dump(data, fh, ensure_ascii=False, indent=1)
    print("COURSE_OK", sum(len(s["rows"]) for s in data["sections"]), "rows,", len(data["sections"]), "sections")


main()
