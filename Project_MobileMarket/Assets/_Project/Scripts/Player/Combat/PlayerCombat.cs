using UnityEngine;
using UnityEngine.EventSystems;
using MobileMarket.Common;
using MobileMarket.Items;

namespace MobileMarket.Player
{
    /// <summary>Fires a bullet in the direction the player faces. Damage comes from the equipped weapon.</summary>
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private WeaponData equippedWeapon;
        [SerializeField] private Bullet bulletPrefab;

        private PlayerInput input;
        private Collider ownCollider;

        private void Awake()
        {
            input = GetComponent<PlayerInput>();
            ownCollider = GetComponent<Collider>();
        }

        private void Update()
        {
            if (!input.IsAiming || !input.IsFirePressed || equippedWeapon == null || bulletPrefab == null) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Fire();
        }

        private void Fire()
        {
            Vector3 origin = transform.position + Vector3.up + transform.forward * 0.5f;
            var bullet = Instantiate(bulletPrefab, origin, Quaternion.LookRotation(transform.forward));
            bullet.Launch(transform.forward, equippedWeapon.damage, ownCollider);
        }
    }
}
