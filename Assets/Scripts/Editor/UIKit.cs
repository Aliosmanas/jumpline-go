using System.Collections.Generic;
using System.IO;
using Jumpline.Core;
using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Sahne üreticilerinin ortak arayüz araçları. Konumlar referans piksel koordinatıdır
    /// (1116x2000, sol-üst köken). Kullanım: <c>using static Jumpline.EditorTools.UIKit;</c>
    /// </summary>
    public static class UIKit
    {
        public const string UiDir = "Assets/Art/UI";
        public const string SpriteDir = UiDir + "/Sprites";
        public const string FontDir = UiDir + "/Fonts";
        public const string IconDir = UiDir + "/Icons";
        public const string ReferenceDir = "docs/references/menu";

        public static readonly Vector2 RefSize = new Vector2(1116f, 2000f);
        public static readonly Rect Full = new Rect(0f, 0f, 1116f, 2000f);
        private const int SpriteSize = 128;
        private const float RoundedRadius = 40f;
        private const int RoundedBorder = 48;

        public static class C
        {
            public static readonly Color Bg = Hex("#0B2C3C");   // tema "Efsane": derin turkuaz-lacivert
            public static readonly Color NavyDark = Hex("#1B2236");
            public static readonly Color BorderDim = Hex("#55658C");
            public static readonly Color Blue = Hex("#5AA8FF");
            public static readonly Color BlueTop = Hex("#4A7BE0");
            public static readonly Color BlueBottom = Hex("#1B3487");
            public static readonly Color TextLight = Hex("#E1E4EA");
            public static readonly Color TextSoft = Hex("#D5D8E0");
            public static readonly Color TextGrey = Hex("#A9AEBD");
            public static readonly Color TextDim = Hex("#6F7280");
            public static readonly Color Gold = Hex("#E8B95B");
            public static readonly Color GemText = Hex("#A9A6F5");
            public static readonly Color Price = Hex("#5FB4F5");
        }

        public struct Box
        {
            public RectTransform rt;
            public Rect r;
            public Box(RectTransform rt, Rect r) { this.rt = rt; this.r = r; }
            public GameObject go => rt.gameObject;
        }

        public const string SkinDir = UiDir + "/Skin";
        /// <summary>Tema "Taş & Çini" parçaları (Google Flow, docs/flow_ui/parts → Assets/Art/UI/Skin).</summary>
        public static readonly Dictionary<string, Sprite> Skin = new Dictionary<string, Sprite>();
        public static Sprite rounded, outline, circle, glowRect, glowLine, knob, ringGlow, softCircle, background, circuit, arcRing;
        public static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        public static TMP_FontAsset fMedium, fBold, fExtra, fTitle;
        public static Material chromeMaterial;

        // =====================================================================
        // Sahne iskeleti
        // =====================================================================

        public static void BuildCameraAndEvents(bool uiOnly)
        {
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            if (uiOnly)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = C.Bg;
                cam.orthographic = true;
            }
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        public static Transform BuildCanvas(string name, bool withBackground)
        {
            var canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = RefSize;
            scaler.matchWidthOrHeight = 0f; // dikey telefonlarda genişlik sabit, fazla yükseklik boşluğa gider
            if (withBackground) Stretch(Img(canvasGo.transform, "Background", background, Color.white, false).rectTransform);
            return canvasGo.transform;
        }

        /// <summary>Referans boyutunda (1116x2000) bir çerçeve; ebeveynin üstüne / ortasına / altına yaslanır.</summary>
        public static Box Group(Transform parent, string name, float anchorY)
        {
            var rt = NewUI(name, parent);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, anchorY);
            rt.pivot = new Vector2(0.5f, anchorY);
            rt.sizeDelta = RefSize;
            rt.anchoredPosition = Vector2.zero;
            return new Box(rt, Full);
        }

        // =====================================================================
        // Temel çizim
        // =====================================================================

        public static Box Add(Box parent, string name, Rect r)
        {
            var rt = NewUI(name, parent.rt);
            Place(rt, parent, r);
            return new Box(rt, r);
        }

        /// <summary>Referans koordinatlı dikdörtgeni ebeveyn kutusuna göre yerleştirir.</summary>
        public static void Place(RectTransform rt, Box parent, Rect r)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = r.size;
            rt.anchoredPosition = new Vector2(r.center.x - parent.r.center.x, -(r.center.y - parent.r.center.y));
        }

        /// <summary>Sola yaslı (genişliği koddan değişen) çubuk dolgusu için pivotu sol kenara alır.</summary>
        public static void PivotLeft(Box b)
        {
            float w = b.rt.sizeDelta.x;
            b.rt.pivot = new Vector2(0f, 0.5f);
            b.rt.anchoredPosition -= new Vector2(w * 0.5f, 0f);
        }

        public static Box Panel(Box parent, string name, Rect r, Color bottom, Color top, float radius, Color border)
        {
            if (!themeBypass && Skin.Count > 0 && IsThemedDark(top))
            {
                var sb = Add(parent, name, r);
                var si = sb.go.AddComponent<Image>();
                bool small = Mathf.Min(r.width, r.height) < 96f;
                SkinSprite(si, small ? "pill" : "card_lime", r, small ? 0.45f : 0.22f);
                if (top.a < 0.99f) si.color = new Color(1f, 1f, 1f, Mathf.Max(top.a, 0.85f));
                return sb;
            }
            var b = Add(parent, name, r);
            var img = b.go.AddComponent<Image>();
            Rounded(img, radius);
            b.go.AddComponent<UIGradient>().SetColors(Theme(bottom), Theme(top, true));
            if (border.a > 0f) Border(b, radius, ThemeBorder(border));
            return b;
        }

        // ---------------------------------------------------------------------
        // Tema "Efsane" (kullanıcı referansları: çizgi film kiti + parlak mobil oyun + altın/turkuaz süslü bantlar).
        // Eski koyu lacivert/bordo yüzeyler canlı turkuaz-maviye, gri-mavi çerçeveler altına çevrilir; sıcak renkler
        // (bronz rozet, kırmızı sayaç, altın) olduğu gibi kalır. themeBypass: koyu kalması gereken (çubuk oluğu) yüzeyler.
        // ---------------------------------------------------------------------
        public static bool themeBypass;
        private static readonly Color ThemeDeep = Hex("#124C66"), ThemeLight = Hex("#2E93AE"), ThemeGold = Hex("#F2C24E");

        /// <summary>Tema çevirisine giren koyu (soğuk) yüzey mi? Sıcak renkler (bronz, kırmızı, altın) ve açık yüzeyler değil.</summary>
        public static bool IsThemedDark(Color c)
        {
            if (c.a <= 0.01f) return false;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            bool warm = (h < 0.14f || h > 0.97f) && sat > 0.45f && v > 0.4f;
            return !warm && v <= 0.45f;
        }

        public static Color Theme(Color c, bool top = false)
        {
            if (themeBypass || c.a <= 0.01f) return c;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            bool warm = (h < 0.14f || h > 0.97f) && sat > 0.45f && v > 0.4f;   // bronz/kırmızı/altın: dokunma
            if (warm || v > 0.45f) return c;
            float t = Mathf.Clamp01(v / 0.45f);
            Color m = Color.Lerp(ThemeDeep, ThemeLight, top ? Mathf.Lerp(0.55f, 1f, t) : Mathf.Lerp(0.15f, 0.6f, t));
            m.a = c.a;
            return m;
        }

        public static Color ThemeBorder(Color c)
        {
            if (themeBypass || c.a <= 0.01f) return c;
            Color.RGBToHSV(c, out float h, out float sat, out float v);
            if (h > 0.5f && h < 0.75f && sat < 0.55f && v > 0.2f && v < 0.9f) { var g = ThemeGold; g.a = c.a; return g; }
            return c;
        }

        public static void Fill(Box b, Color bottom, Color top)
        {
            var img = b.go.AddComponent<Image>();
            img.raycastTarget = false;
            b.go.AddComponent<UIGradient>().SetColors(bottom, top);
        }

        public static void Border(Box b, float radius, Color color)
        {
            var o = Img(b.rt, "Border", outline, color);
            o.pixelsPerUnitMultiplier = RoundedRadius / radius;
            o.raycastTarget = false;
            Stretch(o.rectTransform);
        }

        public static void Rounded(Image img, float radius)
        {
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RoundedRadius / Mathf.Max(radius, 1f);
        }

        public static void Glow(Box parent, Rect r, Color color)
        {
            var b = Add(parent, "Glow", r);
            var img = b.go.AddComponent<Image>();
            img.sprite = glowRect;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
        }

        public static void GlowLine(Box parent, Rect r, Color color)
        {
            var b = Add(parent, "GlowLine", r);
            var img = b.go.AddComponent<Image>();
            img.sprite = glowLine;
            img.color = color;
            img.raycastTarget = false;
        }

        public static Image Line(Box parent, Rect r, Color color)
        {
            var b = Add(parent, "Line", r);
            var img = b.go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Image Icon(Box parent, string icon, Rect r, Color color)
        {
            var b = Add(parent, icon + "Icon", r);
            var img = b.go.AddComponent<Image>();
            img.sprite = Icons[icon];
            img.color = IsSkinSprite(img.sprite) && color.a > 0.9f && color.maxColorComponent > 0.5f ? Color.white : color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Metin. Sabit metin çeviri tablosunda varsa LocalizedText eklenir (dinamik metinlerde localize: false).</summary>
        public static TextMeshProUGUI Txt(Box parent, string name, string text, Rect r, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, Material material = null, bool localize = true, bool wrap = false)
        {
            var b = Add(parent, name, r);
            var t = b.go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (Skin.Count > 0 && inkBody && font == fMedium && material == null)
            {
                color = Ink;
                material = fMedium.material;
            }
            else if (Skin.Count > 0 && font != fTitle)
            {
                Color.RGBToHSV(color, out _, out float cs, out float cv);
                if (cs < 0.2f && cv > 0.55f) color = Color.white;   // soluk gri etiket → beyaz + kontur
            }
            if (material == null && Skin.Count > 0) material = AutoOutline(font);   // tema: her yazı konturlu
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            float want = Skin.Count > 0 ? Mathf.Max(size * 1.18f, MinTextSize) : size;
            t.fontSize = want;
            if (Skin.Count > 0)
            {
                t.enableAutoSizing = true;   // büyüyen yazı kutusuna sığmazsa küçülür (taşma yok)
                t.fontSizeMax = want;
                t.fontSizeMin = Mathf.Min(20f, want);
            }
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            if (localize && Localization.Has(text)) b.go.AddComponent<LocalizedText>().SetKey(text);
            return t;
        }

        public const float MinTextSize = 28f;   // rehber: mutlak alt sınır 28
        /// <summary>Menülerde açıklama metni (fMedium) krem kartlar üstünde koyu mürekkep, kontursuz; açık gri etiketler beyaz.</summary>
        public static bool inkBody;
        private static readonly Color Ink = new Color32(59, 36, 18, 255);
        private static readonly Dictionary<TMP_FontAsset, Material> autoOutline = new Dictionary<TMP_FontAsset, Material>();

        /// <summary>Beyaz/açık yazılara koyu kahve kontur (rehber: kalınlık 0.25–0.3, alt gölge).</summary>
        public static Material AutoOutline(TMP_FontAsset font)
        {
            if (font == null) return null;
            if (font == fExtra) return OutlineText();
            if (font == fTitle) return chromeMaterial;
            if (autoOutline.TryGetValue(font, out var m) && m != null) return m;
            string path = $"{FontDir}/{font.name}_Outline.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = font.material.shader;
            m.CopyPropertiesFromMaterial(font.material);
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.3f);
            m.SetColor("_OutlineColor", new Color32(42, 22, 8, 255));
            m.SetFloat("_FaceDilate", 0.12f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            m.SetFloat("_UnderlayOffsetY", -0.8f);
            m.SetFloat("_UnderlayDilate", 0.3f);
            EditorUtility.SetDirty(m);
            autoOutline[font] = m;
            return m;
        }

        public static void Chrome(TextMeshProUGUI t)
        {
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(Color.white, Color.white, Hex("#FFE7A0"), Hex("#FFE7A0"));   // beyaz → açık sarı
        }

        public static void Shadow(TextMeshProUGUI t)
        {
            var s = t.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            s.effectColor = new Color(0f, 0f, 0f, 0.35f);
            s.effectDistance = new Vector2(0f, -3f);
        }

        public static Button MakeButton(Box b, Graphic target, bool feedback = true)
        {
            var button = b.go.AddComponent<Button>();
            button.targetGraphic = target;
            var colors = button.colors;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            button.colors = colors;
            NoNavigation(button);
            if (feedback) b.go.AddComponent<ButtonPressFeedback>();
            return button;
        }

        public static void NoNavigation(Selectable s)
        {
            var nav = s.navigation;
            nav.mode = Navigation.Mode.None;
            s.navigation = nav;
        }

        public static UIPolygon AddPolygon(Box b)
        {
            b.go.AddComponent<CanvasRenderer>();
            return b.go.AddComponent<UIPolygon>();
        }

        public static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = LayerMask.NameToLayer("UI");
            return (RectTransform)go.transform;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, bool sliced = true)
        {
            var img = NewUI(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced && sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        public static void Stretch(RectTransform rt, Vector2 offsetMin = default, Vector2 offsetMax = default)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        public static Rect Expand(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        // =====================================================================
        // Hazır parçalar (referans stili)
        // =====================================================================

        /// <summary>PLAY / CUSTOMIZE: dış halka + mavi parlak kapsül.</summary>
        public static Button PrimaryButton(Box parent, string name, Rect outer, Rect inner, string label, float size)
        {
            var root = Add(parent, name, outer);
            Glow(root, Expand(inner, 50f), Hex("#3F7DFF", 0.45f));
            var ring = Add(root, "Ring", outer);
            var ringImg = ring.go.AddComponent<Image>();
            Rounded(ringImg, outer.height * 0.5f);
            ringImg.color = Hex("#AFC3FF", 0.07f);
            ringImg.raycastTarget = false;
            Border(ring, outer.height * 0.5f, Hex("#B9C8EE", 0.55f));

            var face = Panel(root, "Face", inner, C.BlueBottom, C.BlueTop, inner.height * 0.5f, Hex("#8AB4FF"));
            var gloss = Add(face, "Gloss", new Rect(inner.x + 24f, inner.y + 8f, inner.width - 48f, inner.height * 0.45f));
            var glossImg = gloss.go.AddComponent<Image>();
            Rounded(glossImg, inner.height * 0.22f);
            glossImg.raycastTarget = false;
            gloss.go.AddComponent<UIGradient>().SetColors(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.16f));
            GlowLine(face, new Rect(inner.x + 70f, inner.yMax - 9f, inner.width - 140f, 18f), Hex("#9CCBFF"));
            Shadow(Txt(face, "Label", label, inner, fExtra, size, Color.white));
            return MakeButton(root, face.rt.GetComponent<Image>());
        }

        /// <summary>QUICK RACE / BUY: koyu kapsül, alt kenarda ışık.</summary>
        public static Button PillButton(Box parent, string name, Rect r, string label, float size, Color textColor, float emphasis,
            Color? border = null, Color? glow = null)
        {
            return PillButton(parent, name, r, label, size, textColor, emphasis, out _, border, glow);
        }

        public static Button PillButton(Box parent, string name, Rect r, string label, float size, Color textColor, float emphasis,
            out TextMeshProUGUI labelText, Color? border = null, Color? glow = null, bool localize = true)
        {
            var root = Add(parent, name, r);
            var face = Panel(root, "Face", r, Hex("#1C2236", 0.92f), Hex("#28324F", 0.92f), r.height * 0.5f,
                (border ?? C.BorderDim) * new Color(1f, 1f, 1f, 0.35f + 0.65f * emphasis));
            if (emphasis > 0.5f)
                GlowLine(face, new Rect(r.x + r.width * 0.15f, r.yMax - 8f, r.width * 0.7f, 14f), glow ?? C.Blue);
            labelText = Txt(face, "Label", label, r, fExtra, size, textColor, localize: localize);
            return MakeButton(root, face.rt.GetComponent<Image>());
        }

        /// <summary>LANGUAGE / HELP / LEGAL tarzı köşeli kutu buton.</summary>
        public static Button BoxButton(Box parent, string name, Rect r, string label, float size = 38f)
        {
            return BoxButton(parent, name, r, label, size, out _);
        }

        public static Button BoxButton(Box parent, string name, Rect r, string label, float size, out TextMeshProUGUI labelText, bool localize = true)
        {
            var b = GoldFace(parent, name, r, 26f, Hex("#E8901A"), Hex("#FFD957"));
            labelText = Txt(b, "Label", label, r, fExtra, size, Color.white, material: OutlineText(), localize: localize);
            return MakeButton(b, b.rt.GetComponent<Image>());
        }

        /// <summary>Tombul parlak düğme yüzü: renk geçişi, beyaz kenar, üstte parlama, altta koyu gölge.</summary>
        public static Box GoldFace(Box parent, string name, Rect r, float radius, Color bottom, Color top)
        {
            if (Skin.Count > 0)
            {
                var fb = Add(parent, name, r);
                var fi = fb.go.AddComponent<Image>();
                SkinSprite(fi, ButtonSkin(top), r);
                AddShine(fb, r, name);
                return fb;
            }
            var shadow = Add(parent, name + "Shadow", new Rect(r.x, r.y + 7f, r.width, r.height));
            var si = shadow.go.AddComponent<Image>();
            Rounded(si, radius);
            si.color = Hex("#5A2E06", 0.75f);
            si.raycastTarget = false;
            themeBypass = true;
            var b = Panel(parent, name, r, bottom, top, radius, Hex("#FFF4CC"));
            themeBypass = false;
            var gloss = Add(b, "Gloss", new Rect(r.x + r.width * 0.06f, r.y + r.height * 0.08f, r.width * 0.88f, r.height * 0.4f));
            var gi = gloss.go.AddComponent<Image>();
            Rounded(gi, radius * 0.6f);
            gi.raycastTarget = false;
            gloss.go.AddComponent<UIGradient>().SetColors(new Color(1f, 1f, 1f, 0f), new Color(1f, 1f, 1f, 0.32f));
            AddShine(b, r, name);
            return b;
        }

        /// <summary>Kayan ışık şeridi (UIShine): yüzey maskeler, şerit çapraz.</summary>
        private static void AddShine(Box b, Rect r, string name)
        {
            b.go.AddComponent<Mask>().showMaskGraphic = true;
            var band = NewUI("Shine", b.rt);
            band.anchorMin = band.anchorMax = new Vector2(0.5f, 0.5f);
            band.sizeDelta = new Vector2(Mathf.Max(40f, r.width * 0.22f), r.height * 2.4f);
            band.localRotation = Quaternion.Euler(0f, 0f, -22f);
            var bi = band.gameObject.AddComponent<Image>();
            bi.color = new Color(1f, 1f, 1f, 0.28f);
            bi.raycastTarget = false;
            var shine = b.go.AddComponent<UIShine>();
            Set(shine, "band", band);
            SetFloat(shine, "startDelay", (Mathf.Abs(name.GetHashCode()) % 100) / 100f * 2.5f);   // düğmeler aynı anda parlamasın
        }

        private static Material outlineText;

        /// <summary>Beyaz yazı + kalın koyu kahve kontur (çizgi film düğme yazısı).</summary>
        public static Material OutlineText()
        {
            if (outlineText != null) return outlineText;
            const string path = "Assets/Art/UI/Fonts/ExtraBold_Brown.mat";
            outlineText = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (outlineText == null)
            {
                outlineText = new Material(fExtra.material);
                AssetDatabase.CreateAsset(outlineText, path);
            }
            outlineText.shader = fExtra.material.shader;
            outlineText.mainTexture = fExtra.material.mainTexture;   // font değişince eski atlas kalmasın
            outlineText.EnableKeyword("OUTLINE_ON");
            outlineText.SetFloat("_OutlineWidth", 0.32f);
            outlineText.SetColor("_OutlineColor", new Color32(52, 24, 6, 255));
            outlineText.SetFloat("_FaceDilate", 0.18f);
            outlineText.EnableKeyword("UNDERLAY_ON");
            outlineText.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            outlineText.SetFloat("_UnderlayOffsetY", -0.9f);
            outlineText.SetFloat("_UnderlayDilate", 0.35f);
            outlineText.SetFloat("_UnderlaySoftness", 0.1f);
            EditorUtility.SetDirty(outlineText);
            return outlineText;
        }

        /// <summary>Ayarlar tarzı üst başlık çubuğu: geri butonu + krom başlık. Geri butonunu döndürür.</summary>
        public static Button Header(Box parent, string title)
        {
            if (Skin.Count > 0)
            {
                SkinImage(parent, "HeaderPlaque", new Rect(150f, 36f, 816f, 150f), "plaque", false, 0.5f);
                var backBox = Add(parent, "BackButton", new Rect(30f, 44f, 128f, 128f));
                var bi = backBox.go.AddComponent<Image>();
                bi.sprite = Skin["i_back"];
                bi.preserveAspect = true;
                Button bb = MakeButton(backBox, bi);
                Chrome(Txt(parent, "Title", title, new Rect(250f, 50f, 616f, 120f), fTitle, title.Length > 10 ? 70f : 84f, Color.white, material: chromeMaterial));
                return bb;
            }
            var header = Add(parent, "HeaderBar", new Rect(0f, 0f, 1116f, 200f));
            var poly = AddPolygon(header);
            poly.Configure(new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.86f),
                new Vector2(0.833f, 0.86f), new Vector2(0.81f, 1f), new Vector2(0f, 1f),
            });
            poly.color = Color.white;
            poly.raycastTarget = false;
            header.go.AddComponent<UIGradient>().SetColors(Hex("#0E4555"), Hex("#2A8C9C"));
            header.go.AddComponent<Mask>().showMaskGraphic = true;
            var glow = Img(header.rt, "GoldGlow", softCircle, Hex("#F2C24E", 0.35f), false);
            Place(glow.rectTransform, header, new Rect(230f, -150f, 660f, 420f));
            glow.raycastTarget = false;
            Ribbon(parent, new Rect(905f, 18f, 190f, 150f), false);
            Ribbon(parent, new Rect(8f, 150f, 170f, 70f), true);

            var edge = Add(parent, "HeaderEdge", new Rect(0f, 0f, 1116f, 200f));
            var edgePoly = AddPolygon(edge);
            edgePoly.Configure(new[] { new Vector2(0f, 0.99f), new Vector2(0.81f, 0.99f), new Vector2(0.833f, 0.855f), new Vector2(1f, 0.855f) }, true, 4f);
            edgePoly.color = ThemeGold;
            edgePoly.raycastTarget = false;

            var back = Panel(parent, "BackButton", new Rect(40f, 42f, 123f, 123f), Hex("#4F1B2E"), Hex("#2E3A5E"), 18f, Hex("#7C8DB8"));
            Icon(back, "arrow-left", new Rect(57f, 59f, 89f, 89f), Color.white);
            Button backButton = MakeButton(back, back.rt.GetComponent<Image>());

            Chrome(Txt(parent, "Title", title, new Rect(190f, 40f, 740f, 130f), fTitle, title.Length > 9 ? 76f : 92f, Color.white, material: chromeMaterial));
            Gem(parent, new Vector2(560f, 186f), 22f);
            return backButton;
        }

        /// <summary>Altın kurdele kıvrımı (bant uçları): iki ince hilal + uçta turkuaz damla.</summary>
        public static void Ribbon(Box parent, Rect r, bool flip)
        {
            Vector2[] Crescent(float a0, float a1, float thick)
            {
                var pts = new System.Collections.Generic.List<Vector2>();
                for (int i = 0; i <= 12; i++) { float a = Mathf.Lerp(a0, a1, i / 12f); pts.Add(new Vector2(0.5f + 0.5f * Mathf.Cos(a), 0.5f + 0.5f * Mathf.Sin(a))); }
                for (int i = 12; i >= 0; i--) { float a = Mathf.Lerp(a0, a1, i / 12f); float k = 0.5f - thick * Mathf.Sin(i / 12f * Mathf.PI); pts.Add(new Vector2(0.5f + k * Mathf.Cos(a), 0.5f + k * Mathf.Sin(a))); }
                if (flip) for (int i = 0; i < pts.Count; i++) pts[i] = new Vector2(1f - pts[i].x, pts[i].y);
                return pts.ToArray();
            }
            var a = Poly(parent, "RibbonGold", r, Crescent(0.4f, 2.6f, 0.13f), ThemeGold);
            a.raycastTarget = false;
            var b = Poly(parent, "RibbonTeal", new Rect(r.x + r.width * 0.15f, r.y + r.height * 0.2f, r.width * 0.7f, r.height * 0.7f), Crescent(3.4f, 5.6f, 0.16f), Hex("#5FE0D0"));
            b.raycastTarget = false;
        }

        /// <summary>Elmas biçimli turkuaz mücevher (bant ortası), altın çerçeveli.</summary>
        public static void Gem(Box parent, Vector2 c, float s)
        {
            Vector2[] diamond = { new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f) };
            Poly(parent, "GemFrame", new Rect(c.x - s * 1.35f, c.y - s * 1.35f, s * 2.7f, s * 2.7f), diamond, ThemeGold).raycastTarget = false;
            Poly(parent, "Gem", new Rect(c.x - s, c.y - s, s * 2f, s * 2f), diamond, Hex("#7FF0E4")).raycastTarget = false;
        }

        /// <summary>Başlıklı bölüm paneli (AUDIO / GRAPHICS / CONTROLS tarzı).</summary>
        public static Box Section(Box parent, string name, string title, Rect r)
        {
            if (Skin.Count > 0)
            {
                var sp = Add(parent, name + "Panel", r);
                var spi = sp.go.AddComponent<Image>();
                SkinSprite(spi, "panel_basalt", r, 0.11f);   // ince çerçeve: içerik kenara taşmasın
                spi.raycastTarget = true;
                float rw = Mathf.Min(r.width * 0.8f, 640f);
                RibbonLabel(sp, "Title", new Rect(r.center.x - rw * 0.5f, r.y - 34f, rw, 132f), title, 44f);
                return sp;
            }
            var panel = Panel(parent, name + "Panel", r, Hex("#4A1B2C"), Hex("#212A44"), 28f, Hex("#7083B3"));
            var band = Add(panel, "HeaderBand", new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 90f));
            var bandImg = band.go.AddComponent<Image>();
            Rounded(bandImg, 26f);
            bandImg.raycastTarget = false;
            band.go.AddComponent<UIGradient>().SetColors(Hex("#B9802A"), Hex("#F5D57E"));   // altın bant
            Txt(panel, "Title", title, new Rect(r.x, r.y + 12f, r.width, 72f), fExtra, 50f, Color.white, material: OutlineText());
            Line(panel, new Rect(r.x + 4f, r.y + 92f, r.width - 8f, 3f), Hex("#7A4A14"));
            Gem(panel, new Vector2(r.center.x, r.y + 93f), 12f);
            return panel;
        }

        /// <summary>Koyu iz + kırmızı→mavi dolgu çubuğu. Dolgu sola yaslıdır (genişliği koddan değişir).</summary>
        public static RectTransform ProgressBar(Box parent, string name, Rect r, float fill)
        {
            themeBypass = true;
            var track = Panel(parent, name, r, Hex("#082331"), Hex("#0C3042"), r.height * 0.5f, Hex("#C99A3A"));
            themeBypass = false;
            var bar = Add(track, "Fill", new Rect(r.x + 3f, r.y + 3f, (r.width - 6f) * fill, r.height - 6f));
            var img = bar.go.AddComponent<Image>();
            Rounded(img, (r.height - 6f) * 0.5f);
            img.raycastTarget = false;
            bar.go.AddComponent<UIGradient>().SetColors(Hex("#F29A1D"), Hex("#FFE07A"), true);   // altın dolgu
            PivotLeft(bar);
            return bar.rt;
        }

        public static Slider RefSlider(Box parent, string name, Rect track, float value)
        {
            var root = Add(parent, name, new Rect(track.x, track.y - 20f, track.width, track.height + 40f));

            var bg = Add(root, "Background", track);
            var bgImg = bg.go.AddComponent<Image>();
            Rounded(bgImg, track.height * 0.5f);
            bgImg.color = Hex("#082331");
            Border(bg, track.height * 0.5f, Hex("#C99A3A"));

            var fillArea = Add(root, "Fill Area", track);
            var fill = Add(fillArea, "Fill", track);
            var fillImg = fill.go.AddComponent<Image>();
            Rounded(fillImg, track.height * 0.5f);
            fill.go.AddComponent<UIGradient>().SetColors(Hex("#E8901A"), Hex("#FFD957"), true);   // altın dolgu
            fill.rt.anchorMin = Vector2.zero;
            fill.rt.anchorMax = new Vector2(0f, 1f);
            fill.rt.sizeDelta = Vector2.zero;
            fill.rt.anchoredPosition = Vector2.zero;

            var handleArea = Add(root, "Handle Slide Area", track);
            var handle = Add(handleArea, "Handle", new Rect(track.x, track.y - 16f, 68f, 68f));
            handle.rt.anchorMin = new Vector2(0f, 0.5f);
            handle.rt.anchorMax = new Vector2(0f, 0.5f);
            handle.rt.anchoredPosition = Vector2.zero;
            var ring = Img(handle.rt, "RingGlow", ringGlow, Hex("#FFD957", 0.95f), false);
            ring.raycastTarget = false;
            ring.rectTransform.sizeDelta = new Vector2(100f, 100f);
            var knobImg = Img(handle.rt, "Knob", knob, Color.white, false);
            knobImg.rectTransform.sizeDelta = new Vector2(54f, 54f);

            var slider = root.go.AddComponent<Slider>();
            slider.fillRect = fill.rt;
            slider.handleRect = handle.rt;
            slider.targetGraphic = knobImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            NoNavigation(slider);
            return slider;
        }

        public static SwitchToggle RefSwitch(Box parent, string name, Rect r, bool on)
        {
            var root = Add(parent, name, r);
            var img = root.go.AddComponent<Image>();
            GameObject onTrack = null;
            if (Skin.Count > 0)
            {
                SkinSprite(img, "btn_red", r, 0.5f);
                var onImg = Img(root.rt, "OnTrack", Skin["btn_green"], Color.white);
                SkinSprite(onImg, "btn_green", r, 0.5f);
                onImg.raycastTarget = false;
                Stretch(onImg.rectTransform);
                onTrack = onImg.gameObject;
                onTrack.SetActive(on);
            }
            else
            {
                Rounded(img, r.height * 0.5f);
                root.go.AddComponent<UIGradient>().SetColors(Hex("#0A2A3A"), Hex("#123C50"), true);
                Border(root, r.height * 0.5f, Hex("#C99A3A"));
            }
            var k = Img(root.rt, "Knob", knob, Color.white, false);
            k.raycastTarget = false;
            k.rectTransform.sizeDelta = new Vector2(52f, 52f);

            var toggle = root.go.AddComponent<Toggle>();
            toggle.targetGraphic = img;
            toggle.isOn = on;
            NoNavigation(toggle);
            var sw = root.go.AddComponent<SwitchToggle>();
            float travel = r.width * 0.5f - r.height * 0.5f;
            Set(sw, "knob", k.rectTransform);
            if (onTrack != null) Set(sw, "onTrack", onTrack);
            SetFloat(sw, "offX", -travel);
            SetFloat(sw, "onX", travel);
            k.rectTransform.anchoredPosition = new Vector2(on ? travel : -travel, 0f);
            return sw;
        }

        public static void Logo(Box parent, string subtitle)
        {
            if (Skin.TryGetValue("logo", out var logoSprite))
            {
                var lb = Add(parent, "Logo", new Rect(288f, 70f, 540f, 270f));
                var lim = lb.go.AddComponent<Image>();
                lim.sprite = logoSprite;
                lim.preserveAspect = true;
                lim.raycastTarget = false;
                if (!string.IsNullOrEmpty(subtitle))
                {
                    RibbonLabel(parent, "Subtitle", new Rect(258f, 326f, 600f, 120f), subtitle, 32f);
                }
                return;
            }
            const float skew = -12f;
            var plate = Panel(parent, "LogoPlate", new Rect(128f, 118f, 860f, 165f), Hex("#1A2340"), Hex("#2E4272"), 18f, Hex("#8FA4D6"));
            plate.go.AddComponent<UISkew>().SetAngle(skew);
            plate.rt.Find("Border").gameObject.AddComponent<UISkew>().SetAngle(skew);

            var redBar = Panel(parent, "LogoBar", new Rect(150f, 243f, 805f, 20f), Hex("#8E2A44"), Hex("#6A1F33"), 10f, Hex("#C9637B", 0.6f));
            redBar.rt.GetComponent<UIGradient>().SetColors(Hex("#8E2A44"), Hex("#6A1F33"), true);
            redBar.go.AddComponent<UISkew>().SetAngle(skew);

            Chrome(Txt(parent, "LogoText", "JUMPLINE GO", new Rect(128f, 112f, 860f, 140f), fTitle, 130f, Color.white, material: chromeMaterial));

            var tab = Add(parent, "SubtitleTab", new Rect(305f, 283f, 505f, 58f));
            var poly = AddPolygon(tab);
            poly.Configure(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.95f, 1f), new Vector2(0.05f, 1f) });
            poly.color = Hex("#151A28", 0.8f);
            poly.raycastTarget = false;
            Txt(parent, "Subtitle", subtitle, new Rect(305f, 286f, 505f, 54f), fBold, 27f, Hex("#8C95AA"));
        }

        // ---------------------------------------------------------------------
        // home_v2 referansı: cam paneller, devre çizgileri, ışık halkaları
        // ---------------------------------------------------------------------

        public static readonly Color Pink = Hex("#FF5C93");
        public static readonly Color Cyan = Hex("#5AA8FF");

        /// <summary>Sol üstü içeri kaymış paralelkenar ("/" kenarlı).</summary>
        public static Vector2[] Parallelogram(float slant) =>
            new[] { new Vector2(slant, 0f), new Vector2(1f, 0f), new Vector2(1f - slant, 1f), new Vector2(0f, 1f) };

        /// <summary>Sol üstte sekmesi olan dosya biçimi (alt gezinme kutuları).</summary>
        public static readonly Vector2[] FolderShape =
        {
            new Vector2(0f, 0.16f), new Vector2(0.12f, 0f), new Vector2(0.46f, 0f), new Vector2(0.54f, 0.08f),
            new Vector2(1f, 0.08f), new Vector2(1f, 1f), new Vector2(0f, 1f),
        };

        public static UIPolygon Poly(Box parent, string name, Rect r, Vector2[] shape, Color color)
        {
            var b = Add(parent, name, r);
            var p = AddPolygon(b);
            p.Configure(shape);
            p.color = color;
            p.raycastTarget = false;
            return p;
        }

        public static UIPolygon PolyStroke(Box parent, string name, Rect r, Vector2[] shape, Color color, float thickness = 2.5f)
        {
            var closed = new Vector2[shape.Length + 1];
            shape.CopyTo(closed, 0);
            closed[shape.Length] = shape[0];
            var b = Add(parent, name, r);
            var p = AddPolygon(b);
            p.Configure(closed, true, thickness);
            p.color = color;
            p.raycastTarget = false;
            return p;
        }

        /// <summary>Yavaş dönen, nefes alan ışık halkası (elips).</summary>
        public static Image Ring(Box parent, Rect r, Color color, float angle, float speed, float phase = 0f)
        {
            var b = Add(parent, "OrbitRing", r);
            var img = b.go.AddComponent<Image>();
            img.sprite = arcRing;
            img.color = color;
            img.raycastTarget = false;
            b.rt.localEulerAngles = new Vector3(0f, 0f, angle);
            var o = b.go.AddComponent<OrbitRing>();
            SetFloat(o, "speed", speed);
            SetFloat(o, "phase", phase);
            return img;
        }

        private static void Pulse(Graphic g, float amount, float period, float phase)
        {
            var p = g.gameObject.AddComponent<GlowPulse>();
            SetFloat(p, "amount", amount);
            SetFloat(p, "period", period);
            SetFloat(p, "phase", phase);
        }

        private static Button WithFeedback(Box root, Graphic target, Graphic flash)
        {
            Button b = MakeButton(root, target, false);
            var fb = root.go.AddComponent<ButtonPressFeedback>();
            Set(fb, "flash", flash);
            return b;
        }

        /// <summary>Ana PLAY: cam dış plaka + bordo devreli iç panel + iki ışık halkası.</summary>
        public static Button GlassPlayButton(Box parent, string name, Rect r, string label, float size)
        {
            // tema "Efsane": parlak yeşil, altın çerçeveli büyük oyna düğmesi (2. referans "hızlı başla")
            var root = Add(parent, name, r);
            var halo = Img(root.rt, "Halo", softCircle, Hex("#FFE07A", 0.35f), false);
            Place(halo.rectTransform, root, new Rect(r.x - 60f, r.y - 70f, r.width + 120f, r.height + 140f));
            halo.raycastTarget = false;
            Pulse(halo, 0.3f, 3f, 0f);
            if (Skin.Count == 0)
            {
                var frame = Add(root, "GoldFrame", new Rect(r.x - 14f, r.y - 14f, r.width + 28f, r.height + 28f));
                var fi = frame.go.AddComponent<Image>();
                Rounded(fi, r.height * 0.5f + 14f);
                fi.raycastTarget = false;
                frame.go.AddComponent<UIGradient>().SetColors(Hex("#B9802A"), Hex("#FFE38A"));
            }
            var face = GoldFace(root, "Face", r, r.height * 0.5f, Hex("#E8901A"), Hex("#FFD23F"));   // referans: dev safran "OYNA"
            if (Skin.Count == 0)
            {
                Ribbon(root, new Rect(r.xMax - 70f, r.y - 60f, 150f, 120f), false);
                Ribbon(root, new Rect(r.x - 80f, r.yMax - 70f, 150f, 120f), true);
            }
            Txt(face, "Label", label, new Rect(r.x, r.y - r.height * 0.06f, r.width, r.height), fExtra, size, Color.white, material: OutlineText());
            AddSparkles(root, r, 14, 46f);
            return MakeButton(root, face.rt.GetComponent<Image>());
        }

        /// <summary>Öğenin çevresinde parlayıp sönen yıldız parçacıkları (UISparkles).</summary>
        public static UISparkles AddSparkles(Box parent, Rect r, int count, float size)
        {
            var b = Add(parent, "Sparkles", r);
            var sp = b.go.AddComponent<UISparkles>();
            Set(sp, "sprite", Icons["star"]);
            SetInt(sp, "count", count);
            SetFloat(sp, "size", size);
            return sp;
        }

        /// <summary>QUICK RACE / DAILY CHALLENGE etiket butonu (mavi ya da bordo).</summary>
        public static Button TagButton(Box parent, string name, Rect r, string label, bool blue, float size = 38f)
        {
            // tema "Efsane": tombul parlak düğme — turuncu (hızlı yarış) / kırmızı (günlük görev), 1. referans
            var root = Add(parent, name, r);
            var face = GoldFace(root, "Face", r, r.height * 0.5f, blue ? Hex("#E2761A") : Hex("#B8282A"), blue ? Hex("#FFC24A") : Hex("#FF6A55"));
            Txt(face, "Label", label, r, fExtra, size, Color.white, material: OutlineText());
            return MakeButton(root, face.rt.GetComponent<Image>());
        }

        /// <summary>Alt gezinme kutusu: dosya biçimi, devre çizgisi, ikon, ışık halkası.</summary>
        public static Button FolderTile(Box parent, string name, Rect r, string icon, string label, int index)
        {
            // tema "Efsane": yuvarlak köşeli altın ikon kutusu (1. referans), konturlu etiket
            var root = Add(parent, name, r);
            var face = GoldFace(root, "Tile", r, 34f, Hex("#E8901A"), Hex("#FFD957"));
            var shade = Icon(face, icon, new Rect(r.center.x - 44f, r.center.y - 34f, 88f, 88f), Hex("#6A3A0A", 0.6f));
            shade.name = "IconShadow";
            Icon(face, icon, new Rect(r.center.x - 44f, r.center.y - 40f, 88f, 88f), Color.white);
            Txt(parent, name + "Label", label, new Rect(r.center.x - 115f, r.yMax + 12f, 230f, 40f), fExtra, 24f, Color.white, material: OutlineText());
            return MakeButton(root, face.rt.GetComponent<Image>());
        }

        /// <summary>3B karakter görüntüsü (RenderTexture). Kare dokuyu kutunun oranına göre kırpar.</summary>
        public static RawImage RawPreview(Box parent, string name, Rect r, Texture texture)
        {
            var b = Add(parent, name, r);
            var raw = b.go.AddComponent<RawImage>();
            raw.texture = texture;
            raw.raycastTarget = false;
            float aspect = r.width / r.height;
            raw.uvRect = aspect < 1f
                ? new Rect((1f - aspect) * 0.5f, 0f, aspect, 1f)
                : new Rect(0f, (1f - 1f / aspect) * 0.5f, 1f, 1f / aspect);
            return raw;
        }

        public static Toast BuildToast(Transform parent)
        {
            var rt = NewUI("ToastLayer", parent);
            Stretch(rt);
            Box mid = Group(rt, "Middle", 0.5f);
            var t = Panel(mid, "Toast", new Rect(208f, 1180f, 700f, 92f), Hex("#1B2236", 0.95f), Hex("#2A3556", 0.95f), 46f, Hex("#7083B3"));
            t.rt.GetComponent<Image>().raycastTarget = false;
            var label = Txt(t, "Label", "", new Rect(208f, 1180f, 700f, 92f), fBold, 34f, Color.white, localize: false);
            var toast = t.go.AddComponent<Toast>();
            Set(toast, "label", label);
            toast.GetComponent<CanvasGroup>().alpha = 0f;
            return toast;
        }

        // =====================================================================
        // Serileştirilmiş alanlar
        // =====================================================================

        private static SerializedProperty Prop(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p == null) throw new System.ArgumentException($"{target.GetType().Name} içinde '{field}' alanı yok");
            return p;
        }

        public static void Set(Object target, string field, Object value)
        {
            Prop(target, field, out var so).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var p = Prop(target, field, out var so);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            Prop(target, field, out var so).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            Prop(target, field, out var so).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string field, string value)
        {
            Prop(target, field, out var so).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string field, int value)
        {
            Prop(target, field, out var so).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string field, Color value)
        {
            Prop(target, field, out var so).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Serileştirilmiş [Serializable] sınıf dizisini doldurur: her eleman için alan adı → değer.</summary>
        public static void SetStructArray(Object target, string field, List<Dictionary<string, object>> elements)
        {
            var p = Prop(target, field, out var so);
            p.arraySize = elements.Count;
            for (int i = 0; i < elements.Count; i++)
            {
                var el = p.GetArrayElementAtIndex(i);
                foreach (var kv in elements[i])
                {
                    var sub = el.FindPropertyRelative(kv.Key);
                    if (sub == null) throw new System.ArgumentException($"{field}[{i}] içinde '{kv.Key}' yok");
                    switch (kv.Value)
                    {
                        case Object o: sub.objectReferenceValue = o; break;
                        case string s: sub.stringValue = s; break;
                        case float f: sub.floatValue = f; break;
                        case int n: sub.intValue = n; break;
                        case bool bo: sub.boolValue = bo; break;
                        case Color col: sub.colorValue = col; break;
                        case Dictionary<string, object> nested:
                            foreach (var nkv in nested)
                            {
                                var nsub = sub.FindPropertyRelative(nkv.Key);
                                if (nsub == null) throw new System.ArgumentException($"{field}[{i}].{kv.Key} içinde '{nkv.Key}' yok");
                                if (nkv.Value is Object no) nsub.objectReferenceValue = no;
                                else if (nkv.Value is string ns) nsub.stringValue = ns;
                                else if (nkv.Value is float nf) nsub.floatValue = nf;
                            }
                            break;
                        default: sub.objectReferenceValue = null; break;
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        // Asset üretimi
        // =====================================================================

        public static void LoadAssets()
        {
            rounded = MakeSprite("RoundedRect", RoundedRectAlpha, RoundedBorder);
            outline = MakeSprite("RoundedOutline", RoundedOutlineAlpha, RoundedBorder);
            circle = MakeSprite("Circle", CircleAlpha, 0);
            glowRect = MakeSprite("GlowRect", GlowRectAlpha, 63);
            ringGlow = MakeSprite("RingGlow", RingGlowAlpha, 0);
            softCircle = MakeSprite("SoftCircle", SoftCircleAlpha, 0);
            glowLine = MakeGlowLine();
            knob = MakeKnob();
            background = MakeBackground();

            Icons.Clear();
            foreach (string n in new[] { "shopping-cart", "calendar-star", "backpack", "helmet", "settings", "arrow-left", "ban", "player-pause", "check",
                                         "heart", "throw_melon", "throw_jug", "boss_head", "mission_pouch", "mission_fish", "star", "lock" })
                Icons[n] = ImportSprite($"{IconDir}/{n}.png");
            circuit = MakeCircuit();
            arcRing = MakeSizedSprite("ArcRing", 256, ArcRingAlpha);
            Icons["coin"] = MakeCoin();
            Icons["gem"] = MakeGem();
            // docs/references/items + character: güç ve ekipman görselleri (referanstan kesilir)
            Icons["shield"] = CropReference("shield", "items/shield.jpg", new RectInt(130, 520, 700, 790), false);
            Icons["speed_boost"] = CropReference("speed_boost", "items/speed_boost.jpg", new RectInt(480, 380, 1060, 960), false);
            Icons["magnet"] = CropReference("magnet", "items/magnet.jpg", new RectInt(30, 440, 980, 1020), false);
            Icons["double_gold"] = CropReference("double_gold", "items/double_gold.jpg", new RectInt(330, 540, 1360, 850), false);
            Icons["gear_helmet"] = CropReference("gear_helmet", "character/helmet.jpg", new RectInt(210, 560, 800, 920), true);
            Icons["gear_gloves"] = CropReference("gear_gloves", "character/gloves.jpg", new RectInt(130, 630, 880, 830), true);
            // docs/references/market: market görselleri (768x1376 referanslar)
            (string key, string file, RectInt r)[] market =
            {
                ("pack_velocity", "offers.jpg", new RectInt(70, 255, 155, 130)),
                ("pack_bundle", "offers.jpg", new RectInt(630, 250, 138, 150)),
                ("pack_starter", "offers.jpg", new RectInt(80, 1158, 125, 102)),
                ("pack_value", "offers.jpg", new RectInt(312, 1155, 128, 105)),
                ("pack_goldbundle", "offers.jpg", new RectInt(548, 1150, 135, 110)),
                ("gold_pouch", "gold.jpg", new RectInt(60, 225, 180, 160)),
                ("gold_stack", "gold.jpg", new RectInt(285, 222, 200, 170)),
                ("gold_vault", "gold.jpg", new RectInt(530, 222, 180, 165)),
                ("gem_pouch", "gems.jpg", new RectInt(65, 240, 175, 150)),
                ("gem_cluster", "gems.jpg", new RectInt(290, 240, 185, 145)),
                ("gem_cache", "gems.jpg", new RectInt(530, 236, 180, 155)),
                ("stealth_helmet", "gear.jpg", new RectInt(60, 262, 178, 165)),
                ("velocity_helmet", "gear.jpg", new RectInt(292, 262, 200, 165)),
                ("titanium_helmet", "gear.jpg", new RectInt(528, 262, 182, 165)),
                ("stealth_gloves", "gear.jpg", new RectInt(60, 622, 178, 165)),
                ("velocity_gloves", "gear.jpg", new RectInt(292, 622, 200, 165)),
                ("titanium_gloves", "gear.jpg", new RectInt(528, 622, 182, 165)),
                ("stealth_boots", "gear.jpg", new RectInt(60, 988, 178, 165)),
                ("velocity_boots", "gear.jpg", new RectInt(292, 988, 200, 165)),
                ("titanium_boots", "gear.jpg", new RectInt(528, 988, 182, 165)),
                ("boost_magnet_big", "boosts.jpg", new RectInt(70, 210, 340, 290)),
                ("boost_x2_big", "boosts.jpg", new RectInt(95, 560, 300, 270)),
                ("boost_speed_big", "boosts.jpg", new RectInt(95, 900, 300, 280)),
            };
            foreach (var m in market) Icons[m.key] = CropReference(m.key, "market/" + m.file, m.r, false);
            Icons["bg_road"] = CropPlain("bg_road", "market/gold.jpg", new RectInt(0, 500, 768, 720));
            Icons["gear_boots"] = CropReference("gear_boots", "character/boots.jpg", new RectInt(110, 600, 1000, 810), true);

            LoadSkin();
            // tema "Taş & Çini": tombul oyun fontu (başlık/düğme/sayı) + çok okunur kalın gövde (OFL, Türkçe tam; kullanıcı 13 Eki seçimi)
            fMedium = GetOrCreateFont("Rubik-Bold");
            fBold = GetOrCreateFont("Rubik-ExtraBold");
            fExtra = GetOrCreateFont("PaytoneOne-Regular");
            fTitle = fExtra;
            chromeMaterial = GetOrCreateChromeMaterial();
        }

        /// <summary>9 dilimli kenarlar (kaynak piksel). Sözlükte olmayan parçalar dilimlenmez.</summary>
        private static Vector4 SkinBorder(string n)
        {
            if (n.StartsWith("btn_")) return new Vector4(74f, 74f, 74f, 74f);
            switch (n)
            {
                case "panel_basalt": return new Vector4(98f, 98f, 98f, 98f);
                case "card_lime": return new Vector4(58f, 58f, 58f, 58f);
                case "plaque": return new Vector4(124f, 54f, 124f, 54f);
                case "ribbon": return new Vector4(214f, 0f, 214f, 0f);   // dikeyde dilimlenmez: gövde hep %5-55 yükseklikte
                case "pill": return new Vector4(54f, 46f, 54f, 46f);
                case "navbar": return new Vector4(60f, 40f, 60f, 40f);
                case "parchment": return new Vector4(64f, 64f, 64f, 64f);
                default: return Vector4.zero;
            }
        }

        private static void LoadSkin()
        {
            Skin.Clear();
            if (!AssetDatabase.IsValidFolder(SkinDir)) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SkinDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(path);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                bool bg = n.StartsWith("bg_");
                Vector4 border = SkinBorder(n);
                if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != border || imp.mipmapEnabled)
                {
                    imp.textureType = TextureImporterType.Sprite;
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.spriteBorder = border;
                    imp.alphaIsTransparency = !bg;
                    imp.mipmapEnabled = false;
                    imp.wrapMode = TextureWrapMode.Clamp;
                    imp.maxTextureSize = 2048;
                    imp.textureCompression = bg ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;
                    imp.SaveAndReimport();
                }
                Skin[n] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            if (Skin.TryGetValue("bg_menu", out var bgm)) background = bgm;
            // renkli ikonlar (eski tek renkli ikonların yerine)
            (string key, string skin)[] icons =
            {
                ("coin", "i_coin"), ("gem", "i_gem"), ("star", "i_star"), ("heart", "i_heart"), ("lock", "i_lock"),
                ("settings", "i_gear"), ("shopping-cart", "i_shop"), ("calendar-star", "i_calendar"), ("backpack", "i_chest"),
                ("helmet", "i_shield"), ("arrow-left", "i_back"), ("player-pause", "i_pause"),
                ("book", "i_book"), ("map", "i_map"), ("scroll", "i_scroll"), ("trophy", "i_trophy"), ("home", "i_home"),
                ("replay", "i_replay"), ("music", "i_music"), ("sound", "i_sound"), ("swords", "i_swords"), ("video", "i_video"),
                ("bolt", "i_bolt"), ("close", "i_close"), ("globe", "i_globe"), ("help", "i_help"), ("vibrate", "i_vibrate"),
                ("bell", "i_bell"), ("chest", "i_chest"), ("nazar", "i_nazar"), ("melon", "i_melon"), ("gear", "i_gear"),
            };
            foreach (var (key, skin) in icons)
                if (Skin.TryGetValue(skin, out var sp)) Icons[key] = sp;
        }

        public static bool IsSkinSprite(Sprite sp) => sp != null && Skin.ContainsValue(sp);

        /// <summary>Kutunun kısa kenarına göre 9 dilim kenarını ölçekler (küçük düğmede köşeler taşmasın).</summary>
        public static void SkinSprite(Image img, string skin, Rect r, float edge = 0.42f)
        {
            img.sprite = Skin[skin];
            Vector4 b = img.sprite.border;
            if (b == Vector4.zero) { img.type = Image.Type.Simple; return; }
            img.type = Image.Type.Sliced;
            float maxB = Mathf.Max(Mathf.Max(b.x, b.z), Mathf.Max(b.y, b.w));
            float fit = Mathf.Min(r.width, r.height) * edge;
            img.pixelsPerUnitMultiplier = Mathf.Max(0.6f, maxB / Mathf.Max(fit, 1f));
        }

        /// <summary>Altın kurdele + gövdesine ortalı beyaz konturlu yazı (kurdele gövdesi resmin üst yarısında, uçlar katlı).</summary>
        public static TextMeshProUGUI RibbonLabel(Box parent, string name, Rect r, string text, float size, bool localize = true, TMP_FontAsset font = null)
        {
            SkinImage(parent, name + "Ribbon", r, "ribbon", false, 0.6f);
            var tr = new Rect(r.x + r.height * 0.5f, r.y + r.height * 0.06f, r.width - r.height, r.height * 0.48f);
            var t = Txt(parent, name, text, tr, font ?? fExtra, size, Color.white, localize: localize);
            t.alignment = TextAlignmentOptions.Center;
            return t;
        }

        public static Image SkinImage(Box parent, string name, Rect r, string skin, bool raycast = false, float edge = 0.42f)
        {
            var b = Add(parent, name, r);
            var img = b.go.AddComponent<Image>();
            SkinSprite(img, skin, r, edge);
            img.raycastTarget = raycast;
            if (img.type == Image.Type.Simple) img.preserveAspect = true;
            return img;
        }

        /// <summary>Hazır düğmenin yüzünü başka renkteki parçayla değiştirir (yeşil DEVAM, kırmızı ANA MENÜ…).</summary>
        public static Button Reskin(Button b, string skin)
        {
            if (b == null || !Skin.ContainsKey(skin)) return b;
            var img = b.GetComponent<Image>();
            var rt = (RectTransform)b.transform;
            SkinSprite(img, skin, new Rect(0f, 0f, rt.sizeDelta.x, rt.sizeDelta.y));
            return b;
        }

        /// <summary>Düğme yüzü rengi → hazır düğme parçası.</summary>
        public static string ButtonSkin(Color top)
        {
            Color.RGBToHSV(top, out float h, out float sat, out float v);
            if (sat < 0.22f) return "btn_basalt";
            if (h < 0.035f || h > 0.92f) return "btn_red";
            if (h < 0.17f) return "btn_saffron";
            if (h < 0.42f) return "btn_green";
            if (h < 0.56f) return "btn_turquoise";
            return "btn_cobalt";
        }

        public static T GetOrCreateAsset<T>(string path, System.Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            init?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static TMP_FontAsset GetOrCreateFont(string fileName)
        {
            string assetPath = $"{FontDir}/{fileName} SDF.asset";
            var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (asset != null) return asset;

            var source = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{fileName}.ttf");
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 14, GlyphRenderMode.SDFAA, 1024, 1024);   // geniş dolgu: kalın kontur
            asset.name = $"{fileName} SDF";
            AssetDatabase.CreateAsset(asset, assetPath);
            asset.atlasTextures[0].name = $"{fileName} Atlas";
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            asset.material.name = $"{fileName} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?.,:;%/-+'\"()·ÇĞİÖŞÜçğıöşü", out _);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static Material GetOrCreateChromeMaterial()
        {
            string path = $"{FontDir}/Exo2 Chrome.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(fTitle.material) { name = "Exo2 Chrome" };
            mat.shader = fTitle.material.shader;
            mat.CopyPropertiesFromMaterial(fTitle.material);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.34f);
            mat.SetFloat("_FaceDilate", 0.18f);
            mat.SetColor("_OutlineColor", Hex("#3A1C08"));
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.6f));
            mat.SetFloat("_UnderlayOffsetX", 0.25f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlayDilate", 0.2f);
            mat.SetFloat("_UnderlaySoftness", 0.3f);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private delegate float AlphaFn(float x, float y);

        private static Sprite MakeSprite(string name, AlphaFn alpha, int border)
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            var px = new Color32[SpriteSize * SpriteSize];
            for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
                px[y * SpriteSize + x] = new Color32(255, 255, 255, ToByte(alpha(x + 0.5f, y + 0.5f)));
            tex.SetPixels32(px);
            return SaveSprite($"{SpriteDir}/{name}.png", tex, new Vector4(border, border, border, border));
        }

        private static Sprite MakeGlowLine()
        {
            const int w = 256, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f - h * 0.5f) / 3.2f;
                float a = Mathf.Exp(-v * v * 0.5f) * Mathf.Clamp01(1f - u * u);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            return SaveSprite($"{SpriteDir}/GlowLine.png", tex, Vector4.zero);
        }

        /// <summary>Gümüş küre topuz (renk dokuda pişmiş).</summary>
        private static Sprite MakeKnob()
        {
            var tex = new Texture2D(SpriteSize, SpriteSize, TextureFormat.RGBA32, false);
            float half = SpriteSize * 0.5f;
            Color light = Hex("#FFFFFF"), dark = Hex("#8F96A8");
            for (int y = 0; y < SpriteSize; y++)
            for (int x = 0; x < SpriteSize; x++)
            {
                float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((1f - r) * half);
                float t = Mathf.Clamp01(0.5f + 0.5f * (dy * 0.9f - dx * 0.3f));
                Color c = Color.Lerp(dark, light, t);
                float spec = Mathf.Exp(-((dx + 0.3f) * (dx + 0.3f) + (dy - 0.4f) * (dy - 0.4f)) * 8f) * 0.6f;
                c = Color.Lerp(c, Color.white, spec);
                c.a = a;
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{SpriteDir}/Knob.png", tex, Vector4.zero);
        }

        /// <summary>Koyu arka plan: dikey geçiş + bordo / lacivert yumuşak ışıklar.</summary>
        private static Sprite MakeBackground()
        {
            const int w = 279, h = 500;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color top = Hex("#14566E"), mid = Hex("#0D3A50"), bottom = Hex("#082434");   // tema "Efsane"
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = 1f - y / (h - 1f);
                Color c = v < 0.45f ? Color.Lerp(top, mid, v / 0.45f) : Color.Lerp(mid, bottom, (v - 0.45f) / 0.55f);
                c = AddGlow(c, u, v, 0.15f, 0.4f, 0.5f, Hex("#2AA3A8"), 0.45f);
                c = AddGlow(c, u, v, 0.85f, 0.2f, 0.45f, Hex("#C99A3A"), 0.35f);
                c = AddGlow(c, u, v, 0.8f, 0.68f, 0.4f, Hex("#1E7F8C"), 0.4f);
                tex.SetPixel(x, y, c);
            }
            return SaveSprite($"{SpriteDir}/Background.png", tex, Vector4.zero);
        }

        private static Color AddGlow(Color c, float u, float v, float cx, float cy, float radius, Color glow, float strength)
        {
            float dx = (u - cx) / radius, dy = (v - cy) / (radius * 0.56f);
            float k = Mathf.Exp(-(dx * dx + dy * dy) * 2f) * strength;
            return Color.Lerp(c, glow, k);
        }

        /// <summary>
        /// Referans görselden bölge keser (r: sol-üst köken, piksel). keyGrey: düz gri arka planı ve ince açıklama
        /// çizgilerini siler (tasarım sayfaları). Aksi halde kenarlar yumuşakça saydamlaşır, parlak kısımlar korunur.
        /// </summary>
        private static Sprite CropReference(string name, string file, RectInt r, bool keyGrey)
        {
            const int maxSize = 512;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(Path.Combine("docs/references", file)));
            int w = r.width, h = r.height;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = src.GetPixel(r.x + x, src.height - (r.y + r.height) + y);
            Object.DestroyImmediate(src);

            if (keyGrey)
            {
                Color bg = px[2 * w + 2]; // sol-alt köşe arka planı
                bool IsBg(Color c) => Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) < 0.09f;
                var bgMask = new bool[w * h];
                for (int i = 0; i < px.Length; i++) bgMask[i] = IsBg(px[i]);
                // İnce çizgiler: iki yanında (yatay ya da dikey) arka plan olan dar parçalar
                var line = new bool[w * h];
                const int reach = 6;
                for (int y = reach; y < h - reach; y++)
                for (int x = reach; x < w - reach; x++)
                {
                    int i = y * w + x;
                    if (bgMask[i]) continue;
                    bool horiz = false, vert = false;
                    for (int k = 1; k <= reach && !horiz; k++)
                        for (int j = 1; j <= reach; j++)
                            if (bgMask[i - k] && bgMask[i + j]) { horiz = true; break; }
                    for (int k = 1; k <= reach && !vert; k++)
                        for (int j = 1; j <= reach; j++)
                            if (bgMask[i - k * w] && bgMask[i + j * w]) { vert = true; break; }
                    line[i] = horiz || vert;
                }
                for (int i = 0; i < px.Length; i++)
                {
                    float d = Mathf.Abs(px[i].r - bg.r) + Mathf.Abs(px[i].g - bg.g) + Mathf.Abs(px[i].b - bg.b);
                    float a = Mathf.Clamp01((d - 0.05f) / 0.1f);
                    px[i].a = line[i] ? 0f : a;
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
                    float edge = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) / 0.35f);
                    Color c = px[y * w + x];
                    float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float a = Mathf.Max(edge * edge, Mathf.Clamp01((lum - 0.35f) / 0.4f) * Mathf.Clamp01(edge * 3f));
                    px[y * w + x].a = a;
                }
            }

            float scale = Mathf.Min(1f, maxSize / (float)Mathf.Max(w, h));
            int ow = Mathf.RoundToInt(w * scale), oh = Mathf.RoundToInt(h * scale);
            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                // kutu filtreli küçültme (alfa ağırlıklı)
                int x0 = (int)(x / scale), x1 = Mathf.Min(w, (int)((x + 1) / scale));
                int y0 = (int)(y / scale), y1 = Mathf.Min(h, (int)((y + 1) / scale));
                Color sum = Color.clear;
                float aSum = 0f;
                int n = 0;
                for (int yy = y0; yy < Mathf.Max(y1, y0 + 1); yy++)
                for (int xx = x0; xx < Mathf.Max(x1, x0 + 1); xx++)
                {
                    Color c = px[yy * w + xx];
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, 0f);
                    aSum += c.a;
                    n++;
                }
                tex.SetPixel(x, y, aSum > 0f ? new Color(sum.r / aSum, sum.g / aSum, sum.b / aSum, aSum / n) : Color.clear);
            }
            return SaveSprite($"{IconDir}/{name}.png", tex, Vector4.zero);
        }

        /// <summary>Referanstan düz kesim (saydamlık yok) — arka plan görselleri.</summary>
        private static Sprite CropPlain(string name, string file, RectInt r)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(Path.Combine("docs/references", file)));
            var tex = new Texture2D(r.width, r.height, TextureFormat.RGB24, false);
            tex.SetPixels(src.GetPixels(r.x, src.height - (r.y + r.height), r.width, r.height));
            Object.DestroyImmediate(src);
            return SaveSprite($"{SpriteDir}/{name}.png", tex, Vector4.zero);
        }

        // ---------------------------------------------------------------------
        // Nadirlik (rarity) rozetleri — docs/references/market
        // ---------------------------------------------------------------------

        public static Color RarityColor(Jumpline.Shop.Rarity r) => r switch
        {
            Jumpline.Shop.Rarity.Rare => Hex("#4F9CFF"),
            Jumpline.Shop.Rarity.Epic => Hex("#B45CFF"),
            Jumpline.Shop.Rarity.Legendary => Hex("#E8B95B"),
            Jumpline.Shop.Rarity.Gold => Hex("#F2C044"),
            _ => Hex("#A9AEBD"),
        };

        public static string RarityLabel(Jumpline.Shop.Rarity r) => r.ToString().ToUpperInvariant();

        /// <summary>Sağ üst köşe: şekil ikonu (elmas / üçgen / yıldız / taç) + altında küçük etiket.</summary>
        public static void RarityIcon(Box parent, Rect card, Jumpline.Shop.Rarity rarity)
        {
            Color c = RarityColor(rarity);
            var r = new Rect(card.xMax - 64f, card.y + 14f, 40f, 40f);
            Vector2[] shape = rarity switch
            {
                Jumpline.Shop.Rarity.Rare => new[] { new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) },
                Jumpline.Shop.Rarity.Epic => Star(),
                Jumpline.Shop.Rarity.Gold or Jumpline.Shop.Rarity.Legendary => new[]
                {
                    new Vector2(0f, 0.25f), new Vector2(0.25f, 0.55f), new Vector2(0.5f, 0f), new Vector2(0.75f, 0.55f),
                    new Vector2(1f, 0.25f), new Vector2(0.9f, 1f), new Vector2(0.1f, 1f),
                },
                _ => new[] { new Vector2(0.5f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f) },
            };
            if (rarity == Jumpline.Shop.Rarity.Rare || rarity == Jumpline.Shop.Rarity.Common)
                Poly(parent, "RarityFill", Expand(r, -9f), shape, c * new Color(1f, 1f, 1f, 0.55f));
            PolyStroke(parent, "RarityIcon", r, shape, c, 3f);
            Txt(parent, "RarityLabel", RarityLabel(rarity), new Rect(card.xMax - 120f, card.y + 58f, 104f, 24f), fBold, 16f, c);
        }

        /// <summary>Sağ üst köşe dolu etiket ("RARE", "EPIC", "LEGENDARY") — ekipman kartları.</summary>
        public static void RarityTag(Box parent, Rect card, Jumpline.Shop.Rarity rarity)
        {
            Color c = RarityColor(rarity);
            string label = RarityLabel(rarity);
            float w = 22f + label.Length * 13f;
            var r = new Rect(card.xMax - w - 12f, card.y + 12f, w, 34f);
            var tag = Panel(parent, "RarityTag", r, c * 0.75f, c, 7f, new Color(0, 0, 0, 0));
            tag.rt.GetComponent<Image>().raycastTarget = false;
            Txt(tag, "Label", label, r, fExtra, 18f, Color.white);
        }

        private static Vector2[] Star()
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = -Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = i % 2 == 0 ? 0.5f : 0.21f;
                pts[i] = new Vector2(0.5f + Mathf.Cos(a) * rad, 0.53f + Mathf.Sin(a) * rad);
            }
            return pts;
        }

        private static Sprite MakeSizedSprite(string name, int size, AlphaFn alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, ToByte(alpha(x + 0.5f, y + 0.5f)));
            tex.SetPixels32(px);
            return SaveSprite($"{SpriteDir}/{name}.png", tex, Vector4.zero);
        }

        /// <summary>Halka; bir yanı parlak, diğer yanına doğru sönen (döndükçe "yörünge" hissi verir).</summary>
        private static float ArcRingAlpha(float x, float y)
        {
            const float c = 128f, radius = 116f;
            float dx = x - c, dy = y - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float core = Mathf.Clamp01(1f - Mathf.Abs(r - radius) / 1.8f);
            float glow = Mathf.Exp(-(r - radius) * (r - radius) / (2f * 5f * 5f)) * 0.55f;
            float a = Mathf.Atan2(dy, dx);
            float arc = 0.1f + 0.9f * Mathf.Pow(0.5f + 0.5f * Mathf.Cos(a - 0.6f), 3f);
            return Mathf.Max(core, glow) * arc;
        }

        /// <summary>Devre kartı çizgileri: kenarlardan içe kıvrılan hatlar ve uç düğümleri; orta boş (yazı için).</summary>
        private static Sprite MakeCircuit()
        {
            const int w = 512, h = 256;
            Vector2[][] lines =
            {
                new[] { new Vector2(0f, 0.25f), new Vector2(0.17f, 0.25f), new Vector2(0.24f, 0.12f), new Vector2(0.42f, 0.12f) },
                new[] { new Vector2(0f, 0.72f), new Vector2(0.11f, 0.72f), new Vector2(0.18f, 0.86f), new Vector2(0.36f, 0.86f) },
                new[] { new Vector2(0.04f, 0.47f), new Vector2(0.2f, 0.47f) },
                new[] { new Vector2(1f, 0.3f), new Vector2(0.82f, 0.3f), new Vector2(0.75f, 0.15f), new Vector2(0.6f, 0.15f) },
                new[] { new Vector2(1f, 0.76f), new Vector2(0.86f, 0.76f), new Vector2(0.79f, 0.89f), new Vector2(0.63f, 0.89f) },
                new[] { new Vector2(0.96f, 0.55f), new Vector2(0.8f, 0.55f) },
            };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float a = 0f;
                foreach (Vector2[] line in lines)
                {
                    for (int i = 0; i < line.Length - 1; i++)
                    {
                        Vector2 s0 = Scale(line[i]), s1 = Scale(line[i + 1]);
                        a = Mathf.Max(a, Mathf.Clamp01(1.4f - SegmentDistance(p, s0, s1)));
                    }
                    float node = (p - Scale(line[line.Length - 1])).magnitude;
                    a = Mathf.Max(a, Mathf.Clamp01(4.5f - node) * (node > 2.2f ? 1f : 0.35f));
                }
                px[y * w + x] = new Color32(255, 255, 255, ToByte(a));
            }
            tex.SetPixels32(px);
            return SaveSprite($"{SpriteDir}/Circuit.png", tex, Vector4.zero);

            Vector2 Scale(Vector2 n) => new Vector2(n.x * w, (1f - n.y) * h);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Altıgen altın: altın çerçeve, bordo iç, ince iç çizgi (currency_v2 referansı).</summary>
        private static Sprite MakeCoin()
        {
            const int s = 256;
            const float c = 128f, outer = 112f, ring = 88f, line = 66f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color goldLight = Hex("#FFE08A"), goldDark = Hex("#C4862A"), edge = Hex("#6E3F0E");
            Color maroonTop = Hex("#7A2040"), maroonBottom = Hex("#4A1226"), lineColor = Hex("#D9A441");
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f - c, py = y + 0.5f - c;
                float d = HexDistance(px, py);
                float v = (py / outer + 1f) * 0.5f; // 0 alt, 1 üst
                Color col;
                if (d > outer) col = new Color(goldLight.r, goldLight.g, goldLight.b, Mathf.Exp(-(d - outer) * (d - outer) / 60f) * 0.45f);
                else if (d > outer - 3f) col = edge;
                else if (d > ring) col = Color.Lerp(goldDark, goldLight, v);
                else if (d > ring - 3f) col = edge;
                else col = Color.Lerp(maroonBottom, maroonTop, v);
                if (d < ring && Mathf.Abs(d - line) < 1.6f) col = Color.Lerp(col, lineColor, 0.85f);
                if (d <= outer) col.a = Mathf.Clamp01(outer + 0.5f - d) > 0 ? 1f : col.a;
                tex.SetPixel(x, y, col);
            }
            return SaveSprite($"{IconDir}/coin.png", tex, Vector4.zero);
        }

        private static float HexDistance(float x, float y)
        {
            // Sivri tepeli altıgen: kenar normalleri 0°, ±60°.
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            return Mathf.Max(ax, ax * 0.5f + ay * 0.866f);
        }

        /// <summary>Mavi kristal elmas: koyu iç, parlak mavi kenarlar ve faset çizgileri.</summary>
        private static Sprite MakeGem()
        {
            const int s = 256;
            const float c = 128f, hw = 74f, hh = 118f;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            Color edge = Hex("#7CC4FF"), fillTop = Hex("#1E3A7A"), fillBottom = Hex("#0E1A40"), facet = Hex("#4F9CFF");
            Vector2 left = new Vector2(-hw, 18f), right = new Vector2(hw, 18f), top = new Vector2(0f, hh), bottom = new Vector2(0f, -hh), mid = new Vector2(0f, 40f);
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float yy = p.y - 18f;
                float d = Mathf.Abs(p.x) / hw + (yy > 0 ? yy / (hh - 18f) : -yy / (hh + 18f));
                Color col;
                if (d > 1f)
                {
                    float g = Mathf.Exp(-(d - 1f) * (d - 1f) / 0.004f) * 0.6f;
                    col = new Color(edge.r, edge.g, edge.b, g);
                }
                else
                {
                    col = Color.Lerp(fillBottom, fillTop, (p.y + hh) / (2f * hh));
                    if (d > 0.93f) col = edge;
                    float f = Mathf.Min(SegmentDistance(p, left, mid), SegmentDistance(p, right, mid));
                    f = Mathf.Min(f, Mathf.Min(SegmentDistance(p, mid, bottom), SegmentDistance(p, mid, top)));
                    if (f < 1.6f) col = Color.Lerp(col, facet, 0.8f);
                    col.a = 1f;
                }
                tex.SetPixel(x, y, col);
            }
            return SaveSprite($"{IconDir}/gem.png", tex, Vector4.zero);
        }

        private static Sprite SaveSprite(string path, Texture2D tex, Vector4 border)
        {
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return ImportSprite(path, border);
        }

        private static Sprite ImportSprite(string path, Vector4 border = default)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static byte ToByte(float a) => (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);

        private static float RoundedDistance(float x, float y, float inset, float radius)
        {
            float half = SpriteSize * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - inset - radius);
            float qy = Mathf.Abs(y - half) - (half - inset - radius);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        private static float RoundedRectAlpha(float x, float y) => 0.5f - RoundedDistance(x, y, 0f, RoundedRadius);

        private static float RoundedOutlineAlpha(float x, float y)
        {
            const float stroke = 2.5f;
            float d = RoundedDistance(x, y, 0f, RoundedRadius);
            return Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(0.5f + d + stroke);
        }

        private static float GlowRectAlpha(float x, float y)
        {
            float d = RoundedDistance(x, y, 40f, 18f);
            return d <= 0f ? 1f : Mathf.Exp(-d * d / (2f * 13f * 13f));
        }

        private static float CircleAlpha(float x, float y)
        {
            float half = SpriteSize * 0.5f;
            return 0.5f - (new Vector2(x - half, y - half).magnitude - (half - 1f));
        }

        private static float RingGlowAlpha(float x, float y)
        {
            float half = SpriteSize * 0.5f;
            float r = new Vector2(x - half, y - half).magnitude;
            float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 38f) / 3f);
            float glow = Mathf.Exp(-(r - 38f) * (r - 38f) / (2f * 9f * 9f)) * 0.55f;
            return Mathf.Max(ring, glow);
        }

        private static float SoftCircleAlpha(float x, float y)
        {
            float half = SpriteSize * 0.5f;
            float r = new Vector2(x - half, y - half).magnitude / half;
            return Mathf.Exp(-r * r * 3f) * Mathf.Clamp01((1f - r) * 8f);
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            c.a = alpha;
            return c;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
