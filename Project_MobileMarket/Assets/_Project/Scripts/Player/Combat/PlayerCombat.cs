using System;
using UnityEngine;
using UnityEngine.EventSystems;
using MobileMarket.Common;
using MobileMarket.Data;
using MobileMarket.Items;
using MobileMarket.Systems;

namespace MobileMarket.Player
{
    /// <summary>Fires a bullet in the direction the player faces. Damage/fire rate/aim speed come from the weapon in hand,
    /// which is whatever sits in the Inventory's Primary slot.
    /// Firing requires both aiming and being armed (hold R to draw/holster the weapon, tap R to reload;
    /// holding the fire button also draws it).</summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private float armHoldDuration = 0.5f;
        [SerializeField] private Transform weaponSocket;
        [Tooltip("Length of the reload clip on the Animator's Reload layer; its playback speed is scaled to each weapon's reloadTime.")]
        [SerializeField] private float reloadClipLength = 1.7f;
        [Tooltip("Seconds for the Reload layer to fade in/out.")]
        [SerializeField] private float reloadBlendTime = 0.15f;
        [SerializeField] private float pelletSpreadAngle = 5f;

        private const string ReloadLayerName = "Reload";
        private const float SingleShotMinInterval = 0.05f;
        private static readonly int ReloadStateHash = Animator.StringToHash("Reload");
        private static readonly int IsReloadingParam = Animator.StringToHash("IsReloading");
        private static readonly int ReloadSpeedParam = Animator.StringToHash("ReloadSpeed");

        public event Action<WeaponData> OnWeaponEquipped;
        public event Action<bool> OnArmedChanged;
        public event Action<int, int> OnAmmoChanged;
        /// <summary>Raised once per trigger pull that fires (once per shotgun blast, not per pellet).</summary>
        public event Action<WeaponData> OnFired;
        public WeaponData EquippedWeapon => equippedWeapon;
        public Transform WeaponSocket => weaponSocket;
        public bool IsArmed { get; private set; }
        public int CurrentAmmo => currentAmmo;
        public bool IsReloading => isReloading;

        /// <summary>Tip of the weapon in hand (forward = barrel direction), or null while unarmed.</summary>
        public Transform Muzzle { get; private set; }

