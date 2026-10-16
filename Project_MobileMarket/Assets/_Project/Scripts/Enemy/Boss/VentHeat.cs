using System;
using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// One of the bunker boss's heat vents. Open() lifts the hinged cover and the vent glows like forged iron (hot core,
    /// glowing metal, flickering light, sparks, smoke); only then do hits count. Losing half its health shows the next
    /// damage model; at 0 it breaks: the heat dies and it shorts out with electric sparks until Close(). Every time it has
    /// broken before, it starts the next cooling one damage model further along (intact, damaged, wrecked).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class VentHeat : MonoBehaviour, IDamageable
    {
        public enum State { Closed, Open, Broken }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private const int WreckedStage = 2;

        [SerializeField] private float maxHealth = 450f;

        [Header("Cover")]
        [Tooltip("Pivot on the cover's top edge; turns around its X axis to lift the cover.")]
        [SerializeField] private Transform coverHinge;
        [SerializeField] private float openAngle = -105f;
        [Tooltip("Open/close progress per second.")]
        [SerializeField] private float coverSpeed = 1.5f;

        [Header("Damage models (intact, damaged, wrecked)")]
        [SerializeField] private GameObject[] frameStages = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] coverStages = Array.Empty<GameObject>();

        [Header("Heat")]
        [Tooltip("Metal that glows when hot (frame and cover models).")]
        [SerializeField] private Renderer[] glowRenderers = Array.Empty<Renderer>();
        [Tooltip("Plate deep inside the vent: the white-hot core.")]
        [SerializeField] private Renderer coreRenderer;
        [ColorUsage(false, true)] [SerializeField] private Color metalHotColor = new Color(2.2f, 0.45f, 0.08f);
        [ColorUsage(false, true)] [SerializeField] private Color coreHotColor = new Color(6f, 2.6f, 0.7f);
        [SerializeField] private Light heatLight;
        [SerializeField] private float heatLightIntensity = 6f;
        [Tooltip("Heat-up/cool-down per second.")]
        [SerializeField] private float heatSpeed = 0.8f;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystem smoke;

        [Header("Short circuit (broken)")]
        [SerializeField] private ParticleSystem shortCircuit;
        [SerializeField] private Light shortLight;
        [SerializeField] private float shortLightIntensity = 5f;

        public event Action<VentHeat> OnBroken;

        public State CurrentState { get; private set; } = State.Closed;
        public int TimesBroken { get; private set; }
        public float Health { get; private set; }
        /// <summary>Broken during the current boss phase: it reopens still shorting out and can't be hurt again.</summary>
        public bool BrokenThisPhase { get; private set; }

        private MaterialPropertyBlock block;
        private float coverOpen;   // 0 closed .. 1 open
        private float heat;        // 0 cold .. 1 glowing
        private int startStage;
        private int stage = -1;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Health = maxHealth;
            ShowStage(0);
            ApplyHeat(0f);
            SetLight(heatLight, 0f);
            SetLight(shortLight, 0f);
        }

        /// <summary>Lifts the cover and heats up. Health refills; the vent starts as damaged as it has been broken before.</summary>
        public void Open()
        {
            if (CurrentState != State.Closed) return;
            if (BrokenThisPhase)
            {
                // Already wrecked this phase: the cover lifts on a dead, sparking vent.
                CurrentState = State.Broken;
                ShowStage(WreckedStage);
                if (shortCircuit != null) shortCircuit.Play();
                return;
            }
            CurrentState = State.Open;
            Health = maxHealth;
            startStage = Mathf.Min(WreckedStage, TimesBroken);
            ShowStage(startStage);
            if (sparks != null) sparks.Play();
            if (smoke != null) smoke.Play();
        }

        /// <summary>Lowers the cover and lets everything cool down and stop sparking.</summary>
        public void Close()
        {
            CurrentState = State.Closed;
            if (sparks != null) sparks.Stop();
            if (smoke != null) smoke.Stop();
            if (shortCircuit != null) shortCircuit.Stop();
        }

        /// <summary>A new boss phase: every vent can be opened and broken again (it still starts as damaged as before).</summary>
        public void ResetForNewPhase() => BrokenThisPhase = false;

        public void TakeDamage(float amount)
        {
            // Only an open vent (cover mostly up) can be hurt.
            if (CurrentState != State.Open || coverOpen < 0.5f || amount <= 0f) return;

            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f)
            {
                Break();
                return;
            }
            ShowStage(Mathf.Min(WreckedStage, startStage + (Health <= maxHealth * 0.5f ? 1 : 0)));
        }

        private void Break()
        {
            CurrentState = State.Broken;
            BrokenThisPhase = true;
            TimesBroken++;
            ShowStage(WreckedStage);
            if (sparks != null) sparks.Stop();
            if (smoke != null) smoke.Stop();
            if (shortCircuit != null) shortCircuit.Play();
            OnBroken?.Invoke(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            coverOpen = Mathf.MoveTowards(coverOpen, CurrentState == State.Closed ? 0f : 1f, coverSpeed * dt);
            if (coverHinge != null) coverHinge.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, openAngle, coverOpen), 0f, 0f);

            // Glows only while open and intact enough to run; breaking or closing lets it cool off.
            float heatTarget = CurrentState == State.Open ? 1f : 0f;
            heat = Mathf.MoveTowards(heat, heatTarget, heatSpeed * dt);
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 3f, transform.position.x);
            ApplyHeat(heat * flicker);
            SetLight(heatLight, heat * flicker * heatLightIntensity);

            // Electric short: harsh random blue flashes while broken.
            float shortLevel = CurrentState == State.Broken && UnityEngine.Random.value < 0.35f ? shortLightIntensity : 0f;
            SetLight(shortLight, shortLevel);
        }

        private void ApplyHeat(float amount)
        {
            if (block == null) return;
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                if (glowRenderers[i] == null) continue;
                glowRenderers[i].GetPropertyBlock(block);
                block.SetColor(EmissionColorId, metalHotColor * amount);
                glowRenderers[i].SetPropertyBlock(block);
            }
            if (coreRenderer != null)
            {
                coreRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, coreHotColor * amount);
                coreRenderer.SetPropertyBlock(block);
            }
        }

        private void ShowStage(int index)
        {
            if (index == stage) return;
            stage = index;
            for (int i = 0; i < frameStages.Length; i++) if (frameStages[i] != null) frameStages[i].SetActive(i == index);
            for (int i = 0; i < coverStages.Length; i++) if (coverStages[i] != null) coverStages[i].SetActive(i == index);
        }

        private static void SetLight(Light light, float intensity)
        {
            if (light == null) return;
            light.intensity = intensity;
            light.enabled = intensity > 0.01f;
        }
    }
}
