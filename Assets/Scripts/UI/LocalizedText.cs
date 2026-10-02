using Jumpline.Core;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Sabit bir metni seçili dile çevirir; dil değişince kendini günceller.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private TMP_Text text;

        public void SetKey(string newKey)
        {
            key = newKey;
            Apply();
        }

        private void OnEnable()
        {
            Localization.Changed += Apply;
            Apply();
        }

        private void OnDisable() => Localization.Changed -= Apply;

        private void Apply()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            text.text = Localization.Get(key);
        }
    }
}
