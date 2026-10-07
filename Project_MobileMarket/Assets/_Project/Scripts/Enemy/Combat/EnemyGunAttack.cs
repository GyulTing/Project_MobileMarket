using UnityEngine;
using MobileMarket.Items;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Human enemy gunfire: holds a weapon model in the right hand, stops in range, aims and fires P_Bullet with some spread.
    /// Bullets pass through members of the same faction, so allies in the line of fire are never hit.
    /// </summary>
    public class EnemyGunAttack : EnemyAttack
    {
        [Tooltip("Gun model and muzzle position come from this weapon; damage/fire rate below are the enemy's own.")]
        [SerializeField] private WeaponData weapon;
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField] private float range = 12f;
        [SerializeField] private float damage = 10f;
        [Tooltip("Shots per second.")]
        [SerializeField] private float fireRate = 2f;
        [SerializeField] private float spreadAngle = 4f;
        [Tooltip("Seconds spent raising the gun before the first shot.")]
        [SerializeField] private float aimDelay = 0.4f;
        [Tooltip("World size of the held gun model (the player holds guns at 0.8).")]
        [SerializeField] private float weaponScale = 0.8f;

        public override float Range => range;

        private Animator animator;
        private Collider ownCollider;
        private FactionMember self;
        private Transform hand;
        private Transform weaponModel;
        private Transform muzzle;
        private bool isAiming;
        private float nextFireTime;
        private Vector3 aimDirection;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ownCollider = GetComponent<Collider>();
            self = GetComponent<FactionMember>();
            if (animator != null && animator.isHuman) hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            CreateWeaponModel();
        }

        private void OnEnable()
        {
            isAiming = false;
            aimDirection = transform.forward;
        }

        private void CreateWeaponModel()
        {
            if (weapon == null || weapon.weaponModelPrefab == null || hand == null) return;

            weaponModel = Instantiate(weapon.weaponModelPrefab, hand).transform;
            foreach (var col in weaponModel.GetComponentsInChildren<Collider>()) Destroy(col);
            Vector3 handScale = hand.lossyScale;
            weaponModel.localScale = new Vector3(weaponScale / handScale.x, weaponScale / handScale.y, weaponScale / handScale.z);

            var muzzlePoint = new GameObject("Muzzle").transform;
            muzzlePoint.SetParent(weaponModel, false);
            muzzlePoint.localPosition = weapon.muzzleOffset;
            muzzle = muzzlePoint;
        }

        public override void Tick(FactionMember target)
        {
            if (animator != null) animator.SetBool(AttackingParam, true);
            aimDirection = (target.AimPoint - MuzzlePosition).normalized;

            if (!isAiming)
            {
                isAiming = true;
                nextFireTime = Time.time + aimDelay;
            }
            if (Time.time < nextFireTime) return;

            nextFireTime = Time.time + 1f / fireRate;
            Fire();
        }

        public override void End()
        {
            if (animator != null) animator.SetBool(AttackingParam, false);
            isAiming = false;
        }

        private Vector3 MuzzlePosition => muzzle != null ? muzzle.position : self.AimPoint + transform.forward * 0.5f;

        private void Fire()
        {
            if (bulletPrefab == null) return;

            Vector3 direction = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle) * 0.5f, Random.Range(-spreadAngle, spreadAngle), 0f) * aimDirection;
            var bullet = Instantiate(bulletPrefab, MuzzlePosition, Quaternion.LookRotation(direction));
            bullet.Launch(direction, damage, ownCollider, self);
        }

        /// <summary>
        /// The gun hangs from the right hand (so it follows the animation) but is pointed by code: at the target while aiming,
        /// along the body's forward and tipped down otherwise. Enemy rigs differ, so no per-model hold pose is needed.
        /// </summary>
        private void LateUpdate()
        {
            if (weaponModel == null) return;

            Vector3 forward = isAiming ? aimDirection : Quaternion.AngleAxis(35f, transform.right) * transform.forward;
            weaponModel.SetPositionAndRotation(hand.position, Quaternion.LookRotation(forward, Vector3.up));
        }
    }
}
