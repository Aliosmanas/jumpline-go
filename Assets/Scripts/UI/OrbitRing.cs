using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Butonların etrafındaki ışık halkası: çok yavaş döner, parlaklığı hafifçe nefes alır.</summary>
    [RequireComponent(typeof(Graphic))]
    public class OrbitRing : MonoBehaviour
    {
        [Tooltip("Derece / saniye. Göz yormaması için düşük tut.")]
        [SerializeField] private float speed = 8f;
        [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.25f;
        [SerializeField, Min(0.1f)] private float pulsePeriod = 4f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private Graphic graphic;
        private float baseAlpha;
        private float baseAngle;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            baseAlpha = graphic.color.a;
            baseAngle = transform.localEulerAngles.z;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + t * speed);
            float pulse = 1f - pulseAmount * (0.5f + 0.5f * Mathf.Sin((t / pulsePeriod + phase) * Mathf.PI * 2f));
            Color c = graphic.color;
            c.a = baseAlpha * pulse;
            graphic.color = c;
        }
    }
}
