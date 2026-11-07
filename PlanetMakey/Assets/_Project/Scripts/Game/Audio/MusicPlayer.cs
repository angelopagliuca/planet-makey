using System;
using PlanetMakey.Game.Round;
using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// The game's music: it starts at the beginning of dusk and fades in, plays through the night, and fades
    /// out when dawn begins. It follows the round, so skipping the night to dawn or aborting a round fades it
    /// out the same way, and a new round plays the track again from the start. It plays one track, once, on
    /// an AudioSource that this component takes over while it is enabled and puts back when it is disabled.
    /// The round's length should match the track's, so that the track ends when the round does.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoundController _round;

        [Tooltip("The AudioSource that plays the music. Set it to 2D (Spatial Blend 0), with Play On Awake off and Loop off; this component sets those itself too.")]
        [SerializeField] private AudioSource _source;

        [Tooltip("Optional. The round's lengths, only used to warn in the Console when the whole round (dusk, night and dawn) is not about as long as the clip.")]
        [SerializeField] private RoundConfig _config;

        [Header("Music")]
        [Tooltip("The track. If it is empty, nothing plays and nothing breaks.")]
        [SerializeField] private AudioClip _clip;

        [Tooltip("How loud the music is at full volume, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.8f;

        [Tooltip("Seconds the music takes to fade in when dusk begins.")]
        [Min(0f)]
        [SerializeField] private float _fadeInSeconds = 2f;

        [Tooltip("Seconds the music takes to fade out when dawn begins.")]
        [Min(0f)]
        [SerializeField] private float _fadeOutSeconds = 2f;

        [Tooltip("How many seconds the round and the clip may differ in length before the Console warns.")]
        [Min(0f)]
        [SerializeField] private float _lengthTolerance = 1f;

        [Header("Debug")]
        [Tooltip("Write to the Console when the music starts, fades and stops. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // How loud the fade has brought the music, 0 to 1, and where it is heading.
        [NonSerialized] private float _level;
        [NonSerialized] private float _target;
        [NonSerialized] private bool _captured;

        // The AudioSource as it was when this component was enabled, put back when it is disabled.
        [NonSerialized] private AudioClip _clipWas;
        [NonSerialized] private bool _loopWas;
        [NonSerialized] private bool _playOnAwakeWas;
        [NonSerialized] private float _volumeWas;
        [NonSerialized] private float _spatialBlendWas;

        private void OnEnable()
        {
            if (_round == null || _source == null)
            {
                Debug.LogError("MusicPlayer needs a RoundController and an AudioSource, so it will do nothing.", this);
                return;
            }

            if (_clip == null)
            {
                Debug.LogWarning("MusicPlayer has no clip, so no music will play.", this);
            }
            else if (_config != null)
            {
                float roundSeconds = _config.DuskSeconds + _config.NightSeconds + _config.DawnSeconds;
                if (AudioFade.LengthsDiffer(roundSeconds, _clip.length, _lengthTolerance))
                {
                    Debug.LogWarning("The round lasts " + roundSeconds.ToString("0.0") + " s but the music '" + _clip.name + "' lasts "
                        + _clip.length.ToString("0.0") + " s, so the music will " + (_clip.length < roundSeconds ? "end before the round does" : "be faded out before it ends")
                        + ". Change the night length on the Round Config to match.", this);
                }
            }

            _clipWas = _source.clip;
            _loopWas = _source.loop;
            _playOnAwakeWas = _source.playOnAwake;
            _volumeWas = _source.volume;
            _spatialBlendWas = _source.spatialBlend;

            _source.Stop();
            _source.clip = _clip;
            _source.loop = false;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;

            _level = 0f;
            _target = 0f;
            _captured = true;

            _round.StateChanged += OnRoundStateChanged;

            // Enabled in the middle of a round: join in as if the music had started with it.
            RoundState state = _round.State;
            if (state == RoundState.Dusk || state == RoundState.Night)
            {
                StartMusic();
            }
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // The round can already be destroyed when the scene shuts down.
            if (_round != null)
            {
                _round.StateChanged -= OnRoundStateChanged;
            }

            // Leave the AudioSource as it was found, so a Play session leaves nothing changed.
            if (_source != null)
            {
                _source.Stop();
                _source.clip = _clipWas;
                _source.loop = _loopWas;
                _source.playOnAwake = _playOnAwakeWas;
                _source.volume = _volumeWas;
                _source.spatialBlend = _spatialBlendWas;
            }
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float seconds = _target > _level ? _fadeInSeconds : _fadeOutSeconds;
            _level = AudioFade.Approach(_level, _target, Time.deltaTime, seconds);
            _source.volume = _volume * AudioFade.Gain(_level);

            // Faded out: stop the track, so a new round plays it from the start.
            if (_target <= 0f && _level <= 0f && _source.isPlaying)
            {
                _source.Stop();
                Log("Faded out: the music stops.");
            }
        }

        private void OnRoundStateChanged(RoundState state)
        {
            switch (state)
            {
                case RoundState.Dusk:
                    StartMusic();
                    break;

                case RoundState.Dawn:
                case RoundState.Idle:
                    // Both fade out (and Idle is also where a round that was aborted early ends up).
                    if (_target > 0f)
                    {
                        _target = 0f;
                        Log("Fading out over " + _fadeOutSeconds.ToString("0.0#") + " s.");
                    }

                    break;
            }
        }

        // Plays the track from its start and fades it in.
        private void StartMusic()
        {
            if (_clip == null)
            {
                return;
            }

            _source.Stop();
            _source.clip = _clip;
            _source.time = 0f;
            _level = 0f;
            _target = 1f;
            _source.volume = 0f;
            _source.Play();
            Log("Music starts, fading in over " + _fadeInSeconds.ToString("0.0#") + " s.");
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[MusicPlayer] " + message, this);
            }
        }
    }
}
