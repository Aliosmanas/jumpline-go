using System.Collections;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Level.Designed;
using Jumpline.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Tasarım haritalarındaki rampalı araç (kamyon / karpuz kamyonu) düz koşarak, zıplamadan çıkılabilmeli (16 Eki kullanıcı:
    /// "rampa aracına hiçbir şekilde çıkılmıyor"). Koşucu rampanın şeridine, önüne konur; başka engeller kaldırılır.
    /// </summary>
    public class DesignedRampTests
    {
        [UnityTest]
        public IEnumerator RampVehicle_WalkUp() => Run(1f / 60f);

        [UnityTest]
        public IEnumerator RampVehicle_WalkUp_25fps() => Run(1f / 25f);

        private static IEnumerator Run(float dt)
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.QuickRace;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            var chain = Object.FindAnyObjectByType<DesignedMapChain>();
            Assert.IsNotNull(chain, "tasarım haritası zinciri yok");
            // rampalı aracı içeren bir harita prefab'ını koşucunun önüne kur
            Obstacle ramp = null;
            foreach (var map in Resources.FindObjectsOfTypeAll<DesignedMap>())
            {
                if (!map.gameObject.scene.IsValid() || !map.gameObject.activeInHierarchy) continue;
                foreach (var o in map.Obstacles)
                    if (o != null && o.name.Contains("ramp_vehicle") && o.transform.position.z > player.transform.position.z + 20f) { ramp = o; break; }
                if (ramp != null) break;
            }
            if (ramp == null)
            {   // görünür haritalarda yoksa: bir prefab'ı doğrudan koy
                foreach (var pf in Resources.FindObjectsOfTypeAll<DesignedMap>())
                {
                    if (pf.gameObject.scene.IsValid()) continue;
                    foreach (var o in pf.Obstacles) if (o != null && o.name.Contains("ramp_vehicle")) { ramp = o; break; }
                    if (ramp == null) continue;
                    var inst = Object.Instantiate(pf, new Vector3(player.LaneOriginX, player.GroundHeight, player.transform.position.z + 30f - ramp.transform.localPosition.z), Quaternion.identity);
                    foreach (var o in inst.Obstacles) if (o != null && o.name == ramp.name) ramp = o;
                    break;
                }
            }
            Assert.IsNotNull(ramp, "rampalı araç bulunamadı");
            float rz = ramp.MinZ;
            int lane = Mathf.RoundToInt((ramp.transform.position.x - player.LaneOriginX) / player.LaneWidth);
            // rampa dışındaki engelleri kaldır, koşucuyu rampanın şeridine 22 m geriye al
            foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
                if (o != ramp && !o.name.Contains("plank")) o.gameObject.SetActive(false);
            var p = player.transform.position;
            player.Teleport(new Vector3(player.LaneOriginX + lane * player.LaneWidth, player.GroundHeight, rz - 22f), player.GroundHeight, player.LaneOriginX);
            typeof(PlayerController).GetField("lane", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(player, lane);
            Time.captureDeltaTime = dt;
            float maxY = 0f;
            for (int f = 0; f < 900 && player.transform.position.z < rz + 4.5f; f++)
            {
                maxY = Mathf.Max(maxY, player.transform.position.y - player.GroundHeight);
                Assert.IsFalse(player.IsDead, $"rampada öldü (z={player.transform.position.z - rz:F2} m, y={player.transform.position.y:F2})");
                yield return null;
            }
            Time.captureDeltaTime = 0f;
            Assert.Greater(maxY, 2.0f, "rampadan aracın üstüne yürüyerek çıkmalı");
        }

        [TearDown]
        public void Reset() => Time.captureDeltaTime = 0f;
    }
}
