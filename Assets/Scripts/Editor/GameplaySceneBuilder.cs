using Jumpline.Core;
using Jumpline.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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

            BuildWorld();
            BuildRunner();
            BuildCameraAndEvents(false);
            var cam = Camera.main;
            // Üçüncü şahıs: karakterin arkasında, ekranın alt-ortasında (brief)
            cam.transform.SetPositionAndRotation(new Vector3(0f, 3.0f, -5.5f), Quaternion.Euler(14f, 0f, 0f));
            cam.fieldOfView = 62f;
            cam.farClipPlane = 220f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#C3CCD8");

            Transform canvas = BuildCanvas("GameplayCanvas", false);
            var safe = NewUI("SafeArea", canvas);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            BuildUI(safe, canvas.gameObject.AddComponent<LevelFlowController>(), config);

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

        private static void BuildWorld()
        {
            // Güneş: alçak açıdan, sokağa çapraz gölge (referanstaki gibi)
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(38f, -55f, 0f);
            light.color = Hex("#FFF1DE");
            light.intensity = 1.6f;
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
                seg.isStatic = true;
                foreach (Transform t in seg.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
                PlaceDecor(seg.transform);
            }
        }

        // Dekorlar — Blender/scripts/build_decor.py (Desktop/Yeni klasör dekor videoları)
        public const string DecorPath = "Assets/Art/Environment/Diyarbakir/DiyarbakirDecor.fbx";
        private const string DecorMatDir = "Assets/Art/Environment/Diyarbakir/DecorMaterials";
        private static GameObject decorPrefab;

        /// <summary>Segment içi yerleşim (yerel x, y, z, Y açısı). Hepsi duvar dibinde / kaldırımda; koşu şeridi boş.</summary>
        private static readonly (string name, float x, float y, float z, float rot)[] DecorLayout =
        {
            ("Samovar", -2.45f, 0f, 6f, 90f), ("Crates", 2.2f, 0f, 9f, -90f), ("WallLantern", -2.9f, 2.6f, 4f, 90f),
            ("WallLantern", 2.9f, 2.6f, 13f, -90f), ("KilimPile", -2.3f, 0f, 14f, 90f), ("FruitBasket", 2.45f, 0f, 16.5f, -90f),
            ("IronGate", 2.95f, 0f, 21f, -90f), ("Sack", -2.45f, 0f, 22f, 90f), ("Watermelon", 2.35f, 0f, 25f, -90f),
            ("Nazar", -2.92f, 2.0f, 26f, 90f), ("CopperJugs", -2.3f, 0f, 30f, 90f), ("BreadBasket", 2.45f, 0f, 33f, -90f),
            ("CarvedPillar", -2.6f, 0f, 35.5f, 90f), ("Chest", 2.35f, 0f, 38f, -90f), ("RolledCarpet", -2.5f, 0f, 41f, 90f),
            ("WallLantern", -2.9f, 2.6f, 44f, 90f), ("Ladder", 2.7f, 0f, 47f, -90f), ("CopperPlates", -2.45f, 0f, 50f, 90f),
            ("Birdhouse", 2.45f, 0f, 52.5f, -90f), ("KebabCart", -2.1f, 0f, 56f, 90f), ("WallLantern", 2.9f, 2.6f, 58f, -90f),
        };

        private static void PlaceDecor(Transform segment)
        {
            if (decorPrefab == null)
            {
                ConfigureDecorImport();
                decorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DecorPath);
                if (decorPrefab == null) return;
            }
            var root = new GameObject("Decor").transform;
            root.SetParent(segment, false);
            foreach (var d in DecorLayout)
            {
                Transform src = decorPrefab.transform.Find("Decor_" + d.name);
                if (src == null) continue;
                var go = Object.Instantiate(src.gameObject, root);
                go.name = d.name;
                go.transform.localPosition = new Vector3(d.x, d.y, d.z);
                go.transform.localRotation = Quaternion.Euler(0f, d.rot, 0f) * src.localRotation;
                go.transform.localScale = src.localScale;
                go.isStatic = true;
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
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Koşucu: başlangıç noktasında, koşu yönüne (+Z) bakar, giyili kostümle. Hareket sonraki adım.</summary>
        private static void BuildRunner()
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
        }

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

            Txt(mid, "PlaceholderInfo", "The runner and obstacles come next.\nUse the test buttons to try the flow.",
                new Rect(108f, 820f, 900f, 160f), fMedium, 34f, C.TextSoft, wrap: true);
            Button win = PillButton(bot, "TestWin", new Rect(118f, 1780f, 420f, 90f), "WIN (TEST)", 34f, Color.white, 1f);
            Button fail = PillButton(bot, "TestFail", new Rect(578f, 1780f, 420f, 90f), "FAIL (TEST)", 34f, Color.white, 1f, Hex("#E2B45A"), Hex("#F2C366"));

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
            GameplaySceneBuilder.Build();
            MainMenuBuilder.Build();
        }
    }
}
