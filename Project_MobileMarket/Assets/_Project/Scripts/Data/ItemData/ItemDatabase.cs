using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.Data
{
    /// <summary>Resolves a saved item id back to its ItemData asset. Assets must live under a Resources/Items folder.</summary>
    public static class ItemDatabase
    {
        private static Dictionary<string, ItemData> cache;

        public static ItemData GetById(string id)
        {
            if (cache == null) BuildCache();
            cache.TryGetValue(id, out var item);
            return item;
        }

        private static void BuildCache()
        {
            cache = new Dictionary<string, ItemData>();
            foreach (var item in Resources.LoadAll<ItemData>("Items"))
            {
                if (!string.IsNullOrEmpty(item.id)) cache[item.id] = item;
            }
        }
    }
}
