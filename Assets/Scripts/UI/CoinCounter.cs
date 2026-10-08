using Jumpline.Level;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Sağ üstte koşu para sayacı: her toplamada sayı güncellenir, ikon kısa bir "zıplama" yapar.</summary>
    public class CoinCounter : MonoBehaviour
    {
        [SerializeField] private CoinField field;
        [SerializeField] private TMP_Text label;
        [SerializeField] private RectTransform icon;
        [SerializeField, Min(0.05f)] private float punchTime = 0.18f;
        [SerializeField] private float punchScale = 1.25f;

        private float punchedAt = -10f;

        private void OnEnable()
        {
            if (field != null) field.Collected += OnCollected;
            if (label != null) label.text = "0";
        }

        private void OnDisable()
        {
            if (field != null) field.Collected -= OnCollected;
        }

        private void OnCollected(int total)
        {
            if (label != null) label.text = total.ToString();
            punchedAt = Time.unscaledTime;
        }

        private void Update()
        {
            if (icon == null) return;
            float t = (Time.unscaledTime - punchedAt) / punchTime;
            float s = t >= 1f ? 1f : 1f + (punchScale - 1f) * Mathf.Sin(t * Mathf.PI);
            icon.localScale = new Vector3(s, s, 1f);
        }
    }
}
