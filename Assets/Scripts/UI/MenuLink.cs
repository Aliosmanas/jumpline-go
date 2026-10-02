using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Butona basınca menüde bir ekran açar ya da geri gider.</summary>
    [RequireComponent(typeof(Button))]
    public class MenuLink : MonoBehaviour
    {
        public enum LinkAction { Open, OpenRoot, Back }

        [SerializeField] private LinkAction action;
        [SerializeField] private string target;

        private MenuNavigator navigator;

        public void Configure(LinkAction linkAction, string screenId)
        {
            action = linkAction;
            target = screenId;
        }

        private void Awake()
        {
            navigator = GetComponentInParent<MenuNavigator>(true);
            GetComponent<Button>().onClick.AddListener(Go);
        }

        private void Go()
        {
            switch (action)
            {
                case LinkAction.Open: navigator.Open(target); break;
                case LinkAction.OpenRoot: navigator.OpenRoot(target); break;
                case LinkAction.Back: navigator.Back(); break;
            }
        }
    }
}
