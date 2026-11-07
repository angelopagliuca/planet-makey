using System;
using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// One slot for a short sound that plays once, such as a moo or a chime. It holds a list of takes
    /// (one is picked at random, never the same one twice in a row), how loud it is, how much its pitch
    /// varies and how long to wait before it plays. A slot with no clip plays nothing, and nothing breaks.
    /// </summary>
    [Serializable]
    public sealed class SoundCue
    {
        [Tooltip("The takes of this sound. One is picked at random each time. Leave the list empty for silence.")]
        [SerializeField] private AudioClip[] _clips;

        [Tooltip("How loud the sound is, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 1f;

        [Tooltip("The lowest pitch it plays at. 1 is the clip as recorded, 2 is an octave up.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float _pitchMin = 1f;

        [Tooltip("The highest pitch it plays at. A random pitch between the lowest and this is used each time, so the same clip does not sound the same twice. Set both to 1 for no change.")]
        [Range(0.5f, 2f)]
        [SerializeField] private float _pitchMax = 1f;

        [Tooltip("Seconds to wait after the moment this sound belongs to before it plays.")]
        [Min(0f)]
        [SerializeField] private float _delay;

        // The take played last, so the next one is a different take when there is a choice.
        [NonSerialized] private int _lastPicked = -1;

        /// <summary>A slot with no delay, at full volume.</summary>
        public SoundCue()
        {
        }

        /// <summary>A slot whose sound waits <paramref name="delaySeconds"/> after its moment before it plays.</summary>
        public SoundCue(float delaySeconds)
        {
            _delay = delaySeconds;
        }

        /// <summary>True if the slot has at least one clip.</summary>
        public bool HasClip
        {
            get
            {
                if (_clips == null)
                {
                    return false;
                }

                for (int i = 0; i < _clips.Length; i++)
                {
                    if (_clips[i] != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>How loud the sound is, 0 to 1.</summary>
        public float Volume => _volume;

        /// <summary>Seconds to wait before the sound plays.</summary>
        public float Delay => _delay;

        /// <summary>Picks a take at random, avoiding the one played last when there is a choice. Null if the slot has no clip.</summary>
        public AudioClip PickClip()
        {
            if (_clips == null)
            {
                return null;
            }

            int usable = 0;
            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] != null)
                {
                    usable++;
                }
            }

            if (usable == 0)
            {
                return null;
            }

            // Skip the take played last, if there is another to choose.
            bool skipLast = usable > 1 && _lastPicked >= 0 && _lastPicked < _clips.Length && _clips[_lastPicked] != null;
            int choices = skipLast ? usable - 1 : usable;
            int pick = UnityEngine.Random.Range(0, choices);

            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i] == null || (skipLast && i == _lastPicked))
                {
                    continue;
                }

                if (pick == 0)
                {
                    _lastPicked = i;
                    return _clips[i];
                }

                pick--;
            }

            return null;
        }

        /// <summary>A random pitch between the lowest and the highest, never below 0.1.</summary>
        public float PickPitch()
        {
            float low = Mathf.Min(_pitchMin, _pitchMax);
            float high = Mathf.Max(_pitchMin, _pitchMax);
            return Mathf.Max(0.1f, UnityEngine.Random.Range(low, high));
        }
    }
}
