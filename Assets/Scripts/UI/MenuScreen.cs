using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Gezinmede adıyla açılan bir menü ekranı.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MenuScreen : MonoBehaviour
    {
        [SerializeField] private string id;
        [SerializeField] private bool showBottomNav = true;

        private CanvasGroup group;

        public string Id => id;
        public bool ShowBottomNav => showBottomNav;
        public CanvasGroup Group => group != null ? group : group = GetComponent<CanvasGroup>();

        public void Configure(string screenId, bool bottomNav)
        {
            id = screenId;
            showBottomNav = bottomNav;
        }
    }
}
