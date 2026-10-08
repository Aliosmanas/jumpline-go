"""Parkur geçilebilirlik kontrolü (Assets/Data/Course_SurIci.json) — "yerde sıkışma" tuzaklarını bulur.

Her 0.5 m'de oyuncunun YERDE bulunabileceği şeritler (R) izlenir:
  • önceki adımda yerde olduğu şerit açık kalıyorsa devam eder,
  • açık komşu şeride geçebilir (şerit değiştirme),
  • bir aracın tavanından/sonundan aşağı inebilir (araç biten şerit açılınca).
Yerden aşılamayan engel (dolmuş/otobüs gövdesi, araç kabini, duvar, tezgâh…) R'deki tüm şeritleri kapatıyor ve oyuncu
o şeritte bir rampaya çıkmıyorsa → TUZAK (yerde kalan oyuncunun hiç çıkışı yok).
Rampa bölgesi yerden çıkılabilir (oyuncu aracın üstüne geçer) ve tuzak sayılmaz."""
import json
import sys

COURSE = r"D:\Repolar\junpline\Assets\Data\Course_SurIci.json"
# id: (uzunluk m, yerden aşılamaz mı, rampa uzunluğu)
SPEC = {
    "low": (0.9, False, 0), "crates": (0.9, False, 0), "copper": (0.9, False, 0), "teatable": (0.8, False, 0),
    "bar": (0.6, False, 0), "kilim": (0.6, False, 0), "potrack": (0.6, False, 0), "arch": (0.9, False, 0),
    "patpat": (2.7, False, 0), "taxi": (4.5, False, 0), "gate": (1.6, False, 0),
    "wall": (1.4, True, 0), "busstop": (3.1, True, 0), "grill": (1.2, True, 0), "stall": (1.5, True, 0),
    "teacart": (1.5, True, 0), "simitcart": (1.4, True, 0), "melonstall": (1.5, True, 0),
    "bus": (5.9, True, 0), "citybus": (9.2, True, 0), "truck": (11.6, True, 4), "melon": (12.0, True, 4),
    "tractor": (9.5, True, 3),
}
STEP = 0.5


def main():
    data = json.load(open(COURSE, encoding="utf-8"))
    traps = 0
    for sec in data["sections"]:
        rows = [r for r in sec["rows"] if r["id"] in SPEC]

        def blocked(lane, z):
            for r in rows:
                length, solid, ramp = SPEC[r["id"]]
                if r["lane"] == lane and solid and r["z"] + ramp <= z <= r["z"] + length:
                    return True
            return False

        def on_ramp(lane, z):
            for r in rows:
                length, solid, ramp = SPEC[r["id"]]
                if r["lane"] == lane and ramp > 0 and r["z"] <= z < r["z"] + ramp:
                    return True
            return False

        reach = {-1, 0, 1}
        z = 0.0
        reported = False
        while z <= sec["length"]:
            nxt = {l for l in reach if not blocked(l, z)}
            nxt |= {l for l in (-1, 0, 1) if not blocked(l, z) and blocked(l, z - STEP)}   # araç bitti: aşağı inilir
            changed = True
            while changed:                                                             # şerit değiştirme
                changed = False
                for l in (-1, 0, 1):
                    if l not in nxt and not blocked(l, z) and ((l - 1) in nxt or (l + 1) in nxt):
                        nxt.add(l)
                        changed = True
            stuck = [l for l in reach if l not in nxt and not on_ramp(l, z - STEP)]
            if not nxt and stuck:
                if not reported:
                    print(f"TUZAK  {sec['name']}  z={z:.1f} m (bölüm içi): yerdeki oyuncunun çıkışı yok, şerit {stuck}")
                    traps += 1
                reported = True
            else:
                reported = False
            reach = nxt
            z += STEP
    traps += blind_landings(data)
    print("KONTROL", "TEMIZ" if traps == 0 else f"{traps} sorun")
    sys.exit(1 if traps else 0)


VEHICLES = {"bus", "citybus", "taxi", "truck", "melon", "tractor", "patpat"}
LANDING_CLEAR = 9.0


def blind_landings(data):
    """İniş bölgesi: aracın tavanından inen oyuncu önünü göremez. Araç bittikten sonraki LANDING_CLEAR metrede,
    aynı ve komşu şeritlerde araç olmayan engel olmamalı (tren devamı olan araçlar serbest)."""
    bad = 0
    for sec in data["sections"]:
        rows = [r for r in sec["rows"] if r["id"] in SPEC]
        for v in rows:
            if v["id"] not in VEHICLES:
                continue
            end = v["z"] + SPEC[v["id"]][0]
            for r in rows:
                if r["id"] in VEHICLES or r["id"] == "gate":
                    continue
                if abs(r["lane"] - v["lane"]) <= 1 and end < r["z"] <= end + LANDING_CLEAR:
                    print(f"KÖR İNİŞ  {sec['name']}  {r['id']} z={r['z']} şerit {r['lane']}: "
                          f"{v['id']} (şerit {v['lane']}) bitişinden {r['z'] - end:.1f} m sonra")
                    bad += 1
    return bad


main()
