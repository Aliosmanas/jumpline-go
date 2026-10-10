using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşu başında seçili bölümün tanımını (GameConfig.LevelSpec) uygular: koşucu hızı, engel yoğunluğu, canavar
    /// kalbi ve kaçma hızı. Diğer bileşenlerin Start'ından önce çalışır.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LevelDirector : MonoBehaviour
    {
        [SerializeField] private GameConfig config;
        [SerializeField] private PlayerController player;
        [SerializeField] private ObstacleSpawner[] spawners;
        [SerializeField] private BossChase boss;

        public GameConfig.LevelSpec Current { get; private set; }

        private void Awake()
        {
            if (config == null) return;
            Current = config.GetLevel(GameSession.Level);
            if (player != null) player.SetSpeeds(Current.runSpeed, Current.maxSpeed);
            if (spawners != null) foreach (var s in spawners) if (s != null) s.SetGap(Current.obstacleGap);
            if (boss != null) boss.SetDifficulty(Current.bossHearts, Current.bossDrift);
        }
    }
}
