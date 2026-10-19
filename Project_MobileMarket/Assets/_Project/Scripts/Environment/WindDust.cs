using UnityEngine;
using MobileMarket.Enemy;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Wind-blown sand around the player: keeps the dust emitters centred on the player (shifted upwind so the sand
    /// drifts in across the view), blows every particle the same fixed direction, and lets gusts swell and fade.
    /// The particle systems simulate in world space, so the sand keeps flowing past while the player moves.
    /// </summary>
    public class WindDust : MonoBehaviour
    {
        [Tooltip("Direction the wind blows toward (degrees around Y; 0 = +Z / north).")]
        [SerializeField] private float windYaw = 60f;
        [Tooltip("How far upwind of the player the emitters sit.")]
        [SerializeField] private float upwindOffset = 12f;
        [Tooltip("Dust systems (Play On Awake off, Prewarm on); each keeps the speed set on Z of its own Velocity over Lifetime, turned to the wind.")]
        [SerializeField] private ParticleSystem[] systems = System.Array.Empty<ParticleSystem>();

        [Header("Gusts")]
        [Tooltip("Seconds for one rise and fall of the wind.")]
        [SerializeField] private float gustPeriod = 9f;
        [Range(0f, 1f)] [SerializeField] private float gustStrength = 0.45f;

        private Transform target;
        private Vector3 windDir;
        private float[] baseRate;
        private bool started;

        private void Awake()
        {
            windDir = Quaternion.Euler(0f, windYaw, 0f) * Vector3.forward;
            baseRate = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                var vel = systems[i].velocityOverLifetime;
                // The prefab stores the speed on Z; point it down the wind.
                float speed = vel.z.constantMax;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(windDir.x * vel.z.constantMin, windDir.x * speed);
                vel.z = new ParticleSystem.MinMaxCurve(windDir.z * vel.z.constantMin, windDir.z * speed);
                baseRate[i] = systems[i].emission.rateOverTimeMultiplier;
            }
        }

        private void LateUpdate()
        {
            if (target == null) target = FindPlayer();
            if (target != null) transform.position = target.position - windDir * upwindOffset;
            if (!started && target != null)
            {
                // Start (pre-warmed) only once we stand on the player, so the sand is already all around them.
                started = true;
                for (int i = 0; i < systems.Length; i++) systems[i].Play();
            }

            // 0..1 gust swell: smooth noise so it never pulses on a fixed beat.
            float gust = Mathf.PerlinNoise(Time.time / gustPeriod, 0.37f);
            float k = 1f + gustStrength * (gust * 2f - 1f);
            for (int i = 0; i < systems.Length; i++)
            {
                var em = systems[i].emission;
                em.rateOverTimeMultiplier = baseRate[i] * k;
                var vel = systems[i].velocityOverLifetime;
                vel.speedModifierMultiplier = k;
            }
        }

        private static Transform FindPlayer()
        {
            var members = FactionMember.Active;
            for (int i = 0; i < members.Count; i++)
                if (members[i].Faction == Faction.Player) return members[i].transform;
            return null;
        }
    }
}
