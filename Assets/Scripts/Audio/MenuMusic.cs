using UnityEngine;

namespace Jumpline.Audio
{
    /// <summary>Ana menü müziği: çatı müziği ("Moon Over Fez") kısık; oyundan dönünce zafer parçası susar.</summary>
    public class MenuMusic : MonoBehaviour
    {
        private void Start()
        {
            AudioManager.Instance.StopStinger();
            AudioManager.Instance.StopAllLoops();
            AudioManager.Instance.Layer(null);
            AudioManager.Instance.Music("menu", 1.0f);
        }
    }
}
