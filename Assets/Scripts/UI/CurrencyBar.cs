using System.Globalization;
using Jumpline.Save;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Menünün her ekranında üstte duran altın + elmas çubuğu; değerler değişince (alışveriş, ödül) güncellenir.</summary>
    public class CurrencyBar : MonoBehaviour
    {
        [SerializeField] private TMP_Text goldLabel;
        [SerializeField] private TMP_Text gemsLabel;
        [Tooltip("Değerleri kontrol aralığı (gerçek sn).")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private int gold = -1, gems = -1;
        private float next;

        private void OnEnable() => Refresh();

        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + refreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            int g = SaveManager.Gold, m = SaveManager.Gems;
            if (g != gold && goldLabel != null) goldLabel.text = g.ToString("N0", CultureInfo.InvariantCulture);
            if (m != gems && gemsLabel != null) gemsLabel.text = m.ToString("N0", CultureInfo.InvariantCulture);
            gold = g;
            gems = m;
        }
    }
}
