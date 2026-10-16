using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// A round that flies to a fixed ground point over a set time (40 mm shell: flat and fast; S-5 rocket: lobbed in an arc)
    /// and explodes there: everything on the player side within the radius takes damage once, an explosion effect is
    /// spawned, and the round goes back to the pool. Pooled.
    /// </summary>
    public class BossArtilleryRound : MonoBehaviour
    {
        private static readonly Collider[] OverlapBuffer = new Collider[16];

        [SerializeField] private GameObject explosionPrefab;

        private Vector3 start, end;
        private float flightTime, arcHeight, elapsed;
        private float damage, radius;
        private bool flying;
        private TrailRenderer[] trails;
        private ParticleSystem[] particles;

        private void Awake()
        {
            trails = GetComponentsInChildren<TrailRenderer>();
            particles = GetComponentsInChildren<ParticleSystem>();
        }

        public void Launch(Vector3 from, Vector3 groundTarget, float time, float arc, float roundDamage, float blastRadius)
        {
            start = from; end = groundTarget; flightTime = Mathf.Max(0.05f, time); arcHeight = arc;
            damage = roundDamage; radius = blastRadius; elapsed = 0f; flying = true;
            transform.position = from;
            foreach (var t in trails) t.Clear();
            foreach (var p in particles) { p.Clear(); p.Play(); }
        }

        private void Update()
        {
            if (!flying) return;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightTime);
            Vector3 next = Vector3.Lerp(start, end, t) + Vector3.up * (arcHeight * 4f * t * (1f - t));
            Vector3 delta = next - transform.position;
            if (delta.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(delta);
            transform.position = next;
            if (t >= 1f) Explode();
        }

        private void Explode()
        {
            flying = false;
            int count = Physics.OverlapSphereNonAlloc(end, radius, OverlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var member = OverlapBuffer[i].GetComponent<FactionMember>();
                if (member == null || member.Faction != Faction.Player) continue;
                OverlapBuffer[i].GetComponent<IDamageable>()?.TakeDamage(damage);
            }
            if (explosionPrefab != null)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Get(explosionPrefab, end, Quaternion.identity);
                else Instantiate(explosionPrefab, end, Quaternion.identity);
            }
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
