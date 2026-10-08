using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Tek şeritte duran portal (Blender/scripts/build_rooftops.py portal()).
    /// Yukarı portal (sokakta, mavi): o şeritten geçen koşucu çatı haritasına çıkar.
    /// Aşağı portal (çatıda, pembe): o şeritten geçen koşucu ana sokağa iner.
    /// Geçince (girse de kaçırsa da) ileride rastgele bir şeritte yeniden belirir; dünya değişince koşucunun önüne gelir.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [Tooltip("true: sokaktan çatıya çıkar. false: çatıdan sokağa iner.")]
        [SerializeField] private bool leadsToRooftop = true;
        [SerializeField] private Transform spinner;
        [SerializeField] private float spinSpeed = -160f;
        [Tooltip("Koşucunun (dünyaya girince) ilk portala kadar koşacağı mesafe.")]
        [SerializeField, Min(5f)] private float firstDistance = 160f;
        [Tooltip("Sonraki portallar arası mesafe.")]
        [SerializeField, Min(10f)] private float spacing = 420f;
        [Tooltip("İlk portal şeridi: -1 sol, 0 orta, 1 sağ, -2 rastgele.")]
        [SerializeField, Range(-2, 1)] private int startLane = 0;
        [Tooltip("Renk (alfa 0 = modelin kendi rengi). Girdap, ışık ve parçacıklara uygulanır.")]
        [SerializeField] private Color tint = new Color(0f, 0f, 0f, 0f);

        private int lane;

        public int Lane => lane;
        private bool InMyWorld => switcher == null ? leadsToRooftop : switcher.InStreet == leadsToRooftop;

        private void OnEnable()
        {
            if (switcher != null) switcher.WorldChanged += OnWorldChanged;
        }

        private void OnDisable()
        {
            if (switcher != null) switcher.WorldChanged -= OnWorldChanged;
        }

        private void Start()
        {
            ApplyTint();
            if (player == null) return;
            lane = startLane < -1 ? Random.Range(-1, 2) : startLane;
            Place(player.transform.position.z + firstDistance);
        }

        private void Update()
        {
            if (spinner != null) spinner.Rotate(Vector3.forward, spinSpeed * Time.deltaTime, Space.World);
            if (player == null || !InMyWorld) return;
            Vector3 p = player.transform.position;
            if (p.z < transform.position.z) return;
            if (Mathf.Abs(p.x - transform.position.x) < player.LaneWidth * 0.5f && p.z - transform.position.z < 3f && switcher != null)
            {
                if (leadsToRooftop) switcher.EnterRooftop();
                else switcher.ExitToStreet();
            }
            lane = Random.Range(-1, 2);
            Place(p.z + spacing);
        }

        private void OnWorldChanged(bool street)
        {
            if (player == null || street != leadsToRooftop) return;   // benim dünyama girildi
            lane = Random.Range(-1, 2);
            Place(player.transform.position.z + firstDistance);
        }

        private void Place(float z)
        {
            float originX = 0f, ground = 0f;
            if (switcher != null)
            {
                originX = leadsToRooftop ? switcher.StreetOriginX : switcher.RooftopOriginX;
                ground = leadsToRooftop ? switcher.StreetGround : switcher.RooftopGround;
                if (!leadsToRooftop) z = switcher.SafeRooftopZ(z);   // zipline boşluğuna konmasın
            }
            transform.position = new Vector3(originX + lane * player.LaneWidth, ground, z);
        }

        private void ApplyTint()
        {
            if (tint.a <= 0f) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.name.StartsWith("Portal_Core")) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_BaseColor", tint);
                block.SetColor("_EmissionColor", tint * 3.5f);
                r.SetPropertyBlock(block);
            }
            foreach (var l in GetComponentsInChildren<Light>(true)) l.color = tint;
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(tint, Color.white);
            }
        }
    }
}
