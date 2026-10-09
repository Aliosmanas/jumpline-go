using Jumpline.Boss;
using Jumpline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Canavar kovalaması arayüzü: kalpler, mesafe göstergesi (koşucu ←→ canavar), "KAÇIYOR!" uyarısı, faz duyurusu
    /// ve atma düğmesi (yalnız elde nesne varken belirir; nesnenin ikonu + sayısı). Canavar yoksa kendini gizler.
    /// </summary>
    public class BossHud : MonoBehaviour
    {
        [SerializeField] private BossChase boss;
        [SerializeField] private ThrowSystem throws;
        [SerializeField] private GameObject root;

        [Header("Kalpler")]
        [SerializeField] private Image[] hearts;
        [SerializeField] private Color heartFull = Color.white;
        [SerializeField] private Color heartEmpty = new Color(0.35f, 0.32f, 0.4f, 0.55f);

        [Header("Mesafe")]
        [Tooltip("Dolgu: sola yaslı (pivot 0). Mesafe arttıkça uzar.")]
        [SerializeField] private RectTransform meterFill;
        [SerializeField] private Image meterFillImage;
        [SerializeField] private RectTransform bossMarker;
        [SerializeField, Min(10f)] private float meterWidth = 520f;
        [SerializeField] private Color nearColor = new Color(0.33f, 0.81f, 1f);
        [SerializeField] private Color farColor = new Color(0.95f, 0.25f, 0.22f);
        [SerializeField] private TMP_Text warningLabel;

        [Header("Duyuru")]
        [SerializeField] private TMP_Text announceLabel;
        [SerializeField, Min(0.2f)] private float announceTime = 2.2f;

        [Header("Atma düğmesi")]
        [SerializeField] private Button throwButton;
        [SerializeField] private CanvasGroup throwGroup;
        [SerializeField] private Image throwIcon;
        [SerializeField] private TMP_Text throwCount;
        [SerializeField, Min(0.01f)] private float popTime = 0.18f;

        private float announceUntil = -1f;
        private float shownAt = -1f;
        private bool shown;
        private float heartPunchAt = -10f;
        private int heartPunchIndex = -1;

        private void Start()
        {
            bool active = boss != null && boss.gameObject.activeSelf;
            if (root != null) root.SetActive(active);
            if (!active) { enabled = false; return; }
            boss.HeartsChanged += OnHearts;
            boss.PhaseChanged += OnPhase;
            boss.Dodged += () => Announce(Localization.Get("DODGED!"), 1.0f);
            if (throws != null) throws.Changed += RefreshThrow;
            if (throwButton != null) throwButton.onClick.AddListener(() => { if (throws != null) throws.Throw(); });
            if (announceLabel != null) announceLabel.alpha = 0f;
            OnHearts(boss.Hearts, boss.MaxHearts);
            RefreshThrow();
            SetThrowVisible(false, true);
        }

        private void OnDestroy()
        {
            if (boss == null) return;
            boss.HeartsChanged -= OnHearts;
            boss.PhaseChanged -= OnPhase;
        }

        private void OnHearts(int left, int max)
        {
            if (hearts == null) return;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                hearts[i].gameObject.SetActive(i < max);
                bool wasFull = hearts[i].color == heartFull;
                hearts[i].color = i < left ? heartFull : heartEmpty;
                if (wasFull && i >= left) { heartPunchIndex = i; heartPunchAt = Time.unscaledTime; }
            }
        }

        private void OnPhase(BossChase.Phase phase)
        {
            switch (phase)
            {
                case BossChase.Phase.Rooftop: Announce(Localization.Get("TO THE ROOFTOPS! FOLLOW IT!"), announceTime); break;
                case BossChase.Phase.Fury: Announce(Localization.Get("IT'S FURIOUS! WATCH ITS DODGE!"), announceTime); break;
                case BossChase.Phase.LastHeart: Announce(Localization.Get("TO THE BRIDGE! LAST HEART!"), announceTime); break;
            }
        }

        private void Announce(string text, float time)
        {
            if (announceLabel == null) return;
            announceLabel.text = text;
            announceUntil = Time.unscaledTime + time;
        }

        private void RefreshThrow()
        {
            if (throws == null) return;
            int n = throws.Count;
            if (throwIcon != null && throws.CurrentIcon != null) throwIcon.sprite = throws.CurrentIcon;
            if (throwCount != null)
            {
                throwCount.text = n > 1 ? "x" + n : "";
                throwCount.gameObject.SetActive(n > 1);
            }
            SetThrowVisible(n > 0, false);
        }

        private void SetThrowVisible(bool visible, bool instant)
        {
            if (throwGroup == null) return;
            if (visible && !shown) shownAt = Time.unscaledTime;
            shown = visible;
            throwGroup.interactable = throwGroup.blocksRaycasts = visible;
            if (instant)
            {
                throwGroup.alpha = visible ? 1f : 0f;
                throwGroup.transform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (boss == null) return;
            float t = Time.unscaledTime;

            // mesafe göstergesi
            float k = boss.Gap01;
            if (meterFill != null) meterFill.sizeDelta = new Vector2(Mathf.Max(8f, meterWidth * k), meterFill.sizeDelta.y);
            if (meterFillImage != null) meterFillImage.color = Color.Lerp(nearColor, farColor, Mathf.SmoothStep(0.35f, 1f, k));
            if (bossMarker != null)
            {
                Vector2 a = bossMarker.anchoredPosition;
                a.x = Mathf.Lerp(a.x, -meterWidth * 0.5f + meterWidth * k, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
                bossMarker.anchoredPosition = a;
                float pulse = boss.Warning ? 1f + 0.12f * Mathf.Sin(t * 14f) : 1f;
                bossMarker.localScale = Vector3.one * pulse;
            }
            if (warningLabel != null)
            {
                bool warn = boss.Warning;
                warningLabel.gameObject.SetActive(warn);
                if (warn) warningLabel.alpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 6f));
            }

            // kalp kırılma: kısa büyüyüp sönme
            if (heartPunchIndex >= 0 && hearts != null && heartPunchIndex < hearts.Length)
            {
                float u = (t - heartPunchAt) / 0.45f;
                hearts[heartPunchIndex].transform.localScale = Vector3.one * (u < 1f ? 1f + 0.6f * Mathf.Sin(u * Mathf.PI) : 1f);
                if (u >= 1f) heartPunchIndex = -1;
            }

            // duyuru
            if (announceLabel != null)
            {
                float left = announceUntil - t;
                announceLabel.alpha = left > 0f ? Mathf.Clamp01(left / 0.35f) : 0f;
                announceLabel.transform.localScale = Vector3.one * (left > 0f ? Mathf.Lerp(1f, 1.08f, Mathf.Clamp01(left - announceTime + 0.25f) * 4f) : 1f);
            }

            // atma düğmesi: belirirken zıplar, hazırken nefes alır
            if (throwGroup != null)
            {
                float target = shown ? 1f : 0f;
                throwGroup.alpha = Mathf.MoveTowards(throwGroup.alpha, target, Time.unscaledDeltaTime / popTime);
                float s = 1f;
                if (shown)
                {
                    float u = (t - shownAt) / (popTime * 2f);
                    s = u < 1f ? Mathf.Lerp(0.6f, 1f, u) + 0.18f * Mathf.Sin(u * Mathf.PI) : 1f + 0.04f * Mathf.Sin(t * 5f);
                }
                throwGroup.transform.localScale = Vector3.one * s;
            }
        }
    }
}
