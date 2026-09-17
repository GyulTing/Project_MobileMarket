using UnityEngine;
using UnityEngine.EventSystems;

namespace MobileMarket.UI
{
    /// <summary>Drop target representing the player's bag. Dropping a LootSlotUI here moves its item to inventory.</summary>
    public class InventoryDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            var slot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<LootSlotUI>() : null;
            if (slot == null || slot.Item == null) return;

            slot.Panel.TakeItem(slot.SlotIndex);
        }
    }
}
