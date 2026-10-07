using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.VFX
{
    /// <summary>Sprays a short blood burst where a hit lands, along the hit direction. Nothing once the owner is dead.</summary>
    public class BloodOnHit : MonoBehaviour, IHitReaction
    {
        [Tooltip("Particle prefab with a PooledParticleEffect (red for humans, dark for zombies).")]
        [SerializeField] private GameObject effectPrefab;

        private IHealth health;

        private void Awake()
        {
            health = GetComponent<IHealth>();
        }

        public void OnHit(Vector3 point, Vector3 direction)
        {
            if (effectPrefab == null || (health != null && health.IsDead)) return;

            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            if (PoolManager.Instance != null) PoolManager.Instance.Get(effectPrefab, point, rotation);
            else Instantiate(effectPrefab, point, rotation);
        }
    }
}
