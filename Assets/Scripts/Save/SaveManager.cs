using System;
using UnityEngine;

namespace Jumpline.Save
{
    /// <summary>
    /// Cihazda kalıcı oyuncu verisi (PlayerPrefs). İlerleme ve cüzdan hemen diske yazılır;
    /// ayarlar sık değiştiği için <see cref="Flush"/> çağrılınca yazılır.
    /// </summary>
    public static class SaveManager
    {
        public const string UnlockedLevelKey = "jl.unlockedLevel";
        public const string MusicVolumeKey = "jl.musicVolume";
        public const string SfxVolumeKey = "jl.sfxVolume";
        public const string MutedKey = "jl.muted";
        public const string GraphicsQualityKey = "jl.graphicsQuality";
        public const string SensitivityKey = "jl.sensitivity";
        public const string LeftHandedKey = "jl.leftHanded";
        public const string HighContrastKey = "jl.highContrast";
        public const string CurrencySigKey = "jl.sig";
        public const string VibrationKey = "jl.vibration";
        public const string GoldKey = "jl.gold";
        public const string GemsKey = "jl.gems";
        public const string EquippedSkinKey = "jl.equippedSkin";
        public const string LanguageKey = "jl.language";
        private const string LanguageMigrationKey = "jl.language.tr2026";
        public const string DailyClaimKey = "jl.dailyClaim";
        public const string OwnedPrefix = "jl.owned.";
        public const string BoostPrefix = "jl.boost.";
        public const string StatPrefix = "jl.stat.";
        public const string LegendPrefix = "jl.legend.";
        public const string QuickRaceBestKey = "jl.quickBest";
        public const string StarsPrefix = "jl.stars.";
        public const string SaveVersionKey = "jl.saveVersion";
        /// <summary>Kayıt biçimi sürümü. Artınca <see cref="Migrate"/> eski kaydı yeni biçime taşır (oyuncu ilerlemesi kaybolmaz).</summary>
        public const int CurrentSaveVersion = 2;

        public const int FirstLevel = 1;
        public const float DefaultMusicVolume = 0.6f;
        public const float DefaultSfxVolume = 0.6f;
        public const float DefaultSensitivity = 0.55f;
        public const int GraphicsQualityCount = 4; // Low, Med, High, Ultra
        public const int DefaultGraphicsQuality = 1;
        public const int DefaultGold = 500;    // ekonomi (aşama 13): kostüm almak hedef olsun; eski kayıtlar etkilenmez
        public const int DefaultGems = 50;
        public const string DefaultSkin = "rookie";
        public const string DefaultLanguage = "tr";   // arayüz referansları Türkçe (13 Eki 2026)

        // ---------- Kayıt sürümü ----------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Migrate();
            VerifyCurrency();
#if JL_TEST_BUILD
            UnlimitedCurrency = true;   // test APK'sı (AndroidBuild.BuildApk): sınırsız altın/elmas; mağaza sürümünde yok
#endif
        }

        /// <summary>
        /// v1 → v2: geliştirme ekranı karakter özelliklerinden (hız/zıplama/ivme) güç sürelerine geçti — eski seviyelere
        /// harcanan altın iade edilir. Yeni sürümler buraya bir adım daha ekler.
        /// </summary>
        public static void Migrate()
        {
            int v = PlayerPrefs.GetInt(SaveVersionKey, PlayerPrefs.HasKey(GoldKey) ? 1 : CurrentSaveVersion);
            if (v >= CurrentSaveVersion) { PlayerPrefs.SetInt(SaveVersionKey, CurrentSaveVersion); return; }
            if (v < 2)
            {
                (string id, int costBase)[] old = { ("speed", 500), ("jump", 500), ("momentum", 400) };
                int refund = 0;
                foreach (var (id, costBase) in old)
                {
                    int level = PlayerPrefs.GetInt(StatPrefix + id, 0);
                    for (int i = 0; i < level; i++) refund += costBase * (i + 1);
                    PlayerPrefs.DeleteKey(StatPrefix + id);
                }
                if (refund > 0) { PlayerPrefs.SetInt(GoldKey, PlayerPrefs.GetInt(GoldKey, DefaultGold) + refund); SignCurrency(); }
            }
            PlayerPrefs.SetInt(SaveVersionKey, CurrentSaveVersion);
            PlayerPrefs.Save();
        }

