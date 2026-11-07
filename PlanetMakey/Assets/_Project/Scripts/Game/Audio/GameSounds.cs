using System;
using PlanetMakey.Core;
using PlanetMakey.Game.Cows;
using PlanetMakey.Game.Round;
using PlanetMakey.Game.Ufo;
using UnityEngine;

namespace PlanetMakey.Game.Audio
{
    /// <summary>
    /// Every sound effect slot in the game, in one place, and the moments that play them. It listens to the
    /// round, to Makey and to the herd, and asks the <see cref="SfxPlayer"/> to play the matching slot. A
    /// slot with no clip is silent, so the game runs the same with none, some or all of them filled. The
    /// music is separate (<see cref="MusicPlayer"/>).
    /// </summary>
    /// <remarks>
    /// The beam and the cow loops follow Makey's abduction phase, which is read every frame because he does
    /// not announce phase changes. Everything else is event driven. The round, Makey and the cow view are
    /// optional: without one, only the sounds that depend on it are missing.
    /// </remarks>
    public sealed class GameSounds : MonoBehaviour
    {
        // The beam's visual pulse runs at these rates (see BeamView), and its hum tremolo follows them.
        private const float CalmTremoloPerSecond = 1f;
        private const float UrgentTremoloPerSecond = 3f;

        // At most this many hop thumps in this many seconds, however many cows hop.
        private const int ThumpsPerWindow = 3;
        private const float ThumpWindowSeconds = 0.5f;

        [Header("Wiring")]
        [Tooltip("The sound engine that plays the slots below.")]
        [SerializeField] private SfxPlayer _sfx;

        [Tooltip("Optional. The round: gives the dusk, dawn and idle sounds. Without it those are missing.")]
        [SerializeField] private RoundController _round;

        [Tooltip("Optional. Makey: gives his sounds and the abduction sounds. Without it those are missing.")]
        [SerializeField] private Makey _makey;

        [Tooltip("Optional. The cow view: gives the hop thumps. Without it those are missing.")]
        [SerializeField] private CowHerdView _cowHerdView;

        [Header("Round: dusk")]
        [Tooltip("A bed of crickets. Fades in at dusk and out at dawn.")]
        [SerializeField] private SoundLoop _crickets = new SoundLoop(2f, 2f);

        [Header("Round: dawn")]
        [Tooltip("The rooster crows as dawn begins. This is the sound that ends the night.")]
        [SerializeField] private SoundCue _rooster = new SoundCue();

        [Tooltip("A chime as the moon turns into the sun on the bar. Plays when dawn begins.")]
        [SerializeField] private SoundCue _sunriseChime = new SoundCue();

        [Tooltip("The score sound when every cow was saved. Its delay lets the rooster finish first.")]
        [SerializeField] private SoundCue _scoreAllSaved = new SoundCue(1.5f);

        [Tooltip("The score sound when some cows were saved.")]
        [SerializeField] private SoundCue _scoreSomeSaved = new SoundCue(1.5f);

        [Tooltip("The score sound when no cow was saved, for example a sad moo.")]
        [SerializeField] private SoundCue _scoreNoneSaved = new SoundCue(1.5f);

        [Header("Round: idle")]
        [Tooltip("A quiet hum while the booth waits for a round, to draw people in. Stops when dusk begins.")]
        [SerializeField] private SoundLoop _idleHum = new SoundLoop(1f, 1f);

        [Tooltip("A far-off moo now and then while the booth waits.")]
        [SerializeField] private SoundCue _idleMoo = new SoundCue();

        [Tooltip("The shortest wait between far-off moos, in seconds.")]
        [Min(1f)]
        [SerializeField] private float _idleMooMin = 15f;

        [Tooltip("The longest wait between far-off moos, in seconds.")]
        [Min(1f)]
        [SerializeField] private float _idleMooMax = 40f;

        [Header("Makey: arriving")]
        [Tooltip("A swoop as Makey flies in. Match it to the length of the fly-in clip (about 2.5 s).")]
        [SerializeField] private SoundCue _flyInWhoosh = new SoundCue();

        [Tooltip("A giggle as Makey arrives. Its delay times it to the flight.")]
        [SerializeField] private SoundCue _flyInGiggle = new SoundCue(1f);

        [Header("Makey: shield up (one note per color)")]
        [SerializeField] private SoundCue _shieldRed = new SoundCue();
        [SerializeField] private SoundCue _shieldBlue = new SoundCue();
        [SerializeField] private SoundCue _shieldYellow = new SoundCue();
        [SerializeField] private SoundCue _shieldGreen = new SoundCue();
        [SerializeField] private SoundCue _shieldPurple = new SoundCue();
        [SerializeField] private SoundCue _shieldOrange = new SoundCue();

