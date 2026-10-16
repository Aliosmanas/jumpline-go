using System.Collections;
using System.IO;
using Jumpline.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Tasarım önizlemesi için ARAYÜZSÜZ oyun kareleri (oyunu değiştirmez): ana kamera RenderTexture'a çizilir
    /// (ekran üstü arayüz dahil olmaz) → Logs/Concept/*.png. Elle çalıştırılır: -testFilter ConceptCapture
    /// </summary>
    [Explicit, Category("Concept")]
    public class ConceptCapture
    {
        [UnityTest]
        public IEnumerator Level1_Clean() => Shoot(1, "concept_lv1");

        [UnityTest]
        public IEnumerator Level2_Clean() => Shoot(2, "concept_lv2");

        /// <summary>Arayüzlü oyun karesi (HUD tema kontrolü) → Logs/Concept/hud_lv1.png.</summary>
        [UnityTest]
        public IEnumerator Level1_Hud()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            PlayerPrefs.SetInt("jl.introSeen.1", 1);
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return new WaitForSeconds(4.5f);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "Concept");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, "hud_lv1.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            Assert.Pass();
        }

        private static IEnumerator Shoot(int level, string name)
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            PlayerPrefs.SetInt("jl.introSeen." + level, 1);
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = level;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
                            Jumpline.Level.Course.ClearRange(-1e6f, 40f);   // yakın plan boş, ileride gerçek engeller
            yield return new WaitForSeconds(3.2f);
            var cam = Camera.main;
            var rt = new RenderTexture(1080, 1920, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "Concept");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            GameSession.Level = 1;
            Assert.Pass();
        }
    }
}
