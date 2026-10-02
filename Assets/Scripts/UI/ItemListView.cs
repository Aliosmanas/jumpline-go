using Jumpline.Core;
using Jumpline.Shop;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Envanter (sadece gösterir) veya Özelleştirme (sahip olunan kostümü giyer) listesi.</summary>
    public class ItemListView : MonoBehaviour
    {
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private CardMode mode = CardMode.Inventory;
        [SerializeField] private ShopItemCard[] cards;

        private void Awake()
        {
            foreach (ShopItemCard c in cards) c.Clicked += OnCard;
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void OnCard(ShopItemCard card)
        {
            if (mode != CardMode.Customize) return;
            ShopItem item = catalog.Find(card.ItemId);
            if (item == null) return;

            string message = ShopService.TryEquip(item)
                ? Localization.Format("{0} EQUIPPED", Localization.Get(item.displayName))
                : Localization.Get("BUY IT IN THE MARKET");
            GetComponentInParent<MenuNavigator>(true).Toast.Show(message);
            Refresh();
        }

        private void Refresh()
        {
            foreach (ShopItemCard c in cards)
            {
                ShopItem item = catalog.Find(c.ItemId);
                if (item != null) c.Refresh(item, mode);
            }
        }
    }
}
