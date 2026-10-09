using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jumpline.Level
{
    /// <summary>
    /// Bölüm akışı: HUD, duraklatma, bölüm sonu ve oyun bitti panelleri; ilerleme ve ödül.
    /// Oyuncu sistemi geldiğinde <see cref="CompleteLevel"/> / <see cref="FailLevel"/> çağıracak.
    /// </summary>
    public class LevelFlowController : MonoBehaviour
    {
        private enum State { Playing, Paused, Finished }

        [SerializeField] private GameConfig config;
        [Tooltip("Koşucu öldüğünde (ölme animasyonundan sonra) oyun bitti paneli açılır.")]
        [SerializeField] private Jumpline.Player.PlayerController player;

        [Header("HUD")]
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text modeLabel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button testWinButton;
        [SerializeField] private Button testFailButton;

        [Header("Paneller")]
        [SerializeField] private CanvasGroup pausePanel;
        [SerializeField] private CanvasGroup completePanel;
        [SerializeField] private CanvasGroup failPanel;
        [SerializeField] private TMP_Text rewardText;

        [Header("Panel butonları")]
        [SerializeField] private Button continueButton;
        [SerializeField] private Button[] restartButtons;
        [SerializeField] private Button[] mainMenuButtons;
        [SerializeField] private Button nextButton;

        [Header("Devam et (ölünce)")]
        [Tooltip("Elmasla devam bedeli (şimdilik 50; ilerde ekonomiyle ayarlanacak).")]
        [SerializeField, Min(0)] private int reviveGemCost = 50;
        [SerializeField] private Button reviveGemsButton;
        [Tooltip("Reklam izleyerek devam — reklam henüz yok, buton pasif.")]
        [SerializeField] private Button reviveAdButton;
        [SerializeField] private TMP_Text reviveGemsLabel;
        [SerializeField] private TMP_Text gemBalanceLabel;
        [SerializeField] private TMP_Text countdownLabel;
        [SerializeField, Min(1)] private int countdownFrom = 3;
        [Tooltip("Devam edince koşucunun önünden temizlenen mesafe (m).")]
        [SerializeField, Min(5f)] private float clearAhead = 35f;
        [SerializeField] private WorldSwitcher worldSwitcher;

        [Header("Canavar kovalaması (kampanya)")]
        [Tooltip("Canavar: kaçarsa bölüm kaybedilir, yenilirse bölüm biter.")]
        [SerializeField] private Jumpline.Boss.BossChase boss;
        [Tooltip("Oyun bitti paneli başlığı (canavar kaçınca \"TEPEGÖZ KAÇTI!\" olur).")]
        [SerializeField] private TMP_Text failReasonLabel;
        [Tooltip("Kovalamada gizlenen bölüm ilerleme çubuğu ve mod yazısı.")]
        [SerializeField] private GameObject[] hideDuringChase;
        [Tooltip("Canavarı ilk kez yenince verilen elmas.")]
        [SerializeField, Min(0)] private int bossFirstWinGems = 25;
        [Header("Hızlı Yarış")]
        [Tooltip("Yalnız Hızlı Yarış'ta görünen (para kapsülündeki 2x rozeti).")]
        [SerializeField] private GameObject[] quickRaceOnly;
        [SerializeField] private Jumpline.UI.ScoreCounter score;
        [Tooltip("Efsaneler Kitabı sayfa kimliği (bölüm 1: tepegoz).")]
        [SerializeField] private string legendId = "tepegoz";

        private State state;
        /// <summary>Bölüm tamamlandı (ödül paneli açıldı).</summary>
        public event System.Action LevelCompleted;
        private Color? failTitleColor;

        private void Awake()
        {
            pauseButton.onClick.AddListener(Pause);
            continueButton.onClick.AddListener(Resume);
            nextButton.onClick.AddListener(() => SceneLoader.StartLevel(config, GameSession.Level + 1, RunMode.Campaign));
            foreach (Button b in restartButtons) b.onClick.AddListener(() => SceneLoader.StartLevel(config, GameSession.Level, GameSession.Mode));
            foreach (Button b in mainMenuButtons) b.onClick.AddListener(() => SceneLoader.LoadMainMenu(config));
            testWinButton.onClick.AddListener(CompleteLevel);
            testFailButton.onClick.AddListener(FailLevel);
            if (player != null) player.Died += FailLevel;
            if (reviveGemsButton != null) reviveGemsButton.onClick.AddListener(ReviveWithGems);
            if (reviveAdButton != null) reviveAdButton.interactable = false;   // reklam ileride
            if (boss != null)
            {
                boss.Escaped += FailLevel;
                boss.Defeated += CompleteLevel;
            }
        }

        private void Start()
        {
            Time.timeScale = 1f;
            state = State.Playing;
            levelLabel.text = Localization.Format("LEVEL {0}", GameSession.Level);
            modeLabel.text = Localization.Get(GameSession.Mode switch
            {
                RunMode.QuickRace => "QUICK RACE",
                RunMode.Daily => "DAILY CHALLENGE",
                _ => "CAMPAIGN",
            });
            Show(pausePanel, false);
            Show(completePanel, false);
            Show(failPanel, false);
            bool chase = boss != null && boss.gameObject.activeSelf;
            bool quick = GameSession.Mode == RunMode.QuickRace;
            if (hideDuringChase != null) foreach (var g in hideDuringChase) if (g != null) g.SetActive(!chase && !quick);
            if (quickRaceOnly != null) foreach (var g in quickRaceOnly) if (g != null) g.SetActive(quick);
            if (quick) levelLabel.text = Localization.Get("QUICK RACE");   // sonsuz: bölüm numarası yok
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (state == State.Playing) Pause();
            else if (state == State.Paused) Resume();
        }

        // telefon çaldı / uygulama arka plana geçti: oyun kendiliğinden duraklar (dönünce oyuncu "devam"a basar)
        private void OnApplicationPause(bool paused)
        {
            if (paused) Pause();
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && !Application.isEditor) Pause();
        }

        public void Pause()
        {
            if (state != State.Playing) return;
            state = State.Paused;
            Time.timeScale = 0f;
            Show(pausePanel, true);
        }

        public void Resume()
        {
            if (state != State.Paused) return;
            state = State.Playing;
            Time.timeScale = 1f;
            Show(pausePanel, false);
        }

        public void CompleteLevel()
        {
            if (state == State.Finished) return;
            state = State.Finished;
            int level = GameSession.Level;
            SaveManager.UnlockUpTo(Mathf.Min(level + 1, config.LevelCount));

            SaveManager.Gold += config.LevelRewardGold;
            string reward = Localization.Format("+{0} GOLD", config.LevelRewardGold);
            if (GameSession.Mode == RunMode.Daily && !SaveManager.DailyClaimedToday)
            {
                SaveManager.Gems += config.DailyRewardGems;
                SaveManager.MarkDailyClaimed();
                reward += "    " + Localization.Format("+{0} GEMS", config.DailyRewardGems);
            }
            if (boss != null && boss.IsDefeated && SaveManager.UnlockLegend(legendId))
            {
                SaveManager.Gems += bossFirstWinGems;
                reward += "    " + Localization.Format("+{0} GEMS", bossFirstWinGems);
                reward += "\n" + Localization.Get("NEW PAGE IN THE BOOK OF LEGENDS!");
            }
            bool lastLevel = level >= config.LevelCount;
            if (GameSession.Mode == RunMode.Campaign && lastLevel) reward += "\n" + Localization.Get("ALL LEVELS COMPLETE!");
            SaveManager.Flush();

            Time.timeScale = 0f;   // ödül ekranı açıkken dünya durur
            rewardText.text = reward;
            LevelCompleted?.Invoke();
            nextButton.gameObject.SetActive(GameSession.Mode == RunMode.Campaign && !lastLevel);
            Show(completePanel, true);
        }

        public void FailLevel()
        {
            if (state == State.Finished) return;
            state = State.Finished;
            bool escaped = boss != null && boss.IsEscaped && (player == null || !player.IsDead);
            if (escaped) Time.timeScale = 0f;   // canavar kaçtı: koşucu yaşıyor, panel açıkken dünya dursun
            RefreshRevive();
            Show(failPanel, true);
            if (failReasonLabel != null)   // başlık: "OYUN BİTTİ" ya da "TEPEGÖZ KAÇTI!" (panel açıldıktan sonra; çeviri bileşeni ezmesin)
            {
                if (failTitleColor == null) failTitleColor = failReasonLabel.color;
                failReasonLabel.text = Localization.Get(escaped ? "TEPEGOZ ESCAPED!" : "GAME OVER");
                failReasonLabel.color = escaped ? new Color(1f, 0.36f, 0.3f) : failTitleColor.Value;
                if (GameSession.Mode == RunMode.QuickRace && score != null)   // Hızlı Yarış: puan ve rekor
                {
                    bool record = SaveManager.SubmitQuickRace(score.Score);
                    failReasonLabel.text = Localization.Format(record ? "NEW RECORD! {0}" : "SCORE {0}", score.Score.ToString("N0"));
                    if (record) failReasonLabel.color = new Color(1f, 0.85f, 0.35f);
                }
            }
        }

        private void RefreshRevive()
        {
            bool canAfford = SaveManager.Gems >= reviveGemCost;
            if (reviveGemsButton != null) reviveGemsButton.interactable = canAfford && player != null && CanRevive;
            if (reviveGemsLabel != null) reviveGemsLabel.text = Localization.Format("CONTINUE · {0}", reviveGemCost);
            if (gemBalanceLabel != null)
            {
                string gems = Localization.Format("YOUR GEMS: {0}", SaveManager.Gems.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
                if (GameSession.Mode == RunMode.QuickRace)
                    gems = Localization.Format("BEST {0}", SaveManager.QuickRaceBest.ToString("N0")) + "   ·   " + gems;
                gemBalanceLabel.text = gems;
            }
        }

        /// <summary>50 elmas ver, ölünen yerden 3-2-1 geri sayımla devam et.</summary>
        private void ReviveWithGems()
        {
            if (state != State.Finished || player == null || !CanRevive) return;
            if (SaveManager.Gems < reviveGemCost) return;
            SaveManager.Gems -= reviveGemCost;
            SaveManager.Flush();
            StartCoroutine(ReviveRoutine());
        }

        /// <summary>Devam edilebilir mi: koşucu öldü ya da canavar kaçtı (yenilmediyse).</summary>
        private bool CanRevive => player.IsDead || (boss != null && boss.IsEscaped);

        private System.Collections.IEnumerator ReviveRoutine()
        {
            Show(failPanel, false);
            Vector3 p = player.transform.position;
            Vector3 safe = worldSwitcher != null ? worldSwitcher.SafeRevivePoint(p) : new Vector3(p.x, 0f, p.z);
            foreach (var sp in FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
                if (sp.isActiveAndEnabled) sp.ClearRange(safe.z - 8f, safe.z + clearAhead);
            if (player.IsDead) player.Revive(safe);
            if (boss != null && boss.gameObject.activeSelf) boss.Recover();
            Time.timeScale = 0f;                                   // geri sayım boyunca dünya donuk
            if (countdownLabel != null)
            {
                countdownLabel.gameObject.SetActive(true);
                for (int n = countdownFrom; n >= 1; n--)
                {
                    countdownLabel.text = n.ToString();
                    float t0 = Time.unscaledTime;
                    while (Time.unscaledTime - t0 < 0.8f)
                    {
                        float k = (Time.unscaledTime - t0) / 0.8f;
                        countdownLabel.transform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Mathf.Min(1f, k * 3f));
                        countdownLabel.alpha = 1f - Mathf.Max(0f, k - 0.7f) / 0.3f;
                        yield return null;
                    }
                }
                countdownLabel.gameObject.SetActive(false);
            }
            Time.timeScale = 1f;
            state = State.Playing;
        }

        private static void Show(CanvasGroup panel, bool visible)
        {
            panel.gameObject.SetActive(visible);
            panel.alpha = visible ? 1f : 0f;
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
        }
    }
}
