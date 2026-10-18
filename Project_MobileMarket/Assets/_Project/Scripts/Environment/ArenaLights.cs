using System.Collections;
using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Environment
{
    /// <summary>
    /// Boss-room floodlights: off (dark room) until TurnOn() runs, then they switch on one by one from the entrance
    /// toward the boss, each with a short over-bright pop (and its switch sound, if set); a few stutter before catching.
    /// The ambient light is raised along with them. Run TurnOn() as a coroutine to wait for the whole sequence.
    /// Later they break down: StartFailing() kills about half and leaves the rest glitching, Blackout() kills the rest.
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
        [Tooltip("Played at each light as it switches on.")]
        [SerializeField] private SoundEffect switchSound = new SoundEffect();
        [Tooltip("Bulb popping as a light dies.")]
        [SerializeField] private SoundEffect dieSound = new SoundEffect();
        [Tooltip("Electric buzz for as long as a failing light glitches.")]
        [SerializeField] private SoundEffect glitchSound = new SoundEffect();
        [Tooltip("Power going down when the blackout starts.")]
        [SerializeField] private SoundEffect blackoutSound = new SoundEffect();

        [Header("Failing (StartFailing / Blackout)")]
        [Tooltip("Share of all the lights (sequence + finale) that die when they start failing.")]
        [Range(0f, 1f)] [SerializeField] private float failingDeadShare = 0.5f;
        [Tooltip("Seconds between two lights dying (random in this range).")]
        [SerializeField] private Vector2 dieInterval = new Vector2(0.25f, 0.7f);
        [Tooltip("Seconds a surviving light stays steady between glitches (random in this range).")]
        [SerializeField] private Vector2 glitchGap = new Vector2(1.2f, 4f);
        [Tooltip("Seconds one glitch (stuttering on and off) lasts (random in this range).")]
        [SerializeField] private Vector2 glitchLength = new Vector2(0.15f, 0.5f);
        [Tooltip("Seconds between two lights dying during the blackout.")]
        [SerializeField] private Vector2 blackoutInterval = new Vector2(0.08f, 0.25f);

        public bool IsOn { get; private set; }
        public bool IsFailing { get; private set; }

        private Color darkAmbient;
        private float[] onIntensity, finaleIntensity;
        private WaitForSeconds popWait, gapWait, flickerWait;

        // Every light (sequence, then finale) with its lit intensity, for failing and blackout.
        private Light[] allLights;
        private float[] allIntensity;
        private bool[] dead;
        private float[] nextGlitch, glitchEnd;
        private int[] dieOrder;
        private int deadCount;

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

            int count = lights.Length + finaleLights.Length;
            allLights = new Light[count];
            allIntensity = new float[count];
            dead = new bool[count];
            nextGlitch = new float[count];
            glitchEnd = new float[count];
            dieOrder = new int[count];
            lights.CopyTo(allLights, 0);
            finaleLights.CopyTo(allLights, lights.Length);
            onIntensity.CopyTo(allIntensity, 0);
            finaleIntensity.CopyTo(allIntensity, lights.Length);
        }

        /// <summary>
        /// The lights start breaking down: about failingDeadShare of them stutter and go out one after another, the rest keep
        /// glitching (brief stutters) now and then. Run as a coroutine or fire and forget.
        /// </summary>
        public IEnumerator StartFailing()
        {
            if (IsFailing || allLights.Length == 0) yield break;
            IsFailing = true;
            for (int i = 0; i < allLights.Length; i++) nextGlitch[i] = Time.time + Random.Range(glitchGap.x, glitchGap.y);
            ShuffleDieOrder();

            int toKill = Mathf.RoundToInt(allLights.Length * failingDeadShare);
            for (int n = 0; n < toKill; n++)
            {
                yield return Die(dieOrder[n]);
                yield return new WaitForSeconds(Random.Range(dieInterval.x, dieInterval.y));
            }
        }

        /// <summary>Every light still working stutters and goes out, quickly one after another, and the room goes dark.</summary>
        public IEnumerator Blackout()
        {
            if (allLights.Length == 0) yield break;
            IsFailing = true;
            SoundManager.PlayGlobal(blackoutSound);
            ShuffleDieOrder();
            foreach (int i in dieOrder)
            {
                if (dead[i]) continue;
                yield return Die(i);
                yield return new WaitForSeconds(Random.Range(blackoutInterval.x, blackoutInterval.y));
            }
        }

        /// <summary>A light's last moments: a few hard stutters, then off for good. The ambient light drops with it.</summary>
        private IEnumerator Die(int i)
        {
            var l = allLights[i];
            if (dead[i] || l == null) yield break;
            dead[i] = true;
            for (int f = 0; f < 4; f++)
            {
                l.enabled = true; l.intensity = allIntensity[i] * Random.Range(0.4f, 1.3f); yield return flickerWait;
                l.enabled = false; yield return flickerWait;
            }
            SoundManager.PlayAt(dieSound, l.transform.position);
            deadCount++;
            RenderSettings.ambientLight = Color.Lerp(litAmbient, darkAmbient, (float)deadCount / allLights.Length);
        }

        private void ShuffleDieOrder()
        {
            for (int i = 0; i < dieOrder.Length; i++) dieOrder[i] = i;
            for (int i = dieOrder.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (dieOrder[i], dieOrder[j]) = (dieOrder[j], dieOrder[i]);
            }
        }

        /// <summary>While failing, each surviving light glitches now and then: it stutters on and off, then steadies again.</summary>
        private void Update()
        {
            if (!IsFailing) return;
            float now = Time.time;
            for (int i = 0; i < allLights.Length; i++)
            {
                var l = allLights[i];
                if (dead[i] || l == null) continue;

                if (now >= nextGlitch[i])
                {
                    glitchEnd[i] = now + Random.Range(glitchLength.x, glitchLength.y);
                    nextGlitch[i] = glitchEnd[i] + Random.Range(glitchGap.x, glitchGap.y);
                    SoundManager.PlayAt(glitchSound, l.transform.position, glitchEnd[i] - now);
                }
                if (now < glitchEnd[i])
                {
                    l.enabled = Random.value > 0.45f;
                    l.intensity = allIntensity[i] * Random.Range(0.3f, 1.1f);
                }
                else
                {
                    l.enabled = true;
                    l.intensity = allIntensity[i];
                }
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
                SoundManager.PlayAt(switchSound, l.transform.position);
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
            if (finaleLights[0] != null) SoundManager.PlayAt(switchSound, finaleLights[0].transform.position);
            yield return popWait;
            for (int i = 0; i < finaleLights.Length; i++) if (finaleLights[i] != null) finaleLights[i].intensity = finaleIntensity[i];
        }
    }
}