        // ---------- İlerleme ----------

        /// <summary>Oynanabilen en yüksek bölüm (1 tabanlı).</summary>
        public static int UnlockedLevel => Mathf.Max(FirstLevel, PlayerPrefs.GetInt(UnlockedLevelKey, FirstLevel));

        public static bool IsLevelUnlocked(int level) => level >= FirstLevel && level <= UnlockedLevel;

        /// <summary>Bölümü (ve öncekileri) açar. Daha düşük bir değer ilerlemeyi geri almaz.</summary>
        public static void UnlockUpTo(int level)
        {
            if (level <= UnlockedLevel) return;
            PlayerPrefs.SetInt(UnlockedLevelKey, level);
            PlayerPrefs.Save();
        }

        /// <summary>Bölümün en iyi yıldızı (0-3).</summary>
        public static int GetStars(int level) => PlayerPrefs.GetInt(StarsPrefix + level, 0);

        /// <summary>Daha iyiyse kaydeder.</summary>
        public static void SubmitStars(int level, int stars)
        {
            if (stars <= GetStars(level)) return;
            PlayerPrefs.SetInt(StarsPrefix + level, Mathf.Clamp(stars, 0, 3));
            PlayerPrefs.Save();
        }

        /// <summary>Hızlı Yarış en iyi puanı.</summary>
        public static int QuickRaceBest => PlayerPrefs.GetInt(QuickRaceBestKey, 0);

