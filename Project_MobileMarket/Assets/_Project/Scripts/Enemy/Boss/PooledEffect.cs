using UnityEngine;
using MobileMarket.Common;
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
        [Tooltip("Camera shake (meters) when it goes off right next to the player; 0 for none.")]
        [SerializeField] private float shakeStrength;

        [Header("Sounds")]
        [Tooltip("Played the moment it goes off (the bang).")]
        [SerializeField] private SoundEffect sound = new SoundEffect();
        [Tooltip("Played a moment later, on some of them (falling debris).")]
        [SerializeField] private SoundEffect followUpSound = new SoundEffect();
        [SerializeField] private float followUpDelay = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float followUpChance = 0.5f;

        private ParticleSystem[] particles;
        private float startTime;
        private bool followUpPending;

        private void Awake() => particles = GetComponentsInChildren<ParticleSystem>();

        private void OnEnable()
        {
            startTime = Time.time;
            foreach (var p in particles) { p.Clear(); p.Play(); }
            if (flash != null) flash.intensity = flashIntensity;
            ScreenShake.Request(transform.position, shakeStrength);
            SoundManager.PlayAt(sound, transform.position);
            followUpPending = followUpSound.clip != null && Random.value < followUpChance;
        }

        private void Update()
        {
            float age = Time.time - startTime;
            if (followUpPending && age >= followUpDelay)
            {
                followUpPending = false;
                SoundManager.PlayAt(followUpSound, transform.position);
            }
            if (flash != null) flash.intensity = flashIntensity * Mathf.Clamp01(1f - age / flashFade);
            if (age < lifetime) return;
            if (PoolManager.Instance != null) PoolManager.Instance.Release(gameObject);
            else Destroy(gameObject);
        }
    }
}
