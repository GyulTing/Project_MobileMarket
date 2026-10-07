using UnityEngine;

namespace MobileMarket.Managers
{
    /// <summary>Added by PoolManager to every instance it creates, so Release() knows which pool it belongs to.</summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObject Source { get; set; }
    }
}
