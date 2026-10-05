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
        [Header("Hız hissi")]
        [Tooltip("Hız çarpanındaki her +1 için görüş açısı artışı (derece).")]
        [SerializeField, Min(0f)] private float fovPerSpeed = 14f;
        [SerializeField, Min(0f)] private float maxFovBoost = 12f;
        [Tooltip("Koşarken hafif sarsıntı (m), hızla artar.")]
        [SerializeField, Min(0f)] private float shake = 0.025f;

        private PlayerController player;
        private Camera cam;
        private float baseFov;
        private float smoothedY;
        private float smoothedX;

        private float BaseX => player != null ? player.LaneOriginX : 0f;
        private float BaseY => player != null ? player.GroundHeight : 0f;

        private void Start()
        {
            if (target == null) return;
            player = target.GetComponent<PlayerController>();
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
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
            if (player == null) return;
            float factor = player.IsDead ? 1f : player.SpeedFactor;
            if (cam != null)
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + Mathf.Min(maxFovBoost, (factor - 1f) * fovPerSpeed), 1f - Mathf.Exp(-3f * dt));
            if (!player.IsDead && !player.OnZipline)
            {
                float a = shake * factor, t = Time.time * 18f;
                transform.position += new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * a, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * a, 0f);
            }
        }

        private void Apply()
        {
            transform.position = new Vector3(BaseX + smoothedX + offset.x, BaseY + smoothedY + offset.y, target.position.z + offset.z);
        }
    }
}
