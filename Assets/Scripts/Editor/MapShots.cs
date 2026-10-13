using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>Oyun dünyası fotoğrafları (sokak + çatı): oyuncu gözü ve kuşbakışı → Logs/MapShots. Menü: Jumpline → Map Shots.</summary>
    public static class MapShots
    {
        private const string OutDir = "Logs/MapShots";

        [MenuItem("Jumpline/Map Shots")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(GameplaySceneBuilder.ScenePath);
            Directory.CreateDirectory(OutDir);
            var roots = new[] { "Street", "Rooftop", "BridgeArena" };   // köprü finali (düşüş bölümü) dahil
            var objs = new GameObject[roots.Length];
            for (int i = 0; i < roots.Length; i++) objs[i] = FindIncludingInactive(roots[i]);
            var cam = new GameObject("MapShotCam", typeof(Camera)).GetComponent<Camera>();
            var main = Camera.main;
            if (main != null) { cam.CopyFrom(main); cam.targetTexture = null; }
            cam.farClipPlane = 600f;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;   // kuşbakışında sis her şeyi beyazlatıyor
            for (int i = 0; i < objs.Length; i++)
            {
                if (objs[i] == null) continue;
                bool[] was = System.Array.ConvertAll(objs, o => o != null && o.activeSelf);
                for (int k = 0; k < objs.Length; k++) if (objs[k] != null) objs[k].SetActive(k == i);
                Bounds b = new Bounds(objs[i].transform.position, Vector3.one);
                foreach (var r in objs[i].GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                // koşu yüzeyi: şeridin başında yukarıdan aşağı ışın
                float baseY = b.min.y;
                if (Physics.Raycast(new Vector3(b.center.x, b.max.y + 50f, b.min.z + 20f), Vector3.down, out RaycastHit hit, 500f)) baseY = hit.point.y;
                // oyuncu gözü: şeridin başında, arkadan
                if (roots[i] == "Street")
                    Shot(cam, rt, new Vector3(b.center.x, baseY + 4.5f, b.min.z + 6f), Quaternion.Euler(14f, 0f, 0f), $"{roots[i]}_oyuncu_gozu");
                else   // çatı / köprü: koşu yüzeyi collider'sız; en yüksek noktanın biraz üstünden yakın çapraz
                    Shot(cam, rt, new Vector3(b.center.x - 12f, b.max.y + 6f, b.min.z + 4f), Quaternion.Euler(28f, 12f, 0f), $"{roots[i]}_yakin");
                // kuşbakışı çapraz: ilk ~120 m
                Shot(cam, rt, new Vector3(b.center.x - 45f, baseY + 55f, b.min.z - 10f), Quaternion.Euler(35f, 35f, 0f), $"{roots[i]}_kusbakisi");
                // tepeden: tüm uzunluk
                float len = Mathf.Min(b.size.z, 400f);
                Shot(cam, rt, new Vector3(b.center.x, baseY + len * 0.9f, b.min.z + len * 0.5f), Quaternion.Euler(90f, 0f, 0f), $"{roots[i]}_tepeden");
                if (roots[i] == "BridgeArena")
                {
                    // yandan (nehir ve düşüş görünsün) + köprü sonu (atlama/düşüş kenarı)
                    Shot(cam, rt, new Vector3(b.center.x + 70f, baseY + 10f, b.center.z), Quaternion.Euler(8f, -90f, 0f), "BridgeArena_yandan");
                    Shot(cam, rt, new Vector3(b.center.x - 15f, b.max.y + 8f, b.max.z - 70f), Quaternion.Euler(25f, 10f, 0f), "BridgeArena_kopru_sonu");
                    Shot(cam, rt, new Vector3(b.center.x + 25f, baseY + 20f, b.max.z + 25f), Quaternion.Euler(25f, 225f, 0f), "BridgeArena_sondan_geriye");
                }
                for (int k = 0; k < objs.Length; k++) if (objs[k] != null) objs[k].SetActive(was[k]);
            }
            RenderSettings.fog = fog;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(cam.gameObject);
            Debug.Log($"[Jumpline] Harita fotoğrafları → {OutDir}");
        }

        private static GameObject FindIncludingInactive(string name)
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name == name) return go;
            return null;
        }

        private static void Shot(Camera cam, RenderTexture rt, Vector3 pos, Quaternion rot, string name)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