        [Header("Makey: hit with the wrong color")]
        [Tooltip("A metallic ping or boing as the ball bounces off the shield.")]
        [SerializeField] private SoundCue _deflectPing = new SoundCue();

        [Tooltip("A taunting giggle after a wrong color.")]
        [SerializeField] private SoundCue _deflectTaunt = new SoundCue();

        [Tooltip("How often the taunt plays after a wrong color, from 0 (never) to 1 (every time). Under 1 keeps it from getting annoying.")]
        [Range(0f, 1f)]
        [SerializeField] private float _tauntChance = 0.5f;

        [Header("Makey: hit with the right color")]
        [Tooltip("The shield shatters: glass and a zap.")]
        [SerializeField] private SoundCue _shatter = new SoundCue();

        [Tooltip("Makey says ow. Its delay puts it just after the shatter.")]
        [SerializeField] private SoundCue _shatterOw = new SoundCue(0.15f);

        [Header("Makey: dazed and leaving")]
        [Tooltip("Dizzy tweets for as long as he is dazed.")]
        [SerializeField] private SoundLoop _dazedTweets = new SoundLoop(0.2f, 0.3f);

        [Tooltip("A rattle and a boing as he shakes his head clear.")]
        [SerializeField] private SoundCue _recoverRattle = new SoundCue();

        [Tooltip("A long warp-away whoosh as he leaves at dawn. Match it to the escape clip (about 4.1 s).")]
        [SerializeField] private SoundCue _warpAway = new SoundCue();

        [Header("Abduction: the beam")]
        [Tooltip("A power-up as the beam starts down.")]
        [SerializeField] private SoundCue _beamStart = new SoundCue();

        [Tooltip("The beam's hum, from when it starts down until it shuts off. Its pitch and wobble rise as the cow nears the ship.")]
        [SerializeField] private SoundLoop _beamLoop = new SoundLoop(0.3f, 0.4f);

        [Tooltip("The share of the lift (0 to 1) after which the hum rises. Match the beam's Urgency Start.")]
        [Range(0f, 1f)]
        [SerializeField] private float _urgencyStart = 0.6f;

        [Tooltip("How much higher the hum's pitch is at the worst of the urgency, as a multiple: 1.4 is 40 percent higher.")]
        [Range(1f, 2f)]
        [SerializeField] private float _urgentPitch = 1.4f;

