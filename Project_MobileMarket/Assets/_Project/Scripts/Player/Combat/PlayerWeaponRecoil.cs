using UnityEngine;
using MobileMarket.Items;

namespace MobileMarket.Player
{
    /// <summary>
    /// Procedural recoil: on each shot the chest leans back (raising both arms and the gun together) while the weapon
    /// kicks back slightly along the barrel, then both settle. Applied in LateUpdate on top of the animated pose, so the Animator is untouched.
    /// Strength scales with WeaponData.recoil; rapid fire stacks up to maxStack shots' worth.
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerWeaponRecoil : MonoBehaviour
    {
        // Most of the kick goes through the chest so both arms move with the gun; rotating only the gun around the
        // right-hand grip would lift the handguard off the left hand.
        [Header("Per 1.0 recoil")]
        [SerializeField] private float kickBack = 0.01f;
        [SerializeField] private float muzzleRise = 1f;
        [SerializeField] private float randomYaw = 1.5f;
        [SerializeField] private float chestLean = 6f;

        [Tooltip("Recoil stacks during rapid fire, up to this many single shots' worth.")]
        [SerializeField] private float maxStack = 1.5f;
        [Tooltip("How fast the kick settles (higher = faster; 25 settles in about 0.12s).")]
        [SerializeField] private float recoverySpeed = 25f;

        private PlayerCombat combat;
        private Transform socket;
        private Transform chest;
        private Vector3 socketBasePosition;
        private Quaternion socketBaseRotation;
        private float kick;
        private float yaw;

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            socket = combat.WeaponSocket;
            if (socket != null)
            {
                socketBasePosition = socket.localPosition;
                socketBaseRotation = socket.localRotation;
            }

            var animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            }
        }

        private void OnEnable()
        {
            combat.OnFired += HandleFired;
        }

        private void OnDisable()
        {
            combat.OnFired -= HandleFired;
        }

        private void HandleFired(WeaponData weapon)
        {
            float cap = weapon.recoil * maxStack;
            kick = Mathf.Min(kick + weapon.recoil, cap);
            yaw = Mathf.Clamp(yaw + Random.Range(-randomYaw, randomYaw) * weapon.recoil, -randomYaw * cap, randomYaw * cap);
        }

        private void LateUpdate()
        {
            if (socket == null) return;

            // The socket isn't animated, so put it back to its rest pose before adding this frame's kick.
            socket.localPosition = socketBasePosition;
            socket.localRotation = socketBaseRotation;

            float settle = Mathf.Exp(-recoverySpeed * Time.deltaTime);
            kick *= settle;
            yaw *= settle;
            if (kick < 0.001f)
            {
                kick = 0f;
                yaw = 0f;
                return;
            }

            // Chest first (the animator already wrote this frame's bone pose): lean back around the body's right axis.
            if (chest != null) chest.rotation = Quaternion.AngleAxis(-chestLean * kick, transform.right) * chest.rotation;

            // Weapon: pushed back along the barrel, muzzle rising around the grip (socket), plus a little side-to-side.
            Vector3 barrel = combat.Muzzle != null ? combat.Muzzle.forward : transform.forward;
            Vector3 riseAxis = Vector3.Cross(barrel, Vector3.up);
            if (riseAxis.sqrMagnitude < 0.0001f) riseAxis = -transform.right;

            socket.position -= barrel * (kickBack * kick);
            socket.rotation = Quaternion.AngleAxis(muzzleRise * kick, riseAxis.normalized) * Quaternion.AngleAxis(yaw, Vector3.up) * socket.rotation;
        }
    }
}
