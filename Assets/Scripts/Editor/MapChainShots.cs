using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Harita zinciri kontrol görüntüleri: oyun sahnesinde zinciri kurar, oyun kamerasından ileriye doğru birkaç noktadan
    /// çeker → Logs/MapChainShots/*.png. Menü: Jumpline → Map Chain Shots (toplu modda da çalışır).
    /// </summary>
    public static class MapChainShots
    {
        [MenuItem("Jumpline/Map Chain Shots")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(GameplaySceneBuilder.ScenePath);
            var chain = Object.FindFirstObjectByType<Jumpline.Level.MapChain>();
            var cam = Camera.main;
            if (chain == null || cam == null) { Debug.LogError("[Jumpline] Zincir ya da kamera yok"); return; }
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "MapChainShots");
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(1280, 720, 24);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            Vector3 camOffset = cam.transform.position;
            for (int i = 0; i < 8; i++)
            {
                float z = i * 75f;
                chain.Rebuild(z);
                cam.transform.position = new Vector3(camOffset.x, camOffset.y, z + camOffset.z);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"zincir_{i:00}.png"), tex.EncodeToPNG());
            }
            RenderTexture.active = null; cam.targetTexture = null;
            Debug.Log("[Jumpline] Zincir görüntüleri: " + dir);
        }
    }
}
