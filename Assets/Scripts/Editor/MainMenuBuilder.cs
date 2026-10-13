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
    public static partial class MainMenuBuilder
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
            inkBody = true;
            LoadData();
            var skins = RunnerStageBuilder.EnsureLibrary();
            RunnerStageBuilder.ConfigureImport();
            RunnerStageBuilder.SetupAnimations();
            preview = RunnerStageBuilder.BuildStage(skins, out _, out var runner, out var stageCamera);
            thumbs = RunnerStageBuilder.RenderThumbnails(skins, runner, stageCamera);
            RunnerStageBuilder.FinishStage(runner, stageCamera); // podyum + kahraman duruşu

            BuildCameraAndEvents(true);
            Transform canvas = BuildCanvas("MainMenuCanvas", true);
            var safe = NewUI("SafeArea", canvas);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var navigator = canvas.gameObject.AddComponent<MenuNavigator>();
            canvas.gameObject.AddComponent<Jumpline.Audio.MenuMusic>();
            AudioSetup.Setup();

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
            BuildCredits(NewScreen(safe, "credits", false));
            BuildMap(NewScreen(safe, "map", true));
            BuildTasks(NewScreen(safe, "tasks", false));
            BuildBook(NewScreen(safe, "book", true));

            GameObject nav = BuildBottomNav(safe);
            BuildCurrencyBar(safe);   // her ekranın üstünde altın + elmas
            BuildPlayerBadge(safe);
            BuildLoginCalendar(safe);
            Toast toast = BuildToast(safe);
            Set(navigator, "bottomNav", nav);
            Set(navigator, "toast", toast);

            inkBody = false;
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

        // ---------------------------------------------------------------------
        // ALTIN + ELMAS ÇUBUĞU (her ekranda)
        // ---------------------------------------------------------------------
        private const float CurrencyBarHeight = 92f;

        /// <summary>Üstte sabit çubuk; ekranların üst içeriği çubuk kadar aşağı kayar (logo, sekmeler, başlıklar altında kalmasın).</summary>
        private static void BuildCurrencyBar(Transform safe)
        {
            foreach (Transform screen in safe)
            {
                if (screen.GetComponent<MenuScreen>() == null) continue;
                var top = screen.Find("Top") as RectTransform;
                if (top != null) top.anchoredPosition += new Vector2(0f, -CurrencyBarHeight);
            }
            Box bar = Group(safe, "CurrencyBar", 1f);
            var shade = Add(bar, "Shade", new Rect(0f, 0f, 1116f, CurrencyBarHeight));
            var shadeImg = shade.go.AddComponent<Image>();
            shadeImg.color = Hex("#0E1428", 0.6f);
            shadeImg.raycastTarget = false;
            TMP_Text Pill(string name, string icon, Rect r)
            {
                SkinImage(bar, name, r, "pill", false, 0.45f);
                Icon(bar, icon, new Rect(r.x - 16f, r.y - 8f, r.height + 16f, r.height + 16f), Color.white);
                var t = Txt(bar, name + "Text", "0", new Rect(r.x + r.height + 4f, r.y, r.width - r.height - 66f, r.height), fExtra, 38f, Color.white,
                    TextAlignmentOptions.Midline, localize: false);
                t.raycastTarget = false;
                var plus = Add(bar, name + "Plus", new Rect(r.xMax - 58f, r.y + 6f, r.height - 12f, r.height - 12f));
                var pi = plus.go.AddComponent<Image>();
                pi.sprite = Skin["round_green"];
                pi.preserveAspect = true;
                Txt(plus, "Plus", "+", new Rect(r.xMax - 58f, r.y + 2f, r.height - 12f, r.height - 12f), fExtra, 40f, Color.white, localize: false);
                Link(MakeButton(plus, pi), MenuLink.LinkAction.OpenRoot, "market");
                return t;
            }
            var gold = Pill("GoldPill", "coin", new Rect(330f, 14f, 300f, 66f));
            var gems = Pill("GemsPill", "gem", new Rect(668f, 14f, 280f, 66f));
            var gear = Add(bar, "SettingsButton", new Rect(980f, 4f, 112f, 86f));
            var gi = gear.go.AddComponent<Image>();
            gi.sprite = Skin["round_saffron"];
            gi.preserveAspect = true;
            Icon(gear, "gear", new Rect(1000f, 12f, 72f, 70f), Color.white);
            Link(MakeButton(gear, gi), MenuLink.LinkAction.OpenRoot, "settings");
            var cb = bar.rt.gameObject.AddComponent<CurrencyBar>();
            Set(cb, "goldLabel", gold);
            Set(cb, "gemsLabel", gems);
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
            // docs/flow_ui/anamenu_A.jpg — gece Diyarbakır arka planı, bazalt-bakır sahne halkası, dev safran OYNA
            var logo = Add(s.top, "Logo", new Rect(318f, 6f, 480f, 240f));
            var li = logo.go.AddComponent<Image>();
            li.sprite = Skin["logo"];
            li.preserveAspect = true;
            li.raycastTarget = false;
            var stage = Add(s.top, "StageWindow", new Rect(218f, 300f, 680f, 680f));
            var sti = stage.go.AddComponent<Image>();
            sti.sprite = circle;
            sti.color = Hex("#16203C", 0.85f);
            stage.go.AddComponent<Mask>().showMaskGraphic = true;
            RawPreview(stage, "RunnerPreview", new Rect(118f, 250f, 880f, 880f), preview);
            var ring = SkinImage(s.top, "StageRing", new Rect(168f, 250f, 780f, 780f), "stage_ring", true);
            Link(MakeButton(new Box(ring.rectTransform, new Rect(168f, 250f, 780f, 780f)), ring), MenuLink.LinkAction.Open, "character");
            AddSparkles(s.top, new Rect(168f, 250f, 780f, 780f), 8, 40f);
            // sol kısayollar (görevler / etkinlikler / karakter)
            (string icon, string label, string target)[] side = { ("scroll", "TASKS", "tasks"), ("calendar-star", "EVENTS", "events"), ("helmet", "CHARACTER", "character") };
            for (int i = 0; i < side.Length; i++)
            {
                var r = new Rect(24f, 300f + i * 200f, 130f, 130f);
                var sb = Add(s.top, "Side" + side[i].target, r);
                var sbi = sb.go.AddComponent<Image>();
                SkinSprite(sbi, "tab", r);
                Icon(sb, side[i].icon, new Rect(r.x + 14f, r.y + 10f, 102f, 102f), Color.white);
                Txt(s.top, "Side" + side[i].target + "Label", side[i].label, new Rect(r.x - 20f, r.yMax - 6f, 170f, 44f), fExtra, 24f, Color.white);
                Link(MakeButton(sb, sbi), MenuLink.LinkAction.Open, side[i].target);
            }
            Link(GlassPlayButton(s.bot, "PlayButton", new Rect(148f, 1188f, 680f, 230f), "PLAY", 128f), MenuLink.LinkAction.Open, "map");
            var mode = Add(s.bot, "QuickRaceMode", new Rect(852f, 1200f, 206f, 206f));
            var mi = mode.go.AddComponent<Image>();
            SkinSprite(mi, "btn_turquoise", new Rect(852f, 1200f, 206f, 206f), 0.4f);
            Icon(mode, "bolt", new Rect(892f, 1220f, 126f, 126f), Color.white);
            Txt(mode, "Label", "QUICK RACE", new Rect(852f, 1340f, 206f, 50f), fExtra, 24f, Color.white);
            Starts(MakeButton(mode, mi), RunMode.QuickRace);
            Starts(TagButton(s.bot, "DailyChallengeButton", new Rect(188f, 1470f, 740f, 120f), "DAILY CHALLENGE", false, 44f), RunMode.Daily);
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
                StatRow(s.top, "magnet", "MAGNET", 515f),
                StatRow(s.top, "shield", "SHIELD", 640f),
                StatRow(s.top, "double_gold", "DOUBLE GOLD", 765f),
                StatRow(s.top, "speed_boost", "SPEED BOOST", 890f),
            };

            Txt(s.top, "GearHeader", "GEAR", new Rect(862f, 420f, 213f, 56f), fBold, 34f, Hex("#C9CCD6"));
            Line(s.top, new Rect(862f, 479f, 213f, 2f), Hex("#4E546B"));
            Panel(s.top, "GearPanel", new Rect(862f, 497f, 213f, 871f), Hex("#1E2438", 0.75f), Hex("#2A1A28", 0.75f), 18f, Hex("#5D6B93"));
            TMP_Text suit = GearSlot(s.top, "Suit", 523f, "SUIT:", "ROOKIE", null, null);
            GearSlot(s.top, "Boots", 742f, "BOOTS:", "SPRING BOOTS", "gear_boots", RunnerSkin.Boots);
            GearSlot(s.top, "Gloves", 950f, "GLOVES:", "GRIP-TECH", "gear_gloves", RunnerSkin.Gloves);
            GearSlot(s.top, "Helmet", 1160f, "HELMET:", "VELOCITY VISOR", "gear_helmet", RunnerSkin.Helmet);

            Link(PrimaryButton(s.bot, "CustomizeButton", new Rect(262f, 1362f, 590f, 180f), new Rect(285f, 1380f, 545f, 142f), "CUSTOMIZE", 80f), MenuLink.LinkAction.Open, "customize");
            Link(PillButton(s.bot, "UpgradeButton", new Rect(378f, 1572f, 360f, 68f), "UPGRADE", 30f, Hex("#C3C8D6"), 1f), MenuLink.LinkAction.Open, "upgrade");
            Link(PillButton(s.bot, "BackButton", new Rect(765f, 1572f, 308f, 68f), "BACK", 30f, Hex("#C3C8D6"), 1f), MenuLink.LinkAction.Back);

            var view = s.screen.gameObject.AddComponent<CharacterView>();
            Set(view, "stats", stats);
            Set(view, "catalog", catalog);
            Set(view, "suitName", suit);
            SetStructArray(view, "bars", bars);
        }

        /// <summary>Takıp çıkarma + takılı çeşide göre ikon (standard / stealth / velocity / titanium).</summary>
        private static void AddGearToggle(GameObject go, string slot, string label, Image art, TMP_Text status, string standardIcon)
        {
            var toggle = go.AddComponent<GearToggle>();
            toggle.Configure(slot, label, art, status);
            string[] ids = { "standard", "stealth", "velocity", "titanium" };
            var sprites = new Sprite[ids.Length];
            sprites[0] = Icons[standardIcon];
            for (int i = 1; i < ids.Length; i++)
                sprites[i] = Icons.TryGetValue($"{ids[i]}_{slot}", out Sprite sp) ? sp : sprites[0];
            toggle.SetVariants(ids, sprites);
            EditorUtility.SetDirty(toggle);
        }

        private static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, object> StatRow(Box parent, string id, string label, float y)
        {
            Txt(parent, id + "Label", label, new Rect(45f, y, 230f, 40f), fBold, 28f, Hex("#E2E4EA"), TextAlignmentOptions.Left);
            var value = Txt(parent, id + "Value", "0", new Rect(225f, y, 100f, 40f), fBold, 28f, Hex("#E2E4EA"), TextAlignmentOptions.Right, localize: false);
            RectTransform fill = ProgressBar(parent, id + "Bar", new Rect(42f, y + 43f, 286f, 24f), 0.5f);
            var tier = Txt(parent, id + "Tier", "", new Rect(45f, y + 70f, 280f, 36f), fMedium, 26f, Hex("#B5B9C6"), TextAlignmentOptions.Left, localize: false);
            return new Dictionary<string, object> { { "statId", id }, { "value", value }, { "tier", tier }, { "fill", fill }, { "fullWidth", 280f }, { "valueFormat", "{0} SEC" } };
        }

        private static TMP_Text GearSlot(Box parent, string name, float y, string kind, string item, string icon, string gearSlot)
        {
            var r = new Rect(901f, y, 134f, 134f);
            var slot = Panel(parent, name + "Slot", r, Hex("#5A1D33"), Hex("#2F3A5E"), 18f, Hex("#8193C0"));
            Image art = icon != null ? Icon(slot, icon, new Rect(905f, y + 6f, 126f, 122f), Color.white) : null;
            if (icon == null) Thumb(slot, "rookie", new Rect(905f, y + 4f, 126f, 126f));
            Color c = Hex("#D9DCE3");
            Txt(parent, name + "Kind", kind, new Rect(868f, y + 142f, 200f, 26f), fBold, 18f, c);
            var itemText = Txt(parent, name + "Item", item, new Rect(862f, y + 165f, 213f, 26f), fBold, 18f, c, localize: name != "Suit");
            Button button = MakeButton(slot, slot.rt.GetComponent<Image>());
            if (gearSlot == null)
            {
                Link(button, MenuLink.LinkAction.Open, "customize");
            }
            else
            {
                var status = Txt(slot, "Status", "", new Rect(r.x, r.yMax - 26f, r.width, 22f), fExtra, 15f, c, localize: false);
                AddGearToggle(button.gameObject, gearSlot, item, art, status, icon);
            }
            return itemText;
        }

        // ---------------------------------------------------------------------
        // MARKET
        // ---------------------------------------------------------------------

        // =====================================================================
        // MARKET — docs/references/market/*.jpg (768x1376 → x1.453) + menu/market_v2 (Kostümler sekmesi)
        // Sekmeler: OFFERS, SKINS, GEAR, BOOSTS, CURRENCY
        // =====================================================================

        private const float NavTop = 1735f;

        private static void BuildMarket(Screen s)
        {
            var cards = new List<ShopItemCard>();
            string[] tabNames = { "OFFERS", "SKINS", "GEAR", "BOOSTS", "CURRENCY" };

            var offers = TabPage(s.top, "OffersTab", false);
            BuildOffersTab(offers, cards);
            var skins = TabPage(s.top, "SkinsTab", false);
            BuildSkinsTab(skins, cards, out TMP_Text gold, out TMP_Text gems);
            var gear = TabPage(s.top, "GearTab", false);
            BuildGearTab(gear, cards);
            var boosts = TabPage(s.top, "BoostsTab", true);
            BuildBoostsTab(boosts, cards);
            var currency = TabPage(s.top, "CurrencyTab", true);
            BuildCurrencyTab(currency, cards);

            SegmentedControl tabs = Tabs(s.top, tabNames);

            var view = s.screen.gameObject.AddComponent<MarketView>();
            Set(view, "catalog", catalog);
            Set(view, "goldText", gold);
            Set(view, "gemsText", gems);
            Set(view, "tabs", tabs);
            SetArray(view, "tabPanels", new Object[] { offers.go, skins.go, gear.go, boosts.go, currency.go });
            offers.go.SetActive(true);
            foreach (var t in new[] { skins, gear, boosts, currency }) t.go.SetActive(false);
            SetArray(view, "cards", cards.ToArray());
        }

        /// <summary>Sekme sayfası: kendi arka planı (koyu geçiş ya da yol görseli).</summary>
        private static Box TabPage(Box parent, string name, bool road)
        {
            var page = Add(parent, name, Full);
            road = false;   // tema "Taş & Çini": tüm sekmeler gece Diyarbakır arka planı
            var bg = Img(page.rt, "Background", background, Color.white, false);
            bg.raycastTarget = false;
            if (road)
            {
                // Görsel ekranın altını kaplasın; üstü koyu laciverte erisin (referans)
                Place(bg.rectTransform, page, new Rect(-200f, 320f, 1516f, 1600f));
                var solid = Add(page, "TopSolid", new Rect(0f, -300f, 1116f, 700f));
                Fill(solid, Hex("#141826"), Hex("#141826"));
                var fade = Add(page, "TopFade", new Rect(0f, 400f, 1116f, 600f));
                Fill(fade, new Color(0.078f, 0.094f, 0.149f, 0f), Hex("#141826"));
                solid.rt.SetSiblingIndex(1);
                fade.rt.SetSiblingIndex(2);
            }
            else
            {
                Stretch(bg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            }
            return page;
        }

        private static TextMeshProUGUI SectionTitle(Box parent, string text, float y) =>
            Txt(parent, text.Replace(" ", "") + "Title", text, new Rect(58f, y, 900f, 56f), fExtra, 38f, Color.white, TextAlignmentOptions.Left);

        /// <summary>Lacivert parlak fiyat kapsülü (referanstaki "$2.99").</summary>
        private static Button PricePill(Box parent, Rect r, out TextMeshProUGUI label, float size = 34f)
        {
            var b = GoldFace(parent, "PriceButton", r, 14f, Hex("#3E8E2E"), Hex("#6CC24A"));   // referans: yeşil fiyat düğmesi
            label = Txt(b, "Label", "", r, fExtra, size, Color.white, localize: false);
            return MakeButton(b, b.rt.GetComponent<Image>());
        }

        /// <summary>Bordo fiyat butonu (ekipman mağazası).</summary>
        private static Button RedPill(Box parent, Rect r, out TextMeshProUGUI label)
        {
            var b = GoldFace(parent, "PriceButton", r, 14f, Hex("#3E8E2E"), Hex("#6CC24A"));
            label = Txt(b, "Label", "", r, fExtra, 34f, Color.white, localize: false);
            return MakeButton(b, b.rt.GetComponent<Image>());
        }

        /// <summary>Mavi dikdörtgen fiyat butonu (güç mağazası).</summary>
        private static Button BluePill(Box parent, Rect r, out TextMeshProUGUI label)
        {
            var b = GoldFace(parent, "PriceButton", r, 14f, Hex("#3E8E2E"), Hex("#6CC24A"));
            label = Txt(b, "Label", "", r, fExtra, 44f, Color.white, localize: false);
            return MakeButton(b, b.rt.GetComponent<Image>());
        }

        private static Box RarityCard(Box parent, string name, Rect r, Rarity rarity, Color bottom, Color top)
        {
            var card = Add(parent, name, r);
            Panel(card, "Face", r, bottom, top, 20f, RarityColor(rarity));
            return card;
        }

        private static ShopItemCard Bind(Box root, string itemId, Button button, TMP_Text title, TMP_Text status, TMP_Text action, bool keepTitle)
        {
            var card = Card(root, itemId, button, title, status, action, null);
            if (keepTitle) SetBool(card, "keepTitle", true);
            return card;
        }

        // ---------------- OFFERS ----------------
        private static void BuildOffersTab(Box page, List<ShopItemCard> cards)
        {
            Box c = VerticalScroll(page, "OffersScroll", new Rect(0f, 113f, 1116f, NavTop - 113f), 2060f);

            SectionTitle(c, "FEATURED", 150f);
            var vp = Add(c, "FeaturedViewport", new Rect(0f, 222f, 1116f, 440f));
            vp.go.AddComponent<RectMask2D>();
            vp.go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var strip = Add(vp, "Content", new Rect(0f, 222f, 1716f, 440f));
            strip.rt.anchorMin = strip.rt.anchorMax = new Vector2(0f, 0.5f);
            strip.rt.pivot = new Vector2(0f, 0.5f);
            strip.rt.anchoredPosition = Vector2.zero;
            var sr = vp.go.AddComponent<ScrollRect>();
            sr.content = strip.rt;
            sr.viewport = vp.rt;
            sr.horizontal = true;
            sr.vertical = false;
            cards.Add(FeaturedCard(strip, "velocity_pack", new Rect(58f, 240f, 785f, 404f), "pack_velocity", true));
            cards.Add(FeaturedCard(strip, "velocity_bundle", new Rect(873f, 240f, 785f, 404f), "pack_bundle", false));

            SectionTitle(c, "CHARACTER SKINS", 700f);
            string[] skinOffers = { "offer_stealth", "offer_titanium", "offer_crimson", "offer_glitch", "offer_velocity", "offer_blaze" };
            for (int i = 0; i < skinOffers.Length; i++)
            {
                var r = new Rect(58f + (i % 3) * 341f, 780f + (i / 3) * 391f, 317f, 366f);
                ShopItem item = catalog.Find(skinOffers[i]);
                var card = RarityCard(c, skinOffers[i], r, item.rarity, Hex("#1C2236"), Hex("#2A3352"));
                Thumb(card, item.rewardSkin + "_bust", new Rect(r.x + 30f, r.y + 18f, r.width - 60f, 238f));
                RarityIcon(card, r, item.rarity);
                var title = Txt(card, "Title", item.displayName, new Rect(r.x, r.y + 252f, r.width, 40f), fExtra, 28f, Color.white, localize: false);
                Button b = PricePill(card, new Rect(r.x + 50f, r.y + 296f, r.width - 100f, 60f), out TextMeshProUGUI price);
                cards.Add(Bind(card, item.id, b, title, null, price, true));
            }

            SectionTitle(c, "GEMS & GOLD", 1590f);
            (string id, string art)[] packs = { ("pack_starter", "pack_starter"), ("pack_value", "pack_value"), ("pack_goldbundle", "pack_goldbundle") };
            for (int i = 0; i < packs.Length; i++)
            {
                var r = new Rect(58f + i * 341f, 1664f, 317f, 366f);
                ShopItem item = catalog.Find(packs[i].id);
                var card = RarityCard(c, packs[i].id, r, item.rarity, Hex("#1C2236"), Hex("#2A3352"));
                Icon(card, packs[i].art, new Rect(r.x + 40f, r.y + 40f, r.width - 80f, 200f), Color.white);
                RarityIcon(card, r, item.rarity);
                var title = Txt(card, "Title", item.displayName, new Rect(r.x, r.y + 252f, r.width, 40f), fExtra, 28f, Color.white, localize: false);
                Button b = PricePill(card, new Rect(r.x + 50f, r.y + 296f, r.width - 100f, 60f), out TextMeshProUGUI price);
                cards.Add(Bind(card, item.id, b, title, null, price, true));
            }
        }

        private static ShopItemCard FeaturedCard(Box parent, string itemId, Rect r, string art, bool pink)
        {
            ShopItem item = catalog.Find(itemId);
            var card = Add(parent, itemId, r);
            Color accent = pink ? Hex("#FF6FB0") : Hex("#6FA8FF");
            Panel(card, "Face", r, pink ? Hex("#3A1530") : Hex("#152040"), pink ? Hex("#6A2048") : Hex("#22386A"), 22f, RarityColor(item.rarity));
            var glow = Img(card.rt, "Glow", softCircle, accent * new Color(1f, 1f, 1f, 0.35f), false);
            Place(glow.rectTransform, card, new Rect(r.x + 300f, r.y - 60f, 520f, 520f));
            glow.raycastTarget = false;
            if (pink) Thumb(card, "velocity", new Rect(r.x + 400f, r.y + 6f, 240f, 300f));
            Icon(card, art, new Rect(r.x + 40f, r.y + 130f, 260f, 210f), Color.white);
            var title = Txt(card, "Title", item.displayName, new Rect(r.x + 32f, r.y + 26f, 700f, 60f), fExtra, 44f, Color.white, TextAlignmentOptions.Left, localize: false);
            Txt(card, "Subtitle", item.subtitle, new Rect(r.x + 32f, r.y + 86f, 500f, 36f), fExtra, 24f, accent, TextAlignmentOptions.Left, localize: false);
            RarityIcon(card, r, item.rarity);
            Txt(card, "Rarity", pink ? "RARITY: 0/3" : "RARITY: 0/51", new Rect(r.x + 32f, r.yMax - 64f, 400f, 36f), fBold, 24f, Hex("#A9AEBD"), TextAlignmentOptions.Left, localize: false);
            Button b = PricePill(card, new Rect(r.xMax - 250f, r.yMax - 98f, 221f, 68f), out TextMeshProUGUI price, 38f);
            return Bind(card, itemId, b, title, null, price, true);
        }

        // ---------------- SKINS (menu/market_v2.jpg) ----------------
        private static void BuildSkinsTab(Box page, List<ShopItemCard> cards, out TMP_Text gold, out TMP_Text gems)
        {
            var bar = Add(page, "TopBar", new Rect(0f, 113f, 1116f, 104f));
            Fill(bar, Hex("#141826"), Hex("#1C2133"));
            Line(page, new Rect(0f, 215f, 1116f, 2f), Hex("#3B4A6E"));
            Txt(page, "Title", "JUMPLINE GO", new Rect(48f, 125f, 520f, 80f), fExtra, 50f, Color.white, TextAlignmentOptions.Left, localize: false);
            Icon(page, "coin", new Rect(655f, 135f, 60f, 60f), Color.white);
            gold = Txt(page, "GoldText", "0", new Rect(722f, 133f, 180f, 64f), fExtra, 44f, Color.white, TextAlignmentOptions.Left, localize: false);
            Icon(page, "gem", new Rect(890f, 133f, 62f, 64f), Color.white);
            gems = Txt(page, "GemsText", "0", new Rect(958f, 133f, 150f, 64f), fExtra, 44f, Color.white, TextAlignmentOptions.Left, localize: false);

            Box skins = VerticalScroll(page, "SkinsScroll", new Rect(0f, 217f, 1116f, NavTop - 217f), 2420f);
            RawPreview(skins, "RunnerPreview", new Rect(430f, 470f, 680f, 1000f), preview);
            var deals = Panel(skins, "DealsPanel", new Rect(75f, 250f, 333f, 1340f), Hex("#1E2236", 0.85f), Hex("#262D48", 0.85f), 22f, Hex("#5D6B93"));
            Txt(deals, "DealsTitle", "DAILY DEALS", new Rect(75f, 270f, 333f, 70f), fExtra, 38f, Color.white);
            cards.Add(DealCard(deals, 1, "stealth", new Rect(102f, 352f, 279f, 412f), true));
            cards.Add(DealCard(deals, 2, "speed_boost", new Rect(102f, 780f, 279f, 398f), false));
            cards.Add(DealCard(deals, 3, "titanium", new Rect(102f, 1194f, 279f, 370f), false));
            float[] xs = { 75f, 323f, 571f, 820f };
            float[] ys = { 1640f, 1950f };
            int n = 0;
            foreach (float y in ys)
            foreach (float x in xs)
            {
                cards.Add(GridCard(skins, SkinIds[n], new Rect(x, y, 221f, 281f), false));
                n++;
            }
        }

        // ---------------- GEAR (market/gear.jpg) ----------------
        private static void BuildGearTab(Box page, List<ShopItemCard> cards)
        {
            Box c = VerticalScroll(page, "GearScroll", new Rect(0f, 113f, 1116f, NavTop - 113f), 1880f);
            var banner = Add(c, "Banner", new Rect(150f, 150f, 816f, 82f));
            var poly = AddPolygon(banner);
            poly.Configure(new[] { new Vector2(0.08f, 0f), new Vector2(0.92f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) });
            poly.color = Hex("#6A1430");
            poly.raycastTarget = false;
            banner.go.AddComponent<UIGradient>().SetColors(Hex("#3A0C1E"), Hex("#8A1A3A"), true);
            GlowLine(c, new Rect(150f, 226f, 816f, 12f), Hex("#FF4F7A"));
            Txt(c, "BannerTitle", "GEAR SHOP", new Rect(150f, 152f, 816f, 80f), fExtra, 54f, Color.white);

            string[] slots = { "helmet", "gloves", "boots" };
            string[] titles = { "HELMETS", "GLOVES", "BOOTS" };
            string[] variants = { "stealth", "velocity", "titanium" };
            for (int row = 0; row < 3; row++)
            {
                float y = 330f + row * 520f;
                Txt(c, titles[row] + "Title", titles[row], new Rect(52f, y - 62f, 600f, 56f), fExtra, 38f, Color.white, TextAlignmentOptions.Left);
                for (int col = 0; col < 3; col++)
                {
                    string id = $"{variants[col]}_{slots[row]}";
                    ShopItem item = catalog.Find(id);
                    var r = new Rect(52f + col * 345f, y, 322f, 433f);
                    var card = RarityCard(c, id, r, item.rarity, Hex("#151C30"), Hex("#22304E"));
                    Icon(card, id, new Rect(r.x + 26f, r.y + 50f, r.width - 52f, 240f), Color.white);
                    RarityTag(card, r, item.rarity);
                    var title = Txt(card, "Title", item.displayName, new Rect(r.x, r.y + 298f, r.width, 44f), fExtra, 28f, Color.white, localize: false);
                    Button b = RedPill(card, new Rect(r.x + 22f, r.y + 356f, r.width - 44f, 60f), out TextMeshProUGUI price);
                    cards.Add(Bind(card, id, b, title, null, price, true));
                }
            }
        }

        // ---------------- BOOSTS (market/boosts.jpg) ----------------
        private static void BuildBoostsTab(Box page, List<ShopItemCard> cards)
        {
            Box c = VerticalScroll(page, "BoostsScroll", new Rect(0f, 113f, 1116f, NavTop - 113f), 2280f);
            Chrome(Txt(c, "Title", "BOOSTS SHOP", new Rect(158f, 165f, 800f, 90f), fExtra, 62f, Color.white));
            (string id, string art, string desc)[] boosts =
            {
                ("magnet", "boost_magnet_big", "Attracts nearby coins automatically."),
                ("double_gold", "boost_x2_big", "Earn double gold coins for 24h."),
                ("speed_boost", "boost_speed_big", "Temporarily increases speed."),
                ("shield", "shield", "Blocks one hit from an obstacle."),
            };
            for (int i = 0; i < boosts.Length; i++)
            {
                var r = new Rect(90f, 293f + i * 495f, 937f, 439f);
                ShopItem item = catalog.Find(boosts[i].id);
                var card = Add(c, boosts[i].id, r);
                Panel(card, "Face", r, Hex("#4A1428", 0.92f), Hex("#22305A", 0.92f), 18f, Hex("#8095C4"));
                Icon(card, boosts[i].art, new Rect(r.x + 15f, r.y + 15f, 470f, 410f), Color.white);
                var title = Txt(card, "Title", item.displayName, new Rect(r.x + 515f, r.y + 70f, 410f, 64f), fExtra, 44f, Color.white, TextAlignmentOptions.Left, localize: false);
                Chrome(title);
                Txt(card, "Description", boosts[i].desc, new Rect(r.x + 515f, r.y + 140f, 400f, 120f), fMedium, 30f, C.TextSoft, TextAlignmentOptions.TopLeft, wrap: true);
                Button b = BluePill(card, new Rect(r.x + 515f, r.y + 300f, 312f, 90f), out TextMeshProUGUI price);
                cards.Add(Bind(card, item.id, b, title, null, price, true));
            }
        }

        // ---------------- CURRENCY (market/gold.jpg + gems.jpg) ----------------
        private static void BuildCurrencyTab(Box page, List<ShopItemCard> cards)
        {
            Box c = VerticalScroll(page, "CurrencyScroll", new Rect(0f, 113f, 1116f, NavTop - 113f), 1500f);
            (string title, float y, Color border, (string id, string art)[] items)[] groups =
            {
                ("GOLD PACKS", 150f, Hex("#C9A06A"), new[] { ("gold_small", "gold_pouch"), ("gold_medium", "gold_stack"), ("gold_large", "gold_vault") }),
                ("GEM PACKS", 800f, Hex("#9AA8D8"), new[] { ("gem_small", "gem_pouch"), ("gem_medium", "gem_cluster"), ("gem_large", "gem_cache") }),
            };
            foreach (var g in groups)
            {
                SectionTitle(c, g.title, g.y);
                for (int i = 0; i < g.items.Length; i++)
                {
                    var r = new Rect(58f + i * 341f, g.y + 90f, 317f, 491f);
                    ShopItem item = catalog.Find(g.items[i].id);
                    var card = Add(c, item.id, r);
                    Panel(card, "Face", r, Hex("#151A2C", 0.95f), Hex("#232C46", 0.95f), 20f, g.border);
                    Txt(card, "Name", item.displayName, new Rect(r.x, r.y + 22f, r.width, 50f), fBold, 31f, Color.white, localize: false);
                    Icon(card, g.items[i].art, new Rect(r.x + 28f, r.y + 82f, r.width - 56f, 228f), Color.white);
                    string amount = item.rewardGold > 0 ? $"{item.rewardGold} GOLD" : $"{item.rewardGems} GEMS";
                    var title = Txt(card, "Amount", amount, new Rect(r.x, r.y + 322f, r.width, 48f), fExtra, 33f, Color.white, localize: false);
                    Button b = PricePill(card, new Rect(r.x + 50f, r.y + 396f, r.width - 100f, 62f), out TextMeshProUGUI price);
                    cards.Add(Bind(card, item.id, b, title, null, price, true));
                }
            }
        }

        /// <summary>
        /// Dikey kaydırma alanı. Dönen kutuya referans koordinatlarıyla eklenir; içerik viewport'un üstünden
        /// contentBottom'a kadar uzanır.
        /// </summary>
        private static Box VerticalScroll(Box parent, string name, Rect viewportRect, float contentBottom)
        {
            var viewport = Add(parent, name, viewportRect);
            viewport.go.AddComponent<RectMask2D>();
            viewport.go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // sürükleme için raycast hedefi
            var contentRect = new Rect(viewportRect.x, viewportRect.y, viewportRect.width, contentBottom - viewportRect.y);
            var content = Add(viewport, "Content", contentRect);
            content.rt.anchorMin = content.rt.anchorMax = new Vector2(0.5f, 1f);
            content.rt.pivot = new Vector2(0.5f, 1f);
            content.rt.anchoredPosition = Vector2.zero;
            var scroll = viewport.go.AddComponent<ScrollRect>();
            scroll.content = content.rt;
            scroll.viewport = viewport.rt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            return content;
        }

        private static SegmentedControl Tabs(Box parent, string[] names)
        {
            var strip = Add(parent, "Tabs", new Rect(0f, 0f, 1116f, 113f));
            var stripImg = strip.go.AddComponent<Image>();
            SkinSprite(stripImg, "navbar", new Rect(0f, 0f, 1116f, 113f), 0.4f);   // bazalt şerit, bakır kenar
            float w = 1116f / names.Length;
            var buttons = new Button[names.Length];
            var selected = new GameObject[names.Length];
            var labels = new TMP_Text[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                var r = new Rect(i * w, 0f, w, 111f);
                var tab = Add(strip, names[i] + "Tab", r);
                var hit = tab.go.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                var sel = Add(tab, "Selected", new Rect(r.x + 8f, r.y + 10f, r.width - 16f, r.height - 18f));
                var selImg = sel.go.AddComponent<Image>();
                SkinSprite(selImg, "btn_saffron", new Rect(0f, 0f, r.width - 16f, r.height - 18f));
                selImg.raycastTarget = false;
                selected[i] = sel.go;
                labels[i] = Txt(tab, "Label", names[i], r, fExtra, names[i].Length > 7 ? 32f : 36f, Color.white);
                labels[i].enableAutoSizing = true;
                labels[i].fontSizeMin = 20f;
                labels[i].fontSizeMax = labels[i].fontSize;
                buttons[i] = MakeButton(tab, hit, false);
            }
            var control = strip.go.AddComponent<SegmentedControl>();
            SetArray(control, "segments", buttons);
            SetArray(control, "selectedVisuals", selected);
            SetArray(control, "labels", labels);
            SetColor(control, "normalLabelColor", Hex("#E9D3A1"));
            SetColor(control, "selectedLabelColor", Color.white);
            control.SetWithoutNotify(0);
            return control;
        }

        private static ShopItemCard DealCard(Box parent, int number, string itemId, Rect r, bool highlighted)
        {
            ShopItem item = catalog.Find(itemId);
            bool gold = item.currency == CurrencyType.Gold;
            var root = Add(parent, $"DealCard_{number}", r);
            if (highlighted) Glow(root, Expand(r, 22f), Hex("#5AA8FF", 0.4f));
            Panel(root, "Card", r, Hex("#5B1C30"), Hex("#263257"), 20f, highlighted ? Hex("#A9CCFF") : Hex("#8095C4"));
            var tab = Panel(root, "NumberTab", new Rect(r.x + 10f, r.y + 10f, 50f, 50f), Hex("#2A3556"), Hex("#3A4A72"), 12f, Hex("#8095C4"));
            Txt(tab, "Number", number.ToString(), new Rect(r.x + 10f, r.y + 12f, 50f, 46f), fExtra, 30f, Color.white, localize: false);
            float bottom = r.yMax;
            Thumb(root, itemId, new Rect(r.x + 30f, r.y + 20f, r.width - 60f, r.height - 190f));
            var title = Txt(root, "Title", item.displayName, new Rect(r.x, bottom - 168f, r.width, 40f), fBold, 28f, Color.white, localize: false);
            var price = Txt(root, "Price", "", new Rect(r.x, bottom - 130f, r.width, 40f), fExtra, 26f, gold ? C.Gold : C.Price, localize: false);
            foreach (var t in new[] { title, price })
            {
                t.enableAutoSizing = true;
                t.fontSizeMin = 14f;
                t.fontSizeMax = t.fontSize;
            }
            Button buy = PillButton(root, "BuyButton", new Rect(r.center.x - 90f, bottom - 82f, 180f, 58f), "BUY", 26f, Color.white, 1f, out TextMeshProUGUI buyLabel,
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
            if ((!thumbs.TryGetValue(itemId, out Sprite sprite) || sprite == null) && !Icons.TryGetValue(itemId, out sprite)) return;
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
                string art = Icons.ContainsKey(item.id) ? item.id : "coin";
                Icon(root, art, new Rect(r.center.x - 150f, r.y + 30f, 300f, 290f), Color.white);
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

            var graphics = Section(s.top, "Graphics", "GRAPHICS", new Rect(63f, 776f, 990f, 260f));
            SegmentedControl quality = QualitySelector(graphics, new Rect(123f, 900f, 870f, 95f));

            var controls = Section(s.top, "Controls", "CONTROLS", new Rect(63f, 1066f, 990f, 560f));
            Txt(controls, "SensitivityLabel", "Sensitivity", new Rect(123f, 1196f, 260f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            Slider sensitivity = RefSlider(controls, "SensitivitySlider", new Rect(388f, 1205f, 605f, 36f), 0.54f);
            Txt(controls, "VibrationLabel", "Vibration", new Rect(123f, 1296f, 500f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle vibration = RefSwitch(controls, "VibrationToggle", new Rect(860f, 1290f, 133f, 68f), true);
            Txt(controls, "LeftHandedLabel", "Left-handed", new Rect(123f, 1396f, 600f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle leftHanded = RefSwitch(controls, "LeftHandedToggle", new Rect(860f, 1390f, 133f, 68f), false);
            Txt(controls, "ContrastLabel", "High contrast", new Rect(123f, 1496f, 600f, 56f), fMedium, 40f, C.TextSoft, TextAlignmentOptions.Left);
            SwitchToggle contrast = RefSwitch(controls, "ContrastToggle", new Rect(860f, 1490f, 133f, 68f), false);

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
            Set(view, "leftHandedToggle", leftHanded);
            Set(view, "contrastToggle", contrast);
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
                seg.go.AddComponent<UIGradient>().SetColors(maroon ? Hex("#0E4555") : Hex("#14566A"), maroon ? Hex("#1C6C80") : Hex("#227A90"));
                var sel = Add(seg, "Selected", segRect);
                var selImg = sel.go.AddComponent<Image>();
                selImg.color = Hex("#F2C24E", 0.55f);
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

            var season = Section(s.top, "Season", "THE BOOK OF LEGENDS", new Rect(63f, 250f, 990f, 300f));
            Txt(season, "SeasonName", "8 CITIES · 8 LEGENDS", new Rect(123f, 360f, 870f, 50f), fBold, 32f, Hex("#8C95AA"));
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

            var gear = Section(s.top, "Gear", "GEAR", new Rect(63f, 1320f, 990f, 400f));
            (string slot, string icon, string label)[] items =
            {
                (RunnerSkin.Helmet, "gear_helmet", "VELOCITY VISOR"),
                (RunnerSkin.Gloves, "gear_gloves", "GRIP-TECH"),
                (RunnerSkin.Boots, "gear_boots", "SPRING BOOTS"),
            };
            for (int i = 0; i < items.Length; i++)
            {
                var r = new Rect(93f + i * 316f, 1430f, 298f, 262f);
                var card = Panel(gear, items[i].slot + "Card", r, Hex("#5A1C30"), Hex("#273256"), 22f, Hex("#7184B0"));
                Image art = Icon(card, items[i].icon, new Rect(r.x + 40f, r.y + 8f, r.width - 80f, 160f), Color.white);
                Txt(card, "Title", items[i].label, new Rect(r.x + 6f, r.y + 172f, r.width - 12f, 40f), fBold, 26f, Color.white);
                var status = Txt(card, "Status", "", new Rect(r.x + 6f, r.y + 212f, r.width - 12f, 36f), fExtra, 24f, C.Price, localize: false);
                AddGearToggle(MakeButton(card, card.rt.GetComponent<Image>()).gameObject, items[i].slot, items[i].label, art, status, items[i].icon);
            }

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

            RawPreview(s.top, "RunnerPreview", new Rect(158f, 1145f, 800f, 850f), preview);

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

            Txt(s.top, "DurationsHeader", "POWER-UP DURATIONS", new Rect(63f, 330f, 990f, 50f), fBold, 34f, Hex("#C9CCD6"));
            var rows = new List<Dictionary<string, object>>();
            for (int i = 0; i < PowerUpRows.Length; i++)
            {
                var pu = PowerUpRows[i];
                float y = 395f + i * 345f;
                var sec = Section(s.top, pu.id, pu.title, new Rect(63f, y, 990f, 325f));
                Icon(sec, pu.icon, new Rect(93f, y + 108f, 180f, 180f), Color.white);
                Txt(sec, "Desc", pu.desc, new Rect(300f, y + 104f, 720f, 40f), fMedium, 28f, Hex("#B5B9C6"), TextAlignmentOptions.Left);
                RectTransform fill = ProgressBar(sec, "Bar", new Rect(300f, y + 152f, 720f, 28f), 0.5f);
                var value = Txt(sec, "Value", "0", new Rect(300f, y + 186f, 300f, 46f), fExtra, 36f, Color.white, TextAlignmentOptions.Left, localize: false);
                var level = Txt(sec, "Level", "", new Rect(620f, y + 186f, 400f, 46f), fBold, 30f, C.TextGrey, TextAlignmentOptions.Right, localize: false);
                Button button = BoxButton(sec, "UpgradeButton", new Rect(400f, y + 240f, 520f, 76f), "", 32f, out TextMeshProUGUI cost, false);
                rows.Add(new Dictionary<string, object>
                {
                    { "bar", new Dictionary<string, object> { { "statId", pu.id }, { "value", value }, { "level", level }, { "fill", fill }, { "fullWidth", 714f }, { "valueFormat", "{0} SEC" } } },
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
                "Your runner moves forward on its own.\n\nSWIPE UP to jump.\nSWIPE DOWN to slide, or to drop fast from the air.\nSWIPE LEFT / RIGHT to change lanes.\n\nWhen Tepegoz chases you, tap THROW to hit him with melons. Four hits and he falls!\n\nCrash into an obstacle and the run ends. Retry instantly and beat the level!",
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
            Link(BoxButton(s.top, "CreditsButton", new Rect(313f, 1210f, 490f, 106f), "CREDITS"), MenuLink.LinkAction.Open, "credits");
            Txt(s.top, "Version", $"JUMPLINE GO  v{PlayerSettings.bundleVersion}", new Rect(63f, 1350f, 990f, 50f), fBold, 30f, C.TextDim, localize: false);
        }

        // ---------------------------------------------------------------------
        // Jenerik (aşama 12): docs/CREDITS.md tablolarından üretilir — kaynak listesi tek yerde kalır
        // ---------------------------------------------------------------------

        private const string CreditsDoc = "docs/CREDITS.md";

        /// <summary>CREDITS.md: "## Başlık" bölümleri ve tablo satırları → (bölüm, satırlar).</summary>
        private static List<(string section, List<string> lines)> ReadCredits()
        {
            var result = new List<(string, List<string>)>();
            if (!System.IO.File.Exists(CreditsDoc)) return result;
            List<string> current = null;
            foreach (string raw in System.IO.File.ReadAllLines(CreditsDoc))
            {
                string line = raw.Trim();
                if (line.StartsWith("## "))
                {
                    string h = line.Substring(3);
                    string key = h.StartsWith("Ses") ? "SOUNDS" : h.StartsWith("Müzik") ? "MUSIC" : h.StartsWith("3D") ? "3D AND TEXTURES" : "SOUNDS";
                    current = null;
                    foreach (var r in result) if (r.Item1 == key) current = r.Item2;
                    if (current == null) { current = new List<string>(); result.Add((key, current)); }
                    continue;
                }
                if (current == null || !line.StartsWith("|") || line.StartsWith("|---") || line.Contains("| Oyunda |") || line.Contains("| Kullanım |")) continue;
                string[] c = line.Trim('|').Split('|');
                for (int i = 0; i < c.Length; i++) c[i] = c[i].Trim();
                if (c.Length >= 4) current.Add($"{c[1]}  ·  {c[2]}");          // kayıt · sanatçı
                else if (c.Length == 3) current.Add($"{c[1]}  ·  {c[2]}");     // kaynak · lisans
            }
            return result;
        }

        private static void BuildCredits(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "CREDITS"), MenuLink.LinkAction.Back);
            var sections = ReadCredits();
            const float lineH = 50f, headH = 120f;
            float contentH = 360f;
            foreach (var (_, lines) in sections) contentH += headH + lines.Count * lineH + 30f;
            Box list = VerticalScroll(s.top, "CreditsList", new Rect(63f, 230f, 990f, 1730f), 230f + contentH);
            float y = 250f;
            Txt(list, "By", "A GAME BY", new Rect(63f, y, 990f, 50f), fBold, 32f, C.TextDim);
            Txt(list, "Author", "ALI OSMAN KESKIN", new Rect(63f, y + 50f, 990f, 80f), fExtra, 60f, Color.white, localize: false);
            Txt(list, "Tools", "Made with Unity and Blender. All other models, textures and UI were made for this game.",
                new Rect(103f, y + 140f, 910f, 110f), fMedium, 30f, C.TextSoft, wrap: true);
            y += 300f;
            int n = 0;
            foreach (var (section, lines) in sections)
            {
                Txt(list, "Head" + n, section, new Rect(63f, y + 30f, 990f, 70f), fExtra, 46f, C.Gold);
                Line(list, new Rect(363f, y + 102f, 390f, 2f), Hex("#3B4A70"));
                y += headH;
                foreach (string l in lines)
                {
                    var t = Txt(list, "Line" + n++, l, new Rect(83f, y, 950f, lineH), fMedium, 30f, C.TextSoft, localize: false);
                    t.enableAutoSizing = true;   // uzun kayıt adları satıra sığsın
                    t.fontSizeMin = 20f;
                    t.fontSizeMax = 30f;
                    y += lineH;
                }
                y += 30f;
            }
        }

        // ---------------------------------------------------------------------
        // Alt gezinme
        // ---------------------------------------------------------------------

        private static GameObject BuildBottomNav(Transform safe)
        {
            var rt = NewUI("BottomNav", safe);
            Stretch(rt);
            Box bot = Group(rt, "Bottom", 0f);
            var barImg = SkinImage(bot, "NavBar", new Rect(-20f, 1790f, 1156f, 230f), "navbar", true, 0.3f);
            barImg.raycastTarget = true;
            NavTile(bot, "Market", 112f, "shopping-cart", "MARKET", "market", 0);
            NavTile(bot, "Inventory", 335f, "chest", "COLLECTION", "inventory", 1);
            NavTile(bot, "Home", 558f, "swords", "PLAY", "home", 2);
            NavTile(bot, "Book", 781f, "book", "BOOK", "book", 3);
            NavTile(bot, "Map", 1004f, "map", "MAP", "map", 4);
            return rt.gameObject;
        }

        // docs/references/menu/home_v2.jpg — dosya biçimli kutular, ışık halkaları
        private static void NavTile(Box parent, string name, float cx, string icon, string label, string screenId, int index)
        {
            bool raised = index == 2;
            var r = raised ? new Rect(cx - 112f, 1700f, 224f, 196f) : new Rect(cx - 98f, 1772f, 196f, 160f);
            var tile = Add(parent, name + "Tile", r);
            var ti = tile.go.AddComponent<Image>();
            SkinSprite(ti, raised ? "tab_raised" : "tab", r, 0.3f);
            float isz = raised ? 132f : 104f;
            Icon(tile, icon, new Rect(r.center.x - isz * 0.5f, r.y + (raised ? 14f : 10f), isz, isz), Color.white);
            Txt(tile, "Label", label, new Rect(r.x - 6f, r.yMax - (raised ? 60f : 52f), r.width + 12f, 48f), fExtra, raised ? 32f : 26f, Color.white);
            Link(MakeButton(tile, ti), MenuLink.LinkAction.OpenRoot, screenId);
        }

        // ---------------------------------------------------------------------
        // Veri
        // ---------------------------------------------------------------------

        private static void LoadData()
        {
            config = GetOrCreateAsset<GameConfig>(ConfigPath, null);
            config.SetLevels(DefaultLevels());   // bölüm tanımları bu dosyada; her üretimde yenilenir
            EditorUtility.SetDirty(config);
            catalog = GetOrCreateAsset<ShopCatalog>(CatalogPath, null);
            catalog.SetItems(DefaultItems()); // içerik bu dosyada tanımlı; her üretimde yenilenir
            EditorUtility.SetDirty(catalog);
            stats = GetOrCreateAsset<CharacterStats>(StatsPath, s => s.SetStats(DefaultStats()));
            if (stats.Find("magnet") == null)   // eski özellik listesi (hız/zıplama/ivme) → güç süreleri
            {
                stats.SetStats(DefaultStats());
                EditorUtility.SetDirty(stats);
            }
        }

        /// <summary>
        /// Efsaneler haritası (8 durak). Zorluk eğrisi: hız +0.6/bölüm, tavan +0.7, engel boşluğu daralır, canavar
        /// kalbi 4→6, kaçış hızı artar. built: canavarı ve sahnesi hazır olanlar (şimdilik yalnız Tepegöz).
        /// </summary>
        private static GameConfig.LevelSpec[] DefaultLevels()
        {
            (string city, string legend, string name)[] stops =
            {
                ("DIYARBAKIR", "tepegoz", "TEPEGOZ"), ("MARDIN", "sahmeran", "SHAHMARAN"), ("HASANKEYF", "humbaba", "HUMBABA"),
                ("SANLIURFA", "stone_guardian", "STONE GUARDIAN"), ("VAN", "van_monster", "VAN LAKE MONSTER"),
                ("KARS", "karakoncolos", "KARAKONCOLOS"), ("???", "lamassu", "LAMASSU"), ("???", "dragon", "THE DRAGON"),
            };
            const int built = 8;   // 8 efsanenin hepsi hazır
            // canavar karakteri: (fırlatma, şerit, sıyrılma) — açılış konuşmalarındaki ipuçlarıyla uyumlu
            var personality = new System.Collections.Generic.Dictionary<string, (float t, float l, float d)>
            {
                { "tepegoz", (1f, 1f, 0f) },
                { "sahmeran", (1f, 1.5f, 0.1f) },          // kıvrak: sık şerit değiştirir
                { "humbaba", (1.4f, 0.7f, -0.15f) },       // çok atar, yavaş döner (sırtı açık)
                { "stone_guardian", (1.2f, 0.8f, -1f) },   // taş: hiç sıyrılamaz, ama çok kalpli
                { "van_monster", (1f, 1.6f, 0.15f) },      // kaygan
                { "karakoncolos", (1.3f, 1.3f, 0.1f) },
                { "lamassu", (1.2f, 1.2f, 0.2f) },         // kanatlı: hızlı
                { "dragon", (1.6f, 1.4f, 0.2f) },          // final
            };   // canavarı hazır olan bölümler (… Van Gölü Canavarı, Karakoncolos)
            var specs = new GameConfig.LevelSpec[stops.Length];
            for (int i = 0; i < stops.Length; i++)
                specs[i] = new GameConfig.LevelSpec
                {
                    city = stops[i].city, legend = stops[i].legend, displayName = stops[i].name, built = i < built,
                    bossThrowRate = personality[stops[i].legend].t, bossLaneRate = personality[stops[i].legend].l,
                    bossDodgeBonus = personality[stops[i].legend].d,
                    roarPitch = stops[i].legend switch
                    {
                        "sahmeran" => 1.45f, "humbaba" => 0.8f, "stone_guardian" => 0.62f, "van_monster" => 1.25f,
                        "karakoncolos" => 1.12f, "lamassu" => 0.9f, "dragon" => 0.7f, _ => 1f,
                    },
                    // denge (telefon testi: "çok zor"): yumuşak hız eğrisi, seyrek başlayan parkur
                    runSpeed = 10.5f + 0.45f * i, maxSpeed = 16.5f + 0.6f * i,
                    obstacleGap = new Vector2(Mathf.Max(8.5f, 12f - 0.45f * i), Mathf.Max(12.5f, 18f - 0.6f * i)),
                    courseSpacing = 1.25f - 0.03f * i,
                    bossHearts = Mathf.Min(6, 4 + i / 3), bossDrift = 1f + 0.07f * i,
                    stoneTint = stops[i].city == "MARDIN" ? new Color(2.1f, 1.75f, 1.2f)      // Mardin: bal rengi kalker
                              : stops[i].city == "HASANKEYF" ? new Color(1.75f, 1.35f, 1.05f)   // Hasankeyf: kırmızımsı kum taşı
                              : stops[i].city == "SANLIURFA" ? new Color(2.0f, 1.85f, 1.55f)    // Şanlıurfa: açık kireçtaşı
                              : stops[i].city == "VAN" ? new Color(1.25f, 1.45f, 1.6f)          // Van: gri-mavi volkanik taş
                              : stops[i].city == "KARS" ? new Color(1.9f, 2.05f, 2.3f)          // Kars: karlı, soğuk
                              : stops[i].legend == "lamassu" ? new Color(2.0f, 1.65f, 1.1f)     // kayıp şehir: altın kum
                              : stops[i].legend == "dragon" ? new Color(1.6f, 0.95f, 0.8f)      // ejderha yuvası: volkanik kızıl
                              : Color.white,
                };
            return specs;
        }

        private static List<ShopItem> DefaultItems()
        {
            ShopItem I(string id, string name, ItemCategory cat, CurrencyType cur, int price, int reward = 0) =>
                new ShopItem { id = id, displayName = name, category = cat, currency = cur, price = price, rewardGold = reward };
            ShopItem Usd(string id, string name, ItemCategory cat, Rarity rarity, float usd, int gold = 0, int gems = 0, string skin = null, string sub = null) =>
                new ShopItem { id = id, displayName = name, subtitle = sub, category = cat, rarity = rarity, priceUsd = usd, rewardGold = gold, rewardGems = gems, rewardSkin = skin };
            ShopItem Gear(string variant, string slot, string name, Rarity rarity, float usd) =>
                new ShopItem { id = $"{variant}_{slot}", displayName = name, category = ItemCategory.Gear, rarity = rarity, priceUsd = usd, gearSlot = slot, gearVariant = variant };

            return new List<ShopItem>
            {
                // Kostümler (altın / elmas) — menu/market_v2.jpg
                I("rookie", "ROOKIE", ItemCategory.Skin, CurrencyType.Gold, 0),
                I("stealth", "STEALTH", ItemCategory.Skin, CurrencyType.Gems, 200),
                I("velocity", "VELOCITY", ItemCategory.Skin, CurrencyType.Gold, 1500),
                I("titanium", "TITANIUM", ItemCategory.Skin, CurrencyType.Gold, 3000),
                I("glitch", "GLITCH", ItemCategory.Skin, CurrencyType.Gems, 300),
                I("crimson", "CRIMSON", ItemCategory.Skin, CurrencyType.Gold, 1200),
                I("frost", "FROST", ItemCategory.Skin, CurrencyType.Gems, 150),
                I("blaze", "BLAZE", ItemCategory.Skin, CurrencyType.Gold, 2500),

                // Fırsatlar — market/offers.jpg
                Usd("velocity_pack", "VELOCITY PACK", ItemCategory.Bundle, Rarity.Epic, 9.99f, gems: 500, skin: "velocity", sub: "SPECIAL RACER"),
                Usd("velocity_bundle", "VELOCITY", ItemCategory.Bundle, Rarity.Rare, 14.99f, gold: 10000, gems: 1000, sub: "SPECIAL BUNDLE"),
                Usd("offer_stealth", "NIGHT RUNNER", ItemCategory.Bundle, Rarity.Common, 2.99f, skin: "stealth"),
                Usd("offer_titanium", "CHROME FLASH", ItemCategory.Bundle, Rarity.Rare, 2.99f, skin: "titanium"),
                Usd("offer_crimson", "CRIMSON SKIN", ItemCategory.Bundle, Rarity.Epic, 3.99f, skin: "crimson"),
                Usd("offer_glitch", "GLITCH SKIN", ItemCategory.Bundle, Rarity.Rare, 4.99f, skin: "glitch"),
                Usd("offer_velocity", "VELOCITY SKIN", ItemCategory.Bundle, Rarity.Epic, 3.99f, skin: "velocity"),
                Usd("offer_blaze", "BLAZE SKIN", ItemCategory.Bundle, Rarity.Common, 5.99f, skin: "blaze"),
                Usd("pack_starter", "STARTER BUNDLE", ItemCategory.Currency, Rarity.Common, 7.99f, gems: 800),
                Usd("pack_value", "VALUE PACK", ItemCategory.Currency, Rarity.Epic, 25.99f, gems: 3000),
                Usd("pack_goldbundle", "GOLD BUNDLE", ItemCategory.Currency, Rarity.Gold, 29.99f, gold: 30000),

                // Ekipman — market/gear.jpg
                Gear("stealth", "helmet", "STEALTH HELMET", Rarity.Rare, 4.99f),
                Gear("velocity", "helmet", "VELOCITY HELMET", Rarity.Epic, 7.99f),
                Gear("titanium", "helmet", "TITANIUM HELMET", Rarity.Legendary, 9.99f),
                Gear("stealth", "gloves", "STEALTH GLOVES", Rarity.Rare, 3.99f),
                Gear("velocity", "gloves", "VELOCITY GLOVES", Rarity.Epic, 5.99f),
                Gear("titanium", "gloves", "TITANIUM GLOVES", Rarity.Legendary, 8.99f),
                Gear("stealth", "boots", "STEALTH BOOTS", Rarity.Rare, 5.99f),
                Gear("velocity", "boots", "VELOCITY BOOTS", Rarity.Epic, 8.99f),
                Gear("titanium", "boots", "TITANIUM BOOTS", Rarity.Legendary, 12.99f),

                // Güçler — market/boosts.jpg
                Usd("magnet", "COIN MAGNET", ItemCategory.Boost, Rarity.Rare, 1.99f),
                Usd("double_gold", "DOUBLE GOLD", ItemCategory.Boost, Rarity.Epic, 2.99f),
                Usd("speed_boost", "SPEED BOOST", ItemCategory.Boost, Rarity.Rare, 1.99f),
                Usd("shield", "SHIELD", ItemCategory.Boost, Rarity.Rare, 1.99f),

                // Para — market/gold.jpg, gems.jpg
                Usd("gold_small", "Pouch of Coins", ItemCategory.Currency, Rarity.Common, 1.99f, gold: 1000),
                Usd("gold_medium", "Stack of Coins", ItemCategory.Currency, Rarity.Rare, 9.99f, gold: 5000),
                Usd("gold_large", "Vault of Coins", ItemCategory.Currency, Rarity.Epic, 49.99f, gold: 20000),
                Usd("gem_small", "Gem Pouch", ItemCategory.Currency, Rarity.Common, 1.99f, gems: 500),
                Usd("gem_medium", "Gem Cluster", ItemCategory.Currency, Rarity.Rare, 9.99f, gems: 2500),
                Usd("gem_large", "Gem Cache", ItemCategory.Currency, Rarity.Epic, 49.99f, gems: 10000),
            };
        }

        /// <summary>Güç süreleri (sn): 5 kademe, her kademe +2 sn (yol haritası aşama 05). Maliyet = taban × (kademe+1).</summary>
        private static List<StatDefinition> DefaultStats() => new List<StatDefinition>
        {
            new StatDefinition { id = "magnet", displayName = "MAGNET", baseValue = 8, perLevel = 2, maxValue = 18, maxLevel = 5, upgradeCostBase = 600 },
            new StatDefinition { id = "shield", displayName = "SHIELD", baseValue = 8, perLevel = 2, maxValue = 18, maxLevel = 5, upgradeCostBase = 700 },
            new StatDefinition { id = "double_gold", displayName = "DOUBLE GOLD", baseValue = 10, perLevel = 2, maxValue = 20, maxLevel = 5, upgradeCostBase = 800 },
            new StatDefinition { id = "speed_boost", displayName = "SPEED BOOST", baseValue = 5, perLevel = 2, maxValue = 15, maxLevel = 5, upgradeCostBase = 900 },
        };

        private static readonly (string id, string title, string icon, string desc)[] PowerUpRows =
        {
            ("magnet", "MAGNET", "magnet", "Pulls nearby coins to you."),
            ("shield", "SHIELD", "shield", "Absorbs one crash."),
            ("double_gold", "DOUBLE GOLD", "double_gold", "Every coin counts twice."),
            ("speed_boost", "SPEED BOOST", "speed_boost", "Run faster, smash through obstacles."),
        };
    }
}