        private void Equip(WeaponData weapon)
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
        private PlayerMovement movement;
        private Collider ownCollider;
        private PlayerHealth health;
        private Animator animator;
        private int reloadLayer = -1;
        private WeaponData equippedWeapon;
        private GameObject currentWeaponModel;
        private int currentAmmo;
        private bool isReloading;
        private float reloadEndTime;
        private float nextFireTime;
        private float armHoldTimer;
        private float fireHoldTimer;
        private bool hasToggledThisHold;
        private bool reloadTapped;

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            inventory = GetComponent<Inventory>();
            movement = GetComponent<PlayerMovement>();
            ownCollider = GetComponent<Collider>();
            health = GetComponent<PlayerHealth>();
            animator = GetComponentInChildren<Animator>();
            if (animator != null) reloadLayer = animator.GetLayerIndex(ReloadLayerName);
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.OnInventoryChanged += SyncWeaponWithSlot;
            if (health != null) health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.OnInventoryChanged -= SyncWeaponWithSlot;
            if (health != null) health.OnDied -= HandleDied;
        }

        private void HandleDied() => enabled = false;

        private void Update()
        {
            UpdateArmToggle();
            UpdateFireHoldToArm();
            UpdateReload();
            UpdateReloadLayer();

            if (!IsArmed || !input.IsAiming || equippedWeapon == null || bulletPrefab == null) return;
            bool trigger = equippedWeapon.fireMode == FireMode.Single ? input.IsFireJustPressed : input.IsFirePressed;
            if (!trigger) return;
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

            if (!reloadTapped || !IsArmed || equippedWeapon == null) return;
            if (currentAmmo >= equippedWeapon.magazineSize) return;

            float reloadTime = Mathf.Max(0.1f, equippedWeapon.reloadTime);
            reloadEndTime = Time.time + reloadTime;
            isReloading = true;
            if (animator == null || reloadLayer < 0) return;

            animator.SetFloat(ReloadSpeedParam, reloadClipLength / reloadTime);
            animator.SetBool(IsReloadingParam, true);
            animator.Play(ReloadStateHash, reloadLayer, 0f);
        }

        /// <summary>
        /// Fades the Reload layer's weight in while reloading and out afterwards. The layer only leaves the Reload state
        /// once its weight is 0, because its idle (empty) state would otherwise pull the upper body into the default pose.
        /// </summary>
        private void UpdateReloadLayer()
        {
            if (animator == null || reloadLayer < 0) return;

            float weight = Mathf.MoveTowards(animator.GetLayerWeight(reloadLayer), isReloading ? 1f : 0f, Time.deltaTime / reloadBlendTime);
            animator.SetLayerWeight(reloadLayer, weight);
            if (!isReloading && weight <= 0f) animator.SetBool(IsReloadingParam, false);
        }

        /// <summary>R key: holding it for armHoldDuration draws/holsters the weapon; releasing it sooner is a tap (reload).</summary>
        private void UpdateArmToggle()
        {
            reloadTapped = false;
            if (!input.IsWeaponKeyHeld)
            {
                if (input.IsWeaponKeyReleased && !hasToggledThisHold) reloadTapped = true;
                armHoldTimer = 0f;
                hasToggledThisHold = false;
                return;
            }

            if (hasToggledThisHold) return;

            armHoldTimer += Time.deltaTime;
            if (armHoldTimer < armHoldDuration) return;

            hasToggledThisHold = true;
            SetArmed(!IsArmed);
        }

        /// <summary>
        /// Holding the fire button for armHoldDuration while unarmed draws the weapon (it never holsters it — that's R only,
        /// so long bursts can't put the gun away). Ignored over UI and when no weapon is equipped.
        /// </summary>
        private void UpdateFireHoldToArm()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (IsArmed || equippedWeapon == null || !input.IsFirePressed || overUi)
            {
                fireHoldTimer = 0f;
                return;
            }

            fireHoldTimer += Time.deltaTime;
            if (fireHoldTimer < armHoldDuration) return;

            fireHoldTimer = 0f;
            SetArmed(true);
        }

        private void SetArmed(bool armed)
        {
            IsArmed = armed;
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
            Muzzle = null;

            if (!IsArmed || equippedWeapon == null || equippedWeapon.weaponModelPrefab == null || weaponSocket == null) return;

            var prefabTransform = equippedWeapon.weaponModelPrefab.transform;
            currentWeaponModel = Instantiate(equippedWeapon.weaponModelPrefab, weaponSocket);
            currentWeaponModel.transform.localPosition = prefabTransform.localPosition;
            currentWeaponModel.transform.localRotation = prefabTransform.localRotation;
            currentWeaponModel.transform.localScale = prefabTransform.localScale;
            CreateMuzzle(currentWeaponModel.transform);
        }

        /// <summary>
        /// Muzzle = the front tip of the held model (weapon models point their barrel along local +Z),
        /// facing down the barrel. Bullets leave from here and the aim rotation lines it up with the cursor.
        /// The position is pre-baked into WeaponData.muzzleOffset (MobileMarket > Items > Bake Weapon Muzzles).
        /// </summary>
        private void CreateMuzzle(Transform model)
        {
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = equippedWeapon.muzzleOffset;
            muzzle.localRotation = Quaternion.identity;
            Muzzle = muzzle;
        }

        private void Fire()
        {
            // Semi-auto fires on every click (only a tiny gap against double-registered clicks); auto uses the weapon's fire rate.
            float interval = equippedWeapon.fireMode == FireMode.Single ? SingleShotMinInterval : 1f / equippedWeapon.fireRate;
            nextFireTime = Time.time + interval;
            currentAmmo--;

            // From the muzzle toward the aim point; falls back to the body if no weapon model is shown.
            Vector3 origin = Muzzle != null ? Muzzle.position : transform.position + Vector3.up + transform.forward * 0.5f;
            Vector3 aimDirection = transform.forward;
            if (movement != null && movement.HasAimPoint)
            {
                Vector3 toAim = movement.AimPoint - origin;
                toAim.y = 0f;
                if (toAim.sqrMagnitude > 0.25f) aimDirection = toAim.normalized;
            }

            int pellets = Mathf.Max(1, equippedWeapon.pelletCount);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 direction = aimDirection;
                if (pellets > 1)
                {
                    float yaw = UnityEngine.Random.Range(-pelletSpreadAngle, pelletSpreadAngle);
                    float pitch = UnityEngine.Random.Range(-pelletSpreadAngle, pelletSpreadAngle);
                    direction = Quaternion.Euler(pitch, yaw, 0f) * direction;
                }

                var bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(direction));
                bullet.Launch(direction, equippedWeapon.damage, ownCollider);
            }

            OnFired?.Invoke(equippedWeapon);
            OnAmmoChanged?.Invoke(currentAmmo, equippedWeapon.magazineSize);
        }

        /// <summary>Keeps the weapon in hand matching the Primary slot.</summary>
        private void SyncWeaponWithSlot()
        {
            var weapon = inventory.GetSlot(EquipSlot.Primary)?.Data as WeaponData;
            if (weapon != equippedWeapon) Equip(weapon);
        }
    }
}
