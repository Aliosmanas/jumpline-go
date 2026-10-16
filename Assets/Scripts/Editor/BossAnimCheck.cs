using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Kontrol aracı: canavar modellerini her klibin birkaç anında (Playable ile, oyundaki gibi) poza sokar, deriyi
    /// BakeMesh ile dondurup render eder → Logs/BossCheck/&lt;ad&gt;.png (sütun = klip, satır = an).
    /// </summary>
    public static class BossAnimCheck
    {
        private static readonly string[] Clips = { "Idle", "Run", "Roar", "Throw", "Hit", "Defeat" };
        private static readonly float[] Phases = { 0.2f, 0.55f };
        private const int Cell = 256;

        [MenuItem("Jumpline/Check/Boss Animations")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs/BossCheck");
            foreach (var path in Directory.GetFiles("Assets/Art/Boss", "*.fbx"))
            {
                string p = path.Replace('\\', '/');
                if (p.EndsWith("Block.fbx") || p.EndsWith("Throwables.fbx")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (prefab == null) continue;
                var clips = new Dictionary<string, AnimationClip>();
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (o is AnimationClip c && !c.name.StartsWith("__preview")) clips[c.name] = c;
                RuntimeAnimatorController ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Path.ChangeExtension(p, ".controller"));
                if (ctrl != null) foreach (var c in ctrl.animationClips) clips[c.name] = c;   // oyundaki controller'ın klipleri öncelikli
                Shoot(prefab, clips, $"Logs/BossCheck/{prefab.name}.png");
            }
            Debug.Log("[Jumpline] BOSSCHECK_OK");
        }

        public static void Shoot(GameObject prefab, Dictionary<string, AnimationClip> clips, string outPath)
        {
            var go = Object.Instantiate(prefab);
            go.transform.position = new Vector3(0f, -500f, 0f);
            var anim = go.GetComponentInChildren<Animator>() ?? go.AddComponent<Animator>();
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            Bounds b0 = smrs[0].bounds; foreach (var s in smrs) b0.Encapsulate(s.bounds);
            float H = Mathf.Max(b0.size.y, b0.size.x, b0.size.z);

            var camGo = new GameObject("CheckCam"); var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = H * 0.75f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.16f, 0.16f, 0.18f);
            Vector3 c = b0.center; Vector3 dir = new Vector3(0.75f, 0.25f, 1f).normalized;
            cam.transform.position = c + dir * H * 4f; cam.transform.LookAt(c);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = H * 10f;
            var lightGo = new GameObject("CheckLight"); var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Directional; lt.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(35f, 200f, 0f);
            var rt = new RenderTexture(Cell, Cell, 24); cam.targetTexture = rt;

            var sheet = new Texture2D(Cell * Clips.Length, Cell * Phases.Length, TextureFormat.RGB24, false);
            var holders = new List<GameObject>();
            foreach (var s in smrs) s.enabled = false;
            for (int ci = 0; ci < Clips.Length; ci++)
            {
                if (!clips.TryGetValue(Clips[ci], out var clip)) continue;
                for (int pi = 0; pi < Phases.Length; pi++)
                {
                    clip.SampleAnimation(anim.gameObject, clip.length * Phases[pi]);
                    foreach (var h in holders) Object.DestroyImmediate(h);
                    holders.Clear();
                    foreach (var s in smrs)
                    {
                        var m = new Mesh(); s.BakeMesh(m, true);
                        var h = new GameObject("baked"); h.transform.SetPositionAndRotation(s.transform.position, s.transform.rotation);
                        h.AddComponent<MeshFilter>().sharedMesh = m;
                        h.AddComponent<MeshRenderer>().sharedMaterials = s.sharedMaterials;
                        holders.Add(h);
                    }
                    cam.Render();
                    RenderTexture.active = rt;
                    var tmp = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                    tmp.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0); tmp.Apply();
                    sheet.SetPixels(ci * Cell, (Phases.Length - 1 - pi) * Cell, Cell, Cell, tmp.GetPixels());
                    Object.DestroyImmediate(tmp);
                    RenderTexture.active = null;
                }
            }
            sheet.Apply();
            File.WriteAllBytes(outPath, sheet.EncodeToPNG());
            foreach (var h in holders) Object.DestroyImmediate(h);
            Object.DestroyImmediate(go); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo);
            rt.Release();
        }
    }
}
