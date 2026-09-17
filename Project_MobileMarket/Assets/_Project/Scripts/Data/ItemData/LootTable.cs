using System;
using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.Data
{
    [CreateAssetMenu(fileName = "LootTable", menuName = "MobileMarket/Loot Table")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemData item;
            [Min(0f)] public float weight = 1f;
        }

        public List<Entry> entries = new List<Entry>();
        [Range(0f, 1f)] public float chanceToDropAnything = 1f;

        /// <summary>Weighted random pick. Returns null on a "nothing" roll or an empty table.</summary>
        public ItemData RollItem()
        {
            if (entries.Count == 0 || UnityEngine.Random.value > chanceToDropAnything) return null;

            float totalWeight = 0f;
            foreach (var entry in entries) totalWeight += entry.weight;
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            float cumulative = 0f;
            foreach (var entry in entries)
            {
                cumulative += entry.weight;
                if (roll <= cumulative) return entry.item;
            }
            return entries[entries.Count - 1].item;
        }
    }
}
