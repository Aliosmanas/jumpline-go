using Jumpline.Core;
using Jumpline.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Bağlılık arayüzü (aşama 11): Görevler ekranı (seviye + XP, günlük 3 görev, başarımlar), ana menü seviye rozeti,
    /// alt menüde alınmamış ödül sayacı, 7 günlük giriş takvimi penceresi. Veri: Core/Progression.cs.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private static void BuildTasks(Screen s)
        {
            SolidBackground(s);
            Link(Header(s.top, "TASKS"), MenuLink.LinkAction.Back);
            // seviye
            var lvSec = Section(s.top, "Level", "PLAYER LEVEL", new Rect(63f, 220f, 990f, 220f));
            var lvBadge = Panel(lvSec, "LevelBadge", new Rect(103f, 330f, 200f, 90f), Hex("#7A3B1C"), Hex("#E39A55"), 45f, Hex("#FFE0A8"));
            var lvText = Txt(lvBadge, "Level", "LV 1", new Rect(103f, 330f, 200f, 90f), fExtra, 44f, Color.white, localize: false);
            RectTransform xpFill = ProgressBar(lvSec, "XpBar", new Rect(330f, 345f, 680f, 30f), 0.3f);
            var xpText = Txt(lvSec, "Xp", "", new Rect(330f, 382f, 680f, 40f), fBold, 28f, Hex("#B5B9C6"), TextAlignmentOptions.Right, localize: false);
            // günlük görevler
            var taskSec = Section(s.top, "Daily", "DAILY TASKS", new Rect(63f, 460f, 990f, 540f));
            var taskRows = new System.Collections.Generic.List<(TMP_Text text, TMP_Text reward, RectTransform fill, TMP_Text count, Button claim, TMP_Text label)>();
            for (int i = 0; i < 3; i++)
            {
                float y = 568f + i * 142f;
                taskRows.Add(TaskRow(taskSec, "Task" + i, y));
            }
            // başarımlar (kaydırılabilir)
            var achSec = Section(s.top, "Achievements", "ACHIEVEMENTS", new Rect(63f, 1020f, 990f, 920f));
            int count = Progression.Achievements.Length;
            Box list = VerticalScroll(achSec, "AchList", new Rect(83f, 1122f, 950f, 800f), 1122f + count * 142f + 16f);
            var achRows = new System.Collections.Generic.List<(TMP_Text, TMP_Text, RectTransform, TMP_Text, Button, TMP_Text)>();
            for (int i = 0; i < count; i++) achRows.Add(TaskRow(list, "Ach" + i, 1130f + i * 142f));

            var view = s.screen.gameObject.AddComponent<TasksView>();
            Set(view, "levelLabel", lvText);
            Set(view, "xpLabel", xpText);
            Set(view, "xpFill", xpFill);
            SetFloat(view, "xpWidth", 674f);
            var so = new SerializedObject(view);
            void Rows(string field, System.Collections.Generic.List<(TMP_Text, TMP_Text, RectTransform, TMP_Text, Button, TMP_Text)> rows)
            {
                var arr = so.FindProperty(field);
                arr.arraySize = rows.Count;
                for (int i = 0; i < rows.Count; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("text").objectReferenceValue = rows[i].Item1;
                    e.FindPropertyRelative("reward").objectReferenceValue = rows[i].Item2;
                    e.FindPropertyRelative("fill").objectReferenceValue = rows[i].Item3;
                    e.FindPropertyRelative("fullWidth").floatValue = 514f;
                    e.FindPropertyRelative("count").objectReferenceValue = rows[i].Item4;
                    e.FindPropertyRelative("claim").objectReferenceValue = rows[i].Item5;
                    e.FindPropertyRelative("claimLabel").objectReferenceValue = rows[i].Item6;
                }
            }
            Rows("taskRows", taskRows);
            Rows("achievementRows", achRows);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Görev satırı: metin, ödül, ilerleme çubuğu + sayı, "AL" düğmesi.</summary>
        private static (TMP_Text, TMP_Text, RectTransform, TMP_Text, Button, TMP_Text) TaskRow(Box parent, string name, float y)
        {
            var card = Panel(parent, name, new Rect(93f, y, 930f, 132f), Hex("#1B2236", 0.9f), Hex("#2A3556", 0.9f), 22f, Hex("#3B4A70"));
            var text = Txt(card, "Text", "", new Rect(118f, y + 10f, 620f, 46f), fBold, 30f, Color.white, TextAlignmentOptions.Left, localize: false);
            var reward = Txt(card, "Reward", "", new Rect(118f, y + 52f, 620f, 34f), fBold, 24f, C.Gold, TextAlignmentOptions.Left, localize: false);
            RectTransform fill = ProgressBar(card, "Bar", new Rect(118f, y + 92f, 520f, 22f), 0.2f);
            var count = Txt(card, "Count", "", new Rect(646f, y + 86f, 130f, 34f), fBold, 22f, Hex("#B5B9C6"), TextAlignmentOptions.Left, localize: false);
            Button claim = BoxButton(card, "Claim", new Rect(800f, y + 26f, 200f, 80f), "CLAIM", 30f, out TextMeshProUGUI label, false);
            return (text, reward, fill, count, claim, label);
        }

        /// <summary>Üst şeritte (para çubuğunun solu, her ekranda) seviye rozeti; üstünde alınmamış ödül sayacı (dokununca Görevler).</summary>
        private static void BuildPlayerBadge(Transform safe)
        {
            var rt = NewUI("PlayerBadge", safe);
            Stretch(rt);
            Box top = Group(rt, "Top", 1f);
            var badgeBox = Panel(top, "PlayerLevel", new Rect(30f, 12f, 220f, 68f), Hex("#7A3B1C"), Hex("#E39A55"), 34f, Hex("#FFE0A8"));
            var lv = Txt(badgeBox, "Level", "LV 1", new Rect(30f, 10f, 220f, 46f), fExtra, 32f, Color.white, localize: false);
            Panel(badgeBox, "XpTrack", new Rect(55f, 54f, 170f, 12f), C.NavyDark, C.NavyDark, 6f, Hex("#00000000"));
            var fill = Add(badgeBox, "XpFill", new Rect(57f, 56f, 166f, 8f));
            var fi = fill.go.AddComponent<Image>();
            Rounded(fi, 4f);
            fi.color = Hex("#FFE08A");
            fi.raycastTarget = false;
            PivotLeft(fill);
            var bb = MakeButton(badgeBox, badgeBox.go.GetComponent<Image>());
            Link(bb, MenuLink.LinkAction.Open, "tasks");
            var d = Panel(badgeBox, "UnclaimedBadge", new Rect(228f, 0f, 44f, 44f), Hex("#B3262E"), Hex("#FF4B5A"), 22f, Hex("#FFFFFF"));
            TMP_Text dotText = Txt(d, "Count", "1", new Rect(228f, 0f, 44f, 44f), fExtra, 26f, Color.white, localize: false);
            GameObject dot = d.go;
            var badge = badgeBox.go.AddComponent<PlayerLevelBadge>();
            Set(badge, "level", lv);
            Set(badge, "xpFill", fill.rt);
            SetFloat(badge, "xpWidth", 166f);
            Set(badge, "unclaimedBadge", dot);
            Set(badge, "unclaimedCount", dotText);
        }

        /// <summary>7 günlük giriş takvimi penceresi (tüm ekranların üstünde).</summary>
        private static void BuildLoginCalendar(Transform safe)
        {
            var rt = NewUI("LoginCalendar", safe);
            Stretch(rt);
            var dim = Img(rt, "Dim", null, new Color(0f, 0f, 0f, 0.7f), false);
            Stretch(dim.rectTransform, new Vector2(-300f, -300f), new Vector2(300f, 300f));
            dim.raycastTarget = true;
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            Box mid = Group(rt, "Middle", 0.5f);
            var panel = Section(mid, "Panel", "DAILY LOGIN", new Rect(63f, 500f, 990f, 1000f));
            var tiles = new Object[7];
            var checks = new Object[7];
            var labels = new Object[7];
            for (int i = 0; i < 7; i++)
            {
                bool big = i == 6;
                float x = big ? 113f : 113f + (i % 3) * 300f;
                float y = big ? 1080f : 620f + (i / 3) * 230f;
                float w = big ? 890f : 290f;
                var tile = Panel(panel, "Day" + (i + 1), new Rect(x, y, w, 210f), Hex("#2A3556"), Hex("#4A5A86"), 24f, Hex("#E39A55"));
                tiles[i] = tile.go.GetComponent<Image>();
                labels[i] = Txt(tile, "DayLabel", "DAY " + (i + 1), new Rect(x, y + 8f, w, 40f), fExtra, 28f, Color.white, localize: false);
                var (gold, gems) = Progression.LoginRewards[i];
                if (gems > 0) Icon(tile, "gem", new Rect(x + w * 0.5f - 46f - (gold > 0 ? 70f : 0f), y + 56f, 92f, 92f), Color.white);
                if (gold > 0) Icon(tile, "coin", new Rect(x + w * 0.5f - 46f + (gems > 0 ? 70f : 0f), y + 56f, 92f, 92f), Color.white);
                string amount = (gold > 0 ? gold.ToString() : "") + (gold > 0 && gems > 0 ? " + " : "") + (gems > 0 ? gems.ToString() : "");
                Txt(tile, "Amount", amount, new Rect(x, y + 152f, w, 46f), fExtra, 32f, C.Gold, localize: false);
                checks[i] = Icon(tile, "check", new Rect(x + w - 62f, y + 10f, 50f, 50f), Hex("#7CFF8A")).gameObject;
            }
            Button claim = BoxButton(panel, "Claim", new Rect(313f, 1320f, 490f, 120f), "CLAIM", 48f);
            Button close = PillButton(panel, "Close", new Rect(950f, 515f, 86f, 64f), "X", 34f, Color.white, 0.2f);
            var view = rt.gameObject.AddComponent<LoginCalendarView>();
            Set(view, "group", group);
            SetArray(view, "dayTiles", tiles);
            SetArray(view, "dayChecks", checks);
            SetArray(view, "dayLabels", labels);
            Set(view, "claim", claim);
            Set(view, "close", close);
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
    }
}
