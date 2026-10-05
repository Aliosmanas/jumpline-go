using UnityEngine;

namespace Jumpline.CameraSystem
{
    /// <summary>Üçüncü şahıs takip: hedefin arkasında sabit uzaklık; yükseklikte yumuşak (zıplamada ani sarsıntı yok).</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 4f, -7f);
        [SerializeField, Min(0f)] private float heightSmoothing = 4f;
        [Tooltip("Zıplama yüksekliğinin ne kadarını kamera izlesin (0 = hiç).")]
        [SerializeField, Range(0f, 1f)] private float heightFollow = 0.4f;

        [Tooltip("Şerit değişiminde yatay hareketin ne kadarını kamera izlesin (0 = sabit, 1 = tam).")]
        [SerializeField, Range(0f, 1f)] private float sideFollow = 0.6f;
        [SerializeField, Min(0f)] private float sideSmoothing = 8f;

        private float smoothedY;
        private float smoothedX;

        private void Start()
        {
            if (target == null) return;
            smoothedY = target.position.y * heightFollow;
            smoothedX = target.position.x * sideFollow;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float k = 1f - Mathf.Exp(-heightSmoothing * Time.deltaTime);
            smoothedY = Mathf.Lerp(smoothedY, target.position.y * heightFollow, k);
            smoothedX = Mathf.Lerp(smoothedX, target.position.x * sideFollow, 1f - Mathf.Exp(-sideSmoothing * Time.deltaTime));
            transform.position = new Vector3(smoothedX + offset.x, smoothedY + offset.y, target.position.z + offset.z);
        }
    }
}
