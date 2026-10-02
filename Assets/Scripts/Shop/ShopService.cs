using Jumpline.Save;

namespace Jumpline.Shop
{
    public enum PurchaseResult { Success, AlreadyOwned, NotEnoughFunds }

    /// <summary>Yerel cüzdanla satın alma. Gerçek para (IAP) yok.</summary>
    public static class ShopService
    {
        /// <summary>Kostüm giyildiğinde (id ile) — 3B vitrinler dinler.</summary>
        public static event System.Action<string> SkinEquipped;

        public static int Balance(CurrencyType currency) => currency == CurrencyType.Gold ? SaveManager.Gold : SaveManager.Gems;

        public static PurchaseResult TryPurchase(ShopItem item)
        {
            if (item.category == ItemCategory.Skin && SaveManager.IsOwned(item.id)) return PurchaseResult.AlreadyOwned;
            if (Balance(item.currency) < item.price) return PurchaseResult.NotEnoughFunds;

            if (item.currency == CurrencyType.Gold) SaveManager.Gold -= item.price;
            else SaveManager.Gems -= item.price;

            switch (item.category)
            {
                case ItemCategory.Skin: SaveManager.SetOwned(item.id); break;
                case ItemCategory.Boost: SaveManager.AddBoost(item.id, 1); break;
                case ItemCategory.Currency: SaveManager.Gold += item.rewardGold; break;
            }
            SaveManager.Flush();
            return PurchaseResult.Success;
        }

        /// <summary>Sahip olunan kostümü giyer.</summary>
        public static bool TryEquip(ShopItem item)
        {
            if (item.category != ItemCategory.Skin || !SaveManager.IsOwned(item.id)) return false;
            SaveManager.EquippedSkin = item.id;
            SaveManager.Flush();
            SkinEquipped?.Invoke(item.id);
            return true;
        }
    }
}
