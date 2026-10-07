using UnityEngine;

namespace MobileMarket.Data
{
    /// <summary>Player equipment slots. Every weapon goes into Primary.</summary>
    public enum EquipSlot
    {
        Head,
        Body,
        Backpack,
        Primary,
    }

    /// <summary>Wearable gear (gas mask, body armor, backpack). Goes into the matching equipment slot and is shown on the player.</summary>
    [CreateAssetMenu(fileName = "EquipmentData", menuName = "MobileMarket/Equipment Data")]
    public class EquipmentData : ItemData
    {
        public EquipSlot slot = EquipSlot.Head;

        [Header("Worn on the player")]
        [Tooltip("Model shown on the character while equipped.")]
        public GameObject wornModel;
        [Tooltip("Position of the model's center relative to the bone it hangs on (Head for masks, upper chest for armor/backpacks).")]
        public Vector3 wornPosition;
        public Vector3 wornRotation;
        [Tooltip("Largest dimension of the worn model, in meters.")]
        public float wornSize = 0.5f;
    }
}
