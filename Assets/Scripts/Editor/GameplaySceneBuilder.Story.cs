using Jumpline.Boss;
using Jumpline.Level;
using Jumpline.Player;
using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Hikâye (aşama 08): portreli konuşma kutusu (DialogueBox), açılış sahnesi (IntroSequence — Efsaneler Kitabı),
    /// çaycı amcalı öğretici (TutorialDirector: yavaşlatma + parmak işareti). Portreler: Blender/scripts/build_portraits.py.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string PortraitDir = "Assets/Art/UI/Portraits";

        private static Sprite Portrait(string name)
        {
            string path = $"{PortraitDir}/{name}.png";
            if (AssetImporter.GetAtPath(path) is not TextureImporter imp) return null;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 512;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void BuildStory(Transform safe, Transform runner)
        {
            var player = runner.GetComponent<PlayerController>();
            // --- açılış karartması + atla (konuşma kutusunun altında) ---
            var backdropRt = NewUI("IntroBackdrop", safe);
            Stretch(backdropRt);
            var dim = Img(backdropRt, "Dim", null, new Color(0.05f, 0.04f, 0.06f, 0.78f), false);
            Stretch(dim.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            dim.raycastTarget = true;
            var backdrop = backdropRt.gameObject.AddComponent<CanvasGroup>();
            Box bTop = Group(backdropRt, "Top", 1f);
            Box bMid = Group(backdropRt, "Middle", 0.5f);
            var bookArt = Add(bMid, "BookArt", new Rect(308f, 380f, 500f, 500f));
            var bookImg = bookArt.go.AddComponent<Image>();
            bookImg.sprite = Portrait("book");
            bookImg.preserveAspect = true;
            bookImg.raycastTarget = false;
            Txt(bMid, "IntroTitle", "THE BOOK OF LEGENDS", new Rect(0f, 860f, 1116f, 90f), fExtra, 58f, Hex("#E8B95B"));
            Button skip = PillButton(bTop, "SkipIntro", new Rect(40f, 56f, 240f, 84f), "SKIP", 34f, Color.white, 1f);

            // --- konuşma kutusu ---
            var rootRt = NewUI("Dialogue", safe);
            Stretch(rootRt);
            var group = rootRt.gameObject.AddComponent<CanvasGroup>();
            var tapImg = Img(rootRt, "TapArea", null, new Color(0f, 0f, 0f, 0f), false);
            Stretch(tapImg.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            tapImg.raycastTarget = true;
            var tap = tapImg.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            Box bot = Group(rootRt, "Bottom", 0f);
            var pl = Add(bot, "PortraitLeft", new Rect(30f, 1040f, 300f, 300f));
            var plImg = pl.go.AddComponent<Image>();
            plImg.preserveAspect = true;
            plImg.raycastTarget = false;
            var pr = Add(bot, "PortraitRight", new Rect(786f, 1040f, 300f, 300f));
            var prImg = pr.go.AddComponent<Image>();
            prImg.preserveAspect = true;
            prImg.raycastTarget = false;
            Panel(bot, "Box", new Rect(30f, 1320f, 1056f, 250f), Hex("#2A1420", 0.94f), Hex("#3B2232", 0.94f), 34f, Hex("#E39A55"));
            var plate = Panel(bot, "NamePlate", new Rect(70f, 1290f, 380f, 62f), Hex("#7A3B1C"), Hex("#C9773A"), 31f, Hex("#FFE0A8"));
            var name = Txt(plate, "Name", "", new Rect(70f, 1290f, 380f, 62f), fExtra, 32f, Color.white, localize: false);
            var body = Txt(bot, "Body", "", new Rect(70f, 1362f, 976f, 180f), fBold, 36f, Hex("#F4ECDD"), TextAlignmentOptions.TopLeft, localize: false, wrap: true);
            var hint = Txt(bot, "Continue", "TAP TO CONTINUE", new Rect(646f, 1526f, 420f, 36f), fBold, 24f, Hex("#E8B95B"), TextAlignmentOptions.Right);
            hint.gameObject.SetActive(false);
            var box = rootRt.gameObject.AddComponent<DialogueBox>();
            Set(box, "group", group);
            Set(box, "portraitLeft", plImg);
            Set(box, "portraitRight", prImg);
            Set(box, "nameLabel", name);
            Set(box, "namePlate", plate.go.GetComponent<Image>());
            Set(box, "body", body);
            Set(box, "continueHint", hint.gameObject);
            Set(box, "tapArea", tap);
            var speakers = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>>
            {
                Speaker("narrator", "", "book", "#5A1A1E", false),
                Speaker("hero", "NEPHEW", "hero", "#1F4E9C", false),
                Speaker("tepegoz", "TEPEGOZ", "tepegoz", "#8E1F1A", true),
                Speaker("sahmeran", "SHAHMARAN", "sahmeran", "#1E6B4A", true),
                Speaker("humbaba", "HUMBABA", "humbaba", "#5C4A1E", true),
                Speaker("stone_guardian", "STONE GUARDIAN", "stone_guardian", "#2C6E6A", true),
                Speaker("van_monster", "VAN LAKE MONSTER", "van_monster", "#155A55", true),
                Speaker("karakoncolos", "KARAKONCOLOS", "karakoncolos", "#2B3A55", true),
                Speaker("lamassu", "LAMASSU", "lamassu", "#1F3E8C", true),
                Speaker("dragon", "THE DRAGON", "dragon", "#8A1A0E", true),
                Speaker("cayci", "TEA UNCLE", "cayci", "#7A3B1C", false),
                Speaker("thief", "PURSE-SNATCHER", "thief", "#30343C", true),
                Speaker("cat", "SUR CAT", "cat", "#C46A1E", true),
            };
            SetStructArray(box, "speakers", speakers);

            // --- açılış ---
            var intro = new GameObject("IntroSequence").AddComponent<IntroSequence>();
            Set(intro, "dialogue", box);
            Set(intro, "backdrop", backdrop);
            Set(intro, "skipButton", skip);
            var flowForOutro = Object.FindAnyObjectByType<LevelFlowController>(FindObjectsInactive.Include);
            if (flowForOutro != null) Set(flowForOutro, "outro", intro);   // oyun sonu konuşması (son bölüm)

            // --- öğretici: parmak/ok işareti + çaycı amca ---
            Box mid = Group(rootRt, "Middle", 0.5f);
            var fingerBox = Add(mid, "TutorialFinger", new Rect(478f, 930f, 160f, 160f));
            var fingerGroup = fingerBox.go.AddComponent<CanvasGroup>();
            fingerGroup.blocksRaycasts = false;
            var fingerImg = fingerBox.go.AddComponent<Image>();
            fingerImg.sprite = Icons["arrow-left"];
            fingerImg.color = Hex("#FFE08A");
            fingerImg.preserveAspect = true;
            fingerImg.raycastTarget = false;
            fingerBox.go.AddComponent<Shadow>().effectDistance = new Vector2(0f, -6f);
            var tut = new GameObject("TutorialDirector").AddComponent<TutorialDirector>();
            Set(tut, "player", player);
            Set(tut, "throws", Object.FindAnyObjectByType<ThrowSystem>(FindObjectsInactive.Include));
            Set(tut, "dialogue", box);
            Set(tut, "finger", fingerBox.rt);
            Set(tut, "fingerGroup", fingerGroup);
            var throwBtn = GameObject.Find("ThrowButton");
            if (throwBtn != null) Set(tut, "throwButton", throwBtn.transform);
            rootRt.SetAsLastSibling();
        }

        private static System.Collections.Generic.Dictionary<string, object> Speaker(string id, string display, string portrait, string plate, bool right) =>
            new System.Collections.Generic.Dictionary<string, object>
            {
                { "id", id }, { "displayName", display }, { "portrait", Portrait(portrait) }, { "plateColor", Hex(plate) }, { "right", right },
            };
    }
}
