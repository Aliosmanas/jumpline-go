using System;
using Jumpline.Core;
using Jumpline.Player;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Bir karakter özelliğinin değer / seviye / çubuk göstergesi.</summary>
    [Serializable]
    public class StatBar
    {
        public string statId;
        public TMP_Text value;
        public TMP_Text tier;
        public TMP_Text level;
        public RectTransform fill;
        public float fullWidth;
        [Tooltip("Boşsa ham sayı; ör. \"{0} SEC\" (güç süreleri).")]
        public string valueFormat;

        public void Refresh(CharacterStats stats)
        {
            StatDefinition s = stats.Find(statId);
            if (s == null) return;
            if (value != null) value.text = string.IsNullOrEmpty(valueFormat) ? CharacterStats.Value(s).ToString()
                : Localization.Format(valueFormat, CharacterStats.Value(s));
            if (tier != null) tier.text = Localization.Get(stats.Tier(s));
            if (level != null) level.text = Localization.Format("LV {0}/{1}", CharacterStats.Level(s), s.maxLevel);
            if (fill != null) fill.sizeDelta = new Vector2(fullWidth * CharacterStats.Fill(s), fill.sizeDelta.y);
        }
    }
}
