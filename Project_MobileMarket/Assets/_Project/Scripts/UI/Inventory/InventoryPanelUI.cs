using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>Grid view of a player's Inventory, shown alongside a LootPanel. Items can be dragged out (e.g. to an EquipSlotUI).</summary>
    public class InventoryPanelUI : MonoBehaviour
    {
        [SerializeField] private Text weightText;
        [SerializeField] private BagSlotUI[] cells;

        private Inventory inventory;

        public void Show(Inventory targetInventory)
        {
            inventory = targetInventory;
            inventory.OnInventoryChanged += Refresh;
            gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (inventory != null) inventory.OnInventoryChanged -= Refresh;
            inventory = null;
            gameObject.SetActive(false);
        }

        public void Toggle(Inventory targetInventory)
        {
            if (gameObject.activeSelf) Hide();
            else Show(targetInventory);
        }

        private void Refresh()
        {
            if (inventory == null) return;

            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].SetItem(i < inventory.Items.Count ? inventory.Items[i] : null);
            }
            weightText.text = $"{inventory.CurrentWeight:0.#}/{inventory.MaxWeight:0.#}";
        }
    }
}
