using System.Collections;
using System.Reflection;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using Jumpline.Save;
using Jumpline.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Jumpline.Tests
{
    /// <summary>Günlük görev: çaycı amca konuşur, hırsız önde kaçar, düşürdüğü toplanır, hedefte bölüm tamam + günlük ödül.</summary>
    public class DailyTests
    {
        private const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnitySetUp]
        public IEnumerator Load()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            PlayerPrefs.DeleteKey(SaveManager.DailyClaimKey);
            GameSession.Mode = RunMode.Daily;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e6f, 1e6f);
                sp.enabled = false;
            }
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            GameSession.Mode = RunMode.Campaign;
            Time.timeScale = 1f;
            yield return null;
        }

        private static IEnumerator SkipDialogue()
        {
            var tap = GameObject.Find("TapArea").GetComponent<Button>();
            for (int i = 0; i < 12 && Time.timeScale < 0.5f; i++)
            {
                yield return new WaitForSecondsRealtime(0.3f);
                tap.onClick.Invoke();
            }
            yield return new WaitForSecondsRealtime(0.4f);
        }

        [UnityTest]
        public IEnumerator Daily_DialogueThenThiefRunsAhead_CollectCompletes()
        {
            var dm = Object.FindAnyObjectByType<DailyMission>();
            Assert.IsNotNull(dm, "DailyMission yok");
            Assert.IsTrue(dm.enabled, "günlük modda görev açık");
            Assert.AreEqual(0f, Time.timeScale, "görev konuşması sırasında dünya donuk");
            yield return SkipDialogue();
            Assert.AreEqual(1f, Time.timeScale, "konuşma bitince koşu başlar");
            var player = Object.FindAnyObjectByType<PlayerController>();
            var m = (DailyMission.Mission)typeof(DailyMission).GetField("m", F).GetValue(dm);
            Assert.IsNotNull(m.thief);
            Assert.IsTrue(m.thief.activeInHierarchy, "bugünün hırsızı sahnede");
            yield return new WaitForSeconds(1f);
            Assert.Greater(m.thief.transform.position.z - player.transform.position.z, 10f, "hırsız önde kaçar");
            // hırsızı koşucunun şeridine al ve düşürt → koşucu üstünden geçince toplanır
            typeof(DailyMission).GetField("nextLane", F).SetValue(dm, Time.time + 60f);   // hırsız şerit değiştirmesin
            typeof(DailyMission).GetField("lane", F).SetValue(dm, player.Lane);
            yield return new WaitForSeconds(0.6f);
            typeof(DailyMission).GetField("lane", F).SetValue(dm, player.Lane);
            typeof(DailyMission).GetMethod("TryDrop", F).Invoke(dm, null);
            for (float t = 0f; t < 3f && dm.Collected == 0; t += Time.deltaTime) yield return null;
            Assert.AreEqual(1, dm.Collected, "düşürülen nesne toplandı");
            typeof(DailyMission).GetField("collected", F).SetValue(dm, dm.Target - 1);
            typeof(DailyMission).GetField("lane", F).SetValue(dm, player.Lane);
            yield return new WaitForSeconds(0.3f);
            typeof(DailyMission).GetMethod("TryDrop", F).Invoke(dm, null);
            var flow = Object.FindAnyObjectByType<LevelFlowController>();
            var complete = (CanvasGroup)typeof(LevelFlowController).GetField("completePanel", F).GetValue(flow);
            for (float t = 0f; t < 6f && !complete.gameObject.activeSelf; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(complete.gameObject.activeSelf, "hedefe ulaşınca bölüm tamam");
            Assert.IsTrue(SaveManager.DailyClaimedToday, "günlük ödül alındı");
        }
    }
}
