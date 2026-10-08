using System.Collections;
using Jumpline.CameraSystem;
using Jumpline.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jumpline.Level
{
    /// <summary>
    /// Sokak (gündüz) ↔ çatı haritası (gece) geçişi: portal flaşı, ışık/pus/gökyüzü ayarları, koşucuyu ışınlama.
    /// Çatı modülleri sokak modülleriyle aynı z'de ilerler (StreetRecycler), ışınlanmada z korunur; zipline boşluğuna
    /// denk gelirse karşı çatıya ötelenir. Geliştirici kısayolu (editör / debug build): T = dünya değiştir.
    /// </summary>
    public class WorldSwitcher : MonoBehaviour
    {
        [System.Serializable]
        public struct WorldLook
        {
            public Color sky;
            public Color fog;
            public float fogStart, fogEnd;
            public Color ambientSky, ambientEquator, ambientGround;
            public Color sunColor;
            public float sunIntensity;
            public Vector3 sunEuler;
            [Tooltip("Gökyüzü: true = RenderSettings.skybox (gün batımı), false = düz renk (gece).")]
            public bool skybox;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private Camera cam;
        [SerializeField] private Light sun;
        [SerializeField] private Image flash;

        [Header("Dünyalar")]
        [SerializeField] private float streetOriginX = 0f;
        [SerializeField] private float streetGround = 0f;
        [Tooltip("Sokak modüllerinin kökü — çatıdayken kapatılır (performans).")]
        [SerializeField] private Transform streetRoot;
        [Tooltip("Dünyayla birlikte açılıp kapanan ek kökler (engeller).")]
        [SerializeField] private GameObject[] streetExtras;
        [SerializeField] private GameObject[] rooftopExtras;
        [Tooltip("Çatı modüllerinin kökü (x = orta şerit, y = çatı yüzeyi, z = modül hizası).")]
        [SerializeField] private Transform rooftopRoot;
        [SerializeField, Min(1f)] private float rooftopModuleLength = 80f;
        [Tooltip("Çatıda ışınlanmanın yasak olduğu modül içi aralık (zipline boşluğu + tutunma payı).")]
        [SerializeField] private Vector2 rooftopUnsafeZ = new Vector2(38f, 68f);
        [SerializeField] private WorldLook streetLook;
        [SerializeField] private WorldLook rooftopLook;

        [Header("Geçiş")]
        [SerializeField, Min(0.01f)] private float flashIn = 0.12f;
        [SerializeField, Min(0.01f)] private float flashOut = 0.45f;
        [SerializeField] private Color flashColor = new Color(0.55f, 0.85f, 1f, 1f);

        private bool busy;

        public bool InStreet { get; private set; } = true;
        public float StreetOriginX => streetOriginX;
        public float StreetGround => streetGround;
        public float RooftopOriginX => rooftopRoot != null ? rooftopRoot.position.x : 0f;
        public float RooftopGround => rooftopRoot != null ? rooftopRoot.position.y : 0f;
        public float RooftopRootZ => rooftopRoot != null ? rooftopRoot.position.z : 0f;
        public float RooftopModuleLength => rooftopModuleLength;

        /// <summary>Dünya değişti (true = sokak). Portallar kendi dünyalarında koşucunun önüne yerleşir.</summary>
        public event System.Action<bool> WorldChanged;

        /// <summary>Elmasla devam için güvenli nokta: bulunulan dünyanın zemininde; çatıda boşluktaysa karşı çatıya.</summary>
        public Vector3 SafeRevivePoint(Vector3 p)
        {
            if (InStreet) return new Vector3(p.x, streetGround, p.z);
            return new Vector3(p.x, RooftopGround, SafeRooftopZ(p.z));
        }

        /// <summary>Çatıda verilen z zipline boşluğuna denk geliyorsa karşı çatıya öteler.</summary>
        public float SafeRooftopZ(float z)
        {
            float rootZ = rooftopRoot != null ? rooftopRoot.position.z : 0f;
            float rel = Mathf.Repeat(z - rootZ, rooftopModuleLength);
            if (rel > rooftopUnsafeZ.x && rel < rooftopUnsafeZ.y) z += rooftopUnsafeZ.y - rel;
            return z;
        }

        private void Start()
        {
            Apply(streetLook);
            SetWorldActive(true);
            if (flash != null) flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
        }

        private void Update()
        {
            if (!Debug.isDebugBuild || Keyboard.current == null) return;
            if (Keyboard.current.tKey.wasPressedThisFrame)
            {
                if (InStreet) EnterRooftop();
                else ExitToStreet();
            }
        }

        public void EnterRooftop()
        {
            if (busy || !InStreet || rooftopRoot == null || player.IsDead) return;
            StartCoroutine(Switch(false));
        }

        public void ExitToStreet()
        {
            if (busy || InStreet || player.IsDead) return;
            StartCoroutine(Switch(true));
        }

        private IEnumerator Switch(bool toStreet)
        {
            busy = true;
            yield return Fade(0f, 1f, flashIn);
            Vector3 p = player.transform.position;
            if (toStreet)
            {
                player.Teleport(new Vector3(streetOriginX, streetGround, p.z), streetGround, streetOriginX);
                Apply(streetLook);
            }
            else
            {
                Vector3 root = rooftopRoot.position;
                float z = SafeRooftopZ(p.z);
                player.Teleport(new Vector3(root.x, root.y, z), root.y, root.x);
                Apply(rooftopLook);
            }
            InStreet = toStreet;
            SetWorldActive(toStreet);   // görünmeyen dünya çizilmez, gölge hesaplamaz
            if (cameraFollow != null) cameraFollow.Snap();
            WorldChanged?.Invoke(toStreet);
            yield return Fade(1f, 0f, flashOut);
            busy = false;
        }

        private void SetWorldActive(bool street)
        {
            if (streetRoot != null) streetRoot.gameObject.SetActive(street);
            if (rooftopRoot != null) rooftopRoot.gameObject.SetActive(!street);
            if (streetExtras != null) foreach (var g in streetExtras) if (g != null) g.SetActive(street);
            if (rooftopExtras != null) foreach (var g in rooftopExtras) if (g != null) g.SetActive(!street);
        }

        private IEnumerator Fade(float from, float to, float time)
        {
            if (flash == null) yield break;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, Mathf.Lerp(from, to, t / time));
                yield return null;
            }
            flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, to);
        }

        private void Apply(WorldLook look)
        {
            if (cam != null)
            {
                cam.backgroundColor = look.sky;
                cam.clearFlags = look.skybox && RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            }
            RenderSettings.fogColor = look.fog;
            RenderSettings.fogStartDistance = look.fogStart;
            RenderSettings.fogEndDistance = look.fogEnd;
            RenderSettings.ambientSkyColor = look.ambientSky;
            RenderSettings.ambientEquatorColor = look.ambientEquator;
            RenderSettings.ambientGroundColor = look.ambientGround;
            if (sun != null)
            {
                sun.color = look.sunColor;
                sun.intensity = look.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(look.sunEuler);
            }
        }
    }
}
