using System.Collections;
using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Player;
using Jumpline.UI;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Öğretici (ilk kampanya koşusu, ayrı bölüm yok): önündeki ilk uygun engelde oyun yavaşlar, çaycı amca seslenir,
    /// parmak işareti doğru kaydırmayı gösterir; oyuncu hareketi yapınca hız normale döner. Her hareket bir kez öğretilir:
    ///  zıpla (alçak engel) · kay (bariyer/kemer) · şerit değiştir (yüksek engel) · at (elde nesne varken).
    /// Bitince kayda işlenir; sonraki koşularda çıkmaz. Ayarlar → öğreticiyi sıfırla ile yeniden açılabilir.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        private enum Step { Jump, Slide, Lane, Throw }

        [SerializeField] private PlayerController player;
        [SerializeField] private ThrowSystem throws;
        [SerializeField] private DialogueBox dialogue;
        [Tooltip("Parmak/ok işareti (yön göstergesi).")]
        [SerializeField] private RectTransform finger;
        [SerializeField] private CanvasGroup fingerGroup;
        [Tooltip("Atma düğmesi vurgusu (nabız gibi büyür).")]
        [SerializeField] private RectTransform throwButton;
        [Tooltip("Öğretirken zaman ölçeği (neredeyse durur; girdi okunabilsin diye 0 değil).")]
        [SerializeField, Range(0.01f, 1f)] private float slowMo = 0.03f;
        [Tooltip("Engelin öğretildiği mesafe (m): hemen yapılan hareket engeli tam aşacak şekilde (zıplama yayı 2–6.5 m'yi kapsar).")]
        [SerializeField] private Vector2 jumpWindow = new Vector2(3.2f, 5.2f);
        [SerializeField] private Vector2 slideWindow = new Vector2(2.5f, 5.5f);
        [SerializeField] private Vector2 laneWindow = new Vector2(4f, 9f);
        [Tooltip("Oyuncu hiç hareket etmezse bu kadar (gerçek sn) sonra yavaşlatma biter.")]
        [SerializeField, Min(1f)] private float giveUpAfter = 10f;
        [SerializeField, Min(0.5f)] private float fingerTravel = 220f;
        [Tooltip("Koşu başladıktan sonra ilk öğretim için bekleme (sn).")]
        [SerializeField, Min(0f)] private float startDelay = 2.5f;

        private readonly bool[] done = new bool[4];
        private bool busy;
        private float startTime;
        private int laneAtStart;
        private bool acted;

        public static bool Completed => PlayerPrefs.GetInt("jl.tutorialDone", 0) == 1;
        public static void Reset() { PlayerPrefs.DeleteKey("jl.tutorialDone"); PlayerPrefs.Save(); }

        private void Start()
        {
            if (Completed || GameSession.Mode != RunMode.Campaign || player == null) { gameObject.SetActive(false); return; }
            startTime = Time.time;
            if (fingerGroup != null) fingerGroup.alpha = 0f;
            player.Jumped += () => acted |= current == Step.Jump;
            player.SlideStarted += () => acted |= current == Step.Slide;
            if (throws != null) throws.Thrown += () => acted |= current == Step.Throw;
        }

        private Step current;

        private void Update()
        {
            if (busy || player.IsDead || Time.time - startTime < startDelay || Time.timeScale < 0.99f) return;
            if (!done[(int)Step.Throw] && throws != null && throws.Count > 0) { StartCoroutine(Teach(Step.Throw, 0)); return; }
            float g = player.GroundHeight;
            float x = player.LaneOriginX + player.Lane * player.LaneWidth;
            float z = player.transform.position.z;
            // en yakın engeli bul (0.25 m adım); türü ve mesafesi öğretim penceresine uyuyorsa öğret
            for (float d = 1f; d <= laneWindow.y; d += 0.25f)
            {
                float s = ObstacleField.SurfaceAt(x, z + d, g);
                bool ceiling = ObstacleField.HitsCeiling(x, z + d, g, g + 1.7f) && !ObstacleField.HitsCeiling(x, z + d, g, g + 1.0f);
                if (ceiling)
                {
                    if (!done[(int)Step.Slide] && d >= slideWindow.x && d <= slideWindow.y) StartCoroutine(Teach(Step.Slide, 0));
                    return;
                }
                if (s <= g + 0.3f) continue;
                float h = s - g;
                if (h <= 1.45f)
                {
                    if (!done[(int)Step.Jump] && d >= jumpWindow.x && d <= jumpWindow.y) StartCoroutine(Teach(Step.Jump, 0));
                    return;
                }
                if (h > 1.9f && !done[(int)Step.Lane] && d >= laneWindow.x)
                {
                    int dir = FreeSide(z + d, g);
                    if (dir != 0) StartCoroutine(Teach(Step.Lane, dir));
                }
                return;
            }
        }

        /// <summary>Yanda boş şerit (sağ önce): +1 sağ, -1 sol, 0 yok.</summary>
        private int FreeSide(float z, float g)
        {
            foreach (int dir in new[] { 1, -1 })
            {
                int lane = player.Lane + dir;
                if (lane < -1 || lane > 1) continue;
                float x = player.LaneOriginX + lane * player.LaneWidth;
                if (ObstacleField.SurfaceAt(x, z, g) <= g + 0.3f && !ObstacleField.HitsCeiling(x, z, g, g + 1.7f)) return dir;
            }
            return 0;
        }

        private IEnumerator Teach(Step step, int dir)
        {
            busy = true;
            current = step;
            acted = false;
            laneAtStart = player.Lane;
            string line = step switch
            {
                Step.Jump => "Jump, nephew! Swipe up!",
                Step.Slide => "Duck, duck! Swipe down!",
                Step.Throw => "Throw it at his head!",
                _ => dir > 0 ? "Dodge right! Swipe right!" : "Dodge left! Swipe left!",
            };
            if (dialogue != null) dialogue.Say("cayci", line, 2.5f);
            Jumpline.Audio.AudioManager.Play("tea", 0.7f);
            float before = Time.timeScale;
            Time.timeScale = step == Step.Throw ? 0.5f : slowMo;
            Vector2 swipe = step switch
            {
                Step.Jump => Vector2.up,
                Step.Slide => Vector2.down,
                Step.Lane => new Vector2(dir, 0f),
                _ => Vector2.zero,
            };
            float t0 = Time.unscaledTime;
            Vector2 home = finger != null ? finger.anchoredPosition : Vector2.zero;
            while (!acted && Time.unscaledTime - t0 < giveUpAfter && !player.IsDead)
            {
                if (step == Step.Lane && player.Lane != laneAtStart) acted = true;
                float u = Mathf.Repeat((Time.unscaledTime - t0) / 0.9f, 1f);
                if (finger != null && swipe != Vector2.zero)
                {
                    finger.anchoredPosition = home + swipe * (fingerTravel * Mathf.SmoothStep(0f, 1f, u));
                    finger.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(swipe.y, swipe.x) * Mathf.Rad2Deg + 180f);
                    if (fingerGroup != null) fingerGroup.alpha = u < 0.15f ? u / 0.15f : u > 0.8f ? (1f - u) / 0.2f : 1f;
                }
                if (throwButton != null && step == Step.Throw)
                    throwButton.localScale = Vector3.one * (1f + 0.15f * Mathf.Abs(Mathf.Sin((Time.unscaledTime - t0) * 6f)));
                yield return null;
            }
            if (finger != null) finger.anchoredPosition = home;
            if (fingerGroup != null) fingerGroup.alpha = 0f;
            if (throwButton != null) throwButton.localScale = Vector3.one;
            if (Time.timeScale > 0f) Time.timeScale = 1f;
            done[(int)step] = true;
            if (acted && dialogue != null) dialogue.Say("cayci", step == Step.Throw ? "Bullseye, nephew!" : "Well done, nephew!", 1.2f);
            yield return new WaitForSecondsRealtime(1.5f);
            if (AllDone())
            {
                PlayerPrefs.SetInt("jl.tutorialDone", 1);
                PlayerPrefs.Save();
                if (dialogue != null) dialogue.Say("cayci", "Now go, don't let him get away!", 2f);
                enabled = false;
            }
            busy = false;
        }

        private bool AllDone()
        {
            foreach (bool d in done) if (!d) return false;
            return true;
        }
    }
}
