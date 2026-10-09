using System.Collections;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Güçler: kalkan bir çarpmayı emer, hız gücü hızlandırır ve engeli parçalar, güç kutusu toplanınca etkinleşir.</summary>
    public class PowerUpTests
    {
        private PlayerController player;
        private PowerUps pu;

        [UnitySetUp]
        public IEnumerator Load()
        {
            GameSession.Mode = RunMode.QuickRace;   // canavarsız: yalın koşu
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e6f, 1e6f);
                sp.enabled = false;
            }
            player = Object.FindAnyObjectByType<PlayerController>();
            pu = player.GetComponent<PowerUps>();
            Time.timeScale = 1f;
        }

        private Obstacle WallAhead(float dist)
        {
            Obstacle tpl = null;
            foreach (var o in Resources.FindObjectsOfTypeAll<Obstacle>())
                if (o.name == "Obs_Minibus") { tpl = o; break; }
            Assert.IsNotNull(tpl, "dolmuş şablonu yok");
            var w = Object.Instantiate(tpl);
            w.gameObject.SetActive(true);
            w.enabled = true;
            Vector3 p = player.transform.position;
            w.transform.position = new Vector3(player.LaneOriginX + player.Lane * player.LaneWidth, player.GroundHeight, p.z + dist);
            return w;
        }

        [UnityTest]
        public IEnumerator NoPowerUp_CrashKills()
        {
            yield return new WaitForSeconds(0.5f);
            WallAhead(12f);
            yield return new WaitForSeconds(2f);
            Assert.IsTrue(player.IsDead, "kontrol: güçsüz dolmuşa çarpınca ölür");
        }

        [UnityTest]
        public IEnumerator Shield_AbsorbsOneCrash()
        {
            Assert.IsNotNull(pu, "koşucuda PowerUps yok");
            yield return new WaitForSeconds(0.5f);
            pu.Activate(PowerUpType.Shield);
            WallAhead(12f);
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(player.IsDead, "kalkan çarpmayı emer");
            Assert.IsFalse(pu.IsActive(PowerUpType.Shield), "kalkan bir kez emer");
        }

        [UnityTest]
        public IEnumerator SpeedBoost_FasterAndSmashes()
        {
            yield return new WaitForSeconds(0.5f);
            float before = player.RunSpeed;
            pu.Activate(PowerUpType.SpeedBoost);
            yield return null;
            Assert.Greater(player.RunSpeed, before * 1.25f, "hız gücü koşuyu hızlandırır");
            WallAhead(14f);
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(player.IsDead, "hız gücünde engel parçalanır");
            Assert.IsTrue(pu.IsActive(PowerUpType.SpeedBoost), "hız gücü süresince sürer");
        }

        [UnityTest]
        public IEnumerator Durations_ComeFromUpgrades()
        {
            yield return null;
            Assert.AreEqual(8f, pu.DurationOf(PowerUpType.Magnet), 0.01f, "mıknatıs taban süre 8 sn");
            Assert.AreEqual(5f, pu.DurationOf(PowerUpType.SpeedBoost), 0.01f);
        }
    }
}
