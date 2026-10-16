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
            Assert.Greater(cfg.GetLevel(8).maxSpeed, cfg.GetLevel(1).maxSpeed, "tavan hız bölümle artmalı");
            Assert.AreEqual(8f, cfg.GetLevel(1).runSpeed, 0.01f, "bölüm 1 başlangıç hızı 8 m/sn");
            Assert.AreEqual(16f, cfg.GetLevel(1).maxSpeed, 0.01f, "bölüm 1 tavan hızı 16 m/sn");
            Assert.AreSame(cfg.GetLevel(3), director.Current, "seçili bölüm uygulanmalı");
            GameSession.Level = 1;
        }

        [UnityTest]
        public IEnumerator Level2_PlaysMardinIntroOnce()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            PlayerPrefs.DeleteKey("jl.introSeen.2");
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 2;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            Assert.IsTrue(IntroSequence.Playing, "bölüm 2 açılış konuşması oynamalı");
            Assert.AreEqual("sahmeran", IntroSequence.LinesFor(2)[1].speaker);
            Object.FindAnyObjectByType<IntroSequence>().SendMessage("Finish");
            Assert.IsTrue(IntroSequence.SeenLevel(2), "bir kez gösterilir");
            Assert.AreEqual(1f, Time.timeScale, "konuşma bitince koşu başlar");
            GameSession.Level = 1;
        }

        [UnityTest]
        public IEnumerator FinalLevel_PlaysEndingOnce()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            PlayerPrefs.SetInt("jl.introSeen.8", 1);
            PlayerPrefs.DeleteKey("jl.outroSeen");
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 8;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<LevelFlowController>().CompleteLevel();
            yield return null;
            Assert.IsTrue(IntroSequence.OutroSeen, "oyun sonu konuşması bir kez gösterilir");
            Assert.IsTrue(IntroSequence.Playing, "oyun sonu konuşması oynuyor");
            GameSession.Level = 1;
            Time.timeScale = 1f;
        }

        [Test]
        public void Legends_HaveDistinctPersonalities()
        {
            var cfg = Resources.FindObjectsOfTypeAll<GameConfig>();
            if (cfg.Length == 0) Assert.Ignore("GameConfig yüklü değil");
            var stone = System.Array.Find(cfg[0].Levels, l => l.legend == "stone_guardian");
            var dragon = System.Array.Find(cfg[0].Levels, l => l.legend == "dragon");
            Assert.Less(stone.bossDodgeBonus, 0f, "Taş Bekçi sıyrılamaz");
            Assert.Greater(dragon.bossThrowRate, 1.2f, "Ejderha sık atar");
        }
    }
}
