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

        public void Refresh(CharacterStats stats)
        {
            StatDefinition s = stats.Find(statId);
            if (s == null) return;
            if (value != null) value.text = CharacterStats.Value(s).ToString();
            if (tier != null) tier.text = Localization.Get(stats.Tier(s));
            if (level != null) level.text = Localization.Format("LV {0}/{1}", CharacterStats.Level(s), s.maxLevel);
            if (fill != null) fill.sizeDelta = new Vector2(fullWidth * CharacterStats.Fill(s), fill.sizeDelta.y);
        }
    }
}