        /// <summary>Yeni rekorsa kaydeder ve true döner.</summary>
        public static bool SubmitQuickRace(int score)
        {
            if (score <= QuickRaceBest) return false;
            PlayerPrefs.SetInt(QuickRaceBestKey, score);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Efsaneler Kitabı: yenilen canavarın sayfası açık mı.</summary>
        public static bool HasLegend(string id) => PlayerPrefs.GetInt(LegendPrefix + id, 0) == 1;

        /// <summary>Canavar yenildi: kitaba sayfası döner. İlk kez açıldıysa true.</summary>
        public static bool UnlockLegend(string id)
        {
            if (HasLegend(id)) return false;
            PlayerPrefs.SetInt(LegendPrefix + id, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool DailyClaimedToday => PlayerPrefs.GetString(DailyClaimKey, "") == Today;

        public static void MarkDailyClaimed()
        {
            PlayerPrefs.SetString(DailyClaimKey, Today);
            PlayerPrefs.Save();
        }

        private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

        // ---------- Cüzdan ve eşyalar ----------

        /// <summary>
        /// Geliştirici modu: altın/elmas sınırsız (harcama düşmez). Editörde ve Development build'de açık,
        /// yayın (release) build'inde otomatik kapalı.
        /// </summary>
        public static bool UnlimitedCurrency = Application.isEditor;   // telefon yapılarında (test APK dahil) gerçek ekonomi
        public const int UnlimitedAmount = 999999;

        public static int Gold
        {
            get => UnlimitedCurrency ? UnlimitedAmount : PlayerPrefs.GetInt(GoldKey, DefaultGold);
            set { if (!UnlimitedCurrency) { PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value)); SignCurrency(); } }
        }

        public static int Gems
        {
            get => UnlimitedCurrency ? UnlimitedAmount : PlayerPrefs.GetInt(GemsKey, DefaultGems);
            set { if (!UnlimitedCurrency) { PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value)); SignCurrency(); } }
        }

        // ---------- Ekipman (takılı / çıkarılmış) ----------

        public const string GearPrefix = "jl.gear.";

        public static event Action GearChanged;

        public const string GearVariantPrefix = "jl.gearVariant.";
        public const string DefaultGearVariant = "standard";

        /// <summary>Yuvaya takılı ekipman çeşidi (standard, stealth, velocity, titanium).</summary>
        public static string GetGearVariant(string slot) => PlayerPrefs.GetString(GearVariantPrefix + slot, DefaultGearVariant);

        public static void SetGearVariant(string slot, string variant)
        {
            PlayerPrefs.SetString(GearVariantPrefix + slot, variant);
            PlayerPrefs.SetInt(GearPrefix + slot, 1); // çeşit seçmek takar
            PlayerPrefs.Save();
            GearChanged?.Invoke();
        }

        public static bool IsGearEquipped(string slot) => PlayerPrefs.GetInt(GearPrefix + slot, 1) == 1;

        public static void SetGearEquipped(string slot, bool equipped)
        {
            PlayerPrefs.SetInt(GearPrefix + slot, equipped ? 1 : 0);
            PlayerPrefs.Save();
            GearChanged?.Invoke();
        }

        public static bool IsOwned(string itemId) => itemId == DefaultSkin || PlayerPrefs.GetInt(OwnedPrefix + itemId, 0) == 1;

        public static void SetOwned(string itemId) => PlayerPrefs.SetInt(OwnedPrefix + itemId, 1);

        public static int GetBoostCount(string itemId) => PlayerPrefs.GetInt(BoostPrefix + itemId, 0);

        public static void AddBoost(string itemId, int amount) =>
            PlayerPrefs.SetInt(BoostPrefix + itemId, Mathf.Max(0, GetBoostCount(itemId) + amount));

        public static string EquippedSkin
        {
            get => PlayerPrefs.GetString(EquippedSkinKey, DefaultSkin);
            set => PlayerPrefs.SetString(EquippedSkinKey, value);
        }

        public static int GetStatLevel(string statId) => PlayerPrefs.GetInt(StatPrefix + statId, 0);

        public static void SetStatLevel(string statId, int level) => PlayerPrefs.SetInt(StatPrefix + statId, Mathf.Max(0, level));

        // ---------- Ayarlar ----------

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume);
            set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume);
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutedKey, 0) == 1;
            set => PlayerPrefs.SetInt(MutedKey, value ? 1 : 0);
        }

        public static int GraphicsQuality
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(GraphicsQualityKey, DefaultGraphicsQuality), 0, GraphicsQualityCount - 1);
            set => PlayerPrefs.SetInt(GraphicsQualityKey, Mathf.Clamp(value, 0, GraphicsQualityCount - 1));
        }

        public static float Sensitivity
        {
            get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
            set => PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp01(value));
        }

        public static bool LeftHanded
        {
            get => PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;
            set => PlayerPrefs.SetInt(LeftHandedKey, value ? 1 : 0);
        }

        public static bool HighContrast
        {
            get => PlayerPrefs.GetInt(HighContrastKey, 0) == 1;
            set => PlayerPrefs.SetInt(HighContrastKey, value ? 1 : 0);
        }

        // ---------- Basit hile koruması ----------
        // Altın/elmas kayıt dosyasında elle değiştirilirse imza tutmaz → CurrencyTampered (ileride liderlik tablosundan
        // ayırmak için). İlerleme SİLİNMEZ: yanlış alarm oyuncuya zarar vermesin.

        private const string SigSalt = "jl-sur-1453";

        /// <summary>Bu açılışta para kaydı imzayla uyuşmadı.</summary>
        public static bool CurrencyTampered { get; private set; }

        private static string CurrencySignature()
        {
            int gold = PlayerPrefs.GetInt(GoldKey, DefaultGold), gems = PlayerPrefs.GetInt(GemsKey, DefaultGems);
            unchecked
            {
                int h = 17;
                foreach (char c in SigSalt + gold + ":" + gems) h = h * 31 + c;
                return h.ToString("X8");
            }
        }

        private static void SignCurrency() => PlayerPrefs.SetString(CurrencySigKey, CurrencySignature());

        public static void VerifyCurrency()
        {
            string sig = PlayerPrefs.GetString(CurrencySigKey, "");
            CurrencyTampered = sig.Length > 0 && sig != CurrencySignature();
            if (CurrencyTampered) Debug.LogWarning("[Save] para kaydı imzası tutmuyor (elle değiştirilmiş olabilir)");
            SignCurrency();
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
        }

        public static string Language
        {
            get
            {
                // 13 Eki 2026: arayüz Türkçe tasarlandı → eski "en" kaydı bir kez Türkçeye çevrilir (sonra oyuncu seçimi korunur)
                if (PlayerPrefs.GetInt(LanguageMigrationKey, 0) == 0)
                {
                    PlayerPrefs.SetInt(LanguageMigrationKey, 1);
                    PlayerPrefs.SetString(LanguageKey, DefaultLanguage);
                }
                return PlayerPrefs.GetString(LanguageKey, DefaultLanguage);
            }
            set => PlayerPrefs.SetString(LanguageKey, value);
        }

        public static void Flush() => PlayerPrefs.Save();
    }
}
