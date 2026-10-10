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

        private static void BuildMap(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "LEGENDS MAP"), MenuLink.LinkAction.Back);
            const float bottomY = 1400f, topY = 380f;
            float step = (bottomY - topY) / (MapStops.Length - 1);
            var nodes = new System.Collections.Generic.List<(int level, Button b, Image[] stars, GameObject lockIcon, GameObject portrait, TMP_Text status)>();
            Vector2 prev = Vector2.zero;
            for (int i = 0; i < MapStops.Length; i++)
            {
                var st = MapStops[i];
                bool left = i % 2 == 0;
                float cx = left ? 236f : 880f;
                float cy = bottomY - i * step;
                var center = new Vector2(cx, cy);
                if (i > 0)   // noktalı patika (önce çizilir, düğümlerin altında kalır)
                {
                    for (int d = 1; d < 9; d++)
                    {
                        Vector2 p = Vector2.Lerp(prev, center, d / 9f) + new Vector2(0f, Mathf.Sin(d / 9f * Mathf.PI) * 18f);
                        var dot = Add(s.top, $"Path{i}_{d}", new Rect(p.x - 7f, p.y - 7f, 14f, 14f));
                        var di = dot.go.AddComponent<Image>();
                        di.sprite = circle;
                        di.color = Hex("#E8B95B", 0.55f);
                        di.raycastTarget = false;
                    }
                }
                prev = center;
                var r = new Rect(cx - 78f, cy - 78f, 156f, 156f);
                var ring = Panel(s.top, $"Stop{i + 1}", r, Hex("#7A3B1C"), Hex("#E39A55"), 78f, Hex("#FFE0A8"));
                Panel(ring, "Face", new Rect(r.x + 10f, r.y + 10f, 136f, 136f), Hex("#2A1420"), Hex("#4A2236"), 68f, Hex("#00000000"));
                GameObject portraitGo = null;
                if (st.portrait != null && AssetImporter.GetAtPath(st.portrait) is TextureImporter ti)
                {
                    ti.textureType = TextureImporterType.Sprite;
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                    var pb = Add(ring, "Portrait", new Rect(r.x + 14f, r.y + 8f, 128f, 128f));
                    var pi = pb.go.AddComponent<Image>();
                    pi.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(st.portrait);
                    pi.preserveAspect = true;
                    pi.raycastTarget = false;
                    portraitGo = pb.go;
                }
                var lockIcon = Icon(ring, "lock", new Rect(r.x + 38f, r.y + 36f, 80f, 80f), Color.white);
                Txt(ring, "Number", (i + 1).ToString(), new Rect(r.x + 106f, r.y - 4f, 56f, 56f), fExtra, 34f, Hex("#FFE08A"), localize: false);
                var img = ring.go.GetComponent<Image>();
                img.raycastTarget = true;
                var button = MakeButton(ring, img);
                // yazılar düğümün altında (patika ortadan akar, yazıya değmez)
                float tx = cx - 160f;
                var align = TextAlignmentOptions.Center;
                Txt(s.top, $"City{i + 1}", st.city, new Rect(tx, cy + 124f, 320f, 44f), fExtra, 34f, Color.white, align);
                Txt(s.top, $"Legend{i + 1}", st.legend, new Rect(tx, cy + 164f, 320f, 34f), fBold, 24f, Hex("#E8B95B"), align);
                var status = Txt(s.top, $"Status{i + 1}", "", new Rect(cx + 70f, cy - 92f, 180f, 30f), fBold, 22f, Hex("#B5B9C6"),
                    left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, localize: false);
                if (!left) status.rectTransform.anchoredPosition += new Vector2(-320f, 0f);
                var stars = new Image[3];
                for (int k = 0; k < 3; k++)
                    stars[k] = Icon(s.top, "star", new Rect(cx - 66f + k * 44f, cy + 80f, 44f, 44f), Color.white);
                nodes.Add((i + 1, button, stars, lockIcon.gameObject, portraitGo, status));
            }
            Txt(s.top, "StarRule", "3 STARS: WIN WITHOUT CONTINUE, IN 6 THROWS OR LESS", new Rect(40f, 300f, 1036f, 36f), fBold, 22f, Hex("#8C95AA"));

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
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
