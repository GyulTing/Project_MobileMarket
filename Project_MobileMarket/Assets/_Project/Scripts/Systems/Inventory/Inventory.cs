using System;
using System.Collections.Generic;
using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.Systems
{
    /// <summary>Per-owner item bag with a weight limit. Not a singleton — lives on the player, accessed via GetComponent.</summary>
    public class Inventory : MonoBehaviour
    {
        public event Action OnInventoryChanged;

        [SerializeField] private float maxWeight = 20f;

        private readonly List<ItemData> items = new List<ItemData>();
        public IReadOnlyList<ItemData> Items => items;
        public float MaxWeight => maxWeight;
        public float CurrentWeight { get; private set; }

        private void Start()
        {
            items.Clear();
            items.AddRange(SaveManager.Instance.LoadInventory());
            RecalculateWeight();
            OnInventoryChanged?.Invoke();
        }

        public bool TryAddItem(ItemData item)
        {
            if (item == null) return false;
            if (CurrentWeight + item.weight > maxWeight) return false;

            items.Add(item);
            CurrentWeight += item.weight;
            OnInventoryChanged?.Invoke();
            SaveManager.Instance.SaveInventory(items);
            return true;
        }

        public void RemoveItem(ItemData item)
        {
            if (!items.Remove(item)) return;
            CurrentWeight -= item.weight;
            OnInventoryChanged?.Invoke();
            SaveManager.Instance.SaveInventory(items);
        }

        private void RecalculateWeight()
        {
            CurrentWeight = 0f;
            foreach (var item in items) CurrentWeight += item.weight;
        }
    }
}
