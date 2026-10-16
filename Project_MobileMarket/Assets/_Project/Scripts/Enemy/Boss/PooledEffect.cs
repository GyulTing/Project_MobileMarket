using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// One-shot effect (explosion and the like): replays its particles whenever it is taken from the pool, flashes its
    /// light and fades it out, then returns itself to the pool after its lifetime.
    /// </summary>
    public class PooledEffect : MonoBehaviour
    {
        [SerializeField] private float lifetime = 2.5f;
        [SerializeField] private Light flash;
        [SerializeField] private float flashIntensity = 40f;
        [SerializeField] private float flashFade = 0.35f;

        private ParticleSystem[] particles;
        private float startTime;

        private void Awake() => particles = GetComponentsInChildren<ParticleSystem>();

        private void OnEnable()
        {
            startTime = Time.time;
            foreach (var p in particles) { p.Clear(); p.Play(); }
            if (flash != null) flash.intensity = flashIntensity;
        }

        private void Update()
        {
            float age = Time.time - startTime;
            if (flash != null) flash.intensity = flashIntensity * Mathf.Clamp01(1f - age / flashFade);
            if (age < lifetime) return;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
