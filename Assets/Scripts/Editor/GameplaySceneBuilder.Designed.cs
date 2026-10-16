using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jumpline.Level;
using Jumpline.Level.Designed;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Tasarım haritaları (16 Eki 2026): Blender/scripts/map_designs.py → Assets/Data/Designed/Harita_NN.json →
    /// Assets/Prefabs/Designed/Harita_NN.prefab (engeller + çarpışma profilleri + işaretler + kenar dekoru + zemin + deniz).
    /// Blender (x sağ, y koşu, z yukarı) → Unity (x, y = Blender z, z = Blender y). Yerleşim modelin Blender'daki dünya
    /// kutusuna (min/max) oturtulur: eksen/ölçek farkı tahmin edilmez. Dönüş: Unity yaw = taban − Blender yaw
    /// (taban: oyun FBX'i 0°, Meshy .blend kaynaklı FBX 180° — eski harita aktarımıyla aynı).
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string DesignedDataDir = "Assets/Data/Designed";
        private const string DesignedPrefabDir = "Assets/Prefabs/Designed";

        [System.Serializable] private class DItem { public string m, kind, src; public int lane; public float yaw; public float[] min, max; public float y0, z0, y1, z1; }
        [System.Serializable] private class DDecor { public string m, src; public float yaw; public float[] min, max; }
        [System.Serializable] private class DRegion { public string theme; public float y0, y1; }
        [System.Serializable] private class DPortal { public int lane; public float y, z; }
        [System.Serializable] private class DMap { public string name; public float length; public string[] theme; public DRegion[] regions; public DPortal[] portals; public DItem[] items; public DDecor[] decor; }

        private static readonly HashSet<string> MeshyDirModels = new HashSet<string> { "ClayJug", "Watermelon" };
        private static readonly HashSet<string> BigDecor = new HashSet<string> { "M03_CornerBuilding", "M07_CityWall", "M08_WallTower", "M09_RooftopBlock", "M04_BazaarArch", "M11_Dome", "M10_Minaret" };

        /// <summary>Modelin Unity prefab'ı: G = oyunun engel FBX'i (Obstacles; testi/karpuz Meshy klasöründe), D = Meshy FBX.</summary>
        private static GameObject DesignedModel(string model, string src)
        {
            if (src == "D" || MeshyDirModels.Contains(model)) return MeshyPrefab(model);
            string path = $"{ObstacleDir}/{model}.fbx";
            if (designedModelCache.TryGetValue(path, out var cached) && cached != null) return cached;
            ConfigureVehicleImport(path);   // içe aktarma ayarı kurulum başına bir kez (her yerleşimde yeniden içe aktarmak Unity'yi çökertiyordu)
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            designedModelCache[path] = go;
            return go;
        }

        private static readonly Dictionary<string, GameObject> designedModelCache = new Dictionary<string, GameObject>();

        private static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>
        /// Modeli koyar ve Blender dünya kutusuna oturtur. Yaw 90°'nin katıysa her eksen ayrı ölçeklenir (bariyerin enine
        /// daraltılması gibi), değilse yükseklikten tek ölçek. Kök: haritanın kökü (dünya = harita yereli, kök 0'da kurulur).
        /// </summary>
        private static GameObject PlaceDesigned(GameObject prefab, Transform parent, string src, float yawB, float[] mn, float[] mx)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = Vector3.zero;
            float baseYaw = src == "D" ? 180f : 0f;
            go.transform.localRotation = Quaternion.Euler(0f, baseYaw - yawB, 0f) * prefab.transform.localRotation;
            go.transform.localScale = prefab.transform.localScale;
            var target = new Vector3(mx[0] - mn[0], mx[2] - mn[2], mx[1] - mn[1]);   // Unity eksenleri
            Bounds b = WorldBounds(go);
            float yawMod = Mathf.Repeat(yawB, 90f);
            bool axisAligned = yawMod < 0.5f || yawMod > 89.5f;
            if (axisAligned)
            {
                // her dünya ekseni için ona en paralel yerel ekseni ölçekle
                Vector3 ls = go.transform.localScale;
                Vector3[] axes = { go.transform.right, go.transform.up, go.transform.forward };
                for (int w = 0; w < 3; w++)
                {
                    if (b.size[w] < 1e-4f || target[w] < 1e-4f) continue;
                    int best = 0; float bd = -1f;
                    for (int a = 0; a < 3; a++) { float d = Mathf.Abs(axes[a][w]); if (d > bd) { bd = d; best = a; } }
                    ls[best] *= target[w] / b.size[w];
                }
                go.transform.localScale = ls;
            }
            else
            {
                float k = target.y / Mathf.Max(1e-4f, b.size.y);
                go.transform.localScale *= k;
            }
            b = WorldBounds(go);
            var wantCenter = new Vector3((mn[0] + mx[0]) * 0.5f, 0f, (mn[1] + mx[1]) * 0.5f);
            go.transform.position += new Vector3(wantCenter.x - b.center.x, mn[2] - b.min.y, wantCenter.z - b.center.z);
            return go;
        }

        /// <summary>Engel kökü: şerit ortasında, ön kenarda, tabanı yerde/tavanda; görsel çocuğu dünya konumunu korur.</summary>
        private static Obstacle DesignedObstacle(GameObject visual, Transform parent, DItem it, out float len)
        {
            float x = it.lane * 2.2f, baseY = it.min[2], front = it.min[1];
            len = it.max[1] - it.min[1];
            var root = new GameObject($"Obs_{it.kind}_{it.m}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(x, baseY, front);
            visual.transform.SetParent(root.transform, true);
            visual.name = "Visual";
            return root.AddComponent<Obstacle>();
        }

        private static Material[] hintMats;

        private static void DesignedHint(Obstacle o, ObstacleHint.Action a)
        {
            hintMats ??= new[] { "Hint_Jump", "Hint_Slide", "Hint_Block", "Hint_Climb", "Hint_Moving" }
                .Select(n => AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/UI/Hints/{n}.mat")).ToArray();
            Material m = a switch
            {
                ObstacleHint.Action.Jump => hintMats[0], ObstacleHint.Action.Slide => hintMats[1], ObstacleHint.Action.Block => hintMats[2],
                ObstacleHint.Action.Climb => hintMats[3], _ => null,
            };
            if (m != null) ObstacleHint.ApplyAction(o, a, m, 2.2f, 0.6f);
        }

        /// <summary>Rampasız araç: ön yüz (tampon/kaput/cam) tırmanılmaz — tavan başlayana kadar tavan yüksekliğinde dik blok
        /// (Blender simülasyonuyla aynı; aksi hâlde kaput rampa gibi tırmanılırdı).</summary>
        private static Obstacle.Segment[] BlockVehicleFront(Obstacle.Segment[] segs)
        {
            if (segs == null || segs.Length == 0) return segs;
            float top = segs.Max(s => Mathf.Max(s.h0, s.h1));
            int i0 = System.Array.FindIndex(segs, s => Mathf.Max(s.h0, s.h1) >= 0.9f * top);
            if (i0 <= 0) return segs;
            float h = Mathf.Max(segs[i0].h0, segs[i0].h1);
            var res = new List<Obstacle.Segment> { new Obstacle.Segment { z0 = segs[0].z0, z1 = segs[i0].z0, h0 = h, h1 = h } };
            for (int i = i0; i < segs.Length; i++) res.Add(segs[i]);
            return res.ToArray();
        }

        private static GameObject BuildDesignedMap(DMap d, Material plankMat, Material sea)
        {
            var root = new GameObject(d.name);
            var obsRoot = new GameObject("Engeller").transform;
            obsRoot.SetParent(root.transform, false);
            var decRoot = new GameObject("Dekor").transform;
            decRoot.SetParent(root.transform, false);
            var obstacles = new List<Obstacle>();
            foreach (var it in d.items ?? new DItem[0])
            {
                if (it.kind == "plank")
                {
                    float L = Mathf.Sqrt((it.y1 - it.y0) * (it.y1 - it.y0) + (it.z1 - it.z0) * (it.z1 - it.z0));
                    var pr = new GameObject("Obs_plank_Kalas");
                    pr.transform.SetParent(obsRoot, false);
                    pr.transform.localPosition = new Vector3(it.lane * 2.2f, 0f, it.y0);
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(cube.GetComponent<Collider>());
                    cube.name = "Visual";
                    cube.transform.SetParent(pr.transform, false);
                    cube.transform.localPosition = new Vector3(0f, (it.z0 + it.z1) * 0.5f - 0.05f, (it.y1 - it.y0) * 0.5f);
                    cube.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(it.z1 - it.z0, it.y1 - it.y0) * Mathf.Rad2Deg, 0f, 0f);
                    cube.transform.localScale = new Vector3(1.2f, 0.1f, L);
                    cube.GetComponent<MeshRenderer>().sharedMaterial = plankMat;
                    var po = pr.AddComponent<Obstacle>();
                    po.Configure(Obstacle.Kind.Solid, 0.6f, new[] { new Obstacle.Segment { z0 = 0f, z1 = it.y1 - it.y0, h0 = it.z0, h1 = it.z1 } });
                    obstacles.Add(po);
                    continue;
                }
                var prefab = DesignedModel(it.m, it.src ?? "G");
                if (prefab == null || it.min == null) { Debug.LogWarning($"[Jumpline] {d.name}: model yok {it.m}"); continue; }
                var vis = PlaceDesigned(prefab, obsRoot, it.src ?? "G", it.yaw, it.min, it.max);
                var o = DesignedObstacle(vis, obsRoot, it, out float len);
                float top = it.max[2] - it.min[2];
                switch (it.kind)
                {
                    case "vehicle":
                        o.Configure(Obstacle.Kind.Solid, 1.1f, BlockVehicleFront(AutoSegments(o.gameObject, 1.0f, false))
                                    ?? new[] { Seg(0f, len, top) });
                        DesignedHint(o, top <= 1.75f ? ObstacleHint.Action.Jump : ObstacleHint.Action.Block);
                        break;
                    case "ramp_vehicle":
                        o.Configure(Obstacle.Kind.Solid, 1.0f, AutoSegments(o.gameObject, 1.0f, true) ?? new[] { Seg(0f, len, 0.05f, top) });
                        DesignedHint(o, ObstacleHint.Action.Climb);
                        break;
                    case "vehicle_wall":
                    case "dodge":
                        o.Configure(Obstacle.Kind.Solid, 1.0f, new[] { Seg(0f, len, 4f) });
                        DesignedHint(o, ObstacleHint.Action.Block);
                        break;
                    case "jump":
                        float hw = Mathf.Clamp((it.max[0] - it.min[0]) * 0.5f, 0.45f, 1.0f);
                        o.Configure(Obstacle.Kind.Solid, hw, new[] { Seg(0f, len, top) });
                        o.gameObject.AddComponent<Breakable>();
                        DesignedHint(o, ObstacleHint.Action.Jump);
                        break;
                    case "slide":
                        o.Configure(Obstacle.Kind.Ceiling, 1.0f, new[] { Seg(0f, len, 1.25f) }, 0.38f);
                        DesignedHint(o, ObstacleHint.Action.Slide);
                        break;
                }
                bool big = it.kind.StartsWith("vehicle") || it.kind == "ramp_vehicle";
                foreach (var r in o.GetComponentsInChildren<Renderer>()) if (r.name != "Hint") r.shadowCastingMode = big ? ShadowCastingMode.On : ShadowCastingMode.Off;
                obstacles.Add(o);
            }
            foreach (var dc in d.decor ?? new DDecor[0])
            {
                var prefab = DesignedModel(dc.m, dc.src ?? "D");
                if (prefab == null || dc.min == null) continue;
                var go = PlaceDesigned(prefab, decRoot, dc.src ?? "D", dc.yaw, dc.min, dc.max);
                bool big = BigDecor.Contains(dc.m);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = big ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            // zemin: mahalle/geçiş eski taş yol + kaldırım; sur üstü kaldırımsız taş yol + aşağıda deniz
            foreach (var rg in d.regions ?? new DRegion[0])
            {
                OldStreetGround(root.transform, rg.y0, rg.y1, rg.theme != "sur");
                if (rg.theme != "sur") continue;
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                plane.name = "Deniz";
                plane.transform.SetParent(root.transform, false);
                plane.transform.localPosition = new Vector3(0f, -7.2f, (rg.y0 + rg.y1) * 0.5f);
                plane.transform.localScale = new Vector3(9f, 1f, (rg.y1 - rg.y0 + 4f) / 10f);
                var mr = plane.GetComponent<MeshRenderer>();
                mr.sharedMaterial = sea;
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            var map = root.AddComponent<DesignedMap>();
            var portals = (d.portals ?? new DPortal[0]).Select(p => new DesignedMap.PortalSpot { lane = p.lane, z = p.y, y = p.z }).ToArray();
            map.Configure(d.length, d.theme ?? new string[0], portals, obstacles.ToArray());
            return root;
        }

        /// <summary>Bütün tasarım haritalarının prefab'ları (yoksa boş dizi).</summary>
        private static DesignedMap[] BuildDesignedMapPrefabs()
        {
            if (!Directory.Exists(DesignedDataDir)) return new DesignedMap[0];
            designedModelCache.Clear();
            EnsureFolder(DesignedPrefabDir);
            var mats = MapMaterials();
            var plank = MapMat("Kalas", m => { m.SetColor("_BaseColor", new Color(0.62f, 0.47f, 0.31f)); m.SetFloat("_Smoothness", 0.15f); m.SetFloat("_Metallic", 0f); });
            var list = new List<DesignedMap>();
            foreach (string file in Directory.GetFiles(DesignedDataDir, "Harita_*.json").OrderBy(f => f))
            {
                var d = JsonUtility.FromJson<DMap>(File.ReadAllText(file));
                var root = BuildDesignedMap(d, plank, mats["Deniz"]);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{DesignedPrefabDir}/{d.name}.prefab");
                Object.DestroyImmediate(root);
                list.Add(prefab.GetComponent<DesignedMap>());
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jumpline] Tasarım haritaları: {list.Count} prefab ({list.Count(m => m.IsSeaOnly)} sur üstü)");
            return list.ToArray();
        }

        /// <summary>Sokak: tasarım haritaları zinciri (yol, dekor, engeller haritanın içinde).</summary>
        private static Transform BuildDesignedStreet(Transform runner)
        {
            var maps = BuildDesignedMapPrefabs();
            var root = new GameObject("Street").transform;
            var chain = root.gameObject.AddComponent<DesignedMapChain>();
            Set(chain, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            SetArray(chain, "maps", maps);
            return root;
        }
    }
}
