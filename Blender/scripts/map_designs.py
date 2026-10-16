"""JUMPLINE GO — 50+ tasarım haritası (map_kit.py ile). Referans: kullanıcının Blender'da yaptığı harita
(Blender/Harita/Referans_Harita_01.blend). Bölüm blokları elle tasarlandı; harita = blokların farklı sırası, aynalanması
ve model/aralık varyasyonları. Her harita geçilebilirlik simülasyonundan geçmezse başka varyasyonla yeniden kurulur.

blender -b -P map_designs.py -- [adet=54] [ilk=1]
"""
import bpy, sys, os, math, random, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import map_kit as K

L, M, R = -1, 0, 1
JUMPERS = ["CrateStack", "ClayJug", "Watermelon", "TeaTable", "CrateStack"]
DODGERS = ["TeaCart", "SimitCart", "MelonStall", "KebabGrill", "BusStop"]
GAP_JUMP = 8.0          # zıplama/kayma engelleri arası (kullanıcı: 5 m çok zor → 8)
ROW_GAP = 0.3           # yanaşık araçlar
ROOF_GAP = 3.0          # tavandan tavana atlama boşluğu


def jm(rng):
    return rng.choice(JUMPERS)


# ------------------------------------------------------------------ A: karışık araç + engel (referans tarzı)
def A_park_duvari(b, rng, y, mi):
    """Referans 10 m: iki yanda yanaşık minibüsler (1–2 sıra), ortada taksi (üstüne zıpla); sıraların sonunda ortada bariyer."""
    rows = 2 if rng.random() < 0.65 else 1
    end = y
    for side in (L, R):
        yy = y
        for r in range(rows):
            _, y0, y1, _ = b.vehicle("Minibus", side * mi, yy)
            yy = y1 + ROW_GAP
        end = max(end, yy - ROW_GAP)
    mid = rng.choice(["Taxi", "Taxi", "PatPat"])
    if mid == "Taxi": b.vehicle("Taxi", M, y)
    else: b.jump("PatPat", M, y + 0.5)
    b.slide(M, end) if rng.random() < 0.7 else b.jump("CrateStack", M, end, h=1.25)
    return end + 6.0


def A_otobus_tren(b, rng, y, mi):
    """Referans 27–69 m: ortada üstüne çıkılmaz otobüs; bir yanda sandık (zıpla), öbür yanda rampalı kamyon → kalas →
    2–3 sıra yan yana otobüs (tavanda koş), son sıradan önce 3 m atlama, son sıra kayık; boş şeritte zıpla-zıpla-kay."""
    a, c = L * mi, R * mi              # a: kamyon tarafı, c: yer tarafı
    _, by0, by1, _ = b.vehicle("CityBus", M, y, walk=False)
    mid = (by0 + by1) / 2
    b.jump("CrateStack", c, mid - 0.4, h=1.32)
    truck, ty0, ty1, _ = b.ramp(rng.choice(["RampTruck", "RampTruck", "MelonTruck"]), a, mid - 0.4)
    # kalas: kamyonun en yüksek tavanından ilk otobüs tavanına
    rows = rng.choice([2, 3, 3])
    yy = ty1 + ROW_GAP
    first_bus = None
    row_lanes = [(a, M)] * rows
    for r in range(rows):
        if r == rows - 1 and rows == 3:
            yy += ROOF_GAP - ROW_GAP
            row_lanes[r] = (a, c) if rng.random() < 0.6 else (a, M)
        y1s = []
        for ln in row_lanes[r]:
            o, b0, b1, top = b.vehicle("CityBus", ln, yy, tag=f"_S{r + 1}")
            if r == 0 and ln == a: first_bus = (b0, top)
            y1s.append(b1)
        if r == rows - 1 and row_lanes[r] == (a, c):
            b.jump("CrateStack", M, (yy + max(y1s)) / 2 - 0.4, h=1.32)
        yy = max(y1s) + ROW_GAP
    end = yy
    # kalas: kamyon kabin tavanı (en yüksek) → ilk otobüs tavanı (1.5 m içeri)
    tz = b.roof_at(a, ty1 - 2.0)
    best_y, best_z = ty0, 0
    yk = ty0
    while yk < ty1:
        z = b.roof_at(a, yk)
        if z > best_z: best_y, best_z = yk, z
        yk += 0.25
    b.plank(a, best_y, best_z, first_bus[0] + 1.5, b.roof_at(a, first_bus[0] + 1.5))
    # boş şerit (c): sandıktan sonra 8 m arayla zıpla, zıpla, kay
    yc = mid + 0.5 + GAP_JUMP
    b.jump(jm(rng), c, yc)
    b.slide(c, yc + GAP_JUMP + 0.8)
    return max(end, yc + GAP_JUMP + 2) + 6.0


