using System;
using Jumpline.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jumpline.Core
{
    public static class SceneLoader
    {
        public static void StartLevel(GameConfig config, int level, RunMode mode)
        {
            GameSession.Level = Mathf.Clamp(level, 1, config.LevelCount);
            GameSession.Mode = mode;
            Time.timeScale = 1f;
            SceneManager.LoadScene(config.GameplayScene);
        }

        public static void LoadMainMenu(GameConfig config)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(config.MainMenuScene);
        }

        /// <summary>Açık en yüksek bölüm (kampanyada sıradaki).</summary>
        public static int ContinueLevel(GameConfig config) => Mathf.Min(SaveManager.UnlockedLevel, config.LevelCount);

        /// <summary>Günlük görev bölümü: güne göre sabit, açık bölümler arasından.</summary>
        public static int DailyLevel(GameConfig config) =>
            DateTime.Now.DayOfYear % ContinueLevel(config) + 1;
    }
}
