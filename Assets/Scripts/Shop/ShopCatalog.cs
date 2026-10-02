using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Shop
{
    public enum ItemCategory { Skin, Boost, Currency }
    public enum CurrencyType { Gold, Gems }

    [Serializable]
    public class ShopItem
    {
        public string id;
        public string displayName;
        public ItemCategory category;
        public CurrencyType currency;
        [Min(0)] public int price;
        [Tooltip("Sadece Currency: satın alınca verilen altın.")]
        [Min(0)] public int rewardGold;
    }

    /// <summary>Marketteki tüm eşyalar. Kostümler, güçler ve elmas→altın paketleri.</summary>
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
