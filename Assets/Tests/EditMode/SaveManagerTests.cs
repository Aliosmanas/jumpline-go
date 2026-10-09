using System.Collections.Generic;
using Jumpline.Player;
using Jumpline.Save;
using Jumpline.Shop;
using NUnit.Framework;
using UnityEngine;

namespace Jumpline.Tests
{
    public class SaveManagerTests
    {
        private const string TestSkin = "test_skin";
        private const string TestBoost = "test_boost";
        private const string TestStat = "test_stat";

        private static readonly string[] IntKeys =
        {
            SaveManager.UnlockedLevelKey, SaveManager.MutedKey, SaveManager.GraphicsQualityKey, SaveManager.VibrationKey,
            SaveManager.GoldKey, SaveManager.GemsKey,
            SaveManager.OwnedPrefix + TestSkin, SaveManager.BoostPrefix + TestBoost, SaveManager.StatPrefix + TestStat,
        };
        private static readonly string[] FloatKeys =
            { SaveManager.MusicVolumeKey, SaveManager.SfxVolumeKey, SaveManager.SensitivityKey };
        private static readonly string[] StringKeys =
            { SaveManager.EquippedSkinKey, SaveManager.LanguageKey, SaveManager.DailyClaimKey };

        private bool unlimited;
        private readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
        private readonly Dictionary<string, float> savedFloats = new Dictionary<string, float>();
        private readonly Dictionary<string, string> savedStrings = new Dictionary<string, string>();

        [SetUp]
        public void SetUp()
        {
            unlimited = SaveManager.UnlimitedCurrency;
            SaveManager.UnlimitedCurrency = false; // cüzdan testleri gerçek bakiye ister
            // Geliştiricinin gerçek kaydını koru.
            savedInts.Clear();
            savedFloats.Clear();
            savedStrings.Clear();
            foreach (string k in IntKeys) { if (PlayerPrefs.HasKey(k)) savedInts[k] = PlayerPrefs.GetInt(k); PlayerPrefs.DeleteKey(k); }
            foreach (string k in FloatKeys) { if (PlayerPrefs.HasKey(k)) savedFloats[k] = PlayerPrefs.GetFloat(k); PlayerPrefs.DeleteKey(k); }
            foreach (string k in StringKeys) { if (PlayerPrefs.HasKey(k)) savedStrings[k] = PlayerPrefs.GetString(k); PlayerPrefs.DeleteKey(k); }
        }

