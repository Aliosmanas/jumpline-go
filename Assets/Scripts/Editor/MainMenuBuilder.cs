using System.Collections.Generic;
using Jumpline.Core;
using Jumpline.Player;
using Jumpline.Shop;
using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;
using SafeArea = Jumpline.UI.SafeArea;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Ana menü sahnesini kurar. Home / Character / Market / Settings referans görsellerden
    /// (docs/references/menu) birebir; Events / Inventory / Upgrade / Customize / Language / Help / Legal
    /// aynı stille. Karakter görselleri bilerek boş. Menü: Jumpline → Build Main Menu.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainMenu/MainMenu.unity";
        public const string ConfigPath = "Assets/Settings/GameConfig.asset";
        private const string CatalogPath = "Assets/Settings/ShopCatalog.asset";
        private const string StatsPath = "Assets/Settings/CharacterStats.asset";

        private static readonly string[] SkinIds = { "rookie", "stealth", "velocity", "titanium", "glitch", "crimson", "frost", "blaze" };
        private static readonly string[] BoostIds = { "speed_boost", "shield", "magnet", "double_gold" };
        private static readonly string[] CurrencyIds = { "gold_small", "gold_medium", "gold_large" };

        private sealed class Screen
        {
            public MenuScreen screen;
            public Box top, mid, bot;
        }

        private static GameConfig config;
        private static ShopCatalog catalog;
        private static CharacterStats stats;
        private static RenderTexture preview;
        private static Dictionary<string, Sprite> thumbs = new Dictionary<string, Sprite>();

        [MenuItem("Jumpline/Build Main Menu")]
        public static void Build()
        {
            EnsureFolder(SpriteDir);
            EnsureFolder("Assets/Scenes/MainMenu");

            // NewScene kullanılmayan asset'leri boşaltır; her şeyi sahne açıldıktan sonra yükle.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LoadAssets();
            LoadData();
            var skins = RunnerStageBuilder.EnsureLibrary();
            RunnerStageBuilder.ConfigureImport();
            preview = RunnerStageBuilder.BuildStage(skins, out _, out var runner, out var stageCamera);
            thumbs = RunnerStageBuilder.RenderThumbnails(skins, runner, stageCamera);

            BuildCameraAndEvents(true);
            Transform canvas = BuildCanvas("MainMenuCanvas", true);
            var safe = NewUI("SafeArea", canvas);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var navigator = canvas.gameObject.AddComponent<MenuNavigator>();

            BuildHome(NewScreen(safe, "home", true));
            BuildCharacter(NewScreen(safe, "character", true));
            BuildMarket(NewScreen(safe, "market", true));
            BuildSettings(NewScreen(safe, "settings", false));
            BuildEvents(NewScreen(safe, "events", true));
            BuildInventory(NewScreen(safe, "inventory", true));
            BuildUpgrade(NewScreen(safe, "upgrade", false));
            BuildCustomize(NewScreen(safe, "customize", false));
            BuildLanguage(NewScreen(safe, "language", false));
            BuildHelp(NewScreen(safe, "help", false));
            BuildLegal(NewScreen(safe, "legal", false));

            GameObject nav = BuildBottomNav(safe);
            Toast toast = BuildToast(safe);
            Set(navigator, "bottomNav", nav);
            Set(navigator, "toast", toast);

            EditorSceneManager.SaveScene(scene, ScenePath);
            ScenesSetup.ApplyBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jumpline] Ana menü üretildi: {ScenePath}");
        }

        private static Screen NewScreen(Transform parent, string id, bool bottomNav)
        {
            var rt = NewUI(char.ToUpper(id[0]) + id.Substring(1) + "Screen", parent);
            Stretch(rt);
            var screen = rt.gameObject.AddComponent<MenuScreen>();
            screen.Configure(id, bottomNav);
            return new Screen
            {
                screen = screen,
                top = Group(rt, "Top", 1f),
                mid = Group(rt, "Middle", 0.5f),
                bot = Group(rt, "Bottom", 0f),
            };
        }

        private static void Link(Button b, MenuLink.LinkAction action, string target = null) =>
            b.gameObject.AddComponent<MenuLink>().Configure(action, target);

        private static void Starts(Button b, RunMode mode) =>
            b.gameObject.AddComponent<LevelStartButton>().Configure(mode, config);

        // ---------------------------------------------------------------------
        // HOME
        // ---------------------------------------------------------------------

        private static void BuildHome(Screen s)
        {
            // docs/references/menu/home_v2.jpg
            RawPreview(s.top, "RunnerPreview", new Rect(108f, 330f, 900f, 900f), preview);
            Logo(s.top, "SEASON 4: VELOCITY");
            Starts(GlassPlayButton(s.bot, "PlayButton", new Rect(118f, 1215f, 880f, 250f), "PLAY", 120f), RunMode.Campaign);
            Starts(TagButton(s.bot, "QuickRaceButton", new Rect(70f, 1535f, 470f, 118f), "QUICK RACE", true), RunMode.QuickRace);
            Starts(TagButton(s.bot, "DailyChallengeButton", new Rect(576f, 1535f, 470f, 118f), "DAILY CHALLENGE", false, 33f), RunMode.Daily);
        }

        // ---------------------------------------------------------------------
        // CHARACTER / PROFILE
        // ---------------------------------------------------------------------

        private static void BuildCharacter(Screen s)
        {
            RawPreview(s.top, "RunnerPreview", new Rect(320f, 460f, 540f, 900f), preview);
            Logo(s.top, "CHARACTER / PROFILE");

            Txt(s.top, "StatsHeader", "STATS", new Rect(40f, 420f, 288f, 56f), fBold, 34f, Hex("#C9CCD6"));
            Line(s.top, new Rect(40f, 479f, 288f, 2f), Hex("#4E546B"));
            var bars = new List<Dictionary<string, object>>
            {
                StatRow(s.top, "speed", "SPEED", 520f),
                StatRow(s.top, "jump", "JUMP", 649f),
                StatRow(s.top, "momentum", "MOMENTUM", 802f),
            };

            Txt(s.top, "GearHeader", "GEAR", new Rect(862f, 420f, 213f, 56f), fBold, 34f, Hex("#C9CCD6"));
            Line(s.top, new Rect(862f, 479f, 213f, 2f), Hex("#4E546B"));
            Panel(s.top, "GearPanel", new Rect(862f, 497f, 213f, 871f), Hex("#1E2438", 0.75f), Hex("#2A1A28", 0.75f), 18f, Hex("#5D6B93"));
            TMP_Text suit = GearSlot(s.top, "Suit", 523f, "SUIT:", "ROOKIE", false);
            GearSlot(s.top, "Boots", 742f, "BOOTS:", "GRAV-THRUSTERS", false);
            GearSlot(s.top, "Gloves", 950f, "GLOVES:", "GRIP-TECH", false);
            GearSlot(s.top, "Accessory", 1160f, "ACCESSORY:", "NONE", true);

            Link(PrimaryButton(s.bot, "CustomizeButton", new Rect(262f, 1362f, 590f, 180f), new Rect(285f, 1380f, 545f, 142f), "CUSTOMIZE", 80f), MenuLink.LinkAction.Open, "customize");
            Link(PillButton(s.bot, "UpgradeButton", new Rect(378f, 1572f, 360f, 68f), "UPGRADE", 30f, Hex("#C3C8D6"), 1f), MenuLink.LinkAction.Open, "upgrade");
            Link(PillButton(s.bot, "BackButton", new Rect(765f, 1572f, 308f, 68f), "BACK", 30f, Hex("#C3C8D6"), 1f), MenuLink.LinkAction.Back);

            var view = s.screen.gameObject.AddComponent<CharacterView>();
            Set(view, "stats", stats);
            Set(view, "catalog", catalog);
            Set(view, "suitName", suit);
            SetStructArray(view, "bars", bars);
        }

        private static Dictionary<string, object> StatRow(Box parent, string id, string label, float y)
        {
            Txt(parent, id + "Label", label, new Rect(45f, y, 230f, 40f), fBold, 28f, Hex("#E2E4EA"), TextAlignmentOptions.Left);
            var value = Txt(parent, id + "Value", "0", new Rect(225f, y, 100f, 40f), fBold, 28f, Hex("#E2E4EA"), TextAlignmentOptions.Right, localize: false);
            RectTransform fill = ProgressBar(parent, id + "Bar", new Rect(42f, y + 43f, 286f, 24f), 0.5f);
            var tier = Txt(parent, id + "Tier", "", new Rect(45f, y + 70f, 280f, 36f), fMedium, 26f, Hex("#B5B9C6"), TextAlignmentOptions.Left, localize: false);
            return new Dictionary<string, object> { { "statId", id }, { "value", value }, { "tier", tier }, { "fill", fill }, { "fullWidth", 280f } };
        }

        private static TMP_Text GearSlot(Box parent, string name, float y, string kind, string item, bool empty)
        {
            var slot = Panel(parent, name + "Slot", new Rect(901f, y, 134f, 134f), Hex("#5A1D33"), Hex("#2F3A5E"), 18f, Hex("#8193C0"));
            if (empty) Icon(slot, "ban", new Rect(933f, y + 32f, 70f, 70f), Hex("#8D7480", 0.8f));
            Color c = empty ? Hex("#9A9FAE") : Hex("#D9DCE3");
            Txt(parent, name + "Kind", kind, new Rect(868f, y + 142f, 200f, 26f), fBold, 18f, c);
            return Txt(parent, name + "Item", item, new Rect(862f, y + 165f, 213f, 26f), fBold, 18f, c, localize: name != "Suit");
        }

        // ---------------------------------------------------------------------
        // MARKET
        // ---------------------------------------------------------------------

        private static void BuildMarket(Screen s)
        {
            var cards = new List<ShopItemCard>();

            var bar = Add(s.top, "CurrencyBar", new Rect(0f, 0f, 1116f, 212f));
            Fill(bar, Hex("#1C2133"), Hex("#232A40"));
            Panel(s.top, "GoldBox", new Rect(63f, 66f, 464f, 87f), Hex("#1B2236"), Hex("#232C46"), 26f, Hex("#4F5E86"));
            var gold = Txt(s.top, "GoldText", "GOLD: 5000", new Rect(80f, 76f, 350f, 66f), fExtra, 42f, C.Gold, TextAlignmentOptions.Right, localize: false);
            Icon(s.top, "coin", new Rect(442f, 76f, 66f, 66f), Color.white);
            Panel(s.top, "GemsBox", new Rect(589f, 66f, 464f, 87f), Hex("#1B2236"), Hex("#232C46"), 26f, Hex("#4F5E86"));
            var gems = Txt(s.top, "GemsText", "GEMS: 250", new Rect(600f, 76f, 350f, 66f), fExtra, 42f, C.GemText, TextAlignmentOptions.Right, localize: false);
            Icon(s.top, "gem", new Rect(966f, 74f, 68f, 68f), Color.white);

            var strip = Add(s.top, "TabStrip", new Rect(0f, 212f, 1116f, 128f));
            Fill(strip, Hex("#1F2840"), Hex("#253049"));
            Line(s.top, new Rect(0f, 212f, 1116f, 2f), Hex("#3B4A6E"));
            Line(s.top, new Rect(0f, 338f, 1116f, 2f), Hex("#3B4A6E"));
            SegmentedControl tabs = Tabs(s.top);

            // Skins sekmesi: referanstaki düzen
            var skins = Add(s.top, "SkinsTab", Full);
            var band = Add(skins, "DealsBand", new Rect(0f, 380f, 1116f, 820f));
            Fill(band, Hex("#5C1C2E", 0f), Hex("#5C1C2E", 0.95f));
            Line(skins, new Rect(0f, 380f, 1116f, 2f), Hex("#6E2338"));
            Txt(skins, "DealsTitle", "DAILY DEALS", new Rect(258f, 425f, 600f, 70f), fExtra, 50f, Color.white);
            DealsScroller(skins, cards);
            float[] xs = { 63f, 318f, 572f, 827f };
            float[] ys = { 1190f, 1560f };
            int n = 0;
            foreach (float y in ys)
            foreach (float x in xs)
            {
                cards.Add(GridCard(skins, SkinIds[n], new Rect(x, y, 227f, 342f), false));
                n++;
            }

            // Boosts / Currency sekmeleri
            var boosts = Add(s.top, "BoostsTab", Full);
            OfferGrid(boosts, BoostIds, cards);
            var currency = Add(s.top, "CurrencyTab", Full);
            OfferGrid(currency, CurrencyIds, cards);

            var view = s.screen.gameObject.AddComponent<MarketView>();
            Set(view, "catalog", catalog);
            Set(view, "goldText", gold);
            Set(view, "gemsText", gems);
            Set(view, "tabs", tabs);
            SetArray(view, "tabPanels", new Object[] { skins.go, boosts.go, currency.go });
            SetArray(view, "cards", cards.ToArray());
        }

        private static SegmentedControl Tabs(Box parent)
        {
            string[] names = { "SKINS", "BOOSTS", "CURRENCY" };
            float[] xs = { 63f, 405f, 748f };
            var root = Add(parent, "Tabs", new Rect(63f, 236f, 990f, 78f));
            var buttons = new Button[3];
            var selected = new GameObject[3];
            var labels = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(xs[i], 236f, 305f, 78f);
                var tab = Panel(root, names[i] + "Tab", r, Hex("#1C2338"), Hex("#232C46"), 22f, Hex("#4C5A80"));
                var sel = Add(tab, "Selected", r);
                Glow(sel, Expand(r, 26f), Hex("#5AA8FF", 0.35f));
                var selFill = Panel(sel, "Fill", r, Hex("#223463"), Hex("#2E4478"), 22f, Hex("#8CC0FF"));
                selFill.rt.GetComponent<Image>().raycastTarget = false;
                GlowLine(sel, new Rect(r.x + 50f, r.yMax - 9f, r.width - 100f, 16f), Hex("#7FB8FF"));
                selected[i] = sel.go;
                labels[i] = Txt(tab, "Label", names[i], r, fExtra, 36f, Color.white);
                buttons[i] = MakeButton(tab, tab.rt.GetComponent<Image>());
            }
            var control = root.go.AddComponent<SegmentedControl>();
            SetArray(control, "segments", buttons);
            SetArray(control, "selectedVisuals", selected);
            SetArray(control, "labels", labels);
            SetColor(control, "normalLabelColor", Hex("#A7AEC2"));
            SetColor(control, "selectedLabelColor", Color.white);
            control.SetWithoutNotify(0);
            return control;
        }

        private static void DealsScroller(Box parent, List<ShopItemCard> cards)
        {
            var viewport = Add(parent, "DealsViewport", new Rect(0f, 505f, 1116f, 630f));
            viewport.go.AddComponent<RectMask2D>();
            viewport.go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // sürükleme için raycast hedefi

            var content = Add(viewport, "Content", new Rect(0f, 505f, 1310f, 630f));
            content.rt.anchorMin = content.rt.anchorMax = new Vector2(0f, 0.5f);
            content.rt.pivot = new Vector2(0f, 0.5f);
            content.rt.anchoredPosition = Vector2.zero;

            var scroll = viewport.go.AddComponent<ScrollRect>();
            scroll.content = content.rt;
            scroll.viewport = viewport.rt;
            scroll.horizontal = true;
            scroll.vertical = false;

            cards.Add(DealCard(content, 1, "stealth",new Rect(63f, 520f, 402f, 595f), true, 905f, 957f, new Rect(134f, 1007f, 260f, 70f)));
            cards.Add(DealCard(content, 2, "speed_boost", new Rect(497f, 545f, 355f, 528f), false, 888f, 936f, new Rect(560f, 975f, 230f, 65f)));
            cards.Add(DealCard(content, 3, "titanium",new Rect(886f, 545f, 355f, 528f), false, 888f, 936f, new Rect(948f, 975f, 230f, 65f)));
        }

        private static ShopItemCard DealCard(Box parent, int number, string itemId, Rect r, bool highlighted, float titleY, float priceY, Rect buyRect)
        {
            ShopItem item = catalog.Find(itemId);
            bool gold = item.currency == CurrencyType.Gold;
            var root = Add(parent, $"DealCard_{number}", r);
            if (highlighted) Glow(root, Expand(r, 30f), Hex("#5AA8FF", 0.4f));
            Panel(root, "Card", r, Hex("#5B1C30"), Hex("#263257"), 24f, highlighted ? Hex("#A9CCFF") : Hex("#8095C4"));
            var tab = Panel(root, "NumberTab", new Rect(r.x, r.y, 80f, 78f), Hex("#2A3556"), Hex("#3A4A72"), 18f, highlighted ? Hex("#A9CCFF") : Hex("#8095C4"));
            Txt(tab, "Number", number.ToString(), new Rect(r.x + 4f, r.y + 6f, 72f, 66f), fExtra, 44f, Color.white, localize: false);
            Thumb(root, itemId, new Rect(r.x + 30f, r.y + 50f, r.width - 60f, titleY - 30f - (r.y + 50f)));
            var title = Txt(root, "Title", item.displayName, new Rect(r.x, titleY - 22f, r.width, 44f), fBold, 34f, Color.white, localize: false);
            var price = Txt(root, "Price", "", new Rect(r.x, priceY - 24f, r.width, 48f), fExtra, 36f, gold ? C.Gold : C.Price, localize: false);
            Button buy = PillButton(root, "BuyButton", buyRect, "BUY", 32f, Color.white, 1f, out TextMeshProUGUI buyLabel,
                gold ? Hex("#E2B45A") : Hex("#6EA6FF"), gold ? Hex("#F2C366") : C.Blue, false);
            return Card(root, itemId, buy, title, price, buyLabel, null);
        }

        /// <summary>Kostüm ızgarası kartı (görsel alanı boş): alt kısımda ad ve durum.</summary>
        private static ShopItemCard GridCard(Box parent, string itemId, Rect r, bool withMark)
        {
            ShopItem item = catalog.Find(itemId);
            var root = Add(parent, $"Card_{itemId}", r);
            var face = Panel(root, "Face", r, Hex("#5A1C30"), Hex("#273256"), 22f, Hex("#7184B0"));
            Thumb(root, itemId, new Rect(r.x + 8f, r.y + 8f, r.width - 16f, r.height - 100f));
            var title = Txt(root, "Title", item.displayName, new Rect(r.x + 6f, r.yMax - 92f, r.width - 12f, 40f), fBold, 26f, Color.white, localize: false);
            var status = Txt(root, "Status", "", new Rect(r.x + 6f, r.yMax - 52f, r.width - 12f, 36f), fExtra, 24f, C.Price, localize: false);
            foreach (var t in new[] { title, status })
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 14f;
                t.fontSizeMax = t.fontSize;
            }
            GameObject mark = null;
            if (withMark)
            {
                var m = Add(root, "EquippedMark", r);
                Border(m, 22f, Hex("#A9CCFF"));
                Border(m, 22f, Hex("#A9CCFF")); // kalın çerçeve
                Icon(m, "check", new Rect(r.xMax - 60f, r.y + 14f, 44f, 44f), Hex("#A9CCFF"));
                mark = m.go;
            }
            Button button = MakeButton(root, face.rt.GetComponent<Image>());
            return Card(root, itemId, button, title, status, null, mark);
        }

        /// <summary>Kostümün 3B vitrinden çekilmiş görseli (varsa).</summary>
        private static void Thumb(Box parent, string itemId, Rect r)
        {
            if (!thumbs.TryGetValue(itemId, out Sprite sprite) || sprite == null) return;
            var b = Add(parent, "Thumb", r);
            var img = b.go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        /// <summary>Güç / para paketi kartları: 2x2 ızgara.</summary>
        private static void OfferGrid(Box parent, string[] ids, List<ShopItemCard> cards)
        {
            Rect[] slots =
            {
                new Rect(63f, 420f, 480f, 600f), new Rect(573f, 420f, 480f, 600f),
                new Rect(63f, 1060f, 480f, 600f), new Rect(573f, 1060f, 480f, 600f),
            };
            for (int i = 0; i < ids.Length; i++)
            {
                Rect r = slots[i];
                ShopItem item = catalog.Find(ids[i]);
                bool gold = item.currency == CurrencyType.Gold;
                var root = Add(parent, $"Offer_{ids[i]}", r);
                Panel(root, "Card", r, Hex("#5B1C30"), Hex("#263257"), 24f, Hex("#8095C4"));
                string art = item.category == ItemCategory.Currency ? "coin" : item.currency == CurrencyType.Gems ? "gem" : "coin";
                Icon(root, art, new Rect(r.center.x - 110f, r.y + 70f, 220f, 220f), Color.white);
                var title = Txt(root, "Title", item.displayName, new Rect(r.x, r.y + 330f, r.width, 50f), fBold, 36f, Color.white, localize: false);
                var price = Txt(root, "Price", "", new Rect(r.x, r.y + 390f, r.width, 50f), fExtra, 36f, gold ? C.Gold : C.Price, localize: false);
                Button buy = PillButton(root, "BuyButton", new Rect(r.center.x - 140f, r.y + 470f, 280f, 72f), "BUY", 32f, Color.white, 1f,
                    out TextMeshProUGUI buyLabel, gold ? Hex("#E2B45A") : Hex("#6EA6FF"), gold ? Hex("#F2C366") : C.Blue, false);
                cards.Add(Card(root, ids[i], buy, title, price, buyLabel, null));
            }
        }

        private static ShopItemCard Card(Box root, string itemId, Button button, TMP_Text title, TMP_Text status, TMP_Text action,
            GameObject equippedMark)
        {
            var card = root.go.AddComponent<ShopItemCard>();
            SetString(card, "itemId", itemId);
            Set(card, "button", button);
            Set(card, "title", title);
            Set(card, "status", status);
            Set(card, "action", action);
            Set(card, "equippedMark", equippedMark);
            if (equippedMark != null) equippedMark.SetActive(false);
            return card;
        }

        // ---------------------------------------------------------------------
        // SETTINGS
        // ---------------------------------------------------------------------

        private static void BuildSettings(Screen s)
        {
            var bg = Img(s.screen.transform, "SolidBackground", null, C.Bg, false);
            Stretch(bg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            bg.transform.SetAsFirstSibling();

            Link(Header(s.top, "SETTINGS"), MenuLink.LinkAction.Back);

            var audio = Section(s.top, "Audio", "AUDIO", new Rect(63f, 285f, 990f, 460f));
            Txt(audio, "MusicLabel", "Music", new Rect(123f, 430f, 210f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider music = RefSlider(audio, "MusicSlider", new Rect(340f, 438f, 653f, 36f), 0.61f);
            Txt(audio, "SfxLabel", "SFX", new Rect(123f, 533f, 210f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider sfx = RefSlider(audio, "SfxSlider", new Rect(340f, 541f, 653f, 36f), 0.61f);
            Txt(audio, "MuteLabel", "Mute", new Rect(123f, 635f, 210f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle mute = RefSwitch(audio, "MuteToggle", new Rect(860f, 628f, 133f, 68f), false);

            var graphics = Section(s.top, "Graphics", "GRAPHICS", new Rect(63f, 798f, 990f, 279f));
            SegmentedControl quality = QualitySelector(graphics, new Rect(123f, 935f, 870f, 95f));

            var controls = Section(s.top, "Controls", "CONTROLS", new Rect(63f, 1142f, 990f, 380f));
            Txt(controls, "SensitivityLabel", "Sensitivity", new Rect(123f, 1296f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider sensitivity = RefSlider(controls, "SensitivitySlider", new Rect(388f, 1305f, 605f, 36f), 0.54f);
            Txt(controls, "VibrationLabel", "Vibration", new Rect(123f, 1406f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle vibration = RefSwitch(controls, "VibrationToggle", new Rect(860f, 1400f, 133f, 68f), true);

            Link(BoxButton(s.bot, "LanguageButton", new Rect(63f, 1766f, 307f, 106f), "LANGUAGE"), MenuLink.LinkAction.Open, "language");
            Link(BoxButton(s.bot, "HelpButton", new Rect(405f, 1766f, 306f, 106f), "HELP"), MenuLink.LinkAction.Open, "help");
            Link(BoxButton(s.bot, "LegalButton", new Rect(746f, 1766f, 307f, 106f), "LEGAL"), MenuLink.LinkAction.Open, "legal");

            var view = s.screen.gameObject.AddComponent<SettingsView>();
            Set(view, "musicSlider", music);
            Set(view, "sfxSlider", sfx);
            Set(view, "muteToggle", mute);
            Set(view, "graphicsQuality", quality);
            Set(view, "sensitivitySlider", sensitivity);
            Set(view, "vibrationToggle", vibration);
        }

        private static SegmentedControl QualitySelector(Box parent, Rect r)
        {
            string[] names = { "Low", "Med", "High", "Ultra" };
            var root = Add(parent, "QualitySelector", r);
            Rounded(root.go.AddComponent<Image>(), 22f);
            root.go.AddComponent<Mask>().showMaskGraphic = false;

            float w = r.width / names.Length;
            var buttons = new Button[names.Length];
            var selected = new GameObject[names.Length];
            var labels = new TMP_Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var segRect = new Rect(r.x + i * w, r.y, w, r.height);
                var seg = Add(root, names[i], segRect);
                var segImg = seg.go.AddComponent<Image>();
                bool maroon = i % 2 == 0;
                seg.go.AddComponent<UIGradient>().SetColors(maroon ? Hex("#5A1F33") : Hex("#2B3656"), maroon ? Hex("#3E1A2B") : Hex("#222C47"));
                var sel = Add(seg, "Selected", segRect);
                var selImg = sel.go.AddComponent<Image>();
                selImg.color = Hex("#3A5A9A", 0.45f);
                selImg.raycastTarget = false;
                GlowLine(sel, new Rect(segRect.x + 30f, r.yMax - 12f, w - 60f, 16f), Hex("#7FB8FF"));
                selected[i] = sel.go;
                labels[i] = Txt(seg, "Label", names[i], segRect, fMedium, 40f, Hex("#DDE0E8"));
                buttons[i] = MakeButton(seg, segImg, false);
                if (i > 0) Line(root, new Rect(segRect.x - 1f, r.y, 2f, r.height), Hex("#6D7FAE"));
            }
            Border(Add(parent, "QualityFrame", r), 22f, Hex("#6D7FAE"));

            var control = root.go.AddComponent<SegmentedControl>();
            SetArray(control, "segments", buttons);
            SetArray(control, "selectedVisuals", selected);
            SetArray(control, "labels", labels);
            SetColor(control, "normalLabelColor", Hex("#DDE0E8"));
            SetColor(control, "selectedLabelColor", Color.white);
            control.SetWithoutNotify(1);
            return control;
        }

        // ---------------------------------------------------------------------
        // Referansı olmayan ekranlar (aynı stil)
        // ---------------------------------------------------------------------

        private static void SolidBackground(Screen s)
        {
            var bg = Img(s.screen.transform, "SolidBackground", null, C.Bg, false);
            Stretch(bg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            bg.transform.SetAsFirstSibling();
        }

        private static void BuildEvents(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "EVENTS"), MenuLink.LinkAction.Back);

            var season = Section(s.top, "Season", "SEASON PROGRESS", new Rect(63f, 250f, 990f, 300f));
            Txt(season, "SeasonName", "SEASON 4: VELOCITY", new Rect(123f, 360f, 870f, 50f), fBold, 32f, Hex("#8C95AA"));
            RectTransform progress = ProgressBar(season, "SeasonBar", new Rect(123f, 430f, 870f, 30f), 0f);
            var progressText = Txt(season, "SeasonProgress", "", new Rect(123f, 475f, 870f, 50f), fExtra, 34f, Color.white, localize: false);

            EventCard(s.top, "Campaign", "CAMPAIGN", 590f, "Continue the campaign from your latest level.", RunMode.Campaign, out _);
            EventCard(s.top, "QuickRace", "QUICK RACE", 950f, "A random level you have already unlocked.", RunMode.QuickRace, out _);
            var daily = EventCard(s.top, "Daily", "DAILY CHALLENGE", 1310f, "", RunMode.Daily, out TMP_Text dailyText);
            var dailyStatus = Txt(daily, "Status", "", new Rect(123f, 1468f, 870f, 40f), fExtra, 28f, C.Gold, localize: false);

            var view = s.screen.gameObject.AddComponent<EventsView>();
            Set(view, "config", config);
            Set(view, "progressText", progressText);
            Set(view, "progressFill", progress);
            SetFloat(view, "progressWidth", 864f);
            Set(view, "dailyDescription", dailyText);
            Set(view, "dailyStatus", dailyStatus);
        }

        private static Box EventCard(Box parent, string name, string title, float y, string description, RunMode mode, out TMP_Text descriptionText)
        {
            var card = Section(parent, name, title, new Rect(63f, y, 990f, 330f));
            descriptionText = Txt(card, "Description", description, new Rect(123f, y + 108f, 870f, 70f), fMedium, 32f, C.TextSoft, wrap: true);
            Starts(PillButton(card, "StartButton", new Rect(368f, y + 232f, 380f, 72f), "START", 32f, Color.white, 1f), mode);
            return card;
        }

        private static void BuildInventory(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "INVENTORY"), MenuLink.LinkAction.Back);
            var cards = new List<ShopItemCard>();

            var skins = Section(s.top, "Skins", "SKINS", new Rect(63f, 240f, 990f, 640f));
            for (int i = 0; i < SkinIds.Length; i++)
                cards.Add(GridCard(skins, SkinIds[i], new Rect(93f + (i % 4) * 237f, 350f + (i / 4) * 262f, 220f, 245f), true));

            var boosts = Section(s.top, "Boosts", "BOOSTS", new Rect(63f, 910f, 990f, 380f));
            for (int i = 0; i < BoostIds.Length; i++)
                cards.Add(GridCard(boosts, BoostIds[i], new Rect(93f + i * 237f, 1020f, 220f, 245f), false));

            var view = s.screen.gameObject.AddComponent<ItemListView>();
            Set(view, "catalog", catalog);
            SetEnum(view, "mode", (int)CardMode.Inventory);
            SetArray(view, "cards", cards.ToArray());
        }

        private static void BuildCustomize(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "CUSTOMIZE"), MenuLink.LinkAction.Back);
            var cards = new List<ShopItemCard>();
            var skins = Section(s.top, "Skins", "SKINS", new Rect(63f, 260f, 990f, 880f));
            float[] xs = { 93f, 330f, 567f, 804f };
            for (int i = 0; i < SkinIds.Length; i++)
                cards.Add(GridCard(skins, SkinIds[i], new Rect(xs[i % 4], 380f + (i / 4) * 370f, 220f, 342f), true));

            RawPreview(s.top, "RunnerPreview", new Rect(258f, 1150f, 600f, 600f), preview);

            var view = s.screen.gameObject.AddComponent<ItemListView>();
            Set(view, "catalog", catalog);
            SetEnum(view, "mode", (int)CardMode.Customize);
            SetArray(view, "cards", cards.ToArray());
        }

        private static void BuildUpgrade(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "UPGRADE"), MenuLink.LinkAction.Back);

            Panel(s.top, "GoldBox", new Rect(276f, 236f, 564f, 87f), Hex("#1B2236"), Hex("#232C46"), 26f, Hex("#4F5E86"));
            var gold = Txt(s.top, "GoldText", "GOLD: 5000", new Rect(296f, 246f, 430f, 66f), fExtra, 42f, C.Gold, TextAlignmentOptions.Right, localize: false);
            Icon(s.top, "coin", new Rect(744f, 246f, 66f, 66f), Color.white);

            string[] ids = { "speed", "jump", "momentum" };
            string[] titles = { "SPEED", "JUMP", "MOMENTUM" };
            var rows = new List<Dictionary<string, object>>();
            for (int i = 0; i < ids.Length; i++)
            {
                float y = 360f + i * 410f;
                var sec = Section(s.top, titles[i], titles[i], new Rect(63f, y, 990f, 380f));
                RectTransform fill = ProgressBar(sec, "Bar", new Rect(123f, y + 140f, 870f, 30f), 0.5f);
                var value = Txt(sec, "Value", "0", new Rect(123f, y + 185f, 400f, 50f), fExtra, 38f, Color.white, TextAlignmentOptions.Left, localize: false);
                var level = Txt(sec, "Level", "", new Rect(593f, y + 185f, 400f, 50f), fBold, 32f, C.TextGrey, TextAlignmentOptions.Right, localize: false);
                var tier = Txt(sec, "Tier", "", new Rect(323f, y + 185f, 470f, 50f), fMedium, 30f, Hex("#B5B9C6"), localize: false);
                Button button = BoxButton(sec, "UpgradeButton", new Rect(308f, y + 260f, 500f, 92f), "", 36f, out TextMeshProUGUI cost, false);
                rows.Add(new Dictionary<string, object>
                {
                    { "bar", new Dictionary<string, object> { { "statId", ids[i] }, { "value", value }, { "tier", tier }, { "level", level }, { "fill", fill }, { "fullWidth", 864f } } },
                    { "button", button },
                    { "cost", cost },
                });
            }

            var view = s.screen.gameObject.AddComponent<UpgradeView>();
            Set(view, "stats", stats);
            Set(view, "goldText", gold);
            SetStructArray(view, "rows", rows);
        }

        private static void BuildLanguage(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "LANGUAGE"), MenuLink.LinkAction.Back);
            var options = new List<Dictionary<string, object>>();
            (string code, string label)[] langs = { (Localization.English, "English"), (Localization.Turkish, "Türkçe") };
            for (int i = 0; i < langs.Length; i++)
            {
                var r = new Rect(63f, 300f + i * 180f, 990f, 140f);
                Button b = BoxButton(s.top, "Lang_" + langs[i].code, r, langs[i].label, 44f, out _, false);
                var check = Icon(new Box((RectTransform)b.transform, r), "check", new Rect(r.xMax - 110f, r.y + 35f, 70f, 70f), Hex("#7FB8FF"));
                options.Add(new Dictionary<string, object> { { "code", langs[i].code }, { "button", b }, { "check", check.gameObject } });
            }
            SetStructArray(s.screen.gameObject.AddComponent<LanguageView>(), "options", options);
        }

        private static void BuildHelp(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "HELP"), MenuLink.LinkAction.Back);
            var sec = Section(s.top, "HowToPlay", "HOW TO PLAY", new Rect(63f, 285f, 990f, 980f));
            Txt(sec, "Body",
                "Your runner moves forward on its own.\n\nHOLD the screen to charge your jump.\nRELEASE to jump.\n\nA short press is a small hop, a long press is a big jump. Read the obstacles ahead and time your release.\n\nHit an obstacle and the run ends. Retry instantly and beat the level!",
                new Rect(123f, 410f, 870f, 820f), fMedium, 38f, C.TextSoft, TextAlignmentOptions.TopLeft, wrap: true);
        }

        private static void BuildLegal(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "LEGAL"), MenuLink.LinkAction.Back);
            var privacy = Section(s.top, "Privacy", "PRIVACY POLICY", new Rect(63f, 285f, 990f, 420f));
            Txt(privacy, "Body", "JUMPLINE GO stores your progress and settings only on this device. No personal data is collected or shared.",
                new Rect(123f, 410f, 870f, 270f), fMedium, 36f, C.TextSoft, TextAlignmentOptions.TopLeft, wrap: true);
            var terms = Section(s.top, "Terms", "TERMS OF USE", new Rect(63f, 750f, 990f, 420f));
            Txt(terms, "Body", "The game is provided as is. In-game gold and gems have no real-world value and cannot be exchanged for money.",
                new Rect(123f, 875f, 870f, 270f), fMedium, 36f, C.TextSoft, TextAlignmentOptions.TopLeft, wrap: true);
            Txt(s.top, "Version", $"JUMPLINE GO  v{PlayerSettings.bundleVersion}", new Rect(63f, 1220f, 990f, 50f), fBold, 30f, C.TextDim, localize: false);
        }

        // ---------------------------------------------------------------------
        // Alt gezinme
        // ---------------------------------------------------------------------

        private static GameObject BuildBottomNav(Transform safe)
        {
            var rt = NewUI("BottomNav", safe);
            Stretch(rt);
            Box bot = Group(rt, "Bottom", 0f);
            Line(bot, new Rect(0f, 1830f, 1116f, 2f), Hex("#3A4058", 0.9f));
            NavTile(bot, "Market", 133f, "shopping-cart", "MARKET", "market", 0);
            NavTile(bot, "Events", 346f, "calendar-star", "EVENTS", "events", 1);
            NavTile(bot, "Inventory", 558f, "backpack", "INVENTORY", "inventory", 2);
            NavTile(bot, "Character", 770f, "helmet", "CHARACTER", "character", 3);
            NavTile(bot, "Settings", 982f, "settings", "SETTINGS", "settings", 4);
            return rt.gameObject;
        }

        // docs/references/menu/home_v2.jpg — dosya biçimli kutular, ışık halkaları
        private static void NavTile(Box parent, string name, float cx, string icon, string label, string screenId, int index)
        {
            Button b = FolderTile(parent, name + "Tile", new Rect(cx - 75f, 1745f, 150f, 150f), icon, label, index);
            Link(b, MenuLink.LinkAction.OpenRoot, screenId);
        }

        // ---------------------------------------------------------------------
        // Veri
        // ---------------------------------------------------------------------

        private static void LoadData()
        {
            config = GetOrCreateAsset<GameConfig>(ConfigPath, null);
            catalog = GetOrCreateAsset<ShopCatalog>(CatalogPath, null);
            catalog.SetItems(DefaultItems()); // içerik bu dosyada tanımlı; her üretimde yenilenir
            EditorUtility.SetDirty(catalog);
            stats = GetOrCreateAsset<CharacterStats>(StatsPath, s => s.SetStats(DefaultStats()));
        }

        private static List<ShopItem> DefaultItems()
        {
            ShopItem I(string id, string name, ItemCategory cat, CurrencyType cur, int price, int reward = 0) =>
                new ShopItem { id = id, displayName = name, category = cat, currency = cur, price = price, rewardGold = reward };
            return new List<ShopItem>
            {
                // docs/references/character/skins.jpg + renk çeşitleri
                I("rookie", "ROOKIE", ItemCategory.Skin, CurrencyType.Gold, 0),
                I("stealth", "STEALTH", ItemCategory.Skin, CurrencyType.Gems, 200),
                I("velocity", "VELOCITY", ItemCategory.Skin, CurrencyType.Gold, 1500),
                I("titanium", "TITANIUM", ItemCategory.Skin, CurrencyType.Gold, 3000),
                I("glitch", "GLITCH", ItemCategory.Skin, CurrencyType.Gems, 300),
                I("crimson", "CRIMSON", ItemCategory.Skin, CurrencyType.Gold, 1200),
                I("frost", "FROST", ItemCategory.Skin, CurrencyType.Gems, 150),
                I("blaze", "BLAZE", ItemCategory.Skin, CurrencyType.Gold, 2500),
                I("speed_boost", "SPEED BOOST", ItemCategory.Boost, CurrencyType.Gems, 150),
                I("shield", "SHIELD", ItemCategory.Boost, CurrencyType.Gems, 100),
                I("magnet", "MAGNET", ItemCategory.Boost, CurrencyType.Gold, 2000),
                I("double_gold", "DOUBLE GOLD", ItemCategory.Boost, CurrencyType.Gems, 300),
                I("gold_small", "GOLD PACK S", ItemCategory.Currency, CurrencyType.Gems, 50, 1000),
                I("gold_medium", "GOLD PACK M", ItemCategory.Currency, CurrencyType.Gems, 200, 5000),
                I("gold_large", "GOLD PACK L", ItemCategory.Currency, CurrencyType.Gems, 400, 12000),
            };
        }

        private static List<StatDefinition> DefaultStats() => new List<StatDefinition>
        {
            // Başlangıç değerleri referanstaki çubuklarla aynı (150 High, 60 Medium, 120 Very High).
            new StatDefinition { id = "speed", displayName = "SPEED", baseValue = 150, perLevel = 3, maxValue = 180, maxLevel = 10, upgradeCostBase = 500 },
            new StatDefinition { id = "jump", displayName = "JUMP", baseValue = 60, perLevel = 5, maxValue = 110, maxLevel = 10, upgradeCostBase = 500 },
            new StatDefinition { id = "momentum", displayName = "MOMENTUM", baseValue = 120, perLevel = 1, maxValue = 130, maxLevel = 10, upgradeCostBase = 400 },
        };
    }
}
