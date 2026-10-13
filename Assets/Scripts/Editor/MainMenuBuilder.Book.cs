using System.Collections.Generic;
using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Efsaneler Kitabı ekranı — docs/flow_ui/kitap_1.jpg (kart ızgarası) + canavar_detay_1.jpg (detay sayfası).
    /// Kart çerçevesi nadirliğe göre: bakır / turkuaz / mor / altın.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static readonly (string region, string story, string weakness, Vector3Int stats)[] BookData =
        {
            ("DIYARBAKIR", "A one-eyed giant from the Dede Korkut tales. He hurls whatever he grabs and never stops chasing.", "EYE", new Vector3Int(5, 2, 1)),
            ("MARDIN", "The wise serpent queen of the underground. She slips between lanes like water.", "LIGHT", new Vector3Int(3, 4, 5)),
            ("HASANKEYF", "Ancient guardian of the cedar forest. Throws often but turns slowly.", "SLOW TURNS", new Vector3Int(4, 2, 3)),
            ("SANLIURFA", "A stone guardian awakened at Gobeklitepe. Too heavy to slide, too tough to fall quickly.", "CAN'T SLIDE", new Vector3Int(5, 1, 2)),
            ("VAN", "The shadow beneath Lake Van. Slippery and hard to follow.", "SHALLOW WATER", new Vector3Int(4, 4, 3)),
            ("KARS", "A hairy winter demon of the snowy nights. Fast and full of tricks.", "DAWN", new Vector3Int(3, 5, 4)),
            ("???", "A winged bull guardian with a human face. It glides over the alleys.", "WINGS", new Vector3Int(4, 4, 4)),
            ("???", "The last legend that escaped the book. Fire, wings and fury.", "THE BOOK", new Vector3Int(5, 5, 5)),
        };

        private static readonly string[] FrameByRarity = { "frame_copper", "frame_turquoise", "frame_purple", "frame_gold" };

        /// <summary>Efsanenin karikatür portresi (Google Flow, Assets/Art/UI/LegendArt) — yoksa eski 3B portre.</summary>
        private static Sprite LegendArt(int i)
        {
            string file = System.IO.Path.GetFileName(MapStops[i].portrait);
            return ImportSpriteHQ("Assets/Art/UI/LegendArt/" + file) ?? ImportSpriteHQ(MapStops[i].portrait);
        }

        private static void BuildBook(Screen s)
        {
            SolidBackground(s);
            var art = Add(s.top, "BookArt", new Rect(0f, -92f, 1116f, 2092f));   // üst grup para çubuğu kadar kaymış
            var artImg = art.go.AddComponent<Image>();
            artImg.sprite = Skin["bg_book"];
            artImg.raycastTarget = false;

            // başlık kurdelesi + geri + ilerleme
            Chrome(RibbonLabel(s.top, "Title", new Rect(178f, 30f, 760f, 180f), "LEGENDS BOOK", 60f, true, fTitle));
            var back = Add(s.top, "BackButton", new Rect(30f, 50f, 120f, 120f));
            var bi = back.go.AddComponent<Image>();
            bi.sprite = Skin["i_back"];
            bi.preserveAspect = true;
            Link(MakeButton(back, bi), MenuLink.LinkAction.Back);
            themeBypass = true;
            var track = Panel(s.top, "ProgressTrack", new Rect(258f, 196f, 600f, 52f), Hex("#0B3A44"), Hex("#0F4C58"), 26f, Hex("#C99A3A"));
            themeBypass = false;
            var fill = Add(track, "Fill", new Rect(262f, 200f, 592f, 44f));
            var fi = fill.go.AddComponent<Image>();
            Rounded(fi, 22f);
            fi.raycastTarget = false;
            fill.go.AddComponent<UIGradient>().SetColors(Hex("#138C86"), Hex("#4FE3D6"));
            PivotLeft(fill);
            var progress = Txt(track, "Label", "0/8 LEGENDS", new Rect(258f, 196f, 600f, 52f), fExtra, 32f, Color.white, localize: false);

            // kart ızgarası (3 sütun)
            var cards = new List<Dictionary<string, object>>();
            var legends = new List<Dictionary<string, object>>();
            for (int i = 0; i < MapStops.Length; i++)
            {
                int row = i / 3, col = i % 3;
                float x = row < 2 ? 118f + col * 300f : 268f + col * 300f;
                float y = 300f + row * 410f;
                var r = new Rect(x, y, 280f, 372f);
                int rarity = Mathf.Min(i / 2, 3);
                var card = Add(s.top, $"Card{i + 1}", r);
                var window = Add(card, "Window", new Rect(x + 30f, y + 30f, 220f, 232f));
                var wi = window.go.AddComponent<Image>();
                Rounded(wi, 18f);
                wi.color = Hex("#D9C49A");   // parşömen: kilitliyken kara siluet seçilsin
                wi.raycastTarget = false;
                window.go.AddComponent<Mask>().showMaskGraphic = true;
                var ps = LegendArt(i);
                var portrait = Add(window, "Portrait", new Rect(x + 22f, y + 26f, 236f, 236f));
                var pi = portrait.go.AddComponent<Image>();
                pi.sprite = ps;
                pi.preserveAspect = true;
                pi.raycastTarget = false;
                var frame = card.go.AddComponent<Image>();
                frame.sprite = Skin[FrameByRarity[rarity]];
                frame.preserveAspect = true;
                frame.raycastTarget = true;
                SkinImage(card, "Frame", r, FrameByRarity[rarity]);   // görünen çerçeve portrenin üstünde; kökteki saydam yüzey dokunuşu alır
                frame.color = new Color(1f, 1f, 1f, 0f);
                var lockIcon = Icon(card, "lock", new Rect(x + 95f, y + 100f, 90f, 90f), Color.white);
                var name = Txt(card, "Name", MapStops[i].legend, new Rect(x + 26f, y + 282f, 228f, 60f), fExtra, 26f, Color.white, localize: false);
                Button b = MakeButton(card, frame);
                cards.Add(new Dictionary<string, object> { { "button", b }, { "portrait", pi }, { "name", name }, { "lockIcon", lockIcon.gameObject } });
                var d = BookData[i];
                legends.Add(new Dictionary<string, object>
                {
                    { "level", i + 1 }, { "nameKey", MapStops[i].legend }, { "regionKey", d.region }, { "storyKey", d.story },
                    { "weaknessKey", d.weakness }, { "rarity", rarity }, { "portrait", ps },
                });
            }
            Txt(s.top, "Hint", "Beat a legend to add it to the book!", new Rect(118f, 1508f, 880f, 56f), fExtra, 32f, Color.white);

            // ---- detay sayfası ----
            var detailRt = NewUI("Detail", s.screen.transform);
            Stretch(detailRt);
            var dim = Img(detailRt, "Dim", null, new Color(0.03f, 0.04f, 0.1f, 0.78f), false);
            Stretch(dim.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            dim.raycastTarget = true;
            Box dm = Group(detailRt, "Middle", 0.5f);
            SkinImage(dm, "Page", new Rect(50f, 150f, 1016f, 1640f), "parchment", true, 0.12f);
            SkinImage(dm, "NamePlaque", new Rect(208f, 110f, 700f, 150f), "plaque", false, 0.5f);
            var dName = Txt(dm, "Name", "", new Rect(330f, 128f, 456f, 110f), fTitle, 72f, Color.white, material: chromeMaterial, localize: false);
            Chrome(dName);
            var dRarity = RibbonLabel(dm, "Rarity", new Rect(328f, 246f, 460f, 120f), "", 34f, false);
            var dRegion = Txt(dm, "Region", "", new Rect(358f, 338f, 400f, 50f), fExtra, 34f, Hex("#7A3B1C"), localize: false);
            var closeBox = Add(dm, "Close", new Rect(930f, 120f, 116f, 116f));
            var ci = closeBox.go.AddComponent<Image>();
            ci.sprite = Skin["i_close"];
            ci.preserveAspect = true;
            Button close = MakeButton(closeBox, ci);

            var pwin = Add(dm, "PortraitWindow", new Rect(268f, 420f, 580f, 580f));
            var pwi = pwin.go.AddComponent<Image>();
            pwi.sprite = circle;
            pwi.color = Hex("#1A2E40");
            pwi.raycastTarget = false;
            pwin.go.AddComponent<Mask>().showMaskGraphic = true;
            var dp = Add(pwin, "Portrait", new Rect(258f, 420f, 600f, 600f));
            var dpi = dp.go.AddComponent<Image>();
            dpi.preserveAspect = true;
            dpi.raycastTarget = false;
            SkinImage(dm, "Ring", new Rect(228f, 380f, 660f, 660f), "stage_ring");
            var prev = Add(dm, "Prev", new Rect(90f, 650f, 120f, 120f));
            var pvi = prev.go.AddComponent<Image>();
            pvi.sprite = Skin["i_back"];
            pvi.preserveAspect = true;
            Button prevB = MakeButton(prev, pvi);
            var next = Add(dm, "Next", new Rect(906f, 650f, 120f, 120f));
            var nxHit = next.go.AddComponent<Image>();
            nxHit.color = new Color(1f, 1f, 1f, 0f);
            var nxArt = Img(next.rt, "Arrow", Skin["i_back"], Color.white, false);   // ok sağa: ayna çocukta (basma yayı kökün ölçeğini sıfırlar)
            nxArt.preserveAspect = true;
            nxArt.raycastTarget = false;
            Stretch(nxArt.rectTransform);
            nxArt.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            Button nextB = MakeButton(next, nxHit);

            string[] statKeys = { "POWER", "SPEED", "CUNNING" };
            var segs = new Object[15];
            for (int r2 = 0; r2 < 3; r2++)
            {
                float y = 1080f + r2 * 78f;
                Txt(dm, "Stat" + r2, statKeys[r2], new Rect(130f, y, 360f, 60f), fExtra, 38f, Color.white, TextAlignmentOptions.Left);
                for (int k = 0; k < 5; k++)
                {
                    var seg = Add(dm, $"Seg{r2}_{k}", new Rect(520f + k * 92f, y + 10f, 82f, 42f));
                    var si = seg.go.AddComponent<Image>();
                    Rounded(si, 10f);
                    si.raycastTarget = false;
                    segs[r2 * 5 + k] = si;
                }
            }
            Txt(dm, "StoryTitle", "STORY", new Rect(130f, 1320f, 400f, 56f), fExtra, 38f, Color.white, TextAlignmentOptions.Left);
            var dStory = Txt(dm, "Story", "", new Rect(130f, 1376f, 856f, 150f), fMedium, 32f, Hex("#3A2410"), TextAlignmentOptions.TopLeft, localize: false, wrap: true);
            dStory.fontSharedMaterial = fMedium.material;   // mürekkep rengi, kontursuz
            dStory.enableAutoSizing = true;
            dStory.fontSizeMin = 22f;
            Txt(dm, "WeaknessTitle", "WEAKNESS", new Rect(130f, 1530f, 380f, 56f), fExtra, 34f, Color.white, TextAlignmentOptions.Left);
            var dWeak = Txt(dm, "Weakness", "", new Rect(500f, 1530f, 300f, 56f), fExtra, 34f, Hex("#FFD23F"), TextAlignmentOptions.Left, localize: false);
            var dCaught = Txt(dm, "Caught", "", new Rect(760f, 1530f, 240f, 56f), fExtra, 30f, Color.white, TextAlignmentOptions.Right, localize: false);
            var seeMap = BoxButton(dm, "SeeOnMap", new Rect(258f, 1610f, 600f, 140f), "SEE ON MAP", 48f);
            seeMap.GetComponent<Image>().sprite = Skin["btn_green"];
            Link(seeMap, MenuLink.LinkAction.Open, "map");

            var view = s.screen.gameObject.AddComponent<BookView>();
            SetStructArray(view, "legends", legends);
            SetStructArray(view, "cards", cards);
            var so = new SerializedObject(view);
            var lp = so.FindProperty("legends");
            for (int i = 0; i < lp.arraySize; i++)
                lp.GetArrayElementAtIndex(i).FindPropertyRelative("stats").vector3IntValue = BookData[i].stats;
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(view, "progressLabel", progress);
            Set(view, "progressFill", fill.rt);
            SetFloat(view, "progressWidth", 592f);
            Set(view, "detail", detailRt.gameObject);
            Set(view, "detailName", dName);
            Set(view, "detailRarity", dRarity);
            Set(view, "detailRegion", dRegion);
            Set(view, "detailPortrait", dpi);
            Set(view, "detailStory", dStory);
            Set(view, "detailWeakness", dWeak);
            Set(view, "detailCaught", dCaught);
            SetArray(view, "statSegments", segs);
            Set(view, "closeButton", close);
            Set(view, "prevButton", prevB);
            Set(view, "nextButton", nextB);
            detailRt.gameObject.SetActive(false);
        }
    }
}
