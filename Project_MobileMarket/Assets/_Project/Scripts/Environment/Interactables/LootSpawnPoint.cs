using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// A spot on the map that may hold a loot container: when the map starts, with spawnChance it places one of the candidate
    /// boxes here (facing the point's rotation), already filled with its loot. Never refilled while the map stays loaded —
    /// entering the map again starts fresh.
    /// </summary>
    public class LootSpawnPoint : MonoBehaviour
    {
        [SerializeField] private LootBox[] candidates;
        [Tooltip("Chance that anything spawns here at all.")]
        [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;

        public LootBox Spawned { get; private set; }

        private void Start()
        {
            if (candidates == null || candidates.Length == 0 || Random.value > spawnChance) return;

            var prefab = candidates[Random.Range(0, candidates.Length)];
            if (prefab != null) Spawned = Instantiate(prefab, transform.position, transform.rotation);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.4f, new Vector3(1.2f, 0.8f, 0.8f));
            Gizmos.DrawLine(transform.position + Vector3.up * 0.4f, transform.position + Vector3.up * 0.4f - transform.forward * 0.8f);
        }
    }
}
