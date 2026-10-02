using System;
using System.Collections.Generic;
using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Player
{
    [Serializable]
    public class StatDefinition
    {
        public string id;
        public string displayName;
        [Min(0)] public int baseValue;
        [Min(0)] public int perLevel;
        [Min(1)] public int maxValue = 100;
        [Min(1)] public int maxLevel = 10;
        [Min(0)] public int upgradeCostBase = 500;
    }

    /// <summary>Karakter özellikleri ve geliştirme maliyetleri. Seviyeler kayıtta tutulur.</summary>
    [CreateAssetMenu(fileName = "CharacterStats", menuName = "Jumpline/Character Stats")]
    public class CharacterStats : ScriptableObject
    {
        [SerializeField] private List<StatDefinition> stats = new List<StatDefinition>();
        [Tooltip("Doluluk eşikleri: altı Low, sonra Medium, High, üstü Very High.")]
        [SerializeField] private Vector3 tierThresholds = new Vector3(0.35f, 0.6f, 0.85f);

        public IReadOnlyList<StatDefinition> Stats => stats;

        public StatDefinition Find(string id) => stats.Find(s => s.id == id);

        public void SetStats(List<StatDefinition> newStats) => stats = newStats;

        public static int Level(StatDefinition s) => Mathf.Min(SaveManager.GetStatLevel(s.id), s.maxLevel);
        public static int Value(StatDefinition s) => s.baseValue + s.perLevel * Level(s);
        public static float Fill(StatDefinition s) => Mathf.Clamp01(Value(s) / (float)s.maxValue);
        public static bool IsMaxed(StatDefinition s) => Level(s) >= s.maxLevel;
        public static int UpgradeCost(StatDefinition s) => s.upgradeCostBase * (Level(s) + 1);

        public string Tier(StatDefinition s)
        {
            float f = Fill(s);
            if (f < tierThresholds.x) return "Low";
            if (f < tierThresholds.y) return "Medium";
            if (f < tierThresholds.z) return "High";
            return "Very High";
        }

        /// <summary>Altınla bir seviye artırır.</summary>
        public static bool TryUpgrade(StatDefinition s)
        {
            if (IsMaxed(s)) return false;
            int cost = UpgradeCost(s);
            if (SaveManager.Gold < cost) return false;
            SaveManager.Gold -= cost;
            SaveManager.SetStatLevel(s.id, Level(s) + 1);
            SaveManager.Flush();
            return true;
        }
    }
}
