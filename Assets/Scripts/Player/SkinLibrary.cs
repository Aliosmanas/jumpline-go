using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Player
{
    /// <summary>Bir kostümün malzeme slotu renkleri (docs/references/character/skins.jpg).</summary>
    [Serializable]
    public class SkinPreset
    {
        public string id;
        public Color primary = Color.white;
        public Color secondary = Color.white;
        public Color armor = Color.white;
        public Color glove = Color.white;
        public Color glow = Color.cyan;
        public Color visor = Color.white;
        [Min(0f)] public float glowIntensity = 4f;
        [Tooltip("Vizör de ışık saçsın mı (Velocity gibi).")]
        public bool visorGlows;
        [Tooltip("Lacivert parçalara hafif ışık (Glitch etkisi).")]
        [Min(0f)] public float secondaryGlow;
        [Range(0f, 1f)] public float metallic = 0.15f;
        [Range(0f, 1f)] public float smoothness = 0.45f;
    }

    [CreateAssetMenu(fileName = "SkinLibrary", menuName = "Jumpline/Skin Library")]
    public class SkinLibrary : ScriptableObject
    {
        [SerializeField] private List<SkinPreset> skins = new List<SkinPreset>();

        public IReadOnlyList<SkinPreset> Skins => skins;

        public SkinPreset Find(string id) => skins.Find(s => s.id == id) ?? (skins.Count > 0 ? skins[0] : null);

        public void SetSkins(List<SkinPreset> newSkins) => skins = newSkins;
    }
}
