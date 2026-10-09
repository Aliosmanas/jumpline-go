using Jumpline.Boss;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>Bağlılık (aşama 11): koşu olaylarını görev/başarım/XP sayaçlarına işleyen ProgressTracker.</summary>
    public static partial class GameplaySceneBuilder
    {
        private static void BuildProgressTracker(Transform runner, CoinField coins, LevelFlowController flow)
        {
            var tracker = new GameObject("ProgressTracker").AddComponent<ProgressTracker>();
            Set(tracker, "player", runner.GetComponent<PlayerController>());
            Set(tracker, "coins", coins);
            Set(tracker, "throws", Object.FindAnyObjectByType<ThrowSystem>(FindObjectsInactive.Include));
            Set(tracker, "boss", Object.FindAnyObjectByType<BossChase>(FindObjectsInactive.Include));
            Set(tracker, "powerUps", Object.FindAnyObjectByType<PowerUps>(FindObjectsInactive.Include));
            Set(flow, "tracker", tracker);
        }
    }
}
