using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Keeps a stretched box's texture at a fixed real-world size: the Base Map tiling follows the object's scale (X across,
    /// Z deep), so a concrete slab stays 4 m whatever size the box is reshaped to. Set per renderer (property block), so
    /// boxes of different sizes can share one material. Recomputed only when the transform changes.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Renderer))]
    public class WorldSizeTiling : MonoBehaviour
    {
        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

        [Tooltip("World size, in metres, covered by one repeat of the texture.")]
        [SerializeField] private float metresPerTile = 8f;

        private Renderer cachedRenderer;
        private MaterialPropertyBlock block;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void Update()
        {
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            Apply();
        }

        private void Apply()
        {
            if (cachedRenderer == null) cachedRenderer = GetComponent<Renderer>();
            if (block == null) block = new MaterialPropertyBlock();
            var size = transform.lossyScale;
            float tile = Mathf.Max(0.01f, metresPerTile);
            cachedRenderer.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(Mathf.Abs(size.x) / tile, Mathf.Abs(size.z) / tile, 0f, 0f));
            cachedRenderer.SetPropertyBlock(block);
        }
    }
}
