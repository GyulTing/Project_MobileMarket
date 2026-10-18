using System.Collections;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// 20 mm wall gun: lays down dodgeable bullet curtains. Sweep = a stream swung across the room; Fan = volleys of spread
    /// shots aimed at the player, each volley turned a little. The boss runs both guns at once for the crossfire.
    /// </summary>
    [RequireComponent(typeof(TurretAim))]
    public class BossGun20 : MonoBehaviour
    {
        [SerializeField] private BossProjectile bulletPrefab;
        [SerializeField] private float bulletSpeed = 10f;
        [SerializeField] private float damage = 5f;
        [Tooltip("Looping fire sound, on while a sweep or fan is running.")]
        [SerializeField] private AudioSource fireLoop;

        [Header("Sweep")]
        [SerializeField] private float sweepHalfAngle = 55f;
        [SerializeField] private float sweepDuration = 3f;
        [SerializeField] private float sweepFireRate = 14f;

        [Header("Fan")]
        [SerializeField] private int fanBullets = 8;
        [SerializeField] private float fanSpread = 80f;
        [SerializeField] private int fanVolleys = 3;
        [SerializeField] private float fanInterval = 0.55f;

        private TurretAim aim;
        private WaitForSeconds fanWait;

        private void Awake()
        {
            aim = GetComponent<TurretAim>();
            fanWait = new WaitForSeconds(fanInterval);
        }

        /// <summary>Swings a continuous stream from one side of the player to the other.</summary>
        public IEnumerator Sweep(Transform player)
        {
            Vector3 toPlayer = Flat(player.position - aim.Muzzle.position);
            float range = FlatDistance(player.position, aim.Muzzle.position);
            float side = Random.value < 0.5f ? -1f : 1f;
            float shotTimer = 0f;
            SetFiring(true);
            for (float t = 0f; t < sweepDuration; t += Time.deltaTime)
            {
                float angle = Mathf.Lerp(-sweepHalfAngle, sweepHalfAngle, t / sweepDuration) * side;
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * toPlayer;
                aim.AimAt(aim.Muzzle.position + dir * 20f + Vector3.down * 2f);
                shotTimer += Time.deltaTime;
                while (shotTimer >= 1f / sweepFireRate)
                {
                    shotTimer -= 1f / sweepFireRate;
                    Fire(dir, range);
                }
                yield return null;
            }
            SetFiring(false);
        }

        /// <summary>A few spread volleys aimed at the player.</summary>
        public IEnumerator Fan(Transform player)
        {
            SetFiring(true);
            for (int v = 0; v < fanVolleys; v++)
            {
                Vector3 toPlayer = Flat(player.position - aim.Muzzle.position);
                float range = FlatDistance(player.position, aim.Muzzle.position);
                aim.AimAt(player.position);
                float twist = (v % 2 == 0 ? 0f : 0.5f) * fanSpread / Mathf.Max(1, fanBullets - 1);
                for (int i = 0; i < fanBullets; i++)
                {
                    float a = -fanSpread * 0.5f + fanSpread * i / Mathf.Max(1, fanBullets - 1) + twist;
                    Fire(Quaternion.Euler(0f, a, 0f) * toPlayer, range);
                }
                yield return fanWait;
            }
            SetFiring(false);
        }

        public void Track(Vector3 point) => aim.AimAt(point);

        private void SetFiring(bool firing)
        {
            if (fireLoop == null) return;
            if (firing && !fireLoop.isPlaying) fireLoop.Play();
            else if (!firing) fireLoop.Stop();
        }

        /// <summary>Fires along a ground direction, dipping so the round reaches chest height at the given range.</summary>
        private void Fire(Vector3 flatDir, float range)
        {
            if (bulletPrefab == null) return;
            Vector3 from = aim.Muzzle.position;
            Vector3 chest = from + flatDir.normalized * Mathf.Max(4f, range);
            chest.y = ChestHeight;
            Vector3 dir = (chest - from).normalized;
            var go = PoolManager.Instance != null
                ? PoolManager.Instance.Get(bulletPrefab.gameObject, from, Quaternion.LookRotation(dir))
                : Instantiate(bulletPrefab.gameObject, from, Quaternion.LookRotation(dir));
            // The round ignores this turret's own colliders (it spawns right at the barrel).
            go.GetComponent<BossProjectile>().Launch(dir, bulletSpeed, damage, transform, ChestHeight);
        }

        private const float ChestHeight = 1.1f;

        private static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }
        private static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }
    }
}
