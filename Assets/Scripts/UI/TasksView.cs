using System;
using Jumpline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Görevler ekranı: oyuncu seviyesi + XP çubuğu, bugünün 3 görevi, kalıcı başarımlar. Tamamlanan satırda "AL" düğmesi
    /// yanar; alınınca ödül verilir (Progression). Ekran açıldıkça ve dil değiştikçe yenilenir.
    /// </summary>
    public class TasksView : MonoBehaviour
    {
        [Serializable]
        public struct Row
        {
            public TMP_Text text;
            public TMP_Text reward;
            public RectTransform fill;
            public float fullWidth;
            public TMP_Text count;
            public Button claim;
            public TMP_Text claimLabel;
        }

        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text xpLabel;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private float xpWidth = 600f;
        [SerializeField] private Row[] taskRows;
        [SerializeField] private Row[] achievementRows;

        private void Awake()
        {
            for (int i = 0; i < taskRows.Length; i++)
            {
                int k = i;
                if (taskRows[i].claim != null) taskRows[i].claim.onClick.AddListener(() => { if (Progression.ClaimTask(k)) Claimed(); });
            }
            for (int i = 0; i < achievementRows.Length; i++)
            {
                int k = i;
                if (achievementRows[i].claim != null)
                    achievementRows[i].claim.onClick.AddListener(() => { if (k < Progression.Achievements.Length && Progression.ClaimAchievement(Progression.Achievements[k])) Claimed(); });
            }
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Claimed()
        {
            Jumpline.Audio.AudioManager.Play("purchase");
            Refresh();
        }

        public void Refresh()
        {
            int lv = Progression.Level;
            if (levelLabel != null) levelLabel.text = Localization.Format("LV {0}", lv);
            float p = Progression.LevelProgress;
            if (xpFill != null) xpFill.sizeDelta = new Vector2(Mathf.Max(8f, xpWidth * p), xpFill.sizeDelta.y);
            if (xpLabel != null) xpLabel.text = Mathf.RoundToInt(p * Progression.XpForLevel(lv)) + " / " + Progression.XpForLevel(lv);

            Progression.Task[] today = Progression.TodayTasks();
            for (int i = 0; i < taskRows.Length; i++)
            {
                if (i >= today.Length) continue;
                Progression.Task t = today[i];
                Fill(taskRows[i], Localization.Format(t.text, t.target.ToString("N0")), Progression.TaskProgress(t), t.target,
                     t.gold, t.gems, Progression.TaskDone(t), Progression.TaskClaimed(i));
            }
            for (int i = 0; i < achievementRows.Length && i < Progression.Achievements.Length; i++)
            {
                Progression.Achievement a = Progression.Achievements[i];
                Fill(achievementRows[i], Localization.Format(a.text, a.target.ToString("N0")), Progression.AchievementProgress(a), a.target,
                     0, a.gems, Progression.AchievementDone(a), Progression.AchievementClaimed(a));
            }
        }

        private static void Fill(Row r, string text, int progress, int target, int gold, int gems, bool done, bool claimed)
        {
            if (r.text != null) r.text.text = text;
            if (r.reward != null) r.reward.text = (gold > 0 ? "+" + gold + " " + Localization.Get("GOLD") : "") + (gold > 0 && gems > 0 ? "  " : "") + (gems > 0 ? "+" + gems + " " + Localization.Get("GEMS") : "");
            if (r.fill != null) r.fill.sizeDelta = new Vector2(Mathf.Max(6f, r.fullWidth * Mathf.Clamp01(progress / (float)target)), r.fill.sizeDelta.y);
            if (r.count != null) r.count.text = progress.ToString("N0") + "/" + target.ToString("N0");
            if (r.claim != null) r.claim.interactable = done && !claimed;
            if (r.claimLabel != null) r.claimLabel.text = Localization.Get(claimed ? "CLAIMED" : "CLAIM");
        }
    }
}
