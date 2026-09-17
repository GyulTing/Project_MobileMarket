using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Items
{
    /// <summary>Straight-line projectile, no gravity (quarter-view — arcs aren't needed).</summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float speed = 30f;
        [SerializeField] private float maxLifetime = 3f;

        private Rigidbody rb;
        private float damage;
        private Collider ignoreCollider;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        public void Launch(Vector3 direction, float bulletDamage, Collider shooterCollider)
        {
            damage = bulletDamage;
            ignoreCollider = shooterCollider;
            transform.forward = direction;
            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            rb.MovePosition(transform.position + transform.forward * speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == ignoreCollider) return;

            var damageable = other.GetComponent<IDamageable>();
            damageable?.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
