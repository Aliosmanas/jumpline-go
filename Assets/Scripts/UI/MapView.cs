using System;
using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Bölüm haritası (Efsaneler Kitabı yolculuğu): her durak bir şehir + bir efsane. Açık ve hazır bölüm oynanır,
    /// kilitli bölüm kilit gösterir, henüz yapılmamış bölüm "Yakında" der. Durak altında en iyi yıldızlar (0-3).
    /// Oynanacak sıradaki bölüm nabız gibi büyüyüp küçülür.
    /// </summary>
    public class MapView : MonoBehaviour
    {
        [Serializable]
        public struct Node
        {
            [Min(1)] public int level;
            public Button button;
            public Image[] stars;
            public GameObject lockIcon;
            public GameObject portrait;
            public TMP_Text status;
        }

        [SerializeField] private GameConfig config;
        [SerializeField] private Node[] nodes;

        private int BuiltLevels => config != null ? config.BuiltLevels : 1;
        [Tooltip("Şu an oynanabilir (yapılmış) bölüm sayısı; sonrası 'Yakında'.")]
        [SerializeField] private Color starOn = Color.white;
        [SerializeField] private Color starOff = new Color(0.25f, 0.22f, 0.28f, 0.75f);

        private int pulseIndex = -1;

        private void Awake()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                Node n = nodes[i];
                if (n.button != null) n.button.onClick.AddListener(() => Play(n.level));
            }
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Refresh()
        {
            pulseIndex = -1;
            for (int i = 0; i < nodes.Length; i++)
            {
                Node n = nodes[i];
                bool built = n.level <= BuiltLevels;
                bool unlocked = built && SaveManager.IsLevelUnlocked(n.level);
                int stars = SaveManager.GetStars(n.level);
                if (n.lockIcon != null) n.lockIcon.SetActive(!unlocked);
                if (n.portrait != null) n.portrait.SetActive(unlocked);
                if (n.stars != null)
                    for (int s = 0; s < n.stars.Length; s++)
                        if (n.stars[s] != null) { n.stars[s].gameObject.SetActive(built); n.stars[s].color = s < stars ? starOn : starOff; }
                if (n.status != null) n.status.text = !built ? Localization.Get("COMING SOON") : unlocked ? "" : Localization.Get("LOCKED");
                if (unlocked && stars < 3 && pulseIndex < 0) pulseIndex = i;
            }
        }

        private void Update()
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].button == null) continue;
                float s = i == pulseIndex ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f) : 1f;
                nodes[i].button.transform.localScale = Vector3.one * s;
            }
        }

        private void Play(int level)
        {
            var toast = GetComponentInParent<MenuNavigator>(true)?.Toast;
            if (level > BuiltLevels) { toast?.Show(Localization.Get("THIS LEGEND IS COMING SOON!")); return; }
            if (!SaveManager.IsLevelUnlocked(level)) { toast?.Show(Localization.Get("BEAT THE PREVIOUS LEGEND FIRST!")); return; }
            SceneLoader.StartLevel(config, level, RunMode.Campaign);
        }
    }
}