def A_taksi_dizisi(b, rng, y, mi):
    """Üç şeritte kaydırmalı taksi/minibüs: birine zıplanır (taksi), öbürü yandan geçilir; arada sandık."""
    _, t0, t1, _ = b.vehicle("Taxi", L * mi, y)
    _, m0, m1, _ = b.vehicle("Minibus", R * mi, y + 4.0)
    b.jump(jm(rng), M, y + 1.0)
    _, n0, n1, _ = b.vehicle("Taxi", M, m1 + 2.0)
    b.slide(L * mi, m1 + 2.0)
    return n1 + 6.0


# ------------------------------------------------------------------ dalga: her 8–9 m'de iki şeritte engel, bir şerit açık
def waves(b, rng, y, n, dodgers=DODGERS, start_open=None):
    """n dalga. Her dalgada açık şerit değişebilir (koşucu sürekli hareket eder); iki şeritte zıpla/kay/kaç karışık,
    bir dalgada en fazla iki farklı hareket. Dönüş: son dalganın bitişi."""
    open_lane = start_open if start_open is not None else rng.choice([L, M, R])
    yy = y
    end = y
    for k in range(n):
        if k > 0 and rng.random() < 0.7:                      # açık şerit kayar (komşu şeride: 3 m'de geçilir)
            opts = [ln for ln in (L, M, R) if abs(ln - open_lane) == 1]
            open_lane = rng.choice(opts)
        kinds = rng.sample(["jump", "jump", "slide", "dodge"], 2)
        e = yy
        for ln, kd in zip([ln for ln in (L, M, R) if ln != open_lane], kinds):
            if kd == "jump": _, a0, a1 = b.jump(jm(rng), ln, yy)
            elif kd == "slide": _, a0, a1 = b.slide(ln, yy)
            else: _, a0, a1 = b.dodge(rng.choice(dodgers), ln, yy)
            e = max(e, a1)
        end = e
        yy = e + rng.uniform(6.5, 8.0)
    return end


# ------------------------------------------------------------------ B: araçsız, engelli
def B_dalga(b, rng, y, mi):
    return waves(b, rng, y, rng.choice([4, 5])) + 7.0


def B_zikzak(b, rng, y, mi):
    lanes = [L, M, R]
    rng.shuffle(lanes)
    for k, ln in enumerate(lanes):
        if k == 1 and rng.random() < 0.5: b.slide(ln, y + k * GAP_JUMP)
        else: b.jump(jm(rng), ln, y + k * GAP_JUMP)
    return waves(b, rng, y + 3 * GAP_JUMP, 2) + 7.0


def B_huni(b, rng, y, mi):
    """İki yanda tezgâh (orta açık) → ortada zıpla → iki yanda bariyer/zıpla."""
    d1, d2 = rng.sample(DODGERS, 2)
    _, a0, a1 = b.dodge(d1, L * mi, y)
    _, c0, c1 = b.dodge(d2, R * mi, y + 0.5)
    e = max(a1, c1)
    b.jump(jm(rng), M, e + 3.0)
    b.slide(L * mi, e + 3.0 + GAP_JUMP)
    b.jump(jm(rng), R * mi, e + 3.0 + GAP_JUMP)
    return e + 3.0 + GAP_JUMP + 7.0


def B_kay_zipla(b, rng, y, mi):
    b.slide(L * mi, y)
    b.jump(jm(rng), R * mi, y)
    _, d0, d1 = b.dodge(rng.choice(DODGERS), M, y + GAP_JUMP)
    b.slide(R * mi, d1 + 4.0)
    b.jump(jm(rng), L * mi, d1 + 4.0)
    return d1 + 4.0 + 7.0


def B_slalom(b, rng, y, mi):
    """Tezgâh slalomu: her 9 m'de bir başka şeritte tezgâh; aralara zıplama."""
    order = [L * mi, R * mi, M, L * mi]
    yy = y
    for k, ln in enumerate(order[: rng.choice([3, 4])]):
        _, d0, d1 = b.dodge(rng.choice(DODGERS), ln, yy)
        other = [x for x in (L, M, R) if x != ln]
        b.jump(jm(rng), rng.choice(other), yy + 0.3)        # yanında zıplanır engel, üçüncü şerit açık
        yy = d1 + 7.0
    return yy + 1.0


