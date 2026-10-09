using System.Collections;
using Jumpline.CameraSystem;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Boss
{
    /// <summary>
    /// Bölüm 1 finali — On Gözlü Köprü. Canavarın son kalbinde koşucu ve canavar flaşla köprüye geçer (temiz arena:
    /// sıradan engel yok, canavar yine engel fırlatır, en fazla 2 kez kaçınır — BossChase.finalMaxDodges).
    /// Son isabet: kontrol alınır, ağır çekim, kamera yandan Tepegöz'ün korkuluğun üstünden Dicle'ye düşüşünü gösterir
    /// (su sıçraması), sonra kamera koşucuya döner ve bölüm biter (BossChase.FinishDefeat → LevelFlowController).
    /// </summary>
    public class BossFinale : MonoBehaviour
    {
        [SerializeField] private BossChase boss;
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [SerializeField] private CameraFollow cameraFollow;
        [SerializeField] private ThrowSystem throws;

        [Header("Köprüye geçiş")]
        [Tooltip("Son kalp düşünce köprüye geçişe kadar bekleme (sn) — duyuru okunur.")]
        [SerializeField, Min(0f)] private float arenaDelay = 1.6f;

        [Header("Düşüş sinematiği")]
        [SerializeField, Range(0.05f, 1f)] private float slowMo = 0.4f;
        [Tooltip("Canavarın devrilip korkuluğa yığılma süresi (gerçek sn).")]
        [SerializeField, Min(0.1f)] private float stumbleTime = 1.0f;
        [Tooltip("Korkuluktan suya düşüş süresi (gerçek sn).")]
        [SerializeField, Min(0.1f)] private float fallTime = 1.4f;
        [Tooltip("Korkuluk (köprü kenarı) orta şeritten uzaklığı (m).")]
        [SerializeField, Min(1f)] private float edgeDistance = 5.2f;
        [Tooltip("Su yüzeyi köprü yüzeyinin bu kadar altında (m).")]
        [SerializeField, Min(1f)] private float waterDepth = 16f;
        [SerializeField] private Vector3 cameraOffset = new Vector3(-9f, 3.5f, -6f);
        [SerializeField, Min(0f)] private float afterSplash = 1.3f;
        [SerializeField] private ParticleSystem splashFx;
        [Tooltip("Sinematikte gizlenen arayüz (HUD, canavar HUD'ı).")]
        [SerializeField] private GameObject[] hideDuringCinematic;

        private bool arenaStarted;

        public event System.Action ArenaEntered;

        private void Awake()
        {
            if (boss == null || !boss.gameObject.activeSelf) { gameObject.SetActive(false); return; }
            boss.PhaseChanged += OnPhase;
            boss.Defeating += OnDefeating;
        }

        private void OnDestroy()
        {
            if (boss == null) return;
            boss.PhaseChanged -= OnPhase;
            boss.Defeating -= OnDefeating;
        }

        private void OnPhase(BossChase.Phase phase)
        {
            if (phase != BossChase.Phase.LastHeart || arenaStarted || switcher == null) return;
            arenaStarted = true;
            boss.Invulnerable = true;   // köprüye varmadan son kalp düşmesin
            StartCoroutine(GoToArena());
        }

        private IEnumerator GoToArena()
        {
            yield return new WaitForSeconds(arenaDelay);
            while (player.IsDead || boss.IsEscaped) yield return null;   // devam paneli açıksa bekle
            switcher.EnterArena();
            while (!switcher.InArena) yield return null;
            boss.ResetForArena();
            boss.Invulnerable = false;
            if (throws != null) throws.SetFinale(true);
            ArenaEntered?.Invoke();
        }

        private void OnDefeating()
        {
            if (switcher == null || !switcher.InArena) return;   // köprü yoksa BossChase kendi devrilmesini oynatır
            StartCoroutine(FallCinematic());
        }

        private IEnumerator FallCinematic()
        {
            boss.ExternalControl = true;
            player.InputEnabled = false;
            if (hideDuringCinematic != null) foreach (var g in hideDuringCinematic) if (g != null) g.SetActive(false);
            if (throws != null) throws.enabled = false;
            Time.timeScale = slowMo;
            Transform bt = boss.transform;
            Vector3 start = bt.position;
            float ground = player.GroundHeight;
            float side = start.x >= player.LaneOriginX ? 1f : -1f;          // yakın korkuluğa
            if (Mathf.Abs(start.x - player.LaneOriginX) < 0.5f) side = Random.value < 0.5f ? -1f : 1f;
            Vector3 edge = new Vector3(player.LaneOriginX + side * edgeDistance, ground, start.z + 6f);
            Camera cam = cameraFollow != null ? cameraFollow.GetComponent<Camera>() : Camera.main;
            if (cameraFollow != null) cameraFollow.enabled = false;
            Vector3 camFrom = cam.transform.position;
            Quaternion camRotFrom = cam.transform.rotation;
            Vector3 camOffset = new Vector3(cameraOffset.x * -side, cameraOffset.y, cameraOffset.z);

            // 1) sendeleyip korkuluğa yığılır (koşucu ağır çekimde koşmaya devam eder)
            for (float t = 0f; t < stumbleTime; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / stumbleTime);
                bt.position = Vector3.Lerp(start, edge, k) + Vector3.forward * (player.RunSpeed * slowMo * t * 0.5f);
                if (boss.Visual != null) boss.Visual.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(0f, side * 70f, side * -15f), k);
                CameraTo(cam, camFrom, camRotFrom, bt.position, camOffset, Mathf.SmoothStep(0f, 1f, t / (stumbleTime * 0.7f)));
                yield return null;
            }
            // 2) korkuluğun üstünden devrilip suya düşer
            Vector3 top = bt.position;
            bool splashed = false;
            // düşüş kamerası: korkuluğun dışında, suya yakın sabit nokta; köprüye ve düşen canavara bakar
            Vector3 fallCamPos = new Vector3(top.x + side * 17f, ground - waterDepth * 0.55f, top.z - 13f);
            Vector3 camStartPos = cam.transform.position;
            Quaternion camStartRot = cam.transform.rotation;
            for (float t = 0f; t < fallTime; t += Time.unscaledDeltaTime)
            {
                float k = t / fallTime;
                float drop = waterDepth * k * k;
                bt.position = top + new Vector3(side * 2.8f * Mathf.Sqrt(k), 1.2f * Mathf.Sin(k * Mathf.PI) - drop, 0f);
                if (boss.Visual != null) boss.Visual.localRotation = Quaternion.Euler(k * 140f, side * 70f, side * (-15f - 60f * k));
                float ck = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (fallTime * 0.35f)));
                Vector3 look = Vector3.Lerp(top, bt.position, 0.75f) + Vector3.up * 1.5f;
                cam.transform.SetPositionAndRotation(Vector3.Lerp(camStartPos, fallCamPos, ck),
                    Quaternion.Slerp(camStartRot, Quaternion.LookRotation(look - fallCamPos), ck));
                if (!splashed && drop > waterDepth - 1.5f)
                {
                    splashed = true;
                    if (splashFx != null)
                    {
                        splashFx.transform.position = new Vector3(bt.position.x, ground - waterDepth + 0.2f, bt.position.z);
                        splashFx.Emit(160);
                    }
                    if (cameraFollow != null) cameraFollow.Shake(0.8f);
                    Haptics.Pulse();
                }
                yield return null;
            }
            if (boss.Visual != null) boss.Visual.gameObject.SetActive(false);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(afterSplash);
            // 3) kamera koşucuya döner, bölüm biter
            if (cameraFollow != null) cameraFollow.enabled = true;
            if (cameraFollow != null) cameraFollow.Snap();
            boss.FinishDefeat();
        }

        private static void CameraTo(Camera cam, Vector3 fromPos, Quaternion fromRot, Vector3 target, Vector3 offset, float k)
        {
            Vector3 pos = target + offset;
            Quaternion rot = Quaternion.LookRotation((target + Vector3.up * 1.6f) - pos);
            cam.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, pos, k), Quaternion.Slerp(fromRot, rot, k));
        }
    }
}
