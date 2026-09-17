using System;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    public class LootBox : MonoBehaviour, IInteractable
    {
        public static event Action<LootBox, GameObject> OnBoxOpened;

        [SerializeField] private LootTable lootTable;
        [SerializeField] private int slotCount = 3;

        private ItemData[] contents;

        public int SlotCount => slotCount;
        public ItemData GetItemAt(int slotIndex) => contents[slotIndex];

        private void Awake()
        {
            contents = new ItemData[slotCount];
            if (lootTable == null) return;

            for (int i = 0; i < slotCount; i++)
            {
                contents[i] = lootTable.RollItem();
            }
        }

        public void Interact(GameObject interactor)
        {
            OnBoxOpened?.Invoke(this, interactor);
        }

        /// <summary>Moves the item in a slot into the given inventory. Returns false (slot untouched) if the inventory can't fit it.</summary>
        public bool TakeItem(int slotIndex, Inventory inventory)
        {
            if (slotIndex < 0 || slotIndex >= contents.Length) return false;

            var item = contents[slotIndex];
            if (item == null) return false;
            if (!inventory.TryAddItem(item)) return false;

            contents[slotIndex] = null;
            return true;
        }
    }
}
