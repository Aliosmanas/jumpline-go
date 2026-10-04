using System;
using System.Globalization;
using Jumpline.Core;
using Jumpline.Save;
using Jumpline.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    public enum CardMode { Shop, Inventory, Customize }

    /// <summary>Bir eşyanın kartı: ad, fiyat / durum, aksiyon. Market, envanter ve özelleştirmede kullanılır.</summary>
    public class ShopItemCard : MonoBehaviour
    {
        [SerializeField] private string itemId;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text action;
        [SerializeField] private GameObject equippedMark;
        [Tooltip("Başlık sabit kalsın (ör. '1000 GOLD', 'VELOCITY PACK'); kod değiştirmesin.")]
        [SerializeField] private bool keepTitle;
        [SerializeField] private Color gemsColor = new Color(0.373f, 0.706f, 0.961f);
        [SerializeField] private Color goldColor = new Color(0.91f, 0.725f, 0.357f);
        [SerializeField] private Color stateColor = new Color(0.85f, 0.87f, 0.92f);

        public string ItemId => itemId;
        public event Action<ShopItemCard> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public static string FormatUsd(float price) => "$" + price.ToString("0.00", CultureInfo.InvariantCulture);

        public void Refresh(ShopItem item, CardMode mode)
        {
            bool wearable = item.category == ItemCategory.Skin || item.category == ItemCategory.Gear;
            bool owned = ShopService.IsOwned(item);
            bool equipped = wearable && ShopService.IsEquipped(item);
            bool gold = item.currency == CurrencyType.Gold;

            if (!keepTitle)
                Set(title, item.category == ItemCategory.Currency && item.rewardGems == 0 && !item.IsRealMoney
                    ? Localization.Format("{0} GOLD", item.rewardGold)
                    : Localization.Get(item.displayName));

            string cost = item.IsRealMoney ? FormatUsd(item.priceUsd) : Localization.Format(gold ? "{0} GOLD" : "{0} GEMS", item.price);
            Color costColor = item.IsRealMoney ? stateColor : gold ? goldColor : gemsColor;

            switch (mode)
            {
                case CardMode.Shop:
                    if (owned && wearable)
                    {
                        Set(status, Localization.Get("OWNED"), stateColor);
                        Set(action, Localization.Get(equipped ? "EQUIPPED" : "EQUIP"));
                    }
                    else if (owned)
                    {
                        Set(status, Localization.Get("OWNED"), stateColor);
                        Set(action, Localization.Get("OWNED"));
                    }
                    else if (item.IsRealMoney)
                    {
                        Set(status, BoostCount(item), stateColor);
                        Set(action, cost);
                    }
                    else
                    {
                        string price = cost + BoostCount(item);
                        Set(status, price, costColor);
                        Set(action, Localization.Get(gold && wearable ? "BUY GOLD" : "BUY"));
                    }
                    break;

                case CardMode.Inventory:
                    if (wearable) Set(status, Localization.Get(equipped ? "EQUIPPED" : owned ? "OWNED" : "LOCKED"), owned ? stateColor : costColor);
                    else Set(status, $"x{SaveManager.GetBoostCount(item.id)}", stateColor);
                    break;

                case CardMode.Customize:
                    Set(status, owned ? Localization.Get(equipped ? "EQUIPPED" : "EQUIP") : $"{Localization.Get("LOCKED")} · {cost}",
                        owned ? stateColor : costColor);
                    break;
            }
            if (equippedMark != null) equippedMark.SetActive(equipped && mode != CardMode.Shop);
        }

        private static string BoostCount(ShopItem item)
        {
            if (item.category != ItemCategory.Boost) return "";
            int n = SaveManager.GetBoostCount(item.id);
            return n > 0 ? $"  (x{n})" : "";
        }

        private static void Set(TMP_Text text, string value, Color? color = null)
        {
            if (text == null) return;
            text.text = value;
            if (color.HasValue) text.color = color.Value;
        }
    }
}
