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
            Show(failPanel, true);
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
