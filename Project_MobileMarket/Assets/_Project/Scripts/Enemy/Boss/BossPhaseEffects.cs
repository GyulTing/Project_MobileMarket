using System.Collections;
using UnityEngine;
using MobileMarket.Environment;
using MobileMarket.Managers;
using MobileMarket.VFX;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Room effects that mark the boss's progress. Phase 2: the vents start smoking, fires break out around the arena (each
    /// with a blast first) and the floodlights start failing. Phase 3: the floodlights go out, the orange warning beacon in
    /// the middle starts turning, the vents blow in a chain and more fires break out (floor, boss monitor, side walls).
    /// Defeat: the vents and the weapons catch fire.
    /// </summary>
    public class BossPhaseEffects : MonoBehaviour
    {
        [SerializeField] private BossController boss;
        [Tooltip("Floodlights: half of them fail in phase 2 (the rest glitch), all go out in phase 3.")]
        [SerializeField] private ArenaLights arenaLights;

        [Header("Phase 2")]
        [Tooltip("Smoke rising from the vents from phase 2 on.")]
        [SerializeField] private ParticleSystem[] ventSmoke = System.Array.Empty<ParticleSystem>();
        [Tooltip("Fires around the arena, lit one after another, each with a blast first.")]
        [SerializeField] private FireSpot[] phase2Fires = System.Array.Empty<FireSpot>();
        [SerializeField] private float phase2Interval = 0.35f;

        [Header("Phase 3")]
        [SerializeField] private RotatingBeacon beacon;
        [Tooltip("Blast prefab (pooled) for the vents' chain of explosions.")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("In front of each vent: they blow one after another when phase 3 starts.")]
        [SerializeField] private Transform[] ventBlastPoints = System.Array.Empty<Transform>();
        [SerializeField] private float ventBlastInterval = 0.15f;
        [Tooltip("More fires after the vent blasts (floor, boss monitor, side walls), each with a blast first.")]
        [SerializeField] private FireSpot[] phase3Fires = System.Array.Empty<FireSpot>();
        [SerializeField] private float phase3Interval = 0.3f;

        [Header("Defeat")]
        [Tooltip("Fire and smoke on the vents and weapons once the boss is destroyed.")]
        [SerializeField] private FireSpot[] defeatFires = System.Array.Empty<FireSpot>();
        [SerializeField] private float defeatInterval = 0.12f;

        private WaitForSeconds phase2Wait, ventBlastWait, phase3Wait, defeatWait;

        private void Awake()
        {
            phase2Wait = new WaitForSeconds(phase2Interval);
            ventBlastWait = new WaitForSeconds(ventBlastInterval);
            phase3Wait = new WaitForSeconds(phase3Interval);
            defeatWait = new WaitForSeconds(defeatInterval);
            foreach (var smoke in ventSmoke) if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnEnable()
        {
            if (boss == null) return;
            boss.OnPhaseChanged += HandlePhaseChanged;
            boss.OnDefeated += HandleDefeated;
        }

        private void OnDisable()
        {
            if (boss == null) return;
            boss.OnPhaseChanged -= HandlePhaseChanged;
            boss.OnDefeated -= HandleDefeated;
        }

        private void HandlePhaseChanged(int phase)
        {
            if (phase == 2) StartPhase2();
            else if (phase == 3) StartPhase3();
        }

        private void StartPhase3()
        {
            if (beacon != null) beacon.TurnOn();
            if (arenaLights != null) StartCoroutine(arenaLights.Blackout());
            StartCoroutine(Phase3Blasts());
        }

        /// <summary>The vents blow one after another, then new fires break out around the room.</summary>
        private IEnumerator Phase3Blasts()
        {
            foreach (var point in ventBlastPoints)
            {
                if (point == null || explosionPrefab == null) continue;
                if (PoolManager.Instance != null) PoolManager.Instance.Get(explosionPrefab, point.position, Quaternion.identity);
                else Instantiate(explosionPrefab, point.position, Quaternion.identity);
                yield return ventBlastWait;
            }
            yield return IgniteInTurn(phase3Fires, true, phase3Wait);
        }

        private void HandleDefeated() => StartCoroutine(IgniteInTurn(defeatFires, false, defeatWait));

        private void StartPhase2()
        {
            foreach (var smoke in ventSmoke)
            {
                if (smoke == null) continue;
                smoke.Play(true);
                // The smoke prefab carries its own hiss loop.
                var hiss = smoke.GetComponentInParent<AudioSource>();
                if (hiss != null) hiss.Play();
            }
            StartCoroutine(IgniteInTurn(phase2Fires, true, phase2Wait));
            if (arenaLights != null) StartCoroutine(arenaLights.StartFailing());
        }

        private static IEnumerator IgniteInTurn(FireSpot[] fires, bool withBlast, WaitForSeconds wait)
        {
            foreach (var fire in fires)
            {
                if (fire == null) continue;
                fire.Ignite(withBlast);
                yield return wait;
            }
        }

        [ContextMenu("Test Phase 2")] private void TestPhase2() => HandlePhaseChanged(2);
        [ContextMenu("Test Phase 3")] private void TestPhase3() => HandlePhaseChanged(3);
        [ContextMenu("Test Defeat")] private void TestDefeat() => HandleDefeated();
    }
}
