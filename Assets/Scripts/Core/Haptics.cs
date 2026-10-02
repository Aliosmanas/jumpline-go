using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>Titreşim ayarı açıksa ve cihaz destekliyorsa titretir.</summary>
    public static class Haptics
    {
        public static void Pulse()
        {
            if (!SaveManager.Vibration) return;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
