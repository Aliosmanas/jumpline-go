using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>Yükleme ekranı prefab'ı (Assets/Resources/LoadingScreen.prefab): gece surları resmi (Flow), logo, bakır çerçeveli çubuk, ipucu.</summary>
    public static class LoadingScreenBuilder
    {
        public const string PrefabPath = "Assets/Resources/LoadingScreen.prefab";

        [MenuItem("Jumpline/Build Loading Screen")]
        public static void Build()
        {
            LoadAssets();
            EnsureFolder("Assets/Resources");
            Transform canvas = BuildCanvas("LoadingScreen", false);
            var c = canvas.GetComponent<Canvas>();
            c.sortingOrder = 500;
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var bg = Img(canvas, "Background", null, Hex("#141A33"), false);
            Stretch(bg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            Box mid = Group(canvas, "Middle", 0.5f);
            // docs/flow_ui/yukleme_1.jpg: surların üstünde koşan kahraman + logo + bakır çerçeveli çubuk (dolgusu boşaltıldı)
            var art = Add(mid, "Art", Full);
            var artImg = art.go.AddComponent<Image>();
            artImg.sprite = Skin["bg_loading"];
            artImg.raycastTarget = false;
            var fill = Add(mid, "BarFill", new Rect(178f, 1726f, 748f, 82f));
            var fillImg = fill.go.AddComponent<Image>();
            Rounded(fillImg, 32f);
            fillImg.raycastTarget = false;
            fill.go.AddComponent<UIGradient>().SetColors(Hex("#E8901A"), Hex("#FFD23F"));
            PivotLeft(fill);
            var tipBack = Add(mid, "TipBack", new Rect(98f, 1556f, 920f, 140f));
            var tbi = tipBack.go.AddComponent<Image>();
            Rounded(tbi, 30f);
            tbi.color = Hex("#0E1428", 0.72f);
            tbi.raycastTarget = false;
            var tip = Txt(mid, "Tip", "", new Rect(128f, 1566f, 860f, 120f), fBold, 34f, Color.white, localize: false, wrap: true);
            var ls = canvas.gameObject.AddComponent<LoadingScreen>();
            Set(ls, "group", group);
            Set(ls, "barFill", fill.rt);
            SetFloat(ls, "barWidth", 748f);
            Set(ls, "tipLabel", tip);
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jumpline] Yükleme ekranı: {PrefabPath}");
        }
    }
}
