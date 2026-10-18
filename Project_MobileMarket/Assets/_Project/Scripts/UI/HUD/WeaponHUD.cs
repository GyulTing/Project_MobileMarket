using UnityEngine;
using UnityEngine.UI;
using MobileMarket.Items;
using MobileMarket.Player;

namespace MobileMarket.UI
{
    /// <summary>
    /// Bottom-right weapon readout: icon of the weapon in hand, rounds in the magazine and reserve ammo, plus the grenade slots.
    /// Reserve ammo and grenades have no system yet, so they show placeholders.
    /// </summary>
    public class WeaponHUD : MonoBehaviour
    {
        private const string NoValue = "--";

        [SerializeField] private PlayerCombat playerCombat;
        [Tooltip("Weapon part (icon + ammo); hidden while no weapon is equipped. Grenade slots stay visible.")]
        [SerializeField] private GameObject weaponGroup;
        [SerializeField] private Image weaponIcon;
        [SerializeField] private Text magazineText;
        [SerializeField] private Text reserveText;
        [Tooltip("Count labels of the grenade slots (frag, flashbang).")]
        [SerializeField] private Text[] grenadeCounts;
        [SerializeField] private Color ammoColor = Color.white;
        [Tooltip("Magazine count tint when it's empty.")]
        [SerializeField] private Color emptyAmmoColor = new Color(0.86f, 0.25f, 0.2f);

        private void OnEnable()
        {
            if (playerCombat == null) return;
            playerCombat.OnWeaponEquipped += RefreshWeapon;
            playerCombat.OnAmmoChanged += RefreshAmmo;
        }

        private void OnDisable()
        {
            if (playerCombat == null) return;
            playerCombat.OnWeaponEquipped -= RefreshWeapon;
            playerCombat.OnAmmoChanged -= RefreshAmmo;
        }

        private void Start()
        {
            if (reserveText != null) reserveText.text = "/" + NoValue;
            if (grenadeCounts != null)
                foreach (var count in grenadeCounts) if (count != null) count.text = NoValue;

            var weapon = playerCombat != null ? playerCombat.EquippedWeapon : null;
            RefreshWeapon(weapon);
            if (weapon != null) RefreshAmmo(playerCombat.CurrentAmmo, weapon.magazineSize);
        }

        private void RefreshWeapon(WeaponData weapon)
        {
            if (weaponGroup != null) weaponGroup.SetActive(weapon != null);
            if (weapon == null || weaponIcon == null) return;

            // The silhouette material makes either sprite a flat glyph in the icon's color.
            var sprite = weapon.hudIcon != null ? weapon.hudIcon : weapon.icon;
            weaponIcon.sprite = sprite;
            weaponIcon.enabled = sprite != null;
        }

        private void RefreshAmmo(int current, int magazineSize)
        {
            if (magazineText == null) return;
            magazineText.text = current.ToString();
            magazineText.color = current <= 0 ? emptyAmmoColor : ammoColor;
        }
    }
}
