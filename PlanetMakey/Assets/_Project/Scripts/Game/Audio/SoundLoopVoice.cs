using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// A running <see cref="SoundLoop"/>: it starts and stops the sound with a fade, and lets the caller
    /// bend its pitch and volume while it plays. <see cref="SfxPlayer"/> makes these and moves their fades
    /// along. A voice made for an empty slot does nothing at all, so callers never need to check.
    /// </summary>
    public sealed class SoundLoopVoice
    {
        private readonly SoundLoop _loop;
        private readonly SfxPlayer _owner;

        // The source that plays the sound. The owner attaches one when it is ready and takes it back when it
        // is switched off; without one the voice does nothing.
        private AudioSource _source;

        // How loud the fade has brought the sound, 0 to 1, and where it is heading.
        private float _level;
        private float _target;
        private float _pitchScale = 1f;
        private float _volumeScale = 1f;

        internal SoundLoopVoice(SoundLoop loop, SfxPlayer owner)
        {
            _loop = loop;
            _owner = owner;
        }

        /// <summary>True if the slot has a clip, so starting the voice makes a sound.</summary>
        public bool HasClip => _loop != null && _loop.Clip != null;

        /// <summary>True from <see cref="Begin"/> until <see cref="End"/>. During the fade-out after <see cref="End"/> it is already false.</summary>
        public bool IsOn => _target > 0f;

        /// <summary>Starts the sound and fades it in. If it is still fading out, it turns back up from where it is.</summary>
        public void Begin()
        {
            if (!HasClip || _source == null)
            {
                return;
            }

            _target = 1f;
            if (_source.isPlaying)
            {
                return;
            }

            _level = 0f;
            _source.clip = _loop.Clip;
            _source.loop = true;
            ApplyLevel();
            _source.Play();
        }

        /// <summary>Fades the sound out and stops it. Does nothing if it is not playing.</summary>
        public void End()
        {
            _target = 0f;
        }

        /// <summary>
        /// Bends the playing sound: its pitch is the slot's pitch times <paramref name="pitchScale"/>, and its
        /// volume is multiplied by <paramref name="volumeScale"/>. It stays set until it is changed.
        /// </summary>
        public void Bend(float pitchScale, float volumeScale)
        {
            _pitchScale = Mathf.Max(0.1f, pitchScale);
            _volumeScale = Mathf.Max(0f, volumeScale);
        }

        // Gives the voice the source it plays on.
        internal void Attach(AudioSource source)
        {
            _source = source;
            _source.loop = true;
            _level = 0f;
            _target = 0f;
        }

        // Takes the source back (the owner destroys it) and silences the voice until it is attached again.
        internal AudioSource Detach()
        {
            AudioSource source = _source;
            _source = null;
            _level = 0f;
            _target = 0f;
            return source;
        }

        // Moves the fade along. Called by the owner every frame.
        internal void Tick(float deltaTime)
        {
            if (_source == null || !_source.isPlaying)
            {
                return;
            }

            float seconds = _target > _level ? _loop.FadeInSeconds : _loop.FadeOutSeconds;
            _level = AudioFade.Approach(_level, _target, deltaTime, seconds);
            ApplyLevel();

            // Faded out: stop it, so the next start begins from the top.
            if (_target <= 0f && _level <= 0f)
            {
                _source.Stop();
            }
        }

        private void ApplyLevel()
        {
            _source.volume = Mathf.Clamp01(_loop.Volume * _volumeScale * _owner.MasterVolume * AudioFade.Gain(_level));
            _source.pitch = _loop.Pitch * _pitchScale;
        }
    }
}
