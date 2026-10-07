using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.Managers
{
    /// <summary>
    /// Reuses instances of a prefab instead of Instantiate/Destroy. Get() hands out an inactive pooled copy (or a new one),
    /// Release() deactivates it and puts it back. Released objects are parked under this manager.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = null;
            if (pools.TryGetValue(prefab, out var pool))
            {
                // Skip anything destroyed while parked (e.g. by a scene change).
                while (pool.Count > 0 && instance == null) instance = pool.Pop();
            }

            if (instance == null)
            {
                instance = Instantiate(prefab, position, rotation);
                instance.AddComponent<PooledObject>().Source = prefab;
            }
            else
            {
                instance.transform.SetParent(null);
                instance.transform.SetPositionAndRotation(position, rotation);
            }

            instance.SetActive(true);
            return instance;
        }

        /// <summary>Returns a pooled object; anything that didn't come from Get() is simply destroyed.</summary>
        public void Release(GameObject instance)
        {
            var pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || pooled.Source == null)
            {
                Destroy(instance);
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);
            if (!pools.TryGetValue(pooled.Source, out var pool))
            {
                pool = new Stack<GameObject>();
                pools.Add(pooled.Source, pool);
            }
            pool.Push(instance);
        }
    }
}
