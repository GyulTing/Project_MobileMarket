using MobileMarket.Data;
using MobileMarket.Items;

namespace MobileMarket.Systems
{
    /// <summary>
    /// One physical item. ItemData is the shared definition; the instance is what sits in a grid cell or equipment slot.
    /// Backpacks carry their own contents grid, so their items travel with them.
    /// </summary>
    public class ItemInstance
    {
        public ItemData Data { get; }
        public ItemGrid Contents { get; }

        public ItemInstance(ItemData data)
        {
            Data = data;
            if (data is BackpackData backpack) Contents = new ItemGrid(backpack.columns, backpack.rows, false);
        }

        public bool IsFilledBackpack => Contents != null && !Contents.IsEmpty;

        /// <summary>The equipment slot this item can go into, or null if it can't be equipped.</summary>
        public EquipSlot? Slot => SlotOf(Data);

        public static EquipSlot? SlotOf(ItemData data)
        {
            if (data is EquipmentData equipment) return equipment.slot;
            if (data is WeaponData) return EquipSlot.Primary;
            return null;
        }
    }
}
