using UnityEngine;

namespace MobileMarket.Data
{
    /// <summary>A backpack: worn in the Backpack slot, and carries its own columns x rows item grid.</summary>
    [CreateAssetMenu(fileName = "BackpackData", menuName = "MobileMarket/Backpack Data")]
    public class BackpackData : EquipmentData
    {
        [Min(1)] public int columns = 4;
        [Min(1)] public int rows = 4;

        [Header("Inventory screen")]
        [Tooltip("Drawing of this backpack shown behind the grid while it is worn.")]
        public Sprite panelArt;
        [Tooltip("Where the grid sits on panelArt, normalized (0..1, origin bottom-left) — the flat front pocket.")]
        public Rect gridArea = new Rect(0.2f, 0.2f, 0.6f, 0.6f);

        private void OnValidate()
        {
            slot = EquipSlot.Backpack;
        }
    }
}
