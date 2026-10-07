using System;
using System.Collections;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Enemy hit points. Bullets call TakeDamage; a hit plays a hit reaction (at most once per flinchInterval, pausing
    /// attacks for staggerTime via OnStaggered), reaching 0 plays Death, turns off the collider
    /// (so bullets pass over the body) and returns the enemy to the pool after despawnDelay. Reset when reused from the pool.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class EnemyHealth : MonoBehaviour, IDamageable, IHealth
    {
        [Serializable]
        public struct HitReaction
        {
            [Tooltip("Animator state that plays this hit reaction.")]
            public string state;
            [Tooltip("Seconds the hit animation runs.")]
            public float duration;
        }

        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        [SerializeField] private float maxHealth = 100f;
        [Tooltip("Seconds the body stays after death before it goes back to the pool.")]
        [SerializeField] private float despawnDelay = 5f;
        [Tooltip("The hit flinch plays at most once per this many seconds, so automatic fire can't keep an enemy flinching forever.")]
        [SerializeField] private float flinchInterval = 1.5f;
        [Tooltip("Seconds a flinch interrupts the enemy's attacks when it uses the Hit trigger (no hit reactions set).")]
        [SerializeField] private float staggerTime = 0.5f;
        [Tooltip("Hit reactions played at random on a flinch (never the same twice in a row). Empty = fire the Hit trigger.")]
        [SerializeField] private HitReaction[] hitReactions = Array.Empty<HitReaction>();
        [Tooltip("Share of the played hit reaction during which attacks stay paused, so the reaction isn't cut off by the next attack.")]
        [Range(0f, 1f)]
        [SerializeField] private float hitStaggerFraction = 0.8f;

        public event Action<float, float> OnHealthChanged;
        public event Action<EnemyHealth> OnDied;
        /// <summary>Raised when a hit makes the enemy flinch; the argument is how long attacks should pause.</summary>
        public event Action<float> OnStaggered;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        private Animator animator;
        private Collider bodyCollider;
        private WaitForSeconds despawnWait;
        private float nextFlinchTime;
        private int[] hitStateHashes;
        private int lastHitState = -1;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            bodyCollider = GetComponent<Collider>();
            despawnWait = new WaitForSeconds(despawnDelay);
            hitStateHashes = new int[hitReactions.Length];
            for (int i = 0; i < hitReactions.Length; i++) hitStateHashes[i] = Animator.StringToHash(hitReactions[i].state);
        }

        private void OnEnable()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
            nextFlinchTime = 0f;
            lastHitState = -1;
            bodyCollider.enabled = true;
            if (animator != null)
            {
                animator.Rebind();
                animator.SetBool(DeadParam, false);
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

            if (CurrentHealth > 0f)
            {
                Flinch();
                return;
            }
            Die();
        }

        private void Flinch()
        {
            if (Time.time < nextFlinchTime) return;
            nextFlinchTime = Time.time + flinchInterval;
            OnStaggered?.Invoke(PlayHitReaction());
        }

        /// <summary>Plays a hit reaction and returns how long attacks should pause for it.</summary>
        private float PlayHitReaction()
        {
            if (hitStateHashes.Length == 0)
            {
                if (animator != null) animator.SetTrigger(HitParam);
                return staggerTime;
            }

            int index = UnityEngine.Random.Range(0, hitStateHashes.Length);
            if (hitStateHashes.Length > 1 && lastHitState >= 0)
            {
                index = UnityEngine.Random.Range(0, hitStateHashes.Length - 1);
                if (index >= lastHitState) index++;
            }
            lastHitState = index;
            if (animator != null) animator.CrossFadeInFixedTime(hitStateHashes[index], 0.05f);
            return hitReactions[index].duration * hitStaggerFraction;
        }

        private void Die()
        {
            IsDead = true;
            bodyCollider.enabled = false;
            if (animator != null) animator.SetBool(DeadParam, true);
            OnDied?.Invoke(this);
            StartCoroutine(DespawnRoutine());
        }

        private IEnumerator DespawnRoutine()
        {
            yield return despawnWait;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
