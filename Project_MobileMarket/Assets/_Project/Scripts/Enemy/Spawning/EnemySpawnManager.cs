using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Watches the player's distance to every registered EnemySpawnPoint (a few times a second, not every frame)
    /// and tells a point to spawn its group the first time the player comes within its activation radius.
    /// </summary>
    public class EnemySpawnManager : MonoBehaviour
    {
        public static EnemySpawnManager Instance { get; private set; }

        [SerializeField] private Transform player;
        [SerializeField] private float checkInterval = 0.5f;

        private readonly List<EnemySpawnPoint> points = new List<EnemySpawnPoint>();
        private float nextCheckTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void Register(EnemySpawnPoint point)
        {
            if (!points.Contains(point)) points.Add(point);
        }

        public void Unregister(EnemySpawnPoint point)
        {
            points.Remove(point);
        }

        private void Update()
        {
            if (player == null || Time.time < nextCheckTime) return;
            nextCheckTime = Time.time + checkInterval;

            Vector3 playerPosition = player.position;
            for (int i = points.Count - 1; i >= 0; i--)
            {
                var point = points[i];
                if (point.HasSpawned) continue;

                float radius = point.ActivationRadius;
                if ((point.transform.position - playerPosition).sqrMagnitude <= radius * radius) point.Spawn();
            }
        }
    }
}
