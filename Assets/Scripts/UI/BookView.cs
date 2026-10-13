using System;
using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Efsaneler Kitabı (docs/flow_ui/kitap_*, canavar_detay_*): her bölümün efsanesi bir kart. Bölüm kazanılınca
    /// (en az 1 yıldız) efsane "yakalanmış" sayılır ve renkli görünür; değilse kara siluet + kilit + "???".
    /// Karta dokununca detay sayfası açılır (güç/hız/kurnazlık, hikâye, zayıf nokta).
    /// </summary>
    public class BookView : MonoBehaviour
    {
        [Serializable]
        public struct Legend
        {
            [Min(1)] public int level;
            [Tooltip("Çeviri anahtarı (İngilizce).")] public string nameKey;
            public string regionKey;
            [Tooltip("Çeviri anahtarı (İngilizce).")] public string storyKey;
            public string weaknessKey;
            [Range(0, 3)] public int rarity;
            public Vector3Int stats;   // güç, hız, kurnazlık (1-5)
            public Sprite portrait;
        }

        [Serializable]
        public struct Card
        {
            public Button button;
            public Image portrait;
            public TMP_Text name;
            public GameObject lockIcon;
        }

        [SerializeField] private Legend[] legends;
        [SerializeField] private Card[] cards;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private RectTransform progressFill;
        [SerializeField, Min(1f)] private float progressWidth = 600f;

        [Header("Detay sayfası")]
        [SerializeField] private GameObject detail;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailRarity;
        [SerializeField] private TMP_Text detailRegion;
        [SerializeField] private Image detailPortrait;
        [SerializeField] private TMP_Text detailStory;
        [SerializeField] private TMP_Text detailWeakness;
        [SerializeField] private TMP_Text detailCaught;
        [SerializeField] private Image[] statSegments;   // 3 satır x 5 parça
        [SerializeField] private Button closeButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Color segmentOn = new Color(0.12f, 0.65f, 0.63f);
        [SerializeField] private Color segmentOff = new Color(0.35f, 0.27f, 0.18f, 0.35f);
        [SerializeField] private Color lockedTint = new Color(0.2f, 0.17f, 0.22f, 1f);   // karikatür kare: gölgede kalsın, seçilsin

        private static readonly string[] RarityKeys = { "COMMON", "RARE", "EPIC", "LEGENDARY" };
        private int shown = -1;

        private void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int k = i;
                if (cards[i].button != null) cards[i].button.onClick.AddListener(() => Show(k));
            }
            if (closeButton != null) closeButton.onClick.AddListener(() => detail.SetActive(false));
            if (prevButton != null) prevButton.onClick.AddListener(() => Step(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Step(1));
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            if (detail != null) detail.SetActive(false);
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private bool Caught(int i) => SaveManager.GetStars(legends[i].level) > 0;

        private void Refresh()
        {
            int caught = 0;
            for (int i = 0; i < legends.Length && i < cards.Length; i++)
            {
                bool c = Caught(i);
                if (c) caught++;
                Card card = cards[i];
                if (card.portrait != null) card.portrait.color = c ? Color.white : lockedTint;
                if (card.name != null) card.name.text = c ? Localization.Get(legends[i].nameKey) : "???";
                if (card.lockIcon != null) card.lockIcon.SetActive(!c);
            }
            if (progressLabel != null) progressLabel.text = Localization.Format("{0}/{1} LEGENDS", caught, legends.Length);
            if (progressFill != null)
                progressFill.sizeDelta = new Vector2(progressWidth * caught / Mathf.Max(1, legends.Length), progressFill.sizeDelta.y);
            if (shown >= 0 && detail != null && detail.activeSelf) Show(shown);
        }

        private void Step(int d)
        {
            if (legends.Length == 0) return;
            Show((shown + d + legends.Length) % legends.Length);
        }

        private void Show(int i)
        {
            if (detail == null || i < 0 || i >= legends.Length) return;
            shown = i;
            Legend l = legends[i];
            bool c = Caught(i);
            detail.SetActive(true);
            if (detailName != null) detailName.text = c ? Localization.Get(l.nameKey) : "???";
            if (detailRarity != null) detailRarity.text = Localization.Get(RarityKeys[Mathf.Clamp(l.rarity, 0, 3)]);
            if (detailRegion != null) detailRegion.text = Localization.Get(l.regionKey);
            if (detailPortrait != null) { detailPortrait.sprite = l.portrait; detailPortrait.color = c ? Color.white : lockedTint; }
            if (detailStory != null) detailStory.text = c ? Localization.Get(l.storyKey) : Localization.Get("Beat this level to capture the legend and read its story.");
            if (detailWeakness != null) detailWeakness.text = c ? Localization.Get(l.weaknessKey) : "?";
            if (detailCaught != null) detailCaught.text = Localization.Format("STARS: {0}/3", SaveManager.GetStars(l.level));
            if (statSegments != null)
            {
                int[] v = { l.stats.x, l.stats.y, l.stats.z };
                for (int s = 0; s < statSegments.Length; s++)
                    if (statSegments[s] != null) statSegments[s].color = c && (s % 5) < v[Mathf.Min(s / 5, 2)] ? segmentOn : segmentOff;
            }
        }
    }
}
