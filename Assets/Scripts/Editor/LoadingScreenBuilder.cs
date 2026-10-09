using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>Yükleme ekranı prefab'ı (Assets/Resources/LoadingScreen.prefab): koyu bazalt zemin, almaşık şerit, logo, çubuk, ipucu.</summary>
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
            var bg = Img(canvas, "Background", null, Hex("#17161A"), false);
            Stretch(bg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            Box mid = Group(canvas, "Middle", 0.5f);
            // almaşık şerit (bazalt-kireç) iki yanda
            for (int i = 0; i < 12; i++)
            {
                var stripe = Add(mid, "Stripe" + i, new Rect(i * 96f, 640f, 48f, 18f));
                var img = stripe.go.AddComponent<Image>();
                img.color = Hex("#ECE6DA", 0.85f);
                img.raycastTarget = false;
                var stripe2 = Add(mid, "StripeB" + i, new Rect(i * 96f + 48f, 1342f, 48f, 18f));
                var img2 = stripe2.go.AddComponent<Image>();
                img2.color = Hex("#ECE6DA", 0.85f);
                img2.raycastTarget = false;
            }
            var title = Txt(mid, "Title", "JUMPLINE GO", new Rect(0f, 760f, 1116f, 170f), fTitle != null ? fTitle : fExtra, 120f, Color.white, localize: false);
            Chrome(title);
            Txt(mid, "Subtitle", "SUR İÇİ", new Rect(0f, 925f, 1116f, 60f), fBold, 38f, Hex("#E8B95B"), localize: false);
            Panel(mid, "BarTrack", new Rect(258f, 1050f, 600f, 30f), C.NavyDark, C.NavyDark, 15f, Hex("#5D6A91"));
            var fill = Add(mid, "BarFill", new Rect(261f, 1053f, 594f, 24f));
            var fillImg = fill.go.AddComponent<Image>();
            Rounded(fillImg, 12f);
            fillImg.color = Hex("#E8B95B");
            fillImg.raycastTarget = false;
            PivotLeft(fill);
            var tip = Txt(mid, "Tip", "", new Rect(118f, 1130f, 880f, 150f), fMedium, 38f, Hex("#C9CCD6"), localize: false, wrap: true);
            var ls = canvas.gameObject.AddComponent<LoadingScreen>();
            Set(ls, "group", group);
            Set(ls, "barFill", fill.rt);
            SetFloat(ls, "barWidth", 594f);
            Set(ls, "tipLabel", tip);
            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, PrefabPath);
            Object.DestroyImmediate(canvas.gameObject);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Jumpline] Yükleme ekranı: {PrefabPath}");
        }
    }
}
