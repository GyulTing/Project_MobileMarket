using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>Read-only grid view of a player's Inventory, shown alongside a LootPanel.</summary>
    public class InventoryPanelUI : MonoBehaviour
    {
        [SerializeField] private Text weightText;
        [SerializeField] private Text[] cells;

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
                cells[i].text = i < inventory.Items.Count ? inventory.Items[i].itemName : "";
            }
            weightText.text = $"{inventory.CurrentWeight:0.#}/{inventory.MaxWeight:0.#}";
        }
    }
}
