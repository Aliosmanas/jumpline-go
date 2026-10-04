using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Shop
{
    public enum ItemCategory { Skin, Boost, Currency, Gear, Bundle }
    public enum CurrencyType { Gold, Gems }
    public enum Rarity { Common, Rare, Epic, Legendary, Gold }

    [Serializable]
    public class ShopItem
    {
        public string id;
        public string displayName;
        [Tooltip("Kartta başlığın altındaki küçük yazı (paketler).")]
        public string subtitle;
        [Tooltip("Uzun açıklama (güçler).")]
        public string description;
        public ItemCategory category;
        public Rarity rarity;
        public CurrencyType currency;
        [Min(0)] public int price;
        [Tooltip("> 0 ise gerçek para fiyatı (USD). Mağaza bağlanana kadar geliştirici modunda ücretsiz verilir.")]
        [Min(0f)] public float priceUsd;
        [Min(0)] public int rewardGold;
        [Min(0)] public int rewardGems;
        [Tooltip("Paket içindeki kostüm (Bundle).")]
        public string rewardSkin;
        [Tooltip("Gear: helmet / gloves / boots. Variant = id'nin ön eki (stealth, velocity, titanium).")]
        public string gearSlot;
        public string gearVariant;

        public bool IsRealMoney => priceUsd > 0f;
    }

    /// <summary>Marketteki tüm eşyalar: kostümler, ekipmanlar, güçler, para ve paketler.</summary>
    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "Jumpline/Shop Catalog")]
    public class ShopCatalog : ScriptableObject
    {
        [SerializeField] private List<ShopItem> items = new List<ShopItem>();

        public IReadOnlyList<ShopItem> Items => items;

        public ShopItem Find(string id) => items.Find(i => i.id == id);

        public List<ShopItem> InCategory(ItemCategory category) => items.FindAll(i => i.category == category);

        public void SetItems(List<ShopItem> newItems) => items = newItems;
    }
}
