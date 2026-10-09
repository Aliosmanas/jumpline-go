using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşu olaylarını bağlılık sayaçlarına işler (Progression): zıplama, kayma, atış, isabet, güç; koşu bitince
    /// (ölüm paneli / bölüm sonu / sahneden çıkış) metre ve para, XP hesabı. Kazanılan XP ve seviye atlama
    /// <see cref="LastRunXp"/> / <see cref="LastLevelUps"/> ile panellerde gösterilir.
    /// XP = metre/10 + para/2 + canavar isabeti başına 25 + yenilgi 250 + günlük görev 120.
    /// </summary>
    public class ProgressTracker : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CoinField coins;
        [SerializeField] private ThrowSystem throws;
        [SerializeField] private BossChase boss;
        [SerializeField] private PowerUps powerUps;

        private float committedMeters;
        private int committedCoins;
        private int bossHitsThisRun;
        private bool bossWon, dailyDone;
        private bool committed;

        public int LastRunXp { get; private set; }
        public int LastLevelUps { get; private set; }

        private void Start()
        {
            if (player != null)
            {
                player.Jumped += () => Progression.Add(Progression.Stat.Jumps);
                player.SlideStarted += () => Progression.Add(Progression.Stat.Slides);
            }
            if (throws != null && throws.isActiveAndEnabled) throws.Thrown += () => Progression.Add(Progression.Stat.Throws);
            if (boss != null && boss.gameObject.activeSelf)
            {
                boss.Hit += () => { Progression.Add(Progression.Stat.BossHits); bossHitsThisRun++; };
                boss.Defeated += () => bossWon = true;
            }
            if (powerUps != null) powerUps.Activated += _ => Progression.Add(Progression.Stat.PowerUps);
            if (GameSession.Mode == RunMode.QuickRace) Progression.Add(Progression.Stat.QuickRaces);
        }

        /// <summary>LevelFlowController: bölüm sonu (completed) ya da ölüm/kaçış paneli açılırken.</summary>
        public void CommitRun(bool completed)
        {
            if (completed && GameSession.Mode == RunMode.Daily) dailyDone = true;
            Commit();
        }

        /// <summary>Ölüm paneli açılınca da çağrılır (devam edilirse kalan yol sonra eklenir).</summary>
        public void Commit()
        {
            if (player == null) return;
            float meters = player.Distance - committedMeters;
            int newCoins = coins != null ? coins.RunCoins - committedCoins : 0;
            committedMeters = player.Distance;
            if (coins != null) committedCoins = coins.RunCoins;
            Progression.Add(Progression.Stat.Meters, Mathf.FloorToInt(meters));
            Progression.Add(Progression.Stat.Coins, newCoins);
            int xp = Mathf.FloorToInt(meters / 10f) + newCoins / 2 + bossHitsThisRun * 25;
            bossHitsThisRun = 0;
            bool won = bossWon || (boss != null && boss.gameObject.activeSelf && boss.IsDefeated);   // Defeated olayı bölüm sonundan önce de gelmeyebilir
            if (won && !committed)
            {
                Progression.Add(Progression.Stat.BossWins);
                if (Jumpline.Save.SaveManager.GetStars(GameSession.Level) >= 3) Progression.Add(Progression.Stat.ThreeStarWins);
                xp += 250;
                committed = true;
            }
            if (dailyDone) { Progression.Add(Progression.Stat.DailyMissions); xp += 120; dailyDone = false; }
            LastRunXp += xp;
            LastLevelUps += Progression.AddXp(xp);
            Progression.Save();
        }

        private void OnDestroy() => Commit();
        private void OnApplicationPause(bool paused) { if (paused) Commit(); }
    }
}
