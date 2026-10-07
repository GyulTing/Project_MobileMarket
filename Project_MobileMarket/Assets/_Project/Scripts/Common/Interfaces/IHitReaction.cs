using UnityEngine;

namespace MobileMarket.Common
{
    /// <summary>Visual reaction to being hit (blood, sparks...). Called by whatever landed the hit, alongside TakeDamage.</summary>
    public interface IHitReaction
    {
        /// <param name="point">Where the hit landed (world space).</param>
        /// <param name="direction">Direction the hit was travelling.</param>
        void OnHit(Vector3 point, Vector3 direction);
    }
}
