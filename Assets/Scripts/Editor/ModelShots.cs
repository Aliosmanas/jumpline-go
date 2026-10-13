using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Model referans fotoğrafları (Meshy AI vb. için): engeller, canavarlar, koşucu, atılan eşyalar — her biri
    /// ön / yan / 3-4 açıdan düz açık gri zeminde 1024px → Logs/ModelShots. Menü: Jumpline → Model Shots.
    /// </summary>
    public static class ModelShots
    {
        private const string OutDir = "Logs/ModelShots";
        private const int Size = 1024;
        private static readonly (string folder, string category)[] Sources =
        {
            ("Assets/Art/Environment/Obstacles", "engel"),
            ("Assets/Art/Boss", "canavar"),
            ("Assets/Art/Characters/Runner", "karakter"),
        };
        private static readonly (string name, float yaw)[] Angles = { ("on", 180f), ("yan", 90f), ("ucdortte", 145f) };

        [MenuItem("Jumpline/Model Shots")]
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutDir);
            var light = new GameObject("Key", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var fill = new GameObject("Fill", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.transform.rotation = Quaternion.Euler(20f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.62f, 0.62f, 0.66f);

            var cam = new GameObject("ShotCam", typeof(Camera)).GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.86f, 0.86f, 0.88f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt;

            int count = 0;
            foreach (var (folder, category) in Sources)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.position = Vector3.zero;
                    var rends = go.GetComponentsInChildren<Renderer>();
                    if (rends.Length == 0) { Object.DestroyImmediate(go); continue; }
                    Bounds b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    float radius = b.extents.magnitude;
                    float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                    cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
                    cam.farClipPlane = dist + radius * 2f;
                    foreach (var (angle, yaw) in Angles)
                    {
                        Quaternion rot = Quaternion.Euler(12f, yaw, 0f);
                        cam.transform.position = b.center - rot * Vector3.forward * dist;
                        cam.transform.rotation = rot;
                        cam.Render();
                        Save(rt, Path.Combine(OutDir, $"{category}_{Path.GetFileNameWithoutExtension(path)}_{angle}.png"));
                        count++;
                    }
                    Object.DestroyImmediate(go);
                }
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Debug.Log($"[Jumpline] Model fotoğrafları: {count} kare → {OutDir}");
        }

        private static void Save(RenderTexture rt, string path)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
