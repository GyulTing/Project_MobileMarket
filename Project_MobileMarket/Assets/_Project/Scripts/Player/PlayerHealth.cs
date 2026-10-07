using System;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Player
{
    /// <summary>
    /// Player hit points. Bullets and zombie swipes call TakeDamage; at 0 the death animation plays (Animator's Death layer)
    /// and OnDied fires so movement, combat and interaction switch themselves off. No respawn yet.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable, IHealth
    {
        private const string DeathLayerName = "Death";
        private static readonly int DeathStateHash = Animator.StringToHash("Death");

        [SerializeField] private float maxHealth = 100f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsDead { get; private set; }

        private Animator animator;
        private int deathLayer = -1;

        private void Awake()
        {
            CurrentHealth = maxHealth;
            animator = GetComponentInChildren<Animator>();
            if (animator != null) deathLayer = animator.GetLayerIndex(DeathLayerName);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
            if (CurrentHealth <= 0f) Die();
        }

        private void Die()
        {
            IsDead = true;
            if (animator != null && deathLayer >= 0)
            {
                // Full-body layer on top of everything else, off (weight 0) until now.
                animator.SetLayerWeight(deathLayer, 1f);
                animator.Play(DeathStateHash, deathLayer, 0f);
            }
            OnDied?.Invoke();
        }
    }
}
