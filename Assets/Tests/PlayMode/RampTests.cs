using System.Collections;
using System.Reflection;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Rampalı araçlar (karpuz kamyonu, kamyon, traktör) düz koşarak aşılabilmeli — normal ve düşük kare hızında.
    /// Önceden tırmanış kare başına tek adımdı: telefonda takılmada/yüksek hızda rampa "duvar" sayılıp ölünüyordu.
    /// </summary>
    public class RampTests
    {
        private const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;

        [UnityTest]
        public IEnumerator MelonTruck_Climbable_60fps() => RunOver("melon", 1f / 60f);

        [UnityTest]
        public IEnumerator MelonTruck_Climbable_25fps() => RunOver("melon", 1f / 25f);

        [UnityTest]
        public IEnumerator Tractor_Climbable_25fps() => RunOver("tractor", 1f / 25f);

        private static IEnumerator RunOver(string id, float dt)
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.QuickRace;   // canavarsız düz koşu
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            ObstacleSpawner street = null;
            foreach (var sp in Object.FindObjectsByType<ObstacleSpawner>())
                if (!(bool)typeof(ObstacleSpawner).GetField("rooftop", Any).GetValue(sp)) street = sp;
            Assert.IsNotNull(street, "sokak üreticisi yok");
            street.ClearRange(-1e6f, 1e6f);
            var sets = (System.Array)typeof(ObstacleSpawner).GetField("assets", Any).GetValue(street);
            Obstacle template = null;
            foreach (var s in sets)
            {
                var t = s.GetType();
                if ((string)t.GetField("id").GetValue(s) == id) template = ((Obstacle[])t.GetField("variants").GetValue(s))[0];
            }
            Assert.IsNotNull(template, id + " şablonu yok");
            float x = player.LaneOriginX + player.Lane * 0f;   // orta şerit
            float z0 = player.transform.position.z + 25f;
            typeof(ObstacleSpawner).GetMethod("Spawn", Any).Invoke(street, new object[] { template, player.LaneOriginX, z0 });
            Time.captureDeltaTime = dt;
            float maxY = 0f;
            for (int f = 0; f < 600 && player.transform.position.z < z0 + 13f; f++)
            {
                street.ClearRange(z0 + 14f, 1e6f);   // arkadan gelen yeni engeller ölçümü bozmasın
                maxY = Mathf.Max(maxY, player.transform.position.y);
                Assert.IsFalse(player.IsDead, $"{id} rampasında öldü (dt={dt:F3}, y={player.transform.position.y:F2})");
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            Assert.Greater(maxY, 1.4f, "rampadan aracın üstüne çıkmalı");
            Assert.IsFalse(player.IsDead);
        }

        [TearDown]
        public void Reset() => Time.captureDeltaTime = 0f;
    }
}
