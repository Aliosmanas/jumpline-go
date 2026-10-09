using System;
using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>
    /// Bağlılık sistemleri (aşama 11) — hepsi cihazda (PlayerPrefs):
    ///  • Oyuncu seviyesi: tecrübe puanı (XP) koşudan gelir; seviye atlayınca altın, her 5 seviyede elmas.
    ///  • Günlük 3 görev: havuzdan güne göre seçilir (aynı gün herkes aynı görevleri görür), ilerleme koşu olaylarından.
    ///  • Başarımlar: kalıcı hedefler, bir kez ödül.
    ///  • 7 günlük giriş takvimi: her yeni gün bir sonraki ödül; gün kaçırılırsa sıfırlanmaz, kaldığı yerden sürer.
    /// Sayaçlar (Stat) ortak: görevler günlük sayaçla, başarımlar toplam sayaçla ölçülür.
    /// </summary>
    public static class Progression
    {
        public enum Stat { Meters, Coins, BossHits, Throws, Jumps, Slides, PowerUps, QuickRaces, BossWins, ThreeStarWins, DailyMissions, QuickBest }

        // ------------------------------------------------------------------
        // Sayaçlar
        // ------------------------------------------------------------------

        private const string TotalPrefix = "jl.total.";
        private const string DayPrefix = "jl.day.";
        private const string DayStampKey = "jl.dayStamp";

        public static string Today => DateTime.Now.ToString("yyyyMMdd");

        public static int Total(Stat s) => PlayerPrefs.GetInt(TotalPrefix + s, 0);
        public static int TodayCount(Stat s) { RollDay(); return PlayerPrefs.GetInt(DayPrefix + s, 0); }

        /// <summary>Koşu olayı: günlük ve toplam sayaç artar.</summary>
        public static void Add(Stat s, int amount = 1)
        {
            if (amount <= 0) return;
            RollDay();
            PlayerPrefs.SetInt(TotalPrefix + s, Total(s) + amount);
            PlayerPrefs.SetInt(DayPrefix + s, PlayerPrefs.GetInt(DayPrefix + s, 0) + amount);
        }

        /// <summary>En yüksek değer sayacı (Hızlı Yarış rekoru gibi).</summary>
        public static void Max(Stat s, int value)
        {
            if (value > Total(s)) PlayerPrefs.SetInt(TotalPrefix + s, value);
        }

        private static void RollDay()
        {
            if (PlayerPrefs.GetString(DayStampKey, "") == Today) return;
            foreach (Stat s in Enum.GetValues(typeof(Stat))) PlayerPrefs.DeleteKey(DayPrefix + s);
            for (int i = 0; i < 3; i++) PlayerPrefs.DeleteKey(TaskClaimPrefix + i);
            PlayerPrefs.SetString(DayStampKey, Today);
        }

        public static void Save() => PlayerPrefs.Save();

        // ------------------------------------------------------------------
        // Seviye
        // ------------------------------------------------------------------

        private const string XpKey = "jl.xp";
        public static int Xp => PlayerPrefs.GetInt(XpKey, 0);

        /// <summary>n. seviyeden n+1'e gereken XP.</summary>
        public static int XpForLevel(int level) => 300 + 150 * (level - 1);

        public static int Level
        {
            get
            {
                int xp = Xp, lv = 1;
                while (xp >= XpForLevel(lv)) { xp -= XpForLevel(lv); lv++; }
                return lv;
            }
        }

        /// <summary>Mevcut seviye içindeki ilerleme (0-1).</summary>
        public static float LevelProgress
        {
            get
            {
                int xp = Xp, lv = 1;
                while (xp >= XpForLevel(lv)) { xp -= XpForLevel(lv); lv++; }
                return xp / (float)XpForLevel(lv);
            }
        }

        public static int LevelUpGold(int level) => 150 + 50 * level;
        public static int LevelUpGems(int level) => level % 5 == 0 ? 20 : 0;

        /// <summary>XP ekler; atlanan her seviyenin ödülünü verir. Kaç seviye atlandığını döner.</summary>
        public static int AddXp(int amount)
        {
            if (amount <= 0) return 0;
            int before = Level;
            PlayerPrefs.SetInt(XpKey, Xp + amount);
            int after = Level;
            for (int lv = before + 1; lv <= after; lv++)
            {
                SaveManager.Gold += LevelUpGold(lv);
                SaveManager.Gems += LevelUpGems(lv);
            }
            return after - before;
        }

        // ------------------------------------------------------------------
        // Günlük görevler
        // ------------------------------------------------------------------

        public struct Task
        {
            public string id;
            public string text;       // çeviri anahtarı, {0} = hedef
            public Stat stat;
            public int target;
            public int gold, gems;
        }

        public static readonly Task[] TaskPool =
        {
            new Task { id = "run1000", text = "Run {0} m", stat = Stat.Meters, target = 1000, gold = 150 },
            new Task { id = "run3000", text = "Run {0} m", stat = Stat.Meters, target = 3000, gems = 5 },
            new Task { id = "coins150", text = "Collect {0} coins", stat = Stat.Coins, target = 150, gold = 150 },
            new Task { id = "coins400", text = "Collect {0} coins", stat = Stat.Coins, target = 400, gems = 5 },
            new Task { id = "hits3", text = "Hit Tepegoz {0} times", stat = Stat.BossHits, target = 3, gold = 200 },
            new Task { id = "throws5", text = "Throw {0} items", stat = Stat.Throws, target = 5, gold = 120 },
            new Task { id = "jumps30", text = "Jump {0} times", stat = Stat.Jumps, target = 30, gold = 100 },
            new Task { id = "slides15", text = "Slide {0} times", stat = Stat.Slides, target = 15, gold = 100 },
            new Task { id = "power2", text = "Pick up {0} power-ups", stat = Stat.PowerUps, target = 2, gems = 3 },
            new Task { id = "quick1", text = "Play Quick Race {0} time(s)", stat = Stat.QuickRaces, target = 1, gold = 150 },
        };

        private const string TaskClaimPrefix = "jl.taskClaim.";

        /// <summary>Bugünün 3 görevi (tarihten tohumlanan karıştırma; aynı türden iki görev gelmez).</summary>
        public static Task[] TodayTasks()
        {
            var rnd = new System.Random(int.Parse(Today));
            var picked = new System.Collections.Generic.List<Task>();
            var used = new System.Collections.Generic.HashSet<Stat>();
            int guard = 0;
            while (picked.Count < 3 && guard++ < 100)
            {
                Task t = TaskPool[rnd.Next(TaskPool.Length)];
                if (used.Add(t.stat)) picked.Add(t);
            }
            return picked.ToArray();
        }

        public static int TaskProgress(Task t) => Mathf.Min(TodayCount(t.stat), t.target);
        public static bool TaskDone(Task t) => TodayCount(t.stat) >= t.target;
        public static bool TaskClaimed(int index) { RollDay(); return PlayerPrefs.GetInt(TaskClaimPrefix + index, 0) == 1; }

        public static bool ClaimTask(int index)
        {
            Task[] today = TodayTasks();
            if (index < 0 || index >= today.Length || !TaskDone(today[index]) || TaskClaimed(index)) return false;
            PlayerPrefs.SetInt(TaskClaimPrefix + index, 1);
            SaveManager.Gold += today[index].gold;
            SaveManager.Gems += today[index].gems;
            AddXp(60);
            PlayerPrefs.Save();
            return true;
        }

        public static int UnclaimedCount()
        {
            Task[] today = TodayTasks();
            int n = 0;
            for (int i = 0; i < today.Length; i++) if (TaskDone(today[i]) && !TaskClaimed(i)) n++;
            foreach (Achievement a in Achievements) if (AchievementDone(a) && !AchievementClaimed(a)) n++;
            return n;
        }

        // ------------------------------------------------------------------
        // Başarımlar
        // ------------------------------------------------------------------

        public struct Achievement
        {
            public string id;
            public string text;
            public Stat stat;
            public int target;
            public int gems;
        }

        public static readonly Achievement[] Achievements =
        {
            new Achievement { id = "beat_tepegoz", text = "Defeat Tepegoz", stat = Stat.BossWins, target = 1, gems = 20 },
            new Achievement { id = "three_star", text = "Win a level with 3 stars", stat = Stat.ThreeStarWins, target = 1, gems = 25 },
            new Achievement { id = "marathon", text = "Run {0} m in total", stat = Stat.Meters, target = 10000, gems = 15 },
            new Achievement { id = "coin_hoard", text = "Collect {0} coins in total", stat = Stat.Coins, target = 2000, gems = 15 },
            new Achievement { id = "sharpshooter", text = "Hit Tepegoz {0} times in total", stat = Stat.BossHits, target = 40, gems = 20 },
            new Achievement { id = "quick_star", text = "Score {0} in Quick Race", stat = Stat.QuickBest, target = 20000, gems = 20 },
            new Achievement { id = "good_neighbour", text = "Complete {0} daily missions", stat = Stat.DailyMissions, target = 10, gems = 30 },
        };

        private const string AchClaimPrefix = "jl.ach.";

        public static int AchievementProgress(Achievement a) => Mathf.Min(Total(a.stat), a.target);
        public static bool AchievementDone(Achievement a) => Total(a.stat) >= a.target;
        public static bool AchievementClaimed(Achievement a) => PlayerPrefs.GetInt(AchClaimPrefix + a.id, 0) == 1;

        public static bool ClaimAchievement(Achievement a)
        {
            if (!AchievementDone(a) || AchievementClaimed(a)) return false;
            PlayerPrefs.SetInt(AchClaimPrefix + a.id, 1);
            SaveManager.Gems += a.gems;
            AddXp(150);
            PlayerPrefs.Save();
            return true;
        }

        // ------------------------------------------------------------------
        // 7 günlük giriş takvimi
        // ------------------------------------------------------------------

        private const string LoginDayKey = "jl.loginDay";      // alınan son ödülün günü (0-6), -1 hiç
        private const string LoginStampKey = "jl.loginStamp";  // son alındığı tarih

        public static readonly (int gold, int gems)[] LoginRewards =
        {
            (100, 0), (0, 5), (200, 0), (0, 10), (300, 0), (0, 15), (500, 30),
        };

        /// <summary>Takvimde sıradaki gün (0-6).</summary>
        public static int LoginNextDay => (PlayerPrefs.GetInt(LoginDayKey, -1) + 1) % LoginRewards.Length;
        public static bool LoginAvailable => PlayerPrefs.GetString(LoginStampKey, "") != Today;

        /// <summary>Bugünün giriş ödülünü verir (günde bir).</summary>
        public static bool ClaimLogin()
        {
            if (!LoginAvailable) return false;
            int day = LoginNextDay;
            SaveManager.Gold += LoginRewards[day].gold;
            SaveManager.Gems += LoginRewards[day].gems;
            PlayerPrefs.SetInt(LoginDayKey, day);
            PlayerPrefs.SetString(LoginStampKey, Today);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Takvimdeki günün durumu: alınmış mı (bu tur).</summary>
        public static bool LoginDayClaimed(int day)
        {
            int last = PlayerPrefs.GetInt(LoginDayKey, -1);
            return !LoginAvailable ? day <= last : day < LoginNextDay && LoginNextDay != 0;
        }
    }
}
