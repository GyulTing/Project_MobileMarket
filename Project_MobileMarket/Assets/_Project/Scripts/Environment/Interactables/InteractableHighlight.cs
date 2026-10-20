using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Slow, soft emission pulse on an object the player needs for progress (generator, switchboard, fuse...), so it reads
    /// in the dark bunker. Stops for good once the object is resolved. Materials need emission enabled (black base).
    /// </summary>
    public class InteractableHighlight : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Renderers that glow. Empty = every renderer under this object.")]
        [SerializeField] private Renderer[] renderers;
        [SerializeField] private Color color = new Color(1f, 0.72f, 0.3f);
        [SerializeField] private float minIntensity = 0.15f;
        [SerializeField] private float maxIntensity = 1.6f;
        [Tooltip("Pulses per second.")]
        [SerializeField] private float speed = 0.6f;

        private IResolvable resolvable;
        private MaterialPropertyBlock block;

        private void Awake()
        {
            resolvable = GetComponent<IResolvable>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (resolvable != null && resolvable.IsResolved)
            {
                SetEmission(Color.black);
                enabled = false;
                return;
            }
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * speed * 2f * Mathf.PI);
            SetEmission(color * Mathf.Lerp(minIntensity, maxIntensity, k));
        }

        private void OnDisable() => SetEmission(Color.black);

        private void SetEmission(Color c)
        {
            if (block == null) return;
            block.SetColor(EmissionColorId, c);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
