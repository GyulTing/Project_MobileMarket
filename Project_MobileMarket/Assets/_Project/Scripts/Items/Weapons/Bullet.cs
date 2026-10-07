using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Enemy;

namespace MobileMarket.Items
{
    /// <summary>
    /// Straight-line projectile, no gravity (quarter-view — arcs aren't needed).
    /// Moves exactly speed x deltaTime each frame, so its speed doesn't depend on the frame rate, and raycasts the
    /// stretch it covers so it can't skip through thin colliders. Trigger volumes and the shooter's allies are passed through.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        [SerializeField] private float speed = 30f;
        [SerializeField] private float maxLifetime = 3f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private float damage;
        private Collider ignoreCollider;
        private FactionMember shooterFaction;

        /// <param name="shooter">When given, the bullet passes through members of the shooter's faction (no friendly fire).</param>
        public void Launch(Vector3 direction, float bulletDamage, Collider shooterCollider, FactionMember shooter = null)
        {
            damage = bulletDamage;
            ignoreCollider = shooterCollider;
            shooterFaction = shooter;
            transform.forward = direction;
            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            Vector3 origin = transform.position;
            Vector3 direction = transform.forward;
            float step = speed * Time.deltaTime;

            if (TryFindHit(origin, direction, step, out RaycastHit hit))
            {
                transform.position = hit.point;
                hit.collider.GetComponent<IHitReaction>()?.OnHit(hit.point, direction);
                var damageable = hit.collider.GetComponent<IDamageable>();
                damageable?.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }

            transform.position = origin + direction * step;
        }

        /// <summary>Nearest collider along this frame's path, skipping the shooter and the shooter's allies.</summary>
        private bool TryFindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(origin, direction, HitBuffer, distance, hitLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (hit.collider == ignoreCollider || hit.distance >= best || IsAlly(hit.collider)) continue;
                best = hit.distance;
                nearest = hit;
            }
            return best < float.MaxValue;
        }

        private bool IsAlly(Collider other)
        {
            if (shooterFaction == null) return false;
            var member = other.GetComponent<FactionMember>();
            return member != null && !shooterFaction.IsHostileTo(member);
        }
    }
}
