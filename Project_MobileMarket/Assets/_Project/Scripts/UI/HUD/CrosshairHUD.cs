using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using MobileMarket.Items;
using MobileMarket.Managers;
using MobileMarket.Player;
using PlayerInputReader = MobileMarket.Player.PlayerInput;

namespace MobileMarket.UI
{
    /// <summary>
    /// Mouse crosshair that replaces the OS cursor during play. Around the aim ring: a red health arc (9 o'clock to 12,
    /// shrinking from the 12 end) and a gray magazine arc (3 o'clock to 6, shrinking from the 3 end; hidden without a drawn
    /// weapon). Faint while not aiming, solid while aiming. Over UI or while a screen owns the input, the cursor comes back.
    /// </summary>
    public class CrosshairHUD : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;
        [Tooltip("Filled image (Radial 360, origin Left, clockwise): fill 0.25 = full health.")]
        [SerializeField] private Image healthFill;
        [Tooltip("Filled image (Radial 360, origin Bottom, counter-clockwise): fill 0.25 = full magazine.")]
        [SerializeField] private Image ammoFill;
        [Tooltip("Ammo arc and its track; hidden while no weapon is drawn.")]
        [SerializeField] private GameObject ammoGroup;
        [SerializeField] private float idleAlpha = 0.55f;
        [SerializeField] private float aimAlpha = 1f;

        private const float QuarterFill = 0.25f;

        private PlayerInputReader input;
        private bool inputBlocked;
        private bool hasWeapon;

        private void Awake()
        {
            if (playerCombat != null) input = playerCombat.GetComponent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            UIManager.OnGameplayInputBlocked += HandleInputBlocked;
            if (playerHealth != null) playerHealth.OnHealthChanged += RefreshHealth;
            if (playerCombat != null)
            {
                playerCombat.OnAmmoChanged += RefreshAmmo;
                playerCombat.OnWeaponEquipped += HandleWeaponEquipped;
                playerCombat.OnArmedChanged += HandleArmedChanged;
            }
        }

        private void OnDisable()
        {
            UIManager.OnGameplayInputBlocked -= HandleInputBlocked;
            if (playerHealth != null) playerHealth.OnHealthChanged -= RefreshHealth;
            if (playerCombat != null)
            {
                playerCombat.OnAmmoChanged -= RefreshAmmo;
                playerCombat.OnWeaponEquipped -= HandleWeaponEquipped;
                playerCombat.OnArmedChanged -= HandleArmedChanged;
            }
            Cursor.visible = true;
        }

        private void Start()
        {
            if (playerHealth != null) RefreshHealth(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            var weapon = playerCombat != null ? playerCombat.EquippedWeapon : null;
            HandleWeaponEquipped(weapon);
            if (weapon != null) RefreshAmmo(playerCombat.CurrentAmmo, weapon.magazineSize);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool show = mouse != null && !inputBlocked && !overUi;
            root.gameObject.SetActive(show);
            Cursor.visible = !show;
            if (!show) return;

            root.position = mouse.position.ReadValue();
            group.alpha = input != null && input.IsAiming ? aimAlpha : idleAlpha;
        }

        private void HandleInputBlocked(bool blocked) => inputBlocked = blocked;

        private void RefreshHealth(float current, float max)
        {
            float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            healthFill.fillAmount = ratio * QuarterFill;
        }

        private void RefreshAmmo(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            ammoFill.fillAmount = ratio * QuarterFill;
        }

        private void HandleWeaponEquipped(WeaponData weapon)
        {
            hasWeapon = weapon != null;
            RefreshAmmoVisibility();
        }

        private void HandleArmedChanged(bool armed) => RefreshAmmoVisibility();

        private void RefreshAmmoVisibility()
        {
            if (ammoGroup != null) ammoGroup.SetActive(hasWeapon && playerCombat != null && playerCombat.IsArmed);
        }
    }
}
