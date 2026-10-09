using System.Collections;
using System.Reflection;
using Jumpline.Save;
using Jumpline.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Efsaneler haritası: 1. bölüm açık + portreli, sonrakiler "yakında", yıldızlar kayıttan.</summary>
    public class MapTests
    {
        [UnityTest]
        public IEnumerator Map_ShowsUnlockedFirstLevel_ComingSoonOthers_Stars()
        {
            int before = SaveManager.GetStars(1);
            SaveManager.SubmitStars(1, 2);
            SceneManager.LoadScene("MainMenu");
            yield return null;
            yield return null;
            var nav = Object.FindAnyObjectByType<MenuNavigator>();
            nav.Open("map");
            yield return new WaitForSecondsRealtime(0.5f);
            var map = Object.FindAnyObjectByType<MapView>(FindObjectsInactive.Include);
            Assert.IsNotNull(map, "harita ekranı yok");
            map.gameObject.SetActive(true);
            map.SendMessage("Refresh");
            var nodes = (MapView.Node[])typeof(MapView).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(map);
            Assert.AreEqual(8, nodes.Length, "8 durak");
            Assert.IsFalse(nodes[0].lockIcon.activeSelf, "1. bölüm açık");
            Assert.IsTrue(nodes[0].portrait.activeSelf, "1. bölümde Tepegöz portresi");
            Assert.IsTrue(nodes[1].lockIcon.activeSelf, "2. bölüm kilitli");
            Assert.IsFalse(string.IsNullOrEmpty(nodes[1].status.text), "2. bölüm yakında yazar");
            int on = 0;
            foreach (var s in nodes[0].stars) if (s.color.r > 0.9f) on++;
            Assert.AreEqual(Mathf.Max(2, before), on, "yıldızlar kayıttan");
        }
    }
}
