using System.Collections;
using System.IO;
using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Görsel kontrol için oyun sahnesinden 1080x1920 kareler kaydeder (Logs/Captures). Elle çalıştırılır:
    /// -runTests -testPlatform PlayMode -testFilter GameplayCapture
    /// </summary>
    [Explicit, Category("Capture")]
    public class GameplayCapture
    {
        private static string Dir => Path.Combine(Application.dataPath, "..", "Logs", "Captures");

        public static void Shot(string name, Camera cam = null)
        {
            Directory.CreateDirectory(Dir);
            if (cam == null) cam = Camera.main;
            var rt = new RenderTexture(1080, 1920, 24);
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var modes = new RenderMode[canvases.Length];
            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                if (canvases[i].isRootCanvas && modes[i] == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[i].worldCamera = cam;
                    canvases[i].planeDistance = 1f;
                }
            }
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            for (int i = 0; i < canvases.Length; i++) canvases[i].renderMode = modes[i];
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToJPG(85));
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator CaptureBossCloseup()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
                sp.ClearRange(-1e6f, 1e6f);
            var boss = Object.FindAnyObjectByType<BossChase>();
            var main = Camera.main;
            var close = new GameObject("CloseCam").AddComponent<Camera>();
            close.fieldOfView = 40f;
            close.enabled = false;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            void Frame(string name, Vector3 offset)
            {
                Vector3 b = boss.Position;
                close.transform.position = b + offset;
                close.transform.LookAt(b + Vector3.up * 1.9f);
                Shot(name, close);
            }
            yield return new WaitForSeconds(0.7f);
            Frame("boss_intro_front", new Vector3(1.5f, 2.2f, -7f));
            yield return new WaitForSeconds(3.5f);
            for (int i = 0; i < 3; i++)
            {
                Frame("boss_run_side_" + i, new Vector3(7f, 2f, 0.5f));
                yield return new WaitForSeconds(0.11f);
            }
            Frame("boss_run_back", new Vector3(1.5f, 2.6f, -7f));
            boss.TakeHit();
            yield return new WaitForSeconds(0.15f);
            Frame("boss_hit", new Vector3(5f, 2.4f, -4f));
            Assert.Pass();
        }

        private static Transform FindDeep(Transform t, string n)
        {
            if (t.name == n) return t;
            foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
            return null;
        }

        [UnityTest]
        public IEnumerator CaptureBossPoses()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var boss = Object.FindAnyObjectByType<BossChase>();
            boss.enabled = false;
            var anim = boss.GetComponentInChildren<Animator>();
            var cam = new GameObject("PoseCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 40f;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            Vector3 b = boss.transform.position;
            cam.transform.position = b + new Vector3(4f, 2.4f, -9f);
            cam.transform.LookAt(b + Vector3.up * 1.9f);
            Debug.Log($"[PoseCheck] animator={anim.name} ctrl={anim.runtimeAnimatorController} avatar={anim.avatar} valid={(anim.avatar != null && anim.avatar.isValid)} init={anim.isInitialized} enabled={anim.enabled} active={anim.isActiveAndEnabled}");
            foreach (var smr in boss.GetComponentsInChildren<SkinnedMeshRenderer>())
                Debug.Log($"[PoseCheck] smr={smr.name} bones={smr.bones.Length} root={smr.rootBone}");
            anim.enabled = false;
            yield return null;
            Shot("pose_bind", cam);
            anim.enabled = true;
            foreach (string st in new[] { "Run", "Roar", "Throw", "Hit", "Defeat" })
            {
                anim.Play(st, 0, 0.3f);
                anim.Update(0f);
                yield return null;
                Debug.Log("[PoseCheck] " + st + " playing=" + anim.GetCurrentAnimatorStateInfo(0).IsName(st));
                Transform foot = FindDeep(anim.transform, "foot_l"), ball = FindDeep(anim.transform, "ball_l");
                if (foot != null && ball != null) Debug.Log("[PoseCheck] toes dir " + (ball.position - foot.position).normalized);
                Shot("pose_" + st, cam);
            }
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureFinale()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e6f, 1e6f);
                sp.enabled = false;
            }
            var boss = Object.FindAnyObjectByType<BossChase>();
            var sw = Object.FindAnyObjectByType<Jumpline.Level.WorldSwitcher>();
            yield return new WaitForSeconds(4f);
            for (int i = 0; i < 3; i++) { boss.TakeHit(); yield return new WaitForSeconds(0.4f); }
            Shot("finale_01_announce");
            for (float t = 0f; t < 6f && !sw.InArena; t += Time.deltaTime) yield return null;
            yield return new WaitForSeconds(2.5f);
            Shot("finale_02_bridge");
            boss.TakeHit();
            yield return new WaitForSecondsRealtime(0.7f);
            Shot("finale_03_stumble");
            yield return new WaitForSecondsRealtime(0.9f);
            Shot("finale_04_fall");
            yield return new WaitForSecondsRealtime(0.95f);
            Shot("finale_05_splash");
            yield return new WaitForSecondsRealtime(2.5f);
            Shot("finale_06_complete");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureThrowHit()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e6f, 1e6f);
                sp.enabled = false;
            }
            var boss = Object.FindAnyObjectByType<BossChase>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            var throws = Object.FindAnyObjectByType<ThrowSystem>();
            yield return new WaitForSeconds(4.5f);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var held = (System.Collections.Generic.Stack<int>)typeof(ThrowSystem).GetField("held", flags).GetValue(throws);
            for (int k = 0; k < 2; k++)
            {
                held.Push(k);
                typeof(BossChase).GetField("lane", flags).SetValue(boss, player.Lane);
                typeof(BossChase).GetField("telegraphUntil", flags).SetValue(boss, -1f);
                typeof(BossChase).GetField("nextLaneChange", flags).SetValue(boss, Time.time + 30f);
                throws.Throw();
                yield return new WaitForSeconds(0.3f);
                Shot("throw_" + k + "_flight");
                int hearts = boss.Hearts;
                for (float t = 0f; t < 2f && boss.Hearts == hearts; t += Time.deltaTime) yield return null;
                yield return new WaitForSecondsRealtime(0.12f);
                Shot("throw_" + k + "_hit");
                yield return new WaitForSeconds(2f);
            }
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CapturePowerUps()
        {
            GameSession.Mode = RunMode.QuickRace;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
                sp.ClearRange(-1e6f, 140f);
            var field = Object.FindAnyObjectByType<Jumpline.Level.PowerUpField>();
            typeof(Jumpline.Level.PowerUpField).GetField("nextDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(field, 5f);
            yield return new WaitForSeconds(4.2f);
            Shot("powerup_01_box");
            var pu = Object.FindAnyObjectByType<PowerUps>();
            pu.Activate(PowerUpType.Magnet);
            yield return new WaitForSeconds(0.6f);
            pu.Activate(PowerUpType.DoubleGold);
            yield return new WaitForSeconds(1.2f);
            Shot("powerup_02_hud");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureIntro()
        {
            UnityEngine.PlayerPrefs.DeleteKey("jl.introSeen");
            GameSession.Mode = RunMode.Campaign;
            SceneManager.LoadScene("Gameplay");
            yield return new WaitForSecondsRealtime(2.2f);
            Shot("intro_01");
            var tap = GameObject.Find("TapArea").GetComponent<UnityEngine.UI.Button>();
            for (int i = 0; i < 6; i++) { tap.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.15f); }
            yield return new WaitForSecondsRealtime(2.2f);
            Shot("intro_02");
            tap.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.2f); tap.onClick.Invoke();
            yield return new WaitForSecondsRealtime(2.5f);
            Shot("intro_03");
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1);
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureDaily()
        {
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1);
            GameSession.Mode = RunMode.Daily;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
                sp.ClearRange(-1e6f, 120f);
            yield return new WaitForSecondsRealtime(2.5f);
            Shot("daily_01_dialogue");
            var tap = GameObject.Find("TapArea").GetComponent<UnityEngine.UI.Button>();
            for (int i = 0; i < 12 && Time.timeScale < 0.5f; i++) { tap.onClick.Invoke(); yield return new WaitForSecondsRealtime(0.25f); }
            yield return new WaitForSeconds(3f);
            Shot("daily_02_chase");
            // diğer hırsız: elle aç (iki görünüşü de görmek için)
            var dm = Object.FindAnyObjectByType<Jumpline.Level.DailyMission>();
            var missions = (Jumpline.Level.DailyMission.Mission[])typeof(Jumpline.Level.DailyMission).GetField("missions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(dm);
            var cur = (Jumpline.Level.DailyMission.Mission)typeof(Jumpline.Level.DailyMission).GetField("m", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(dm);
            foreach (var mm in missions)
                if (mm.thief != cur.thief) { mm.thief.SetActive(true); mm.thief.transform.position = cur.thief.transform.position + new Vector3(2.2f, 0f, -4f); }
            yield return new WaitForSeconds(0.2f);
            var cam = new GameObject("DCam").AddComponent<Camera>();
            cam.enabled = false;
            cam.fieldOfView = 35f;
            Vector3 c = cur.thief.transform.position;
            cam.transform.position = c + new Vector3(5f, 2.2f, -7f);
            cam.transform.LookAt(c + new Vector3(1f, 0.9f, -2f));
            foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) cv.enabled = false;
            Shot("daily_03_thieves", cam);
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureChase()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>(FindObjectsSortMode.None))
                sp.ClearRange(-1e6f, 95f);   // ilk 95 m boş: koşucu çekim sırasında çarpmasın, ilerisi gerçek parkur
            yield return new WaitForSeconds(0.6f);
            Shot("chase_01_intro");
            yield return new WaitForSeconds(4.5f);
            var throws = Object.FindAnyObjectByType<ThrowSystem>();
            var held = (System.Collections.Generic.Stack<int>)typeof(ThrowSystem)
                .GetField("held", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(throws);
            held.Push(0);
            held.Push(1);
            var changed = typeof(ThrowSystem).GetField("Changed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            (changed.GetValue(throws) as System.Action)?.Invoke();
            yield return new WaitForSeconds(0.5f);
            Shot("chase_02_holding");
            var boss = Object.FindAnyObjectByType<BossChase>();
            boss.TakeHit();
            yield return new WaitForSeconds(0.25f);
            Shot("chase_03_hit");
            yield return new WaitForSeconds(2.5f);
            Shot("chase_04_after");
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureSahmeran()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 2;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.2", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("sahmeran_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("sahmeran_02_chase");
            Object.FindAnyObjectByType<BossChase>().TakeHit();
            yield return new WaitForSeconds(0.25f);
            Shot("sahmeran_03_hit");
            GameSession.Level = 1;
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureHumbaba()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 3;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.3", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("humbaba_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("humbaba_02_chase");
            GameSession.Level = 1;
            Assert.Pass();
        }
        [UnityTest]
        public IEnumerator CaptureStoneGuardian()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 4;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.4", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("stone_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("stone_02_chase");
            GameSession.Level = 1;
            Assert.Pass();
        }
        [UnityTest]
        public IEnumerator CaptureVanMonster()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 5;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.5", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("van_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("van_02_chase");
            GameSession.Level = 1;
            Assert.Pass();
        }
        [UnityTest]
        public IEnumerator CaptureKarakoncolos()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 6;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.6", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("kara_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("kara_02_chase");
            GameSession.Level = 1;
            Assert.Pass();
        }
        [UnityTest]
        public IEnumerator CaptureLamassu()
        {
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 7;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen.7", 1);
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            foreach (var sp in Object.FindObjectsByType<Jumpline.Level.ObstacleSpawner>())
                sp.ClearRange(-1e6f, 95f);
            yield return new WaitForSeconds(0.8f);
            Shot("lamassu_01_intro");
            yield return new WaitForSeconds(4.5f);
            Shot("lamassu_02_chase");
            GameSession.Level = 1;
            Assert.Pass();
        }
    }
}
