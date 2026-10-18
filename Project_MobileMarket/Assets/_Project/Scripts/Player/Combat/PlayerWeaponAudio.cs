using UnityEngine;
using MobileMarket.Items;

namespace MobileMarket.Player
{
    /// <summary>
    /// Plays the weapon's fire sound for every round the player fires. Shots take turns over a few voices, so each one's
    /// tail rings out under the next instead of being cut, and each gets a slight random pitch so bursts don't sound
    /// machine-identical. Works in any scene (doesn't need the SoundManager).
    /// </summary>
    [RequireComponent(typeof(PlayerCombat))]
    public class PlayerWeaponAudio : MonoBehaviour
    {
        [SerializeField] private int voiceCount = 4;
        [Range(0f, 0.2f)] [SerializeField] private float pitchJitter = 0.04f;

        private PlayerCombat combat;
        private AudioSource[] voices;
        private int next;

        private void Awake()
        {
            combat = GetComponent<PlayerCombat>();
            voices = new AudioSource[Mathf.Max(1, voiceCount)];
            for (int i = 0; i < voices.Length; i++)
            {
                // The player's own gun: heard at the same level wherever the camera is.
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                voices[i] = source;
            }
        }

        private void OnEnable() => combat.OnFired += HandleFired;
        private void OnDisable() => combat.OnFired -= HandleFired;

        private void HandleFired(WeaponData weapon)
        {
            if (weapon == null || weapon.fireSound == null) return;
            var source = voices[next];
            next = (next + 1) % voices.Length;
            source.clip = weapon.fireSound;
            source.volume = weapon.fireVolume;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.Play();
        }
    }
}
