using System;
using UnityEngine;

namespace MobileMarket.Common
{
    /// <summary>
    /// Shake requests from anything in the world (explosions and the like). The camera listens and shakes by the strength,
    /// weaker the farther the source is from what it follows.
    /// </summary>
    public static class ScreenShake
    {
        /// <summary>World position of the source and its strength (meters of camera shake right next to it).</summary>
        public static event Action<Vector3, float> OnRequested;

        public static void Request(Vector3 source, float strength)
        {
            if (strength > 0f) OnRequested?.Invoke(source, strength);
        }
    }
}
