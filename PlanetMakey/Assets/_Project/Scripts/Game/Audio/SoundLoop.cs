using System;
using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// One slot for a sound that repeats for as long as something lasts, such as the crickets at night or the
    /// tractor beam's hum. It fades in when it starts and out when it stops. A slot with no clip plays
    /// nothing, and nothing breaks.
    /// </summary>
    [Serializable]
    public sealed class SoundLoop
    {
        [Tooltip("The sound that repeats. Make it a clip that loops cleanly. Leave it empty for silence.")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("How loud the sound is, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        [Tooltip("The pitch it plays at. 1 is the clip as recorded, 2 is an octave up.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float _pitch = 1f;

        [Tooltip("Seconds it takes to fade in when it starts.")]
        [Min(0f)]
        [SerializeField] private float _fadeInSeconds = 0.3f;

        [Tooltip("Seconds it takes to fade out when it stops.")]
        [Min(0f)]
        [SerializeField] private float _fadeOutSeconds = 0.3f;

        /// <summary>A slot with short fades.</summary>
        public SoundLoop()
        {
        }

        /// <summary>A slot with the given fade times, in seconds.</summary>
        public SoundLoop(float fadeInSeconds, float fadeOutSeconds)
        {
            _fadeInSeconds = fadeInSeconds;
            _fadeOutSeconds = fadeOutSeconds;
        }

        /// <summary>The sound that repeats, or null if the slot is empty.</summary>
        public AudioClip Clip => _clip;

        /// <summary>How loud the sound is, 0 to 1.</summary>
        public float Volume => _volume;

        /// <summary>The pitch it plays at.</summary>
        public float Pitch => _pitch;

        /// <summary>Seconds to fade in.</summary>
        public float FadeInSeconds => _fadeInSeconds;

        /// <summary>Seconds to fade out.</summary>
        public float FadeOutSeconds => _fadeOutSeconds;
    }
}
