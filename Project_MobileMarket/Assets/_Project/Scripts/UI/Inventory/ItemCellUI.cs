using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MobileMarket.Data;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>
    /// One inventory cell — either a grid cell (Grid + Index) or an equipment slot (IsEquipSlot + Slot).
    /// It is both a drag source and a drop target; the InventoryScreenUI decides what a drop does.
    /// </summary>
    public class ItemCellUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Text label;
        [SerializeField] private Text badge;

        [Header("Equipment slot")]
        [SerializeField] private bool isEquipSlot;
        [SerializeField] private EquipSlot slot;
        [Tooltip("Long vertical weapon slots show the (horizontal) weapon icon turned upright.")]
        [SerializeField] private bool rotateIcon;

        public InventoryScreenUI Screen { get; set; }
        public ItemGrid Grid { get; private set; }
        public int Index { get; private set; }
        public bool IsEquipSlot => isEquipSlot;
        public EquipSlot Slot => slot;
        public ItemInstance Item { get; private set; }

        public void BindGrid(ItemGrid grid, int index)
        {
            Grid = grid;
            Index = index;
        }

        public void Show(ItemInstance item)
        {
            Item = item;
            var sprite = item?.Data.icon;

            icon.enabled = sprite != null;
            icon.sprite = sprite;
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotateIcon ? 90f : 0f);

            // Items without an icon yet show their name instead.
            label.text = item != null && sprite == null ? item.Data.itemName : "";

            // Backpacks show how full they are.
            bool showBadge = item?.Contents != null;
            badge.enabled = showBadge;
            if (showBadge) badge.text = item.Contents.Count + "/" + item.Contents.Size;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Item != null) Screen.BeginDrag(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Item != null) Screen.Drag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Screen.EndDrag();
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemCellUI>() : null;
            if (source != null && source.Item != null) Screen.HandleDrop(source, this);
        }
    }
}
