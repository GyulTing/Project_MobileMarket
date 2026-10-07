using System;
using UnityEngine;
using UnityEngine.Serialization;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.Environment
{
    /// <summary>A lootable container with its own item grid. Opening it shows its grid next to the player's inventory.</summary>
    public class LootBox : MonoBehaviour, IInteractable
    {
        public static event Action<LootBox, GameObject> OnBoxOpened;

        [SerializeField] private string displayName = "상자";
        [SerializeField] private int columns = 3;
        [SerializeField] private int rows = 3;

        [SerializeField] private LootTable lootTable;
        [FormerlySerializedAs("slotCount")]
        [SerializeField] private int rollCount = 3;

        [Tooltip("If set, the box holds exactly these items instead of rolling the loot table.")]
        [SerializeField] private ItemData[] fixedItems;

        public string DisplayName => displayName;
        public ItemGrid Grid { get; private set; }

        private void Awake()
        {
            // Containers can hold backpacks that still have items in them.
            Grid = new ItemGrid(columns, rows, true);

            if (fixedItems != null && fixedItems.Length > 0)
            {
                foreach (var item in fixedItems) if (item != null) Grid.TryAdd(new ItemInstance(item));
                return;
            }

            if (lootTable == null) return;
            for (int i = 0; i < rollCount; i++)
            {
                var item = lootTable.RollItem();
                if (item != null) Grid.TryAdd(new ItemInstance(item));
            }
        }

        public void Interact(GameObject interactor)
        {
            OnBoxOpened?.Invoke(this, interactor);
        }
    }
}