def B_cifte_zipla(b, rng, y, mi):
    """İki şeritte zıplama, üçüncüde kayma, kaydırmalı iki dalga."""
    b.jump(jm(rng), L * mi, y)
    b.jump(jm(rng), M, y)
    b.slide(R * mi, y + GAP_JUMP)
    b.jump(jm(rng), M, y + 2 * GAP_JUMP)
    b.slide(L * mi, y + 2 * GAP_JUMP)
    return waves(b, rng, y + 3 * GAP_JUMP, 1, start_open=R * mi) + 7.0


# ------------------------------------------------------------------ C: araç tavanında engel
def C_tavan_kosusu(b, rng, y, mi):
    """İki şeritte rampa + otobüs dizisi; tavanda sandık (zıpla) ve bariyer (kay); 3 m atlama; üçüncü şerit tezgâhlarla zor."""
    a, m, c = L * mi, M, R * mi
    _, t0, t1, _ = b.ramp("RampTruck", a, y)
    _, u0, u1, _ = b.ramp("MelonTruck", m, y + 1.0)
    ya, ym = t1 + ROW_GAP, u1 + ROW_GAP
    buses = rng.choice([3, 4])
    roof_obs = []
    for k in range(buses):
        if k == 2:
            ya += ROOF_GAP - ROW_GAP
        _, a0, a1, top = b.vehicle("CityBus", a, ya, tag=f"_T{k}")
        _, m0, m1, mtop = b.vehicle("Minibus" if k % 2 else "CityBus", m, ym, tag=f"_T{k}")
        if k == 1: roof_obs.append(("jump", a, a0 + 3.5, top))
        if k == 1: roof_obs.append(("slide", m, m0 + 2.5, mtop))
        ya, ym = a1 + ROW_GAP, m1 + ROW_GAP
    # kalaslar: kamyon kabininden otobüse
    for ln, (s0, s1) in ((a, (t0, t1)), (m, (u0, u1))):
        bz, by = 0, s0
        yk = s0
        while yk < s1:
            z = b.roof_at(ln, yk)
            if z > bz: bz, by = z, yk
            yk += 0.25
        yb = s1 + ROW_GAP + 1.5
        b.plank(ln, by, bz, yb, b.roof_at(ln, yb))
    for kind, ln, yy, top in roof_obs:
        if kind == "jump": b.jump("CrateStack", ln, yy, z=b.roof_at(ln, yy), h=1.1)
        else: b.slide(ln, yy, z=b.roof_at(ln, yy))
    # üçüncü şerit: yerden geçilir ama yoğun (tezgâh + zıpla)
    yc = y + 4
    while yc < max(ya, ym) - 8:
        if rng.random() < 0.5: b.jump(jm(rng), c, yc); yc += GAP_JUMP + 1
        else:
            _, d0, d1 = b.dodge(rng.choice(DODGERS[:4]), c, yc)
            yc = d1 + 10
    return max(ya, ym) + 6.0


def C_cift_tren_orta_yer(b, rng, y, mi):
    """İki yanda tren (rampa + 2 otobüs), ortada yer şeridi zıpla/kay; tavanlar arası orta boşluktan atlanmaz (yer)."""
    end = y
    for side in (L, R):
        _, t0, t1, _ = b.ramp("RampTruck", side, y + (0 if side == L * mi else 6))
        yy = t1 + ROW_GAP
        bz, by = 0, t0
        yk = t0
        while yk < t1:
            z = b.roof_at(side, yk)
            if z > bz: bz, by = z, yk
            yk += 0.25
        for k in range(2):
            _, b0, b1, _ = b.vehicle("CityBus", side, yy, tag=f"_C{k}")
            if k == 0: b.plank(side, by, bz, b0 + 1.5, b.roof_at(side, b0 + 1.5))
            yy = b1 + ROW_GAP
        end = max(end, yy)
    b.jump(jm(rng), M, y + 10)
    b.slide(M, y + 10 + GAP_JUMP + 1)
    b.jump(jm(rng), M, y + 10 + 2 * GAP_JUMP + 2)
    return end + 6.0


