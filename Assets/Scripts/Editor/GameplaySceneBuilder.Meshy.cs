using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Jumpline.Level;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Meshy modelleriyle sokak (15 Eki 2026, kullanıcı kararı: eski Blender evleri/kemerleri yerine Meshy binaları).
    /// Sokak modülünün zemini ve ışık zinciri kalır; iki yana köşe binası + sur duvarı dizilir, arkaya kubbeler,
    /// kaldırıma köprü fenerleri ve tezgâh dekoru. Koşu şeritleri (|x| &lt; 3.6) boş kalır; çarpışma etkilenmez.
    /// Köprü finali: nehre kayıklar, kıyıya köprü kemerleri.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string MeshyDir = "Assets/Art/Environment/Meshy";
        private const string MeshyMatDir = MeshyDir + "/Materials";
        private const float StreetWallX = 5.0f;     // build_street.py: ev cephesi (kaldırım arkası)
        private const float SidewalkPropX = 4.25f;  // kaldırım üstü (dekorla aynı hat)
        private static readonly Dictionary<string, GameObject> meshyPrefabs = new Dictionary<string, GameObject>();
        // eski dekor adı → Meshy modeli (dekor ve alçak engel şablonları da bunu kullanır)
        private static readonly Dictionary<string, string> MeshyDecor = new Dictionary<string, string>
        {
            { "Watermelon", "Watermelon" }, { "CopperJugs", "ClayJug" },
        };

        private static GameObject MeshyPrefab(string name)
        {
            if (meshyPrefabs.TryGetValue(name, out var p) && p != null) return p;
            string path = $"{MeshyDir}/{name}.fbx";
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return null;
            EnsureFolder(MeshyMatDir);
            MeshyImport.Prepare(path);
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MeshyMatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
            }
            imp.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is not Material mat) continue;
                string dst = $"{MeshyMatDir}/{mat.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(mat, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            MeshyImport.Tune(MeshyMatDir);
            p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            meshyPrefabs[name] = p;
            return p;
        }

        private static Bounds LocalBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Meshy modelini koyar: yüksekliği <paramref name="height"/> olacak şekilde ölçekler, tabanı y'ye oturtur.</summary>
        private static GameObject PlaceMeshy(string name, Transform parent, Vector3 local, float yaw, float height)
        {
            var prefab = MeshyPrefab(name);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.localRotation;
            go.transform.localPosition = Vector3.zero;
            Bounds b = LocalBounds(go);
            float s = height / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale *= s;
            b = LocalBounds(go);
            Vector3 offset = parent.TransformPoint(local) - new Vector3(b.center.x, b.min.y, b.center.z);
            go.transform.position += offset;
            return go;
        }

        /// <summary>Sokak modülünü Meshy binalarıyla giydirir (eski evler/kemerler kapanır).</summary>
        private static void DressStreetWithMeshy(Transform seg)
        {
            foreach (string old in new[] { "Street_Houses", "Street_Arches" })
            {
                Transform t = FindPrefix(seg, old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var root = new GameObject("MeshyStreet").transform;
            root.SetParent(seg, false);
            // cepheler: köşe binası / sur duvarı sırası, her iki yanda (karşı taraf yarım adım kaydırılmış)
            (string name, float h)[] row = { ("M03_CornerBuilding", 8.5f), ("M07_CityWall", 7.2f), ("M03_CornerBuilding", 9.5f), ("M07_CityWall", 7.2f) };
            foreach (int s in new[] { -1, 1 })
            {
                float z = s > 0 ? -4f : 0f;
                int k = s > 0 ? 1 : 0;
                while (z < StreetSegment)
                {
                    var (name, h) = row[k++ % row.Length];
                    var go = PlaceMeshy(name, root, Vector3.zero, 0f, h);
                    if (go == null) break;
                    Bounds b = LocalBounds(go);
                    if (b.size.x > b.size.z) { go.transform.Rotate(0f, 90f, 0f, Space.World); b = LocalBounds(go); }   // uzun kenar sokak boyunca
                    if (s > 0) go.transform.Rotate(0f, 180f, 0f, Space.World);                                         // ön yüz sokağa
                    b = LocalBounds(go);
                    float w = b.size.z, d = b.size.x;
                    Vector3 target = seg.TransformPoint(new Vector3(s * (StreetWallX + d * 0.5f), 0f, z + w * 0.5f));
                    go.transform.position += new Vector3(target.x - b.center.x, target.y - b.min.y, target.z - b.center.z);
                    z += w - 0.4f;   // hafif bindirme: aralarda boşluk görünmesin
                }
            }
            // arka plan kubbeleri
            PlaceMeshy("M11_Dome", root, new Vector3(-15f, 0f, 14f), 0f, 9f);
            PlaceMeshy("M11_Dome", root, new Vector3(16f, 0f, 44f), 30f, 10f);
            // kaldırım: fenerler ve tezgâh dekoru
            for (float z = 5f; z < StreetSegment; z += 15f)
            {
                PlaceMeshy("M13_BridgeLantern", root, new Vector3(-SidewalkPropX, 0f, z), 90f, 1.9f);
                PlaceMeshy("M13_BridgeLantern", root, new Vector3(SidewalkPropX, 0f, z + 7.5f), -90f, 1.9f);
            }
            PlaceMeshy("M06_StallDecor", root, new Vector3(-SidewalkPropX + 0.2f, 0f, 19f), 90f, 1.0f);
            PlaceMeshy("M06_StallDecor", root, new Vector3(SidewalkPropX - 0.2f, 0f, 37f), -90f, 1.0f);
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
        }

        private static Transform FindPrefix(Transform t, string name)
        {
            if (t.name.StartsWith(name)) return t;
            foreach (Transform c in t)
            {
                var f = FindPrefix(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>Eski dekor parçasının yerine Meshy modeli (aynı yükseklik); yoksa null.</summary>
        private static GameObject MeshyDecorFor(string decorName, Transform parent, Transform src)
        {
            if (!MeshyDecor.TryGetValue(decorName, out string meshy)) return null;
            float h = 0.6f;
            if (src != null)
            {
                var tmp = Object.Instantiate(src.gameObject);
                h = Mathf.Max(0.2f, LocalBounds(tmp).size.y);
                Object.DestroyImmediate(tmp);
            }
            var go = PlaceMeshy(meshy, parent, Vector3.zero, 0f, h);
            if (go != null) go.name = decorName;
            return go;
        }

        /// <summary>Köprü finali parçası (40 m, döngüde öne taşınır): nehirde kayık, kıyıda komşu köprü gözü.</summary>
        private static void DressBridgeSegment(Transform seg, int index)
        {
            float water = 0f;
            foreach (var r in seg.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.name.StartsWith("BR_Water")) water = r.bounds.max.y - seg.position.y;
            var root = new GameObject("MeshyBridge").transform;
            root.SetParent(seg, false);
            bool left = index % 2 == 0;
            var boat = PlaceMeshy("M15_Boat", root, new Vector3(left ? -16f : 17f, water - 0.25f, 8f + index * 7f), index * 47f, 1.1f);
            if (boat != null) foreach (var r in boat.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            PlaceMeshy("M12_BridgeArch", root, new Vector3(left ? 36f : -36f, water - 1f, 20f), 90f, 12f);
        }

        /// <summary>Meshy modelinden engel şablonu: yükseklik (0 = doğal), yön koşucuya (180°), taban/yükseklik sınırlardan.</summary>
        private static Obstacle MeshyObstacle(string name, Transform parent, float height, float minHeight)
        {
            var root = new GameObject("Obs_" + name);
            root.transform.SetParent(parent, false);
            var pf = MeshyPrefab(name);
            if (pf == null) { Object.DestroyImmediate(root); return null; }
            float h = height;
            if (h <= 0f) { var tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf); h = LocalBounds(tmp).size.y; Object.DestroyImmediate(tmp); }
            var vis = PlaceMeshy(name, root.transform, Vector3.zero, 180f, h);
            vis.name = name;
            Bounds b = LocalBounds(vis);
            Vector3 c = b.center - root.transform.position;
            float halfW = Mathf.Clamp(b.extents.x, 0.45f, 1.0f);
            float halfD = Mathf.Max(b.extents.z, 0.35f);
            float top = Mathf.Max(b.max.y - root.transform.position.y, minHeight);
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Solid, halfW, new[] { new Obstacle.Segment { z0 = c.z - halfD, z1 = c.z + halfD, h0 = top, h1 = top } });
            if (minHeight <= 0f) root.AddComponent<Breakable>();
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(obs);
            return obs;
        }

        /// <summary>Sur kapısı yerine Meshy çarşı kemeri: üç şeridi aşar (9.5 m), tavan 3.7 m (kamyon üstündeki koşucu çarpar).</summary>
        private static Obstacle MeshyGate(Transform parent)
        {
            var root = new GameObject("Obs_BazaarArchGate");
            root.transform.SetParent(parent, false);
            var vis = PlaceMeshy("M04_BazaarArch", root.transform, Vector3.zero, 180f, 1f);
            if (vis == null) { Object.DestroyImmediate(root); return null; }
            Bounds b = LocalBounds(vis);
            float k = 9.5f / Mathf.Max(0.1f, b.size.x);
            vis.transform.localScale *= k;
            b = LocalBounds(vis);
            vis.transform.position += new Vector3(root.transform.position.x - b.center.x, root.transform.position.y - b.min.y, root.transform.position.z - b.center.z);
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Ceiling, 3.65f, new[] { new Obstacle.Segment { z0 = -0.8f, z1 = 0.8f, h0 = 3.7f, h1 = 3.7f } });
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
            EditorUtility.SetDirty(obs);
            return obs;
        }
    }
}
