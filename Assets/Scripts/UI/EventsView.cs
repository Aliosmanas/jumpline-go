using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Etkinlikler: sezon ilerlemesi ve günlük görev durumu.</summary>
    public class EventsView : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private float progressWidth;
        [SerializeField] private TMP_Text dailyDescription;
        [SerializeField] private TMP_Text dailyStatus;

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Refresh()
        {
            int completed = Mathf.Clamp(SaveManager.UnlockedLevel - 1, 0, config.LevelCount);
            progressText.text = Localization.Format("{0}/{1} LEVELS", completed, config.LevelCount);
            progressFill.sizeDelta = new Vector2(progressWidth * completed / config.LevelCount, progressFill.sizeDelta.y);
            dailyDescription.text = Localization.Format("A new level every day. Reward: {0} gems.", config.DailyRewardGems);
            dailyStatus.text = SaveManager.DailyClaimedToday ? Localization.Get("COMPLETED TODAY") : "";
        }
    }
}
