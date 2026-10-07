using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileMarket.Data;
using MobileMarket.Environment;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>
    /// The full inventory screen: equipment slots, the player's grid (pockets or worn backpack) and, when looting, the container's grid.
    /// Every drag-and-drop between cells is resolved here and handed to Inventory / ItemGrid.
    /// </summary>
    public class InventoryScreenUI : MonoBehaviour
    {
        [SerializeField] private ItemCellUI[] equipSlots;
        [SerializeField] private ItemGridViewUI playerGrid;
        [SerializeField] private ItemGridViewUI lootGrid;
        [Tooltip("Drawn behind the 3x3 grid when no backpack is worn.")]
        [SerializeField] private Sprite pocketsArt;
        [SerializeField] private Rect pocketsGridArea = new Rect(0.2f, 0.2f, 0.6f, 0.6f);
        [SerializeField] private Image dragGhost;
        [SerializeField] private Text statusText;
        [SerializeField] private float lootCloseDistance = 2.5f;
        [SerializeField] private float statusDuration = 2f;

        private Inventory inventory;
        private LootBox lootBox;
        private Transform viewer;
        private Coroutine statusRoutine;
        private WaitForSeconds statusWait;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            statusWait = new WaitForSeconds(statusDuration);
            foreach (var slot in equipSlots) slot.Screen = this;
            dragGhost.raycastTarget = false;
            dragGhost.enabled = false;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (lootBox != null && Vector3.Distance(viewer.position, lootBox.transform.position) > lootCloseDistance) Close();
        }

        /// <summary>Opens the screen for the player's inventory, optionally next to a container being looted.</summary>
        public void Open(Inventory target, LootBox box = null)
        {
            if (inventory != target)
            {
                Detach();
                inventory = target;
                inventory.OnInventoryChanged += Refresh;
                inventory.OnActionFailed += ShowStatus;
            }

            viewer = target.transform;
            lootBox = box;
            gameObject.SetActive(true);
            statusText.text = "";

            lootGrid.gameObject.SetActive(box != null);
            if (box != null) lootGrid.Bind(this, box.Grid, box.DisplayName);
            else lootGrid.Unbind();

            Refresh();
        }

        public void Toggle(Inventory target)
        {
            if (IsOpen) Close();
            else Open(target);
        }

        public void Close()
        {
            EndDrag();
            lootGrid.Unbind();
            lootBox = null;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => Detach();

        private void Detach()
        {
            if (inventory == null) return;
            inventory.OnInventoryChanged -= Refresh;
            inventory.OnActionFailed -= ShowStatus;
            inventory = null;
        }

        private void Refresh()
        {
            if (inventory == null || !IsOpen) return;

            foreach (var slot in equipSlots) slot.Show(inventory.GetSlot(slot.Slot));

            // The background follows the worn backpack (its own drawing), or the open cloth bundle without one.
            var grid = inventory.ActiveGrid;
            if (inventory.Backpack?.Data is BackpackData backpack && backpack.panelArt != null)
                playerGrid.Bind(this, grid, $"{backpack.itemName}  ({grid.Columns}x{grid.Rows})", backpack.panelArt, backpack.gridArea);
            else
                playerGrid.Bind(this, grid, $"보따리  ({grid.Columns}x{grid.Rows})", pocketsArt, pocketsGridArea);
        }

        // ---------------------------------------------------------------- drag & drop

        public void BeginDrag(ItemCellUI source, PointerEventData eventData)
        {
            dragGhost.sprite = source.Item.Data.icon;
            dragGhost.enabled = dragGhost.sprite != null;
            dragGhost.transform.position = eventData.position;
            dragGhost.transform.SetAsLastSibling();
        }

        public void Drag(PointerEventData eventData)
        {
            dragGhost.transform.position = eventData.position;
        }

        public void EndDrag()
        {
            dragGhost.enabled = false;
        }

        public void HandleDrop(ItemCellUI source, ItemCellUI target)
        {
            if (source == target || inventory == null) return;

            if (!source.IsEquipSlot && !target.IsEquipSlot)
            {
                if (!ItemGrid.Move(source.Grid, source.Index, target.Grid, target.Index)) ShowStatus("아이템이 든 가방은 넣을 수 없습니다");
            }
            else if (!source.IsEquipSlot)
            {
                if (source.Item.Slot != target.Slot) ShowStatus("맞지 않는 칸입니다");
                else inventory.TryEquip(source.Grid, source.Index);
            }
            else if (!target.IsEquipSlot)
            {
                inventory.TryUnequip(source.Slot, target.Grid, target.Index);
            }
        }

        private void ShowStatus(string message)
        {
            if (!IsOpen) return;
            if (statusRoutine != null) StopCoroutine(statusRoutine);
            statusRoutine = StartCoroutine(StatusRoutine(message));
        }

        private IEnumerator StatusRoutine(string message)
        {
            statusText.text = message;
            yield return statusWait;
            statusText.text = "";
            statusRoutine = null;
        }
    }
}
