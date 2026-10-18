using UnityEngine;
using MobileMarket.Common;
using MobileMarket.Managers;

namespace MobileMarket.Enemy
{
    /// <summary>
    /// The boss fight's overall sound: the battle music (from the first attack, after the floodlights are on: one part
    /// repeated for phases 1-2, another for phase 3, faded out on defeat), the heat build-up whine (louder and higher as the heat rises), the overheat siren when the vents open and
    /// an alarm on every phase change.
    /// </summary>
    public class BossAudio : MonoBehaviour
    {
        [SerializeField] private BossController boss;

        [Header("Music")]
        [SerializeField] private AudioClip music;
        [Tooltip("Seconds: the part repeated during phases 1 and 2.")]
        [SerializeField] private Vector2 earlyLoop = new Vector2(0f, 119f);
        [Tooltip("Seconds: the part repeated during phase 3.")]
        [SerializeField] private Vector2 finalLoop = new Vector2(122f, 176f);

        [Header("Heat")]
        [Tooltip("Looping whine while the boss heats up; muted while its vents are open.")]
        [SerializeField] private AudioSource heatLoop;
        [SerializeField] private float heatMaxVolume = 0.45f;
        [SerializeField] private Vector2 heatPitch = new Vector2(0.8f, 1.3f);
        [SerializeField] private SoundEffect overheatSiren = new SoundEffect();

        [Header("Phases")]
        [SerializeField] private SoundEffect phaseAlarm = new SoundEffect();

        private bool cooling;

        private void OnEnable()
        {
            if (boss == null) return;
            boss.OnPhaseChanged += HandlePhaseChanged;
            boss.OnHeatChanged += HandleHeatChanged;
            boss.OnCoolingStarted += HandleCoolingStarted;
            boss.OnCoolingEnded += HandleCoolingEnded;
            boss.OnDefeated += HandleDefeated;
        }

        private void OnDisable()
        {
            if (boss == null) return;
            boss.OnPhaseChanged -= HandlePhaseChanged;
            boss.OnHeatChanged -= HandleHeatChanged;
            boss.OnCoolingStarted -= HandleCoolingStarted;
            boss.OnCoolingEnded -= HandleCoolingEnded;
            boss.OnDefeated -= HandleDefeated;
        }

        /// <summary>
        /// Phase 1 begins once the floodlights are all on and the boss opens fire: that's when the music starts.
        /// Later phases sound the alarm, and phase 3 switches to its own part of the music.
        /// </summary>
        private void HandlePhaseChanged(int phase)
        {
            if (phase == 1)
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlayMusic(music, earlyLoop.x, earlyLoop.y);
                return;
            }
            SoundManager.PlayGlobal(phaseAlarm);
            if (phase == 3 && SoundManager.Instance != null) SoundManager.Instance.PlayMusic(music, finalLoop.x, finalLoop.y);
        }

        private void HandleHeatChanged(float heat)
        {
            if (heatLoop == null) return;
            float volume = cooling ? 0f : heat * heatMaxVolume;
            heatLoop.volume = volume;
            heatLoop.pitch = Mathf.Lerp(heatPitch.x, heatPitch.y, heat);
            if (volume > 0.01f && !heatLoop.isPlaying) heatLoop.Play();
            else if (volume <= 0.01f && heatLoop.isPlaying) heatLoop.Pause();
        }

        private void HandleCoolingStarted(float seconds)
        {
            cooling = true;
            if (heatLoop != null) heatLoop.Pause();
            SoundManager.PlayGlobal(overheatSiren);
        }

        private void HandleCoolingEnded() => cooling = false;

        private void HandleDefeated()
        {
            if (heatLoop != null) heatLoop.Stop();
            if (SoundManager.Instance != null) SoundManager.Instance.StopMusic();
        }
    }
}
