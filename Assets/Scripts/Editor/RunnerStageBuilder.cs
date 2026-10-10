using System.Collections.Generic;
using System.IO;
using Jumpline.Player;
using Jumpline.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Koşucu modeli (Blender/scripts/build_runner.py çıktısı) için: içe aktarma ayarları, kostüm kütüphanesi,
    /// menüdeki 3B vitrin (kamera → RenderTexture) ve kostüm kart görselleri.
    /// </summary>
    public static class RunnerStageBuilder
    {
        public const string FbxPath = "Assets/Art/Characters/Runner/Runner.fbx";
        private const string MaterialDir = "Assets/Art/Characters/Runner/Materials";
        private const string PreviewPath = "Assets/Art/Characters/Runner/RunnerPreview.renderTexture";
        private const string LibraryPath = "Assets/Settings/SkinLibrary.asset";
        private const string ThumbDir = UiDir + "/SkinThumbs";
        private const int PreviewSize = 1024;
        private static readonly Vector3 StagePosition = new Vector3(0f, -200f, 0f);
        private const float DisplayYaw = 0f; // kameraya dönük (model +Z'ye bakar, kamera +Z'de)

        public static SkinLibrary EnsureLibrary()
        {
            var lib = GetOrCreateAsset<SkinLibrary>(LibraryPath, null);
            lib.SetSkins(DefaultSkins());
            lib.SetGear(DefaultGear());
            EditorUtility.SetDirty(lib);
            return lib;
        }

        /// <summary>Humanoid + malzemeleri düzenlenebilir hale getir + ışık saçan slotlar için emisyon.</summary>
        public static void ConfigureImport()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.humanDescription = BuildHumanDescription(imp.humanDescription);
            imp.SaveAndReimport();

            EnsureFolder(MaterialDir);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (o is not Material m) continue;
                string dst = $"{MaterialDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(FbxPath);
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

            Texture2D carbon = MakeCarbonTexture();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialDir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (!mat.name.StartsWith("M_Glow")) mat.SetColor("_EmissionColor", Color.black);
                if (mat.name.StartsWith("M_Primary"))
                {
                    // Bordo karbon fiber dokusu (renk kostümden _BaseColor ile gelir)
                    mat.SetTexture("_BaseMap", carbon);
                    mat.SetTextureScale("_BaseMap", new Vector2(CarbonTiling, CarbonTiling));
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        private const float CarbonTiling = 70f;
        private const string CarbonPath = "Assets/Art/Characters/Runner/CarbonFiber.png";

        /// <summary>2x2 dimi örgü karbon fiber deseni (gri tonlu; renk malzemeden çarpılır).</summary>
        private static Texture2D MakeCarbonTexture()
        {
            const int size = 64, cell = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int cx = x / cell, cy = y / cell;
                bool warp = ((cx + cy) / 2) % 2 == 0;   // diyagonal (dimi) örgü
                float u = (x % cell + 0.5f) / cell, v = (y % cell + 0.5f) / cell;
                float along = warp ? v : u;             // ipliğin uzunlamasına parlaması
                float across = warp ? u : v;
                float shade = 0.72f + 0.28f * Mathf.Sin(across * Mathf.PI) * (0.75f + 0.25f * Mathf.Sin(along * Mathf.PI));
                if (!warp) shade *= 0.88f;
                tex.SetPixel(x, y, new Color(shade, shade, shade));
            }
            tex.Apply();
            File.WriteAllBytes(CarbonPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CarbonPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(CarbonPath);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CarbonPath);
        }

        /// <summary>MPFB "game_engine" iskeleti → Unity Humanoid kemik eşlemesi.</summary>
        internal static HumanDescription BuildHumanDescription(HumanDescription desc)
        {
            var map = new Dictionary<string, string>
            {
                { "Hips", "pelvis" }, { "Spine", "spine_01" }, { "Chest", "spine_02" }, { "UpperChest", "spine_03" },
                { "Neck", "neck_01" }, { "Head", "head" },
            };
            foreach ((string side, string s) in new[] { ("Left", "l"), ("Right", "r") })
            {
                map[$"{side}Shoulder"] = $"clavicle_{s}";
                map[$"{side}UpperArm"] = $"upperarm_{s}";
                map[$"{side}LowerArm"] = $"lowerarm_{s}";
                map[$"{side}Hand"] = $"hand_{s}";
                map[$"{side}UpperLeg"] = $"thigh_{s}";
                map[$"{side}LowerLeg"] = $"calf_{s}";
                map[$"{side}Foot"] = $"foot_{s}";
                map[$"{side}Toes"] = $"ball_{s}";
                foreach ((string finger, string mpfb) in new[] { ("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"), ("Ring", "ring"), ("Little", "pinky") })
                {
                    map[$"{side} {finger} Proximal"] = $"{mpfb}_01_{s}";
                    map[$"{side} {finger} Intermediate"] = $"{mpfb}_02_{s}";
                    map[$"{side} {finger} Distal"] = $"{mpfb}_03_{s}";
                }
            }

            var human = new List<HumanBone>();
            foreach (string traitName in HumanTrait.BoneName)
            {
                if (!map.TryGetValue(traitName, out string bone)) continue;
                var hb = new HumanBone { humanName = traitName, boneName = bone };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var skeleton = new List<SkeletonBone>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                skeleton.Add(new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });

            desc.human = human.ToArray();
            desc.skeleton = skeleton.ToArray();
            desc.hasTranslationDoF = false;
            return desc;
        }

        // ---------------------------------------------------------------------
        // Animasyonlar — Blender/scripts/build_anims.py (Desktop/Yeni klasör animasyon videoları)
        // ---------------------------------------------------------------------
        public const string MenuControllerPath = "Assets/Art/Characters/Runner/RunnerMenu.controller";
        public const string GameControllerPath = "Assets/Art/Characters/Runner/RunnerGame.controller";
        private static readonly string[] LoopClips = { "Idle", "Run", "Zipline" };
        private static readonly string[] AllClips = { "Idle", "Run", "Crouch", "Jump", "Fall", "Land", "Zipline", "Death", "Slide" };

        /// <summary>FBX'teki action'ları adlandırır, döngüleri ayarlar, menü (Idle) ve oyun (Run) controller'larını kurar.</summary>
        public static void SetupAnimations()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation c in imp.defaultClipAnimations)
            {
                string shortName = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
                if (System.Array.IndexOf(AllClips, shortName) < 0) continue;
                c.name = shortName;
                c.loopTime = System.Array.IndexOf(LoopClips, shortName) >= 0;
                c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                clips.Add(c);
            }
            imp.clipAnimations = clips.ToArray();
            imp.SaveAndReimport();

            var byName = new Dictionary<string, AnimationClip>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview")) byName[clip.name] = clip;

            BuildController(MenuControllerPath, byName, "Idle");
            BuildController(GameControllerPath, byName, "Run");
        }

        private static void BuildController(string path, Dictionary<string, AnimationClip> clips, string defaultState)
        {
            AssetDatabase.DeleteAsset(path);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ctrl.layers[0].stateMachine;
            foreach (string name in AllClips)
            {
                if (!clips.TryGetValue(name, out AnimationClip clip)) continue;
                var st = sm.AddState(name);
                st.motion = clip;
                if (name == defaultState) sm.defaultState = st;
            }
            // Oyun akışı için tetikleyiciler (oynanış kodu bunları çağıracak)
            foreach (string trig in new[] { "Crouch", "Jump", "Fall", "Land", "Zipline", "Death", "Run", "Idle", "Slide" })
                ctrl.AddParameter(trig, AnimatorControllerParameterType.Trigger);
            // kayma: PlayerController klibi kayma süresine eşitler (SlideSpeed = klip uzunluğu / kayma süresi)
            ctrl.AddParameter(new AnimatorControllerParameter { name = "SlideSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            var slideState = System.Array.Find(sm.states, x => x.state.name == "Slide").state;
            if (slideState != null)
            {
                slideState.speedParameterActive = true;
                slideState.speedParameter = "SlideSpeed";
            }
            foreach (var st in sm.states)
            {
                var t = sm.AddAnyStateTransition(st.state);
                t.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0, st.state.name);
                t.duration = 0.12f;
                t.canTransitionToSelf = false;
            }
            // Land → Run, Jump → Fall otomatik
            void Auto(string from, string to)
            {
                var a = System.Array.Find(sm.states, x => x.state.name == from).state;
                var b = System.Array.Find(sm.states, x => x.state.name == to).state;
                if (a == null || b == null) return;
                var t = a.AddTransition(b);
                t.hasExitTime = true;
                t.exitTime = 0.95f;
                t.duration = 0.1f;
            }
            Auto("Land", defaultState);
            Auto("Jump", "Fall");
            AssetDatabase.SaveAssets();
        }

        /// <summary>Açık sahneye vitrin kurar. Vitrin uzakta (y=-200) durur, menü kamerası görmez.</summary>
        public static RenderTexture BuildStage(SkinLibrary library, out Transform stage, out RunnerSkin runner, out Camera camera)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(PreviewPath);
            if (rt == null)
            {
                rt = new RenderTexture(PreviewSize, PreviewSize, 24, RenderTextureFormat.ARGB32) { name = "RunnerPreview", antiAliasing = 2 };
                AssetDatabase.CreateAsset(rt, PreviewPath);
            }

            stage = new GameObject("RunnerStage").transform;
            stage.position = StagePosition;

            camera = new GameObject("StageCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(stage, false);
            camera.transform.localPosition = new Vector3(0f, 1.0f, 4.3f);
            camera.transform.LookAt(stage.position + new Vector3(0f, 0.88f, 0f));
            stageRoot = stage;
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
            if (rt.antiAliasing > 2) { rt.Release(); rt.antiAliasing = 2; EditorUtility.SetDirty(rt); }   // mobil: 4x MSAA hem pahalı hem bazı GPU'larda kara
            camera.targetTexture = rt;
            camera.depth = -10f;
            var camData = camera.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;

            Light("KeyLight", stage, LightType.Directional, new Vector3(0f, 0f, 0f), Quaternion.Euler(30f, 200f, 0f), Hex("#FFF4EA"), 1.3f, 0f);
            Light("RimBlue", stage, LightType.Point, new Vector3(1.3f, 1.7f, -1.3f), Quaternion.identity, Hex("#4F8CFF"), 8f, 5f);
            Light("RimPink", stage, LightType.Point, new Vector3(-1.3f, 1.4f, -1.1f), Quaternion.identity, Hex("#FF4F8A"), 6f, 5f);
            Light("Fill", stage, LightType.Point, new Vector3(0f, 0.6f, 2.5f), Quaternion.identity, Hex("#9AB0FF"), 1.2f, 6f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage);
            model.name = "Runner";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            if (model.TryGetComponent(out Animator anim)) anim.enabled = false; // kartlar statik duruşla çekilir; FinishStage açar

            ApplyDisplayPose(model.transform);
            GroundModel(model.transform, stage.position.y);
            model.transform.localRotation = Quaternion.Euler(0f, DisplayYaw, 0f);

            runner = model.AddComponent<RunnerSkin>();
            Set(runner, "library", library);
            runner.Apply(library.Find("rookie"));

            var display = stage.gameObject.AddComponent<RunnerDisplay>();
            Set(display, "runner", runner);
            Set(display, "library", library);
            return rt;
        }

        // ---------------------------------------------------------------------
        // Podyum (docs/references/menu/podium.jpg) ve kahraman duruşu (market_v2.jpg)
        // ---------------------------------------------------------------------

        public const string PodiumPath = "Assets/Art/Environment/Podium/Podium.fbx";
        private const string PodiumMatDir = "Assets/Art/Environment/Podium/Materials";
        private const float PodiumTop = 0.165f;   // Blender/scripts/build_podium.py: BASE_H + MID_H + TOP_H
        private static Transform stageRoot;

        /// <summary>
        /// Kostüm kartları çekildikten sonra çağrılır: karakteri podyuma çıkarır, kahraman duruşu verir,
        /// ışık hüzmesi ve parçacıkları ekler, kamerayı podyumu da görecek şekilde ayarlar.
        /// </summary>
        public static void FinishStage(RunnerSkin runner, Camera camera)
        {
            Transform stage = stageRoot;
            var podium = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PodiumPath), stage);
            podium.name = "Podium";
            podium.transform.localPosition = Vector3.zero;
            podium.transform.localRotation = Quaternion.identity;
            SetupPodiumMaterials(podium);
            AddSparks(stage);
            Light("PodiumGlow", stage, LightType.Point, new Vector3(0f, 0.35f, 0f), Quaternion.identity, Hex("#7FC0FF"), 3f, 2.2f);

            Transform model = runner.transform;
            model.localRotation = Quaternion.identity;
            if (model.TryGetComponent(out Animator menuAnim))
            {
                // Menüde nefes alan bekleme animasyonu (docs: karakter nefes alma animasyonu)
                menuAnim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MenuControllerPath);
                menuAnim.applyRootMotion = false;
                menuAnim.updateMode = AnimatorUpdateMode.UnscaledTime;
                menuAnim.enabled = true;
            }
            ApplyHeroPose(model);
            GroundModel(model, stage.position.y + PodiumTop);
            model.localRotation = Quaternion.Euler(0f, DisplayYaw, 0f);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true; // editörde de pozu göster

            camera.transform.localPosition = new Vector3(0f, 1.2f, 5.0f);
            camera.transform.LookAt(stage.position + new Vector3(0f, 1.0f, 0f));
            camera.fieldOfView = 30f;
        }

        private static void SetupPodiumMaterials(GameObject podium)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(PodiumPath);
            EnsureFolder(PodiumMatDir);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(PodiumPath))
            {
                if (o is not Material m) continue;
                string dst = $"{PodiumMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(PodiumPath);
            AssetDatabase.ImportAsset(PodiumPath, ImportAssetOptions.ForceUpdate);

            void Lit(string name, string color, float metal, float smooth, string emission = null, float intensity = 0f)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PodiumMatDir}/{name}.mat");
                if (mat == null) return;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                mat.SetColor("_BaseColor", Hex(color));
                mat.SetFloat("_Metallic", metal);
                mat.SetFloat("_Smoothness", smooth);
                if (emission != null)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    mat.SetColor("_EmissionColor", Hex(emission) * intensity);
                }
                EditorUtility.SetDirty(mat);
            }
            Lit("P_Metal", "#3A4052", 0.9f, 0.72f);
            Lit("P_MetalDark", "#1C2030", 0.85f, 0.6f);
            Lit("P_Top", "#4A5470", 0.6f, 0.88f, "#5A8CFF", 0.35f);
            Lit("P_Glow", "#B8DCFF", 0f, 0.7f, "#7FC0FF", 5f);

            // Işık hüzmesi: aşağıdan yukarı sönen, kenarları yumuşak, toplamalı şeffaf
            var beam = AssetDatabase.LoadAssetAtPath<Material>($"{PodiumMatDir}/P_Beam.mat");
            if (beam != null)
            {
                beam.shader = Shader.Find("Universal Render Pipeline/Unlit");
                beam.SetFloat("_Surface", 1f);          // Transparent
                beam.SetFloat("_Blend", 2f);            // Additive
                beam.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                beam.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                beam.SetFloat("_ZWrite", 0f);
                beam.SetFloat("_Cull", 0f);             // iki yüz
                beam.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                beam.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                beam.SetTexture("_BaseMap", MakeBeamTexture());
                beam.SetColor("_BaseColor", new Color(0.55f, 0.75f, 1f, 0.5f));
                EditorUtility.SetDirty(beam);
            }
            AssetDatabase.SaveAssets();
            foreach (var r in podium.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Texture2D MakeBeamTexture()
        {
            const string path = "Assets/Art/Environment/Podium/BeamGradient.png";
            const int w = 64, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = y / (h - 1f);                       // 0 alt → 1 üst
                float fade = Mathf.Pow(1f - v, 1.8f);
                float streak = 0.75f + 0.25f * Mathf.Sin(x / (float)w * Mathf.PI * 2f * 12f); // ince dikey çizgiler
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, fade * streak));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Podyumdan yavaşça yükselen ışık zerreleri.</summary>
        private static void AddSparks(Transform stage)
        {
            var go = new GameObject("PodiumSparks", typeof(ParticleSystem));
            go.transform.SetParent(stage, false);
            go.transform.localPosition = new Vector3(0f, PodiumTop, 0f);
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.GetComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.startColor = new ParticleSystem.MinMaxGradient(Hex("#9FD0FF"), Hex("#FFFFFF"));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 120;
            main.useUnscaledTime = true;
            var emission = ps.emission;
            emission.rateOverTime = 22f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.46f;
            shape.radiusThickness = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{PodiumMatDir}/P_Spark.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "P_Spark" };
                AssetDatabase.CreateAsset(mat, $"{PodiumMatDir}/P_Spark.mat");
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{SpriteDir}/SoftCircle.png"));
            EditorUtility.SetDirty(mat);
            r.sharedMaterial = mat;
        }

        /// <summary>
        /// docs/references/menu/market_v2.jpg: sol diz yukarıda, sağ bacak üstünde durur; sağ yumruk göğüs hizasında,
        /// sol kol aşağıda. Model +Z'ye (kameraya) bakar, sol taraf -X.
        /// </summary>
        public static void ApplyHeroPose(Transform root)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return;

            void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 dir)
            {
                Transform b = animator.GetBoneTransform(bone), c = animator.GetBoneTransform(child);
                if (b == null || c == null) return;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            Aim(HumanBodyBones.Spine, HumanBodyBones.Chest, new Vector3(0.03f, 1f, 0.06f));
            Aim(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, new Vector3(0.05f, -1f, 0.02f));
            Aim(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, new Vector3(0.02f, -1f, -0.02f));
            Aim(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, new Vector3(-0.1f, -0.42f, 0.9f));
            Aim(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, new Vector3(0.04f, -0.96f, -0.28f));
            Aim(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, new Vector3(0f, -0.55f, 0.83f));
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0.3f, -0.88f, 0.36f));
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(-0.35f, 0.45f, 0.82f));
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-0.35f, -0.9f, -0.1f));
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(-0.12f, -0.75f, 0.65f));
        }

        private static void Light(string name, Transform parent, LightType type, Vector3 pos, Quaternion rot, Color color, float intensity, float range)
        {
            var l = new GameObject(name, typeof(Light)).GetComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            l.transform.localRotation = rot;
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            if (type == LightType.Point) l.range = range;
            l.shadows = type == LightType.Directional ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>
        /// Menü vitrini için dik duruş (A-poz iskeletten). Model +Z'ye bakar, sol taraf -X.
        /// Animasyonlar gelince bu duruş yerini onlara bırakır.
        /// </summary>
        public static void ApplyDisplayPose(Transform root)
        {
            // Humanoid avatar üzerinden: kemik adları iskelete göre değişse de çalışır.
            var animator = root.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("[Jumpline] Koşucu Humanoid değil; poz uygulanmadı.");
                return;
            }

            void Aim(HumanBodyBones bone, HumanBodyBones child, Vector3 dir)
            {
                Transform b = animator.GetBoneTransform(bone), c = animator.GetBoneTransform(child);
                if (b == null || c == null) return;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            // Vitrin duruşu: dik, kollar yanda hafif bükük, ayaklar omuz genişliğinde.
            // (Koşu / zıplama animasyonları gelince Animator bu duruşun yerini alır.)
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, new Vector3(-0.2f, -0.98f, 0.02f));
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, new Vector3(-0.06f, -0.96f, 0.26f));
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, new Vector3(0.2f, -0.98f, 0.02f));
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, new Vector3(0.06f, -0.96f, 0.26f));
            Aim(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, new Vector3(-0.06f, -1f, 0f));
            Aim(HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, new Vector3(-0.02f, -1f, 0f));
            Aim(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, new Vector3(0.06f, -1f, 0f));
            Aim(HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, new Vector3(0.02f, -1f, 0f));
        }

        private static void GroundModel(Transform model, float groundY)
        {
            float minY = float.MaxValue;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name == "Runner_Feet") continue; // bot takılıyken gizli
                var mesh = new Mesh();
                smr.BakeMesh(mesh, true);
                foreach (Vector3 v in mesh.vertices) minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
                Object.DestroyImmediate(mesh);
            }
            model.position += new Vector3(0f, groundY - minY, 0f);
        }

        /// <summary>Her kostümü vitrinden 512x768 şeffaf PNG olarak çeker (market / özelleştirme kartları).</summary>
        public static Dictionary<string, Sprite> RenderThumbnails(SkinLibrary library, RunnerSkin runner, Camera camera)
        {
            EnsureFolder(ThumbDir);
            var result = new Dictionary<string, Sprite>();
            RenderTexture previous = camera.targetTexture;
            var rt = new RenderTexture(512, 768, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            float fov = camera.fieldOfView;
            camera.targetTexture = rt;
            camera.fieldOfView = 26f; // kostüm kartı: karakter kareyi doldursun
            Quaternion rot = camera.transform.rotation;
            camera.transform.LookAt(runner.transform.position + new Vector3(0f, 0.95f, 0f));
            foreach (var r in runner.GetComponentsInChildren<Renderer>(true)) r.enabled = r.name != "Runner_Feet"; // tam ekipmanlı
            try
            {
                var bustRt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
                Vector3 full = runner.transform.position + new Vector3(0f, 0.95f, 0f);
                Vector3 bust = runner.transform.position + new Vector3(0f, 1.5f, 0f);
                foreach (SkinPreset skin in library.Skins)
                {
                    runner.Apply(skin);
                    camera.targetTexture = rt;
                    camera.fieldOfView = 26f;
                    camera.transform.LookAt(full);
                    result[skin.id] = Capture(camera, rt, $"{ThumbDir}/{skin.id}.png");
                    // Göğüs portresi (market kostüm kartları — docs/references/market/offers.jpg)
                    camera.targetTexture = bustRt;
                    camera.fieldOfView = 12.5f;
                    camera.transform.LookAt(bust);
                    result[skin.id + "_bust"] = Capture(camera, bustRt, $"{ThumbDir}/{skin.id}_bust.png");
                }
                camera.targetTexture = rt;
                Object.DestroyImmediate(bustRt);
            }
            finally
            {
                camera.targetTexture = previous;
                camera.fieldOfView = fov;
                camera.transform.rotation = rot;
                runner.ApplyGear();
                Object.DestroyImmediate(rt);
                runner.Apply(library.Find("rookie"));
            }
            return result;
        }

        private static Sprite Capture(Camera camera, RenderTexture rt, string path)
        {
            camera.Render();
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Ekipman çeşitleri — docs/references/market/gear.jpg.</summary>
        private static List<SkinPreset> DefaultGear() => new List<SkinPreset>
        {
            // Stealth: mat siyah, kırmızı ışık
            new SkinPreset { id = "stealth", primary = Hex("#1A1A20"), secondary = Hex("#101014"), armor = Hex("#2A2A32"), glove = Hex("#141418"),
                glow = Hex("#FF2A3A"), visor = Hex("#141C34"), glowIntensity = 3f, metallic = 0.3f, smoothness = 0.35f },
            // Velocity: parlak mavi, camgöbeği ışık
            new SkinPreset { id = "velocity", primary = Hex("#1E4FD0"), secondary = Hex("#163A9A"), armor = Hex("#3A6CFF"), glove = Hex("#1A3C9E"),
                glow = Hex("#4FE0FF"), visor = Hex("#0E2A70"), glowIntensity = 6f, metallic = 0.4f, smoothness = 0.7f, visorGlows = true },
            // Titanium: kırmızı + altın, mavi vizör
            new SkinPreset { id = "titanium", primary = Hex("#B0161E"), secondary = Hex("#8A1218"), armor = Hex("#E2B04A"), glove = Hex("#9C141C"),
                glow = Hex("#FFD27A"), visor = Hex("#2F6FE0"), glowIntensity = 3f, metallic = 0.75f, smoothness = 0.75f },
        };
        private static List<SkinPreset> DefaultSkins()
        {
            SkinPreset S(string id, string p, string s, string a, string g, string glow, string visor, float intensity,
                float metallic = 0.15f, float smooth = 0.45f, bool visorGlows = false, float secondaryGlow = 0f) => new SkinPreset
            {
                id = id, primary = Hex(p), secondary = Hex(s), armor = Hex(a), glove = Hex(g), glow = Hex(glow), visor = Hex(visor),
                glowIntensity = intensity, metallic = metallic, smoothness = smooth, visorGlows = visorGlows, secondaryGlow = secondaryGlow,
            };
            return new List<SkinPreset>
            {
                // docs/references/character/design_sheet.jpg
                // docs/references/character/helmet.jpg: mat lacivert, bordo karbon, koyu vizör, mavi ışık
                S("rookie", "#7A2238", "#1E2340", "#4E5675", "#1A1F33", "#3FD8FF", "#152045", 4f),
                // docs/references/character/skins.jpg
                S("stealth", "#4A1A30", "#141418", "#2C2C34", "#101014", "#8A2A4A", "#22242C", 1.5f, 0.3f, 0.5f),
                S("velocity", "#1F2B52", "#18213D", "#2E3E6E", "#121A30", "#39E6FF", "#14203A", 6f, 0.2f, 0.6f, true),
                S("titanium", "#C5CBD6", "#2E3138", "#E8EBF0", "#2A2C33", "#FF3448", "#BFC6D2", 4f, 0.7f, 0.7f),
                S("glitch", "#7A2238", "#5E6478", "#8A93AA", "#2A2F40", "#3FA8FF", "#8EA8FF", 5f, 0.15f, 0.5f, true, 0.6f),
                // Ek renk çeşitleri
                S("crimson", "#C21F3A", "#2A1A22", "#F0C8CF", "#1A1014", "#FF6A3A", "#E8D0D4", 4f),
                S("frost", "#E6EEF8", "#3A5A80", "#9FD8FF", "#2A3A55", "#7FE8FF", "#CFE8FF", 5f, 0.3f, 0.6f),
                S("blaze", "#E0661F", "#2A1E1A", "#FFC36A", "#1E1612", "#FFB23A", "#3A2A20", 5f),
            };
        }
    }
}
