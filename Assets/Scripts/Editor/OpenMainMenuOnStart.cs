using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Jumpline.EditorTools
{
    /// <summary>Editör açıldığında sahne boşsa (Untitled) ana menüyü açar: Play'e basınca oyun baştan başlar.</summary>
    [InitializeOnLoad]
    public static class OpenMainMenuOnStart
    {
        private const string Key = "Jumpline.OpenMainMenuOnStart.Done";

        static OpenMainMenuOnStart()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(Key, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, true);
                Scene s = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(s.path)) EditorSceneManager.OpenScene("Assets/Scenes/MainMenu/MainMenu.unity");
            };
        }
    }
}
