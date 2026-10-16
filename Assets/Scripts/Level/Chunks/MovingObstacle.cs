using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Hareketli engel: koşucu <see cref="activateDistance"/> kadar yaklaşınca şeridinde koşucuya doğru sabit hızla gelir
    /// (Subway'deki gelen tren). Fizik yok; yalnız konum. Koşucunun arkasına geçince durur.
    /// </summary>
    public class MovingObstacle : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 3f;
        [SerializeField, Min(5f)] private float activateDistance = 45f;
        [Tooltip("Gelirken titreyen far / ışık (isteğe bağlı).")]
        [SerializeField] private Light headLight;

        private PlayerController player;
        private bool moving;

        public void Configure(float speed, float activate) { moveSpeed = speed; activateDistance = activate; }

        /// <summary>Havuzdan her yerleşişte: koşucuyu tanı, dur ve bekle.</summary>
        public void Arm(PlayerController p)
        {
            player = p;
            moving = false;
            if (headLight != null) headLight.enabled = false;
        }

        private void Update()
        {
            if (player == null || player.IsDead) return;
            float dz = transform.position.z - player.transform.position.z;
            if (dz < -5f) { moving = false; return; }
            if (!moving && dz < activateDistance)
            {
                moving = true;
                if (headLight != null) headLight.enabled = true;
            }
            if (moving) transform.position += Vector3.back * (moveSpeed * Time.deltaTime);
        }
    }
}