# ------------------------------------------------------------------ D: portal
def D_tavan_portal(b, rng, y, mi):
    """Rampalı kamyon → kalas → 2 otobüs; portal ikinci otobüsün tavanında (sonuna yakın). Diğer şeritler yerden devam."""
    a = rng.choice([L, R]) * mi
    _, t0, t1, _ = b.ramp("RampTruck", a, y)
    bz, by = 0, t0
    yk = t0
    while yk < t1:
        z = b.roof_at(a, yk)
        if z > bz: bz, by = z, yk
        yk += 0.25
    _, b0, b1, _ = b.vehicle("CityBus", a, t1 + ROW_GAP, tag="_P1")
    b.plank(a, by, bz, b0 + 1.5, b.roof_at(a, b0 + 1.5))
    _, c0, c1, _ = b.vehicle("CityBus", a, b1 + ROW_GAP, tag="_P2")
    py = c1 - 2.5
    b.portal(a, py, b.roof_at(a, py))
    other = [ln for ln in (L, M, R) if ln != a]
    b.jump(jm(rng), other[0], y + 6)
    b.slide(other[1], y + 6 + GAP_JUMP)
    b.dodge(rng.choice(DODGERS[:3]), other[0], y + 6 + 2 * GAP_JUMP + 1)
    return c1 + 6.0


def D_yer_portal(b, rng, y, mi):
    """Huni: iki yanda tezgâh/minibüs orta şeride toplar, portal ortada; sonrası boş (girmeyen devam eder)."""
    _, a0, a1, _ = b.vehicle("Minibus", L * mi, y)
    _, c0, c1 = b.dodge(rng.choice(DODGERS), R * mi, y + 1)
    b.jump(jm(rng), M, y)
    py = max(a1, c1) + 6
    b.portal(M, py, 0.0)
    return py + 8.0


# ------------------------------------------------------------------ E: sur üstü (altta deniz) — araçsız, ferah ama akıcı
SUR_DODGE = ["TeaCart", "SimitCart", "MelonStall"]


def E_sur_dalga(b, rng, y, mi):
    return waves(b, rng, y, rng.choice([4, 5]), SUR_DODGE) + 7.0


def E_sur_huni(b, rng, y, mi):
    _, a0, a1 = b.dodge(rng.choice(["TeaCart", "SimitCart"]), L * mi, y)
    b.slide(M, y + 1)
    _, c0, c1 = b.dodge(rng.choice(["MelonStall", "TeaCart"]), R * mi, y + GAP_JUMP + 2)
    b.jump(jm(rng), M, c0)
    return waves(b, rng, c1 + 7.0, 2, SUR_DODGE) + 7.0


# ------------------------------------------------------------------ F: geçiş (kısa, ferah)
def F_gecis(b, rng, y, mi):
    """Kısa geçiş (kemer + kuleler): bir hafif dalga ya da tek engel — nefes ama boş değil."""
    if rng.random() < 0.5: return waves(b, rng, y + 5, 1, SUR_DODGE) + 6.0
    b.jump(jm(rng), rng.choice([L, R]), y + 7)
    return y + 20.0


SECTIONS = {f.__name__: f for f in [A_park_duvari, A_otobus_tren, A_taksi_dizisi, B_dalga, B_zikzak, B_huni, B_kay_zipla, B_slalom,
                                       B_cifte_zipla, C_tavan_kosusu, C_cift_tren_orta_yer, D_tavan_portal, D_yer_portal,
                                       E_sur_dalga, E_sur_huni, F_gecis]}
THEME = {"A": "mahalle", "B": "mahalle", "C": "mahalle", "D": "mahalle", "E": "sur", "F": "gecis"}

