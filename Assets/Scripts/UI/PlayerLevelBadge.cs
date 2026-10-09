using Jumpline.Core;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Oyuncu seviyesi rozeti (ana menü sol üst) + alınmamış görev ödülü sayacı (alt menü "Görevler" üstü).</summary>
    public class PlayerLevelBadge : MonoBehaviour
    {
        [SerializeField] private TMP_Text level;
        [SerializeField] private RectTransform xpFill;
        [SerializeField] private float xpWidth = 160f;
        [SerializeField] private GameObject unclaimedBadge;
        [SerializeField] private TMP_Text unclaimedCount;

        private float nextRefresh;

        private void OnEnable() => Refresh();

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.5f;
            Refresh();
        }

        private void Refresh()
        {
            if (level != null) level.text = Localization.Format("LV {0}", Progression.Level);
            if (xpFill != null) xpFill.sizeDelta = new Vector2(Mathf.Max(6f, xpWidth * Progression.LevelProgress), xpFill.sizeDelta.y);
            int n = Progression.UnclaimedCount();
            if (unclaimedBadge != null) unclaimedBadge.SetActive(n > 0);
            if (unclaimedCount != null) unclaimedCount.text = n.ToString();
        }
    }
}
