"""Onaylanan parkur önizlemesinden (claude.ai/artifact/SrMVSvKMysrSdDjrvqA3RM) Unity bölüm dosyası üretir.
Çıktı: Assets/Data/Course_SurIci.json — ObstacleSpawner (sokak) sırayla okur, sonra karıştırıp aynalar.
Kural: araç trenlerinde bile yerde her zaman en az bir açık şerit kalır (tavandan düşen sıkışmasın).
Satır: z = bölüm başından metre (engelin koşucuya bakan ön kenarı), lane = -1 sol / 0 orta / 1 sağ, id = engel kümesi."""
import json
import os

OUT = r"D:\Repolar\junpline\Assets\Data\Course_SurIci.json"

SECTIONS = [
    ("Dağkapı Girişi", 0, 130, [[10,0,"gate"],[30,0,"low"],[48,0,"bar"],[66,-1,"wall"],[66,1,"wall"],
        [84,-1,"low"],[84,0,"low"],[84,1,"low"],[104,0,"wall"],[104,-1,"low"],[104,1,"bar"]]),
    ("Gazi Caddesi Çarşısı", 130, 270, [[140,-1,"teacart"],[140,1,"simitcart"],[152,0,"kilim"],[168,-1,"patpat"],[168,0,"melonstall"],
        [184,1,"crates"],[184,0,"kilim"],[200,-1,"stall"],[206,0,"bar"],[212,1,"low"],
        [226,0,"stall"],[226,-1,"kilim"],[226,1,"kilim"],[244,-1,"low"],[244,0,"low"],[244,1,"low"],
        [258,-1,"stall"],[258,0,"stall"],[258,1,"patpat"]]),
    ("Dolmuş Durağı", 270, 410, [[280,-1,"bus"],[280,0,"bus"],[289,1,"taxi"],[298,1,"bus"],
        [318,-1,"busstop"],[316,0,"taxi"],[316,1,"taxi"],[334,0,"bus"],[334,-1,"bar"],[334,1,"low"],
        [352,-1,"bus"],[352,1,"bus"],[354,0,"taxi"],[374,-1,"bar"],[374,1,"low"],
        [392,-1,"taxi"],[392,0,"taxi"],[390,1,"bus"]]),
    ("Karpuz Kamyonu Treni", 410, 580, [[420,-1,"melon"],[421,0,"bus"],[426.9,0,"bus"],[421,1,"bus"],[426.9,1,"bus"],
        [434.5,-1,"citybus"],[438,0,"low"],[444,1,"bar"],[446,-1,"gate"],
        [446.5,-1,"bus"],[464,1,"low"],[464,0,"low"],
        [482,0,"tractor"],[482,-1,"citybus"],[482,1,"citybus"],[494,0,"bus"],
        [522,-1,"patpat"],[522,0,"low"],[522,1,"bar"],
        [540,1,"melon"],[540,-1,"citybus"],[540,0,"citybus"],[554.5,1,"bus"],[556,0,"gate"],[566,-1,"low"],[566,0,"low"]]),
    ("Hasan Paşa Hanı Önü", 580, 710, [[590,-1,"arch"],[590,0,"arch"],[590,1,"wall"],[606,-1,"teatable"],[606,0,"teatable"],[606,1,"arch"],
        [622,-1,"arch"],[622,0,"wall"],[622,1,"arch"],[640,-1,"teatable"],[646,0,"low"],[652,1,"teatable"],
        [668,-1,"arch"],[668,0,"arch"],[668,1,"arch"],[686,-1,"low"],[686,0,"bar"],[686,1,"low"],
        [702,-1,"wall"],[702,0,"wall"]]),
    ("Bakırcılar Çarşısı", 710, 830, [[720,-1,"copper"],[720,0,"copper"],[720,1,"potrack"],[736,0,"grill"],[736,-1,"potrack"],[736,1,"copper"],
        [752,-1,"copper"],[752,0,"low"],[752,1,"copper"],[770,-1,"potrack"],[770,0,"potrack"],[770,1,"grill"],
        [790,0,"taxi"],[790,-1,"copper"],[790,1,"bar"],[808,-1,"low"],[808,0,"grill"],[808,1,"copper"]]),
    ("Belediye Otobüsü Kavşağı", 830, 970, [[840,-1,"citybus"],[840,1,"citybus"],[844,0,"bar"],[862,-1,"low"],[862,0,"patpat"],
        [878,0,"truck"],[878,-1,"citybus"],[878,1,"citybus"],[893,-1,"low"],[893,1,"bar"],
        [892,0,"citybus"],[898,0,"gate"],[903.5,0,"bus"],
        [922,-1,"patpat"],[922,1,"patpat"],[922,0,"low"],[940,-1,"taxi"],[940,0,"bus"],[940,1,"taxi"],[958,-1,"low"],[958,0,"low"],[958,1,"low"]]),
    ("Portal Meydanı", 970, 1100, [[996,-1,"low"],[996,1,"low"],[1018,-1,"wall"],[1018,1,"wall"],[1036,0,"portal"]]),
]


def main():
    data = {"sections": []}
    for name, z0, z1, rows in SECTIONS:
        data["sections"].append({
            "name": name,
            "length": z1 - z0,
            "rows": [{"z": round(z - z0, 2), "lane": lane, "id": i} for z, lane, i in sorted(rows, key=lambda r: r[0])],
        })
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as fh:
        json.dump(data, fh, ensure_ascii=False, indent=1)
    print("COURSE_OK", sum(len(s["rows"]) for s in data["sections"]), "rows")


main()
