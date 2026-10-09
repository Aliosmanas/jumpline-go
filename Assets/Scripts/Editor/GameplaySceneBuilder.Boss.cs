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
            var controller = SetupTepegozAnimations();
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();

            // --- canavar ---
            var root = new GameObject("Tepegoz");
            var boss = root.AddComponent<BossChase>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var tgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TepegozPath);
            if (tgPrefab == null) tgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TepegozBlockPath);   // model yoksa kaba gövde
            Renderer[] flash = new Renderer[0];
            if (tgPrefab != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(tgPrefab, visual);
                body.transform.localPosition = Vector3.zero;   // FBX eksen dönüşü korunur
                flash = body.GetComponentsInChildren<Renderer>();
                Bounds b = flash[0].bounds;
                foreach (var r in flash) b.Encapsulate(r.bounds);
                float h = Mathf.Max(0.1f, b.max.y - body.transform.position.y);
                body.transform.localScale *= TepegozHeight / h;
                foreach (var r in flash)
                {
                    r.shadowCastingMode = ShadowCastingMode.On;
                    if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; smr.quality = SkinQuality.Bone2; }
                }
                var anim = body.GetComponentInChildren<Animator>();
                if (anim == null) anim = body.AddComponent<Animator>();
                if (controller != null)
                {
                    anim.runtimeAnimatorController = controller;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    Set(boss, "animator", anim);
                }
                Transform hand = FindDeep(body.transform, "hand_r");
                if (hand != null) Set(boss, "throwHand", hand);
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
        private static RuntimeAnimatorController SetupTepegozAnimations()
        {
            if (AssetImporter.GetAtPath(TepegozPath) is not ModelImporter imp) return null;
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
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(TepegozPath))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) byName[clip.name] = clip;
            AssetDatabase.DeleteAsset(TepegozControllerPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(TepegozControllerPath);
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
            imp.importCameras = imp.importLights = false;
            imp.importAnimation = path == TepegozPath;   // canavarın animasyonları kalır
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
        }
    }
}