# ------------------------------------------------------------------ 54 harita: blok sıraları (tür dengesi: referans tarzı ağırlıklı)
PLANS = [
    # 1–20: mahalle — referans tarzı ve karışımları
    ["A_park_duvari", "A_otobus_tren", "B_zikzak"],
    ["A_park_duvari", "A_otobus_tren"],
    ["B_huni", "A_park_duvari", "A_otobus_tren"],
    ["A_taksi_dizisi", "A_otobus_tren", "B_kay_zipla"],
    ["A_park_duvari", "B_cifte_zipla", "C_tavan_kosusu"],
    ["B_slalom", "A_otobus_tren", "D_yer_portal"],
    ["A_park_duvari", "C_cift_tren_orta_yer", "B_zikzak"],
    ["A_taksi_dizisi", "B_huni", "A_otobus_tren"],
    ["C_tavan_kosusu", "B_kay_zipla", "A_park_duvari"],
    ["A_park_duvari", "A_taksi_dizisi", "D_tavan_portal"],
    ["B_zikzak", "A_otobus_tren", "B_cifte_zipla"],
    ["A_otobus_tren", "B_slalom", "A_park_duvari"],
    ["A_park_duvari", "B_huni", "C_cift_tren_orta_yer"],
    ["C_tavan_kosusu", "A_taksi_dizisi", "B_zikzak"],
    ["B_kay_zipla", "A_park_duvari", "A_otobus_tren"],
    ["A_park_duvari", "C_tavan_kosusu", "D_yer_portal"],
    ["A_taksi_dizisi", "B_cifte_zipla", "A_otobus_tren"],
    ["B_huni", "C_cift_tren_orta_yer", "A_park_duvari"],
    ["A_otobus_tren", "D_tavan_portal"],
    ["B_slalom", "A_park_duvari", "C_tavan_kosusu"],
    # 21–30: araçsız engel ağırlıklı
    ["B_zikzak", "B_huni", "B_kay_zipla", "B_cifte_zipla"],
    ["B_slalom", "B_dalga", "B_zikzak", "B_huni"],
    ["B_cifte_zipla", "B_kay_zipla", "B_slalom", "A_park_duvari"],
    ["B_huni", "B_slalom", "B_zikzak", "D_yer_portal"],
    ["B_kay_zipla", "B_cifte_zipla", "B_huni", "B_zikzak"],
    ["A_taksi_dizisi", "B_slalom", "B_cifte_zipla"],
    ["B_dalga", "B_zikzak", "B_kay_zipla", "A_park_duvari"],
    ["B_huni", "B_cifte_zipla", "B_slalom", "B_kay_zipla"],
    ["B_slalom", "A_taksi_dizisi", "B_zikzak", "B_huni"],
    ["B_cifte_zipla", "B_dalga", "B_zikzak", "B_slalom"],
    # 31–40: araç tavanı ağırlıklı
    ["C_tavan_kosusu", "C_cift_tren_orta_yer"],
    ["A_park_duvari", "C_tavan_kosusu", "A_otobus_tren"],
    ["C_cift_tren_orta_yer", "A_otobus_tren"],
    ["C_tavan_kosusu", "D_tavan_portal"],
    ["A_otobus_tren", "C_tavan_kosusu"],
    ["C_cift_tren_orta_yer", "B_kay_zipla", "C_tavan_kosusu"],
    ["A_taksi_dizisi", "C_tavan_kosusu", "B_huni"],
    ["C_tavan_kosusu", "A_park_duvari", "C_cift_tren_orta_yer"],
    ["A_otobus_tren", "C_cift_tren_orta_yer", "D_tavan_portal"],
    ["C_cift_tren_orta_yer", "A_taksi_dizisi", "A_otobus_tren"],
    # 41–48: sur üstü (deniz) + geçiş
    ["F_gecis", "E_sur_dalga", "E_sur_huni", "E_sur_dalga", "F_gecis"],
    ["F_gecis", "E_sur_huni", "E_sur_dalga", "E_sur_huni", "F_gecis"],
    ["F_gecis", "E_sur_dalga", "E_sur_dalga", "F_gecis"],
    ["F_gecis", "E_sur_huni", "E_sur_dalga", "E_sur_huni", "E_sur_dalga", "F_gecis"],
    ["F_gecis", "E_sur_dalga", "E_sur_huni", "F_gecis"],
    ["F_gecis", "E_sur_huni", "E_sur_huni", "E_sur_dalga", "F_gecis"],
    ["F_gecis", "E_sur_dalga", "E_sur_huni", "E_sur_dalga", "E_sur_huni", "F_gecis"],
    ["F_gecis", "E_sur_dalga", "E_sur_dalga", "E_sur_huni", "F_gecis"],
    # 49–54: tema değişen (mahalle → geçiş → sur, ya da tersi)
    ["A_park_duvari", "A_otobus_tren", "F_gecis", "E_sur_dalga", "E_sur_huni"],
    ["E_sur_dalga", "E_sur_huni", "F_gecis", "A_park_duvari", "B_zikzak"],
    ["B_huni", "C_tavan_kosusu", "F_gecis", "E_sur_huni", "E_sur_dalga"],
    ["E_sur_huni", "F_gecis", "A_otobus_tren", "D_yer_portal"],
    ["A_taksi_dizisi", "B_kay_zipla", "F_gecis", "E_sur_dalga", "E_sur_dalga"],
    ["E_sur_dalga", "E_sur_huni", "F_gecis", "C_cift_tren_orta_yer", "A_park_duvari"],
]


