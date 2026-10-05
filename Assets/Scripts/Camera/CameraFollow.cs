using UnityEngine;

namespace Jumpline.CameraSystem
{
    /// <summary>Üçüncü şahıs takip: hedefin arkasında sabit uzaklık; yükseklikte yumuşak (zıplamada ani sarsıntı yok).</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 3f, -5.5f);
        [SerializeField, Min(0f)] private float heightSmoothing = 4f;
        [Tooltip("Zıplama yüksekliğinin ne kadarını kamera izlesin (0 = hiç).")]
        [SerializeField, Range(0f, 1f)] private float heightFollow = 0.4f;

        private float smoothedY;

        private void Start()
        {
            if (target != null) smoothedY = target.position.y;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float k = 1f - Mathf.Exp(-heightSmoothing * Time.deltaTime);
            smoothedY = Mathf.Lerp(smoothedY, target.position.y * heightFollow, k);
            transform.position = new Vector3(target.position.x + offset.x, smoothedY + offset.y, target.position.z + offset.z);
        }
    }
}
