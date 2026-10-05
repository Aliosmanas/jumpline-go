using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Sokakta tek şeritte duran portal (Blender/scripts/build_rooftops.py portal()). Koşucu o şeritten geçerse çatı
    /// haritasına ışınlanır. Geçince (girse de kaçırsa da) ileride rastgele bir şeritte yeniden belirir.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [SerializeField] private Transform spinner;
        [SerializeField] private float spinSpeed = -160f;
        [Tooltip("Koşucunun ilk portala kadar koşacağı mesafe.")]
        [SerializeField, Min(5f)] private float firstDistance = 45f;
        [Tooltip("Sonraki portallar arası mesafe.")]
        [SerializeField, Min(10f)] private float spacing = 140f;
        [Tooltip("Portal şeridi: -1 sol, 0 orta, 1 sağ (Başlangıçta rastgele ise -2).")]
        [SerializeField, Range(-2, 1)] private int startLane = 0;

        private int lane;

        public int Lane => lane;

        private void Start()
        {
            if (player == null) return;
            lane = startLane < -1 ? Random.Range(-1, 2) : startLane;
            Place(player.transform.position.z + firstDistance);
        }

        private void Update()
        {
            if (spinner != null) spinner.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.World);
            if (player == null) return;
            Vector3 p = player.transform.position;
            if (p.z < transform.position.z) return;
            bool inStreet = switcher == null || switcher.InStreet;
            if (inStreet && Mathf.Abs(p.x - transform.position.x) < player.LaneWidth * 0.5f && p.z - transform.position.z < 3f)
                switcher?.EnterRooftop();
            lane = Random.Range(-1, 2);
            Place(p.z + spacing);
        }

        private void Place(float z)
        {
            float origin = switcher != null ? switcher.StreetOriginX : 0f;
            transform.position = new Vector3(origin + lane * player.LaneWidth, switcher != null ? switcher.StreetGround : 0f, z);
        }
    }
}
