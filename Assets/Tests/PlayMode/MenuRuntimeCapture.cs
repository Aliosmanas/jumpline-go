using System.Collections;
using System.IO;
using Jumpline.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Arayüz kontrolü: ana menü OYUN ÇALIŞIRKEN (yerelleştirme, dolu listeler, dil işaretleri gerçek haliyle) her ekranı
    /// telefon oranlarında çizer → Logs/MenuRuntime/&lt;GxY&gt;/&lt;ekran&gt;.png. Elle: -testFilter MenuRuntimeCapture
    /// </summary>
    [Explicit, Category("Concept")]
    public class MenuRuntimeCapture
    {
        [UnityTest]
        public IEnumerator Capture_S23() => Run(1080, 2340);

        [UnityTest]
        public IEnumerator Capture_Tall() => Run(1080, 2400);

        [UnityTest]
        public IEnumerator Capture_Short() => Run(1080, 1920);

        private static IEnumerator Run(int w, int h)
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            SceneManager.LoadScene("MainMenu");
            yield return new WaitForSeconds(1.5f);
            var nav = Object.FindAnyObjectByType<MenuNavigator>(FindObjectsInactive.Include);
            Assert.IsNotNull(nav);
            var canvas = nav.GetComponent<Canvas>();
            var camGo = new GameObject("CaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "MenuRuntime", $"{w}x{h}");
            Directory.CreateDirectory(dir);
            foreach (MenuScreen s in nav.GetComponentsInChildren<MenuScreen>(true))
            {
                if (s.Id == "home") nav.OpenRoot("home"); else nav.Open(s.Id);
                yield return new WaitForSeconds(0.9f);   // geçiş animasyonu biter
                Canvas.ForceUpdateCanvases();
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(dir, s.Id + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                nav.OpenRoot("home");
                yield return new WaitForSeconds(0.3f);
            }
            rt.Release();
            Assert.Pass();
        }
    }
}
