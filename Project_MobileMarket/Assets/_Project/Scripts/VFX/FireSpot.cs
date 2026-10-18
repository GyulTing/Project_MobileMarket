using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.VFX
{
    /// <summary>
    /// A fire that stays out until Ignite(): optionally a blast goes off first, then its particles (flames, smoke, embers)
    /// start and its light fades in and flickers. Visual only, it doesn't hurt anyone.
    /// </summary>
    public class FireSpot : MonoBehaviour
    {
        [Tooltip("Blast for Ignite(withBlast: true) (pooled).")]
        [SerializeField] private GameObject igniteExplosionPrefab;
        [Tooltip("Seconds between the blast and the flames.")]
        [SerializeField] private float igniteDelay = 0.25f;
        [SerializeField] private Light fireLight;
        [SerializeField] private float lightIntensity = 4f;
        [Tooltip("Seconds for the firelight to reach full strength.")]
        [SerializeField] private float lightFadeIn = 0.6f;
        [SerializeField] private float flickerSpeed = 6f;

        [Header("Sounds")]
        [Tooltip("Whoosh as the flames catch.")]
        [SerializeField] private SoundEffect igniteSound = new SoundEffect();
        [Tooltip("Looping crackle while it burns; fades in with the firelight.")]
        [SerializeField] private AudioSource burnLoop;
        [SerializeField] private float burnVolume = 0.3f;

        public bool IsBurning { get; private set; }

        private ParticleSystem[] particles;
        private bool pending;
        private float startTime;
        private float seed;

        private void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (var p in particles) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (fireLight != null) fireLight.enabled = false;
            seed = Random.value * 100f;
        }

        public void Ignite(bool withBlast)
        {
            if (IsBurning || pending) return;
            bool blast = withBlast && igniteExplosionPrefab != null;
            if (blast)
            {
                if (PoolManager.Instance != null) PoolManager.Instance.Get(igniteExplosionPrefab, transform.position, Quaternion.identity);
                else Instantiate(igniteExplosionPrefab, transform.position, Quaternion.identity);
            }
            startTime = Time.time + (blast ? igniteDelay : 0f);
            pending = true;
        }

        private void Update()
        {
            if (pending && Time.time >= startTime)
            {
                pending = false;
                IsBurning = true;
                foreach (var p in particles) p.Play(true);
                if (fireLight != null) fireLight.enabled = true;
                SoundManager.PlayAt(igniteSound, transform.position);
                if (burnLoop != null)
                {
                    // Start somewhere random in the loop so neighbouring fires don't crackle in sync.
                    burnLoop.volume = 0f;
                    if (burnLoop.clip != null) burnLoop.time = Random.Range(0f, burnLoop.clip.length);
                    burnLoop.Play();
                }
            }
            if (!IsBurning) return;

            float fadeIn = lightFadeIn > 0f ? Mathf.Clamp01((Time.time - startTime) / lightFadeIn) : 1f;
            if (burnLoop != null) burnLoop.volume = burnVolume * fadeIn;
            if (fireLight == null) return;
            float flicker = 0.65f + 0.35f * Mathf.PerlinNoise(Time.time * flickerSpeed, seed);
            fireLight.intensity = lightIntensity * fadeIn * flicker;
        }
    }
}
