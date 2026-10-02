using System.IO;
using Jumpline.UI;
using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Açık ana menü sahnesindeki her ekranı referans çözünürlüğünde (1116x2000) PNG'ye çizer:
    /// Logs/MenuCaptures/{id}.png. Referansla yan yana kıyas için.
    /// </summary>
    public static class MenuScreenCapture
    {
        private const int Width = 1116;
        private const int Height = 2000;
        private const string OutDir = "Logs/MenuCaptures";

        [MenuItem("Jumpline/Capture Menu Screens")]
        public static void CaptureAll()
        {
            var navigator = Object.FindAnyObjectByType<MenuNavigator>(FindObjectsInactive.Include);
            if (navigator == null) { Debug.LogWarning("[Jumpline] MenuNavigator bulunamadı; önce MainMenu sahnesini aç."); return; }
            var canvas = navigator.GetComponent<Canvas>();
            MenuScreen[] screens = navigator.GetComponentsInChildren<MenuScreen>(true);
            GameObject nav = navigator.transform.Find("SafeArea/BottomNav").gameObject;

            Directory.CreateDirectory(OutDir);
            var camGo = new GameObject("CaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;

            // Play modunda gizli ekranların saydamlığı 0'dır; çekim için geçici olarak aç.
            var alphas = new float[screens.Length];
            for (int i = 0; i < screens.Length; i++)
            {
                alphas[i] = screens[i].Group.alpha;
                screens[i].Group.alpha = 1f;
            }
            bool[] wasActive = System.Array.ConvertAll(screens, s => s.gameObject.activeSelf);
            bool navWasActive = nav.activeSelf;

            RenderMode previousMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10f;
            try
            {
                foreach (MenuScreen target in screens)
                {
                    foreach (MenuScreen s in screens) s.gameObject.SetActive(s == target);
                    nav.SetActive(target.ShowBottomNav);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    Save(rt, Path.Combine(OutDir, target.Id + ".png"));
                }
            }
            finally
            {
                for (int i = 0; i < screens.Length; i++)
                {
                    screens[i].gameObject.SetActive(Application.isPlaying ? wasActive[i] : true);
                    screens[i].Group.alpha = alphas[i];
                }
                nav.SetActive(Application.isPlaying ? navWasActive : true);
                canvas.renderMode = previousMode;
                canvas.worldCamera = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGo);
            }
            Debug.Log($"[Jumpline] Menü ekranları kaydedildi: {OutDir}");
        }

        private static void Save(RenderTexture rt, string path)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