def build(lib, idx, plan, seed):
    rng = random.Random(seed)
    name = f"Harita_{idx:02d}"
    b = K.MapBuilder(lib, idx, name, plan)
    y = 8.0                                    # girişte 8 m boş
    regions = []
    for sec in plan:
        mi = rng.choice([1, -1])
        y0 = y
        y = SECTIONS[sec](b, rng, y, mi)
        b.sections.append({"type": sec, "y0": round(y0, 2), "y1": round(y, 2), "mirror": mi})
        th = THEME[sec[0]]
        if regions and regions[-1][0] == th: regions[-1][2] = y
        else: regions.append([th, y0 if regions else 0.0, y])
    length = y + 4.0
    regions[-1][2] = length
    ok, portal_ok, per_lane = b.solve(length)
    return b, ok and portal_ok, per_lane, portal_ok, length, regions, rng


def dress(b, regions, rng):
    for th, y0, y1 in regions:
        b.ground(y0 - (6 if y0 == 0 else 0), y1, th)
        if th == "mahalle": b.dress_mahalle(rng, y0, y1, mosque=rng.random() < 0.55)
        elif th == "sur": b.dress_sur(rng, y0, y1)
        else: b.dress_gecis(rng, y0, y1)


def preview(b, length):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    if sc.world is None: sc.world = bpy.data.worlds.new("Gok")
    sc.world.color = (0.62, 0.74, 0.86)
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    for c in bpy.context.scene.collection.children:
        c.hide_render = c.name not in (b.name,)
    cam = bpy.data.objects.get("OnizlemeKamera") or bpy.data.objects.new("OnizlemeKamera", bpy.data.cameras.new("OnizlemeKamera"))
    if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
    sc.camera = cam
    os.makedirs(K.PREV, exist_ok=True)
    cam.data.type = "PERSP"
    cam.data.lens = 26
    cam.data.clip_end = 400
    for tag, y in (("a", -7.0), ("b", length * 0.45)):
        cam.location = (b.ox, y, 5.2)
        cam.rotation_euler = (math.radians(74), 0, 0)
        sc.render.resolution_x, sc.render.resolution_y = 540, 720
        sc.render.filepath = os.path.join(K.PREV, f"{b.name}_{tag}.png")
        bpy.ops.render.render(write_still=True)
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = length + 6
    cam.location = (b.ox, length / 2, 120)
    cam.rotation_euler = (0, 0, math.radians(90))
    sc.render.resolution_x, sc.render.resolution_y = 1400, 180
    sc.render.filepath = os.path.join(K.PREV, f"{b.name}_ust.png")
    bpy.ops.render.render(write_still=True)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    count = int(args[0]) if args else len(PLANS)
    first = int(args[1]) if len(args) > 1 else 1
    bpy.ops.wm.read_factory_settings(use_empty=True)
    t0 = time.time()
    lib = K.Lib()
    print("KUTUPHANE", round(time.time() - t0, 1), "sn")
    report = []
    for idx in range(first, first + count):
        plan = PLANS[(idx - 1) % len(PLANS)]
        done = None
        for attempt in range(14):
            seed = idx * 1009 + attempt * 7919
            b, ok, per_lane, portal_ok, length, regions, rng = build(lib, idx, plan, seed)
            if ok:
                done = (b, length, regions, rng, attempt)
                break
            print("  YENIDEN", b.name, "deneme", attempt, "seritler", per_lane, "portal", portal_ok)
            for o in list(b.coll.objects): bpy.data.objects.remove(o, do_unlink=True)
            bpy.data.collections.remove(b.coll)
        if not done:
            report.append(f"Harita_{idx:02d} GECILEMEDI")
            print("HATA Harita", idx, "geçilebilir varyasyon bulunamadı")
            continue
        b, length, regions, rng, attempt = done
        dress(b, regions, rng)
        b.save_json(length, [r[0] for r in regions], regions)
        if os.environ.get("ONIZLEME", "1") == "1": preview(b, length)
        n_obs = sum(1 for it in b.items if it.get("kind") not in ("plank",))
        line = f"{b.name} uzunluk {length:.0f} m · bloklar {plan} · engel/araç {n_obs} · portal {len(b.portals)} · deneme {attempt}"
        report.append(line)
        print("HARITA", line, round(time.time() - t0, 1), "sn")
    with open(os.path.join(K.OUT_JSON, "_rapor.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(report))
    for c in bpy.context.scene.collection.children: c.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=K.OUT_BLEND)
    print("BITTI", len(report), round(time.time() - t0, 1), "sn")


main()
