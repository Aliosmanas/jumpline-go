using System.Collections;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Level.Chunks;
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
            ChunkSpawner street = null;
            foreach (var sp in Object.FindObjectsByType<ChunkSpawner>())
                if (!sp.IsRooftop) street = sp;
            Assert.IsNotNull(street, "sokak üreticisi yok");
            Jumpline.Level.Course.ClearAll(true);
            float z0 = player.transform.position.z + 25f;
            Assert.IsNotNull(street.SpawnForTest(id, 0, z0), id + " tanımı yok");   // orta şerit
            Time.captureDeltaTime = dt;
            float maxY = 0f;
            for (int f = 0; f < 600 && player.transform.position.z < z0 + 13f; f++)
            {
                Jumpline.Level.Course.ClearRange(z0 + 14f, 1e6f);   // arkadan gelen yeni engeller ölçümü bozmasın
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
