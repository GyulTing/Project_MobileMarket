using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MobileMarket.Common;
using MobileMarket.Items;
using MobileMarket.Systems;

namespace MobileMarket.Player
{
    /// <summary>Fires a bullet in the direction the player faces. Damage/fire rate/aim speed come from the equipped weapon.
    /// Firing requires both aiming and being armed (hold R to draw/holster the weapon).</summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private WeaponData equippedWeapon;
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private float armHoldDuration = 0.5f;

        public event Action<WeaponData> OnWeaponEquipped;
        public event Action<bool> OnArmedChanged;
        public WeaponData EquippedWeapon => equippedWeapon;
        public bool IsArmed { get; private set; }

        public void Equip(WeaponData weapon)
        {
            equippedWeapon = weapon;
            OnWeaponEquipped?.Invoke(equippedWeapon);
        }

        private PlayerInput input;
        private Inventory inventory;
        private Collider ownCollider;
        private float nextFireTime;
        private float armHoldTimer;
        private bool hasToggledThisHold;

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            inventory = GetComponent<Inventory>();
            ownCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (input.IsEquipNextPressed) EquipNextFromInventory();

            UpdateArmToggle();

            if (!IsArmed || !input.IsAiming || !input.IsFirePressed || equippedWeapon == null || bulletPrefab == null) return;
            if (Time.time < nextFireTime) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Fire();
        }

        private void UpdateArmToggle()
        {
            if (!input.IsArmTogglePressed)
            {
                armHoldTimer = 0f;
                hasToggledThisHold = false;
                return;
            }

            if (hasToggledThisHold) return;

            armHoldTimer += Time.deltaTime;
            if (armHoldTimer < armHoldDuration) return;

            IsArmed = !IsArmed;
            hasToggledThisHold = true;
            OnArmedChanged?.Invoke(IsArmed);
        }

        private void Fire()
        {
            nextFireTime = Time.time + 1f / equippedWeapon.fireRate;

            Vector3 origin = transform.position + Vector3.up + transform.forward * 0.5f;
            var bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(transform.forward));
            bullet.Launch(transform.forward, equippedWeapon.damage, ownCollider);
        }

        /// <summary>Cycles to the next WeaponData found in the inventory, wrapping back to none if the current weapon is last.</summary>
        private void EquipNextFromInventory()
        {
            if (inventory == null) return;

            var weapons = new List<WeaponData>();
            foreach (var item in inventory.Items)
            {
                if (item is WeaponData weapon) weapons.Add(weapon);
            }
            if (weapons.Count == 0) return;

            int currentIndex = equippedWeapon != null ? weapons.IndexOf(equippedWeapon) : -1;
            Equip(weapons[(currentIndex + 1) % weapons.Count]);
        }
    }
}
