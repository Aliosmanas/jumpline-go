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
        public const string TepegozPath = BossDir + "/Tepegoz.fbx";
        private const string TepegozControllerPath = BossDir + "/Tepegoz.controller";
        private const float TepegozHeight = 3.6f;   // oyundaki boy (m): koşucunun ~2 katı
        private static readonly string[] BossClips = { "Idle", "Run", "Roar", "Throw", "Hit", "Defeat" };
        public const string SahmeranPath = BossDir + "/Sahmeran.fbx";
        public const string HumbabaPath = BossDir + "/Humbaba.fbx";
        public const string StoneGuardianPath = BossDir + "/StoneGuardian.fbx";
        public const string VanMonsterPath = BossDir + "/VanMonster.fbx";
        public const string KarakoncolosPath = BossDir + "/Karakoncolos.fbx";
        public const string LamassuPath = BossDir + "/Lamassu.fbx";
        public const string DragonPath = BossDir + "/Dragon.fbx";
        /// <summary>Bölüm canavarları (efsane kimliği, model, canlandırıcı) — Blender/scripts/build_*.py.</summary>
        private static readonly (string legend, string path, string controller)[] BossModels =
        {
            ("tepegoz", TepegozPath, TepegozControllerPath),
            ("sahmeran", SahmeranPath, BossDir + "/Sahmeran.controller"),
            ("humbaba", HumbabaPath, BossDir + "/Humbaba.controller"),
            ("stone_guardian", StoneGuardianPath, BossDir + "/StoneGuardian.controller"),
            ("van_monster", VanMonsterPath, BossDir + "/VanMonster.controller"),
            ("karakoncolos", KarakoncolosPath, BossDir + "/Karakoncolos.controller"),
            ("lamassu", LamassuPath, BossDir + "/Lamassu.controller"),
            ("dragon", DragonPath, BossDir + "/Dragon.controller"),
        };
        private static readonly System.Collections.Generic.List<Jumpline.Level.LevelDirector.BossBody> bossBodies = new();
        private static readonly System.Collections.Generic.List<Image> bossIconImages = new();
        private const string KarpuzTexPath = BossDir + "/Karpuz_Albedo.png";

        public const string BridgePath = "Assets/Art/Environment/Bridge/OnGozluBridge.fbx";
        private const string BridgeMatDir = "Assets/Art/Environment/Bridge/Materials";
        private static readonly Vector3 ArenaOrigin = new Vector3(-400f, 0f, 0f);
        private const float BridgeSegment = 40f;   // build_bridge.py SEG
        private const int BridgeCopies = 7;

        private static Box hudTop, hudBot;
        private static GameObject[] chaseHidden;

        private static void BuildBossChase(Transform runner, Jumpline.CameraSystem.CameraFollow follow, WorldSwitcher switcher,
                                           ObstacleSpawner street, Portal up, Portal down, LevelFlowController flow)
        {
            ConfigureBossImport(ThrowablesPath);
            ConfigureBossImport(TepegozBlockPath);
            ConfigureBossImport(TepegozPath);
            ConfigureBossImport(SahmeranPath);
            ConfigureBossImport(HumbabaPath);
            ConfigureBossImport(StoneGuardianPath);
            ConfigureBossImport(VanMonsterPath);
            ConfigureBossImport(KarakoncolosPath);
            ConfigureBossImport(LamassuPath);
            ConfigureBossImport(DragonPath);
            var controller = SetupTepegozAnimations();
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();

            // --- canavar ---
            var root = new GameObject("Tepegoz");
            var boss = root.AddComponent<BossChase>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            // bölüm canavarları: hepsi Visual altında hazır, LevelDirector bölüme göre birini açar
            bossBodies.Clear();
            Renderer[] flash = new Renderer[0];
            foreach (var (legend, path, ctrlPath) in BossModels)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null && legend == "tepegoz") prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TepegozBlockPath);   // model yoksa kaba gövde
                if (prefab == null) continue;
                var ctrl = path == TepegozPath ? controller : SetupBossAnimations(path, ctrlPath);
                var body = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
                body.name = legend;
                body.transform.localPosition = Vector3.zero;   // FBX eksen dönüşü korunur
                var rends = body.GetComponentsInChildren<Renderer>();
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float h = Mathf.Max(0.1f, b.max.y - body.transform.position.y);
                body.transform.localScale *= TepegozHeight / h;
                foreach (var r in rends)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; smr.quality = SkinQuality.Bone2; }
                }
                var anim = body.GetComponentInChildren<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }
                Transform hand = FindDeep(body.transform, "hand_r");
                bossBodies.Add(new Jumpline.Level.LevelDirector.BossBody { legend = legend, root = body, animator = ctrl != null ? anim : null, hand = hand, renderers = rends, icon = legend == "tepegoz" ? Icons["boss_head"] : Portrait(legend) });
                if (bossBodies.Count == 1)   // varsayılan (bölüm 1): sahnede doğrudan bağlı
                {
                    flash = rends;
                    if (ctrl != null) Set(boss, "animator", anim);
                    if (hand != null) Set(boss, "throwHand", hand);
                }
                else body.SetActive(false);
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
            var land = MakeBurst(root.transform, "BossLandDust", Hex("#D8C3A0"), Hex("#9C8B74"), 2f, 5f, 0.3f);
            land.GetComponent<ParticleSystemRenderer>().sharedMaterial = SplashMaterial();
            land.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            var lm = land.main;
            lm.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.76f, 0.62f, 0.8f), new Color(0.6f, 0.53f, 0.45f, 0.6f));
            lm.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            lm.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            Set(boss, "landFx", land);

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
                var trail = holder.AddComponent<TrailRenderer>();   // uçarken kısa iz
                trail.time = 0.18f;
                trail.minVertexDistance = 0.2f;
                trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(1f, 0f));
                trail.sharedMaterial = FxMaterial();
                trail.colorGradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(child == "Karpuz" ? Hex("#9BE07A") : Hex("#FFB35C"), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) },
                };
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.emitting = true;
                holder.SetActive(false);
                return holder;
            }
            var melon = Template("Karpuz", 1.0f);
            var jug = Template("Testi", 1.0f);
            var so = new SerializedObject(throws);
            var kinds = so.FindProperty("kinds");
            var melonFx = MakeChunks(throwsGo.transform, "MelonSmashFx", Hex("#E8334A"), Hex("#2F7A35"));
            var jugFx = MakeBurst(throwsGo.transform, "JugSmashFx", Hex("#FFB35C"), Hex("#C46A33"), 3f, 9f, 1.2f);
            var defs = new (string id, GameObject t, string icon, float w, ParticleSystem fx)[]
                { ("karpuz", melon, "throw_melon", 0.6f, melonFx), ("testi", jug, "throw_jug", 0.4f, jugFx) };
            kinds.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var e = kinds.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = defs[i].id;
                e.FindPropertyRelative("template").objectReferenceValue = defs[i].t;
                e.FindPropertyRelative("icon").objectReferenceValue = Icons.TryGetValue(defs[i].icon, out var sp) ? sp : null;
                e.FindPropertyRelative("weight").floatValue = defs[i].w;
                e.FindPropertyRelative("smashFx").objectReferenceValue = defs[i].fx;
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
            Set(flow, "throws", throws);

            // --- final: On Gözlü Köprü arenası + düşüş sinematiği ---
            Transform arena = BuildBridgeArena(runner);
            Set(switcher, "arenaRoot", arena);
            SetLook(switcher, "arenaLook", "#F2B98A", "#E9B48C", 45f, 240f, "#E6ECF7", "#F0D6C0", "#A48A76", "#FFC48A", 2.2f,
                    new Vector3(34f, -50f, 0f), true);
            var finaleGo = new GameObject("BossFinale");
            var finale = finaleGo.AddComponent<BossFinale>();
            Set(finale, "boss", boss);
            Set(finale, "player", player);
            Set(finale, "switcher", switcher);
            Set(finale, "cameraFollow", follow);
            Set(finale, "throws", throws);
            var splash = MakeBurst(finaleGo.transform, "SplashFx", Hex("#FFFFFF"), Hex("#9FE3FF"), 6f, 15f, 1.6f);
            splash.GetComponent<ParticleSystemRenderer>().sharedMaterial = SplashMaterial();
            splash.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
            var sm = splash.main;
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.95f), new Color(0.75f, 0.92f, 1f, 0.85f));
            sm.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            sm.maxParticles = 600;
            var ssh = splash.shape;
            ssh.shapeType = ParticleSystemShapeType.Cone;
            ssh.angle = 25f;
            ssh.radius = 1.5f;
            splash.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            Set(finale, "splashFx", splash);
            SetArray(finale, "hideDuringCinematic", new Object[] { hudTop.rt.parent.gameObject, GameObject.Find("BossHud") });
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
            bossIconImages.Clear();
            SkinImage(top, "BossMedallion", new Rect(24f, 196f, 132f, 132f), "medallion");   // plakanın altında, sol üst (hud_1.jpg)
            bossIconImages.Add(Icon(top, "boss_head", new Rect(42f, 214f, 96f, 96f), Color.white));
            var hearts = new Image[6];   // bölüme göre 4-6 kalp (LevelSpec.bossHearts); fazlası gizlenir
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Icon(top, "heart", new Rect(166f + i * 66f, 214f, 62f, 62f), Color.white);

            // mesafe: koşucu (sol) ←→ canavar; dolgu uzadıkça kırmızılaşır
            const float mx = 176f, my = 300f, mw = 470f, mh = 26f;
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
            bossIconImages.Add(marker);
            var warn = Txt(top, "GapWarning", "ESCAPING!", new Rect(mx, my + 34f, mw, 54f), fExtra, 40f, Hex("#FF4B3E"));
            warn.gameObject.SetActive(false);

            Material outlined = OutlinedFont();
            var announce = Txt(mid, "BossAnnounce", "", new Rect(60f, 540f, 996f, 160f), fExtra, 58f, Hex("#FFE08A"), TextAlignmentOptions.Center, outlined, localize: false, wrap: true);
            announce.enableAutoSizing = true;
            announce.fontSizeMin = 36f;
            announce.fontSizeMax = 58f;

            // atma düğmesi: bakır halkalı yuvarlak, içinde nesne, altında "AT", sayısı köşede
            var btnRoot = Add(bot, "ThrowButton", new Rect(826f, 1592f, 250f, 250f));
            btnRoot.go.AddComponent<Jumpline.UI.HandednessMirror>();   // solak modu: sola
            var group = btnRoot.go.AddComponent<CanvasGroup>();
            // referans: bakır halkalı safran yuvarlak düğme (hud_1.jpg)
            var ringImg = SkinImage(btnRoot, "Ring", new Rect(826f, 1592f, 250f, 250f), "round_saffron", true);
            var ring = new Box(ringImg.rectTransform, new Rect(826f, 1592f, 250f, 250f));
            var faceImg = SkinImage(btnRoot, "Face", new Rect(856f, 1620f, 190f, 190f), "medallion");
            faceImg.color = new Color(1f, 1f, 1f, 0.55f);
            var icon = Icon(btnRoot, "throw_melon", new Rect(871f, 1622f, 160f, 160f), Color.white);
            Txt(btnRoot, "ThrowLabel", "THROW", new Rect(826f, 1764f, 250f, 56f), fExtra, 40f, Color.white, TextAlignmentOptions.Center, outlined);
            var badgeImg = SkinImage(btnRoot, "CountBadge", new Rect(1004f, 1576f, 90f, 90f), "badge_red");
            var badge = new Box(badgeImg.rectTransform, new Rect(1004f, 1576f, 90f, 90f));
            var count = Txt(badge, "Count", "x2", new Rect(1010f, 1582f, 78f, 78f), fExtra, 34f, Color.white, localize: false);
            foreach (var g in btnRoot.go.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;   // süs katmanları dokunuşu yutmasın
            var hit = ring.go.GetComponent<Image>();
            hit.raycastTarget = true;   // tek dokunuş hedefi: halka (düğmenin tamamını kaplar)
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

        /// <summary>Karpuz parçaları: iri kırmızı/yeşil kütleler (alfa, dönen), yerçekimiyle düşer.</summary>
        private static ParticleSystem MakeChunks(Transform parent, string name, Color flesh, Color rind)
        {
            var fx = MakeBurst(parent, name, flesh, rind, 3f, 8f, 2.2f);
            var r = fx.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = SplashMaterial();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var m = fx.main;
            m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.38f);
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var rot = fx.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            return fx;
        }

        /// <summary>Su sıçraması: yumuşak beyaz damla/köpük (alfa karışımlı, suyun üstünde görünür).</summary>
        private static Material SplashMaterial()
        {
            const string path = "Assets/Art/Materials/FxSplash.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);   // alfa
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex != null) mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Köprü arenası: 40 m'lik parçalar sonsuz döngüde (StreetRecycler). Başta kapalı; WorldSwitcher.EnterArena açar.</summary>
        private static Transform BuildBridgeArena(Transform runner)
        {
            ConfigureBridgeImport();
            var root = new GameObject("BridgeArena").transform;
            root.position = ArenaOrigin;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BridgePath);
            if (prefab != null)
            {
                for (int i = 0; i < BridgeCopies; i++)
                {
                    var seg = new GameObject("BridgeSeg_" + i).transform;
                    seg.SetParent(root, false);
                    seg.localPosition = new Vector3(0f, 0f, (i - 1) * BridgeSegment);
                    var vis = (GameObject)PrefabUtility.InstantiatePrefab(prefab, seg);
                    vis.transform.localPosition = Vector3.zero;
                    foreach (var r in vis.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;
                    DressBridgeSegment(seg, i);   // Meshy kayık + komşu köprü gözü
                }
            }
            var rec = root.gameObject.AddComponent<StreetRecycler>();
            Set(rec, "player", runner);
            SetFloat(rec, "segmentLength", BridgeSegment);
            root.gameObject.SetActive(false);
            return root;
        }

        private static void ConfigureBridgeImport()
        {
            if (AssetImporter.GetAtPath(BridgePath) is not ModelImporter imp) return;
            EnsureFolder(BridgeMatDir);
            imp.importCameras = imp.importLights = imp.importAnimation = false;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { BridgeMatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
            }
            imp.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(BridgePath))
            {
                if (o is not Material m) continue;
                string dst = $"{BridgeMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(BridgePath);
            AssetDatabase.ImportAsset(BridgePath, ImportAssetOptions.ForceUpdate);
            var basalt = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/Diyarbakir/DiyarbakirStreet.fbm/basalt.png");
            (string name, string color, float smooth, bool tex)[] defs =
            {
                ("BR_Stone", "#B2A79C", 0.12f, true), ("BR_Light", "#E2D3BE", 0.15f, true), ("BR_Deck", "#D2C7BB", 0.2f, true),
                ("BR_Cap", "#CFC2B2", 0.18f, true), ("BR_Water", "#2E6A6E", 0.92f, false), ("BR_Earth", "#9A774E", 0.05f, false),
                ("BR_Grass", "#6E8540", 0.05f, false), ("BR_Wall", "#7A6F66", 0.1f, true),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{BridgeMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetColor("_BaseColor", Hex(d.color));
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", 0f);
                if (d.tex && basalt != null) mat.SetTexture("_BaseMap", basalt);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>Tepegoz.fbx klipleri (Idle/Run döngü) + tetikleyicili controller (BossChase.Anim adları).</summary>
        private static RuntimeAnimatorController SetupTepegozAnimations() => SetupBossAnimations(TepegozPath, TepegozControllerPath);

        private static RuntimeAnimatorController SetupBossAnimations(string modelPath, string controllerPath)
        {
            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter imp) return null;
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            var clips = new System.Collections.Generic.List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation c in imp.defaultClipAnimations)
            {
                string shortName = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                if (System.Array.IndexOf(BossClips, shortName) < 0) continue;
                c.name = shortName;
                c.loopTime = shortName == "Run" || shortName == "Idle";
                clips.Add(c);
            }
            imp.clipAnimations = clips.ToArray();
            imp.SaveAndReimport();
            var byName = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) byName[clip.name] = clip;
            AssetDatabase.DeleteAsset(controllerPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm = ctrl.layers[0].stateMachine;
            var states = new System.Collections.Generic.Dictionary<string, UnityEditor.Animations.AnimatorState>();
            foreach (string n in BossClips)
            {
                if (!byName.TryGetValue(n, out var clip)) continue;
                var st = sm.AddState(n);
                st.motion = clip;
                states[n] = st;
                if (n == "Roar") sm.defaultState = st;   // giriş: kükreyerek başlar
            }
            foreach (string trig in new[] { "Run", "Roar", "Throw", "Hit", "Defeat" })
            {
                ctrl.AddParameter(trig, AnimatorControllerParameterType.Trigger);
                if (!states.TryGetValue(trig, out var st)) continue;
                var t = sm.AddAnyStateTransition(st);
                t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, trig);
                t.duration = trig == "Hit" ? 0.06f : 0.15f;
                t.canTransitionToSelf = trig == "Hit";
            }
            ctrl.AddParameter(new AnimatorControllerParameter { name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            if (states.TryGetValue("Run", out var run))
            {
                run.speedParameterActive = true;
                run.speedParameter = "RunSpeed";
                foreach (string back in new[] { "Roar", "Throw", "Hit" })
                {
                    if (!states.TryGetValue(back, out var a)) continue;
                    var t = a.AddTransition(run);
                    t.hasExitTime = true;
                    t.exitTime = 0.92f;
                    t.duration = 0.18f;
                }
            }
            AssetDatabase.SaveAssets();
            return ctrl;
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
            mat.shader = fExtra.material.shader;
            mat.mainTexture = fExtra.material.mainTexture;
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
            MeshyImport.Prepare(path);
            imp.importCameras = imp.importLights = false;
            imp.importAnimation = path == TepegozPath || path == SahmeranPath || path == HumbabaPath || path == StoneGuardianPath || path == VanMonsterPath || path == KarakoncolosPath || path == LamassuPath || path == DragonPath;   // canavarların animasyonları kalır
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
                ("TG_Skin", "#FFFFFF", 0f, 0.38f, 0f), ("TG_Eye", "#F2EEE4", 0f, 0.85f, 0f), ("TG_Iris", "#E0601F", 1.8f, 0.8f, 0f),
                ("TG_Pupil", "#120808", 0f, 0.9f, 0f), ("TG_Cloth", "#FFFFFF", 0f, 0.12f, 0f), ("TG_Rope", "#6E5236", 0f, 0.1f, 0f),
                ("TG_Brass", "#C9A04A", 0f, 0.65f, 0.9f),
                ("SM_Skin", "#D9A47E", 0f, 0.42f, 0f), ("SM_Scales", "#FFFFFF", 0f, 0.55f, 0.35f), ("SM_Belly", "#E3C77A", 0f, 0.4f, 0f),
                ("SM_Hair", "#16100C", 0f, 0.3f, 0f), ("SM_Gold", "#E2B24A", 0f, 0.75f, 1f), ("SM_Gem", "#2ED18A", 2f, 0.9f, 0f),
                ("SM_Eye", "#F2EEE4", 0f, 0.85f, 0f), ("SM_Iris", "#C9B12A", 1.2f, 0.8f, 0f),
                ("HB_Skin", "#FFFFFF", 0f, 0.3f, 0f), ("HB_Eye", "#E9E2C8", 0f, 0.8f, 0f), ("HB_Iris", "#FFB21E", 2.2f, 0.8f, 0f),
                ("HB_Mane", "#7A3A16", 0f, 0.15f, 0f), ("HB_Horn", "#E6DABB", 0f, 0.55f, 0f), ("HB_Bark", "#3E2E1C", 0f, 0.1f, 0f),
                ("SG_Skin", "#FFFFFF", 0f, 0.1f, 0f), ("SG_Capital", "#FFFFFF", 0f, 0.1f, 0f), ("SG_Dark", "#8C8170", 0f, 0.1f, 0f),
                ("SG_Glow", "#38E0D0", 3f, 0.7f, 0f),
                ("VM_Scales", "#FFFFFF", 0f, 0.4f, 0f), ("VM_Belly", "#5E8C7A", 0f, 0.45f, 0f), ("VM_Bone", "#E8E0C6", 0f, 0.6f, 0f),
                ("VM_Eye", "#C8FF3A", 3f, 0.8f, 0f), ("VM_Spine", "#1E7A6E", 0.6f, 0.65f, 0f),
                ("KK_Skin", "#5A5560", 0f, 0.2f, 0f), ("KK_Fur", "#2A211C", 0f, 0.05f, 0f), ("KK_Eye", "#7FE8FF", 3.5f, 0.8f, 0f),
                ("KK_Horn", "#141012", 0f, 0.65f, 0f), ("KK_Ice", "#CFF4FF", 0.6f, 0.9f, 0f),
                ("LM_Skin", "#B98A62", 0f, 0.4f, 0f), ("LM_Eye", "#F2EEE4", 0f, 0.8f, 0f), ("LM_Lapis", "#1F3E8C", 0.3f, 0.65f, 0.2f),
                ("LM_Gold", "#E2B24A", 0f, 0.75f, 1f), ("LM_Hair", "#1A1210", 0f, 0.3f, 0f), ("LM_Wing", "#D9C7A0", 0f, 0.4f, 0f),
                ("LM_Hoof", "#2A2420", 0f, 0.5f, 0f),
                ("DR_Scales", "#FFFFFF", 0f, 0.55f, 0.1f), ("DR_Belly", "#E0A23A", 0f, 0.5f, 0f), ("DR_Bone", "#F0E6CC", 0f, 0.6f, 0f),
                ("DR_Eye", "#FF8A1E", 4f, 0.8f, 0f), ("DR_Spine", "#FF5A1E", 1.2f, 0.65f, 0f), ("DR_Gold", "#E2B24A", 0f, 0.75f, 1f),
                ("DR_Wing", "#7A1A12", 0f, 0.4f, 0f),
            };
            var skinTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/Tepegoz_Skin.png");
            var clothTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/Pestemal.png");
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
                if (d.name == "TG_Skin" && skinTex != null) mat.SetTexture("_BaseMap", skinTex);
                if (d.name == "SM_Scales") mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/Sahmeran_Scales.png"));
                if (d.name == "SM_Hair") mat.SetFloat("_Cull", 0f);   // tek katman saç perdesi
                if (d.name == "HB_Skin") mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/Humbaba_Skin.png"));
                if (d.name == "HB_Bark" || d.name == "LM_Wing" || d.name == "LM_Lapis" || d.name == "DR_Wing") mat.SetFloat("_Cull", 0f);
                if (d.name == "DR_Scales") mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/Dragon_Scales.png"));
                if (d.name == "VM_Scales") mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/VanMonster_Scales.png"));
                if (d.name == "SG_Skin" || d.name == "SG_Capital") mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BossDir + "/StoneGuardian_Skin.png"));
                if (d.name == "TG_Cloth" && clothTex != null)
                {
                    mat.SetTexture("_BaseMap", clothTex);
                    mat.SetFloat("_Cull", 0f);   // peştemalin iç yüzü de görünsün
                }
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", d.glow > 0f ? c * d.glow : Color.black);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
            MeshyImport.Tune(BossMatDir);
        }
    }
}
