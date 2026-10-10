using System;
using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşu başında seçili bölümün tanımını (GameConfig.LevelSpec) uygular: koşucu hızı, engel yoğunluğu, canavar
    /// kalbi ve kaçma hızı, bölümün canavar gövdesi (sahnede hepsi hazır bekler, yalnız biri açılır) ve efsane adı.
    /// Diğer bileşenlerin Start'ından önce çalışır.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LevelDirector : MonoBehaviour
    {
        [Serializable]
        public struct BossBody
        {
            public string legend;
            public GameObject root;
            public Animator animator;
            public Transform hand;
            public Renderer[] renderers;
            public Sprite icon;
        }

        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private ObstacleSpawner[] spawners;
        [SerializeField] private BossChase boss;
        [SerializeField] private LevelFlowController flow;
        [SerializeField] private BossBody[] bodies;
        [Tooltip("Göstergedeki canavar ikonları (bölümün canavarına göre değişir).")]
        [SerializeField] private UnityEngine.UI.Image[] bossIcons;
        [Tooltip("Şehre göre renklenen taş malzemeleri (sokak, çatı).")]
        [SerializeField] private Material[] stoneMaterials;
        [Tooltip("Taşları aranacak kökler (Street, Rooftop).")]
        [SerializeField] private Transform[] worldRoots;

        public GameConfig.LevelSpec Current { get; private set; }

        private void Awake()
        {
            if (config == null) return;
            Current = config.GetLevel(GameSession.Level);
            if (player != null) player.SetSpeeds(Current.runSpeed, Current.maxSpeed);
            if (spawners != null) foreach (var s in spawners) if (s != null) s.SetGap(Current.obstacleGap);
            if (boss != null) boss.SetDifficulty(Current.bossHearts, Current.bossDrift);
            if (flow != null) flow.SetLegend(Current.legend, Current.displayName);
            ApplyBody(Current.legend);
            ApplyStone(Current.stoneTint);
        }

        /// <summary>Taş malzemelerini bölüm rengiyle çarpılmış kopyalarla değiştirir (asıl dosyalar değişmez; birleştirmeden önce).</summary>
        private void ApplyStone(Color tint)
        {
            if (tint == Color.white || stoneMaterials == null || worldRoots == null) return;
            var swap = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var m in stoneMaterials)
            {
                if (m == null) continue;
                var copy = new Material(m) { name = m.name + "_" + Current.city };
                if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint);
                swap[m] = copy;
            }
            foreach (var root in worldRoots)
            {
                if (root == null) continue;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && swap.TryGetValue(mats[i], out var c)) { mats[i] = c; changed = true; }
                    if (changed) r.sharedMaterials = mats;
                }
            }
        }

        private void ApplyBody(string legend)
        {
            if (bodies == null || bodies.Length == 0) return;
            int pick = 0;
            for (int i = 0; i < bodies.Length; i++) if (bodies[i].legend == legend) pick = i;
            for (int i = 0; i < bodies.Length; i++) if (bodies[i].root != null) bodies[i].root.SetActive(i == pick);
            if (boss != null) boss.SetBody(bodies[pick].animator, bodies[pick].hand, bodies[pick].renderers);
            if (bodies[pick].icon != null && bossIcons != null)
                foreach (var img in bossIcons) if (img != null) img.sprite = bodies[pick].icon;
        }
    }
}
