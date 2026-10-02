using System;
using Jumpline.Core;
using Jumpline.Player;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Özellikleri altınla geliştirme ekranı.</summary>
    public class UpgradeView : MonoBehaviour
    {
        [Serializable]
        public class Row
        {
            public StatBar bar;
            public Button button;
            public TMP_Text cost;
        }

        [SerializeField] private CharacterStats stats;
        [SerializeField] private Row[] rows;
        [SerializeField] private TMP_Text goldText;

        private void Awake()
        {
            foreach (Row r in rows)
            {
                Row row = r;
                row.button.onClick.AddListener(() => Upgrade(row));
            }
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Upgrade(Row row)
        {
            StatDefinition s = stats.Find(row.bar.statId);
            string message = CharacterStats.IsMaxed(s) ? "MAX"
                : CharacterStats.TryUpgrade(s) ? "UPGRADED!"
                : "NOT ENOUGH GOLD";
            GetComponentInParent<MenuNavigator>(true).Toast.Show(Localization.Get(message));
            Refresh();
        }

        private void Refresh()
        {
            goldText.text = Localization.Format("GOLD: {0}", SaveManager.Gold);
            foreach (Row r in rows)
            {
                r.bar.Refresh(stats);
                StatDefinition s = stats.Find(r.bar.statId);
                bool maxed = CharacterStats.IsMaxed(s);
                r.cost.text = maxed ? Localization.Get("MAX") : Localization.Format("{0} GOLD", CharacterStats.UpgradeCost(s));
                r.button.interactable = !maxed;
            }
        }
    }
}
