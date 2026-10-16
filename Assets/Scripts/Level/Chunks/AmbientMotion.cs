using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Hareketli süs (fizik yok, yalnız transform / ışık): sallanan kumaş ve bayrak, yanıp sönen fener, süzülen kuş,
    /// dönen tabela. Her kopya rastgele evreyle başlar (hepsi aynı anda sallanmasın). Kamera görmüyorsa güncellenmez.
    /// </summary>
    public class AmbientMotion : MonoBehaviour
    {
        public enum Mode { Sway, Flicker, Bob, Spin, Glide }

        [SerializeField] private Mode mode = Mode.Sway;
        [Tooltip("Sway: derece · Bob: m · Spin: derece/sn · Glide: daire yarıçapı (m) · Flicker: ışık oranı (0..1).")]
        [SerializeField] private float amount = 6f;
        [Tooltip("Döngü/sn.")]
        [SerializeField] private float speed = 0.6f;
        [Tooltip("Sway ekseni (yerel).")]
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private Light flickerLight;

        private Vector3 basePos;
        private Quaternion baseRot;
        private float baseIntensity, phase;
        private Renderer anyRenderer;

        public void Configure(Mode m, float amt, float spd, Vector3 ax)
        {
            mode = m; amount = amt; speed = spd; axis = ax;
        }

        private void OnEnable()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
            phase = Random.value * Mathf.PI * 2f;
            anyRenderer = GetComponentInChildren<Renderer>();
            if (flickerLight == null) flickerLight = GetComponentInChildren<Light>();
            if (flickerLight != null) baseIntensity = flickerLight.intensity;
        }

        private void OnDisable()
        {
            transform.localPosition = basePos;
            transform.localRotation = baseRot;
        }

        private void Update()
        {
            if (anyRenderer != null && !anyRenderer.isVisible && flickerLight == null) return;
            float t = Time.time * speed * Mathf.PI * 2f + phase;
            switch (mode)
            {
                case Mode.Sway:
                    transform.localRotation = baseRot * Quaternion.AngleAxis(Mathf.Sin(t) * amount, axis);
                    break;
                case Mode.Bob:
                    transform.localPosition = basePos + Vector3.up * (Mathf.Sin(t) * amount);
                    break;
                case Mode.Spin:
                    transform.localRotation = baseRot * Quaternion.AngleAxis(Time.time * amount + phase * Mathf.Rad2Deg, axis);
                    break;
                case Mode.Glide:
                    transform.localPosition = basePos + new Vector3(Mathf.Cos(t), Mathf.Sin(t * 2f) * 0.15f, Mathf.Sin(t)) * amount;
                    transform.localRotation = baseRot * Quaternion.Euler(0f, -t * Mathf.Rad2Deg, Mathf.Sin(t) * 15f);
                    break;
                case Mode.Flicker:
                    if (flickerLight != null)
                        flickerLight.intensity = baseIntensity * (1f - amount * 0.5f * (1f + Mathf.PerlinNoise(t, phase)) * 0.5f);
                    break;
            }
        }
    }
}
