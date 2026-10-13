using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Efsaneler haritası (aşama 10): OYNA buraya açılır. Bölüm 1 altta, yol yukarı zikzakla çıkar; duraklar arasında
    /// noktalı patika. Durak: bakır çerçeveli yuvarlak (açıksa canavar portresi, değilse kilit), şehir + efsane adı, 3 yıldız.
    /// Görsel dil şimdilik menünün diğer ekranlarıyla aynı; arayüz yenilemesinde (referanslarla) birlikte güncellenecek.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static readonly (string city, string legend, string portrait)[] MapStops =
        {
            ("DIYARBAKIR", "TEPEGOZ", "Assets/Art/UI/Portraits/tepegoz.png"),
            ("MARDIN", "SHAHMARAN", "Assets/Art/UI/Portraits/sahmeran.png"),
            ("HASANKEYF", "HUMBABA", "Assets/Art/UI/Portraits/humbaba.png"),
            ("SANLIURFA", "STONE GUARDIAN", "Assets/Art/UI/Portraits/stone_guardian.png"),
            ("VAN", "VAN LAKE MONSTER", "Assets/Art/UI/Portraits/van_monster.png"),
            ("KARS", "KARAKONCOLOS", "Assets/Art/UI/Portraits/karakoncolos.png"),
            ("???", "LAMASSU", "Assets/Art/UI/Portraits/lamassu.png"),
            ("???", "THE DRAGON", "Assets/Art/UI/Portraits/dragon.png"),
        };

        private const string MapArtPath = "Assets/Art/UI/Map/LegendsMap.png";
        /// <summary>Durak merkezleri (arayüz px) — Tools/make_map_art.py STOPS ile AYNI (yol ve oklar resimde çizili).</summary>
        private static readonly Vector2[] MapStopPos =
        {
            new(500f, 1250f), new(230f, 1450f), new(790f, 1420f), new(190f, 1070f),
            new(860f, 960f), new(800f, 640f), new(300f, 760f), new(540f, 455f),
        };

        private static Sprite ImportSpriteHQ(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 4096;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildMap(Screen s)
        {
            SolidBackground(s);
            // eski harita parşömeni (Tools/make_map_art.py): coğrafya, simge yapılar, fırça darbeli oklu yol
            var art = Add(s.top, "MapArt", Full);
            var artImg = art.go.AddComponent<Image>();
            artImg.sprite = ImportSpriteHQ(MapArtPath);
            artImg.raycastTarget = false;
            Link(Header(s.top, "LEGENDS MAP"), MenuLink.LinkAction.Back);
            var nodes = new System.Collections.Generic.List<(int level, Button b, Image[] stars, GameObject lockIcon, GameObject portrait, TMP_Text status, GameObject sparkles)>();
            Color ink = Hex("#5A3212");
            for (int i = 0; i < MapStops.Length; i++)
            {
                var st = MapStops[i];
                float cx = MapStopPos[i].x, cy = MapStopPos[i].y;
                var r = new Rect(cx - 88f, cy - 88f, 176f, 176f);
                var sparkles = AddSparkles(s.top, new Rect(cx - 100f, cy - 100f, 200f, 200f), 10, 40f).gameObject;   // sıradaki durak
                // süslü çerçeve: altın halka > turkuaz halka > yuvarlak portre penceresi
                var ring = Add(s.top, $"Stop{i + 1}", r);
                var ringImg = ring.go.AddComponent<Image>();
                ringImg.sprite = circle;
                ring.go.AddComponent<UIGradient>().SetColors(Hex("#A8701E"), Hex("#FFE38A"));
                var tealRing = Add(ring, "TealRing", new Rect(r.x + 9f, r.y + 9f, 158f, 158f));
                var tri = tealRing.go.AddComponent<Image>();
                tri.sprite = circle;
                tri.raycastTarget = false;
                tealRing.go.AddComponent<UIGradient>().SetColors(Hex("#0E4555"), Hex("#3FB8B0"));
                var window = Add(ring, "Window", new Rect(r.x + 17f, r.y + 17f, 142f, 142f));
                var wi = window.go.AddComponent<Image>();
                wi.sprite = circle;
                wi.raycastTarget = false;
                window.go.AddComponent<UIGradient>().SetColors(Hex("#F5E3BE"), Hex("#FFF6DE"));
                window.go.AddComponent<Mask>().showMaskGraphic = true;
                GameObject portraitGo = null;
                if (st.portrait != null && ImportSpriteHQ(st.portrait) is Sprite ps)
                {
                    var pb = Add(window, "Portrait", new Rect(r.x + 10f, r.y + 14f, 156f, 156f));
                    var pi = pb.go.AddComponent<Image>();
                    pi.sprite = ps;
                    pi.preserveAspect = true;
                    pi.raycastTarget = false;
                    portraitGo = pb.go;
                }
                var lockIcon = Icon(ring, "lock", new Rect(cx - 32f, cy - 32f, 64f, 64f), Color.white);
                Gem(ring, new Vector2(cx, cy - 90f), 14f);
                var badge = Add(ring, "Badge", new Rect(r.x - 6f, r.y - 2f, 50f, 50f));
                var bi = badge.go.AddComponent<Image>();
                bi.sprite = circle;
                bi.raycastTarget = false;
                badge.go.AddComponent<UIGradient>().SetColors(Hex("#8E1E1A"), Hex("#E2463A"));
                Txt(badge, "Number", (i + 1).ToString(), new Rect(r.x - 6f, r.y - 2f, 50f, 50f), fExtra, 28f, Color.white, material: OutlineText(), localize: false);
                ringImg.raycastTarget = true;
                var button = MakeButton(ring, ringImg);
                // altın kurdele (şehir adı) + kuyruklar
                var ribbonRect = new Rect(cx - 112f, cy + 70f, 224f, 50f);
                foreach (int side in new[] { -1, 1 })
                {
                    var tail = Poly(s.top, "RibbonTail", new Rect(side < 0 ? ribbonRect.x - 30f : ribbonRect.xMax - 8f, ribbonRect.y + 12f, 38f, 44f),
                        side < 0 ? new[] { new Vector2(1f, 0f), new Vector2(0f, 0.1f), new Vector2(0.35f, 0.5f), new Vector2(0f, 0.9f), new Vector2(1f, 1f) }
                                 : new[] { new Vector2(0f, 0f), new Vector2(1f, 0.1f), new Vector2(0.65f, 0.5f), new Vector2(1f, 0.9f), new Vector2(0f, 1f) },
                        Hex("#B07422"));
                    tail.raycastTarget = false;
                }
                themeBypass = true;
                var ribbon = Panel(s.top, $"Ribbon{i + 1}", ribbonRect, Hex("#C68A2A"), Hex("#FFDE7A"), 14f, Hex("#7A4A14"));
                themeBypass = false;
                Txt(ribbon, $"City{i + 1}", st.city, ribbonRect, fExtra, 28f, Color.white, material: OutlineText());
                var stars = new Image[3];
                for (int k = 0; k < 3; k++)
                    stars[k] = Icon(s.top, "star", new Rect(cx - 58f + k * 40f, cy + 38f, 40f, 40f), Color.white);
                Txt(s.top, $"Legend{i + 1}", st.legend, new Rect(cx - 160f, cy + 120f, 320f, 34f), fExtra, 23f, ink);
                var status = Txt(s.top, $"Status{i + 1}", "", new Rect(cx - 160f, cy + 148f, 320f, 28f), fBold, 19f, Hex("#8A5A2A"), localize: false);
                nodes.Add((i + 1, button, stars, lockIcon.gameObject, portraitGo, status, sparkles));
            }
            Txt(s.top, "StarRule", "3 STARS: WIN WITHOUT CONTINUE, IN 6 THROWS OR LESS", new Rect(40f, 300f, 1036f, 30f), fBold, 18f, Hex("#5A3212"));

            var view = s.screen.gameObject.AddComponent<MapView>();
            Set(view, "config", config);
            var so = new SerializedObject(view);
            var arr = so.FindProperty("nodes");
            arr.arraySize = nodes.Count;
            for (int i = 0; i < nodes.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("level").intValue = nodes[i].level;
                e.FindPropertyRelative("button").objectReferenceValue = nodes[i].b;
                var sa = e.FindPropertyRelative("stars");
                sa.arraySize = 3;
                for (int k = 0; k < 3; k++) sa.GetArrayElementAtIndex(k).objectReferenceValue = nodes[i].stars[k];
                e.FindPropertyRelative("lockIcon").objectReferenceValue = nodes[i].lockIcon;
                e.FindPropertyRelative("portrait").objectReferenceValue = nodes[i].portrait;
                e.FindPropertyRelative("status").objectReferenceValue = nodes[i].status;
                e.FindPropertyRelative("sparkles").objectReferenceValue = nodes[i].sparkles;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
