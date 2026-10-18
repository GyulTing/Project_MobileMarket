using UnityEngine;
using MobileMarket.Common;

namespace MobileMarket.Managers
{
    /// <summary>
    /// Plays one-shot 3D sounds (SoundEffect) from a fixed pool of AudioSources, so no sources are created during play.
    /// Applies each sound's limits (min interval; max voices, where the oldest copy makes way) and cuts long clips off with a short fade.
    /// Looping sounds that follow an object (turret motors, flying rockets) use their own AudioSource instead.
    /// Also plays music: PlayMusic crossfades into a clip and repeats a chosen part of it.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [SerializeField] private int voiceCount = 32;
        [Tooltip("Full volume within this distance of the listener (the camera sits high above the player).")]
        [SerializeField] private float minDistance = 12f;
        [SerializeField] private float maxDistance = 60f;
        [SerializeField] private float cutFadeTime = 0.15f;

        [Header("Music")]
        [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.5f;
        [Tooltip("Seconds for new music to fade in (and the old one to cross out).")]
        [SerializeField] private float musicFadeIn = 1f;
        [Tooltip("Seconds for music to fade out when stopped or replaced.")]
        [SerializeField] private float musicFadeOut = 3f;

        private AudioSource[] voices;
        private AudioClip[] voiceClip;
        private float[] voiceStopTime;
        private float[] voiceVolume;
        private float[] voiceStartTime;
        private int nextVoice;

        // Two music sources so one part can crossfade into the next.
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly int[] musicLoopStart = new int[2];
        private readonly int[] musicLoopEnd = new int[2];
        private readonly float[] musicTarget = new float[2];
        private int musicIndex;

        // Limits are per clip, so every pooled bullet's own SoundEffect counts as the same sound.
        private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayTime =
            new System.Collections.Generic.Dictionary<AudioClip, float>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            voices = new AudioSource[voiceCount];
            voiceClip = new AudioClip[voiceCount];
            voiceStopTime = new float[voiceCount];
            voiceVolume = new float[voiceCount];
            voiceStartTime = new float[voiceCount];
            for (int i = 0; i < voiceCount; i++)
            {
                var go = new GameObject("Voice_" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.dopplerLevel = 0f;
                voices[i] = source;
            }

            for (int i = 0; i < music.Length; i++)
            {
                var go = new GameObject("Music_" + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.loop = false;   // the loop part is handled in UpdateMusic
                source.priority = 0;
                music[i] = source;
            }
        }

        /// <summary>Plays a sound at a world position (does nothing without a SoundManager in the scene).</summary>
        public static void PlayAt(SoundEffect effect, Vector3 position, float durationOverride = -1f)
        {
            if (Instance != null) Instance.Play(effect, position, durationOverride);
        }

        /// <summary>Plays a sound that isn't tied to a spot (for effects marked Global).</summary>
        public static void PlayGlobal(SoundEffect effect, float durationOverride = -1f)
        {
            if (Instance != null) Instance.Play(effect, Instance.transform.position, durationOverride);
        }

        // ---------------------------------------------------------------- music

        /// <summary>
        /// Crossfades into a clip and keeps repeating the part between loopStart and loopEnd (seconds; loopEnd 0 = clip end).
        /// Playing starts at loopStart.
        /// </summary>
        public void PlayMusic(AudioClip clip, float loopStart, float loopEnd)
        {
            if (clip == null) return;
            // The other source takes the new part; the current one fades out.
            musicTarget[musicIndex] = 0f;
            musicIndex = 1 - musicIndex;

            var source = music[musicIndex];
            source.clip = clip;
            musicLoopStart[musicIndex] = Mathf.Clamp(Mathf.RoundToInt(loopStart * clip.frequency), 0, clip.samples - 1);
            int end = loopEnd > 0f ? Mathf.RoundToInt(loopEnd * clip.frequency) : clip.samples;
            musicLoopEnd[musicIndex] = Mathf.Clamp(end, musicLoopStart[musicIndex] + 1, clip.samples);
            source.volume = 0f;
            source.timeSamples = musicLoopStart[musicIndex];
            source.Play();
            musicTarget[musicIndex] = musicVolume;
        }

        public void StopMusic()
        {
            musicTarget[0] = 0f;
            musicTarget[1] = 0f;
        }

        /// <summary>Fades the music sources toward their targets and jumps playing ones back to their loop start.</summary>
        private void UpdateMusic(float dt)
        {
            for (int i = 0; i < music.Length; i++)
            {
                var source = music[i];
                if (!source.isPlaying) continue;

                float speed = musicTarget[i] > source.volume ? musicVolume / Mathf.Max(0.01f, musicFadeIn) : musicVolume / Mathf.Max(0.01f, musicFadeOut);
                source.volume = Mathf.MoveTowards(source.volume, musicTarget[i], speed * dt);
                if (musicTarget[i] <= 0f && source.volume <= 0f)
                {
                    source.Stop();
                    continue;
                }

                int over = source.timeSamples - musicLoopEnd[i];
                if (over >= 0) source.timeSamples = musicLoopStart[i] + over;
            }
        }

        public void Play(SoundEffect effect, Vector3 position, float durationOverride = -1f)
        {
            if (effect == null || effect.clip == null) return;

            float now = Time.time;
            var clip = effect.clip;
            if (lastPlayTime.TryGetValue(clip, out float last) && now - last < effect.minInterval) return;
            lastPlayTime[clip] = now;

            // At the limit, the oldest copy makes way for the new one, so rapid hits keep sounding instead of dropping out.
            int i = effect.maxVoices > 0 && CountPlaying(clip) >= effect.maxVoices ? OldestVoice(clip) : FreeVoice();
            var source = voices[i];
            source.Stop();
            source.transform.position = position;
            source.spatialBlend = effect.global ? 0f : 1f;
            source.clip = clip;
            voiceVolume[i] = effect.volume;
            source.volume = effect.volume;
            source.pitch = 1f + Random.Range(-effect.pitchJitter, effect.pitchJitter);
            source.Play();
            voiceClip[i] = clip;
            voiceStartTime[i] = now;
            float duration = durationOverride > 0f ? durationOverride : effect.maxDuration;
            voiceStopTime[i] = duration > 0f ? now + duration : float.PositiveInfinity;
        }

        private int CountPlaying(AudioClip clip)
        {
            int count = 0;
            for (int i = 0; i < voices.Length; i++) if (voiceClip[i] == clip && voices[i].isPlaying) count++;
            return count;
        }

        /// <summary>The voice that has been playing this clip the longest.</summary>
        private int OldestVoice(AudioClip clip)
        {
            int oldest = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voiceClip[i] != clip || !voices[i].isPlaying) continue;
                if (oldest < 0 || voiceStartTime[i] < voiceStartTime[oldest]) oldest = i;
            }
            return oldest >= 0 ? oldest : FreeVoice();
        }

        /// <summary>A silent voice, or (all busy) the next one in turn.</summary>
        private int FreeVoice()
        {
            for (int n = 0; n < voices.Length; n++)
            {
                int i = (nextVoice + n) % voices.Length;
                if (voices[i].isPlaying) continue;
                nextVoice = (i + 1) % voices.Length;
                return i;
            }
            int oldest = nextVoice;
            nextVoice = (nextVoice + 1) % voices.Length;
            return oldest;
        }

        /// <summary>Fades out and stops voices that reached their cut-off time.</summary>
        private void Update()
        {
            UpdateMusic(Time.unscaledDeltaTime);
            float now = Time.time;
            for (int i = 0; i < voices.Length; i++)
            {
                if (float.IsPositiveInfinity(voiceStopTime[i]) || !voices[i].isPlaying) continue;
                float left = voiceStopTime[i] - now;
                if (left <= 0f)
                {
                    voices[i].Stop();
                    voiceStopTime[i] = float.PositiveInfinity;
                    voiceClip[i] = null;
                }
                else if (left < cutFadeTime) voices[i].volume = voiceVolume[i] * (left / cutFadeTime);
            }
        }
    }
}
