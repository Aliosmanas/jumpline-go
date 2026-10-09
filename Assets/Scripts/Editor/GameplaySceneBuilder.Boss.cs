using Jumpline.Boss;
using Jumpline.Level;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Canavar kovalaması (bölüm 1, Tepegöz): canavar kökü + geçici gövde (Blender/scripts/build_throwables.py TepegozBlock),
    /// fırlatılabilirler (karpuz, bakır testi), düşme işareti, efektler ve HUD (kalpler, mesafe, atma düğmesi).
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string BossDir = "Assets/Art/Boss";
        private const string BossMatDir = BossDir + "/Materials";
        public const string ThrowablesPath = BossDir + "/Throwables.fbx";
        public const string TepegozBlockPath = BossDir + "/TepegozBlock.fbx";
        private const string KarpuzTexPath = BossDir + "/Karpuz_Albedo.png";

        private static Box hudTop, hudBot;
        private static GameObject[] chaseHidden;

        private static void BuildBossChase(Transform runner, Jumpline.CameraSystem.CameraFollow follow, WorldSwitcher switcher,
                                           ObstacleSpawner street, Portal up, Portal down, LevelFlowController flow)
        {
            ConfigureBossImport(ThrowablesPath);
            ConfigureBossImport(TepegozBlockPath);
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();

            // --- canavar ---
            var root = new GameObject("Tepegoz");
            var boss = root.AddComponent<BossChase>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TepegozBlockPath);
            Renderer[] flash = new Renderer[0];
            if (blockPrefab != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab, visual);
                body.transform.localPosition = Vector3.zero;   // FBX eksen dönüşü korunur
                flash = body.GetComponentsInChildren<Renderer>();
                foreach (var r in flash)
                    foreach (var m in r.sharedMaterials)
                        if (m != null) m.EnableKeyword("_EMISSION");   // isabet parlaması için
            }
            Set(boss, "player", player);
            Set(boss, "switcher", switcher);
            Set(boss, "cameraFollow", follow);
            Set(boss, "streetSpawner", street);
            Set(boss, "upPortal", up);
            Set(boss, "downPortal", down);
            Set(boss, "visual", visual);
            SetArray(boss, "flashRenderers", flash);

            var hit = MakeBurst(root.transform, "BossHitFx", Hex("#FFD27A"), Hex("#FF5A3C"), 3f, 8f, 0.6f);
            var portalFx = MakeBurst(root.transform, "BossPortalFx", Hex("#C9A2FF"), Hex("#55CFFF"), 2f, 6f, 0.2f);
            Set(boss, "hitFx", hit);
            Set(boss, "portalFx", portalFx);

            // fırlatılan engel: sokak üreticisinin sandık yığını şablonu (aşılabilir, kırılır)
            var spso = new SerializedObject(street);
            var lowArr = spso.FindProperty("low");
            Obstacle crate = lowArr.arraySize > 0 ? lowArr.GetArrayElementAtIndex(0).objectReferenceValue as Obstacle : null;
            Set(boss, "thrownTemplate", crate);

            // --- fırlatılabilirler + düşme işareti ---
            var throwPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ThrowablesPath);
            var throwsGo = new GameObject("Throwables");
            var throws = throwsGo.AddComponent<ThrowSystem>();
            Set(throws, "player", player);
            Set(throws, "boss", boss);
            GameObject Template(string child, float scale)
            {
                if (throwPrefab == null) return null;
                Transform src = throwPrefab.transform.Find(child);
                if (src == null) return null;
                var holder = new GameObject("Tpl_" + child);
                holder.transform.SetParent(throwsGo.transform, false);
                var vis = Object.Instantiate(src.gameObject, holder.transform);
                vis.name = child;
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localScale = src.localScale * scale;
                foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                holder.SetActive(false);
                return holder;
            }
            var melon = Template("Karpuz", 1.0f);
            var jug = Template("Testi", 1.0f);
            var so = new SerializedObject(throws);
            var kinds = so.FindProperty("kinds");
            var defs = new (string id, GameObject t, string icon, float w)[] { ("karpuz", melon, "throw_melon", 0.6f), ("testi", jug, "throw_jug", 0.4f) };
            kinds.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var e = kinds.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = defs[i].id;
                e.FindPropertyRelative("template").objectReferenceValue = defs[i].t;
                e.FindPropertyRelative("icon").objectReferenceValue = Icons.TryGetValue(defs[i].icon, out var sp) ? sp : null;
                e.FindPropertyRelative("weight").floatValue = defs[i].w;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(throws, "pickupFx", MakeBurst(throwsGo.transform, "PickupFx", Hex("#FFF3B0"), Hex("#7CFF8A"), 1.5f, 4f, 0.4f));
            Set(throws, "smashFx", MakeBurst(throwsGo.transform, "SmashFx", Hex("#FF5D6C"), Hex("#7FD15A"), 3f, 9f, 1.4f));

            if (throwPrefab != null && throwPrefab.transform.Find("LandingMarker") is Transform mk)
            {
                var marker = Object.Instantiate(mk.gameObject);
                marker.name = "LandingMarker";
                marker.transform.position = Vector3.zero;
                foreach (var r in marker.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                marker.SetActive(false);
                Set(boss, "landingMarker", marker.transform);
            }

            BuildBossHud(boss, throws);
            Set(flow, "boss", boss);
            SetArray(flow, "hideDuringChase", chaseHidden);
        }

        /// <summary>Kalpler + mesafe göstergesi (üst), duyuru (orta), atma düğmesi (sağ alt; kaydırma alanının köşesinde).</summary>
        private static void BuildBossHud(BossChase boss, ThrowSystem throws)
        {
            var hudRoot = NewUI("BossHud", hudTop.rt.parent);
            Stretch(hudRoot);
            Box top = Group(hudRoot, "Top", 1f);
            Box mid = Group(hudRoot, "Middle", 0.5f);
            Box bot = Group(hudRoot, "Bottom", 0f);
            var hud = hudRoot.gameObject.AddComponent<Jumpline.UI.BossHud>();

            // kalpler (canavarın yüzü solda)
            Icon(top, "boss_head", new Rect(300f, 146f, 84f, 84f), Color.white);
            var hearts = new Image[4];
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Icon(top, "heart", new Rect(394f + i * 86f, 150f, 76f, 76f), Color.white);

            // mesafe: koşucu (sol) ←→ canavar; dolgu uzadıkça kırmızılaşır
            const float mx = 318f, my = 244f, mw = 410f, mh = 26f;
            Panel(top, "GapTrack", new Rect(mx, my, mw, mh), C.NavyDark, Hex("#2A3556"), mh * 0.5f, Hex("#7083B3"));
            var fillBox = Add(top, "GapFill", new Rect(mx + 3f, my + 3f, mw - 6f, mh - 6f));
            var fillImg = fillBox.go.AddComponent<Image>();
            Rounded(fillImg, (mh - 6f) * 0.5f);
            fillImg.raycastTarget = false;
            PivotLeft(fillBox);
            var runnerDot = Add(top, "RunnerDot", new Rect(mx - 22f, my - 9f, 44f, 44f));
            var dotImg = runnerDot.go.AddComponent<Image>();
            dotImg.sprite = circle;
            dotImg.color = Hex("#55CFFF");
            dotImg.raycastTarget = false;
            var marker = Icon(top, "boss_head", new Rect(mx + mw * 0.5f - 34f, my - 21f, 68f, 68f), Color.white);
            var warn = Txt(top, "GapWarning", "ESCAPING!", new Rect(mx, my + 34f, mw, 54f), fExtra, 40f, Hex("#FF4B3E"));
            warn.gameObject.SetActive(false);

            Material outlined = OutlinedFont();
            var announce = Txt(mid, "BossAnnounce", "", new Rect(60f, 540f, 996f, 160f), fExtra, 58f, Hex("#FFE08A"), TextAlignmentOptions.Center, outlined, localize: false, wrap: true);
            announce.enableAutoSizing = true;
            announce.fontSizeMin = 36f;
            announce.fontSizeMax = 58f;

            // atma düğmesi: bakır halkalı yuvarlak, içinde nesne, altında "AT", sayısı köşede
            var btnRoot = Add(bot, "ThrowButton", new Rect(826f, 1592f, 250f, 250f));
            var group = btnRoot.go.AddComponent<CanvasGroup>();
            var ring = Panel(btnRoot, "Ring", new Rect(826f, 1592f, 250f, 250f), Hex("#7A3B1C"), Hex("#E39A55"), 125f, Hex("#FFE0A8"));
            var face = Panel(btnRoot, "Face", new Rect(844f, 1610f, 214f, 214f), Hex("#D99A52"), Hex("#FBE7C0"), 107f, Hex("#00000000"));
            var icon = Icon(btnRoot, "throw_melon", new Rect(871f, 1622f, 160f, 160f), Color.white);
            Txt(btnRoot, "ThrowLabel", "THROW", new Rect(826f, 1764f, 250f, 56f), fExtra, 40f, Color.white, TextAlignmentOptions.Center, outlined);
            var badge = Panel(btnRoot, "CountBadge", new Rect(1010f, 1582f, 78f, 78f), Hex("#B3262E"), Hex("#FF4B5A"), 39f, Hex("#FFFFFF"));
            var count = Txt(badge, "Count", "x2", new Rect(1010f, 1582f, 78f, 78f), fExtra, 34f, Color.white, localize: false);
            var hit = ring.go.GetComponent<Image>();
            hit.raycastTarget = true;
            var button = MakeButton(ring, hit);

            Set(hud, "boss", boss);
            Set(hud, "throws", throws);
            Set(hud, "root", hudRoot.gameObject);
            SetArray(hud, "hearts", hearts);
            Set(hud, "meterFill", fillBox.rt);
            Set(hud, "meterFillImage", fillImg);
            Set(hud, "bossMarker", marker.rectTransform);
            SetFloat(hud, "meterWidth", mw - 6f);
            Set(hud, "warningLabel", warn);
            Set(hud, "announceLabel", announce);
            Set(hud, "throwButton", button);
            Set(hud, "throwGroup", group);
            Set(hud, "throwIcon", icon);
            Set(hud, "throwCount", count);
        }

        /// <summary>Koyu konturlu başlık yazısı malzemesi (fExtra'nın kopyası; diğer yazılar etkilenmez).</summary>
        private static Material OutlinedFont()
        {
            const string path = "Assets/Art/UI/Fonts/ExtraBold_Outline.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(fExtra.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", 0.2f);
            mat.SetColor("_OutlineColor", new Color32(40, 12, 20, 255));
            mat.SetFloat("_FaceDilate", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Tek seferlik patlama parçacığı (Emit ile).</summary>
        private static ParticleSystem MakeBurst(Transform parent, string name, Color a, Color b, float speedMin, float speedMax, float gravity)
        {
            var fx = MakeSparks(parent, name);
            var main = fx.main;
            main.loop = false;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.gravityModifier = gravity;
            var em = fx.emission;
            em.rateOverTime = 0f;
            var sh = fx.shape;
            sh.radius = 0.3f;
            return fx;
        }

        /// <summary>Canavar FBX malzemeleri Boss/Materials'a çıkarılır, URP Lit'e çevrilir (karpuz dokusu bağlanır).</summary>
        private static void ConfigureBossImport(string path)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return;
            EnsureFolder(BossMatDir);
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { BossMatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
            }
            imp.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is not Material m) continue;
                string dst = $"{BossMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var rind = AssetDatabase.LoadAssetAtPath<Texture2D>(KarpuzTexPath);
            (string name, string color, float glow, float smooth, float metal)[] defs =
            {
                ("K_Rind", "#FFFFFF", 0f, 0.55f, 0f), ("K_Stem", "#6B4A2A", 0f, 0.2f, 0f),
                ("T_Copper", "#C46A33", 0f, 0.72f, 0.9f), ("T_CopperDark", "#6E3418", 0f, 0.55f, 0.85f),
                ("M_Warn", "#FF3B30", 3.5f, 0.5f, 0f),
                ("B_Skin", "#C9875F", 0f, 0.35f, 0f), ("B_Cloth", "#5A2770", 0f, 0.15f, 0f), ("B_Eye", "#F4F1E8", 0f, 0.8f, 0f),
                ("B_Iris", "#C8261E", 2.5f, 0.7f, 0f), ("B_Rope", "#8B6B43", 0f, 0.1f, 0f),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{BossMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                Color c = Hex(d.color);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", d.metal);
                if (d.name == "K_Rind" && rind != null) mat.SetTexture("_BaseMap", rind);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", d.glow > 0f ? c * d.glow : Color.black);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
