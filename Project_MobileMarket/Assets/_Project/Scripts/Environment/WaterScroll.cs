using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Slowly slides the water's ripple normal map so the surface reads as flowing. Works on this renderer's own material
    /// instance (made once in Awake), so the shared asset is never touched.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterScroll : MonoBehaviour
    {
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

        [Tooltip("Ripple drift in UV units per second.")]
        [SerializeField] private Vector2 speed = new Vector2(0.02f, 0.035f);

        private Material material;
        private Vector2 offset;

        private void Awake() => material = GetComponent<Renderer>().material;

        private void Update()
        {
            offset += speed * Time.deltaTime;
            offset.x %= 1f;
            offset.y %= 1f;
            material.SetTextureOffset(BumpMap, offset);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
