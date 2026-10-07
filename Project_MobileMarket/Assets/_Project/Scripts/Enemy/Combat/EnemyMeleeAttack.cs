using System;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Zombie melee. Each swing plays one of several attack animations at random (never the same one twice in a row);
    /// the next swing starts when that animation ends and the hit lands at the animation's own impact moment.
    /// Without any swings set up it falls back to the Animator's single Attacking state and a fixed cooldown.
    /// </summary>
    public class EnemyMeleeAttack : EnemyAttack
    {
        [Serializable]
        public struct Swing
        {
            [Tooltip("Animator state that plays this attack.")]
            public string state;
            [Tooltip("Seconds the attack animation runs; the next swing starts after it.")]
            public float duration;
            [Tooltip("Seconds into the animation when the blow lands.")]
            public float hitTime;
        }

        private static readonly int LocomotionHash = Animator.StringToHash("Locomotion");

        [SerializeField] private float range = 1.6f;
        [SerializeField] private float damage = 12f;
        [SerializeField] private Swing[] swings = Array.Empty<Swing>();

        [Header("Fallback (no swings)")]
        [Tooltip("Seconds between swings.")]
        [SerializeField] private float cooldown = 1.3f;
        [Tooltip("Seconds into a swing when the hit lands; the target must still be close.")]
        [SerializeField] private float hitDelay = 0.5f;

        public override float Range => range;

        private Animator animator;
        private EnemyHealth health;
        private int[] swingHashes;
        private int lastSwing = -1;
        private bool swinging;
        private float nextSwingTime;
        private float pendingHitTime = -1f;
        private FactionMember pendingTarget;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            health = GetComponent<EnemyHealth>();
            swingHashes = new int[swings.Length];
            for (int i = 0; i < swings.Length; i++) swingHashes[i] = Animator.StringToHash(swings[i].state);
        }

        private void OnEnable()
        {
            if (health != null) health.OnStaggered += HandleStaggered;
        }

        private void OnDisable()
        {
            if (health != null) health.OnStaggered -= HandleStaggered;
            pendingHitTime = -1f;
            pendingTarget = null;
            swinging = false;
            lastSwing = -1;
        }

        /// <summary>A flinch cancels the blow of the swing in progress and holds off the next swing so the Hit reaction shows.</summary>
        private void HandleStaggered(float duration)
        {
            pendingHitTime = -1f;
            pendingTarget = null;
            swinging = false;
            nextSwingTime = Mathf.Max(nextSwingTime, Time.time + duration);
        }

        public override void Tick(FactionMember target)
        {
            if (swings.Length == 0)
            {
                TickFallback(target);
                return;
            }
            if (Time.time < nextSwingTime) return;

            int index = PickSwing();
            lastSwing = index;
            if (animator != null) animator.CrossFadeInFixedTime(swingHashes[index], 0.1f);
            nextSwingTime = Time.time + swings[index].duration;
            pendingHitTime = Time.time + swings[index].hitTime;
            pendingTarget = target;
            swinging = true;
        }

        private void TickFallback(FactionMember target)
        {
            if (animator != null) animator.SetBool(AttackingParam, true);
            if (Time.time < nextSwingTime) return;

            nextSwingTime = Time.time + cooldown;
            pendingHitTime = Time.time + hitDelay;
            pendingTarget = target;
        }

        /// <summary>Random swing, never the same as the previous one when there is a choice.</summary>
        private int PickSwing()
        {
            if (swings.Length == 1 || lastSwing < 0) return UnityEngine.Random.Range(0, swings.Length);
            int index = UnityEngine.Random.Range(0, swings.Length - 1);
            return index >= lastSwing ? index + 1 : index;
        }

        public override void End()
        {
            pendingHitTime = -1f;
            pendingTarget = null;
            if (animator == null) return;

            if (swings.Length == 0)
            {
                animator.SetBool(AttackingParam, false);
                return;
            }
            // Target got away mid-swing: blend back to walking instead of finishing the swing on the move.
            if (swinging && Time.time < nextSwingTime) animator.CrossFadeInFixedTime(LocomotionHash, 0.15f);
            swinging = false;
        }

        private void Update()
        {
            if (pendingHitTime < 0f || Time.time < pendingHitTime) return;
            pendingHitTime = -1f;

            var target = pendingTarget;
            pendingTarget = null;
            if (target == null || !target.IsAlive) return;

            // A little leeway so a target stepping back mid-swing still gets hit.
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            float reach = range * 1.25f;
            if (offset.sqrMagnitude > reach * reach) return;

            Vector3 hitPoint = target.AimPoint;
            target.GetComponent<IHitReaction>()?.OnHit(hitPoint, hitPoint - (transform.position + Vector3.up * hitPoint.y));
            target.GetComponent<IDamageable>()?.TakeDamage(damage);
        }
    }
}
