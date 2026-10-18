using UnityEngine;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// Idle vocal sounds (zombie groans): every few seconds while alive it plays one of its clips, picked at random each
    /// time (never the same one twice in a row), from its own 3D source so the sound moves with it. Fades out with distance.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyVoice : MonoBehaviour
    {
        [SerializeField] private AudioClip[] clips = System.Array.Empty<AudioClip>();
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;
        [Range(0f, 0.3f)] [SerializeField] private float pitchJitter = 0.08f;
        [Tooltip("Seconds between two sounds (random in this range).")]
        [SerializeField] private Vector2 interval = new Vector2(4f, 9f);
        [Tooltip("Full volume within this distance of the listener; silent beyond maxDistance. The listener is the camera, " +
                 "about 23 m from the player (high up and behind), so this has to reach that far for nearby zombies to be heard.")]
        [SerializeField] private float minDistance = 24f;
        [SerializeField] private float maxDistance = 45f;

        private EnemyHealth health;
        private AudioSource source;
        private float nextTime;
        private int lastClip = -1;

        private void Awake()
        {
            health = GetComponent<EnemyHealth>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        private void OnEnable()
        {
            // Reused from the pool: start the timer fresh, and offset it so a spawned group doesn't groan in unison.
            nextTime = Time.time + Random.Range(0.5f, interval.y);
            health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            health.OnDied -= HandleDied;
        }

        private void HandleDied(EnemyHealth _) => source.Stop();

        private void Update()
        {
            if (health.IsDead || clips.Length == 0 || Time.time < nextTime) return;
            nextTime = Time.time + Random.Range(interval.x, interval.y);
            if (source.isPlaying) return;

            source.clip = clips[PickClip()];
            source.volume = volume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }

        private int PickClip()
        {
            if (clips.Length == 1) return 0;
            // Any clip but the last one played (any at all for the first).
            int pick = lastClip < 0 ? Random.Range(0, clips.Length) : Random.Range(0, clips.Length - 1);
            if (lastClip >= 0 && pick >= lastClip) pick++;
            lastClip = pick;
            return pick;
        }
    }
}
