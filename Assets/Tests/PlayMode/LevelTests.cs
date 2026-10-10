using System.Collections;
using Jumpline.Core;
using Jumpline.Level;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Bölüm tanımları: 8 durak, zorluk eğrisi artar, LevelDirector koşu başında uygular.</summary>
    public class LevelTests
    {
        [UnityTest]
        public IEnumerator LevelDirector_AppliesSelectedLevel()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 3;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            var director = Object.FindAnyObjectByType<LevelDirector>();
            Assert.IsNotNull(director, "LevelDirector yok");
            var cfg = Resources.FindObjectsOfTypeAll<GameConfig>()[0];
            Assert.AreEqual(8, cfg.Levels.Length, "8 durak");
            Assert.Greater(cfg.GetLevel(3).runSpeed, cfg.GetLevel(1).runSpeed, "hız bölümle artmalı");
            Assert.Less(cfg.GetLevel(8).obstacleGap.x, cfg.GetLevel(1).obstacleGap.x, "engeller sıklaşmalı");
            Assert.AreSame(cfg.GetLevel(3), director.Current, "seçili bölüm uygulanmalı");
            GameSession.Level = 1;
        }
    }
}
