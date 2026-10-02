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
            public static readonly Color Bg = Hex("#1E1D23");
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
            var b = Add(parent, name, r);
            var img = b.go.AddComponent<Image>();
            Rounded(img, radius);
            b.go.AddComponent<UIGradient>().SetColors(bottom, top);
            if (border.a > 0f) Border(b, radius, border);
            return b;
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
            img.color = color;
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
            if (material != null) t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            if (localize && Localization.Has(text)) b.go.AddComponent<LocalizedText>().SetKey(text);
            return t;
        }

        public static void Chrome(TextMeshProUGUI t)
        {
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(Color.white, Color.white, Hex("#8E97AB"), Hex("#8E97AB"));
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
            var b = Panel(parent, name, r, Hex("#4D1B2D"), Hex("#29345A"), 26f, Hex("#6F80AE"));
            labelText = Txt(b, "Label", label, r, fExtra, size, C.TextLight, localize: localize);
            return MakeButton(b, b.rt.GetComponent<Image>());
        }

        /// <summary>Ayarlar tarzı üst başlık çubuğu: geri butonu + krom başlık. Geri butonunu döndürür.</summary>
        public static Button Header(Box parent, string title)
        {
            var header = Add(parent, "HeaderBar", new Rect(0f, 0f, 1116f, 200f));
            var poly = AddPolygon(header);
            poly.Configure(new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.86f),
                new Vector2(0.833f, 0.86f), new Vector2(0.81f, 1f), new Vector2(0f, 1f),
            });
            poly.color = Hex("#1F2A47");
            poly.raycastTarget = false;
            header.go.AddComponent<Mask>().showMaskGraphic = true;
            var glow = Img(header.rt, "MaroonGlow", softCircle, Hex("#5A1A30", 0.95f), false);
            Place(glow.rectTransform, header, new Rect(130f, -170f, 860f, 470f));
            glow.raycastTarget = false;

            var edge = Add(parent, "HeaderEdge", new Rect(0f, 0f, 1116f, 200f));
            var edgePoly = AddPolygon(edge);
            edgePoly.Configure(new[] { new Vector2(0f, 0.99f), new Vector2(0.81f, 0.99f), new Vector2(0.833f, 0.855f), new Vector2(1f, 0.855f) }, true, 4f);
            edgePoly.color = Hex("#7A2A40");
            edgePoly.raycastTarget = false;

            var back = Panel(parent, "BackButton", new Rect(40f, 42f, 123f, 123f), Hex("#4F1B2E"), Hex("#2E3A5E"), 18f, Hex("#7C8DB8"));
            Icon(back, "arrow-left", new Rect(57f, 59f, 89f, 89f), Color.white);
            Button backButton = MakeButton(back, back.rt.GetComponent<Image>());

            Chrome(Txt(parent, "Title", title, new Rect(190f, 40f, 740f, 130f), fTitle, title.Length > 9 ? 76f : 92f, Color.white, material: chromeMaterial));
            return backButton;
        }

        /// <summary>Başlıklı bölüm paneli (AUDIO / GRAPHICS / CONTROLS tarzı).</summary>
        public static Box Section(Box parent, string name, string title, Rect r)
        {
            var panel = Panel(parent, name + "Panel", r, Hex("#4A1B2C"), Hex("#212A44"), 28f, Hex("#7083B3"));
            var band = Add(panel, "HeaderBand", new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 90f));
            var bandImg = band.go.AddComponent<Image>();
            Rounded(bandImg, 26f);
            bandImg.raycastTarget = false;
            band.go.AddComponent<UIGradient>().SetColors(Hex("#2A3556", 0f), Hex("#2A3556", 0.6f));
            Txt(panel, "Title", title, new Rect(r.x, r.y + 12f, r.width, 72f), fExtra, 50f, Color.white);
            Line(panel, new Rect(r.x + 4f, r.y + 92f, r.width - 8f, 2f), Hex("#3B4A70"));
            GlowLine(panel, new Rect(r.center.x - 130f, r.y + 85f, 260f, 16f), C.Blue);
            return panel;
        }

        /// <summary>Koyu iz + kırmızı→mavi dolgu çubuğu. Dolgu sola yaslıdır (genişliği koddan değişir).</summary>
        public static RectTransform ProgressBar(Box parent, string name, Rect r, float fill)
        {
            var track = Panel(parent, name, r, C.NavyDark, C.NavyDark, r.height * 0.5f, Hex("#5D6A91"));
            var bar = Add(track, "Fill", new Rect(r.x + 3f, r.y + 3f, (r.width - 6f) * fill, r.height - 6f));
            var img = bar.go.AddComponent<Image>();
            Rounded(img, (r.height - 6f) * 0.5f);
            img.raycastTarget = false;
            bar.go.AddComponent<UIGradient>().SetColors(Hex("#7A2A45"), Hex("#6FC3F5"), true);
            PivotLeft(bar);
            return bar.rt;
        }

        public static Slider RefSlider(Box parent, string name, Rect track, float value)
        {
            var root = Add(parent, name, new Rect(track.x, track.y - 20f, track.width, track.height + 40f));

            var bg = Add(root, "Background", track);
            var bgImg = bg.go.AddComponent<Image>();
            Rounded(bgImg, track.height * 0.5f);
            bgImg.color = Hex("#1B2236");
            Border(bg, track.height * 0.5f, Hex("#5B6992"));

            var fillArea = Add(root, "Fill Area", track);
            var fill = Add(fillArea, "Fill", track);
            var fillImg = fill.go.AddComponent<Image>();
            Rounded(fillImg, track.height * 0.5f);
            fill.go.AddComponent<UIGradient>().SetColors(Hex("#3C1626"), Hex("#7E2A44"), true);
            fill.rt.anchorMin = Vector2.zero;
            fill.rt.anchorMax = new Vector2(0f, 1f);
            fill.rt.sizeDelta = Vector2.zero;
            fill.rt.anchoredPosition = Vector2.zero;

            var handleArea = Add(root, "Handle Slide Area", track);
            var handle = Add(handleArea, "Handle", new Rect(track.x, track.y - 16f, 68f, 68f));
            handle.rt.anchorMin = new Vector2(0f, 0.5f);
            handle.rt.anchorMax = new Vector2(0f, 0.5f);
            handle.rt.anchoredPosition = Vector2.zero;
            var ring = Img(handle.rt, "RingGlow", ringGlow, Hex("#7AA6FF", 0.95f), false);
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
            Rounded(img, r.height * 0.5f);
            root.go.AddComponent<UIGradient>().SetColors(Hex("#4A1A2E"), Hex("#2A1C30"), true);
            Border(root, r.height * 0.5f, Hex("#6E7FAE"));
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
            SetFloat(sw, "offX", -travel);
            SetFloat(sw, "onX", travel);
            k.rectTransform.anchoredPosition = new Vector2(on ? travel : -travel, 0f);
            return sw;
        }

        public static void Logo(Box parent, string subtitle)
        {
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
            var root = Add(parent, name, r);
            Ring(root, new Rect(r.x - 30f, r.y - 70f, r.width * 0.6f, r.height + 140f), Pink * new Color(1, 1, 1, 0.85f), -14f, 6f);
            Ring(root, new Rect(r.xMax - r.width * 0.62f + 30f, r.y - 80f, r.width * 0.62f, r.height + 160f), Cyan * new Color(1, 1, 1, 0.85f), 18f, -5f, 0.5f);

            const float slant = 0.14f;
            Poly(root, "Glass", r, Parallelogram(slant), Hex("#AFC3FF", 0.07f));
            PolyStroke(root, "GlassEdge", r, Parallelogram(slant), Hex("#C8D6FF", 0.45f), 2f);

            var ri = new Rect(r.x + r.width * 0.07f, r.y + r.height * 0.14f, r.width * 0.86f, r.height * 0.72f);
            UIPolygon inner = Poly(root, "Inner", ri, Parallelogram(slant * 0.9f), Color.white);
            inner.raycastTarget = true;
            inner.gameObject.AddComponent<UIGradient>().SetColors(Hex("#46142A"), Hex("#7A1F44"));
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var innerBox = new Box(inner.rectTransform, ri);
            var core = Img(inner.rectTransform, "CoreGlow", softCircle, Hex("#FF5C93", 0.45f), false);
            Place(core.rectTransform, innerBox, new Rect(ri.center.x - ri.width * 0.4f, ri.y - ri.height * 0.3f, ri.width * 0.8f, ri.height * 1.6f));
            core.raycastTarget = false;
            Pulse(core, 0.35f, 4f, 0f);
            var lines = Img(inner.rectTransform, "Circuit", circuit, Hex("#7FB8FF", 0.6f), false);
            Stretch(lines.rectTransform);
            lines.raycastTarget = false;
            Pulse(lines, 0.3f, 5f, 0.3f);
            PolyStroke(root, "InnerEdge", ri, Parallelogram(slant * 0.9f), Hex("#FF7FA8", 0.8f), 2.5f);
            GlowLine(root, new Rect(ri.x + ri.width * 0.25f, ri.y - 8f, ri.width * 0.5f, 16f), Pink);
            GlowLine(root, new Rect(ri.x + ri.width * 0.2f, ri.yMax - 8f, ri.width * 0.6f, 16f), Cyan);

            Shadow(Txt(root, "Label", label, ri, fExtra, size, Color.white));
            UIPolygon flash = Poly(root, "PressFlash", ri, Parallelogram(slant * 0.9f), new Color(1f, 1f, 1f, 0f));
            return WithFeedback(root, inner, flash);
        }

        /// <summary>QUICK RACE / DAILY CHALLENGE etiket butonu (mavi ya da bordo).</summary>
        public static Button TagButton(Box parent, string name, Rect r, string label, bool blue, float size = 38f)
        {
            var root = Add(parent, name, r);
            Color accent = blue ? Cyan : Pink;
            Ring(root, blue
                    ? new Rect(r.x - 40f, r.y - 30f, r.width * 0.55f, r.height + 80f)
                    : new Rect(r.xMax - r.width * 0.55f + 40f, r.y - 30f, r.width * 0.55f, r.height + 80f),
                (blue ? Pink : Cyan) * new Color(1, 1, 1, 0.7f), blue ? -12f : 14f, blue ? 7f : -7f, blue ? 0.2f : 0.7f);

            const float slant = 0.1f;
            var glass = new Rect(r.x - 12f, r.y - 12f, r.width + 24f, r.height + 24f);
            Poly(root, "Glass", glass, Parallelogram(slant), Hex("#AFC3FF", 0.05f));
            PolyStroke(root, "GlassEdge", glass, Parallelogram(slant), Hex("#C8D6FF", 0.32f), 1.6f);

            UIPolygon inner = Poly(root, "Inner", r, Parallelogram(slant), Color.white);
            inner.raycastTarget = true;
            inner.gameObject.AddComponent<UIGradient>().SetColors(blue ? Hex("#151B33") : Hex("#2E1222"), blue ? Hex("#26345E") : Hex("#5E1C36"));
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var lines = Img(inner.rectTransform, "Circuit", circuit, accent * new Color(1, 1, 1, 0.35f), false);
            Stretch(lines.rectTransform);
            lines.raycastTarget = false;
            PolyStroke(root, "InnerEdge", r, Parallelogram(slant), accent * new Color(1, 1, 1, 0.7f), 2f);
            var top = Add(root, "TopAccent", new Rect(r.x + r.width * 0.3f, r.y - 7f, r.width * 0.45f, 14f));
            var topImg = top.go.AddComponent<Image>();
            topImg.sprite = glowLine;
            topImg.color = accent;
            topImg.raycastTarget = false;
            Pulse(topImg, 0.35f, 3.5f, blue ? 0f : 0.5f);
            GlowLine(root, new Rect(r.x + r.width * 0.2f, r.yMax - 7f, r.width * 0.45f, 14f), accent * new Color(1, 1, 1, 0.8f));

            Shadow(Txt(root, "Label", label, r, fExtra, size, Hex("#DDE2EE")));
            UIPolygon flash = Poly(root, "PressFlash", r, Parallelogram(slant), new Color(1f, 1f, 1f, 0f));
            return WithFeedback(root, inner, flash);
        }

        /// <summary>Alt gezinme kutusu: dosya biçimi, devre çizgisi, ikon, ışık halkası.</summary>
        public static Button FolderTile(Box parent, string name, Rect r, string icon, string label, int index)
        {
            var root = Add(parent, name, r);
            bool blue = index % 2 == 0;
            Ring(root, new Rect(r.x - 42f, r.y + r.height * 0.32f, r.width + 84f, r.height * 0.82f),
                (blue ? Cyan : Pink) * new Color(1, 1, 1, 0.75f), -8f, blue ? 9f : -9f, index * 0.17f);

            UIPolygon tile = Poly(root, "Tile", r, FolderShape, Color.white);
            tile.raycastTarget = true;
            tile.gameObject.AddComponent<UIGradient>().SetColors(Hex("#4E1832"), Hex("#232C4C"));
            tile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var lines = Img(tile.rectTransform, "Circuit", circuit, Hex("#8FA8D8", 0.22f), false);
            lines.rectTransform.anchorMin = Vector2.zero;
            lines.rectTransform.anchorMax = Vector2.one;
            lines.rectTransform.offsetMin = new Vector2(-r.width * 0.6f, 0f);
            lines.rectTransform.offsetMax = new Vector2(r.width * 0.6f, 0f);
            lines.raycastTarget = false;
            PolyStroke(root, "Edge", r, FolderShape, Hex("#9AABDB", 0.85f), 2.2f);

            Icon(root, icon, new Rect(r.center.x - 44f, r.center.y - 38f, 88f, 88f), Color.white);
            UIPolygon flash = Poly(root, "PressFlash", r, FolderShape, new Color(1f, 1f, 1f, 0f));
            Txt(parent, name + "Label", label, new Rect(r.center.x - 115f, r.yMax + 12f, 230f, 40f), fBold, 23f, C.TextGrey);
            return WithFeedback(root, tile, flash);
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
            foreach (string n in new[] { "shopping-cart", "calendar-star", "backpack", "helmet", "settings", "arrow-left", "ban", "player-pause", "check" })
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
            Icons["gear_boots"] = CropReference("gear_boots", "character/boots.jpg", new RectInt(110, 600, 1000, 810), true);

            fMedium = GetOrCreateFont("Montserrat-Medium");
            fBold = GetOrCreateFont("Montserrat-Bold");
            fExtra = GetOrCreateFont("Montserrat-ExtraBold");
            fTitle = GetOrCreateFont("Exo2-BlackItalic");
            chromeMaterial = GetOrCreateChromeMaterial();
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
            asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024);
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
            mat.CopyPropertiesFromMaterial(fTitle.material);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.12f);
            mat.SetColor("_OutlineColor", Hex("#11141F"));
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
            Color top = Hex("#20263A"), mid = Hex("#241D2B"), bottom = Hex("#1B1A20");
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (w - 1f), v = 1f - y / (h - 1f);
                Color c = v < 0.45f ? Color.Lerp(top, mid, v / 0.45f) : Color.Lerp(mid, bottom, (v - 0.45f) / 0.55f);
                c = AddGlow(c, u, v, 0.12f, 0.45f, 0.5f, Hex("#5A1F33"), 0.55f);
                c = AddGlow(c, u, v, 0.88f, 0.22f, 0.45f, Hex("#2A3B66"), 0.5f);
                c = AddGlow(c, u, v, 0.85f, 0.62f, 0.4f, Hex("#4A1A2C"), 0.45f);
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
