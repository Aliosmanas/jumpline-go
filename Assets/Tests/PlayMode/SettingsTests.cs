using System.Collections;
using Jumpline.Core;
using Jumpline.Save;
using Jumpline.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Aşama 12: solak modu, para imzası, jenerik ekranı.</summary>
    public class SettingsTests
    {
        [UnityTest]
        public IEnumerator LeftHanded_MirrorsThrowButton()
        {
            bool before = SaveManager.LeftHanded;
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.Campaign;
            SaveManager.LeftHanded = true;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var mirror = Object.FindAnyObjectByType<HandednessMirror>(FindObjectsInactive.Include);
            Assert.IsNotNull(mirror, "atma düğmesinde HandednessMirror yok");
            mirror.gameObject.SetActive(true);
            Assert.Less(((RectTransform)mirror.transform).anchoredPosition.x, 0f, "solak modda atma düğmesi solda olmalı");
            SaveManager.LeftHanded = before;
        }

        [Test]
        public void Currency_TamperIsDetected_ButNotErased()
        {
            int gold = SaveManager.Gold;
            SaveManager.VerifyCurrency();
            Assert.IsFalse(SaveManager.CurrencyTampered, "normal kayıtta alarm olmamalı");
            PlayerPrefs.SetInt(SaveManager.GoldKey, PlayerPrefs.GetInt(SaveManager.GoldKey, SaveManager.DefaultGold) + 99999);   // elle değiştirme
            SaveManager.VerifyCurrency();
            Assert.IsTrue(SaveManager.CurrencyTampered, "elle değiştirme yakalanmalı");
            PlayerPrefs.SetInt(SaveManager.GoldKey, PlayerPrefs.GetInt(SaveManager.GoldKey) - 99999);
            SaveManager.VerifyCurrency();
            if (!SaveManager.UnlimitedCurrency) Assert.AreEqual(gold, SaveManager.Gold, "ilerleme silinmemeli");
        }

        [UnityTest]
        public IEnumerator Credits_ListsSourcesFromCreditsDoc()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            Object.FindAnyObjectByType<MenuNavigator>().Open("credits");
            yield return new WaitForSecondsRealtime(0.5f);
            var screen = GameObject.Find("CreditsScreen");
            Assert.IsNotNull(screen, "jenerik ekranı yok");
            bool pixabay = false;
            foreach (var t in screen.GetComponentsInChildren<TMP_Text>(true))
                if (t.text.Contains("AzadiJihan")) pixabay = true;
            Assert.IsTrue(pixabay, "müzik sanatçısı jenerikte görünmeli");
        }
    }
}
