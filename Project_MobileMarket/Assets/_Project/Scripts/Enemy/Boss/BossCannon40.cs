using System.Collections;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// 40 mm wall cannon: area strikes on the player's spot. A security camera's laser follows the player (tracking), then
    /// locks and a red ring marks the impact area; the moment the cannon fires the laser goes out, and the shell lands a
    /// moment later. A strike can be a short chain of shots leading the player's movement.
    /// </summary>
    [RequireComponent(typeof(TurretAim))]
    public class BossCannon40 : MonoBehaviour
    {
        [SerializeField] private BossArtilleryRound shellPrefab;
        [SerializeField] private AreaMarker markerPrefab;
        [SerializeField] private float damage = 25f;
        [SerializeField] private float blastRadius = 3f;
        [SerializeField] private float trackTime = 1.5f;
        [SerializeField] private float lockTime = 0.6f;
        [SerializeField] private float shellFlightTime = 0.3f;
        [Tooltip("Seconds between shots in a chain.")]
        [SerializeField] private float chainInterval = 0.45f;
        [Tooltip("How far ahead of the player each chained shot is placed, per second of the player's speed.")]
        [SerializeField] private float leadTime = 0.6f;

        private TurretAim aim;
        private WaitForSeconds chainWait, sprayWait, sprayEndWait, sprayFireWait;

        private void Awake()
        {
            aim = GetComponent<TurretAim>();
            chainWait = new WaitForSeconds(chainInterval);
            sprayWait = new WaitForSeconds(sprayInterval);
            sprayEndWait = new WaitForSeconds(sprayWarning);
            sprayFireWait = new WaitForSeconds(Mathf.Max(0f, sprayWarning - shellFlightTime));
        }

        /// <summary>Laser-guided strike: shots fired at the player, each chained shot placed further along their path.</summary>
        public IEnumerator Strike(Transform player, TargetLaser laser, int shots)
        {
            Vector3 lastPos = player.position;
            for (float t = 0f; t < trackTime; t += Time.deltaTime)
            {
                Vector3 p = Ground(player.position);
                if (laser != null) laser.Track(p);
                aim.AimAt(p);
                yield return null;
            }

            Vector3 velocity = (player.position - lastPos) / Mathf.Max(0.01f, trackTime);
            velocity.y = 0f;
            for (int i = 0; i < shots; i++)
            {
                Vector3 target = Ground(player.position + velocity * (leadTime * i));
                var marker = Spawn(markerPrefab.gameObject, target).GetComponent<AreaMarker>();
                marker.Show(target, blastRadius, lockTime + shellFlightTime);
                for (float t = 0f; t < lockTime; t += Time.deltaTime)
                {
                    if (laser != null) laser.Track(target);
                    aim.AimAt(target);
                    yield return null;
                }
                if (laser != null) laser.Hide();
                var shell = Spawn(shellPrefab.gameObject, aim.Muzzle.position).GetComponent<BossArtilleryRound>();
                shell.Launch(aim.Muzzle.position, target, shellFlightTime, 0f, damage, blastRadius);
                if (i < shots - 1) yield return chainWait;
            }
        }

        [Header("Aimed shot")]
        [SerializeField] private float aimedTrackTime = 0.8f;
        [Tooltip("The laser holds still this long before the shot, so the player can step off the line.")]
        [SerializeField] private float aimedLockTime = 0.35f;
        [SerializeField] private float aimedShellSpeed = 55f;
        [SerializeField] private float aimedBlastRadius = 2f;

        [Header("Spray")]
        [SerializeField] private int sprayShots = 8;
        [SerializeField] private float sprayRadius = 8f;
        [SerializeField] private float sprayInterval = 0.18f;
        [SerializeField] private float sprayWarning = 0.5f;
        [SerializeField] private float sprayBlastRadius = 2.2f;

        /// <summary>Direct fire: the laser follows the player, freezes, and a fast shell flies down the laser line.</summary>
        public IEnumerator AimedShot(Transform player, TargetLaser laser)
        {
            Vector3 target = Ground(player.position);
            for (float t = 0f; t < aimedTrackTime; t += Time.deltaTime)
            {
                target = Ground(player.position);
                if (laser != null) laser.Track(target);
                aim.AimAt(target);
                yield return null;
            }
            for (float t = 0f; t < aimedLockTime; t += Time.deltaTime)
            {
                if (laser != null) laser.Track(target);
                aim.AimAt(target);
                yield return null;
            }
            if (laser != null) laser.Hide();
            float flight = Vector3.Distance(aim.Muzzle.position, target) / Mathf.Max(1f, aimedShellSpeed);
            var shell = Spawn(shellPrefab.gameObject, aim.Muzzle.position).GetComponent<BossArtilleryRound>();
            shell.Launch(aim.Muzzle.position, target, flight, 0f, damage, aimedBlastRadius);
        }

        /// <summary>Suppressive spray: rapid shells scattered around the player, each spot flagged just before it lands.</summary>
        public IEnumerator Spray(Transform player)
        {
            for (int i = 0; i < sprayShots; i++)
            {
                Vector2 r = Random.insideUnitCircle * sprayRadius;
                Vector3 target = Ground(player.position) + new Vector3(r.x, 0f, r.y);
                aim.AimAt(target);
                var marker = Spawn(markerPrefab.gameObject, target).GetComponent<AreaMarker>();
                marker.Show(target, sprayBlastRadius, sprayWarning);
                StartCoroutine(SprayShell(target));
                yield return sprayWait;
            }
            yield return sprayEndWait;
        }

        /// <summary>Fires so the shell lands just as its warning ring runs out.</summary>
        private IEnumerator SprayShell(Vector3 target)
        {
            yield return sprayFireWait;
            var shell = Spawn(shellPrefab.gameObject, aim.Muzzle.position).GetComponent<BossArtilleryRound>();
            shell.Launch(aim.Muzzle.position, target, shellFlightTime, 0f, damage, sprayBlastRadius);
        }

        public void Track(Vector3 point) => aim.AimAt(point);

        private static Vector3 Ground(Vector3 p) { p.y = 0f; return p; }

        private static GameObject Spawn(GameObject prefab, Vector3 at) =>
            PoolManager.Instance != null ? PoolManager.Instance.Get(prefab, at, Quaternion.identity) : Instantiate(prefab, at, Quaternion.identity);
    }
}
