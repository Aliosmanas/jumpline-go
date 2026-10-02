using System;
using Jumpline.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Dil seçimi; seçilen dilin yanında onay işareti.</summary>
    public class LanguageView : MonoBehaviour
    {
        [Serializable]
        public class Option
        {
            public string code;
            public Button button;
            public GameObject check;
        }

        [SerializeField] private Option[] options;

        private void Awake()
        {
            foreach (Option o in options)
            {
                string code = o.code;
                o.button.onClick.AddListener(() =>
                {
                    Localization.SetLanguage(code);
                    Refresh();
                });
            }
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            foreach (Option o in options) o.check.SetActive(o.code == Localization.Current);
        }
    }
}
