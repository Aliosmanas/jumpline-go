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
        public const string VibrationKey = "jl.vibration";
        public const string GoldKey = "jl.gold";
        public const string GemsKey = "jl.gems";
        public const string EquippedSkinKey = "jl.equippedSkin";
        public const string LanguageKey = "jl.language";
        public const string DailyClaimKey = "jl.dailyClaim";
        public const string OwnedPrefix = "jl.owned.";
        public const string BoostPrefix = "jl.boost.";
        public const string StatPrefix = "jl.stat.";

        public const int FirstLevel = 1;
        public const float DefaultMusicVolume = 0.6f;
        public const float DefaultSfxVolume = 0.6f;
        public const float DefaultSensitivity = 0.55f;
        public const int GraphicsQualityCount = 4; // Low, Med, High, Ultra
        public const int DefaultGraphicsQuality = 1;
        public const int DefaultGold = 5000;
        public const int DefaultGems = 250;
        public const string DefaultSkin = "rookie";
        public const string DefaultLanguage = "en";

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
        public static bool UnlimitedCurrency = Debug.isDebugBuild;
        public const int UnlimitedAmount = 999999;

        public static int Gold
        {
            get => UnlimitedCurrency ? UnlimitedAmount : PlayerPrefs.GetInt(GoldKey, DefaultGold);
            set { if (!UnlimitedCurrency) PlayerPrefs.SetInt(GoldKey, Mathf.Max(0, value)); }
        }

        public static int Gems
        {
            get => UnlimitedCurrency ? UnlimitedAmount : PlayerPrefs.GetInt(GemsKey, DefaultGems);
            set { if (!UnlimitedCurrency) PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value)); }
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

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set => PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
        }

        public static string Language
        {
            get => PlayerPrefs.GetString(LanguageKey, DefaultLanguage);
            set => PlayerPrefs.SetString(LanguageKey, value);
        }

        public static void Flush() => PlayerPrefs.Save();
    }
}
