using System.Collections;
using UnityEngine;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Boss-room floodlights: off (dark room) until TurnOn() runs, then they switch on one by one from the entrance
    /// toward the boss, each with a short over-bright pop (and a click, if a clip is set); a few stutter before catching.
    /// The ambient light is raised along with them. Run TurnOn() as a coroutine to wait for the whole sequence.
    /// </summary>
    public class ArenaLights : MonoBehaviour
    {
        [Tooltip("Lights in switch-on order (entrance first). Each comes on at the intensity set on the light itself.")]
        [SerializeField] private Light[] lights = System.Array.Empty<Light>();
        [Tooltip("Switched on all together, with one bang, after the sequence (the boss wall).")]
        [SerializeField] private Light[] finaleLights = System.Array.Empty<Light>();
        [SerializeField] private float finalePause = 0.4f;
        [Tooltip("Scales every light's own intensity (one knob for overall brightness).")]
        [SerializeField] private float brightness = 1f;
        [SerializeField] private float interval = 0.35f;
        [SerializeField] private float popMultiplier = 1.8f;
        [SerializeField] private float popTime = 0.08f;
        [Tooltip("Chance that a light flickers a few times before it stays on.")]
        [Range(0f, 1f)] [SerializeField] private float flickerChance = 0.25f;
        [ColorUsage(false, false)] [SerializeField] private Color litAmbient = new Color(0.11f, 0.12f, 0.14f);
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickClip;

        public bool IsOn { get; private set; }

        private Color darkAmbient;
        private float[] onIntensity, finaleIntensity;
        private WaitForSeconds popWait, gapWait, flickerWait;

        private void Awake()
        {
            darkAmbient = RenderSettings.ambientLight;
            popWait = new WaitForSeconds(popTime);
            gapWait = new WaitForSeconds(interval);
            flickerWait = new WaitForSeconds(0.06f);
            onIntensity = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                onIntensity[i] = lights[i].intensity * brightness;
                lights[i].enabled = false;
            }
            finaleIntensity = new float[finaleLights.Length];
            for (int i = 0; i < finaleLights.Length; i++)
            {
                if (finaleLights[i] == null) continue;
                finaleIntensity[i] = finaleLights[i].intensity * brightness;
                finaleLights[i].enabled = false;
            }
        }

        public IEnumerator TurnOn()
        {
            if (IsOn) yield break;
            IsOn = true;
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                if (Random.value < flickerChance)
                {
                    for (int f = 0; f < 3; f++)
                    {
                        l.enabled = true; l.intensity = onIntensity[i] * 0.4f; yield return flickerWait;
                        l.enabled = false; yield return flickerWait;
                    }
                }
                l.enabled = true;
                l.intensity = onIntensity[i] * popMultiplier;
                if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
                RenderSettings.ambientLight = Color.Lerp(darkAmbient, litAmbient, (i + 1f) / lights.Length);
                yield return popWait;
                l.intensity = onIntensity[i];
                yield return gapWait;
            }

            if (finaleLights.Length == 0) yield break;
            yield return new WaitForSeconds(finalePause);
            for (int i = 0; i < finaleLights.Length; i++)
            {
                if (finaleLights[i] == null) continue;
                finaleLights[i].enabled = true;
                finaleLights[i].intensity = finaleIntensity[i] * popMultiplier;
            }
            if (audioSource != null && clickClip != null) audioSource.PlayOneShot(clickClip);
            yield return popWait;
            for (int i = 0; i < finaleLights.Length; i++) if (finaleLights[i] != null) finaleLights[i].intensity = finaleIntensity[i];
        }
    }
}
