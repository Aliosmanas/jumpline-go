using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>Oyun geneli ayarlar: bölüm sayısı, sahne adları ve ödüller.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Jumpline/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int levelCount = 10;
        [SerializeField] private string mainMenuScene = "MainMenu";
        [SerializeField] private string gameplayScene = "Gameplay";
        [SerializeField, Min(0)] private int levelRewardGold = 100;
        [SerializeField, Min(0)] private int dailyRewardGems = 20;

        public int LevelCount => levelCount;
        public string MainMenuScene => mainMenuScene;
        public string GameplayScene => gameplayScene;
        public int LevelRewardGold => levelRewardGold;
        public int DailyRewardGems => dailyRewardGems;
    }
}
