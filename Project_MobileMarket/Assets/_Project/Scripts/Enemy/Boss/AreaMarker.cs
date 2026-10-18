using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Red ring on the floor showing where a shell or rocket will land. Pulses faster and brighter as impact nears, then
    /// returns to the pool. Pooled.
    /// </summary>
    public class AreaMarker : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Renderer ringRenderer;
        [ColorUsage(true, true)] [SerializeField] private Color color = new Color(3f, 0.25f, 0.15f, 1f);
        [Tooltip("Warning when the ring appears; cut off when the ring's time runs out (capped by its Max Duration).")]
        [SerializeField] private SoundEffect warningSound = new SoundEffect();

        private MaterialPropertyBlock block;
        private float startTime, duration;

        private void Awake() => block = new MaterialPropertyBlock();

        /// <summary>Shows the ring at a ground point with the given blast radius for the given seconds.</summary>
        public void Show(Vector3 groundPoint, float radius, float seconds)
        {
            transform.position = groundPoint + Vector3.up * 0.04f;
            transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            startTime = Time.time;
            duration = Mathf.Max(0.05f, seconds);
            float soundLength = warningSound.maxDuration > 0f ? Mathf.Min(duration, warningSound.maxDuration) : duration;
            SoundManager.PlayAt(warningSound, groundPoint, soundLength);
        }

        private void Update()
        {
            float t = (Time.time - startTime) / duration;
            if (t >= 1f)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
                else Destroy(gameObject);
                return;
            }
            // Blinks faster toward impact.
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 30f, t));
            ringRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color * Mathf.Lerp(0.6f, 1.4f, t) * pulse);
            ringRenderer.SetPropertyBlock(block);
        }
    }
}
