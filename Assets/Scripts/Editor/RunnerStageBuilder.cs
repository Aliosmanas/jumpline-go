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
        private const float DisplayYaw = 155f; // kameraya 3/4 dönük

        public static SkinLibrary EnsureLibrary()
        {
            var lib = GetOrCreateAsset<SkinLibrary>(LibraryPath, null);
            lib.SetSkins(DefaultSkins());
            EditorUtility.SetDirty(lib);
            return lib;
        }

        /// <summary>Humanoid + malzemeleri düzenlenebilir hale getir + ışık saçan slotlar için emisyon.</summary>
        public static void ConfigureImport()
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            bool dirty = false;
            if (imp.animationType != ModelImporterAnimationType.Human)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }
            if (imp.importAnimation) { imp.importAnimation = false; dirty = true; }
            if (dirty) imp.SaveAndReimport();

            EnsureFolder(MaterialDir);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
            {
                if (o is not Material m) continue;
                string dst = $"{MaterialDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(FbxPath);
            AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialDir }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (!mat.name.StartsWith("M_Glow")) mat.SetColor("_EmissionColor", Color.black);
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Açık sahneye vitrin kurar. Vitrin uzakta (y=-200) durur, menü kamerası görmez.</summary>
        public static RenderTexture BuildStage(SkinLibrary library, out Transform stage, out RunnerSkin runner, out Camera camera)
        {
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(PreviewPath);
            if (rt == null)
            {
                rt = new RenderTexture(PreviewSize, PreviewSize, 24, RenderTextureFormat.ARGB32) { name = "RunnerPreview", antiAliasing = 4 };
                AssetDatabase.CreateAsset(rt, PreviewPath);
            }

            stage = new GameObject("RunnerStage").transform;
            stage.position = StagePosition;

            camera = new GameObject("StageCamera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(stage, false);
            camera.transform.localPosition = new Vector3(0f, 1.0f, 4.3f);
            camera.transform.LookAt(stage.position + new Vector3(0f, 0.88f, 0f));
            camera.fieldOfView = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.allowHDR = false;
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
            if (model.TryGetComponent(out Animator anim)) anim.enabled = false; // animasyonlar sonraki adım

            ApplyRunPose(model.transform);
            GroundModel(model.transform, stage.position.y);
            model.transform.localRotation = Quaternion.Euler(0f, DisplayYaw, 0f);

            runner = model.AddComponent<RunnerSkin>();
            Set(runner, "target", model.GetComponentInChildren<SkinnedMeshRenderer>());
            runner.Apply(library.Find("rookie"));

            var display = stage.gameObject.AddComponent<RunnerDisplay>();
            Set(display, "runner", runner);
            Set(display, "library", library);
            return rt;
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
        /// Referanstaki koşu pozu (design_sheet, yan görünüş): sağ diz önde yukarıda, sol bacak geride;
        /// kollar karşıt. Model +Z'ye bakar, sol taraf -X. Animasyonlar gelince bu poz yerini onlara bırakır.
        /// </summary>
        public static void ApplyRunPose(Transform root)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>()) bones[t.name] = t;

            void Aim(string bone, string child, Vector3 dir)
            {
                if (!bones.TryGetValue(bone, out Transform b) || !bones.TryGetValue(child, out Transform c)) return;
                b.rotation = Quaternion.FromToRotation(c.position - b.position, dir.normalized) * b.rotation;
            }

            Aim("Spine", "Spine1", new Vector3(0f, 1f, 0.12f));
            Aim("Spine1", "Neck", new Vector3(0f, 1f, 0.2f));
            Aim("Neck", "Head", new Vector3(0f, 1f, 0.05f));

            Aim("RightUpLeg", "RightLeg", new Vector3(0.03f, -0.45f, 0.89f));
            Aim("RightLeg", "RightFoot", new Vector3(0f, -0.92f, -0.38f));
            Aim("RightFoot", "RightToeBase", new Vector3(0f, -0.35f, 0.94f));
            Aim("LeftUpLeg", "LeftLeg", new Vector3(-0.03f, -0.86f, -0.5f));
            Aim("LeftLeg", "LeftFoot", new Vector3(0f, -0.5f, -0.87f));
            Aim("LeftFoot", "LeftToeBase", new Vector3(0f, -0.85f, -0.2f));

            Aim("LeftArm", "LeftForeArm", new Vector3(-0.22f, -0.72f, 0.66f));
            Aim("LeftForeArm", "LeftHand", new Vector3(-0.05f, 0.55f, 0.83f));
            Aim("RightArm", "RightForeArm", new Vector3(0.25f, -0.72f, -0.64f));
            Aim("RightForeArm", "RightHand", new Vector3(0.06f, -0.15f, -0.99f));
        }

        private static void GroundModel(Transform model, float groundY)
        {
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            float minY = float.MaxValue;
            foreach (Vector3 v in mesh.vertices) minY = Mathf.Min(minY, smr.transform.TransformPoint(v).y);
            Object.DestroyImmediate(mesh);
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
            camera.fieldOfView = 34f;
            try
            {
                foreach (SkinPreset skin in library.Skins)
                {
                    runner.Apply(skin);
                    camera.Render();
                    var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    string path = $"{ThumbDir}/{skin.id}.png";
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                    imp.textureType = TextureImporterType.Sprite;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled = false;
                    imp.SaveAndReimport();
                    result[skin.id] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }
            finally
            {
                camera.targetTexture = previous;
                camera.fieldOfView = fov;
                Object.DestroyImmediate(rt);
                runner.Apply(library.Find("rookie"));
            }
            return result;
        }

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
                S("rookie", "#7A2238", "#262B47", "#9EA9C8", "#1C2033", "#3FD8FF", "#AEB8D6", 4f),
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
