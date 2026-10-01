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
        [SerializeField] private Transform weaponSocket;
        [SerializeField] private float reloadDuration = 1.5f;
        [SerializeField] private float pelletSpreadAngle = 5f;

        public event Action<WeaponData> OnWeaponEquipped;
        public event Action<bool> OnArmedChanged;
        public event Action<int, int> OnAmmoChanged;
        public WeaponData EquippedWeapon => equippedWeapon;
        public bool IsArmed { get; private set; }
        public int CurrentAmmo => currentAmmo;
        public bool IsReloading => isReloading;

        public void Equip(WeaponData weapon)
        {
            equippedWeapon = weapon;
            currentAmmo = weapon != null ? weapon.magazineSize : 0;
            isReloading = false;
            OnWeaponEquipped?.Invoke(equippedWeapon);
            OnAmmoChanged?.Invoke(currentAmmo, weapon != null ? weapon.magazineSize : 0);
            RefreshWeaponModel();
        }

        private PlayerInput input;
        private Inventory inventory;
        private Collider ownCollider;
        private GameObject currentWeaponModel;
        private int currentAmmo;
        private bool isReloading;
        private float reloadEndTime;
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
            UpdateReload();

            if (!IsArmed || !input.IsAiming || !input.IsFirePressed || equippedWeapon == null || bulletPrefab == null) return;
            if (isReloading || currentAmmo <= 0) return;
            if (Time.time < nextFireTime) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Fire();
        }

        private void UpdateReload()
        {
            if (isReloading)
            {
                if (Time.time < reloadEndTime) return;

                currentAmmo = equippedWeapon.magazineSize;
                isReloading = false;
                OnAmmoChanged?.Invoke(currentAmmo, equippedWeapon.magazineSize);
                return;
            }

            if (!input.IsReloadPressed || !IsArmed || equippedWeapon == null) return;
            if (currentAmmo >= equippedWeapon.magazineSize) return;

            isReloading = true;
            reloadEndTime = Time.time + reloadDuration;
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
            if (!IsArmed) isReloading = false;
            OnArmedChanged?.Invoke(IsArmed);
            RefreshWeaponModel();
        }

        /// <summary>Shows the equipped weapon's model in hand while armed, removes it while unarmed.</summary>
        private void RefreshWeaponModel()
        {
            if (currentWeaponModel != null)
            {
                Destroy(currentWeaponModel);
                currentWeaponModel = null;
            }

            if (!IsArmed || equippedWeapon == null || equippedWeapon.weaponModelPrefab == null || weaponSocket == null) return;

            var prefabTransform = equippedWeapon.weaponModelPrefab.transform;
            currentWeaponModel = Instantiate(equippedWeapon.weaponModelPrefab, weaponSocket);
            currentWeaponModel.transform.localPosition = prefabTransform.localPosition;
            currentWeaponModel.transform.localRotation = prefabTransform.localRotation;
            currentWeaponModel.transform.localScale = prefabTransform.localScale;
        }

        private void Fire()
        {
            nextFireTime = Time.time + 1f / equippedWeapon.fireRate;
            currentAmmo--;

            Vector3 origin = transform.position + Vector3.up + transform.forward * 0.5f;
            int pellets = Mathf.Max(1, equippedWeapon.pelletCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = transform.forward;
                if (pellets > 1)
                {
                    float yaw = UnityEngine.Random.Range(-pelletSpreadAngle, pelletSpreadAngle);
                    float pitch = UnityEngine.Random.Range(-pelletSpreadAngle, pelletSpreadAngle);
                    direction = Quaternion.Euler(pitch, yaw, 0f) * direction;
                }

                var bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));
                bullet.Launch(direction, equippedWeapon.damage, ownCollider);
            }

            OnAmmoChanged?.Invoke(currentAmmo, equippedWeapon.magazineSize);
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
