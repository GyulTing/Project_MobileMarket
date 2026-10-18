using System.Collections;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Zombie waves during the boss fight: every waveInterval seconds, pointsPerWave random spawn points each release
    /// minPerPoint..maxPerPoint zombies, who head for the spot the player was standing on at that moment (and attack the
    /// player on sight along the way). Runs from the fight's start until the boss is defeated; skips a wave while
    /// maxAlive zombies from it are still up.
    /// </summary>
    public class BossZombieSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct ZombieType
        {
            public GameObject prefab;
            [Tooltip("Max health in this fight only (0 = the prefab's own).")]
            public float health;
        }

        [SerializeField] private BossController boss;
        [SerializeField] private Transform[] spawnPoints = System.Array.Empty<Transform>();
        [Tooltip("Picked at random for every zombie.")]
        [SerializeField] private ZombieType[] zombies = System.Array.Empty<ZombieType>();

        [SerializeField] private float waveInterval = 15f;
        [Tooltip("Seconds after the fight starts before the first wave.")]
        [SerializeField] private float firstWaveDelay = 15f;
        [SerializeField, Min(1)] private int pointsPerWave = 2;
        [SerializeField, Min(1)] private int minPerPoint = 2;
        [SerializeField, Min(1)] private int maxPerPoint = 3;
        [Tooltip("Zombies are scattered within this radius around a spawn point.")]
        [SerializeField] private float spreadRadius = 1.5f;
        [SerializeField] private int maxAlive = 20;

        public int AliveCount { get; private set; }

        private Coroutine waves;
        private int[] pointOrder;

        private void Awake()
        {
            pointOrder = new int[spawnPoints.Length];
        }

        private void OnEnable()
        {
            if (boss == null) return;
            boss.OnFightStarted += HandleFightStarted;
            boss.OnDefeated += HandleDefeated;
        }

        private void OnDisable()
        {
            if (boss == null) return;
            boss.OnFightStarted -= HandleFightStarted;
            boss.OnDefeated -= HandleDefeated;
        }

        private void HandleFightStarted()
        {
            if (waves == null) waves = StartCoroutine(Waves());
        }

        private void HandleDefeated()
        {
            if (waves != null) StopCoroutine(waves);
            waves = null;
        }

        private IEnumerator Waves()
        {
            yield return new WaitForSeconds(firstWaveDelay);
            var wait = new WaitForSeconds(waveInterval);
            while (true)
            {
                SpawnWave();
                yield return wait;
            }
        }

        [ContextMenu("Spawn Wave (test)")]
        public void SpawnWave()
        {
            if (spawnPoints.Length == 0 || zombies.Length == 0 || AliveCount >= maxAlive) return;

            Vector3 rally = PlayerPosition(out bool hasPlayer);
            ShufflePoints();
            int points = Mathf.Min(pointsPerWave, spawnPoints.Length);
            for (int p = 0; p < points; p++)
            {
                var point = spawnPoints[pointOrder[p]];
                if (point == null) continue;
                int count = Random.Range(minPerPoint, Mathf.Max(minPerPoint, maxPerPoint) + 1);
                for (int i = 0; i < count && AliveCount < maxAlive; i++) SpawnOne(point.position, rally, hasPlayer);
            }
        }

        private void SpawnOne(Vector3 at, Vector3 rally, bool hasRally)
        {
            var type = zombies[Random.Range(0, zombies.Length)];
            var prefab = type.prefab;
            if (prefab == null) return;

            Vector2 offset = Random.insideUnitCircle * spreadRadius;
            Vector3 position = at + new Vector3(offset.x, 0f, offset.y);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var zombie = PoolManager.Instance != null
                ? PoolManager.Instance.Get(prefab, position, rotation)
                : Instantiate(prefab, position, rotation);

            if (hasRally && zombie.TryGetComponent(out EnemyBrain brain)) brain.SetRallyPoint(rally);
            if (!zombie.TryGetComponent(out EnemyHealth health)) return;
            if (type.health > 0f) health.SetMaxHealthForThisLife(type.health);
            AliveCount++;
            health.OnDied += HandleZombieDied;
        }

        private void HandleZombieDied(EnemyHealth health)
        {
            health.OnDied -= HandleZombieDied;
            AliveCount--;
        }

        /// <summary>Where the player stands right now (the zombies' gathering point).</summary>
        private static Vector3 PlayerPosition(out bool found)
        {
            var members = FactionMember.Active;
            for (int i = 0; i < members.Count; i++)
            {
                if (members[i].Faction != Faction.Player || !members[i].IsAlive) continue;
                found = true;
                return members[i].transform.position;
            }
            found = false;
            return Vector3.zero;
        }

        private void ShufflePoints()
        {
            for (int i = 0; i < pointOrder.Length; i++) pointOrder[i] = i;
            for (int i = pointOrder.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pointOrder[i], pointOrder[j]) = (pointOrder[j], pointOrder[i]);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.3f);
            foreach (var point in spawnPoints) if (point != null) Gizmos.DrawWireSphere(point.position, spreadRadius);
        }
    }
}
