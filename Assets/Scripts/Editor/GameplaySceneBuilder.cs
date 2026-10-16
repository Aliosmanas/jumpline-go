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
    public static partial class GameplaySceneBuilder
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

            SegmentCombiner.EnsureReadable(StreetPath, DecorPath, RooftopPath, LandmarkPath);   // modüller açılışta birleşir
            Transform oldStreet = BuildWorld();   // ışık + sis (eski Blender sokağı kurulmaz: UseMeshyMaps)
            Transform runner = BuildRunner();
            Transform street;
            if (UseMeshyMaps)
            {
                Object.DestroyImmediate(oldStreet.gameObject);
                street = BuildMapStreet(runner);   // Blender/Harita: 12 mahalle + geçişler + 6 sur üstü, rastgele zincir
            }
            else
            {
                street = oldStreet;
                Set(street.gameObject.AddComponent<StreetRecycler>(), "player", runner);
            }
            Transform rooftop = BuildRooftopWorld(runner);
            BuildCameraAndEvents(false);
            var cam = Camera.main;
            // Üçüncü şahıs: karakterin arkasında, ekranın alt-ortasında (brief)
            // alçak ve yakın takip + geniş açı: zemin hızla akar (koşu hissi), 3 şerit yine ekranda
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.3f, -6.2f), Quaternion.Euler(16f, 0f, 0f));
            cam.fieldOfView = 70f;
            cam.farClipPlane = 165f;   // sis 150 m'de tam kapanır; ötesini çizmek boşa (telefonda pahalı)
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
            Set(canvas.GetComponent<LevelFlowController>(), "worldSwitcher", switcher);
            Portal up = BuildPortal(runner, switcher, true, "PortalUp", null);                 // sokak → çatı (mavi)
            Portal down = BuildPortal(runner, switcher, false, "PortalDown", Hex("#FF3C8E"));  // çatı → sokak (pembe)
            var (streetObs, roofObs) = BuildObstacles(runner, switcher, new[] { up, down });
            var coins = BuildCoins(runner);
            BuildObstacleHints(streetObs, roofObs);   // renk kodlu işaretler: ▲ zıpla ▼ kay ✕ kaç, sarı-siyah hareketli, yeşil çık
            Set(streetObs, "coins", coins);
            Set(roofObs, "coins", coins);
            if (coinLabel != null)
            {
                var counter = coinLabel.gameObject.AddComponent<Jumpline.UI.CoinCounter>();
                Set(counter, "field", coins);
                Set(counter, "label", coinLabel);
                Set(counter, "icon", coinIcon.rectTransform);
                var score = scoreLabel.gameObject.AddComponent<Jumpline.UI.ScoreCounter>();
                Set(score, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
                Set(score, "label", scoreLabel);
                Set(canvas.GetComponent<LevelFlowController>(), "score", score);
                // Hızlı Yarış: para kapsülünün solunda "2x" rozeti
                var x2 = Panel(hudTopBox, "CoinX2", new Rect(686f, 190f, 84f, 58f), Hex("#B3262E"), Hex("#FF4B5A"), 29f, Hex("#FFE08A"));
                Txt(x2, "X2", "2x", new Rect(686f, 190f, 84f, 58f), fExtra, 34f, Color.white, localize: false);
                x2.go.SetActive(false);
                SetArray(canvas.GetComponent<LevelFlowController>(), "quickRaceOnly", new Object[] { x2.go });
            }
            BuildPowerUps(runner, coins);
            BuildBossChase(runner, follow, switcher, streetObs, up, down, canvas.GetComponent<LevelFlowController>());
            BuildGameplayAudio(runner, coins, switcher, canvas.GetComponent<LevelFlowController>());
            BuildStory(safe, runner);
            BuildDailyMission(runner, canvas.GetComponent<LevelFlowController>());
            var director = new GameObject("LevelDirector").AddComponent<LevelDirector>();   // bölüm zorluğu (GameConfig.LevelSpec)
            Set(director, "config", config);
            Set(director, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            SetArray(director, "spawners", new Object[] { streetObs, roofObs });
            Set(director, "boss", Object.FindAnyObjectByType<Jumpline.Boss.BossChase>(FindObjectsInactive.Include));
            Set(director, "flow", canvas.GetComponent<LevelFlowController>());
            SetArray(director, "stoneMaterials", new[] { "S_Basalt", "S_BasaltTrim", "S_Paving", "S_Curb" }
                .Select(n => (Object)AssetDatabase.LoadAssetAtPath<Material>($"{StreetMatDir}/{n}.mat")).Where(m => m != null).ToArray());
            SetArray(director, "worldRoots", new Object[] { street, rooftop });
            var dso = new SerializedObject(director);
            var arr = dso.FindProperty("bodies");
            arr.arraySize = bossBodies.Count;
            for (int i = 0; i < bossBodies.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("legend").stringValue = bossBodies[i].legend;
                e.FindPropertyRelative("root").objectReferenceValue = bossBodies[i].root;
                e.FindPropertyRelative("animator").objectReferenceValue = bossBodies[i].animator;
                e.FindPropertyRelative("hand").objectReferenceValue = bossBodies[i].hand;
                var rr = e.FindPropertyRelative("renderers");
                rr.arraySize = bossBodies[i].renderers.Length;
                for (int k = 0; k < rr.arraySize; k++) rr.GetArrayElementAtIndex(k).objectReferenceValue = bossBodies[i].renderers[k];
            }
            for (int i = 0; i < bossBodies.Count; i++)
                arr.GetArrayElementAtIndex(i).FindPropertyRelative("icon").objectReferenceValue = bossBodies[i].icon;
            var icons = dso.FindProperty("bossIcons");
            icons.arraySize = bossIconImages.Count;
            for (int i = 0; i < bossIconImages.Count; i++) icons.GetArrayElementAtIndex(i).objectReferenceValue = bossIconImages[i];
            dso.ApplyModifiedPropertiesWithoutUndo();
            BuildProgressTracker(runner, coins, canvas.GetComponent<LevelFlowController>());
            Set(canvas.gameObject.AddComponent<Jumpline.UI.ContrastMode>(), "volume", Object.FindAnyObjectByType<UnityEngine.Rendering.Volume>());
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
        // 15 Eki 2026: sokak artık Blender/Harita'daki Meshy haritalarından rastgele zincir (GameplaySceneBuilder.Maps)
        private const bool UseMeshyMaps = true;

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
            RenderSettings.ambientSkyColor = Hex("#D2DCEA");
            RenderSettings.ambientEquatorColor = Hex("#A6ABB5");
            RenderSettings.ambientGroundColor = Hex("#5A5A62");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Hex("#C3CCD8");
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 150f;

            ConfigureStreetImport();
            decorPrefab = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StreetPath);
            var root = new GameObject("Street").transform;
            for (int i = -1; i < (UseMeshyMaps ? -1 : StreetCopies); i++) // bir modül karakterin arkasında (kamera boşluk görmesin)
            {
                var seg = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                seg.name = $"StreetSegment_{i}";
                seg.transform.SetPositionAndRotation(new Vector3(0f, 0f, i * StreetSegment), Quaternion.identity);
                PlaceDecor(seg.transform, DecorLayout); // statik değil: StreetRecycler modülleri öne taşır
                PlaceDecor(seg.transform, MirroredDecor());
                PlaceSigns(seg.transform);
                if (i == 0) PlaceLandmark(seg.transform, "Landmark_DortAyakliMinare", new Vector3(14f, 0f, 28f));
                if (i == 2) PlaceLandmark(seg.transform, "Landmark_UluCamiMinaret", new Vector3(-16f, 0f, 40f));
                DressStreetWithMeshy(seg.transform);   // Meshy binaları (eski evler/kemerler kalkar)
                SegmentCombiner.Combine(seg.transform);   // performans: sabit mesh'ler malzemeye göre tek çizim
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
                var meshy = MeshyDecorFor(d.name, root, src);   // Meshy karpuz / testi
                if (meshy != null)
                {
                    meshy.transform.localRotation = Quaternion.Euler(0f, d.rot, 0f) * meshy.transform.localRotation;
                    meshy.transform.localPosition += new Vector3(d.x, d.y, d.z);
                    continue;
                }
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
                SegmentCombiner.Combine(mod.transform);
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
            SetLook(sw, "streetLook", "#E6CFAE", "#E3C9A4", 30f, 190f, "#DCE6F5", "#C4B4A6", "#6E6158", "#FFDDB0", 2.3f, new Vector3(26f, -35f, 0f), true);
            // çatı: fırtınalı gece (üst map.mp4) — lacivert gök, ay ışığı, koyu mavi pus
            SetLook(sw, "rooftopLook", "#1B2536", "#1E2939", 18f, 130f, "#7487AE", "#46557A", "#262C3A", "#B4C6FF", 1.0f, new Vector3(42f, 35f, 0f), false);
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

        // ---------------------------------------------------------------------
        // Paralar — Blender/scripts/build_coin.py (coin.png ikonunun 3D hâli), CoinField diziler halinde dağıtır
        // ---------------------------------------------------------------------
        public const string CoinPath = "Assets/Art/Pickups/Coin.fbx";
        private const string CoinMatDir = "Assets/Art/Pickups/Materials";

        private static CoinField BuildCoins(Transform runner)
        {
            EnsureFolder(CoinMatDir);
            MeshyImport.Prepare(CoinPath);   // Meshy akçe (dokulu)
            if (AssetImporter.GetAtPath(CoinPath) is ModelImporter imp)
            {
                imp.importCameras = imp.importLights = imp.importAnimation = false;
                imp.SaveAndReimport();
                foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CoinPath))
                {
                    if (o is not Material m) continue;
                    string dst = $"{CoinMatDir}/{m.name}.mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
                }
                AssetDatabase.WriteImportSettingsIfDirty(CoinPath);
                AssetDatabase.ImportAsset(CoinPath, ImportAssetOptions.ForceUpdate);
            }
            foreach (var (name, hex, metal, smooth, glow) in new[] { ("C_Gold", "#E8B95B", 0.9f, 0.75f, 0.35f), ("C_Face", "#5E1634", 0.2f, 0.55f, 0f) })
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{CoinMatDir}/{name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetColor("_BaseColor", Hex(hex));
                mat.SetFloat("_Metallic", metal);
                mat.SetFloat("_Smoothness", smooth);
                if (glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Hex(hex) * glow);   // gölgede de parlasın
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            MeshyImport.Tune(CoinMatDir);
            foreach (string guid in AssetDatabase.FindAssets("Coin_Mat t:Material", new[] { CoinMatDir }))
            {   // akçe gölgede de parlasın, hafif metalik altın
                var cm = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                cm.SetFloat("_Metallic", 0.55f); cm.SetFloat("_Smoothness", 0.6f);
                cm.EnableKeyword("_EMISSION"); cm.SetTexture("_EmissionMap", cm.GetTexture("_BaseMap"));
                cm.SetColor("_EmissionColor", new Color(0.35f, 0.28f, 0.12f));
                EditorUtility.SetDirty(cm);
            }
            AssetDatabase.SaveAssets();

            var go = new GameObject("Coins");
            var field = go.AddComponent<CoinField>();
            Set(field, "player", runner.GetComponent<Jumpline.Player.PlayerController>());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoinPath);
            if (prefab != null)
            {
                var tpl = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                tpl.name = "CoinTemplate";
                foreach (var r in tpl.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                tpl.SetActive(false);
                Set(field, "coinTemplate", tpl);
            }
            // toplama pırıltısı: altın kıvılcımlar
            var fx = MakeSparks(go.transform, "CoinPickupFx");
            var main = fx.main;
            main.loop = false;
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("#FFE08A"), Hex("#E8B95B"));
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.gravityModifier = 0.6f;
            var em = fx.emission;
            em.rateOverTime = 0f;
            Set(field, "pickupFx", fx);
            return field;
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
            tone.mode.Override(TonemappingMode.Neutral);   // ACES orta tonları eziyordu (oyun karanlık görünüyordu)
            var color = profile.Add<ColorAdjustments>(true);   // sıcak, doygun Mezopotamya ikindisi
            color.postExposure.Override(0.35f);
            color.contrast.Override(8f);
            color.saturation.Override(18f);
            color.colorFilter.Override(Hex("#FFF0DC"));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.45f);
            // hareket bulanıklığı yok: telefonda görüntüyü lekeli gösteriyor ve pahalı (koşu hissini hız çizgileri veriyor)
            foreach (VolumeComponent c in new VolumeComponent[] { bloom, tone, color, vignette })
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

        // ---------------------------------------------------------------------
        // Engel modelleri (Blender/scripts/build_course_assets.py, fit_obstacles.py) — chunk sistemi: GameplaySceneBuilder.Chunks.cs
        // ---------------------------------------------------------------------
        private const string ObstacleDir = "Assets/Art/Environment/Obstacles";

        private static Obstacle.Segment Seg(float z0, float z1, float h0, float h1) => new Obstacle.Segment { z0 = z0, z1 = z1, h0 = h0, h1 = h1 };
        private static Obstacle.Segment Seg(float z0, float z1, float h) => Seg(z0, z1, h, h);

        /// <summary>Ayrı parçalı engel (çay masası + tabureler): parçalar köke bağlanır, Breakable savurur.</summary>
        private static Obstacle PiecesObstacle(string path, string name, Transform parent, float scale)
        {
            ConfigureVehicleImport(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.transform.localPosition = Vector3.zero;
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var kids = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in vis.transform) kids.Add(c);
            foreach (Transform c in kids) c.SetParent(root.transform, true);
            if (kids.Count > 0) Object.DestroyImmediate(vis);   // tek parçalı model (Meshy): kendisi kalır
            root.transform.localScale = Vector3.one * scale;
            var rs = root.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 c0 = b.center - root.transform.position;
            float h = b.max.y - root.transform.position.y;
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(Obstacle.Kind.Solid, Mathf.Clamp(b.extents.x, 0.45f, 1f),
                new[] { Seg(c0.z - Mathf.Max(b.extents.z, 0.35f), c0.z + Mathf.Max(b.extents.z, 0.35f), h) });
            root.AddComponent<Breakable>();
            EditorUtility.SetDirty(obs);
            return obs;
        }

        /// <summary>Renk çeşidi: şablonun kopyası, verilen malzeme yuvası yeni renkli malzemeyle (beyaz Şahin, kırmızı kamyonet…).</summary>
        private static Obstacle Recolor(Obstacle src, string name, string slot, string hex, Transform parent)
        {
            if (src == null) return null;
            string path = $"{TruckMatDir}/{slot}_{hex.TrimStart('#')}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>($"{TruckMatDir}/{slot}.mat");
                if (baseMat == null) return null;
                mat = new Material(baseMat);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Hex(hex));
            EditorUtility.SetDirty(mat);
            var go = Object.Instantiate(src.gameObject, parent);
            go.name = name;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i] != null && mats[i].name == slot) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            return go.GetComponent<Obstacle>();
        }

        /// <summary>Dekoru engel şablonu yapar: taban/yükseklik dekorun sınırlarından.</summary>
        private static Obstacle DecorObstacle(string name, Transform parent, float minHeight)
        {
            if (decorPrefab == null) decorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecorPath);
            Transform src = decorPrefab != null ? decorPrefab.transform.Find("Decor_" + name) : null;
            if (src == null) return null;
            var root = new GameObject("Obs_" + name);
            root.transform.SetParent(parent, false);
            var vis = MeshyDecorFor(name, root.transform, src);   // Meshy karpuz / testi
            if (vis == null)
            {
                vis = Object.Instantiate(src.gameObject, root.transform);
                vis.name = name;
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = src.localRotation;
                vis.transform.localScale = src.localScale;
            }
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
            obs.Configure(Obstacle.Kind.Solid, 0.95f, AutoSegments(root, 0.95f, true) ?? RampTruckSegments);   // Meshy kamyon: profil modelden, rampa düzleştirilir
            EditorUtility.SetDirty(obs);
            return obs;
        }

        // Otomatik yüzey profili dilim boyu (m): koşucunun basamak çıkma payından (0.35) küçük adımlar → rampa tırmanılır.
        private const float AutoSliceLength = 0.25f;

        /// <summary>
        /// Modelin kendi yüzeyinden çarpışma profili: koşu yönünde (yerel z) dilimler, her dilim şeritteki en yüksek nokta.
        /// Rampa küçük basamaklara bölünür (koşucu çıkar), kabin önü gibi dik yüzler duvar olur. Boş dilim = yüzey yok.
        /// </summary>
        private static Obstacle.Segment[] AutoSegments(GameObject root, float halfWidth, bool ramp = false)
        {
            var pts = new System.Collections.Generic.List<Vector3>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                foreach (Vector3 v in mf.sharedMesh.vertices) pts.Add(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            if (pts.Count == 0) return null;
            float zMin = float.MaxValue, zMax = float.MinValue;
            foreach (var p in pts) if (Mathf.Abs(p.x) <= halfWidth) { zMin = Mathf.Min(zMin, p.z); zMax = Mathf.Max(zMax, p.z); }
            if (zMax <= zMin) return null;
            int n = Mathf.Max(1, Mathf.CeilToInt((zMax - zMin) / AutoSliceLength));
            var h = new float[n];
            foreach (var p in pts)
            {
                if (Mathf.Abs(p.x) > halfWidth) continue;
                int i = Mathf.Clamp((int)((p.z - zMin) / AutoSliceLength), 0, n - 1);
                h[i] = Mathf.Max(h[i], p.y);
            }
            if (ramp)
            {   // rampa (koşucunun geldiği uç, z küçük): ana düzlüğe (ilk %80'in en yükseği) ilk ulaşılan dilime kadar kesintisiz düz eğim
                int lim = Mathf.Max(2, (int)(n * 0.8f)); float peak = 0f;
                for (int i = 0; i < lim; i++) peak = Mathf.Max(peak, h[i]);
                int top = 0;
                while (top < lim - 1 && h[top] < peak * 0.95f) top++;
                for (int i = 0; i < top; i++) h[i] = Mathf.Lerp(0.05f, h[top], (float)i / Mathf.Max(1, top));
            }
            // pürüzsüz yüzey: komşu dilimle fark basamak payı içindeyse kenar yüksekliği ortalama (eğim) → koşucu
            // basamak basamak "ışınlanmaz"; büyük farklar (kabin önü, tampon) dik duvar kalır
            const float SmoothStep = 0.32f;
            var segs = new System.Collections.Generic.List<Obstacle.Segment>();
            for (int i = 0; i < n; i++)
            {
                if (h[i] < 0.05f) continue;
                float z0 = zMin + i * AutoSliceLength, z1 = Mathf.Min(zMax, z0 + AutoSliceLength);
                float a = i > 0 && h[i - 1] >= 0.05f && Mathf.Abs(h[i] - h[i - 1]) <= SmoothStep ? (h[i] + h[i - 1]) * 0.5f : h[i];
                float b = i < n - 1 && h[i + 1] >= 0.05f && Mathf.Abs(h[i + 1] - h[i]) <= SmoothStep ? (h[i] + h[i + 1]) * 0.5f : h[i];
                if (i == 0 && ramp) a = 0.05f;
                if (segs.Count > 0 && Mathf.Abs(segs[^1].z1 - z0) < 1e-3f && Mathf.Abs(segs[^1].h1 - a) < 0.01f && Mathf.Abs(segs[^1].h0 - segs[^1].h1) < 0.01f && Mathf.Abs(a - b) < 0.01f && Mathf.Abs(segs[^1].h1 - b) < 0.01f)
                { var last = segs[^1]; last.z1 = z1; segs[^1] = last; }
                else segs.Add(Seg(z0, z1, a, b));
            }
            return segs.Count > 0 ? segs.ToArray() : null;
        }

        /// <summary>Tek parçalı araç/kapı FBX'inden engel şablonu (FBX'in eksen dönüşü korunur).</summary>
        private static Obstacle VehicleObstacle(string path, string name, Transform parent, Obstacle.Kind kind, float halfWidth, Obstacle.Segment[] segs,
                                                float thickness = 0f, bool ramp = false)
        {
            ConfigureVehicleImport(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogError($"[Jumpline] {path} yok — Blender/scripts/build_vehicles.py çalıştır"); return null; }
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            vis.transform.localPosition = Vector3.zero;
            var obs = root.AddComponent<Obstacle>();
            obs.Configure(kind, halfWidth, segs ?? AutoSegments(root, halfWidth, ramp), thickness);
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
            if (crates.Count > 0) Object.DestroyImmediate(vis);   // tek parçalı model (Meshy): kendisi kalır, bütün devrilir
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
            MeshyImport.Prepare(path);
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
                ("T_Melon", "#2E6B2F", 0f, 0.55f, 0f), ("T_MelonDark", "#1C4A1F", 0f, 0.55f, 0f), ("T_CityGreen", "#3E8E6E", 0f, 0.65f, 0.1f),
                ("T_Orange", "#C14E2A", 0f, 0.55f, 0.1f), ("T_Hay", "#D9B65A", 0f, 0.1f, 0f), ("T_Copper", "#B8683A", 0f, 0.7f, 0.85f),
                ("T_Simit", "#B9772F", 0f, 0.3f, 0f), ("T_Canvas", "#EFE7D6", 0f, 0.2f, 0f), ("T_Umbrella", "#B3262E", 0f, 0.3f, 0f),
                ("T_Lime", "#E3D8C0", 0f, 0.15f, 0f), ("T_KilimRed", "#9B2335", 0f, 0.1f, 0f), ("T_KilimGold", "#D9A441", 0f, 0.1f, 0f),
                ("T_KilimBlue", "#2C4A7A", 0f, 0.1f, 0f), ("T_Coal", "#FF7A2A", 3f, 0.3f, 0f),
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
            MeshyImport.Tune(TruckMatDir);
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
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerStageBuilder.RunnerPrefabPath));
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
            // kayma tozu: ayak hizasında, geriye savrulan sıcak toz + birkaç kıvılcım
            var dust = MakeBurst(model.transform, "SlideDust", Hex("#E6CFAE"), Hex("#FFD27A"), 1.2f, 3.2f, 0.4f);
            dust.transform.localPosition = new Vector3(0f, 0.15f, -0.2f);
            var dm = dust.main;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            dm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
            var dsh = dust.shape;
            dsh.shapeType = ParticleSystemShapeType.Cone;
            dsh.angle = 35f;
            dsh.radius = 0.25f;
            dust.transform.localRotation = Quaternion.Euler(-160f, 0f, 0f);   // geriye ve yukarı
            Set(player, "slideFx", dust);
            return model.transform;
        }

        private static TextMeshProUGUI speedLabel, coinLabel, scoreLabel;
        private static Box hudMid, hudTopBox;
        private static Image coinIcon;

        private static void BuildUI(Transform safe, LevelFlowController flow, GameConfig config)
        {
            var hud = NewUI("HUD", safe);
            Stretch(hud);
            Box top = Group(hud, "Top", 1f);
            Box mid = Group(hud, "Middle", 0.5f);
            hudMid = mid;
            Box bot = Group(hud, "Bottom", 0f);

            // docs/flow_ui/hud_1.jpg: oymalı bazalt plaka (bölüm + şehir), safran duraklat, bakır kenarlı sayaçlar
            SkinImage(top, "LevelPlaque", new Rect(228f, 30f, 660f, 168f), "plaque", false, 0.5f);
            var level = Txt(top, "LevelLabel", "LEVEL 1", new Rect(338f, 44f, 440f, 84f), fTitle, 66f, Color.white, material: chromeMaterial, localize: false);
            Chrome(level);
            var mode = Txt(top, "ModeLabel", "", new Rect(338f, 124f, 440f, 46f), fExtra, 28f, Hex("#FFD23F"), localize: false);
            var progress = ProgressBar(top, "Progress", new Rect(258f, 214f, 600f, 30f), 0f);
            hudTop = top;
            hudTopBox = top;
            hudBot = bot;
            chaseHidden = new[] { mode.gameObject, progress.parent.gameObject };   // ProgressBar dolguyu döndürür; iz ebeveyn

            var pauseImg = SkinImage(top, "PauseButton", new Rect(950f, 36f, 132f, 132f), "round_saffron", true);
            var pausePanel = new Box(pauseImg.rectTransform, new Rect(950f, 36f, 132f, 132f));
            Txt(pausePanel, "Bars", "II", new Rect(950f, 40f, 132f, 120f), fExtra, 72f, Color.white, localize: false);
            Button pause = MakeButton(pausePanel, pausePanel.rt.GetComponent<Image>());

            // para sayacı (duraklat butonunun altında) + puan (metre başına 10)
            // para kapsülü (altın kenar) + altında puan kapsülü (neon kenar, "PUAN" etiketi)
            SkinImage(top, "CoinPill", new Rect(756f, 186f, 320f, 74f), "pill", false, 0.45f);
            coinIcon = Icon(top, "coin", new Rect(1007f, 189f, 60f, 60f), Color.white);
            coinLabel = Txt(top, "CoinCount", "0", new Rect(778f, 186f, 220f, 74f), fExtra, 42f, Color.white, TextAlignmentOptions.MidlineRight, localize: false);
            SkinImage(top, "ScorePill", new Rect(776f, 272f, 300f, 66f), "pill", false, 0.45f);
            var scoreTag = Txt(top, "ScoreTag", "SCORE", new Rect(800f, 272f, 110f, 66f), fExtra, 24f, Hex("#4FE3D6"), TextAlignmentOptions.MidlineLeft);
            scoreLabel = Txt(top, "Score", "0", new Rect(900f, 268f, 158f, 64f), fExtra, 34f, Color.white, TextAlignmentOptions.MidlineRight, localize: false);
            coinLabel.raycastTarget = scoreLabel.raycastTarget = coinIcon.raycastTarget = scoreTag.raycastTarget = false;

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
            Button cont = Reskin(BoxButton(pauseBox, "Continue", new Rect(213f, 790f, 690f, 150f), "CONTINUE", 60f), "btn_green");
            Button restart1 = Reskin(BoxButton(pauseBox, "Restart", new Rect(213f, 970f, 330f, 120f), "RESTART", 40f), "btn_turquoise");
            Button menu1 = Reskin(BoxButton(pauseBox, "MainMenu", new Rect(573f, 970f, 330f, 120f), "MAIN MENU", 40f), "btn_red");

            CanvasGroup completeGroup = Overlay(safe, "CompletePanel", "LEVEL COMPLETE!", 800f, out Box completeBox);
            var reward = Txt(completeBox, "Reward", "", new Rect(113f, 790f, 890f, 120f), fExtra, 42f, C.Gold, localize: false);
            var starImgs = new Object[3];
            for (int k = 0; k < 3; k++)
                starImgs[k] = Icon(completeBox, "star", new Rect(398f + k * 110f, 700f, 100f, 100f), Color.white);
            SetArray(flow, "starImages", starImgs);
            Button next = Reskin(BoxButton(completeBox, "Next", new Rect(213f, 930f, 690f, 150f), "NEXT", 60f), "btn_green");
            Button replay = Reskin(BoxButton(completeBox, "Replay", new Rect(213f, 1110f, 330f, 120f), "REPLAY", 40f), "btn_turquoise");
            Button menu2 = Reskin(BoxButton(completeBox, "MainMenu", new Rect(573f, 1110f, 330f, 120f), "MAIN MENU", 40f), "btn_red");

            CanvasGroup failGroup = Overlay(safe, "FailPanel", "GAME OVER", 820f, out Box failBox);
            Set(flow, "failReasonLabel", failBox.rt.Find("Title").GetComponent<TextMeshProUGUI>());   // kaçınca başlık değişir
            var gemBalance = Txt(failBox, "GemBalance", "", new Rect(213f, 690f, 690f, 50f), fBold, 30f, Hex("#BFD8FF"), localize: false);
            Button reviveAd = Reskin(BoxButton(failBox, "ReviveAd", new Rect(213f, 750f, 690f, 110f), "WATCH AD · CONTINUE (SOON)", 32f), "btn_turquoise");
            Button reviveGems = Reskin(BoxButton(failBox, "ReviveGems", new Rect(213f, 875f, 690f, 110f), "CONTINUE · 50", 40f, out TextMeshProUGUI reviveLabel, false), "btn_green");
            Icon(failBox, "gem", new Rect(236f, 891f, 78f, 78f), Color.white).raycastTarget = false;
            Button retry = BoxButton(failBox, "Retry", new Rect(213f, 1000f, 690f, 110f), "PLAY AGAIN", 40f);
            Button menu3 = Reskin(BoxButton(failBox, "MainMenu", new Rect(213f, 1125f, 690f, 110f), "MAIN MENU", 40f), "btn_red");
            var countdown = Txt(hudMid, "Countdown", "3", new Rect(258f, 760f, 600f, 400f), fExtra, 280f, Color.white, localize: false);
            countdown.raycastTarget = false;
            countdown.gameObject.SetActive(false);
            Set(flow, "reviveGemsButton", reviveGems);
            Set(flow, "reviveAdButton", reviveAd);
            Set(flow, "reviveGemsLabel", reviveLabel);
            Set(flow, "gemBalanceLabel", gemBalance);
            Set(flow, "countdownLabel", countdown);

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
            // uygulama ikonu (Tools/make_store_assets.py → Assets/Art/UI/AppIcon.png)
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/AppIcon.png");
            if (icon != null)
                PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
            PlayerSettings.productName = "JUMPLINE GO";
        }

        [MenuItem("Jumpline/Build All Scenes")]
        public static void BuildAll()
        {
            LoadingScreenBuilder.Build();
            MainMenuBuilder.Build();        // önce: koşucu FBX içe aktarımı + animasyon controller'ları yeniden kurulur
            GameplaySceneBuilder.Build();   // sonra: oyun sahnesi güncel controller'ı bağlar
        }
    }
}
