using System;
using System.Collections.Generic;
using Jumpline.Save;

namespace Jumpline.Core
{
    /// <summary>
    /// Basit iki dilli tablo. Anahtar = İngilizce metin; Türkçe karşılığı yoksa İngilizce gösterilir.
    /// </summary>
    public static class Localization
    {
        public const string English = "en";
        public const string Turkish = "tr";

        public static event Action Changed;

        public static string Current => SaveManager.Language;

        public static void SetLanguage(string code)
        {
            if (code == Current) return;
            SaveManager.Language = code;
            SaveManager.Flush();
            Changed?.Invoke();
        }

        public static bool Has(string key) => key != null && Tr.ContainsKey(key);

        public static string Get(string key) =>
            Current == Turkish && key != null && Tr.TryGetValue(key, out string value) ? value : key;

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        private static readonly Dictionary<string, string> Tr = new Dictionary<string, string>
        {
            // Ana ekran ve gezinme
            { "SEASON 4: VELOCITY", "SEZON 4: HIZ" },
            { "PLAY", "OYNA" },
            { "QUICK RACE", "HIZLI YARIŞ" },
            { "DAILY CHALLENGE", "GÜNLÜK GÖREV" },
            { "MARKET", "MARKET" },
            { "EVENTS", "ETKİNLİKLER" },
            { "INVENTORY", "ENVANTER" },
            { "CHARACTER", "KARAKTER" },
            { "SETTINGS", "AYARLAR" },
            { "BACK", "GERİ" },

            // Karakter
            { "CHARACTER / PROFILE", "KARAKTER / PROFİL" },
            { "STATS", "ÖZELLİKLER" },
            { "GEAR", "EKİPMAN" },
            { "SPEED", "HIZ" },
            { "JUMP", "ZIPLAMA" },
            { "MOMENTUM", "İVME" },
            { "Low", "Düşük" },
            { "Medium", "Orta" },
            { "High", "Yüksek" },
            { "Very High", "Çok Yüksek" },
            { "SUIT:", "KIYAFET:" },
            { "BOOTS:", "BOTLAR:" },
            { "GLOVES:", "ELDİVEN:" },
            { "ACCESSORY:", "AKSESUAR:" },
            { "HELMET:", "KASK:" },
            { "REMOVED", "ÇIKARILDI" },
            { "OFFERS", "FIRSATLAR" },
            { "GEAR SHOP", "EKİPMAN MAĞAZASI" },
            { "BOOSTS SHOP", "GÜÇ MAĞAZASI" },
            { "FEATURED", "ÖNE ÇIKANLAR" },
            { "CHARACTER SKINS", "KARAKTER KOSTÜMLERİ" },
            { "GEMS & GOLD", "ELMAS VE ALTIN" },
            { "GOLD PACKS", "ALTIN PAKETLERİ" },
            { "GEM PACKS", "ELMAS PAKETLERİ" },
            { "HELMETS", "KASKLAR" },
            { "GLOVES", "ELDİVENLER" },
            { "BOOTS", "BOTLAR" },
            { "COMMON", "SIRADAN" },
            { "RARE", "NADİR" },
            { "EPIC", "DESTANSI" },
            { "LEGENDARY", "EFSANEVİ" },
            { "GOLD", "ALTIN" },
            { "PURCHASED! (DEV: FREE)", "SATIN ALINDI! (GELİŞTİRİCİ: ÜCRETSİZ)" },
            { "STORE COMING SOON", "MAĞAZA YAKINDA" },
            { "Attracts nearby coins automatically.", "Yakındaki altınları otomatik çeker." },
            { "Earn double gold coins for 24h.", "24 saat boyunca çift altın kazan." },
            { "Temporarily increases speed.", "Hızını geçici olarak artırır." },
            { "Blocks one hit from an obstacle.", "Bir engel çarpmasını engeller." },
            { "{0} REMOVED", "{0} ÇIKARILDI" },
            { "SPRING BOOTS", "YAYLI BOTLAR" },
            { "VELOCITY VISOR", "HIZ VİZÖRÜ" },
            { "COIN MAGNET", "ALTIN MIKNATISI" },
            { "GRIP-TECH", "GRIP-TECH" },
            { "NONE", "YOK" },
            { "CUSTOMIZE", "ÖZELLEŞTİR" },
            { "UPGRADE", "GELİŞTİR" },
            { "LV {0}/{1}", "SV {0}/{1}" },
            { "MAX", "MAKS" },
            { "UPGRADED!", "GELİŞTİRİLDİ!" },

            // Market
            { "GOLD: {0}", "ALTIN: {0}" },
            { "GEMS: {0}", "ELMAS: {0}" },
            { "SKINS", "KOSTÜMLER" },
            { "BOOSTS", "GÜÇLER" },
            { "CURRENCY", "PARA" },
            { "DAILY DEALS", "GÜNÜN FIRSATLARI" },
            { "BUY", "AL" },
            { "BUY GOLD", "ALTINLA AL" },
            { "{0} GEMS", "{0} ELMAS" },
            { "{0} GOLD", "{0} ALTIN" },
            { "OWNED", "SAHİPSİN" },
            { "EQUIP", "GİY" },
            { "EQUIPPED", "GİYİLİ" },
            { "LOCKED", "KİLİTLİ" },
            { "PURCHASED!", "SATIN ALINDI!" },
            { "NOT ENOUGH GEMS", "YETERLİ ELMAS YOK" },
            { "NOT ENOUGH GOLD", "YETERLİ ALTIN YOK" },
            { "{0} EQUIPPED", "{0} GİYİLDİ" },
            { "BUY IT IN THE MARKET", "MARKETTEN SATIN AL" },
            { "ROOKIE", "ÇAYLAK" },
            { "SPEED BOOST", "HIZ GÜCÜ" },
            { "SHIELD", "KALKAN" },
            { "MAGNET", "MIKNATIS" },
            { "DOUBLE GOLD", "ÇİFT ALTIN" },

            // Ayarlar
            { "AUDIO", "SES" },
            { "Music", "Müzik" },
            { "SFX", "Efekt" },
            { "Mute", "Sessiz" },
            { "GRAPHICS", "GRAFİK" },
            { "Med", "Orta" },
            { "Ultra", "Ultra" },
            { "CONTROLS", "KONTROLLER" },
            { "Sensitivity", "Hassasiyet" },
            { "Vibration", "Titreşim" },
            { "LANGUAGE", "DİL" },
            { "HELP", "YARDIM" },
            { "LEGAL", "YASAL" },

            // Etkinlikler
            { "SEASON PROGRESS", "SEZON İLERLEMESİ" },
            { "{0}/{1} LEVELS", "{0}/{1} BÖLÜM" },
            { "START", "BAŞLA" },
            { "Continue the campaign from your latest level.", "Kampanyaya son bölümünden devam et." },
            { "A random level you have already unlocked.", "Açtığın bölümlerden rastgele biri." },
            { "A new level every day. Reward: {0} gems.", "Her gün yeni bir bölüm. Ödül: {0} elmas." },
            { "COMPLETED TODAY", "BUGÜN TAMAMLANDI" },
            { "COME BACK TOMORROW!", "YARIN TEKRAR GEL!" },
            { "CAMPAIGN", "KAMPANYA" },

            // Yardım / yasal
            { "HOW TO PLAY", "NASIL OYNANIR" },
            {
                "Your runner moves forward on its own.\n\nHOLD the screen to charge your jump.\nRELEASE to jump.\n\nA short press is a small hop, a long press is a big jump. Read the obstacles ahead and time your release.\n\nHit an obstacle and the run ends. Retry instantly and beat the level!",
                "Koşucun kendiliğinden ileri koşar.\n\nZıplamayı şarj etmek için ekrana BASILI TUT.\nZıplamak için BIRAK.\n\nKısa basış küçük zıplama, uzun basış büyük zıplamadır. Öndeki engelleri oku ve bırakma anını iyi ayarla.\n\nEngele çarparsan koşu biter. Hemen tekrar dene ve bölümü geç!"
            },
            { "PRIVACY POLICY", "GİZLİLİK POLİTİKASI" },
            {
                "JUMPLINE GO stores your progress and settings only on this device. No personal data is collected or shared.",
                "JUMPLINE GO ilerlemeni ve ayarlarını yalnızca bu cihazda saklar. Hiçbir kişisel veri toplanmaz veya paylaşılmaz."
            },
            { "TERMS OF USE", "KULLANIM KOŞULLARI" },
            {
                "The game is provided as is. In-game gold and gems have no real-world value and cannot be exchanged for money.",
                "Oyun olduğu gibi sunulur. Oyun içi altın ve elmasların gerçek bir değeri yoktur ve paraya çevrilemez."
            },
            { "Version {0}", "Sürüm {0}" },

            // Oyun sahnesi
            { "LEVEL {0}", "BÖLÜM {0}" },
            { "PAUSED", "DURAKLATILDI" },
            { "CONTINUE", "DEVAM" },
            { "RESTART", "YENİDEN BAŞLAT" },
            { "MAIN MENU", "ANA MENÜ" },
            { "LEVEL COMPLETE!", "BÖLÜM TAMAMLANDI!" },
            { "NEXT", "SONRAKİ" },
            { "REPLAY", "TEKRAR OYNA" },
            { "GAME OVER", "OYUN BİTTİ" },
            { "RETRY", "TEKRAR DENE" },
            { "+{0} GOLD", "+{0} ALTIN" },
            { "+{0} GEMS", "+{0} ELMAS" },
            { "WIN (TEST)", "KAZAN (TEST)" },
            { "FAIL (TEST)", "KAYBET (TEST)" },
            { "ALL LEVELS COMPLETE!", "TÜM BÖLÜMLER TAMAM!" },
            { "SPEED UP! x{0}", "HIZLANDI! x{0}" },
            { "SWIPE UP: JUMP   ·   DOWN: SLIDE\nLEFT / RIGHT: CHANGE LANE", "YUKARI KAYDIR: ZIPLA   ·   AŞAĞI: KAY\nSAĞA / SOLA: ŞERİT DEĞİŞTİR" },
            {
                "The runner and obstacles come next.\nUse the test buttons to try the flow.",
                "Koşucu ve engeller sıradaki adım.\nAkışı denemek için test butonlarını kullan."
            },
        };
    }
}
