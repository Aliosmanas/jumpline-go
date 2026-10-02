using Jumpline.Core;
using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Ekipman yuvası: basınca takar / çıkarır; çıkarılınca görsel soluklaşır.</summary>
    [RequireComponent(typeof(Button))]
    public class GearToggle : MonoBehaviour
    {
        [SerializeField] private string slot;
        [SerializeField] private string itemName;
        [SerializeField] private Graphic icon;
        [SerializeField] private TMP_Text status;
        [SerializeField, Range(0f, 1f)] private float removedAlpha = 0.3f;
        [SerializeField] private Color equippedColor = new Color(0.85f, 0.87f, 0.92f);
        [SerializeField] private Color removedColor = new Color(0.6f, 0.62f, 0.7f);

        public void Configure(string gearSlot, string displayName, Graphic iconGraphic, TMP_Text statusText)
        {
            slot = gearSlot;
            itemName = displayName;
            icon = iconGraphic;
            status = statusText;
        }

        private void Awake() => GetComponent<Button>().onClick.AddListener(Toggle);

        private void OnEnable()
        {
            SaveManager.GearChanged += Refresh;
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            SaveManager.GearChanged -= Refresh;
            Localization.Changed -= Refresh;
        }

        private void Toggle()
        {
            bool equip = !SaveManager.IsGearEquipped(slot);
            SaveManager.SetGearEquipped(slot, equip);
            GetComponentInParent<MenuNavigator>(true)?.Toast.Show(
                Localization.Format(equip ? "{0} EQUIPPED" : "{0} REMOVED", Localization.Get(itemName)));
        }

        private void Refresh()
        {
            bool on = SaveManager.IsGearEquipped(slot);
            if (icon != null)
            {
                Color c = icon.color;
                c.a = on ? 1f : removedAlpha;
                icon.color = c;
            }
            if (status != null)
            {
                status.text = Localization.Get(on ? "EQUIPPED" : "REMOVED");
                status.color = on ? equippedColor : removedColor;
            }
        }
    }
}
