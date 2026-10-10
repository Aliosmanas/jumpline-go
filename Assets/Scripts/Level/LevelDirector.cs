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
        }

        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private ObstacleSpawner[] spawners;
        [SerializeField] private BossChase boss;
        [SerializeField] private LevelFlowController flow;
        [SerializeField] private BossBody[] bodies;

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
        }

        private void ApplyBody(string legend)
        {
            if (bodies == null || bodies.Length == 0) return;
            int pick = 0;
            for (int i = 0; i < bodies.Length; i++) if (bodies[i].legend == legend) pick = i;
            for (int i = 0; i < bodies.Length; i++) if (bodies[i].root != null) bodies[i].root.SetActive(i == pick);
            if (boss != null) boss.SetBody(bodies[pick].animator, bodies[pick].hand, bodies[pick].renderers);
        }
    }
}
