using Jumpline.Core;
using Jumpline.Player;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Her hızlanmada (30 sn) ekranın üstünde kısa "HIZLANDI!" yazısı: büyüyüp söner.</summary>
    public class SpeedBanner : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.1f)] private float duration = 1.3f;

        private float shownAt = -10f;

        private void OnEnable()
        {
            if (player != null) player.SpeedLevelChanged += Show;
            if (label != null) label.alpha = 0f;
        }

        private void OnDisable()
        {
            if (player != null) player.SpeedLevelChanged -= Show;
        }

        private void Show(int level)
        {
            if (label == null) return;
            label.text = Localization.Format("SPEED UP! x{0}", player.SpeedFactor.ToString("0.0"));
            shownAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (label == null) return;
            float t = (Time.unscaledTime - shownAt) / duration;
            if (t > 1f) { if (label.alpha > 0f) label.alpha = 0f; return; }
            label.alpha = t < 0.15f ? t / 0.15f : 1f - Mathf.InverseLerp(0.6f, 1f, t);
            float s = 1f + 0.25f * Mathf.Sin(Mathf.Clamp01(t / 0.3f) * Mathf.PI);
            label.transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
