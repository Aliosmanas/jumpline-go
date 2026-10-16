using System.Collections;
using System.Reflection;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using Jumpline.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Jumpline.Tests
{
    /// <summary>Açılış konuşması (dünya donuk, atlanabilir, bir kez) ve çaycı amcalı öğretici (yavaşlatma → hareket → normal hız).</summary>
    public class StoryTests
    {
        private static IEnumerator Load(bool introSeen, bool tutorialDone)
        {
            if (introSeen) PlayerPrefs.SetInt("jl.introSeen", 1); else PlayerPrefs.DeleteKey("jl.introSeen");
            if (tutorialDone) PlayerPrefs.SetInt("jl.tutorialDone", 1); else PlayerPrefs.DeleteKey("jl.tutorialDone");
            GameSession.Mode = RunMode.Campaign;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            Jumpline.Level.Course.ClearAll(true);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Intro_FreezesWorld_SkipStartsRun()
        {
            yield return Load(false, true);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(0f, Time.timeScale, "açılışta dünya donuk");
            var box = Object.FindAnyObjectByType<DialogueBox>();
            Assert.IsTrue(box.IsOpen, "konuşma kutusu açık");
            var player = Object.FindAnyObjectByType<PlayerController>();
            float z = player.transform.position.z;
            GameObject.Find("SkipIntro").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.Greater(player.transform.position.z, z + 5f, "atlayınca koşu başlar");
            Assert.IsTrue(IntroSequence.Seen, "açılış bir kez gösterilir");
        }

        [UnityTest]
        public IEnumerator Intro_TapThrough_AllLines()
        {
            yield return Load(false, true);
            var box = Object.FindAnyObjectByType<DialogueBox>();
            var tap = GameObject.Find("TapArea").GetComponent<Button>();
            for (int i = 0; i < IntroSequence.Lines.Length * 2 + 2 && !IntroSequence.Seen; i++)
            {
                yield return new WaitForSecondsRealtime(0.35f);
                tap.onClick.Invoke();
            }
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.IsTrue(IntroSequence.Seen, "dokunarak tüm satırlar geçildi");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Tutorial_SlowsAtFirstLowObstacle_ResumesOnJump()
        {
            yield return Load(true, false);
            var player = Object.FindAnyObjectByType<PlayerController>();
            yield return new WaitForSeconds(3f);
            var lib = Resources.FindObjectsOfTypeAll<Jumpline.Level.Chunks.ObstacleLibrary>();
            Obstacle tpl = lib.Length > 0 ? lib[0].Get("crates")?.prefab : null;
            Assert.IsNotNull(tpl, "sandık prefab'ı yok");
            var crate = Object.Instantiate(tpl);
            crate.gameObject.SetActive(true);
            crate.enabled = true;
            Vector3 p = player.transform.position;
            crate.transform.position = new Vector3(player.LaneOriginX + player.Lane * player.LaneWidth, player.GroundHeight, p.z + 22f);
            for (float t = 0f; t < 3f && Time.timeScale > 0.5f; t += Time.unscaledDeltaTime) yield return null;
            Assert.Less(Time.timeScale, 0.5f, "alçak engelde öğretici yavaşlatır");
            typeof(PlayerController).GetMethod("RequestJump", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(player, null);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1f, Time.timeScale, "zıplayınca hız normale döner");
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(player.IsDead, "öğretilen zıplamayla engel aşıldı");
        }
    }
}
