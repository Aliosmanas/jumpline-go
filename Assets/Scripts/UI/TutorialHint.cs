using Jumpline.Player;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>
    /// Koşunun başında kontrolleri hatırlatır (kaydırma yönleri), birkaç saniye sonra söner. Yalnız ilk
    /// <see cref="maxShows"/> koşuda görünür (deneyimli oyuncunun ekranını kapatmasın).
    /// </summary>
    public class TutorialHint : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.5f)] private float showTime = 4f;
        [SerializeField, Min(0.1f)] private float fadeTime = 0.8f;
        [Tooltip("İpucu kaç koşu boyunca gösterilir.")]
        [SerializeField, Min(1)] private int maxShows = 3;
        private const string ShowsKey = "jl.hintShows";

        private float start;

        private void OnEnable()
        {
            start = Time.unscaledTime;
            int shown = PlayerPrefs.GetInt(ShowsKey, 0);
            if (shown >= maxShows)
            {
                if (label != null) label.alpha = 0f;
                enabled = false;
                return;
            }
            PlayerPrefs.SetInt(ShowsKey, shown + 1);
        }

        private void Update()
        {
            if (label == null) return;
            float t = Time.unscaledTime - start;
            label.alpha = t < showTime ? 1f : 1f - Mathf.Clamp01((t - showTime) / fadeTime);
            if (t > showTime + fadeTime) enabled = false;
        }
    }
}
