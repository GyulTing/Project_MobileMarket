using System.Collections;
using UnityEngine;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// The security AI's death: its lights go haywire, a chain of blasts runs across the unit, then one big explosion
    /// swaps it for the wrecked model, its glow dies, the wreck keeps burning and sparking, and the turrets sag lifeless.
    /// Run Play() as a coroutine.
    /// </summary>
    public class BossDestruction : MonoBehaviour
    {
        [Header("Models")]
        [SerializeField] private GameObject intactModel;
        [SerializeField] private GameObject wreckedModel;

        [Header("Lights that die with it (monitor glow, warning lights)")]
        [SerializeField] private Light[] bossLights = System.Array.Empty<Light>();
        [SerializeField] private float alarmTime = 1.6f;

        [Header("Explosions")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Chain of blasts across the unit, in order.")]
        [SerializeField] private Transform[] chainPoints = System.Array.Empty<Transform>();
        [SerializeField] private float chainInterval = 0.22f;
        [Tooltip("Blasts all at once for the final explosion.")]
        [SerializeField] private Transform[] finalPoints = System.Array.Empty<Transform>();

        [Header("After")]
        [Tooltip("Fire, smoke and sparks left burning on the wreck.")]
        [SerializeField] private ParticleSystem[] burning = System.Array.Empty<ParticleSystem>();
        [Tooltip("Firelight on the wreck; flickers once it is burning.")]
        [SerializeField] private Light[] fireLights = System.Array.Empty<Light>();
        [SerializeField] private float fireLightIntensity = 6f;
        [SerializeField] private TurretAim[] turrets = System.Array.Empty<TurretAim>();

        private bool wrecked;

        private void Awake()
        {
            if (wreckedModel != null) wreckedModel.SetActive(false);
            foreach (var p in burning) if (p != null) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var l in fireLights) if (l != null) l.enabled = false;
        }

        private void Update()
        {
            if (!wrecked) return;
            for (int i = 0; i < fireLights.Length; i++)
            {
                if (fireLights[i] == null) continue;
                fireLights[i].intensity = fireLightIntensity * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 4f, i * 7.3f));
            }
        }

        public IEnumerator Play()
        {
            // 1. Alarm: the unit's lights stutter and flare at random.
            var flickerWait = new WaitForSeconds(0.07f);
            for (float t = 0f; t < alarmTime; t += 0.07f)
            {
                foreach (var l in bossLights) if (l != null) l.enabled = Random.value > 0.4f;
                yield return flickerWait;
            }
            foreach (var l in bossLights) if (l != null) l.enabled = true;

            // 2. Blasts run across it.
            var chainWait = new WaitForSeconds(chainInterval);
            foreach (var p in chainPoints)
            {
                Blast(p);
                yield return chainWait;
            }

            // 3. Final explosion: swap to the wreck, glow out, fires start, turrets die.
            foreach (var p in finalPoints) Blast(p);
            if (intactModel != null) intactModel.SetActive(false);
            if (wreckedModel != null) wreckedModel.SetActive(true);
            foreach (var l in bossLights) if (l != null) l.enabled = false;
            foreach (var p in burning) if (p != null) p.Play();
            foreach (var l in fireLights) if (l != null) l.enabled = true;
            wrecked = true;
            foreach (var t in turrets) if (t != null) t.PowerDown();
            yield return new WaitForSeconds(1f);
        }

        private void Blast(Transform at)
        {
            if (explosionPrefab == null || at == null) return;
            if (PoolManager.Instance != null) PoolManager.Instance.Get(explosionPrefab, at.position, Quaternion.identity);
            else Instantiate(explosionPrefab, at.position, Quaternion.identity);
        }
    }
}
