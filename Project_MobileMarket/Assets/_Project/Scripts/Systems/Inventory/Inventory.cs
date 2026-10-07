using System;
using System.Collections.Generic;
using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.Systems
{
    /// <summary>
    /// Everything the player carries: equipment slots (head, body, backpack, primary weapon) and the item grid.
    /// Without a backpack the grid is the 3x3 pockets; with one it is the backpack's own grid, so its items travel with it.
    /// Not a singleton — lives on the player, accessed via GetComponent.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public event Action OnInventoryChanged;
        public event Action<string> OnActionFailed;

        [SerializeField] private int pocketColumns = 3;
        [SerializeField] private int pocketRows = 3;

        private ItemGrid pockets;
        private readonly Dictionary<EquipSlot, ItemInstance> slots = new Dictionary<EquipSlot, ItemInstance>();
        private ItemGrid subscribedGrid;

        /// <summary>The grid the player's items live in right now: the worn backpack's grid, or the pockets.</summary>
        public ItemGrid ActiveGrid => Backpack != null ? Backpack.Contents : pockets;
        public ItemInstance Backpack => GetSlot(EquipSlot.Backpack);

        public ItemInstance GetSlot(EquipSlot slot) => slots.TryGetValue(slot, out var item) ? item : null;

        private void Awake()
        {
            pockets = new ItemGrid(pocketColumns, pocketRows, false);
        }

        private void Start()
        {
            Load(SaveManager.Instance.LoadInventory());
            RebindActiveGrid();
            OnInventoryChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (subscribedGrid != null) subscribedGrid.OnChanged -= HandleGridChanged;
        }

        // ---------------------------------------------------------------- simple add/remove (farming, pickups)

        public bool TryAddItem(ItemData data)
        {
            return data != null && ActiveGrid.TryAdd(new ItemInstance(data));
        }

        /// <summary>Removes the first carried instance of the given item. Returns false if none is carried.</summary>
        public bool RemoveItem(ItemData data)
        {
            var grid = ActiveGrid;
            for (int i = 0; i < grid.Size; i++)
            {
                if (grid[i] == null || grid[i].Data != data) continue;
                grid.Set(i, null);
                return true;
            }
            return false;
        }

        public T FindFirst<T>() where T : ItemData
        {
            var grid = ActiveGrid;
            for (int i = 0; i < grid.Size; i++)
            {
                if (grid[i] != null && grid[i].Data is T match) return match;
            }
            return null;
        }

        public bool HasFreeCells(int count) => ActiveGrid.FreeCount >= count;

        // ---------------------------------------------------------------- equipping

        /// <summary>Equips the item in grid[index] into its slot. Whatever was in that slot goes back into the same cell.</summary>
        public bool TryEquip(ItemGrid from, int index)
        {
            var item = from[index];
            var slot = item?.Slot;
            if (slot == null) return Fail("장착할 수 없는 아이템입니다");

            return slot == EquipSlot.Backpack ? TryEquipBackpack(from, index, item) : SwapIntoSlot(slot.Value, from, index, item);
        }

        /// <summary>Takes the item out of a slot and puts it into grid[index] (swapping if that cell holds a matching item).</summary>
        public bool TryUnequip(EquipSlot slot, ItemGrid to, int index)
        {
            var item = GetSlot(slot);
            if (item == null) return false;

            // Worn backpack dropped anywhere on its own grid: take it off and keep it in the (empty) pockets.
            if (slot == EquipSlot.Backpack && to == item.Contents) return UnequipBackpackToPockets();

            var occupant = to[index];
            if (occupant != null)
            {
                // Dropping onto a matching item is the same as equipping that item.
                if (occupant.Slot == slot) return TryEquip(to, index);
                return Fail("빈 칸에 놓아주세요");
            }

            if (!to.CanHold(item)) return Fail("아이템이 든 가방은 넣을 수 없습니다");

            SetSlot(slot, null);
            to.Set(index, item);
            Changed();
            return true;
        }

        /// <summary>Takes the worn backpack off into the first pocket cell; its items stay inside it.</summary>
        public bool UnequipBackpackToPockets()
        {
            var backpack = Backpack;
            if (backpack == null) return false;

            // Pockets are empty while a backpack is worn (their items moved into it), so this always finds a cell.
            // Set() is used on purpose: this is the one place a filled backpack may sit in the pockets.
            for (int i = 0; i < pockets.Size; i++)
            {
                if (pockets[i] != null) continue;
                SetSlot(EquipSlot.Backpack, null);
                pockets.Set(i, backpack);
                Changed();
                return true;
            }
            return Fail("주머니에 빈 칸이 없습니다");
        }

        private bool TryEquipBackpack(ItemGrid from, int index, ItemInstance backpack)
        {
            var worn = Backpack;
            if (worn == null)
            {
                // No backpack yet: the pocket items move into the new backpack.
                int toMove = pockets.Count - (from == pockets ? 1 : 0);
                if (backpack.Contents.FreeCount < toMove) return Fail("가방 공간이 부족합니다");
                for (int i = 0; i < pockets.Size; i++)
                {
                    if (pockets[i] != backpack && pockets[i] != null && pockets[i].IsFilledBackpack) return Fail("주머니의 가방을 먼저 비워주세요");
                }

                from.Set(index, null);
                for (int i = 0; i < pockets.Size; i++)
                {
                    if (pockets[i] != null) backpack.Contents.TryAdd(pockets[i]);
                }
                pockets.Clear();
                SetSlot(EquipSlot.Backpack, backpack);
                Changed();
                return true;
            }

            // Swap: each backpack keeps its own items.
            if (from == worn.Contents) return Fail("메고 있는 가방 안의 가방과는 바꿀 수 없습니다");
            if (!from.CanHold(worn)) return Fail("아이템이 든 가방은 넣을 수 없습니다");

            from.Set(index, worn);
            SetSlot(EquipSlot.Backpack, backpack);
            Changed();
            return true;
        }

        private bool SwapIntoSlot(EquipSlot slot, ItemGrid from, int index, ItemInstance item)
        {
            from.Set(index, GetSlot(slot));
            SetSlot(slot, item);
            Changed();
            return true;
        }

        private void SetSlot(EquipSlot slot, ItemInstance item)
        {
            if (item == null) slots.Remove(slot);
            else slots[slot] = item;
        }

        private bool Fail(string reason)
        {
            OnActionFailed?.Invoke(reason);
            return false;
        }

        // ---------------------------------------------------------------- change tracking & saving

        private void Changed()
        {
            RebindActiveGrid();
            Save();
            OnInventoryChanged?.Invoke();
        }

        private void RebindActiveGrid()
        {
            var grid = ActiveGrid;
            if (grid == subscribedGrid) return;
            if (subscribedGrid != null) subscribedGrid.OnChanged -= HandleGridChanged;
            subscribedGrid = grid;
            subscribedGrid.OnChanged += HandleGridChanged;
        }

        private void HandleGridChanged()
        {
            Save();
            OnInventoryChanged?.Invoke();
        }

        private void Save()
        {
            if (SaveManager.Instance == null) return;

            var data = new InventorySaveData
            {
                head = ToSave(GetSlot(EquipSlot.Head)),
                body = ToSave(GetSlot(EquipSlot.Body)),
                backpack = ToSave(Backpack),
                primary = ToSave(GetSlot(EquipSlot.Primary)),
            };
            for (int i = 0; i < pockets.Size; i++) data.pockets.Add(pockets[i] != null ? pockets[i].Data.id : "");
            SaveManager.Instance.SaveInventory(data);
        }

        private static ItemSave ToSave(ItemInstance item)
        {
            var save = new ItemSave();
            if (item == null) return save;

            save.id = item.Data.id;
            if (item.Contents != null)
            {
                for (int i = 0; i < item.Contents.Size; i++) save.contents.Add(item.Contents[i] != null ? item.Contents[i].Data.id : "");
            }
            return save;
        }

        private void Load(InventorySaveData data)
        {
            if (data == null) return;

            if (data.version == 0)
            {
                LoadLegacy(data.itemIds);
                return;
            }

            SetSlot(EquipSlot.Head, FromSave(data.head));
            SetSlot(EquipSlot.Body, FromSave(data.body));
            SetSlot(EquipSlot.Backpack, FromSave(data.backpack));
            SetSlot(EquipSlot.Primary, FromSave(data.primary));
            for (int i = 0; i < data.pockets.Count && i < pockets.Size; i++) pockets.Set(i, Create(data.pockets[i]));
        }

        /// <summary>Old flat item lists: the first weapon goes into the Primary slot, the rest fill the pockets.</summary>
        private void LoadLegacy(List<string> itemIds)
        {
            foreach (var id in itemIds)
            {
                var item = Create(id);
                if (item == null) continue;

                if (item.Slot == EquipSlot.Primary && GetSlot(EquipSlot.Primary) == null) SetSlot(EquipSlot.Primary, item);
                else if (!pockets.TryAdd(item)) Debug.LogWarning("[Inventory] Old save had more items than the pockets hold; dropped " + id);
            }
        }

        private static ItemInstance FromSave(ItemSave save)
        {
            var item = Create(save?.id);
            if (item?.Contents == null) return item;

            for (int i = 0; i < save.contents.Count && i < item.Contents.Size; i++) item.Contents.Set(i, Create(save.contents[i]));
            return item;
        }

        private static ItemInstance Create(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var data = ItemDatabase.GetById(id);
            return data != null ? new ItemInstance(data) : null;
        }
    }
}
