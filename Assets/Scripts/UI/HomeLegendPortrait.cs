using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Ana ekran sahne halkası: sıradaki rakibin (açık ama henüz yenilmemiş ilk bölüm) karikatür portresi + adı.
    /// Hepsi yenildiyse son açık bölümünki.
    /// </summary>
    public class HomeLegendPortrait : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private TMP_Text nameLabel;
        [Tooltip("Bölüm sırasıyla portreler (1. bölüm = 0).")]
        [SerializeField] private Sprite[] portraits;
        [Tooltip("Bölüm sırasıyla efsane adları (çeviri anahtarı).")]
        [SerializeField] private string[] nameKeys;

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        public static int NextLevel(int count)
        {
            int lastUnlocked = 1;
            for (int lv = 1; lv <= count; lv++)
            {
                if (!SaveManager.IsLevelUnlocked(lv)) break;
                lastUnlocked = lv;
                if (SaveManager.GetStars(lv) == 0) return lv;
            }
            return lastUnlocked;
        }

        private void Refresh()
        {
            if (portraits == null || portraits.Length == 0) return;
            int i = Mathf.Clamp(NextLevel(portraits.Length) - 1, 0, portraits.Length - 1);
            if (image != null) image.sprite = portraits[i];
            if (nameLabel != null && nameKeys != null && i < nameKeys.Length) nameLabel.text = Localization.Get(nameKeys[i]);
        }
    }
}
