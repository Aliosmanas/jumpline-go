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

        public static void Shot(string name)
        {
            Directory.CreateDirectory(Dir);
            var cam = Camera.main;
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
        public IEnumerator CaptureChase()
        {
            GameSession.Mode = RunMode.Campaign;
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
    }
}
