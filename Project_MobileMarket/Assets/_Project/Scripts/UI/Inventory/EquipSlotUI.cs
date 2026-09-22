using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using MobileMarket.Items;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>Drop target for equipping a weapon dragged from a BagSlotUI. Item stays in the bag; this just marks it equipped.</summary>
    public class EquipSlotUI : MonoBehaviour, IDropHandler
    {
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private Text label;

        private void OnEnable()
        {
            if (playerCombat != null) playerCombat.OnWeaponEquipped += Refresh;
            Refresh(playerCombat != null ? playerCombat.EquippedWeapon : null);
        }

        private void OnDisable()
        {
            if (playerCombat != null) playerCombat.OnWeaponEquipped -= Refresh;
        }

        public void OnDrop(PointerEventData eventData)
        {
            var slot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<BagSlotUI>() : null;
            if (slot == null || slot.Item == null) return;
            if (slot.Item is WeaponData weapon) playerCombat.Equip(weapon);
        }

        private void Refresh(WeaponData weapon)
        {
            if (label != null) label.text = weapon != null ? "장착: " + weapon.itemName : "장착 없음";
        }
    }
}
