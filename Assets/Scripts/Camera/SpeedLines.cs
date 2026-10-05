using Jumpline.Player;
using UnityEngine;

namespace Jumpline.CameraSystem
{
    /// <summary>
    /// Hız çizgileri: kameranın önünden kenarlara doğru akan parlak çizgiler; koşucu hızlandıkça yoğunlaşır.
    /// Koşucu ölünce ya da halattayken (zipline kendi kıvılcımıyla) azalır.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class SpeedLines : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Taban hızda saniyedeki çizgi sayısı.")]
        [SerializeField, Min(0f)] private float baseRate = 14f;
        [Tooltip("Hız çarpanındaki her +1 için ek çizgi.")]
        [SerializeField, Min(0f)] private float ratePerSpeed = 70f;

        private ParticleSystem ps;

        private void Awake() => ps = GetComponent<ParticleSystem>();

        private void Update()
        {
            if (player == null) return;
            var em = ps.emission;
            em.rateOverTime = player.IsDead ? 0f : baseRate + (player.SpeedFactor - 1f) * ratePerSpeed;
        }
    }
}
