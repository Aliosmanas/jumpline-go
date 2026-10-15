using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Blender/Harita tasarımı → oyun (15 Eki 2026). Her harita: Assets/Data/Maps/&lt;harita&gt;.json (model yerleşimi) +
    /// Assets/Art/Environment/Maps/&lt;harita&gt;_Ozel.fbx (zemin, deniz, tente, bayrak, çamaşır) → Assets/Prefabs/Maps/&lt;harita&gt;.prefab.
    /// Blender (x sağ, y koşu, z yukarı) → Unity: içerik 180° döndürülmüş alt nesnede, yerel konum (-x, z, -y), yön -θ
    /// (FBX eksen çevrimiyle aynı) → parça +Z'ye koşar, sağ/sol korunur. Ev boyaları: Tools/bake_paints.py dokuları.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string MapsDataDir = "Assets/Data/Maps";
        private const string MapsFbxDir = "Assets/Art/Environment/Maps";
        private const string MapMatDir = MapsFbxDir + "/Materials";
        private const string MapPrefabDir = "Assets/Prefabs/Maps";
        private const float MapSegment = 30f;

        [System.Serializable] private class MapItem { public string a; public float[] p; public float r; public float[] s; public string paint; }
        [System.Serializable] private class MapLight { public float[] pos; public float[] color; public float energy; }
        [System.Serializable] private class MapData { public string name; public string zone; public float length; public MapItem[] items; public MapLight[] lights; }

        private static Material MapMat(string name, System.Action<Material> setup)
        {
            string path = $"{MapMatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit");
            setup(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Texture2D MapTex(string file, bool normal = false)
        {
            string path = $"{MapsFbxDir}/Dokular/{file}";
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = false;
                if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
                if (!normal && file.Contains("rough") && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
                if (ti.maxTextureSize != 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void DoubleSided(Material m)
        {
            m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;
        }

        private static Texture2D StripeTex(string name, Color a, Color b)
        {
            string path = $"{MapMatDir}/{name}.png";
            var t = new Texture2D(2, 1, TextureFormat.RGB24, false);
            t.SetPixel(0, 0, a); t.SetPixel(1, 0, b); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.filterMode = FilterMode.Point; ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = false; ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Özel FBX malzemeleri (Blender adlarıyla): zemin, sur taşı, çatı, deniz, tente, bayrak.</summary>
        private static Dictionary<string, Material> MapMaterials()
        {
            EnsureFolder(MapMatDir);
            var d = new Dictionary<string, Material>();
            void Ground(string name, string tex, Color tint, float smooth)
            {
                d[name] = MapMat(name, m =>
                {
                    m.SetTexture("_BaseMap", MapTex(tex + "_diff.jpg")); m.SetColor("_BaseColor", tint);
                    m.SetTexture("_BumpMap", MapTex(tex + "_nor_gl.jpg", true)); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 0.8f);
                    m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
                });
            }
            Ground("Yol_Bazalt", "cobblestone_floor_08", new Color(0.78f, 0.7f, 0.64f), 0.22f);
            Ground("Kaldirim_Tas", "floor_pattern_01", new Color(1f, 0.92f, 0.8f), 0.2f);
            Ground("Sur_Ustu_Tas", "cobblestone_floor_08", new Color(0.62f, 0.58f, 0.58f), 0.18f);
            Ground("Cati_Kiremit_Gece", "floor_pattern_01", new Color(0.5f, 0.42f, 0.45f), 0.25f);
            d["Deniz"] = MapMat("Deniz", m =>
            {
                m.SetColor("_BaseColor", new Color(0.04f, 0.38f, 0.46f)); m.SetFloat("_Smoothness", 0.93f); m.SetFloat("_Metallic", 0.05f);
            });
            Color[,] awn =
            {
                { new Color(0.85f, 0.08f, 0.08f), new Color(0.95f, 0.93f, 0.88f) }, { new Color(0.05f, 0.5f, 0.25f), new Color(0.95f, 0.93f, 0.88f) },
                { new Color(1f, 0.7f, 0.05f), new Color(0.95f, 0.35f, 0.05f) }, { new Color(0.05f, 0.35f, 0.8f), new Color(0.95f, 0.93f, 0.88f) },
                { new Color(0.9f, 0.3f, 0.55f), new Color(0.98f, 0.85f, 0.3f) }, { new Color(0.1f, 0.6f, 0.65f), new Color(0.98f, 0.9f, 0.75f) },
            };
            for (int i = 0; i < awn.GetLength(0); i++)
            {
                var tex = StripeTex($"Tente_{i}_Serit", awn[i, 0], awn[i, 1]);
                d[$"Tente_{i}"] = MapMat($"Tente_{i}", m => { m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", 0.15f); DoubleSided(m); });
            }
            Color[] flags = { new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.75f, 0.05f), new Color(0.1f, 0.55f, 0.9f), new Color(0.15f, 0.7f, 0.3f),
                              new Color(0.95f, 0.4f, 0.6f), new Color(1f, 0.5f, 0.05f), new Color(0.95f, 0.95f, 0.95f) };
            for (int i = 0; i < flags.Length; i++)
            {
                Color c = flags[i];
                d[$"Bayrak_{i}"] = MapMat($"Bayrak_{i}", m => { m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", 0.1f); DoubleSided(m); });
            }
            AssetDatabase.SaveAssets();
            return d;
        }

        private static void ConfigureMapFbx(string path, Dictionary<string, Material> mats)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return;
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.SaveAndReimport();
        }

        private static readonly Dictionary<(string, string), Material> paintCache = new Dictionary<(string, string), Material>();

        private static Material PaintMaterial(string model, string paint, Material baseMat)
        {
            if (paintCache.TryGetValue((model, paint), out var cached) && cached != null) return cached;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{MeshyDir}/{model}_Tex/{model}_Boya_{paint}.png");
            if (tex == null || baseMat == null) return null;
            string path = $"{MapMatDir}/{model}_{paint}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(baseMat);
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            paintCache[(model, paint)] = m;
            return m;
        }

        /// <summary>Tüm harita prefab'lerini üretir; bölgeye göre gruplu döner.</summary>
        private static Dictionary<string, List<GameObject>> BuildMapPrefabs()
        {
            EnsureFolder(MapPrefabDir);
            var mats = MapMaterials();
            var byZone = new Dictionary<string, List<GameObject>>();
            foreach (string file in Directory.GetFiles(MapsDataDir, "Harita_*.json"))
            {
                var data = JsonUtility.FromJson<MapData>(File.ReadAllText(file));
                var root = new GameObject(data.name);
                var content = new GameObject("Icerik").transform;
                content.SetParent(root.transform, false);
                content.localRotation = Quaternion.Euler(0f, 180f, 0f);
                string fbx = $"{MapsFbxDir}/{data.name}_Ozel.fbx";
                ConfigureMapFbx(fbx, mats);
                var custom = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                if (custom != null)
                {
                    var c = (GameObject)PrefabUtility.InstantiatePrefab(custom, content);
                    c.transform.localPosition = Vector3.zero;
                    foreach (var r in c.GetComponentsInChildren<Renderer>())
                    {
                        bool deco = r.name.StartsWith("Tente") || r.name.StartsWith("Bayrak") || r.name.StartsWith("Camasir");
                        r.shadowCastingMode = deco ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    }
                }
                foreach (var it in data.items)
                {
                    var pf = MeshyPrefab(it.a);
                    if (pf == null) continue;
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, content);
                    go.transform.localPosition = new Vector3(-it.p[0], it.p[2], -it.p[1]);
                    go.transform.localRotation = Quaternion.Euler(0f, -it.r, 0f) * pf.transform.localRotation;
                    go.transform.localScale = Vector3.Scale(pf.transform.localScale, new Vector3(it.s[0], it.s[1], it.s[2]));
                    if (!string.IsNullOrEmpty(it.paint))
                        foreach (var r in go.GetComponentsInChildren<Renderer>())
                        {
                            var pm = PaintMaterial(it.a, it.paint, r.sharedMaterial);
                            if (pm != null) r.sharedMaterial = pm;
                        }
                    // büyük yapılar gölge düşürür; küçük süsler düşürmez (mobil)
                    bool big = it.a.StartsWith("M03") || it.a.StartsWith("M07") || it.a.StartsWith("M08") || it.a.StartsWith("M09") || it.a.StartsWith("M04");
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = big ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
                if (data.lights != null)
                    foreach (var l in data.lights)
                    {
                        var lg = new GameObject("Fener_Isigi").AddComponent<Light>();
                        lg.transform.SetParent(content, false);
                        lg.transform.localPosition = new Vector3(-l.pos[0], l.pos[2], -l.pos[1]);
                        lg.type = LightType.Point; lg.color = new Color(l.color[0], l.color[1], l.color[2]);
                        lg.range = 7f; lg.intensity = 2.2f; lg.shadows = LightShadows.None;
                    }
                string path = $"{MapPrefabDir}/{data.name}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                if (!byZone.TryGetValue(data.zone ?? "", out var list)) byZone[data.zone ?? ""] = list = new List<GameObject>();
                list.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            return byZone;
        }

        /// <summary>Sokak: Blender haritalarından rastgele zincir (eski Blender sokağı ve dekorlarının yerine).</summary>
        private static Transform BuildMapStreet(Transform runner)
        {
            var zones = BuildMapPrefabs();
            var root = new GameObject("Street").transform;
            var chain = root.gameObject.AddComponent<Jumpline.Level.MapChain>();
            Set(chain, "player", runner);
            List<GameObject> Z(string z) => zones.TryGetValue(z, out var l) ? l : new List<GameObject>();
            SetArray(chain, "neighborhood", Z("1_MAHALLE").ToArray());
            SetArray(chain, "seaWalls", Z("3_SUR_USTU_DENIZ").ToArray());
            foreach (var g in Z("2_GECIS"))
            {
                if (g.name.Contains("Mahalle_Sur")) Set(chain, "toSeaWalls", g);
                else Set(chain, "toNeighborhood", g);
            }
            SetFloat(chain, "segmentLength", MapSegment);
            Debug.Log($"[Jumpline] Harita zinciri: {Z("1_MAHALLE").Count} mahalle, {Z("3_SUR_USTU_DENIZ").Count} sur üstü, {Z("2_GECIS").Count} geçiş");
            return root;
        }
    }
}
