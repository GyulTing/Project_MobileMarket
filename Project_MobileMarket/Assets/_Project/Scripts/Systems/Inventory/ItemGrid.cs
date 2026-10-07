using System;

namespace MobileMarket.Systems
{
    /// <summary>A columns x rows grid of cells, one item per cell. Used by pockets, backpacks and loot containers.</summary>
    public class ItemGrid
    {
        public event Action OnChanged;

        public int Columns { get; }
        public int Rows { get; }
        public int Size => cells.Length;

        /// <summary>Backpacks that still hold items can only go into grids that allow it (loot containers), never into pockets or another backpack.</summary>
        public bool AcceptsFilledBackpacks { get; }

        private readonly ItemInstance[] cells;

        public ItemGrid(int columns, int rows, bool acceptsFilledBackpacks)
        {
            Columns = columns;
            Rows = rows;
            AcceptsFilledBackpacks = acceptsFilledBackpacks;
            cells = new ItemInstance[columns * rows];
        }

        public ItemInstance this[int index] => cells[index];

        public int Count
        {
            get
            {
                int count = 0;
                foreach (var cell in cells) if (cell != null) count++;
                return count;
            }
        }

        public int FreeCount => Size - Count;
        public bool IsEmpty => Count == 0;

        public bool CanHold(ItemInstance item) => item == null || AcceptsFilledBackpacks || !item.IsFilledBackpack;

        public void Set(int index, ItemInstance item)
        {
            cells[index] = item;
            OnChanged?.Invoke();
        }

        /// <summary>Puts the item in the first empty cell.</summary>
        public bool TryAdd(ItemInstance item)
        {
            if (item == null || !CanHold(item)) return false;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] != null) continue;
                Set(i, item);
                return true;
            }
            return false;
        }

        public bool Remove(ItemInstance item)
        {
            int index = Array.IndexOf(cells, item);
            if (index < 0) return false;
            Set(index, null);
            return true;
        }

        public void Clear()
        {
            Array.Clear(cells, 0, cells.Length);
            OnChanged?.Invoke();
        }

        /// <summary>Swaps the contents of two cells (in the same or different grids). Fails if either grid can't hold what it would receive.</summary>
        public static bool Move(ItemGrid from, int fromIndex, ItemGrid to, int toIndex)
        {
            if (from == to && fromIndex == toIndex) return false;
            var moving = from[fromIndex];
            var displaced = to[toIndex];
            if (moving == null) return false;
            // Rearranging inside one grid is always fine (e.g. a filled backpack just taken off into the pockets).
            if (from != to && (!to.CanHold(moving) || !from.CanHold(displaced))) return false;

            from.cells[fromIndex] = displaced;
            to.cells[toIndex] = moving;
            from.OnChanged?.Invoke();
            if (to != from) to.OnChanged?.Invoke();
            return true;
        }
    }
}
