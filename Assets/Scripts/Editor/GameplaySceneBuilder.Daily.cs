using Jumpline.Level;
using Jumpline.Player;
using Jumpline.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Günlük görev (aşama 09): kapkaççı (Humanoid, koşucunun koşu klibiyle) ve Sur kedisi (parçalı, CatRunner),
    /// keseler / balıklar, üstte görev sayacı, çaycı amca konuşmaları. Varlıklar: Blender/scripts/build_thieves.py.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string ThiefDir = "Assets/Art/Thieves";
        private const string ThiefMatDir = ThiefDir + "/Materials";

        private static void BuildDailyMission(Transform runner, LevelFlowController flow)
        {
            var player = runner.GetComponent<PlayerController>();
            ConfigureThiefImport($"{ThiefDir}/Thief.fbx", true);
            ConfigureThiefImport($"{ThiefDir}/Cat.fbx", false);
            ConfigureThiefImport($"{ThiefDir}/Collectibles.fbx", false);
            var root = new GameObject("DailyMission");
            var dm = root.AddComponent<DailyMission>();

            // kapkaççı: koşucunun Humanoid koşu klibi (aynı iskelet) — RunnerGame controller'ı Run ile başlar
            GameObject thief = null;
            var thiefPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ThiefDir}/Thief.fbx");
            if (thiefPrefab != null)
            {
                thief = new GameObject("Thief_Kapkacci");
                thief.transform.SetParent(root.transform, false);
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(thiefPrefab, thief.transform);
                vis.transform.localPosition = Vector3.zero;
                var anim = vis.GetComponent<Animator>() ?? vis.AddComponent<Animator>();
                anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RunnerStageBuilder.GameControllerPath);
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var r in vis.GetComponentsInChildren<SkinnedMeshRenderer>()) r.updateWhenOffscreen = true;
            }
            GameObject cat = null;
            var catPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ThiefDir}/Cat.fbx");
            if (catPrefab != null)
            {
                cat = new GameObject("Thief_Cat");
                cat.transform.SetParent(root.transform, false);
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab, cat.transform);
                vis.transform.localPosition = Vector3.zero;
                PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                vis.AddComponent<CatRunner>();
            }
            // toplanabilir şablonlar
            var colPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ThiefDir}/Collectibles.fbx");
            GameObject Tpl(string child)
            {
                if (colPrefab == null || colPrefab.transform.Find(child) is not Transform src) return null;
                var holder = new GameObject("Tpl_" + child);
                holder.transform.SetParent(root.transform, false);
                var v = Object.Instantiate(src.gameObject, holder.transform);
                v.transform.localPosition = Vector3.zero;
                foreach (var r in v.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                holder.SetActive(false);
                return holder;
            }
            var pouch = Tpl("Pouch");
            var fish = Tpl("Fish");

            var so = new SerializedObject(dm);
            var arr = so.FindProperty("missions");
            var defs = new (string id, string spk, GameObject th, GameObject col, string icon, string title, string[] intro)[]
            {
                ("pouches", "thief", thief, pouch, "mission_pouch", "COIN PURSES", new[]
                {
                    "cayci|Tea, nephew? ... Wait, later! A purse-snatcher grabbed the grocer's purses!",
                    "thief|Catch me if you can!",
                    "cayci|Collect 5 purses, nephew, and I'll owe you one!",
                }),
                ("fish", "cat", cat, fish, "mission_fish", "FISH", new[]
                {
                    "cayci|Tea, nephew? ... Oh no! The Sur cat ran off with the fishmonger's fish!",
                    "cat|Meow!",
                    "cayci|Collect 5 fish, the fishmonger will bless you!",
                }),
            };
            arr.arraySize = defs.Length;
            for (int i = 0; i < defs.Length; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("id").stringValue = defs[i].id;
                e.FindPropertyRelative("thiefSpeaker").stringValue = defs[i].spk;
                e.FindPropertyRelative("thief").objectReferenceValue = defs[i].th;
                e.FindPropertyRelative("collectible").objectReferenceValue = defs[i].col;
                e.FindPropertyRelative("icon").objectReferenceValue = Icons.TryGetValue(defs[i].icon, out var spr) ? spr : null;
                e.FindPropertyRelative("target").intValue = 5;
                e.FindPropertyRelative("title").stringValue = defs[i].title;
                var intro = e.FindPropertyRelative("intro");
                intro.arraySize = defs[i].intro.Length;
                for (int k = 0; k < defs[i].intro.Length; k++) intro.GetArrayElementAtIndex(k).stringValue = defs[i].intro[k];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            Set(dm, "player", player);
            Set(dm, "flow", flow);
            Set(dm, "dialogue", Object.FindAnyObjectByType<DialogueBox>(FindObjectsInactive.Include));
            Set(dm, "pickupFx", MakeBurst(root.transform, "MissionPickupFx", Hex("#FFF3B0"), Hex("#E8B95B"), 2f, 5f, 0.3f));

            // görev sayacı (üst orta)
            var hud = NewUI("MissionHud", hudTop.rt.parent);
            Stretch(hud);
            Box top = Group(hud, "Top", 1f);
            var pill = Panel(top, "MissionPill", new Rect(368f, 148f, 380f, 96f), Hex("#2A1420", 0.92f), Hex("#4A2236", 0.92f), 48f, Hex("#E39A55"));
            var icon = Icon(pill, "mission_pouch", new Rect(382f, 154f, 84f, 84f), Color.white);
            var count = Txt(pill, "Count", "0/5", new Rect(470f, 148f, 260f, 96f), fExtra, 54f, Color.white, localize: false);
            var title = Txt(top, "MissionTitle", "", new Rect(318f, 250f, 480f, 44f), fBold, 28f, Hex("#E8B95B"), localize: false);
            Set(dm, "hudRoot", hud.gameObject);
            Set(dm, "hudIcon", icon);
            Set(dm, "hudCount", count);
            Set(dm, "hudTitle", title);
            hud.gameObject.SetActive(false);
        }

        private static void ConfigureThiefImport(string path, bool humanoid)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return;
            EnsureFolder(ThiefMatDir);
            imp.importCameras = imp.importLights = false;
            imp.importAnimation = false;
            if (humanoid)
            {
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.humanDescription = RunnerStageBuilder.BuildHumanDescription(imp.humanDescription);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ThiefMatDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
            }
            imp.SaveAndReimport();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is not Material m) continue;
                string dst = $"{ThiefMatDir}/{m.name}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(dst) == null) AssetDatabase.ExtractAsset(m, dst);
            }
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath($"{ThiefDir}/Thief_Skin.png") is TextureImporter ti && ti.maxTextureSize != 1024)
            {
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ThiefDir}/Thief_Skin.png");
            (string name, string color, float smooth, float metal, float glow)[] defs =
            {
                ("TH_Skin", "#FFFFFF", 0.3f, 0f, 0f), ("TH_Cloth", "#3A3F48", 0.1f, 0f, 0f), ("TH_Mask", "#101010", 0.35f, 0f, 0f),
                ("TH_Hat", "#1E1E22", 0.05f, 0f, 0f), ("TH_Sack", "#9A7548", 0.05f, 0f, 0f),
                ("CAT_Fur", "#E08A33", 0.1f, 0f, 0f), ("CAT_Stripe", "#8A4A16", 0.1f, 0f, 0f), ("CAT_White", "#F4EDE0", 0.1f, 0f, 0f),
                ("CAT_Eye", "#7CD34B", 0.8f, 0f, 1.2f), ("CAT_Nose", "#D46A7A", 0.4f, 0f, 0f),
                ("CL_Leather", "#9A6430", 0.3f, 0f, 0f), ("CL_Cord", "#D9B65A", 0.5f, 0.3f, 0f), ("CL_Gold", "#E8B95B", 0.75f, 0.9f, 0.4f),
                ("CL_Fish", "#9CC6D6", 0.75f, 0.4f, 0f), ("CL_FishBack", "#3E6C86", 0.6f, 0.3f, 0f),
            };
            foreach (var d in defs)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ThiefMatDir}/{d.name}.mat");
                if (mat == null) continue;
                mat.shader = Shader.Find("Universal Render Pipeline/Lit");
                Color c = Hex(d.color);
                mat.SetColor("_BaseColor", c);
                mat.SetFloat("_Smoothness", d.smooth);
                mat.SetFloat("_Metallic", d.metal);
                if (d.name == "TH_Skin" && skin != null) mat.SetTexture("_BaseMap", skin);
                if (d.glow > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", c * d.glow);
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
