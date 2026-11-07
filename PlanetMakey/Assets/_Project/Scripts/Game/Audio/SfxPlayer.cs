using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// The sound effects engine: a small pool of AudioSources that plays <see cref="SoundCue"/> slots, and the
    /// sources behind the loops (<see cref="SoundLoopVoice"/>). When every source in the pool is busy, the
    /// one that started longest ago is reused. It knows nothing about the game; <see cref="GameSounds"/>
    /// decides what plays when. The sources are created at run time as hidden children.
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("Optional. The Audio Mixer group every effect is sent to, to set their volume apart from the music. Leave it empty to play straight to the output.")]
        [SerializeField] private AudioMixerGroup _mixerGroup;

        [Tooltip("How loud every effect is, from 0 to 1. A slot's own volume is multiplied by this.")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1f;

        [Tooltip("How many one-shot sounds can play at once. A new one beyond this cuts off the one that started first.")]
        [Range(1, 32)]
        [SerializeField] private int _voices = 12;

        // The pool for one-shots: the sources, when each one is busy until, and when its sound started.
        [NonSerialized] private AudioSource[] _sources;
        [NonSerialized] private float[] _busyUntil;
        [NonSerialized] private float[] _startedAt;
        [NonSerialized] private bool _built;

        // Every loop voice made, whether or not it has a source right now (it has none while this player is off).
        [NonSerialized] private readonly List<SoundLoopVoice> _loops = new List<SoundLoopVoice>();

        /// <summary>How loud every effect is, 0 to 1.</summary>
        public float MasterVolume => _masterVolume;

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            Release();
        }

        private void Update()
        {
            if (!_built)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            for (int i = 0; i < _loops.Count; i++)
            {
                _loops[i].Tick(deltaTime);
            }
        }

        /// <summary>
        /// Plays a slot's sound once, after its delay. Does nothing if the slot is null or empty, or this
        /// player is disabled. Returns true if a sound was started.
        /// </summary>
        /// <param name="cue">The slot to play.</param>
        /// <param name="volumeScale">Multiplies the slot's own volume, for a quieter or louder instance.</param>
        public bool Play(SoundCue cue, float volumeScale = 1f)
        {
            if (cue == null || !cue.HasClip || !Build())
            {
                return false;
            }

            AudioClip clip = cue.PickClip();
            if (clip == null)
            {
                return false;
            }

            float pitch = cue.PickPitch();
            float delay = cue.Delay;
            float now = Time.unscaledTime;

            int index = ChooseVoice(now);
            AudioSource source = _sources[index];
            source.Stop();
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(cue.Volume * volumeScale * _masterVolume);

            if (delay > 0f)
            {
                source.PlayDelayed(delay);
            }
            else
            {
                source.Play();
            }

            // A faster pitch ends sooner. The sound starts after its delay, and that is what "oldest" means.
            _busyUntil[index] = now + delay + clip.length / pitch;
            _startedAt[index] = now + delay;
            return true;
        }

        /// <summary>
        /// Makes a voice for a loop slot. It is silent until its <see cref="SoundLoopVoice.Begin"/> is
        /// called. For an empty slot the voice is made but does nothing. While this player is disabled the
        /// voice waits, and gets its source when the player is enabled. Give it back with
        /// <see cref="ReleaseLoop"/> when it is no longer needed.
        /// </summary>
        public SoundLoopVoice CreateLoop(SoundLoop loop)
        {
            SoundLoopVoice voice = new SoundLoopVoice(loop, this);
            if (loop == null || loop.Clip == null)
            {
                return voice;
            }

            _loops.Add(voice);

            // Building gives a source to every voice already in the list, this one included.
            if (_built)
            {
                voice.Attach(CreateSource("Sfx Loop"));
            }
            else
            {
                Build();
            }

            return voice;
        }

        /// <summary>Stops a loop voice and removes its source. Does nothing for a voice from another player.</summary>
        public void ReleaseLoop(SoundLoopVoice voice)
        {
            if (voice == null || !_loops.Remove(voice))
            {
                return;
            }

            DestroySource(voice.Detach());
        }

        // Makes the sources the first time they are needed. Another component may ask for a sound before this
        // one has had its OnEnable, so it builds on demand. False if this player is switched off.
        private bool Build()
        {
            if (_built)
            {
                return true;
            }

            if (!enabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            int count = Mathf.Max(1, _voices);
            _sources = new AudioSource[count];
            _busyUntil = new float[count];
            _startedAt = new float[count];

            for (int i = 0; i < count; i++)
            {
                _sources[i] = CreateSource("Sfx Voice " + i);
                _busyUntil[i] = float.NegativeInfinity;
                _startedAt[i] = float.NegativeInfinity;
            }

            // Voices made while this player was off get their sources now.
            for (int i = 0; i < _loops.Count; i++)
            {
                _loops[i].Attach(CreateSource("Sfx Loop"));
            }

            _built = true;
            return true;
        }

        // Removes every source this player made, so a Play session leaves nothing behind.
        private void Release()
        {
            if (!_built)
            {
                return;
            }

            _built = false;

            for (int i = 0; i < _sources.Length; i++)
            {
                DestroySource(_sources[i]);
            }

            // The loop voices stay on the list, silent, and get new sources if this player is enabled again.
            for (int i = 0; i < _loops.Count; i++)
            {
                DestroySource(_loops[i].Detach());
            }

            _sources = null;
        }

        private static void DestroySource(AudioSource source)
        {
            if (source != null)
            {
                Destroy(source.gameObject);
            }
        }

        private AudioSource CreateSource(string objectName)
        {
            GameObject child = new GameObject(objectName);
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(transform, false);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = _mixerGroup;
            return source;
        }

        // A source that is free, or else the one whose sound started first.
        private int ChooseVoice(float now)
        {
            int oldest = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_busyUntil[i] <= now)
                {
                    return i;
                }

                if (_startedAt[i] < _startedAt[oldest])
                {
                    oldest = i;
                }
            }

            return oldest;
        }
    }
}
