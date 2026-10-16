using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// The boss's straight-flying round (20 mm). Pooled. Sweeps the distance it covers each frame so it can't skip through
    /// thin colliders; passes through the boss's own parts (turrets, vents, walls of the rig under the ignored root),
    /// hurts only the player side, and stops on anything else (walls, cover).
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        [SerializeField] private float radius = 0.12f;
        [Tooltip("Safety net only: long enough to cross the whole arena, so rounds end on walls/cover, not in mid-air.")]
        [SerializeField] private float maxLifetime = 15f;
        [Tooltip("Spawned where the round hits anything but the player (sparks on walls and cover).")]
        [SerializeField] private GameObject impactPrefab;

        private float speed;
        private float damage;
        private float levelHeight;
        private Transform ignoreRoot;
        private float dieTime;
        private TrailRenderer trail;

        private void Awake() => trail = GetComponentInChildren<TrailRenderer>();

        /// <param name="flyLevelAt">Once a descending round comes down to this height it flies on level from there.</param>
        public void Launch(Vector3 direction, float projectileSpeed, float projectileDamage, Transform ownerRoot, float flyLevelAt = float.NegativeInfinity)
        {
            transform.forward = direction;
            if (trail != null) trail.Clear();   // reused from the pool: don't draw a streak from its last spot
            speed = projectileSpeed;
            damage = projectileDamage;
            ignoreRoot = ownerRoot;
            levelHeight = flyLevelAt;
            dieTime = Time.time + maxLifetime;
        }

        private void Update()
        {
            if (Time.time >= dieTime) { Despawn(); return; }

            Vector3 origin = transform.position;
            Vector3 dir = transform.forward;
            float step = speed * Time.deltaTime;

            int count = Physics.SphereCastNonAlloc(origin, radius, dir, HitBuffer, step, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; int bestIndex = -1;
            for (int i = 0; i < count; i++)
            {
                var hit = HitBuffer[i];
                if (hit.distance >= best || IsBossPart(hit.collider)) continue;
                best = hit.distance; bestIndex = i;
            }
            if (bestIndex >= 0)
            {
                var hit = HitBuffer[bestIndex];
                var member = hit.collider.GetComponent<FactionMember>();
                if (member != null && member.Faction == Faction.Player)
                {
                    hit.collider.GetComponent<IHitReaction>()?.OnHit(hit.point, dir);
                    hit.collider.GetComponent<IDamageable>()?.TakeDamage(damage);
                }
                else SpawnImpact(hit);
                Despawn();
                return;
            }

            Vector3 next = origin + dir * step;
            // Came down to chest height: level out and carry on across the room.
            if (dir.y < 0f && next.y <= levelHeight)
            {
                next.y = levelHeight;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f) transform.forward = dir.normalized;
            }
            transform.position = next;
        }

        /// <summary>The boss's own rig (any turret or camera, the boss body) never stops its rounds; walls, vents and cover do.</summary>
        private bool IsBossPart(Collider c)
        {
            if (ignoreRoot != null && c.transform.IsChildOf(ignoreRoot)) return true;
            return c.GetComponentInParent<TurretAim>() != null || c.GetComponentInParent<BossController>() != null;
        }

        private void SpawnImpact(RaycastHit hit)
        {
            if (impactPrefab == null) return;
            // Sparks fly back out of the surface.
            var rot = Quaternion.LookRotation(hit.normal.sqrMagnitude > 0.01f ? hit.normal : -transform.forward);
            if (PoolManager.Instance != null) PoolManager.Instance.Get(impactPrefab, hit.point, rot);
            else Instantiate(impactPrefab, hit.point, rot);
        }

        private void Despawn()
        {
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
