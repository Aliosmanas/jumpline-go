using System.Linq;
using Jumpline.Core;
using Jumpline.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;
using SafeArea = Jumpline.UI.SafeArea;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Geçici oyun sahnesi: zemin + kamera + HUD (bölüm, duraklat) + duraklatma / bölüm sonu / oyun bitti
    /// panelleri. Oyuncu ve engeller gelene kadar akış TEST butonlarıyla denenir.
    /// Menü: Jumpline → Build Gameplay Scene.
    /// </summary>
    public static class GameplaySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Levels/Gameplay.unity";
        private const string GroundMaterialPath = "Assets/Art/Materials/Ground.mat";

        [MenuItem("Jumpline/Build Gameplay Scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes/Levels");
            EnsureFolder("Assets/Art/Materials");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            LoadAssets();
            var config = GetOrCreateAsset<GameConfig>(MainMenuBuilder.ConfigPath, null);

            Transform street = BuildWorld();
            Transform runner = BuildRunner();
            Set(street.gameObject.AddComponent<StreetRecycler>(), "player", runner);
            Transform rooftop = BuildRooftopWorld(runner);
            BuildCameraAndEvents(false);
            var cam = Camera.main;
            // Üçüncü şahıs: karakterin arkasında, ekranın alt-ortasında (brief)
            // alçak ve yakın takip + geniş açı: zemin hızla akar (koşu hissi), 3 şerit yine ekranda
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.3f, -6.2f), Quaternion.Euler(16f, 0f, 0f));
            cam.fieldOfView = 70f;
            cam.farClipPlane = 260f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = Hex("#E6CFAE");
            RenderSettings.skybox = SunsetSky();
            var follow = cam.gameObject.AddComponent<Jumpline.CameraSystem.CameraFollow>();
            Set(follow, "target", runner);
            var fso = new SerializedObject(follow);
            fso.FindProperty("offset").vector3Value = new Vector3(0f, 3.3f, -6.2f);
            fso.ApplyModifiedPropertiesWithoutUndo();
            BuildDust(cam);
            BuildPostFX(cam);
            BuildSpeedLines(cam, runner);

            Transform canvas = BuildCanvas("GameplayCanvas", false);
            var safe = NewUI("SafeArea", canvas);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            BuildUI(safe, canvas.gameObject.AddComponent<LevelFlowController>(), config);
            Image flash = BuildFlash(canvas);
            var switcher = BuildWorldSwitcher(runner, follow, cam, flash, rooftop);
            Set(switcher, "streetRoot", street);
            Portal up = BuildPortal(runner, switcher, true, "PortalUp", null);                 // sokak → çatı (mavi)
            Portal down = BuildPortal(runner, switcher, false, "PortalDown", Hex("#FF3C8E"));  // çatı → sokak (pembe)
            var (streetObs, roofObs) = BuildObstacles(runner, switcher, new[] { up, down });
            SetArray(switcher, "streetExtras", new Object[] { streetObs });
            SetArray(switcher, "rooftopExtras", new Object[] { roofObs });
            // ölünce oyun bitti paneli (LevelFlowController.FailLevel)
            Set(canvas.GetComponent<LevelFlowController>(), "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            if (speedLabel != null)
            {
                var banner = speedLabel.gameObject.AddComponent<Jumpline.UI.SpeedBanner>();
                Set(banner, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
                Set(banner, "label", speedLabel);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            ScenesSetup.ApplyBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jumpline] Oyun sahnesi üretildi: {ScenePath}");
        }

        // Diyarbakır sokağı — Blender/scripts/build_street.py (Desktop/Yeni klasör/genel map duruşu.mp4)
        public const string StreetPath = "Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbx";
        private const string StreetMatDir = "Assets/Art/Environment/Diyarbakir/Materials";
        private const float StreetSegment = 60f;   // build_street.py SEGMENT
        private const int StreetCopies = 3;

        private static Transform BuildWorld()
        {
            // Güneş: alçak açıdan, sokağa çapraz gölge (referanstaki gibi)
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(26f, -35f, 0f);   // altın saat: uzun gölgeler
            light.color = Hex("#FFD9A6");
            light.intensity = 2.0f;
            light.shadows = LightShadows.Soft;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#B9C6D8");
            RenderSettings.ambientEquatorColor = Hex("#8A8F99");
            RenderSettings.ambientGroundColor = Hex("#3A3A40");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#C3CCD8");
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 150f;

            ConfigureStreetImport();
            decorPrefab = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StreetPath);
            var root = new GameObject("Street").transform;
            for (int i = -1; i < StreetCopies; i++) // bir modül karakterin arkasında (kamera boşluk görmesin)
            {
                var seg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                seg.name = $"StreetSegment_{i}";
                seg.transform.SetPositionAndRotation(new Vector3(0f, 0f, i * StreetSegment), Quaternion.identity);
                PlaceDecor(seg.transform, DecorLayout); // statik değil: StreetRecycler modülleri öne taşır
                PlaceDecor(seg.transform, MirroredDecor());
                PlaceSigns(seg.transform);
                if (i == 0) PlaceLandmark(seg.transform, "Landmark_DortAyakliMinare", new Vector3(14f, 0f, 28f));
                if (i == 2) PlaceLandmark(seg.transform, "Landmark_UluCamiMinaret", new Vector3(-16f, 0f, 40f));
            }
            return root;
        }

        // Dekorlar — Blender/scripts/build_decor.py (Desktop/Yeni klasör dekor videoları)
        public const string DecorPath = "Assets/Art/Environment/Diyarbakir/DiyarbakirDecor.fbx";
        private const string DecorMatDir = "Assets/Art/Environment/Diyarbakir/DecorMaterials";
        private static GameObject decorPrefab;

        /// <summary>Segment içi yerleşim (yerel x, y, z, Y açısı). Hepsi duvar dibinde / kaldırımda; koşu şeridi boş.</summary>
        private static readonly (string name, float x, float y, float z, float rot)[] DecorLayout =
        {
            ("Samovar", -4.45f, 0f, 6f, 90f), ("Crates", 4.2f, 0f, 9f, -90f), ("WallLantern", -4.9f, 2.6f, 4f, 90f),
            ("WallLantern", 4.9f, 2.6f, 13f, -90f), ("KilimPile", -4.3f, 0f, 14f, 90f), ("FruitBasket", 4.45f, 0f, 16.5f, -90f),
            ("IronGate", 4.95f, 0f, 21f, -90f), ("Sack", -4.45f, 0f, 22f, 90f), ("Watermelon", 4.35f, 0f, 25f, -90f),
            ("Nazar", -4.92f, 2.0f, 26f, 90f), ("CopperJugs", -4.3f, 0f, 30f, 90f), ("BreadBasket", 4.45f, 0f, 33f, -90f),
            ("CarvedPillar", -4.6f, 0f, 35.5f, 90f), ("Chest", 4.35f, 0f, 38f, -90f), ("RolledCarpet", -4.5f, 0f, 41f, 90f),
            ("WallLantern", -4.9f, 2.6f, 44f, 90f), ("Ladder", 4.7f, 0f, 47f, -90f), ("CopperPlates", -4.45f, 0f, 50f, 90f),
            ("Birdhouse", 4.45f, 0f, 52.5f, -90f), ("KebabCart", -4.1f, 0f, 56f, 90f), ("WallLantern", 4.9f, 2.6f, 58f, -90f),
        };

        /// <summary>Dekor yoğunluğu ×2: aynı düzen x'te aynalı ve z'de yarım modül kaydırılmış.</summary>
        private static (string name, float x, float y, float z, float rot)[] MirroredDecor()
        {
            var list = new (string, float, float, float, float)[DecorLayout.Length];
            for (int i = 0; i < DecorLayout.Length; i++)
            {
                var d = DecorLayout[i];
                list[i] = (d.name, -d.x, d.y, Mathf.Repeat(d.z + StreetSegment * 0.5f + 1.5f, StreetSegment), -d.rot);
            }
            return list;
        }

        private static void PlaceDecor(Transform segment, (string name, float x, float y, float z, float rot)[] layout)
        {
            if (decorPrefab == null)
            {
                ConfigureDecorImport();
                decorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecorPath);
                if (decorPrefab == null) return;
            }
            var root = new GameObject("Decor").transform;
            root.SetParent(segment, false);
            foreach (var d in layout)
            {
                Transform src = decorPrefab.transform.Find("Decor_" + d.name);
                if (src == null) continue;
                var go = Object.Instantiate(src.gameObject, root);
                go.name = d.name;
                go.transform.localPosition = new Vector3(d.x, d.y, d.z);
                go.transform.localRotation = Quaternion.Euler(0f, d.rot, 0f) * src.localRotation;
                go.transform.localScale = src.localScale;
            }
        }

        private static void ConfigureDecorImport()
        {
            EnsureFolder(DecorMatDir);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(DecorPath))
            {
                if (o is not Material m) continue;
                string dst = $"{DecorMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(DecorPath);
            AssetDatabase.ImportAsset(DecorPath, ImportAssetOptions.ForceUpdate);
            string texDir = "Assets/Art/Environment/Diyarbakir/DiyarbakirDecor.fbm";
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { DecorMatDir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetColor("_BaseColor", c);
                if (mat.name.StartsWith("D_Wood") || mat.name == "D_Stone")
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{(mat.name == "D_Stone" ? "basalt" : "wood")}.png");
                    if (tex != null) { mat.SetTexture("_BaseMap", tex); mat.SetColor("_BaseColor", mat.name == "D_WoodDark" ? Hex("#6A5446") : Color.white); }
                }
                bool glow = mat.name.StartsWith("D_Glow");
                if (glow)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", c * 4f);
                }
                mat.SetFloat("_Metallic", mat.name == "D_Copper" || mat.name == "D_Iron" ? 0.85f : 0f);
                mat.SetFloat("_Smoothness", mat.name == "D_Copper" ? 0.65f : 0.25f);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Malzemeleri dışarı çıkarır, dokuları adlarına göre bağlar (FBX dokuları otomatik bağlanmıyor).</summary>
        private static void ConfigureStreetImport()
        {
            EnsureFolder(StreetMatDir);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(StreetPath))
            {
                if (o is not Material m) continue;
                string dst = $"{StreetMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(StreetPath);
            AssetDatabase.ImportAsset(StreetPath, ImportAssetOptions.ForceUpdate);

            string texDir = System.IO.Path.GetDirectoryName(StreetPath).Replace('\\', '/') + "/DiyarbakirStreet.fbm";
            (string mat, string tex, string tint, float smooth)[] map =
            {
                ("S_Basalt", "basalt", "#FFFFFF", 0.12f), ("S_BasaltTrim", "basalt", "#BDB8AF", 0.15f),
                ("S_Paving", "paving", "#FFFFFF", 0.2f), ("S_Curb", "curb", "#FFFFFF", 0.15f),
                ("S_Wood", "wood", "#FFFFFF", 0.3f), ("S_WoodDark", "wood", "#7A6A60", 0.25f),
                ("S_Glass", null, "#0E1218", 0.85f), ("S_Iron", null, "#1A1A1C", 0.5f),
                ("S_Lime", null, "#D9CDB2", 0.12f), ("S_Bulb", null, "#FFC46B", 0.4f),
                ("S_AwnRed", null, "#B3262E", 0.2f), ("S_AwnGreen", null, "#1F6B4A", 0.2f), ("S_AwnWhite", null, "#EFE7D6", 0.2f),
                ("S_Board", null, "#20352C", 0.4f), ("S_KilimA", null, "#9B2335", 0.1f), ("S_KilimB", null, "#D9A441", 0.1f),
                ("S_KilimC", null, "#2C4A7A", 0.1f),
            };
            foreach (var e in map)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{StreetMatDir}/{e.mat}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                if (e.tex != null)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{texDir}/{e.tex}.png");
                    mat.SetTexture("_BaseMap", tex);
                }
                mat.SetColor("_BaseColor", Hex(e.tint));
                mat.SetFloat("_Smoothness", e.smooth);
                mat.SetFloat("_Metallic", e.mat == "S_Iron" ? 0.8f : 0f);
                if (e.mat == "S_Bulb")
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", Hex(e.tint) * 3.5f);
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------------
        // Çatı haritası (gece) + zipline — Blender/scripts/build_rooftops.py (Desktop/Yeni klasör/üst map.mp4,
        // karakter zipline animasyonu.mp4). Sokaktan uzakta (x=400) ve yukarıda; portal ile ışınlanılır.
        // ---------------------------------------------------------------------
        public const string RooftopPath = "Assets/Art/Environment/Rooftop/RooftopModule.fbx";
        public const string PortalPath = "Assets/Art/Environment/Rooftop/Portal.fbx";
        private const string RooftopDir = "Assets/Art/Environment/Rooftop";
        private const string RoofMatDir = "Assets/Art/Environment/Rooftop/Materials";
        private const string FxMatPath = "Assets/Art/Materials/FX_Additive.mat";
        private const string PostFxPath = "Assets/Settings/GameplayPostFX.asset";
        private static readonly Vector3 RooftopOrigin = new Vector3(400f, 14f, 0f);
        private const float RooftopModule = 80f;   // build_rooftops.py MOD

        private static readonly (string name, float x, float y, float z, float rot)[] RoofDecorLayout =
        {   // build_rooftops.py ROOF_DECOR ile aynı
            ("Crates", 4.0f, 0f, 5.0f, -90f), ("Sack", -4.1f, 0f, 4.0f, 90f), ("CopperJugs", -4.0f, 0f, 13.0f, 90f),
            ("KilimPile", 4.0f, 0f, 20.0f, -90f), ("Birdhouse", 4.15f, 0.95f, 26.0f, -90f), ("RolledCarpet", -4.1f, 0f, 19.0f, 90f),
            ("WallLantern", -4.95f, 1.6f, 33.0f, 90f), ("Chest", 4.0f, 0f, 34.0f, -90f), ("Nazar", 5.0f, 5.2f, 40.0f, -90f),
            ("CopperPlates", -4.0f, 0f, 39.5f, 90f), ("Watermelon", 4.0f, 0f, 69.0f, -90f), ("FruitBasket", -4.0f, 0f, 64.5f, 90f),
            ("Samovar", 4.0f, 0f, 76.0f, -90f), ("WallLantern", 4.95f, 1.6f, 73.0f, -90f), ("BreadBasket", -4.05f, 0f, 78.0f, 90f),
        };

        private static Transform BuildRooftopWorld(Transform runner)
        {
            ConfigureRooftopImport();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RooftopPath);
            var root = new GameObject("Rooftop").transform;
            root.position = RooftopOrigin;
            if (prefab == null) { Debug.LogError("[Jumpline] RooftopModule.fbx yok — Blender/scripts/build_rooftops.py çalıştır"); return root; }
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();
            for (int i = -1; i < 3; i++)
            {
                var mod = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                mod.name = $"RooftopModule_{i}";
                mod.transform.localPosition = new Vector3(0f, 0f, i * RooftopModule);
                mod.transform.localRotation = Quaternion.identity;
                Set(mod.AddComponent<Zipline>(), "player", player);
                var hole = mod.AddComponent<Obstacle>();   // build_rooftops.py: çatı z 45–62 arası boş, ara sokak 14 m aşağıda
                hole.Configure(Obstacle.Kind.Hole, 6f, new[] { new Obstacle.Segment { z0 = 45f, z1 = 62f, h0 = -14f, h1 = -14f } });
                PlaceDecor(mod.transform, RoofDecorLayout);
                // performans: gece sahnesinde yalnızca rota binası gölge düşürür
                foreach (var r in mod.GetComponentsInChildren<Renderer>(true))
                    if (r.name != "Roof_Route") r.shadowCastingMode = ShadowCastingMode.Off;
            }
            var recycler = root.gameObject.AddComponent<StreetRecycler>();
            Set(recycler, "player", runner);
            SetFloat(recycler, "segmentLength", RooftopModule);
            return root;
        }

        private static void ConfigureRooftopImport()
        {
            EnsureFolder(RoofMatDir);
            string[] streetMats = { "S_Basalt", "S_BasaltTrim", "S_Paving", "S_Curb", "S_Wood", "S_WoodDark", "S_Glass", "S_Iron" };
            foreach (string fbx in new[] { RooftopPath, PortalPath })
            {
                if (AssetImporter.GetAtPath(fbx) is not ModelImporter imp) continue;
                imp.importCameras = imp.importLights = false;
                imp.importAnimation = false;
                foreach (string n in streetMats)
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>($"{StreetMatDir}/{n}.mat");
                    if (m != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { RoofMatDir }))
                {
                    var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
                }
                imp.SaveAndReimport();
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    if (o is not Material m || !m.name.StartsWith("R_")) continue;
                    string dst = $"{RoofMatDir}/{m.name}.mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
                }
                AssetDatabase.WriteImportSettingsIfDirty(fbx);
                AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);
            }
            var swirl = AssetDatabase.LoadAssetAtPath<Texture2D>($"{RooftopDir}/portal_swirl.png");
            (string name, string color, float glow, float smooth)[] defs =
            {
                ("R_Neon", "#2AA6FF", 3.2f, 0.5f), ("R_NeonRed", "#FF3048", 4f, 0.5f), ("R_Cloth", "#E8E6E0", 0f, 0.1f),
                ("R_Lamp", "#FFB45A", 3f, 0.3f), ("R_PortalCore", "#9FE8FF", 3.5f, 0.2f),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{RoofMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                Color c = Hex(d.color);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", 0f);
                if (d.name == "R_PortalCore" && swirl != null)
                {
                    mat.SetTexture("_BaseMap", swirl);
                    mat.SetTexture("_EmissionMap", swirl);
                    mat.SetColor("_BaseColor", Color.white);
                }
                if (d.glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", (d.name == "R_PortalCore" ? Color.white : c) * d.glow);
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Portal: taş sivri kemer + dönen girdap + parçacık + ışık. Portal.cs yerleştirir (yukarı: sokakta, aşağı: çatıda).</summary>
        private static Portal BuildPortal(Transform runner, WorldSwitcher switcher, bool leadsToRooftop, string name, Color? tint)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PortalPath);
            if (prefab == null) return null;
            var portal = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            portal.name = name;
            portal.transform.SetPositionAndRotation(leadsToRooftop ? new Vector3(0f, 0f, 45f) : RooftopOrigin + new Vector3(0f, 0f, 30f), Quaternion.identity);
            PrefabUtility.UnpackPrefabInstance(portal, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var spinner = new GameObject("Spinner").transform;
            spinner.SetParent(portal.transform, false);
            spinner.localPosition = new Vector3(0f, 1.45f, 0f);
            foreach (string n in new[] { "Portal_Core", "Portal_CoreBack" })
            {
                Transform c = portal.transform.Find(n);
                if (c != null) c.SetParent(spinner, true);
            }
            // girdap parçacıkları
            var ps = MakeSparks(spinner, "PortalSwirl");
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("#55CFFF"), Color.white);
            var em = ps.emission;
            em.rateOverTime = 45f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 0.95f;
            sh.rotation = Vector3.zero;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = vel.y = vel.z = new ParticleSystem.MinMaxCurve(0f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(2.5f);
            vel.radial = new ParticleSystem.MinMaxCurve(-0.4f);
            ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            // ışık
            var light = new GameObject("PortalLight", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(portal.transform, false);
            light.transform.localPosition = new Vector3(0f, 1.5f, -0.8f);
            light.type = LightType.Point;
            light.color = Hex("#55CFFF");
            light.range = 7f;
            light.intensity = 4f;
            var comp = portal.AddComponent<Portal>();
            Set(comp, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            Set(comp, "switcher", switcher);
            Set(comp, "spinner", spinner);
            var so = new SerializedObject(comp);
            so.FindProperty("leadsToRooftop").boolValue = leadsToRooftop;
            // portallar seyrek: sokakta ~160 m sonra ilki, sonra ~420 m'de bir; çatıda dönüş biraz daha geç
            so.FindProperty("firstDistance").floatValue = leadsToRooftop ? 160f : 200f;
            so.FindProperty("spacing").floatValue = 420f;
            if (tint.HasValue) so.FindProperty("tint").colorValue = tint.Value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return comp;
        }

        private static WorldSwitcher BuildWorldSwitcher(Transform runner, Jumpline.CameraSystem.CameraFollow follow, Camera cam,
                                                        Image flash, Transform rooftop)
        {
            var go = new GameObject("WorldSwitcher");
            var sw = go.AddComponent<WorldSwitcher>();
            Set(sw, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            Set(sw, "cameraFollow", follow);
            Set(sw, "cam", cam);
            var sunGo = GameObject.Find("Directional Light");
            if (sunGo != null) Set(sw, "sun", sunGo.GetComponent<Light>());
            Set(sw, "flash", flash);
            Set(sw, "rooftopRoot", rooftop);
            SetFloat(sw, "rooftopModuleLength", RooftopModule);
            // sokak: BuildWorld değerleri (gündüz)
            // sokak: Sur içi ikindi/gün batımı — sıcak güneş, bal rengi pus, prosedürel gökyüzü
            SetLook(sw, "streetLook", "#E6CFAE", "#E3C9A4", 30f, 190f, "#C9D6EA", "#A8968A", "#4A3F38", "#FFD9A6", 2.0f, new Vector3(26f, -35f, 0f), true);
            // çatı: fırtınalı gece (üst map.mp4) — lacivert gök, ay ışığı, koyu mavi pus
            SetLook(sw, "rooftopLook", "#1B2536", "#1E2939", 18f, 130f, "#5A6E92", "#2E3A52", "#12151C", "#A9BEFF", 0.7f, new Vector3(42f, 35f, 0f), false);
            return sw;
        }

        private static void SetLook(Object target, string field, string sky, string fog, float fogStart, float fogEnd,
                                    string ambSky, string ambEq, string ambGround, string sunColor, float sunIntensity, Vector3 sunEuler,
                                    bool skybox)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.FindPropertyRelative("sky").colorValue = Hex(sky);
            p.FindPropertyRelative("fog").colorValue = Hex(fog);
            p.FindPropertyRelative("fogStart").floatValue = fogStart;
            p.FindPropertyRelative("fogEnd").floatValue = fogEnd;
            p.FindPropertyRelative("ambientSky").colorValue = Hex(ambSky);
            p.FindPropertyRelative("ambientEquator").colorValue = Hex(ambEq);
            p.FindPropertyRelative("ambientGround").colorValue = Hex(ambGround);
            p.FindPropertyRelative("sunColor").colorValue = Hex(sunColor);
            p.FindPropertyRelative("sunIntensity").floatValue = sunIntensity;
            p.FindPropertyRelative("sunEuler").vector3Value = sunEuler;
            p.FindPropertyRelative("skybox").boolValue = skybox;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Portal geçişinde ekranı kaplayan mavi-beyaz flaş (tıklamayı engellemez).</summary>
        private static Image BuildFlash(Transform canvas)
        {
            var rt = NewUI("PortalFlash", canvas);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.55f, 0.85f, 1f, 0f);
            img.raycastTarget = false;
            rt.SetAsLastSibling();
            return img;
        }

        // ---------------------------------------------------------------------
        // Diyarbakır kimliği: çarşı tabelaları (street_signs.json, build_street.py), Dört Ayaklı Minare, Ulu Cami minaresi
        // ---------------------------------------------------------------------
        private const string SignsJson = "Assets/Art/Environment/Diyarbakir/street_signs.json";
        public const string LandmarkPath = "Assets/Art/Environment/Diyarbakir/DiyarbakirLandmarks.fbx";

        [System.Serializable] private class SignList { public SignData[] signs; }
        [System.Serializable] private class SignData { public string text; public float x, y, z; public int side; }

        private static void PlaceSigns(Transform segment)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(SignsJson);
            if (json == null) return;
            var list = JsonUtility.FromJson<SignList>(json.text);
            var root = new GameObject("Signs").transform;
            root.SetParent(segment, false);
            foreach (var d in list.signs)
            {
                var go = new GameObject("Sign_" + d.text);
                go.transform.SetParent(root, false);
                // tabela panosunun sokağa bakan yüzü; yazı sokaktan okunur
                float toward = d.x > 0f ? -1f : 1f;
                go.transform.localPosition = new Vector3(d.x + toward * 0.02f, d.y, d.z);
                go.transform.localRotation = Quaternion.Euler(0f, d.x > 0f ? 90f : -90f, 0f);
                var t = go.AddComponent<TextMeshPro>();
                t.font = fExtra != null ? fExtra : fBold;
                t.text = d.text;
                t.alignment = TextAlignmentOptions.Center;
                t.enableAutoSizing = true;
                t.fontSizeMin = 0.5f;
                t.fontSizeMax = 4.5f;
                t.color = Hex("#F2D27A");
                t.rectTransform.sizeDelta = new Vector2(2.1f, 0.48f);
            }
        }

        private static GameObject landmarkPrefab;

        private static void PlaceLandmark(Transform segment, string name, Vector3 local)
        {
            if (landmarkPrefab == null)
            {
                if (AssetImporter.GetAtPath(LandmarkPath) is ModelImporter imp)
                {
                    foreach (string n in new[] { "S_Basalt", "S_BasaltTrim", "S_Glass", "S_Lime" })
                    {
                        var m = AssetDatabase.LoadAssetAtPath<Material>($"{StreetMatDir}/{n}.mat");
                        if (m != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), m);
                    }
                    imp.importCameras = imp.importLights = imp.importAnimation = false;
                    imp.SaveAndReimport();
                }
                landmarkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LandmarkPath);
                if (landmarkPrefab == null) return;
            }
            Transform src = landmarkPrefab.transform.Find(name);
            if (src == null) return;
            var go = Object.Instantiate(src.gameObject, segment);
            go.name = name;
            go.transform.localPosition = local;
            go.transform.localRotation = src.localRotation;
            go.transform.localScale = src.localScale;
        }

        /// <summary>Gün batımı prosedürel gökyüzü (Mezopotamya ikindisi: sıcak ufuk, açık mavi zenit).</summary>
        private static Material SunsetSky()
        {
            const string path = "Assets/Settings/SkyDiyarbakir.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_SunSize", 0.05f);
            mat.SetFloat("_SunSizeConvergence", 4f);
            mat.SetFloat("_AtmosphereThickness", 1.35f);
            mat.SetColor("_SkyTint", Hex("#8FA9D6"));
            mat.SetColor("_GroundColor", Hex("#6B5848"));
            mat.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Güneşte uçuşan toz zerreleri (kameranın etrafında, dünya uzayında) — ambiyans.</summary>
        private static void BuildDust(Camera cam)
        {
            var ps = MakeSparks(cam.transform, "DustMotes");
            ps.transform.localPosition = new Vector3(0f, 0f, 9f);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new Color(1f, 0.86f, 0.62f, 0.45f);
            main.gravityModifier = -0.01f;
            main.maxParticles = 220;
            var em = ps.emission;
            em.rateOverTime = 45f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = new Vector3(12f, 7f, 18f);
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        /// <summary>Neon ışıklar için bloom (URP Volume).</summary>
        private static void BuildPostFX(Camera cam)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            AssetDatabase.DeleteAsset(PostFxPath);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostFxPath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.7f);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var color = profile.Add<ColorAdjustments>(true);   // sıcak, doygun Mezopotamya ikindisi
            color.postExposure.Override(0.05f);
            color.contrast.Override(14f);
            color.saturation.Override(14f);
            color.colorFilter.Override(Hex("#FFF0DC"));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.26f);
            vignette.smoothness.Override(0.45f);
            var blur = profile.Add<MotionBlur>(true);          // kamera hızı → zemin/duvar akışı (koşu hissi)
            blur.intensity.Override(0.22f);
            blur.quality.Override(MotionBlurQuality.Low);
            foreach (VolumeComponent c in new VolumeComponent[] { bloom, tone, color, vignette, blur })
            {
                c.name = c.GetType().Name;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            AssetDatabase.SaveAssets();
            var vol = new GameObject("PostFX").AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = profile;
        }

        /// <summary>Toplamalı parlayan kıvılcım parçacığı (kapalı başlar; PlayerController zipline'da oynatır).</summary>
        private static ParticleSystem MakeSparks(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Hex("#7FDBFF"));
            main.gravityModifier = 1.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 170f;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.06f;
            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.05f;
            rend.lengthScale = 1.2f;
            rend.sharedMaterial = FxMaterial();
            return ps;
        }

        private static Material FxMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(FxMatPath);
            if (mat != null) return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);   // toplamalı
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", new Color(1.6f, 1.9f, 2.2f, 1f));
            AssetDatabase.CreateAsset(mat, FxMatPath);
            return mat;
        }

        // ---------------------------------------------------------------------
        // Engeller — dosyadaki dekorlar (zıpla / şerit değiştir) + rampalı kamyonet (Blender/scripts/build_obstacles.py)
        // ---------------------------------------------------------------------
        public const string TruckPath = "Assets/Art/Environment/Obstacles/RampTruck.fbx";
        private const string TruckMatDir = "Assets/Art/Environment/Obstacles/Materials";
        private static readonly string[] LowObstacles = { "Chest", "CopperJugs", "Sack", "Watermelon", "RolledCarpet", "FruitBasket", "CopperPlates" };
        public const string TaxiPath = "Assets/Art/Environment/Obstacles/Taxi.fbx";
        public const string MinibusPath = "Assets/Art/Environment/Obstacles/Minibus.fbx";
        public const string SurGatePath = "Assets/Art/Environment/Obstacles/SurGate.fbx";
        public const string CrateStackPath = "Assets/Art/Environment/Obstacles/CrateStack.fbx";
        public const string SlideBarPath = "Assets/Art/Environment/Obstacles/SlideBar.fbx";

        /// <summary>build_vehicles.py taksi: kaput → ön cam eğimi → tavan → arka cam → bagaj (ön -z'de).</summary>
        private static readonly Obstacle.Segment[] TaxiSegments =
        {
            new Obstacle.Segment { z0 = -2.25f, z1 = -0.95f, h0 = 0.95f, h1 = 0.95f },
            new Obstacle.Segment { z0 = -0.95f, z1 = -0.42f, h0 = 0.95f, h1 = 1.45f },
            new Obstacle.Segment { z0 = -0.42f, z1 = 0.86f, h0 = 1.45f, h1 = 1.45f },
            new Obstacle.Segment { z0 = 0.86f, z1 = 1.3f, h0 = 1.45f, h1 = 0.95f },
            new Obstacle.Segment { z0 = 1.3f, z1 = 2.25f, h0 = 0.95f, h1 = 0.95f },
        };
        private static readonly string[] HighObstacles = { "KebabCart", "Samovar" };
        private static readonly string[] WallObstacles = { "IronGate", "CarvedPillar", "Fountain" };

        /// <summary>build_obstacles.py yüzey profili: rampa → kasa tavanı → kabin → kaput.</summary>
        private static readonly Obstacle.Segment[] RampTruckSegments =
        {
            new Obstacle.Segment { z0 = 0f, z1 = 4f, h0 = 0.05f, h1 = 2.35f },
            new Obstacle.Segment { z0 = 4f, z1 = 9.5f, h0 = 2.35f, h1 = 2.35f },
            new Obstacle.Segment { z0 = 9.5f, z1 = 10.8f, h0 = 1.9f, h1 = 1.9f },
            new Obstacle.Segment { z0 = 10.8f, z1 = 11.6f, h0 = 1.25f, h1 = 1.25f },
        };

        /// <summary>Şablonlar + sokak/çatı engel üreticileri. Döndürür: (sokak engel kökü, çatı engel kökü).</summary>
        private static (GameObject street, GameObject roof) BuildObstacles(Transform runner, WorldSwitcher switcher, Portal[] portals)
        {
            var templates = new GameObject("ObstacleTemplates");
            templates.transform.position = new Vector3(0f, -50f, 0f);   // sınırlar ölçülürken görünmesin
            Obstacle[] Make(string[] names, float minHeight)
            {
                var list = new System.Collections.Generic.List<Obstacle>();
                foreach (string n in names)
                {
                    var o = DecorObstacle(n, templates.transform, minHeight);
                    if (o != null) list.Add(o);
                }
                return list.ToArray();
            }
            // duvarlar: çarpışma yüksekliği en yüksek zıplamanın üstünde → şerit değiştirmek şart
            Obstacle[] low = Make(LowObstacles, 0f), high = Make(HighObstacles, 4f), walls = Make(WallObstacles, 4f);   // high: yanından geç
            foreach (var o in low) o.gameObject.AddComponent<Breakable>();    // çarpınca savrulur
            foreach (var o in high) o.gameObject.AddComponent<Breakable>();
            Obstacle crates = CrateStackObstacle(templates.transform);
            if (crates != null) low = new[] { crates, crates }.Concat(low).ToArray();   // en sık çıkan: sandık yığını
            Obstacle truck = TruckObstacle(templates.transform);
            Obstacle taxi = VehicleObstacle(TaxiPath, "Obs_Taxi", templates.transform, Obstacle.Kind.Solid, 0.86f, TaxiSegments);
            Obstacle bus = VehicleObstacle(MinibusPath, "Obs_Minibus", templates.transform, Obstacle.Kind.Solid, 1.0f,
                new[]
                {   // kaput 1.15 + tavan 2.35: yerden çıkılmaz (zıplama 1.9), kamyon tavanından atlanıp üstünde koşulur
                    new Obstacle.Segment { z0 = -2.95f, z1 = -2.2f, h0 = 1.15f, h1 = 1.15f },
                    new Obstacle.Segment { z0 = -2.2f, z1 = 2.92f, h0 = 2.35f, h1 = 2.35f },
                });
            Obstacle gate = VehicleObstacle(SurGatePath, "Obs_SurGate", templates.transform, Obstacle.Kind.Ceiling, 3.65f,
                new[] { new Obstacle.Segment { z0 = -0.8f, z1 = 0.8f, h0 = 3.7f, h1 = 3.7f } });   // alt kenar 3.7 m
            // kalas bariyer: alt kenar 1.15, kalınlık 0.35 (üstü 1.5) → kayarak altından ya da zıplayarak üstünden
            Obstacle bar = VehicleObstacle(SlideBarPath, "Obs_SlideBar", templates.transform, Obstacle.Kind.Ceiling, 1.0f,
                new[] { new Obstacle.Segment { z0 = -0.3f, z1 = 0.3f, h0 = 1.15f, h1 = 1.15f } }, 0.35f);
            templates.SetActive(false);   // şablonlar kayıt olmaz; üretici kopyalar (sınırlar açıkken ölçüldü)
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();

            GameObject Spawner(string name, bool rooftop)
            {
                var go = new GameObject(name);
                var sp = go.AddComponent<ObstacleSpawner>();
                Set(sp, "player", player);
                Set(sp, "switcher", switcher);
                var so = new SerializedObject(sp);
                so.FindProperty("rooftop").boolValue = rooftop;
                so.ApplyModifiedPropertiesWithoutUndo();
                SetArray(sp, "portals", portals);
                SetArray(sp, "low", low);
                SetArray(sp, "high", high);
                SetArray(sp, "walls", walls);
                Object[] One(Obstacle o) => rooftop || o == null ? new Object[0] : new Object[] { o };
                SetArray(sp, "ramps", One(truck));
                SetArray(sp, "cars", One(taxi));
                SetArray(sp, "buses", One(bus));
                SetArray(sp, "gates", One(gate));
                SetArray(sp, "bars", bar == null ? new Object[0] : new Object[] { bar });   // çatıda da var
                return go;
            }
            return (Spawner("StreetObstacles", false), Spawner("RooftopObstacles", true));
        }

        /// <summary>Dekoru engel şablonu yapar: taban/yükseklik dekorun sınırlarından.</summary>
        private static Obstacle DecorObstacle(string name, Transform parent, float minHeight)
        {
            if (decorPrefab == null) decorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecorPath);
            Transform src = decorPrefab != null ? decorPrefab.transform.Find("Decor_" + name) : null;
            if (src == null) return null;
            var root = new GameObject("Obs_" + name);
            root.transform.SetParent(parent, false);
            var vis = Object.Instantiate(src.gameObject, root.transform);
            vis.name = name;
            vis.transform.localPosition = Vector3.zero;
            vis.transform.localRotation = src.localRotation;
            vis.transform.localScale = src.localScale;
            var rs = vis.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 c = b.center - root.transform.position;
            float halfW = Mathf.Clamp(b.extents.x, 0.45f, 1.0f);   // şeritten taşmasın
            float halfD = Mathf.Max(b.extents.z, 0.35f);          // ince engeller (kapı) atlanmasın
            float h = Mathf.Max(b.max.y - root.transform.position.y, minHeight);
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Solid, halfW, new[] { new Obstacle.Segment { z0 = c.z - halfD, z1 = c.z + halfD, h0 = h, h1 = h } });
            EditorUtility.SetDirty(obs);
            return obs;
        }

        private static Obstacle TruckObstacle(Transform parent)
        {
            ConfigureVehicleImport(TruckPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPath);
            if (prefab == null) { Debug.LogError("[Jumpline] RampTruck.fbx yok — Blender/scripts/build_obstacles.py çalıştır"); return null; }
            var root = new GameObject("Obs_RampTruck");
            root.transform.SetParent(parent, false);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.transform.localPosition = Vector3.zero;   // dönüş FBX'in kendi eksen dönüşü kalır (sıfırlanırsa araç dik durur)
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Solid, 0.95f, RampTruckSegments);
            EditorUtility.SetDirty(obs);
            return obs;
        }

        /// <summary>Tek parçalı araç/kapı FBX'inden engel şablonu (FBX'in eksen dönüşü korunur).</summary>
        private static Obstacle VehicleObstacle(string path, string name, Transform parent, Obstacle.Kind kind, float halfWidth, Obstacle.Segment[] segs,
                                                float thickness = 0f)
        {
            ConfigureVehicleImport(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[Jumpline] {path} yok — Blender/scripts/build_vehicles.py çalıştır"); return null; }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.transform.localPosition = Vector3.zero;
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(kind, halfWidth, segs, thickness);
            EditorUtility.SetDirty(obs);
            return obs;
        }

        /// <summary>Üç ayrı sandık (build_vehicles.py crate_stack): sandıklar doğrudan köke bağlanır → Breakable ayrı ayrı savurur.</summary>
        private static Obstacle CrateStackObstacle(Transform parent)
        {
            ConfigureVehicleImport(CrateStackPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CrateStackPath);
            if (prefab == null) return null;
            var root = new GameObject("Obs_CrateStack");
            root.transform.SetParent(parent, false);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.transform.localPosition = Vector3.zero;
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var crates = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in vis.transform) crates.Add(c);
            foreach (Transform c in crates) c.SetParent(root.transform, true);
            Object.DestroyImmediate(vis);
            root.transform.localScale = Vector3.one * 0.85f;   // yığın ~1.3 m: orta şarjla aşılır
            var rs = root.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 c0 = b.center - root.transform.position;
            float h = b.max.y - root.transform.position.y;
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Solid, Mathf.Clamp(b.extents.x, 0.45f, 1f),
                new[] { new Obstacle.Segment { z0 = c0.z - Mathf.Max(b.extents.z, 0.35f), z1 = c0.z + Mathf.Max(b.extents.z, 0.35f), h0 = h, h1 = h } });
            root.AddComponent<Breakable>();
            EditorUtility.SetDirty(obs);
            return obs;
        }

        /// <summary>Araç FBX malzemeleri: Obstacles/Materials'ta varsa ona bağlanır, yoksa çıkarılır; sonra URP ayarı.</summary>
        private static void ConfigureVehicleImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return;
            EnsureFolder(TruckMatDir);
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { TruckMatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
            }
            imp.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is not Material m) continue;
                string dst = $"{TruckMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbm/wood.png");
            var basalt = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbm/basalt.png");
            (string name, string color, float glow, float smooth, float metal)[] defs =
            {
                ("T_Paint", "#3F7F7A", 0f, 0.45f, 0.2f), ("T_PaintRed", "#8E1748", 0f, 0.45f, 0.2f), ("T_Wood", "#FFFFFF", 0f, 0.25f, 0f),
                ("T_Metal", "#2A2C30", 0f, 0.5f, 0.8f), ("T_Glass", "#0E1620", 0f, 0.9f, 0.3f), ("T_Neon", "#2AA6FF", 3.2f, 0.5f, 0f),
                ("T_Tire", "#141414", 0f, 0.2f, 0f), ("T_Lamp", "#FFE6A0", 3f, 0.5f, 0f), ("T_Rust", "#7A4A32", 0f, 0.3f, 0.4f),
                ("T_Yellow", "#F2B705", 0f, 0.7f, 0.1f), ("T_White", "#E9E6DF", 0f, 0.7f, 0.1f), ("T_Blue", "#1F4E9C", 0f, 0.6f, 0.1f),
                ("T_Black", "#111111", 0f, 0.4f, 0f), ("T_Basalt", "#FFFFFF", 0f, 0.12f, 0f), ("T_Trim", "#BDB8AF", 0f, 0.15f, 0f),
                ("T_NeonRed", "#FF3048", 4f, 0.5f, 0f), ("T_TailLight", "#D01020", 2f, 0.6f, 0f), ("T_Chrome", "#C8CCD0", 0f, 0.85f, 0.9f),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{TruckMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                Color c = Hex(d.color);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", d.metal);
                if (d.name == "T_Wood" && wood != null) mat.SetTexture("_BaseMap", wood);
                if ((d.name == "T_Basalt" || d.name == "T_Trim") && basalt != null) mat.SetTexture("_BaseMap", basalt);
                if (d.glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", c * d.glow);
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Hız çizgileri: kameranın önünde, kenarlardan kameraya doğru akar (SpeedLines yoğunluğu ayarlar).</summary>
        private static void BuildSpeedLines(Camera cam, Transform runner)
        {
            var ps = MakeSparks(cam.transform, "SpeedLines");
            ps.transform.localPosition = new Vector3(0f, 0f, 16f);
            ps.transform.localRotation = Quaternion.identity;
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new Color(1f, 1f, 1f, 0.35f);
            main.gravityModifier = 0f;
            main.maxParticles = 400;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Donut;
            sh.radius = 7.5f;
            sh.donutRadius = 2.2f;
            sh.rotation = Vector3.zero;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(-55f);
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.06f;
            rend.lengthScale = 2f;
            var lines = ps.gameObject.AddComponent<Jumpline.CameraSystem.SpeedLines>();
            SetFloat(lines, "baseRate", 30f);
            SetFloat(lines, "ratePerSpeed", 110f);
            Set(lines, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            ps.Play();
        }

        private static void ConfigureTruckImport()
        {
            if (AssetImporter.GetAtPath(TruckPath) is not ModelImporter imp) return;
            EnsureFolder(TruckMatDir);
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(TruckPath))
            {
                if (o is not Material m) continue;
                string dst = $"{TruckMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(TruckPath);
            AssetDatabase.ImportAsset(TruckPath, ImportAssetOptions.ForceUpdate);
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbm/wood.png");
            (string name, string color, float glow, float smooth, float metal)[] defs =
            {
                ("T_Paint", "#3F7F7A", 0f, 0.45f, 0.2f), ("T_PaintRed", "#8E1748", 0f, 0.45f, 0.2f), ("T_Wood", "#FFFFFF", 0f, 0.25f, 0f),
                ("T_Metal", "#2A2C30", 0f, 0.5f, 0.8f), ("T_Glass", "#0E1620", 0f, 0.9f, 0.3f), ("T_Neon", "#2AA6FF", 3.2f, 0.5f, 0f),
                ("T_Tire", "#141414", 0f, 0.2f, 0f), ("T_Lamp", "#FFE6A0", 3f, 0.5f, 0f), ("T_Rust", "#7A4A32", 0f, 0.3f, 0.4f),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{TruckMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                Color c = Hex(d.color);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", d.metal);
                if (d.name == "T_Wood" && wood != null) mat.SetTexture("_BaseMap", wood);
                if (d.glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", c * d.glow);
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Koşucu: başlangıç noktasında, koşu yönüne (+Z) bakar, giyili kostümle. PlayerController koşturur/zıplatır.</summary>
        private static Transform BuildRunner()
        {
            var library = RunnerStageBuilder.EnsureLibrary();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerStageBuilder.FbxPath));
            model.name = "Runner";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (model.TryGetComponent(out Animator anim))
            {
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RunnerStageBuilder.GameControllerPath);
                anim.applyRootMotion = false;
                anim.enabled = true;   // koşu animasyonu (oynanış kodu tetikleyicilerle zıplama/ölüm oynatacak)
            }

            var skin = model.AddComponent<Jumpline.Player.RunnerSkin>();
            Set(skin, "library", library);
            var display = model.AddComponent<Jumpline.UI.RunnerDisplay>();
            Set(display, "runner", skin);
            Set(display, "library", library);
            SetFloat(display, "swayAngle", 0f);
            var player = model.AddComponent<Jumpline.Player.PlayerController>();
            Set(player, "animator", anim);
            // zipline kıvılcımları: sağ elde (karakter zipline animasyonu.mp4 — halatta beyaz-mavi kıvılcım)
            Transform hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
            var sparks = MakeSparks(hand != null ? hand : model.transform, "ZiplineSparks");
            if (hand == null) sparks.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            Set(player, "ziplineSparks", sparks);
            return model.transform;
        }

        private static TextMeshProUGUI speedLabel;

        private static void BuildUI(Transform safe, LevelFlowController flow, GameConfig config)
        {
            var hud = NewUI("HUD", safe);
            Stretch(hud);
            Box top = Group(hud, "Top", 1f);
            Box mid = Group(hud, "Middle", 0.5f);
            Box bot = Group(hud, "Bottom", 0f);

            Panel(top, "LevelPill", new Rect(308f, 50f, 500f, 92f), Hex("#1B2236", 0.9f), Hex("#2A3556", 0.9f), 46f, Hex("#7083B3"));
            var level = Txt(top, "LevelLabel", "LEVEL 1", new Rect(308f, 50f, 500f, 92f), fExtra, 46f, Color.white, localize: false);
            var mode = Txt(top, "ModeLabel", "", new Rect(308f, 150f, 500f, 44f), fBold, 28f, C.TextGrey, localize: false);
            ProgressBar(top, "Progress", new Rect(258f, 210f, 600f, 24f), 0f);

            var pausePanel = Panel(top, "PauseButton", new Rect(953f, 40f, 123f, 123f), Hex("#4F1B2E"), Hex("#2E3A5E"), 18f, Hex("#7C8DB8"));
            Icon(pausePanel, "player-pause", new Rect(975f, 62f, 79f, 79f), Color.white);
            Button pause = MakeButton(pausePanel, pausePanel.rt.GetComponent<Image>());

            Button win = PillButton(bot, "TestWin", new Rect(118f, 1780f, 420f, 90f), "WIN (TEST)", 34f, Color.white, 1f);
            Button fail = PillButton(bot, "TestFail", new Rect(578f, 1780f, 420f, 90f), "FAIL (TEST)", 34f, Color.white, 1f, Hex("#E2B45A"), Hex("#F2C366"));
            speedLabel = Txt(mid, "SpeedBanner", "", new Rect(60f, 380f, 960f, 140f), fExtra, 76f, Hex("#55CFFF"), localize: false);
            speedLabel.raycastTarget = false;
            var hint = Txt(mid, "ControlsHint", "SWIPE UP: JUMP   ·   DOWN: SLIDE\nLEFT / RIGHT: CHANGE LANE", new Rect(40f, 1180f, 1000f, 160f), fBold, 40f, Color.white);
            hint.raycastTarget = false;
            Set(hint.gameObject.AddComponent<Jumpline.UI.TutorialHint>(), "label", hint);
            win.gameObject.SetActive(false);    // test butonları: oynanış gerçek (ölüm → oyun bitti)
            fail.gameObject.SetActive(false);

            // Paneller
            CanvasGroup pauseGroup = Overlay(safe, "PausePanel", "PAUSED", 640f, out Box pauseBox);
            Button cont = BoxButton(pauseBox, "Continue", new Rect(213f, 810f, 690f, 120f), "CONTINUE", 44f);
            Button restart1 = BoxButton(pauseBox, "Restart", new Rect(213f, 960f, 690f, 120f), "RESTART", 44f);
            Button menu1 = BoxButton(pauseBox, "MainMenu", new Rect(213f, 1110f, 690f, 120f), "MAIN MENU", 44f);

            CanvasGroup completeGroup = Overlay(safe, "CompletePanel", "LEVEL COMPLETE!", 800f, out Box completeBox);
            var reward = Txt(completeBox, "Reward", "", new Rect(113f, 790f, 890f, 120f), fExtra, 42f, C.Gold, localize: false);
            Button next = BoxButton(completeBox, "Next", new Rect(213f, 930f, 690f, 120f), "NEXT", 44f);
            Button replay = BoxButton(completeBox, "Replay", new Rect(213f, 1080f, 690f, 120f), "REPLAY", 44f);
            Button menu2 = BoxButton(completeBox, "MainMenu", new Rect(213f, 1230f, 690f, 120f), "MAIN MENU", 44f);

            CanvasGroup failGroup = Overlay(safe, "FailPanel", "GAME OVER", 520f, out Box failBox);
            Button retry = BoxButton(failBox, "Retry", new Rect(213f, 840f, 690f, 120f), "RETRY", 44f);
            Button menu3 = BoxButton(failBox, "MainMenu", new Rect(213f, 990f, 690f, 120f), "MAIN MENU", 44f);

            Set(flow, "config", config);
            Set(flow, "levelLabel", level);
            Set(flow, "modeLabel", mode);
            Set(flow, "pauseButton", pause);
            Set(flow, "testWinButton", win);
            Set(flow, "testFailButton", fail);
            Set(flow, "pausePanel", pauseGroup);
            Set(flow, "completePanel", completeGroup);
            Set(flow, "failPanel", failGroup);
            Set(flow, "rewardText", reward);
            Set(flow, "continueButton", cont);
            Set(flow, "nextButton", next);
            SetArray(flow, "restartButtons", new Object[] { restart1, replay, retry });
            SetArray(flow, "mainMenuButtons", new Object[] { menu1, menu2, menu3 });
        }

        /// <summary>Karartma + ortada başlıklı panel. Panel ekranın ortasına yaslıdır.</summary>
        private static CanvasGroup Overlay(Transform parent, string name, string title, float height, out Box box)
        {
            var rt = NewUI(name, parent);
            Stretch(rt);
            var dim = Img(rt, "Dim", null, new Color(0f, 0f, 0f, 0.65f), false);
            Stretch(dim.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            Box mid = Group(rt, "Middle", 0.5f);
            float y = 1000f - height * 0.5f;
            box = Section(mid, "Panel", title, new Rect(113f, y, 890f, height));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            rt.gameObject.SetActive(false);
            return group;
        }
    }

    /// <summary>Build listesi: ana menü (0) ve oyun sahnesi (1).</summary>
    public static class ScenesSetup
    {
        public static void ApplyBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuBuilder.ScenePath, true),
                new EditorBuildSettingsScene(GameplaySceneBuilder.ScenePath, true),
            };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        }

        [MenuItem("Jumpline/Build All Scenes")]
        public static void BuildAll()
        {
            MainMenuBuilder.Build();        // önce: koşucu FBX içe aktarımı + animasyon controller'ları yeniden kurulur
            GameplaySceneBuilder.Build();   // sonra: oyun sahnesi güncel controller'ı bağlar
        }
    }
}
