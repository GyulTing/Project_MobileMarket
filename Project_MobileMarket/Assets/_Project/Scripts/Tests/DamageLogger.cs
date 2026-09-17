using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Tests
{
    /// <summary>Temporary IDamageable stand-in for verifying weapon damage before a real health system exists.</summary>
    public class DamageLogger : MonoBehaviour, IDamageable
    {
        public void TakeDamage(float amount)
        {
            Debug.Log($"[DamageLogger] {gameObject.name} took {amount} damage");
        }
    }
}
