using System.Collections;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// S-5 rocket pod. Three rain patterns, all marked on the floor before the rockets lob in one after another:
    /// Scatter = spots spread around the player (one on them); Ring = a circle closing around the player, with a gap to
    /// escape through; March = a line of blasts walking from the far side straight through the player's spot.
    /// </summary>
    [RequireComponent(typeof(TurretAim))]
    public class BossRocketPod : MonoBehaviour
    {
        public enum Pattern { Scatter, Ring, March }

        [SerializeField] private BossArtilleryRound rocketPrefab;
        [SerializeField] private AreaMarker markerPrefab;
        [SerializeField] private int rockets = 12;
        [SerializeField] private float scatterRadius = 10f;
        [SerializeField] private float ringRadius = 5.5f;
        [SerializeField] private float marchSpacing = 2.6f;
        [SerializeField] private float damage = 15f;
        [SerializeField] private float blastRadius = 2.5f;
        [Tooltip("Seconds the marks show before the first rocket lands.")]
        [SerializeField] private float warning = 1.2f;
        [SerializeField] private float launchInterval = 0.12f;
        [SerializeField] private float flightTime = 1.1f;
        [SerializeField] private float arcHeight = 9f;

        private TurretAim aim;
        private Vector3[] targets = new Vector3[0];
        private WaitForSeconds launchWait;

        private void Awake()
        {
            aim = GetComponent<TurretAim>();
            launchWait = new WaitForSeconds(launchInterval);
        }

        public IEnumerator Salvo(Transform player) => Salvo(player, (Pattern)Random.Range(0, 3));

        public IEnumerator Salvo(Transform player, Pattern pattern)
        {
            if (targets.Length != rockets) targets = new Vector3[rockets];
            Vector3 centre = player.position; centre.y = 0f;
            aim.AimAt(centre);
            PlaceTargets(pattern, centre);

            // Each mark stays up until its own rocket lands.
            for (int i = 0; i < rockets; i++)
            {
                var marker = Spawn(markerPrefab.gameObject, targets[i]).GetComponent<AreaMarker>();
                marker.Show(targets[i], blastRadius, warning + launchInterval * i);
            }
            yield return new WaitForSeconds(Mathf.Max(0f, warning - flightTime));
            for (int i = 0; i < rockets; i++)
            {
                var rocket = Spawn(rocketPrefab.gameObject, aim.Muzzle.position).GetComponent<BossArtilleryRound>();
                rocket.Launch(aim.Muzzle.position, targets[i], flightTime, arcHeight, damage, blastRadius);
                yield return launchWait;
            }
        }

        private void PlaceTargets(Pattern pattern, Vector3 centre)
        {
            switch (pattern)
            {
                case Pattern.Ring:
                {
                    // Evenly round the player, leaving one slot empty as the way out (that rocket goes on the player).
                    float start = Random.Range(0f, 360f);
                    int gap = Random.Range(0, rockets - 1);
                    for (int i = 0; i < rockets - 1; i++)
                    {
                        float a = (start + 360f * i / (rockets - 1)) * Mathf.Deg2Rad;
                        targets[i] = i == gap ? centre : centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * ringRadius;
                    }
                    targets[rockets - 1] = centre;
                    break;
                }
                case Pattern.March:
                {
                    // From the pod's side of the room, through the player and beyond.
                    Vector3 dir = centre - aim.Muzzle.position; dir.y = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
                    Vector3 first = centre - dir * (marchSpacing * rockets * 0.5f);
                    for (int i = 0; i < rockets; i++) targets[i] = first + dir * (marchSpacing * i);
                    break;
                }
                default:
                {
                    for (int i = 0; i < rockets; i++)
                    {
                        Vector2 r = i == 0 ? Vector2.zero : Random.insideUnitCircle.normalized * Random.Range(scatterRadius * 0.3f, scatterRadius);
                        targets[i] = centre + new Vector3(r.x, 0f, r.y);
                    }
                    break;
                }
            }
        }

        private static GameObject Spawn(GameObject prefab, Vector3 at) =>
            PoolManager.Instance != null ? PoolManager.Instance.Get(prefab, at, Quaternion.identity) : Instantiate(prefab, at, Quaternion.identity);
    }
}
