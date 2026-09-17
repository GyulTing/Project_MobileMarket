using UnityEngine;
using UnityEngine.InputSystem;
using MobileMarket.Environment;
using MobileMarket.Systems;

namespace MobileMarket.UI
{
    /// <summary>Shows a LootBox's contents and lets the player drag items into their inventory.</summary>
    public class LootPanel : MonoBehaviour
    {
        [SerializeField] private LootSlotUI[] slots;
        [SerializeField] private InventoryPanelUI bagPanel;
        [SerializeField] private float closeDistance = 2.5f;

        private LootBox currentBox;
        private Inventory currentInventory;
        private Transform interactorTransform;

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].Panel = this;
                slots[i].SlotIndex = i;
            }
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!gameObject.activeSelf) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (Vector3.Distance(interactorTransform.position, currentBox.transform.position) > closeDistance)
            {
                Close();
            }
        }

        public void Open(LootBox box, GameObject interactor)
        {
            currentBox = box;
            currentInventory = interactor.GetComponent<Inventory>();
            interactorTransform = interactor.transform;
            if (currentInventory == null) return;

            gameObject.SetActive(true);
            bagPanel.Show(currentInventory);
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            bagPanel.Hide();
            currentBox = null;
            currentInventory = null;
        }

        public void TakeItem(int slotIndex)
        {
            if (currentBox == null || currentInventory == null) return;
            currentBox.TakeItem(slotIndex, currentInventory);
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var item = i < currentBox.SlotCount ? currentBox.GetItemAt(i) : null;
                slots[i].SetItem(item);
            }
        }
    }
}
