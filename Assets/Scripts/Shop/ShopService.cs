using Jumpline.Save;

namespace Jumpline.Shop
{
    public enum PurchaseResult { Success, AlreadyOwned, NotEnoughFunds, StoreUnavailable }

    /// <summary>
    /// Satın alma. Altın/elmas fiyatlı eşyalar yerel cüzdandan düşer. Dolar fiyatlı eşyalar için gerçek mağaza
    /// (Google Play Billing) henüz bağlı değil: geliştirici modunda ücretsiz verilir, yayın sürümünde reddedilir.
    /// </summary>
    public static class ShopService
    {
        /// <summary>Kostüm giyildiğinde (id ile) — 3B vitrinler dinler.</summary>
        public static event System.Action<string> SkinEquipped;

        public static int Balance(CurrencyType currency) => currency == CurrencyType.Gold ? SaveManager.Gold : SaveManager.Gems;

        public static bool IsOwned(ShopItem item) => item.category switch
        {
            ItemCategory.Skin => SaveManager.IsOwned(item.id),
            ItemCategory.Gear => item.gearVariant == SaveManager.DefaultGearVariant || SaveManager.IsOwned(item.id),
            ItemCategory.Bundle => SaveManager.IsOwned(item.id)
                || (!string.IsNullOrEmpty(item.rewardSkin) && item.rewardGold == 0 && item.rewardGems == 0 && SaveManager.IsOwned(item.rewardSkin)),
            _ => false,
        };

        public static bool IsEquipped(ShopItem item) => item.category switch
        {
            ItemCategory.Skin => SaveManager.EquippedSkin == item.id,
            ItemCategory.Gear => SaveManager.GetGearVariant(item.gearSlot) == item.gearVariant,
            _ => false,
        };

        public static PurchaseResult TryPurchase(ShopItem item)
        {
            if (IsOwned(item)) return PurchaseResult.AlreadyOwned;

            if (item.IsRealMoney)
            {
                if (!SaveManager.UnlimitedCurrency) return PurchaseResult.StoreUnavailable;
            }
            else
            {
                if (Balance(item.currency) < item.price) return PurchaseResult.NotEnoughFunds;
                if (item.currency == CurrencyType.Gold) SaveManager.Gold -= item.price;
                else SaveManager.Gems -= item.price;
            }

            switch (item.category)
            {
                case ItemCategory.Skin:
                case ItemCategory.Gear:
                    SaveManager.SetOwned(item.id);
                    break;
                case ItemCategory.Boost:
                    SaveManager.AddBoost(item.id, 1);
                    break;
                case ItemCategory.Currency:
                case ItemCategory.Bundle:
                    SaveManager.Gold += item.rewardGold;
                    SaveManager.Gems += item.rewardGems;
                    if (!string.IsNullOrEmpty(item.rewardSkin)) SaveManager.SetOwned(item.rewardSkin);
                    if (item.category == ItemCategory.Bundle) SaveManager.SetOwned(item.id);
                    break;
            }
            SaveManager.Flush();
            Jumpline.Audio.AudioManager.Play("purchase");   // tık + çın (buton sesi zaten çaldı)
            return PurchaseResult.Success;
        }

        /// <summary>Sahip olunan kostümü giyer ya da ekipman çeşidini takar.</summary>
        public static bool TryEquip(ShopItem item)
        {
            if (!IsOwned(item)) return false;
            if (item.category == ItemCategory.Skin)
            {
                SaveManager.EquippedSkin = item.id;
                SaveManager.Flush();
                SkinEquipped?.Invoke(item.id);
                return true;
            }
            if (item.category == ItemCategory.Gear)
            {
                SaveManager.SetGearVariant(item.gearSlot, item.gearVariant);
                return true;
            }
            return false;
        }
    }
}
