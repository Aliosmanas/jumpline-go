using System.Collections;
using System.IO;
using System.Reflection;
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
    /// İnceleme kesitleri (elle: InspectionCapture): oyun gerçek hızında, basit otomatik pilotla koşar; kesit kesit kare
    /// dizileri → Logs/Inceleme/&lt;kesit&gt;/NNN.png. İlk 30 sn, araç tavanı, önden çarpma ölümü, yeniden doğma, canavar, menü.
    /// </summary>
    [Explicit, Category("Concept")]
    public class InspectionCapture
    {
        private const BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
        private static string Root => Path.Combine(Application.dataPath, "..", "Logs", "Inceleme");
        private PlayerController player;
        private bool pilot = true;

        private IEnumerator LoadGameplay(RunMode mode)
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", mode == RunMode.Campaign ? 0 : 1);
            GameSession.Mode = mode;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            player = Object.FindAnyObjectByType<PlayerController>();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 30f;
        }

        [TearDown]
        public void Reset() { Time.captureDeltaTime = 0f; Time.timeScale = 1f; }

        private void Invulnerable(float seconds) => typeof(PlayerController).GetField("graceUntil", NP).SetValue(player, Time.time + seconds);
        private void Call(string m, params object[] a) => typeof(PlayerController).GetMethod(m, NP).Invoke(player, a);
        private int Lane => (int)typeof(PlayerController).GetField("lane", NP).GetValue(player);

        private bool Free(int lane, float y, float from, float to)
        {
            if (lane < -1 || lane > 1) return false;
            float x = player.LaneOriginX + lane * player.LaneWidth, z0 = player.transform.position.z;
            for (float z = z0 + from; z < z0 + to; z += 0.5f)
                if (ObstacleField.SurfaceAt(x, z, player.GroundHeight) > y + 0.35f || ObstacleField.HitsCeiling(x, z, y, y + 1.7f)) return false;
            return true;
        }

        /// <summary>Basit pilot: önü kapalıysa boş şeride geç, yoksa zıpla ya da kay.</summary>
        private void Pilot()
        {
            if (!pilot || player.IsDead || player.IsAirborne || player.IsSliding) return;
            float y = player.transform.position.y, look = Mathf.Max(6f, player.RunSpeed * 0.55f);
            int l = Lane;
            if (Free(l, y, 1f, look)) return;
            foreach (int d in new[] { -1, 1 })
                if (Free(l + d, y, 0f, look)) { Call("ChangeLane", d); return; }
            float x = player.LaneOriginX + l * player.LaneWidth, z = player.transform.position.z;
            bool ceiling = false;
            for (float k = 1f; k < look; k += 0.5f) ceiling |= ObstacleField.HitsCeiling(x, z + k, y, y + 1.7f) && !ObstacleField.HitsCeiling(x, z + k, y, y + 1.0f);
            if (ceiling) Call("StartSlide"); else Call("Jump");
        }

        private IEnumerator Run(string seq, float seconds, float every, bool pilotOn = true)
        {
            pilot = pilotOn;
            string dir = Path.Combine(Root, seq);
            Directory.CreateDirectory(dir);
            int n = 0;
            float next = 0f;
            for (float t = 0f; t < seconds; t += Time.captureDeltaTime)
            {
                Pilot();
                yield return null;
                if (t < next) continue;
                next += every;
                yield return Shoot(Path.Combine(dir, $"{n++:000}.png"));
            }
        }

        private static IEnumerator Shoot(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            try { tex = ScreenCapture.CaptureScreenshotAsTexture(); } catch { }
            if (tex == null || tex.width < 200)
            {
                if (tex != null) Object.Destroy(tex);
                var cam = Camera.main;
                var rt = new RenderTexture(720, 1280, 24);
                cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                RenderTexture.active = rt;
                tex = new Texture2D(720, 1280, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0); tex.Apply();
                RenderTexture.active = null; rt.Release();
            }
            File.WriteAllBytes(path, tex.EncodeToJPG(82));
            Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator A_Ilk30Saniye_Canavar()
        {
            yield return LoadGameplay(RunMode.Campaign);
            Invulnerable(1000f);
            yield return Run("01_ilk_30_saniye_kampanya", 30f, 1f);
            yield return Run("06_canavar_kovalamasi", 12f, 0.4f);
        }

        [UnityTest]
        public IEnumerator B_AracTavani()
        {
            yield return LoadGameplay(RunMode.QuickRace);
            Invulnerable(1000f);
            yield return Run("_isinma", 1f, 99f);
            // en yakın otobüs/minibüs tavanına koy
            Obstacle best = null;
            foreach (var o in Object.FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
                if (o.isActiveAndEnabled && (o.name.Contains("CityBus") || o.name.Contains("Minibus")) && o.transform.position.z > player.transform.position.z + 15f
                    && (best == null || o.transform.position.z < best.transform.position.z)) best = o;
            Assume.That(best != null, "araç yok");
            float z = best.MinZ + 1f, x = best.transform.position.x;
            float top = ObstacleField.SurfaceAt(x, z + 1f, player.GroundHeight);
            typeof(PlayerController).GetField("lane", NP).SetValue(player, Mathf.RoundToInt((x - player.LaneOriginX) / player.LaneWidth));
            player.Teleport(new Vector3(x, top + 0.05f, z), player.GroundHeight, player.LaneOriginX);
            Object.FindAnyObjectByType<Jumpline.CameraSystem.CameraFollow>()?.Snap();
            yield return Run("03_arac_tavaninda_kosu", 6f, 0.25f);
        }

        [UnityTest]
        public IEnumerator C_OndenCarpma_YenidenDogma()
        {
            yield return LoadGameplay(RunMode.QuickRace);
            Invulnerable(4f);
            yield return Run("02_ondan_carpma_olumu", 5f, 0.5f);      // ısınma + ilk kareler
            pilot = false;
            Invulnerable(0f);
            string dir = Path.Combine(Root, "02_ondan_carpma_olumu");
            int n = 100;
            for (int f = 0; f < 30 * 14 && !player.IsDead; f++)
            {
                yield return null;
                if (f % 6 == 0) yield return Shoot(Path.Combine(dir, $"{n++:000}.png"));
            }
            for (int f = 0; f < 30 * 3; f += 6) { yield return Shoot(Path.Combine(dir, $"{n++:000}.png")); for (int k = 0; k < 6; k++) yield return null; }
            // yeniden doğ (elmasla devam)
            SaveManager.Gems = 999;
            var flow = Object.FindAnyObjectByType<LevelFlowController>();
            flow.StartCoroutine((IEnumerator)typeof(LevelFlowController).GetMethod("ReviveRoutine", NP).Invoke(flow, null));
            string rd = Path.Combine(Root, "09_yeniden_dogma");
            Directory.CreateDirectory(rd);
            for (int i = 0; i < 12; i++) { yield return Shoot(Path.Combine(rd, $"{i:000}.png")); for (int k = 0; k < 8; k++) yield return null; }
            yield return Run("09_yeniden_dogma_sonrasi", 8f, 0.4f, false);
        }

        [UnityTest]
        public IEnumerator D_Menu()
        {
            Time.captureDeltaTime = 1f / 30f;
            SceneManager.LoadScene("MainMenu");
            for (int i = 0; i < 20; i++) yield return null;
            string dir = Path.Combine(Root, "10_menu");
            Directory.CreateDirectory(dir);
            yield return Shoot(Path.Combine(dir, "000.png"));
        }

        [UnityTest]
        public IEnumerator E_UzunKosu_Gorsel()
        {
            yield return LoadGameplay(RunMode.QuickRace);
            Invulnerable(1000f);
            yield return Run("05_gorsel_kesitler_uzun_kosu", 75f, 2.5f);
        }
    }
}
