using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>Geliştirici kısayolları: ilk açılış deneyimini (açılış konuşması + öğretici) yeniden görmek için sıfırla.</summary>
    public static class DevTools
    {
        [MenuItem("Jumpline/Reset Intro + Tutorial")]
        public static void ResetStory()
        {
            PlayerPrefs.DeleteKey("jl.introSeen");
            for (int lv = 2; lv <= 8; lv++) PlayerPrefs.DeleteKey("jl.introSeen." + lv);
            PlayerPrefs.DeleteKey("jl.outroSeen");
            PlayerPrefs.DeleteKey("jl.tutorialDone");
            PlayerPrefs.DeleteKey("jl.dailyClaim");
            PlayerPrefs.Save();
            Debug.Log("[Jumpline] Açılış + öğretici + günlük görev sıfırlandı");
        }
    }
}
