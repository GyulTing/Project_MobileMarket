using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// The scene's minimap: a top-down picture of the map and the world XZ area it covers.
    /// The picture is baked with MobileMarket > UI > Bake Minimap (north = +Z is the top of the image).
    /// </summary>
    public class MinimapArea : MonoBehaviour
    {
        [SerializeField] private Sprite mapImage;
        [Tooltip("World X/Z of the image's bottom-left corner.")]
        [SerializeField] private Vector2 worldMin = new Vector2(-25f, -25f);
        [Tooltip("World width (X) and depth (Z) the image covers.")]
        [SerializeField] private Vector2 worldSize = new Vector2(50f, 50f);

        public Sprite MapImage => mapImage;
        public Vector2 WorldMin => worldMin;
        public Vector2 WorldSize => worldSize;

        /// <summary>Position of a world point inside the image, 0..1 on each axis.</summary>
        public Vector2 WorldToNormalized(Vector3 world)
        {
            return new Vector2(
                worldSize.x > 0f ? (world.x - worldMin.x) / worldSize.x : 0f,
                worldSize.y > 0f ? (world.z - worldMin.y) / worldSize.y : 0f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            var center = new Vector3(worldMin.x + worldSize.x * 0.5f, transform.position.y, worldMin.y + worldSize.y * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(worldSize.x, 0f, worldSize.y));
        }
    }
}
