using Jumpline.Core;
using Jumpline.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Oyunu başlatan butonlar: kampanya (PLAY), hızlı yarış, günlük görev.</summary>
    [RequireComponent(typeof(Button))]
    public class LevelStartButton : MonoBehaviour
    {
        [SerializeField] private RunMode mode;
        [SerializeField] private GameConfig config;
        [Tooltip("Günlük görev bugün yapıldıysa butonun saydamlığı.")]
        [SerializeField, Range(0f, 1f)] private float claimedAlpha = 0.45f;

        private CanvasGroup group;

        public void Configure(RunMode playMode, GameConfig gameConfig)
        {
            mode = playMode;
            config = gameConfig;
        }

        private void Awake() => GetComponent<Button>().onClick.AddListener(Launch);

        private void OnEnable()
        {
            if (mode != RunMode.Daily) return;
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = SaveManager.DailyClaimedToday ? claimedAlpha : 1f;
        }

        private void Launch()
        {
            switch (mode)
            {
                case RunMode.Campaign:
                    SceneLoader.StartLevel(config, SceneLoader.ContinueLevel(config), RunMode.Campaign);
                    break;
                case RunMode.QuickRace:
                    SceneLoader.StartLevel(config, Random.Range(1, SceneLoader.ContinueLevel(config) + 1), RunMode.QuickRace);
                    break;
                case RunMode.Daily:
                    if (SaveManager.DailyClaimedToday)
                        GetComponentInParent<MenuNavigator>(true).Toast.Show(Localization.Get("COME BACK TOMORROW!"));
                    else
                        SceneLoader.StartLevel(config, SceneLoader.DailyLevel(config), RunMode.Daily);
                    break;
            }
        }
    }
}
