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

        [Tooltip("If set, the box holds exactly these items instead of rolling the loot table.")]
        [SerializeField] private ItemData[] fixedItems;

        private ItemData[] contents;

        public int SlotCount => slotCount;
        public ItemData GetItemAt(int slotIndex) => contents[slotIndex];

        private void Awake()
        {
            contents = new ItemData[slotCount];

            if (fixedItems != null && fixedItems.Length > 0)
            {
                for (int i = 0; i < slotCount && i < fixedItems.Length; i++) contents[i] = fixedItems[i];
                return;
            }

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