        [TearDown]
        public void TearDown()
        {
            SaveManager.UnlimitedCurrency = unlimited;
            foreach (string k in IntKeys) PlayerPrefs.DeleteKey(k);
            foreach (string k in FloatKeys) PlayerPrefs.DeleteKey(k);
            foreach (string k in StringKeys) PlayerPrefs.DeleteKey(k);
            foreach (var kv in savedInts) PlayerPrefs.SetInt(kv.Key, kv.Value);
            foreach (var kv in savedFloats) PlayerPrefs.SetFloat(kv.Key, kv.Value);
            foreach (var kv in savedStrings) PlayerPrefs.SetString(kv.Key, kv.Value);
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshSave_OnlyFirstLevelUnlocked()
        {
            Assert.AreEqual(1, SaveManager.UnlockedLevel);
            Assert.IsTrue(SaveManager.IsLevelUnlocked(1));
            Assert.IsFalse(SaveManager.IsLevelUnlocked(2));
            Assert.IsFalse(SaveManager.IsLevelUnlocked(0));
        }

        [Test]
        public void UnlockUpTo_NeverGoesBackwards()
        {
            SaveManager.UnlockUpTo(4);
            SaveManager.UnlockUpTo(2);
            Assert.AreEqual(4, SaveManager.UnlockedLevel);
            Assert.IsTrue(SaveManager.IsLevelUnlocked(3));
            Assert.IsFalse(SaveManager.IsLevelUnlocked(5));
        }

        [Test]
        public void Volumes_DefaultAndClamp()
        {
            Assert.AreEqual(SaveManager.DefaultMusicVolume, SaveManager.MusicVolume, 1e-5f);
            SaveManager.MusicVolume = 2f;
            SaveManager.SfxVolume = -1f;
            Assert.AreEqual(1f, SaveManager.MusicVolume, 1e-5f);
            Assert.AreEqual(0f, SaveManager.SfxVolume, 1e-5f);
        }

        [Test]
        public void Settings_DefaultsAndClamp()
        {
            Assert.IsFalse(SaveManager.Muted);
            Assert.IsTrue(SaveManager.Vibration);
            Assert.AreEqual(SaveManager.DefaultGraphicsQuality, SaveManager.GraphicsQuality);
            // ilk açılışta cihaz dili Türkçeyse Türkçe, değilse İngilizce
            string expected = Application.systemLanguage == SystemLanguage.Turkish ? "tr" : SaveManager.DefaultLanguage;
            Assert.AreEqual(expected, SaveManager.Language);

            SaveManager.GraphicsQuality = 99;
            Assert.AreEqual(SaveManager.GraphicsQualityCount - 1, SaveManager.GraphicsQuality);
        }

        [Test]
        public void UnlimitedCurrency_IgnoresSpending()
        {
            SaveManager.UnlimitedCurrency = true;
            SaveManager.Gold -= 500;
            Assert.AreEqual(SaveManager.UnlimitedAmount, SaveManager.Gold);
            SaveManager.UnlimitedCurrency = false;
            Assert.AreEqual(SaveManager.DefaultGold, SaveManager.Gold);
        }

        [Test]
        public void DailyChallenge_ClaimedOncePerDay()
        {
            Assert.IsFalse(SaveManager.DailyClaimedToday);
            SaveManager.MarkDailyClaimed();
            Assert.IsTrue(SaveManager.DailyClaimedToday);
        }

        [Test]
        public void Shop_BuySkin_DeductsAndOwns()
        {
            var skin = new ShopItem { id = TestSkin, category = ItemCategory.Skin, currency = CurrencyType.Gems, price = 200 };
            Assert.AreEqual(PurchaseResult.Success, ShopService.TryPurchase(skin));
            Assert.AreEqual(SaveManager.DefaultGems - 200, SaveManager.Gems);
            Assert.IsTrue(SaveManager.IsOwned(TestSkin));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, ShopService.TryPurchase(skin));
            Assert.IsTrue(ShopService.TryEquip(skin));
            Assert.AreEqual(TestSkin, SaveManager.EquippedSkin);
        }

        [Test]
        public void Shop_NotEnoughFunds_ChangesNothing()
        {
            var boost = new ShopItem { id = TestBoost, category = ItemCategory.Boost, currency = CurrencyType.Gold, price = SaveManager.DefaultGold + 1 };
            Assert.AreEqual(PurchaseResult.NotEnoughFunds, ShopService.TryPurchase(boost));
            Assert.AreEqual(SaveManager.DefaultGold, SaveManager.Gold);
            Assert.AreEqual(0, SaveManager.GetBoostCount(TestBoost));
        }

        [Test]
        public void Shop_CurrencyPack_ExchangesGemsForGold()
        {
            var pack = new ShopItem { id = "pack", category = ItemCategory.Currency, currency = CurrencyType.Gems, price = 50, rewardGold = 1000 };
            Assert.AreEqual(PurchaseResult.Success, ShopService.TryPurchase(pack));
            Assert.AreEqual(SaveManager.DefaultGems - 50, SaveManager.Gems);
            Assert.AreEqual(SaveManager.DefaultGold + 1000, SaveManager.Gold);
        }

        [Test]
        public void Stats_UpgradeCostsGoldAndStopsAtMax()
        {
            var stat = new StatDefinition { id = TestStat, baseValue = 10, perLevel = 5, maxValue = 20, maxLevel = 2, upgradeCostBase = 100 };
            Assert.AreEqual(10, CharacterStats.Value(stat));
            Assert.IsTrue(CharacterStats.TryUpgrade(stat));   // 100
            Assert.IsTrue(CharacterStats.TryUpgrade(stat));   // 200
            Assert.IsFalse(CharacterStats.TryUpgrade(stat));  // maks
            Assert.AreEqual(20, CharacterStats.Value(stat));
            Assert.AreEqual(SaveManager.DefaultGold - 300, SaveManager.Gold);
        }
    }
}
