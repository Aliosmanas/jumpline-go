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
            cam.transform.SetPositionAndRotation(new Vector3(0f, 5f, -9f), Quaternion.Euler(20f, 0f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Hex("#20263A");

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

        private static void BuildWorld()
        {
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.shadows = LightShadows.Soft;

            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            if (mat == null)
            {
                mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial) { name = "Ground" };
                AssetDatabase.CreateAsset(mat, GroundMaterialPath);
            }
            mat.SetColor("_BaseColor", Hex("#5A1F33"));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.25f, 55f);
            ground.transform.localScale = new Vector3(6f, 0.5f, 120f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Koşucu: başlangıç noktasında, koşu yönüne (+Z) bakar, giyili kostümle. Hareket sonraki adım.</summary>
        private static void BuildRunner()
        {
            var library = RunnerStageBuilder.EnsureLibrary();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RunnerStageBuilder.FbxPath));
            model.name = "Runner";
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (model.TryGetComponent(out Animator anim)) anim.enabled = false;
            RunnerStageBuilder.ApplyDisplayPose(model.transform);

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
