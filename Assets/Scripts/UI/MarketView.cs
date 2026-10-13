using Jumpline.Core;
using Jumpline.Save;
using Jumpline.Shop;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Market: kartlardan satın alma / giyme. Sekmeler ve cüzdan yazıları isteğe bağlı (tek sayfa markette yok).</summary>
    public class MarketView : MonoBehaviour
    {
        [SerializeField] private ShopCatalog catalog;
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text gemsText;
        [SerializeField] private SegmentedControl tabs;
        [Tooltip("Sekme sırasıyla: Skins, Boosts, Currency.")]
        [SerializeField] private GameObject[] tabPanels;
        [SerializeField] private ShopItemCard[] cards;

        private void Awake()
        {
            if (tabs != null) tabs.OnChanged.AddListener(ShowTab);
            foreach (ShopItemCard c in cards) c.Clicked += OnCard;
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            if (tabs != null) ShowTab(tabs.SelectedIndex);
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void ShowTab(int index)
        {
            if (tabPanels == null) return;
            for (int i = 0; i < tabPanels.Length; i++) tabPanels[i].SetActive(i == index);
        }

        private void OnCard(ShopItemCard card)
        {
            ShopItem item = catalog.Find(card.ItemId);
            if (item == null) return;

            string message;
            bool wearable = item.category == ItemCategory.Skin || item.category == ItemCategory.Gear;
            if (wearable && ShopService.IsOwned(item))
            {
                ShopService.TryEquip(item);
                message = Localization.Format("{0} EQUIPPED", Localization.Get(item.displayName));
            }
            else
            {
                PurchaseResult result = ShopService.TryPurchase(item);
                message = result switch
                {
                    PurchaseResult.Success => Localization.Get(item.IsRealMoney ? "PURCHASED! (DEV: FREE)" : "PURCHASED!"),
                    PurchaseResult.NotEnoughFunds => Localization.Get(item.currency == CurrencyType.Gold ? "NOT ENOUGH GOLD" : "NOT ENOUGH GEMS"),
                    PurchaseResult.StoreUnavailable => Localization.Get("STORE COMING SOON"),
                    _ => Localization.Get("OWNED"),
                };
                if (result == PurchaseResult.Success && wearable) ShopService.TryEquip(item); // alınca hemen giy
            }
            GetComponentInParent<MenuNavigator>(true).Toast.Show(message);
            Refresh();
        }

        private void Refresh()
        {
            if (goldText != null) goldText.text = SaveManager.Gold.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            if (gemsText != null) gemsText.text = SaveManager.Gems.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            foreach (ShopItemCard c in cards)
            {
                ShopItem item = catalog.Find(c.ItemId);
                if (item != null) c.Refresh(item, CardMode.Shop);
            }
        }
    }
}
