using System;
using UnityEngine;

namespace MobileMarket.Common
{
    /// <summary>
    /// A one-shot sound as set up in the Inspector: the clip plus how to play it (volume, random pitch, an optional cut-off
    /// for long clips, and limits so rapid repeats don't pile up). Played through SoundManager.
    /// </summary>
    [Serializable]
    public class SoundEffect
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Random pitch change either way (0.08 = +-8%), so repeats don't sound identical.")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.06f;
        [Tooltip("Stops (with a short fade) after this many seconds; 0 plays the whole clip.")]
        public float maxDuration;
        [Tooltip("A new play is skipped if the same sound started less than this many seconds ago.")]
        public float minInterval = 0.03f;
        [Tooltip("At most this many copies of the sound play at once; a new one replaces the oldest.")]
        public int maxVoices = 4;
        [Tooltip("Heard everywhere at the same volume (alarms, sirens) instead of from a spot in the world.")]
        public bool global;
    }
}
