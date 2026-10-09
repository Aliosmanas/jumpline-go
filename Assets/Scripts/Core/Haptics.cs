using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>
    /// Titreşim ayarı açıksa kısa, net titreşim. Android'de titreşim motoru doğrudan sürülür (Handheld.Vibrate ~0.5 sn
    /// uzun ve kaba): süre (ms) ve güç (1-255) verilir. Diğer platformlarda Handheld.Vibrate'e düşer.
    /// </summary>
    public static class Haptics
    {
        public const int Light = 20, Medium = 40, Heavy = 70;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static int sdk = -1;
#endif

        public static void Pulse() => Pulse(Medium, 160);

        public static void Pulse(int milliseconds, int amplitude = 160)
        {
            if (!SaveManager.Vibration) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (vibrator == null)
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using var version = new AndroidJavaClass("android.os.Build$VERSION");
                    sdk = version.GetStatic<int>("SDK_INT");
                }
                if (vibrator == null) return;
                if (sdk >= 26)
                {
                    using var effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    using var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, Mathf.Clamp(amplitude, 1, 255));
                    vibrator.Call("vibrate", effect);
                }
                else vibrator.Call("vibrate", (long)milliseconds);
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }
}
