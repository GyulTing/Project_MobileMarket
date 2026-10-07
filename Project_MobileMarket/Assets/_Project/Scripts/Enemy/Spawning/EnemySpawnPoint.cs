using System;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// A spot on the map that fills with one random enemy group: between minCount and maxCount enemies, each picked at random
    /// from candidates. By default the group spawns as soon as the map starts (so enemies can roam and fight on their own);
    /// with spawnOnStart off it waits until the player first comes within activationRadius. Never refilled while the map
    /// stays loaded — entering the map again starts fresh.
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        [SerializeField] private GameObject[] candidates;
        [SerializeField, Min(0)] private int minCount = 2;
        [SerializeField, Min(0)] private int maxCount = 4;
        [Tooltip("Enemies are scattered within this radius around the point.")]
        [SerializeField] private float spreadRadius = 2.5f;
        [SerializeField] private bool spawnOnStart = true;
        [Tooltip("Only used when Spawn On Start is off.")]
        [SerializeField] private float activationRadius = 25f;

        public float ActivationRadius => activationRadius;
        public bool HasSpawned { get; private set; }
        public int AliveCount { get; private set; }

        /// <summary>Raised when every enemy of this group is dead.</summary>
        public event Action<EnemySpawnPoint> OnCleared;

        private void OnEnable()
        {
            if (!spawnOnStart && EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.Register(this);
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                Spawn();
                return;
            }
            // The manager may wake up after this point; register again once everything exists.
            if (EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (EnemySpawnManager.Instance != null) EnemySpawnManager.Instance.Unregister(this);
        }

        public void Spawn()
        {
            if (HasSpawned || candidates == null || candidates.Length == 0) return;
            HasSpawned = true;

            int count = UnityEngine.Random.Range(minCount, Mathf.Max(minCount, maxCount) + 1);
            for (int i = 0; i < count; i++)
            {
                var prefab = candidates[UnityEngine.Random.Range(0, candidates.Length)];
                if (prefab != null) SpawnOne(prefab);
            }
        }

        private void SpawnOne(GameObject prefab)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
            Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            var enemy = PoolManager.Instance != null
                ? PoolManager.Instance.Get(prefab, position, rotation)
                : Instantiate(prefab, position, rotation);

            var health = enemy.GetComponent<EnemyHealth>();
            if (health == null) return;
            AliveCount++;
            health.OnDied += HandleEnemyDied;
        }

        private void HandleEnemyDied(EnemyHealth health)
        {
            health.OnDied -= HandleEnemyDied;
            AliveCount--;
            if (AliveCount == 0) OnCleared?.Invoke(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = HasSpawned ? Color.gray : Color.red;
            Gizmos.DrawWireSphere(transform.position, spreadRadius);
            if (spawnOnStart) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, activationRadius);
        }
    }
}
