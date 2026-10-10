using System;
using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>Oyun geneli ayarlar: bölüm sayısı, sahne adları ve ödüller.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Jumpline/Game Config")]
    public class GameConfig : ScriptableObject
    {
        /// <summary>Bölüm tanımı (harita durağı): şehir, efsane, zorluk. LevelDirector koşu başında uygular.</summary>
        [Serializable]
        public class LevelSpec
        {
            public string city = "DIYARBAKIR";
            public string legend = "tepegoz";
            [Tooltip("Oynanabilir mi (canavarı/sahnesi hazır)? Değilse haritada 'yakında'.")]
            public bool built;
            [Min(1f)] public float runSpeed = 11f;
            [Min(1f)] public float maxSpeed = 18f;
            [Tooltip("Engel desenleri arası boşluk (m), küçük = yoğun.")]
            public Vector2 obstacleGap = new Vector2(11f, 17f);
            [Range(1, 8)] public int bossHearts = 4;
            [Tooltip("Canavarın kaçma hızı çarpanı (1 = bölüm 1).")]
            [Min(0.1f)] public float bossDrift = 1f;
        }

        [SerializeField] private LevelSpec[] levels = Array.Empty<LevelSpec>();

        [SerializeField, Min(1)] private int levelCount = 10;
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameplayScene = "Gameplay";
        [SerializeField, Min(0)] private int levelRewardGold = 100;
        [SerializeField, Min(0)] private int dailyRewardGems = 20;

        public int LevelCount => levelCount;
        public LevelSpec[] Levels => levels;
        public int BuiltLevels { get { int n = 0; foreach (var l in levels) if (l.built) n++; else break; return Mathf.Max(1, n); } }

        /// <summary>1 tabanlı; tanımsız bölümde son tanım (yoksa varsayılan).</summary>
        public LevelSpec GetLevel(int level) =>
            levels.Length == 0 ? new LevelSpec() : levels[Mathf.Clamp(level - 1, 0, levels.Length - 1)];

        public void SetLevels(LevelSpec[] specs) => levels = specs;
        public string MainMenuScene => mainMenuScene;
        public string GameplayScene => gameplayScene;
        public int LevelRewardGold => levelRewardGold;
        public int DailyRewardGems => dailyRewardGems;
    }
}
