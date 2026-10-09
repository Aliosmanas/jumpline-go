using System.Collections;
using Jumpline.Core;
using Jumpline.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Bağlılık (aşama 11): XP/seviye, günlük görev alma, giriş takvimi, menü bağlantıları.</summary>
    public class ProgressionTests
    {
        [Test]
        public void Xp_LevelsUp_WhenThresholdReached()
        {
            int lv = Progression.Level;
            int need = Mathf.CeilToInt((1f - Progression.LevelProgress) * Progression.XpForLevel(lv)) + 1;
            int ups = Progression.AddXp(need);
            Assert.GreaterOrEqual(ups, 1, "seviye atlamadı");
            Assert.AreEqual(lv + ups, Progression.Level);
        }

        [Test]
        public void DailyTask_ClaimOnceAfterDone()
        {
            Progression.Task[] today = Progression.TodayTasks();
            Assert.AreEqual(3, today.Length, "günde 3 görev");
            Assert.AreNotEqual(today[0].stat, today[1].stat, "görevler farklı sayaçlar");
            Progression.Add(today[0].stat, today[0].target);
            Assert.IsTrue(Progression.TaskDone(today[0]));
            Progression.ClaimTask(0);   // daha önce alınmış olabilir
            Assert.IsTrue(Progression.TaskClaimed(0));
            Assert.IsFalse(Progression.ClaimTask(0), "aynı görev iki kez alınamaz");
        }

        [Test]
        public void Login_ClaimOncePerDay()
        {
            if (Progression.LoginAvailable) Assert.IsTrue(Progression.ClaimLogin());
            Assert.IsFalse(Progression.LoginAvailable);
            Assert.IsFalse(Progression.ClaimLogin(), "aynı gün ikinci ödül yok");
        }

        [UnityTest]
        public IEnumerator Menu_HasTasksScreen_Badge_Calendar()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerLevelBadge>(), "seviye rozeti yok");
            Assert.IsNotNull(Object.FindAnyObjectByType<LoginCalendarView>(FindObjectsInactive.Include), "giriş takvimi yok");
            Object.FindAnyObjectByType<MenuNavigator>().Open("tasks");
            yield return new WaitForSecondsRealtime(0.5f);
            var view = Object.FindAnyObjectByType<TasksView>();
            Assert.IsNotNull(view, "görevler ekranı açılmadı");
            Assert.IsTrue(view.isActiveAndEnabled);
        }
    }
}
