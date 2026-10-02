using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Bir ışığın saydamlığını yavaşça artırıp azaltır.</summary>
    [RequireComponent(typeof(Graphic))]
    public class GlowPulse : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amount = 0.3f;
        [SerializeField, Min(0.1f)] private float period = 3.5f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private Graphic graphic;
        private float baseAlpha;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            baseAlpha = graphic.color.a;
        }

        private void Update()
        {
            float k = 1f - amount * (0.5f + 0.5f * Mathf.Sin((Time.unscaledTime / period + phase) * Mathf.PI * 2f));
            Color c = graphic.color;
            c.a = baseAlpha * k;
            graphic.color = c;
        }
    }
}
