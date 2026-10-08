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

        private State state;

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
        }

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
            if (state == State.Playing) Pause();
            else if (state == State.Paused) Resume();
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
            bool lastLevel = level >= config.LevelCount;
            if (GameSession.Mode == RunMode.Campaign && lastLevel) reward += "\n" + Localization.Get("ALL LEVELS COMPLETE!");
            SaveManager.Flush();

            rewardText.text = reward;
            nextButton.gameObject.SetActive(GameSession.Mode == RunMode.Campaign && !lastLevel);
            Show(completePanel, true);
        }

        public void FailLevel()
        {
            if (state == State.Finished) return;
            state = State.Finished;
            RefreshRevive();
            Show(failPanel, true);
        }

        private void RefreshRevive()
        {
            bool canAfford = SaveManager.Gems >= reviveGemCost;
            if (reviveGemsButton != null) reviveGemsButton.interactable = canAfford && player != null;
            if (reviveGemsLabel != null) reviveGemsLabel.text = Localization.Format("CONTINUE · {0}", reviveGemCost);
            if (gemBalanceLabel != null)
                gemBalanceLabel.text = Localization.Format("YOUR GEMS: {0}", SaveManager.Gems.ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>50 elmas ver, ölünen yerden 3-2-1 geri sayımla devam et.</summary>
        private void ReviveWithGems()
        {
            if (state != State.Finished || player == null || !player.IsDead) return;
            if (SaveManager.Gems < reviveGemCost) return;
            SaveManager.Gems -= reviveGemCost;
            SaveManager.Flush();
            StartCoroutine(ReviveRoutine());
        }

        private System.Collections.IEnumerator ReviveRoutine()
        {
            Show(failPanel, false);
            Vector3 p = player.transform.position;
            Vector3 safe = worldSwitcher != null ? worldSwitcher.SafeRevivePoint(p) : new Vector3(p.x, 0f, p.z);
            foreach (var sp in FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
                if (sp.isActiveAndEnabled) sp.ClearRange(safe.z - 8f, safe.z + clearAhead);
            player.Revive(safe);
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
