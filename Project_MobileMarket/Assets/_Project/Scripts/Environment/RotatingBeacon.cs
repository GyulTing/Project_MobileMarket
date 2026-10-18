using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Warning beacon (orange rotating light). Dark until TurnOn(); then its lens glows and the rotor spins its spotlights
    /// so their beams sweep the room.
    /// </summary>
    public class RotatingBeacon : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Turns around its local Y axis, carrying the beam lights.")]
        [SerializeField] private Transform rotor;
        [SerializeField] private float degreesPerSecond = 270f;
        [SerializeField] private Light[] beams = System.Array.Empty<Light>();
        [Tooltip("Glowing lens; its emission is switched with the beacon.")]
        [SerializeField] private Renderer lens;
        [ColorUsage(false, true)] [SerializeField] private Color lensGlow = new Color(4f, 1.2f, 0.1f);
        [Tooltip("Looping sounds that run while it is on (rotor motor, siren).")]
        [SerializeField] private AudioSource[] loops = System.Array.Empty<AudioSource>();

        public bool IsOn { get; private set; }

        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            SetOn(false);
        }

        [ContextMenu("Turn On (test)")]
        public void TurnOn() => SetOn(true);

        public void TurnOff() => SetOn(false);

        private void SetOn(bool on)
        {
            IsOn = on;
            foreach (var beam in beams) if (beam != null) beam.enabled = on;
            foreach (var loop in loops)
            {
                if (loop == null) continue;
                if (on) loop.Play();
                else loop.Stop();
            }
            if (lens == null) return;
            lens.GetPropertyBlock(block);
            block.SetColor(EmissionColorId, on ? lensGlow : Color.black);
            lens.SetPropertyBlock(block);
        }

        private void Update()
        {
            if (IsOn && rotor != null) rotor.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }
    }
}
