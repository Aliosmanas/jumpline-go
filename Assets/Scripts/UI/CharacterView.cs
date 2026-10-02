using Jumpline.Core;
using Jumpline.Player;
using Jumpline.Save;
using Jumpline.Shop;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Karakter / profil: özellik çubukları ve giyili kostüm.</summary>
    public class CharacterView : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats;
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private StatBar[] bars;
        [SerializeField] private TMP_Text suitName;

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void Refresh()
        {
            foreach (StatBar b in bars) b.Refresh(stats);
            ShopItem skin = catalog.Find(SaveManager.EquippedSkin);
            if (skin != null) suitName.text = Localization.Get(skin.displayName);
        }
    }
}