        [Tooltip("How much the hum's volume wobbles at the worst of the urgency, from 0 (not at all) to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _urgentWobble = 0.3f;

        [Header("Abduction: the cow")]
        [Tooltip("A startled moo as the beam reaches the cow.")]
        [SerializeField] private SoundCue _cowStartled = new SoundCue();

        [Tooltip("A cartoon boing as her eyes pop.")]
        [SerializeField] private SoundCue _cowEyePop = new SoundCue();

        [Tooltip("A frightened moo, for as long as she rises.")]
        [SerializeField] private SoundLoop _cowScaredLoop = new SoundLoop(0.2f, 0.2f);

        [Tooltip("A quick buzz under her trembling, for as long as she rises.")]
        [SerializeField] private SoundLoop _cowBuzzLoop = new SoundLoop(0.2f, 0.2f);

        [Tooltip("A pop or slurp, with a flash, as she is absorbed.")]
        [SerializeField] private SoundCue _cowAbsorbedPop = new SoundCue();

        [Tooltip("A faint falling moo as she is absorbed.")]
        [SerializeField] private SoundCue _cowAbsorbedMoo = new SoundCue();

        [Tooltip("A boing as she lands, when the shield breaks after she has started to rise.")]
        [SerializeField] private SoundCue _cowLandBoing = new SoundCue();

        [Tooltip("A relieved moo when she is saved.")]
        [SerializeField] private SoundCue _cowRelievedMoo = new SoundCue(0.15f);

        [Tooltip("A dramatic sting when the last cow is gone.")]
        [SerializeField] private SoundCue _herdEmptied = new SoundCue();

        [Header("The herd")]
        [Tooltip("A very quiet thump when a cow lands from a hop. Keep it soft: it plays often.")]
        [SerializeField] private SoundCue _hopThump = new SoundCue();

        [Tooltip("The shortest time between two thumps, in seconds. Together with a limit of three thumps in half a second, it keeps ten hopping cows from sounding like rain.")]
        [Min(0f)]
        [SerializeField] private float _hopMinInterval = 0.15f;

        [Header("Debug")]
        [Tooltip("Write each sound that plays to the Console, and how many slots have a clip. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // The running loops, made when this component is enabled.
        [NonSerialized] private SoundLoopVoice _cricketsVoice;
        [NonSerialized] private SoundLoopVoice _idleHumVoice;
        [NonSerialized] private SoundLoopVoice _dazedVoice;
        [NonSerialized] private SoundLoopVoice _beamVoice;
        [NonSerialized] private SoundLoopVoice _scaredVoice;
        [NonSerialized] private SoundLoopVoice _buzzVoice;

        // What the abduction sounds last saw of Makey.
        [NonSerialized] private bool _beamOn;
        [NonSerialized] private bool _lifting;
        [NonSerialized] private float _wobblePhase;

        [NonSerialized] private float _idleMooLeft;

        // When the last thumps played, oldest first in a ring.
        [NonSerialized] private float[] _thumpTimes;
        [NonSerialized] private int _thumpNext;
        [NonSerialized] private float _lastThumpTime;

        [NonSerialized] private bool _ready;

        private void OnEnable()
        {
            if (_sfx == null)
            {
                Debug.LogError("GameSounds needs an SfxPlayer, so it will play nothing.", this);
                return;
            }

            if (!_sfx.enabled || !_sfx.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("The SfxPlayer is disabled, so the sound effects will be silent.", this);
            }

            if (_round == null)
            {
                Debug.LogWarning("GameSounds has no RoundController, so the crickets, rooster, score and idle sounds will not play.", this);
            }

            if (_makey == null)
            {
                Debug.LogWarning("GameSounds has no Makey, so his sounds and the abduction sounds will not play.", this);
            }

            if (_cowHerdView == null)
            {
                Debug.LogWarning("GameSounds has no CowHerdView, so the hop thumps will not play.", this);
            }

            _cricketsVoice = _sfx.CreateLoop(_crickets);
            _idleHumVoice = _sfx.CreateLoop(_idleHum);
            _dazedVoice = _sfx.CreateLoop(_dazedTweets);
            _beamVoice = _sfx.CreateLoop(_beamLoop);
            _scaredVoice = _sfx.CreateLoop(_cowScaredLoop);
            _buzzVoice = _sfx.CreateLoop(_cowBuzzLoop);

            _beamOn = false;
            _lifting = false;
            _wobblePhase = 0f;
            _thumpTimes = new float[ThumpsPerWindow];
            for (int i = 0; i < _thumpTimes.Length; i++)
            {
                _thumpTimes[i] = float.NegativeInfinity;
            }

            _thumpNext = 0;
            _lastThumpTime = float.NegativeInfinity;
            _idleMooLeft = RollIdleMoo();

            if (_round != null)
            {
                _round.StateChanged += OnRoundStateChanged;
            }

            if (_makey != null)
            {
                _makey.StateChanged += OnMakeyStateChanged;
                _makey.ShieldRaised += OnShieldRaised;
                _makey.ShieldShattered += OnShieldShattered;
                _makey.BallDeflected += OnBallDeflected;
                _makey.CowAbsorbed += OnCowAbsorbed;
                _makey.CowDropped += OnCowDropped;
                _makey.HerdEmptied += OnHerdEmptied;
            }

            if (_cowHerdView != null)
            {
                _cowHerdView.HopLanded += OnHopLanded;
            }

            _ready = true;

            Log("Enabled. " + CountFilledSlots() + " slots have a clip; the empty ones are silent.");

            // Start from wherever the round already is.
            if (_round != null)
            {
                BeginRoundState(_round.State, false);
            }
        }

        private void OnDisable()
        {
            if (!_ready)
            {
                return;
            }

            _ready = false;

            // Any of these can already be destroyed when the scene shuts down.
            if (_round != null)
            {
                _round.StateChanged -= OnRoundStateChanged;
            }

            if (_makey != null)
            {
                _makey.StateChanged -= OnMakeyStateChanged;
                _makey.ShieldRaised -= OnShieldRaised;
                _makey.ShieldShattered -= OnShieldShattered;
                _makey.BallDeflected -= OnBallDeflected;
                _makey.CowAbsorbed -= OnCowAbsorbed;
                _makey.CowDropped -= OnCowDropped;
                _makey.HerdEmptied -= OnHerdEmptied;
            }

            if (_cowHerdView != null)
            {
                _cowHerdView.HopLanded -= OnHopLanded;
            }

            ReleaseAllLoops();
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            UpdateIdleMoo(deltaTime);
            UpdateAbduction(deltaTime);
        }

        // ---- The round ----

        private void OnRoundStateChanged(RoundState state)
        {
            BeginRoundState(state, true);
        }

        private void BeginRoundState(RoundState state, bool isChange)
        {
            switch (state)
            {
                case RoundState.Idle:
                    _cricketsVoice.End();
                    StartLoop("idle hum", _idleHumVoice);
                    _idleMooLeft = RollIdleMoo();
                    break;

                case RoundState.Dusk:
                case RoundState.Night:
                    // Joining in the middle of the night also gets the crickets, but not the dusk-only changes.
                    _idleHumVoice.End();
                    StartLoop("crickets", _cricketsVoice);
                    break;

                case RoundState.Dawn:
                    _cricketsVoice.End();
                    if (isChange)
                    {
                        Play("rooster", _rooster);
                        Play("sunrise chime", _sunriseChime);
                        PlayScore();
                    }

                    break;
            }
        }

        // Which score sound to play is decided by how many cows came through the night.
        private void PlayScore()
        {
            if (_round == null)
            {
                return;
            }

            int score = _round.Score;
            int total = _round.CowsTotal;

            if (score <= 0)
            {
                Play("score: none saved", _scoreNoneSaved);
            }
            else if (score >= total)
            {
                Play("score: all saved", _scoreAllSaved);
            }
            else
            {
                Play("score: some saved", _scoreSomeSaved);
            }
        }

        private void UpdateIdleMoo(float deltaTime)
        {
            if (_round == null || _round.State != RoundState.Idle)
            {
                return;
            }

            _idleMooLeft -= deltaTime;
            if (_idleMooLeft > 0f)
            {
                return;
            }

            Play("far-off moo", _idleMoo);
            _idleMooLeft = RollIdleMoo();
        }

        private float RollIdleMoo()
        {
            float low = Mathf.Min(_idleMooMin, _idleMooMax);
            float high = Mathf.Max(_idleMooMin, _idleMooMax);
            return UnityEngine.Random.Range(low, high);
        }

        // ---- Makey ----

        private void OnMakeyStateChanged(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Entering:
                    Play("fly-in whoosh", _flyInWhoosh);
                    Play("fly-in giggle", _flyInGiggle);
                    break;

                case MakeyState.Dazed:
                    StartLoop("dazed tweets", _dazedVoice);
                    break;

                case MakeyState.Recovering:
                    _dazedVoice.End();
                    Play("recover rattle", _recoverRattle);
                    break;

                case MakeyState.Leaving:
                    _dazedVoice.End();
                    Play("warp away", _warpAway);
                    break;

                default:
                    _dazedVoice.End();
                    break;
            }
        }

        private void OnShieldRaised(BallColor color)
        {
            switch (color)
            {
                case BallColor.Red:
                    Play("shield up: red", _shieldRed);
                    break;
                case BallColor.Blue:
                    Play("shield up: blue", _shieldBlue);
                    break;
                case BallColor.Yellow:
                    Play("shield up: yellow", _shieldYellow);
                    break;
                case BallColor.Green:
                    Play("shield up: green", _shieldGreen);
                    break;
                case BallColor.Purple:
                    Play("shield up: purple", _shieldPurple);
                    break;
                case BallColor.Orange:
                    Play("shield up: orange", _shieldOrange);
                    break;
            }
        }

        private void OnShieldShattered(BallColor color)
        {
            Play("shield shatter", _shatter);
            Play("shatter ow", _shatterOw);
        }

        private void OnBallDeflected(BallColor color, Vector3 point)
        {
            Play("deflect ping", _deflectPing);

            if (UnityEngine.Random.value < _tauntChance)
            {
                Play("deflect taunt", _deflectTaunt);
            }
        }

        // ---- The abduction ----

        // Reads Makey's phase: the beam sounds run from the beam starting down until it shuts off, and the
        // cow's loops while she rises.
        private void UpdateAbduction(float deltaTime)
        {
            if (_makey == null)
            {
                return;
            }

            AbductionPhase phase = _makey.AbductionPhase;
            bool beamNow = phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting;
            bool liftingNow = phase == AbductionPhase.Lifting;

            if (beamNow && !_beamOn)
            {
                Play("beam start", _beamStart);
                Play("cow startled", _cowStartled);
                Play("cow eye pop", _cowEyePop);
                StartLoop("beam hum", _beamVoice);
                _wobblePhase = 0f;
            }
            else if (!beamNow && _beamOn)
            {
                _beamVoice.End();
            }

            _beamOn = beamNow;

            if (liftingNow && !_lifting)
            {
                StartLoop("cow scared moo", _scaredVoice);
                StartLoop("cow buzz", _buzzVoice);
            }
            else if (!liftingNow && _lifting)
            {
                _scaredVoice.End();
                _buzzVoice.End();
            }

            _lifting = liftingNow;

            if (beamNow)
            {
                BendBeam(phase, deltaTime);
            }
        }

        // The hum rises in pitch and wobbles faster as the cow nears the ship, like the beam's own pulse.
        private void BendBeam(AbductionPhase phase, float deltaTime)
        {
            float urgency = phase == AbductionPhase.Lifting ? BeamShape.Urgency(_makey.AbductionProgress, _urgencyStart) : 0f;

            float pitch = Mathf.Lerp(1f, _urgentPitch, urgency);
            float rate = Mathf.Lerp(CalmTremoloPerSecond, UrgentTremoloPerSecond, urgency);
            _wobblePhase = Mathf.Repeat(_wobblePhase + 2f * Mathf.PI * rate * deltaTime, 2f * Mathf.PI);
            float volume = 1f - _urgentWobble * urgency * (0.5f + 0.5f * Mathf.Sin(_wobblePhase));

            _beamVoice.Bend(pitch, volume);
        }

        private void OnCowAbsorbed(int index)
        {
            Play("cow absorbed pop", _cowAbsorbedPop);
            Play("cow absorbed moo", _cowAbsorbedMoo);
        }

        private void OnCowDropped(int index, bool wasLifted)
        {
            if (wasLifted)
            {
                Play("cow lands", _cowLandBoing);
            }

            Play("cow relieved moo", _cowRelievedMoo);
        }

        private void OnHerdEmptied()
        {
            Play("herd emptied", _herdEmptied);
        }

        // ---- The herd ----

        private void OnHopLanded(int index)
        {
            float now = Time.unscaledTime;
            if (now - _lastThumpTime < _hopMinInterval)
            {
                return;
            }

            // The oldest of the last few thumps must be old enough, or this is one too many for the window.
            if (now - _thumpTimes[_thumpNext] < ThumpWindowSeconds)
            {
                return;
            }

            _thumpTimes[_thumpNext] = now;
            _thumpNext = (_thumpNext + 1) % _thumpTimes.Length;
            _lastThumpTime = now;

            Play("hop thump", _hopThump);
        }

        // ---- Helpers ----

        private void Play(string slot, SoundCue cue)
        {
            if (_sfx == null || !_sfx.Play(cue))
            {
                return;
            }

            Log("Plays " + slot + ".");
        }

        private void StartLoop(string slot, SoundLoopVoice voice)
        {
            if (voice == null || !voice.HasClip)
            {
                return;
            }

            if (!voice.IsOn)
            {
                Log("Starts the " + slot + " loop.");
            }

            voice.Begin();
        }

        // Gives every loop back to the player, which stops it and removes its source. They are made again on enable.
        private void ReleaseAllLoops()
        {
            // The player can already be destroyed when the scene shuts down.
            if (_sfx == null)
            {
                return;
            }

            _sfx.ReleaseLoop(_cricketsVoice);
            _sfx.ReleaseLoop(_idleHumVoice);
            _sfx.ReleaseLoop(_dazedVoice);
            _sfx.ReleaseLoop(_beamVoice);
            _sfx.ReleaseLoop(_scaredVoice);
            _sfx.ReleaseLoop(_buzzVoice);
        }

        // How many of the slots have at least one clip, for the line at the start.
        private string CountFilledSlots()
        {
            SoundCue[] cues =
            {
                _rooster, _sunriseChime, _scoreAllSaved, _scoreSomeSaved, _scoreNoneSaved, _idleMoo,
                _flyInWhoosh, _flyInGiggle, _shieldRed, _shieldBlue, _shieldYellow, _shieldGreen, _shieldPurple, _shieldOrange,
                _deflectPing, _deflectTaunt, _shatter, _shatterOw, _recoverRattle, _warpAway,
                _beamStart, _cowStartled, _cowEyePop, _cowAbsorbedPop, _cowAbsorbedMoo, _cowLandBoing, _cowRelievedMoo,
                _herdEmptied, _hopThump
            };

            SoundLoop[] loops =
            {
                _crickets, _idleHum, _dazedTweets, _beamLoop, _cowScaredLoop, _cowBuzzLoop
            };

            int filled = 0;
            for (int i = 0; i < cues.Length; i++)
            {
                if (cues[i] != null && cues[i].HasClip)
                {
                    filled++;
                }
            }

            for (int i = 0; i < loops.Length; i++)
            {
                if (loops[i] != null && loops[i].Clip != null)
                {
                    filled++;
                }
            }

            return filled + " of " + (cues.Length + loops.Length);
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[GameSounds] " + message, this);
            }
        }
    }
}
