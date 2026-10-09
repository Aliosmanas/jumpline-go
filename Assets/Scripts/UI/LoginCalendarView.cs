using Jumpline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// 7 günlük giriş takvimi: ana menü açılınca, bugünün ödülü alınmadıysa kendiliğinden açılır. Günler: alınmış (soluk ✓),
    /// bugün (parlak, nabız), gelecek. "AL" → ödül + kapanır. Gün kaçırılırsa takvim sıfırlanmaz (Progression).
    /// </summary>
    public class LoginCalendarView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image[] dayTiles;
        [SerializeField] private GameObject[] dayChecks;
        [SerializeField] private TMP_Text[] dayLabels;
        [SerializeField] private Button claim;
        [SerializeField] private Button close;
        [SerializeField] private Color today = new Color(1f, 0.85f, 0.45f);
        [SerializeField] private Color done = new Color(0.45f, 0.42f, 0.5f);
        [SerializeField] private Color future = Color.white;

        private int pulse = -1;

        private void Awake()
        {
            if (claim != null) claim.onClick.AddListener(Claim);
            if (close != null) close.onClick.AddListener(Hide);
        }

        private void Start()
        {
            if (Progression.LoginAvailable) Show();
            else SetVisible(false);
        }

        public void Show()
        {
            int next = Progression.LoginNextDay;
            bool available = Progression.LoginAvailable;
            pulse = available ? next : -1;
            for (int i = 0; i < dayTiles.Length; i++)
            {
                bool claimed = Progression.LoginDayClaimed(i);
                if (dayTiles[i] != null) dayTiles[i].color = i == pulse ? today : claimed ? done : future;
                if (dayLabels != null && i < dayLabels.Length && dayLabels[i] != null) dayLabels[i].text = Localization.Format("DAY {0}", i + 1);
                if (dayChecks != null && i < dayChecks.Length && dayChecks[i] != null) dayChecks[i].SetActive(claimed);
            }
            if (claim != null) claim.interactable = available;
            SetVisible(true);
        }

        private void Claim()
        {
            if (!Progression.ClaimLogin()) return;
            Jumpline.Audio.AudioManager.Play("purchase");
            Haptics.Pulse(Haptics.Light, 160);
            Show();
            Invoke(nameof(Hide), 0.9f);
        }

        public void Hide() => SetVisible(false);

        private void SetVisible(bool on)
        {
            if (group == null) return;
            group.alpha = on ? 1f : 0f;
            group.blocksRaycasts = group.interactable = on;
            if (!on) pulse = -1;
        }

        private void Update()
        {
            if (pulse < 0 || dayTiles == null || pulse >= dayTiles.Length || dayTiles[pulse] == null) return;
            dayTiles[pulse].transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }
}
