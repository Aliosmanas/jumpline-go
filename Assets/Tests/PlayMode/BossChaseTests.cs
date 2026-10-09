using System.Collections;
using System.Reflection;
using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using Jumpline.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Canavar kovalaması akışı (gerçek oyun sahnesinde): giriş, isabet → faz → çatı portalı, kaçış → devam, yenilgi → bölüm sonu.
    /// Engeller kapatılır (koşucu otomatik pilotsuz ölmesin).
    /// </summary>
    public class BossChaseTests
    {
        private BossChase boss;
        private PlayerController player;
        private LevelFlowController flow;

        [UnitySetUp]
        public IEnumerator Load()
        {
            UnityEngine.PlayerPrefs.DeleteKey(SaveManager.LegendPrefix + "tepegoz");
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e6f, 1e6f);
                sp.enabled = false;
            }
            boss = Object.FindAnyObjectByType<BossChase>();
            player = Object.FindAnyObjectByType<PlayerController>();
            flow = Object.FindAnyObjectByType<LevelFlowController>();
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        private static void SetField(object o, string name, object value) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(o, value);

        private static T GetField<T>(object o, string name) =>
            (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);

        [UnityTest]
        public IEnumerator Intro_RunsOffToStartGap_AheadOfRunner()
        {
            Assert.IsNotNull(boss, "Tepegöz sahnede yok");
            Assert.IsTrue(boss.gameObject.activeSelf, "Kampanyada canavar açık olmalı");
            Assert.AreEqual(4, boss.Hearts);
            yield return new WaitForSeconds(4.5f);
            Assert.IsTrue(boss.IsChasing);
            Assert.Greater(boss.Position.z - player.transform.position.z, 18f, "canavar koşucunun önünde, başlangıç mesafesinde");
            Assert.IsTrue(boss.Visible);
            Assert.IsFalse(player.IsDead);
        }

        [UnityTest]
        public IEnumerator Hits_LowerHearts_AndOpenRooftopPortal()
        {
            yield return new WaitForSeconds(4f);
            float before = boss.Gap;
            boss.TakeHit();
            Assert.AreEqual(3, boss.Hearts);
            Assert.AreEqual(BossChase.Phase.Rooftop, boss.CurrentPhase);
            yield return new WaitForSeconds(1.2f);
            Assert.Less(boss.Gap, before - 6f, "isabet mesafeyi kapatır");
            Assert.IsTrue(GetField<bool>(boss, "upPortalOpen"), "3. kalpte çatı portalı açılır");
            // canavar portala girer (bir an görünmez), koşucu da çatıya çıkınca orada yeniden görünür
            var sw = Object.FindAnyObjectByType<WorldSwitcher>();
            bool vanished = false;
            for (float t = 0f; t < 8f && sw.InStreet; t += Time.deltaTime)
            {
                if (!boss.Visible) vanished = true;
                if (t > 6f) sw.EnterRooftop();
                yield return null;
            }
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(vanished, "canavar portaldan çatıya geçti");
            Assert.IsFalse(sw.InStreet);
            Assert.IsTrue(boss.Visible, "koşucu çatıya çıkınca canavar görünür");
        }

        [UnityTest]
        public IEnumerator ThrowSystem_PickupAndThrow_HitsBossInSameLane()
        {
            yield return new WaitForSeconds(4f);
            var throws = Object.FindAnyObjectByType<ThrowSystem>();
            Assert.IsNotNull(throws);
            var held = GetField<System.Collections.Generic.Stack<int>>(throws, "held");
            held.Push(0);
            SetField(boss, "lane", player.Lane);
            SetField(boss, "telegraphUntil", -1f);
            SetField(boss, "nextLaneChange", Time.time + 30f);
            int hearts = boss.Hearts;
            throws.Throw();
            Assert.AreEqual(0, throws.Count);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(hearts - 1, boss.Hearts, "aynı şeritteki canavara atış isabet etmeli");
        }

        [UnityTest]
        public IEnumerator Escape_ShowsFailPanel_ThenGemsContinue()
        {
            yield return new WaitForSeconds(4f);
            SaveManager.Gems = 500;
            SetField(boss, "gap", 60f);
            yield return null;
            yield return null;
            Assert.IsTrue(boss.IsEscaped);
            var fail = GetField<CanvasGroup>(flow, "failPanel");
            Assert.IsTrue(fail.gameObject.activeSelf, "kaçınca oyun bitti paneli");
            Assert.AreEqual(0f, Time.timeScale, "panel açıkken dünya durur");
            var revive = GetField<UnityEngine.UI.Button>(flow, "reviveGemsButton");
            Assert.IsTrue(revive.interactable);
            revive.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(boss.IsChasing, "elmasla devam: kovalama sürer");
            Assert.Less(boss.Gap, 30f);
        }

        [UnityTest]
        public IEnumerator ThreeHits_GoToBridge_FinalHit_FallCinematic_CompleteLevel()
        {
            yield return new WaitForSeconds(4f);
            var sw = Object.FindAnyObjectByType<WorldSwitcher>();
            for (int i = 0; i < 3; i++)
            {
                boss.TakeHit();
                yield return new WaitForSeconds(0.4f);
            }
            Assert.AreEqual(1, boss.Hearts);
            Assert.AreEqual(BossChase.Phase.LastHeart, boss.CurrentPhase);
            boss.TakeHit();
            Assert.AreEqual(1, boss.Hearts, "köprüye varmadan son kalp düşmez");
            for (float t = 0f; t < 6f && !sw.InArena; t += Time.deltaTime) yield return null;
            Assert.IsTrue(sw.InArena, "son kalpte On Gözlü Köprü'ye geçilir");
            yield return new WaitForSeconds(1f);
            Assert.IsTrue(boss.Visible, "canavar köprüde görünür");
            Assert.Less(Mathf.Abs(player.transform.position.x - sw.ArenaOriginX), 3f, "koşucu köprüde");
            boss.TakeHit();
            Assert.IsTrue(boss.IsDefeated);
            Assert.IsFalse(player.InputEnabled, "sinematikte kontrol yok");
            var complete = GetField<CanvasGroup>(flow, "completePanel");
            for (float t = 0f; t < 10f && !complete.gameObject.activeSelf; t += Time.unscaledDeltaTime) yield return null;
            Assert.IsTrue(complete.gameObject.activeSelf, "düşüşten sonra bölüm tamam");
            Assert.IsTrue(SaveManager.HasLegend("tepegoz"), "Efsaneler Kitabı'na Tepegöz sayfası");
        }
    }
}
