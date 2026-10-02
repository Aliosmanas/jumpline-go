using Jumpline.Core;
using Jumpline.Save;
using Jumpline.Shop;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Market: cüzdan, sekmeler ve kartlardan satın alma / giyme.</summary>
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
            tabs.OnChanged.AddListener(ShowTab);
            foreach (ShopItemCard c in cards) c.Clicked += OnCard;
        }

        private void OnEnable()
        {
            Localization.Changed += Refresh;
            ShowTab(tabs.SelectedIndex);
            Refresh();
        }

        private void OnDisable() => Localization.Changed -= Refresh;

        private void ShowTab(int index)
        {
            for (int i = 0; i < tabPanels.Length; i++) tabPanels[i].SetActive(i == index);
        }

        private void OnCard(ShopItemCard card)
        {
            ShopItem item = catalog.Find(card.ItemId);
            if (item == null) return;

            string message;
            if (item.category == ItemCategory.Skin && SaveManager.IsOwned(item.id))
            {
                ShopService.TryEquip(item);
                message = Localization.Format("{0} EQUIPPED", Localization.Get(item.displayName));
            }
            else
            {
                PurchaseResult result = ShopService.TryPurchase(item);
                message = result switch
                {
                    PurchaseResult.Success => Localization.Get("PURCHASED!"),
                    PurchaseResult.NotEnoughFunds => Localization.Get(item.currency == CurrencyType.Gold ? "NOT ENOUGH GOLD" : "NOT ENOUGH GEMS"),
                    _ => Localization.Get("OWNED"),
                };
            }
            GetComponentInParent<MenuNavigator>(true).Toast.Show(message);
            Refresh();
        }

        private void Refresh()
        {
            goldText.text = Localization.Format("GOLD: {0}", SaveManager.Gold);
            gemsText.text = Localization.Format("GEMS: {0}", SaveManager.Gems);
            foreach (ShopItemCard c in cards)
            {
                ShopItem item = catalog.Find(c.ItemId);
                if (item != null) c.Refresh(item, CardMode.Shop);
            }
        }
    }
}
