using Jumpline.Player;
using UnityEngine;

namespace Jumpline.CameraSystem
{
    /// <summary>
    /// Üçüncü şahıs takip: hedefin arkasında sabit uzaklık; yükseklik ve yan hareket yumuşak (zıplamada ani sarsıntı yok).
    /// Hedefte PlayerController varsa zemin yüksekliği ve orta şerit referans alınır (çatı dünyası için).
    /// </summary>
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

        private PlayerController player;
        private float smoothedY;
        private float smoothedX;

        private float BaseX => player != null ? player.LaneOriginX : 0f;
        private float BaseY => player != null ? player.GroundHeight : 0f;

        private void Start()
        {
            if (target == null) return;
            player = target.GetComponent<PlayerController>();
            Snap();
        }

        /// <summary>Yumuşatmayı atla (ışınlanma sonrası).</summary>
        public void Snap()
        {
            if (target == null) return;
            smoothedY = (target.position.y - BaseY) * heightFollow;
            smoothedX = (target.position.x - BaseX) * sideFollow;
            Apply();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            smoothedY = Mathf.Lerp(smoothedY, (target.position.y - BaseY) * heightFollow, 1f - Mathf.Exp(-heightSmoothing * dt));
            smoothedX = Mathf.Lerp(smoothedX, (target.position.x - BaseX) * sideFollow, 1f - Mathf.Exp(-sideSmoothing * dt));
            Apply();
        }

        private void Apply()
        {
            transform.position = new Vector3(BaseX + smoothedX + offset.x, BaseY + smoothedY + offset.y, target.position.z + offset.z);
        }
    }
}
