using Jumpline.Audio;
using Jumpline.Boss;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>Oyun sahnesi sesleri: ses bankası kurulur, GameplayAudio olaylara bağlanır.</summary>
    public static partial class GameplaySceneBuilder
    {
        private static void BuildGameplayAudio(Transform runner, CoinField coins, WorldSwitcher switcher, LevelFlowController flow)
        {
            AudioSetup.Setup();
            var go = new GameObject("GameplayAudio");
            var ga = go.AddComponent<GameplayAudio>();
            Set(ga, "player", runner.GetComponent<PlayerController>());
            Set(ga, "coins", coins);
            Set(ga, "switcher", switcher);
            Set(ga, "throws", Object.FindAnyObjectByType<ThrowSystem>(FindObjectsInactive.Include));
            Set(ga, "boss", Object.FindAnyObjectByType<BossChase>(FindObjectsInactive.Include));
            Set(ga, "powerUps", runner.GetComponent<PowerUps>());
            Set(ga, "flow", flow);
        }
    }
}
