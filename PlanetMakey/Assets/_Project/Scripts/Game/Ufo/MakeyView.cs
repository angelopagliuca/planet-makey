using System;
using PlanetMakey.Core;
using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// How Makey looks while he plays: the colored shield, how it bursts, the flash and sparks
    /// when a wrong color hits, and the bob, spin and head shake of the model. It only reads
    /// from <see cref="Makey"/> and holds no game state. These are placeholder effects made in
    /// code, to be replaced as real animation arrives.
    /// </summary>
    public sealed class MakeyView : MonoBehaviour
    {
        // The color property of the URP Lit shader.
        private const string BaseColorProperty = "_BaseColor";

        // The glow strength of the PlanetMakey/Shield shader.
        private const string GlowProperty = "_Glow";

        // How much brighter the shield glows at the peak of the flash after a wrong color hits.
        private const float FlashGlowBoost = 0.8f;

        [Header("Wiring")]
        [SerializeField] private Makey _makey;

        [Tooltip("Display colors for the ball colors.")]
        [SerializeField] private BallColorPalette _palette;

        [Tooltip("The object that holds the model. It is bobbed, spun and shaken, so the collider on the Makey object stays still.")]
        [SerializeField] private Transform _modelPivot;

        [Tooltip("The shield: a sphere around the model with no collider, using a material with the PlanetMakey/Shield shader (a transparent URP Lit material still works, without the glow).")]
        [SerializeField] private Renderer _shield;

        [Tooltip("Optional. Switched on while Makey is dazed, for example a few small stars.")]
        [SerializeField] private GameObject _dazeEffect;

        [Tooltip("Optional. Gives a burst in the ball's color when a wrong color hits. Set Play On Awake off and Simulation Space to World.")]
        [SerializeField] private ParticleSystem _sparks;

        [Header("Intro animation")]
        [Tooltip("Optional. The Animator on the model, with a controller whose Intro state holds the fly-in clip. When set, the model is hidden until Makey starts to enter, and the clip then follows his entering progress. Also tick Intro Flies In on Makey.")]
        [SerializeField] private Animator _modelAnimator;

        [Tooltip("Name of the state in the Animator that holds the fly-in clip.")]
        [SerializeField] private string _introStateName = "Intro";

        [Tooltip("Name of the state in the Animator that holds the escape clip, played while Makey leaves at the end of a round. If the controller has no such state, he just stays in the idle pose until he is hidden.")]
        [SerializeField] private string _escapeStateName = "Escape";

        [Header("Warp animation")]
        [Tooltip("Name of the Animator layer that holds the warp clip, played once over the idle pose each time Makey's beam comes down on a cow. Add a layer with this name to the controller (blending Override, weight 0). Leave it empty to not use the warp clip. A missing layer is a warning, and the rest works as before.")]
        [SerializeField] private string _warpLayerName = "Warp";

        [Tooltip("Name of the state, on that layer, that holds the warp clip.")]
        [SerializeField] private string _warpStateName = "Warp";

        [Tooltip("How fast the warp clip plays, as a multiple of its own speed.")]
        [Min(0f)]
        [SerializeField] private float _warpSpeed = 1f;

        [Tooltip("Seconds the warp clip takes to blend in when the beam comes down, and out to the idle pose once it has played, so it need not start or end in the idle pose.")]
        [Min(0f)]
        [SerializeField] private float _warpBlendSeconds = 0.25f;

        [Tooltip("Name of the bone whose position the warp clip must not move: the ship's root bone, which carries the whole ship, so the clip would swing all of Makey about. It is held where the idle pose has it while the clip plays; its turn still follows the clip. Leave it empty to let the clip move it too.")]
        [SerializeField] private string _warpFixedBoneName = "Bone_Root";

        [Tooltip("When ticked, the ship stays on the clip's last frame after it has played, until the beam is gone, and only then blends to the idle pose. Unticked, it blends to the idle pose as soon as the clip has played.")]
        [SerializeField] private bool _warpHoldLastFrame;

        [Header("Shield")]
        [Tooltip("How strong the shield is, from 0 (invisible) to 1. The shield shader turns it into a faint fill and a brighter rim.")]
        [Range(0f, 1f)]
        [SerializeField] private float _shieldAlpha = 0.4f;

        [Tooltip("How hard the shield glows: its color is multiplied by this, so values above 1 are brighter than the screen's white and Bloom makes them glow. Needs the PlanetMakey/Shield shader and Bloom (the night grade has it).")]
        [Range(0f, 8f)]
        [SerializeField] private float _shieldGlow = 2.5f;

        [Tooltip("Seconds the shield takes to burst.")]
        [Min(0.01f)]
        [SerializeField] private float _shatterSeconds = 0.35f;

        [Tooltip("How many times its size the shield grows to while it bursts.")]
        [Min(1f)]
        [SerializeField] private float _shatterGrowth = 1.5f;

        [Tooltip("Seconds the shield glows in the ball's color after a wrong color hits.")]
        [Min(0.01f)]
        [SerializeField] private float _flashSeconds = 0.25f;

        [Tooltip("Particles in one burst of sparks.")]
        [Min(0)]
        [SerializeField] private int _sparkCount = 20;

        [Header("Motion")]
        [Tooltip("How far the model bobs up and down, in meters.")]
        [Min(0f)]
        [SerializeField] private float _bobHeight = 0.15f;

        [Tooltip("Bobs per second.")]
        [Min(0f)]
        [SerializeField] private float _bobsPerSecond = 0.6f;

        [Tooltip("How fast the daze stars circle around Makey while he is dazed, in degrees per second.")]
        [SerializeField] private float _starSpinDegreesPerSecond = 180f;

        [Tooltip("Full turns made while dazed. Keep it a whole number so the model ends facing the front.")]
        [Min(0f)]
        [SerializeField] private float _dazeTurns = 3f;

        [Tooltip("How far the head swings to each side while recovering, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _headShakeDegrees = 15f;

        [Tooltip("Swings from side to side while recovering.")]
        [Min(0f)]
        [SerializeField] private float _headShakeCycles = 4f;

        [Tooltip("Seconds the model rattles after a wrong color hits.")]
        [Min(0.01f)]
        [SerializeField] private float _rattleSeconds = 0.3f;

        [Tooltip("How far the model tilts to each side while it rattles, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _rattleDegrees = 10f;

        [Tooltip("Tilts from side to side during one rattle.")]
        [Min(0f)]
        [SerializeField] private float _rattleCycles = 3f;

        [Tooltip("Hops Makey makes while he celebrates an absorbed cow.")]
        [Min(0f)]
        [SerializeField] private float _celebrateHops = 3f;

        [Tooltip("How high each celebration hop goes, in meters.")]
        [Min(0f)]
        [SerializeField] private float _celebrateHopHeight = 0.4f;

        [Tooltip("How far the model wiggles from side to side while it celebrates, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _celebrateWiggleDegrees = 20f;

        [Header("Life: facing and tilt")]
        [Tooltip("How much Makey turns to keep facing the camera when he is off to one side, from 0 (never) to 1 (always straight at it). Below 1 keeps a little 3D look. Needs the camera wired on Makey.")]
        [Range(0f, 1f)]
        [SerializeField] private float _facingWeight = 0.7f;

        [Tooltip("How far the top of the ship tips towards the camera, in degrees, so more of the cockpit shows from below. Needs the camera wired on Makey.")]
        [Min(0f)]
        [SerializeField] private float _cockpitTiltDegrees = 10f;

        [Header("Life: lean into movement")]
        [Tooltip("The most the ship leans while he glides to a cow, in degrees.")]
        [Min(0f)]
        [SerializeField] private float _leanMaxDegrees = 18f;

        [Tooltip("The speed, in world units per second, at which the lean and the stretch reach their full size.")]
        [Min(0.1f)]
        [SerializeField] private float _leanFullSpeed = 8f;

        [Tooltip("How strongly the lean is pulled to where it should be. Higher is quicker.")]
        [Min(0.1f)]
        [SerializeField] private float _leanStiffness = 60f;

        [Tooltip("How much the lean is held back. About 7 with a stiffness of 60 overshoots a little when he stops, then settles. Higher is calmer.")]
        [Min(0f)]
        [SerializeField] private float _leanDamping = 7f;

        [Tooltip("How much the ship stretches up while he moves fast, as a share of its size. When he stops it squashes a little and settles.")]
        [Min(0f)]
        [SerializeField] private float _stretchAmount = 0.06f;

        [Header("Life: idle")]
        [Tooltip("How far the ship sways from side to side while he hovers, in the pivot's units.")]
        [Min(0f)]
        [SerializeField] private float _swayDistance = 0.12f;

        [Tooltip("Sways per second.")]
        [Min(0f)]
        [SerializeField] private float _swaysPerSecond = 0.3f;

        [Tooltip("How much the ship swells and shrinks as if it breathes, as a share of its size.")]
        [Min(0f)]
        [SerializeField] private float _breathAmount = 0.02f;

        [Tooltip("Breaths per second.")]
        [Min(0f)]
        [SerializeField] private float _breathsPerSecond = 0.25f;

        [Header("Life: strain while lifting a cow")]
        [Tooltip("How hard the ship shakes, in degrees, just before the cow reaches it. It grows with the square of the lift's progress.")]
        [Min(0f)]
        [SerializeField] private float _strainShakeDegrees = 3f;

        [Tooltip("How far the ship jitters, in the pivot's units, just before the cow reaches it.")]
        [Min(0f)]
        [SerializeField] private float _strainShakeDistance = 0.05f;

        [Tooltip("How fast the strain shake changes, in changes per second.")]
        [Min(0f)]
        [SerializeField] private float _strainShakeSpeed = 18f;

        [Tooltip("The share of the lift (0 to 1) after which the shield starts to flicker, to tell the players the cow is nearly gone.")]
        [Range(0f, 1f)]
        [SerializeField] private float _urgencyStart = 0.6f;

        [Tooltip("How much of its see-through the shield loses at the worst of the flicker, from 0 (no flicker) to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float _flickerDepth = 0.35f;

        [Tooltip("Flickers per second when the cow is almost at the ship. It starts at 4 and speeds up to this.")]
        [Min(4f)]
        [SerializeField] private float _flickerMaxPerSecond = 10f;

        [Header("Debug")]
        [Tooltip("Write what the view shows to the Console: how it is wired, every state it sees, every effect it starts, and a status line once a second. Turn it off when it is no longer needed.")]
        [SerializeField] private bool _debugLog = true;

        // Seconds between the status lines.
        private const float StatusLogInterval = 1f;

        // What the shield was last drawn as, for the status line.
        [NonSerialized] private bool _shieldShown;
        [NonSerialized] private Color _shieldDrawnColor;
        [NonSerialized] private float _shieldDrawnAlpha;
        [NonSerialized] private float _shieldDrawnSize;
        [NonSerialized] private bool _hasLoggedState;
        [NonSerialized] private MakeyState _lastLoggedState;
        [NonSerialized] private float _nextStatusLogTime;

        // Where things were when this component was enabled, put back when it is disabled.
        [NonSerialized] private Vector3 _pivotPosition;
        [NonSerialized] private Quaternion _pivotRotation;
        [NonSerialized] private Vector3 _pivotScale;
        [NonSerialized] private Vector3 _shieldScale;
        [NonSerialized] private bool _shieldWasEnabled;
        [NonSerialized] private Quaternion _dazeEffectRotation;
        [NonSerialized] private bool _sparksWereActive;
        [NonSerialized] private bool _captured;

        // The model's Animator: its settings when this component was enabled, put back when it is
        // disabled, and where the intro stands.
        [NonSerialized] private int _introHash;
        [NonSerialized] private int _escapeHash;
        [NonSerialized] private bool _escapeUsable;
        [NonSerialized] private bool _modelWasActive;
        [NonSerialized] private AnimatorCullingMode _cullingMode;
        [NonSerialized] private bool _applyRootMotion;
        [NonSerialized] private float _animatorSpeed;
        [NonSerialized] private bool _introChecked;
        [NonSerialized] private bool _introUsable;
        [NonSerialized] private bool _introHeld;
        [NonSerialized] private bool _animatorPrepared;
        [NonSerialized] private bool _introTransformsLogged;

        // The warp clip: its layer and state, how much of it is blended in, how long it has run (a time
        // that is placed by hand like the other clips) and how long the clip is.
        [NonSerialized] private int _warpLayer;
        [NonSerialized] private int _warpHash;
        [NonSerialized] private bool _warpUsable;
        [NonSerialized] private float _warpWeight;
        [NonSerialized] private float _warpTime;
        [NonSerialized] private float _warpLength;
        [NonSerialized] private bool _warpBeamWasOn;
        [NonSerialized] private bool _warpFinished;

        // The bone whose position is kept, and where the idle pose has it (sampled while the clip is off).
        [NonSerialized] private Transform _warpFixedBone;
        [NonSerialized] private Vector3 _warpFixedPosition;

        // Where the model's own object was placed in the scene. The clip rewrites it every
        // frame, so it is put back after the clip is applied.
        [NonSerialized] private Vector3 _modelRootPosition;
        [NonSerialized] private Quaternion _modelRootRotation;
        [NonSerialized] private Vector3 _modelRootScale;

        // A property block overrides the color without creating or editing a material.
        [NonSerialized] private MaterialPropertyBlock _block;

        // Counted down to zero by Update. Zero means the effect is not running.
        [NonSerialized] private float _shatterLeft;
        [NonSerialized] private Color _shatterColor;
        [NonSerialized] private float _flashLeft;
        [NonSerialized] private Color _flashColor;
        [NonSerialized] private float _rattleLeft;
        [NonSerialized] private bool _dazeShown;

        // The state of the lively motion: springs for the lean, the squash and stretch, how much of
        // the motion is switched on, and where Makey was last frame to tell how fast he moves.
        [NonSerialized] private MakeyMotion.Spring _rollSpring;
        [NonSerialized] private MakeyMotion.Spring _pitchSpring;
        [NonSerialized] private MakeyMotion.Spring _stretchSpring;
        [NonSerialized] private float _lifeWeight;
        [NonSerialized] private Vector3 _lastMakeyPosition;
        [NonSerialized] private bool _hasLastMakeyPosition;

        // How fast the lively motion fades in and out between states, per second.
        private const float LifeBlendPerSecond = 2f;

        // Keeps the shake of the strain from repeating the idle sway's phase.
        private const float StrainNoiseSeed = 17.3f;

        private void OnEnable()
        {
            if (_makey == null || _modelPivot == null || _shield == null)
            {
                Debug.LogError("MakeyView needs a Makey, a model pivot and a shield, so it will do nothing.", this);
                return;
            }

            if (_palette == null)
            {
                Debug.LogWarning("MakeyView has no palette, so the shield will be white.", this);
            }

            _pivotPosition = _modelPivot.localPosition;
            _pivotRotation = _modelPivot.localRotation;
            _pivotScale = _modelPivot.localScale;
            _shieldScale = _shield.transform.localScale;
            _shieldWasEnabled = _shield.enabled;
            _captured = true;

            _rollSpring.Reset();
            _pitchSpring.Reset();
            _stretchSpring.Reset();
            _lifeWeight = 0f;
            _hasLastMakeyPosition = false;

            _shatterLeft = 0f;
            _flashLeft = 0f;
            _rattleLeft = 0f;

            // Start with the stars off, whatever the scene was saved with.
            _dazeShown = false;
            if (_dazeEffect != null)
            {
                _dazeEffectRotation = _dazeEffect.transform.localRotation;
                _dazeEffect.SetActive(false);
            }

            if (_sparks != null)
            {
                _sparksWereActive = _sparks.gameObject.activeSelf;
            }

            _shieldShown = false;
            _hasLoggedState = false;
            _nextStatusLogTime = 0f;

            PrepareAnimator();

            _makey.ShieldShattered += OnShieldShattered;
            _makey.BallDeflected += OnBallDeflected;

            CheckWiring();
        }

        private void OnDisable()
        {
            if (!_captured)
            {
                return;
            }

            _captured = false;

            // Any of these can already be destroyed when the scene shuts down.
            if (_makey != null)
            {
                _makey.ShieldShattered -= OnShieldShattered;
                _makey.BallDeflected -= OnBallDeflected;
            }

            // Leave everything as it was found, so a Play session leaves nothing behind.
            if (_modelPivot != null)
            {
                _modelPivot.localPosition = _pivotPosition;
                _modelPivot.localRotation = _pivotRotation;
                _modelPivot.localScale = _pivotScale;
            }

            if (_shield != null)
            {
                _shield.transform.localScale = _shieldScale;
                _shield.SetPropertyBlock(null);
                _shield.enabled = _shieldWasEnabled;
            }

            if (_dazeEffect != null)
            {
                _dazeEffect.transform.localRotation = _dazeEffectRotation;
                _dazeEffect.SetActive(false);
            }

            if (_sparks != null)
            {
                _sparks.gameObject.SetActive(_sparksWereActive);
            }

            RestoreAnimator();
        }

        private void Update()
        {
            if (!_captured)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _shatterLeft = Mathf.Max(0f, _shatterLeft - deltaTime);
            _flashLeft = Mathf.Max(0f, _flashLeft - deltaTime);
            _rattleLeft = Mathf.Max(0f, _rattleLeft - deltaTime);

            MakeyState state = _makey.State;
            UpdateIntro(state);
            UpdateWarp(state);
            UpdateShield(state);
            UpdatePivot(state);
            UpdateDazeEffect(state);
            LogState(state);
        }

        /// <summary>True if a model Animator is wired in, so the intro clip does the fly-in.</summary>
        public bool HasIntroAnimator => _modelAnimator != null;

        private void PrepareAnimator()
        {
            _introChecked = false;
            _introUsable = false;
            _escapeUsable = false;
            _introHeld = false;
            _animatorPrepared = false;
            _introTransformsLogged = false;
            _warpUsable = false;
            _warpWeight = 0f;
            _warpTime = 0f;
            _warpLength = 0f;
            _warpBeamWasOn = false;
            _warpFinished = false;
            _warpFixedBone = null;

            if (_modelAnimator == null)
            {
                return;
            }

            // The model is switched off while Makey is away. If that object were this one, or one
            // above it, the whole view and Makey would be switched off with it.
            // The same goes for the model pivot: the model's own object is pinned to its scene
            // values every frame, which would undo the bob and spin of the pivot.
            if (transform.IsChildOf(_modelAnimator.transform) || _modelPivot.IsChildOf(_modelAnimator.transform))
            {
                Debug.LogWarning("The Model Animator is on this object, on the model pivot, or on one above them, so the intro is not used. Wire the Animator that is on the model itself, below the model pivot.", this);
                _introChecked = true;
                return;
            }

            _animatorPrepared = true;
            _introHash = Animator.StringToHash(_introStateName);
            _escapeHash = Animator.StringToHash(_escapeStateName);
            _modelRootPosition = _modelAnimator.transform.localPosition;
            _modelRootRotation = _modelAnimator.transform.localRotation;
            _modelRootScale = _modelAnimator.transform.localScale;
            _modelWasActive = _modelAnimator.gameObject.activeSelf;
            _cullingMode = _modelAnimator.cullingMode;
            _applyRootMotion = _modelAnimator.applyRootMotion;
            _animatorSpeed = _modelAnimator.speed;

            // The clip starts off to one side, maybe off screen, so it must run anyway. It is
            // stepped by hand and moves bones, not the object, so time and root motion are off.
            _modelAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _modelAnimator.applyRootMotion = false;
            _modelAnimator.speed = 0f;

            Log("Intro animation: Animator on '" + _modelAnimator.name + "', state '" + _introStateName
                + "'. The model stays hidden until Makey starts to enter.");
        }

        private void RestoreAnimator()
        {
            if (_modelAnimator == null || !_animatorPrepared)
            {
                return;
            }

            // The warp layer goes back to no weight, so the controller is left as it was found.
            if (_warpUsable)
            {
                if (_modelAnimator.gameObject.activeInHierarchy)
                {
                    _modelAnimator.SetLayerWeight(_warpLayer, 0f);
                }

                _warpUsable = false;
            }

            // The model's own object goes back to where the scene had it, before this is cleared.
            KeepModelRoot();
            _animatorPrepared = false;

            _modelAnimator.cullingMode = _cullingMode;
            _modelAnimator.applyRootMotion = _applyRootMotion;
            _modelAnimator.speed = _animatorSpeed;
            _modelAnimator.gameObject.SetActive(_modelWasActive);
        }

        // The model is hidden while Makey is away. While he enters, the intro clip is set to the
        // same point in its run as his entering progress, so the two always finish together
        // whatever the enter time. After that the last frame is held, which is the idle pose.
        // While he leaves, the escape clip is scrubbed the same way, and then he is Away again.
        private void UpdateIntro(MakeyState state)
        {
            if (_modelAnimator == null || (_introChecked && !_introUsable))
            {
                return;
            }

            GameObject modelObject = _modelAnimator.gameObject;

            if (state == MakeyState.Away)
            {
                if (modelObject.activeSelf)
                {
                    // A warp clip still blending out must not leave its pose for the next round's intro.
                    if (_warpUsable && _warpWeight > 0f)
                    {
                        _modelAnimator.SetLayerWeight(_warpLayer, 0f);
                        _warpWeight = 0f;
                    }

                    modelObject.SetActive(false);
                    Log("Model hidden until Makey starts to enter.");
                }

                _introHeld = false;
                return;
            }

            if (!modelObject.activeSelf)
            {
                modelObject.SetActive(true);
                Log("Model shown: Makey is " + state + ".");
            }

            if (!_introChecked)
            {
                CheckIntro();
                if (!_introUsable)
                {
                    return;
                }
            }

            if (state == MakeyState.Entering)
            {
                ShowClipAt(_introHash, _makey.StateProgress);
                _introHeld = false;
                return;
            }

            if (state == MakeyState.Leaving && _escapeUsable)
            {
                ShowClipAt(_escapeHash, _makey.StateProgress);
                _introHeld = false;
                return;
            }

            if (!_introHeld)
            {
                ShowClipAt(_introHash, 1f);
                _introHeld = true;
                Log("Intro finished: holding its last frame, the idle pose.");
            }
        }

        // Done once, the first time the model is shown, because an Animator on an object that is
        // switched off cannot be asked about its states.
        private void CheckIntro()
        {
            _introChecked = true;

            if (_modelAnimator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("The model Animator has no controller, so the intro cannot play. Make one with an '" + _introStateName + "' state.", this);
                return;
            }

            if (!_modelAnimator.HasState(0, _introHash))
            {
                Debug.LogWarning("The model Animator's controller has no state called '" + _introStateName + "' on its first layer, so the intro cannot play. Rename the state, or the field on this component.", this);
                return;
            }

            _introUsable = true;

            // The escape clip is optional: without it Makey keeps his idle pose until he is hidden.
            if (_modelAnimator.HasState(0, _escapeHash))
            {
                _escapeUsable = true;
            }
            else
            {
                Debug.LogWarning("The model Animator's controller has no state called '" + _escapeStateName + "' on its first layer, so Makey will not fly off with the escape clip at the end of a round. Add the state, or rename the field on this component.", this);
            }

            CheckWarp();
        }

        // The warp clip is optional too: without its layer Makey just holds the idle pose while he takes a cow.
        private void CheckWarp()
        {
            _warpUsable = false;

            if (string.IsNullOrEmpty(_warpLayerName))
            {
                return;
            }

            _warpLayer = _modelAnimator.GetLayerIndex(_warpLayerName);
            if (_warpLayer < 0)
            {
                Debug.LogWarning("The model Animator's controller has no layer called '" + _warpLayerName + "', so the warp clip will not play while Makey takes a cow. Add a layer with that name (blending Override, weight 0) holding a '" + _warpStateName + "' state, or leave the layer name empty on this component.", this);
                return;
            }

            if (_warpLayer == 0)
            {
                Debug.LogWarning("The warp layer '" + _warpLayerName + "' is the controller's base layer, which carries the intro and escape clips. Put the warp clip on a layer of its own.", this);
                return;
            }

            _warpHash = Animator.StringToHash(_warpStateName);
            if (!_modelAnimator.HasState(_warpLayer, _warpHash))
            {
                Debug.LogWarning("The warp layer '" + _warpLayerName + "' has no state called '" + _warpStateName + "', so the warp clip will not play. Add the state, or rename the field on this component.", this);
                return;
            }

            if (!string.IsNullOrEmpty(_warpFixedBoneName))
            {
                _warpFixedBone = FindBone(_modelAnimator.transform, _warpFixedBoneName);
                if (_warpFixedBone == null)
                {
                    Debug.LogWarning("No bone called '" + _warpFixedBoneName + "' was found under the model, so the warp clip is free to move the whole ship. Fix the bone name on this component, or leave it empty.", this);
                }
                else
                {
                    _warpFixedPosition = _warpFixedBone.localPosition;
                }
            }

            _modelAnimator.SetLayerWeight(_warpLayer, 0f);
            _warpUsable = true;
            Log("Warp animation: layer '" + _warpLayerName + "', state '" + _warpStateName + "', played once each time the beam comes down on a cow.");
        }

        // When the beam comes down on a cow, the warp clip plays once on its own layer, over the idle pose, from
        // its first frame. When it has run its length (or the beam is gone) the layer blends out to the idle pose.
        // Like the other clips it is placed by hand (the Animator's speed is 0): a time that runs on here picks
        // the point in the clip, and the layer's weight blends it in and out, so the clip does not have to start
        // or end in the idle pose.
        private void UpdateWarp(MakeyState state)
        {
            if (_modelAnimator == null || !_introUsable || !_warpUsable)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            bool onStage = state == MakeyState.Shielded || state == MakeyState.Dazed || state == MakeyState.Recovering;
            AbductionPhase phase = _makey.AbductionPhase;
            bool beamOn = onStage && _makey.TargetCowIndex >= 0
                && (phase == AbductionPhase.LoweringBeam || phase == AbductionPhase.Lifting);

            if (beamOn && !_warpBeamWasOn)
            {
                // A new abduction: the clip starts again from its first frame.
                _warpTime = 0f;
                _warpFinished = false;
                if (deltaTime > 0f)
                {
                    Log("Warp clip starts, to play once.");
                }
            }

            _warpBeamWasOn = beamOn;

            // Once played, the clip blends out to the idle pose, or stays on its last frame until the beam is gone.
            bool show = beamOn && (!_warpFinished || _warpHoldLastFrame);

            float weightWas = _warpWeight;
            _warpWeight = BeamShape.MoveLevel(_warpWeight, show ? 1f : 0f, deltaTime, _warpBlendSeconds);

            if (_warpWeight <= 0f)
            {
                // Just faded out: take the clip's pose off the model.
                if (weightWas > 0f && _modelAnimator.gameObject.activeInHierarchy)
                {
                    _modelAnimator.SetLayerWeight(_warpLayer, 0f);
                    _modelAnimator.Update(0f);
                    KeepModelRoot();
                    Log("Warp clip ends.");
                }

                // While the clip is off, the idle pose is what is on the model: that is where the kept bone stays.
                if (_warpFixedBone != null && _modelAnimator.gameObject.activeInHierarchy)
                {
                    _warpFixedPosition = _warpFixedBone.localPosition;
                }

                return;
            }

            if (!_modelAnimator.gameObject.activeInHierarchy)
            {
                return;
            }

            if (beamOn && !_warpFinished)
            {
                _warpTime += deltaTime * _warpSpeed;
                if (_warpLength > 0f && _warpTime >= _warpLength)
                {
                    _warpFinished = true;
                    Log("Warp clip has played once" + (_warpHoldLastFrame ? ": holding its last frame until the beam is gone." : ": blending out to the idle pose."));
                }
            }

            // Once the clip is done, or the beam is gone, it stays on the point it reached while it blends out.
            float normalized = _warpLength > 0f ? Mathf.Clamp01(_warpTime / _warpLength) : 0f;

            _modelAnimator.SetLayerWeight(_warpLayer, Mathf.SmoothStep(0f, 1f, _warpWeight));
            _modelAnimator.Play(_warpHash, _warpLayer, normalized);
            _modelAnimator.Update(0f);
            KeepWarpBone();

            // The clip's length is known once its state has been entered.
            if (_warpLength <= 0f)
            {
                _warpLength = ReadWarpLength();
                if (_warpLength <= 0f)
                {
                    _warpLength = 1f;
                    Debug.LogWarning("The warp state '" + _warpStateName + "' reports no usable length (does it have a clip?), so one second is used for its length.", this);
                }
                else
                {
                    Log("Warp clip is " + _warpLength.ToString("0.00") + " s long.");
                }
            }

            KeepModelRoot();
        }

        // Puts the kept bone back where the idle pose has it. The Animator applies itself once more later in the
        // frame, so this is repeated in LateUpdate.
        private void KeepWarpBone()
        {
            if (_warpFixedBone != null)
            {
                _warpFixedBone.localPosition = _warpFixedPosition;
            }
        }

        // Finds a bone by name anywhere under the given object, or null.
        private static Transform FindBone(Transform parent, string boneName)
        {
            foreach (Transform child in parent)
            {
                if (child.name == boneName)
                {
                    return child;
                }

                Transform found = FindBone(child, boneName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        // The length of the clip in the warp layer's current state, in seconds. It is read from the clip itself:
        // the state's own length is divided by the Animator's speed, and that is 0 here, so it comes out infinite.
        // Returns 0 if there is no usable length.
        private float ReadWarpLength()
        {
            AnimatorClipInfo[] clips = _modelAnimator.GetCurrentAnimatorClipInfo(_warpLayer);
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i].clip != null && clips[i].clip.length > 0f)
                {
                    return clips[i].clip.length;
                }
            }

            float stateLength = _modelAnimator.GetCurrentAnimatorStateInfo(_warpLayer).length;
            return float.IsFinite(stateLength) && stateLength > 0f && stateLength < 60f ? stateLength : 0f;
        }

        // Puts the clip in the given Animator state at this point in its run (0 to 1).
        private void ShowClipAt(int stateHash, float normalizedTime)
        {
            // The first time, and again when a clip ends, the model's transforms are written
            // to the Console before and after the clip is applied, to show what the clip changes.
            bool report = _debugLog && (!_introTransformsLogged || normalizedTime >= 1f);
            if (report)
            {
                LogModelTransforms("before the clip at " + normalizedTime.ToString("0.00"));
            }

            _modelAnimator.Play(stateHash, 0, normalizedTime);

            // Applied now and not at the next animation update, so the first frame is never the
            // bind pose.
            _modelAnimator.Update(0f);

            if (report)
            {
                LogModelTransforms("right after the clip at " + normalizedTime.ToString("0.00"));
            }

            KeepModelRoot();

            if (report)
            {
                LogModelTransforms("after putting the model's own object back at " + normalizedTime.ToString("0.00"));
                _introTransformsLogged = true;
            }
        }

        // The clip has curves for the model's own object (the Blender armature: turned -90 degrees and
        // scaled 100), so applying it overwrites the rotation and size set in the scene. The scene's
        // values are put back here. The Animator also applies itself once more later in the frame, so
        // this is repeated in LateUpdate, which runs after that.
        private void KeepModelRoot()
        {
            if (!_animatorPrepared || _modelAnimator == null)
            {
                return;
            }

            Transform root = _modelAnimator.transform;
            root.localPosition = _modelRootPosition;
            root.localRotation = _modelRootRotation;
            root.localScale = _modelRootScale;
        }

        private void LateUpdate()
        {
            // Not only when the intro is usable: a controller with another default state can rewrite
            // the root just the same.
            if (_captured)
            {
                KeepModelRoot();

                if (_warpUsable && _warpWeight > 0f)
                {
                    KeepWarpBone();
                }
            }
        }

        // Debug text: the position, rotation and scale of the object the Animator is on and of its
        // direct children, in the parent's space, with the root's size and turn in the world. If a
        // clip curve overwrites the root or the armature, the numbers change between "before" and "after".
        private void LogModelTransforms(string when)
        {
            Transform root = _modelAnimator.transform;
            var text = new System.Text.StringBuilder(256);
            text.Append("Model transforms ").Append(when).Append(". Root '").Append(root.name).Append("': local pos ")
                .Append(root.localPosition.ToString("F2")).Append(", rot ").Append(root.localEulerAngles.ToString("F0"))
                .Append(", scale ").Append(root.localScale.ToString("F2")).Append(" (world scale ")
                .Append(root.lossyScale.ToString("F2")).Append(").");

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                text.Append(" Child '").Append(child.name).Append("': pos ").Append(child.localPosition.ToString("F2"))
                    .Append(", rot ").Append(child.localEulerAngles.ToString("F0"))
                    .Append(", scale ").Append(child.localScale.ToString("F2")).Append(".");
            }

            Log(text.ToString());
        }

        private void OnShieldShattered(BallColor color)
        {
            _shatterLeft = _shatterSeconds;
            _shatterColor = GetDisplayColor(color);

            if (_debugLog)
            {
                Log("Shield broken by " + color + ": it bursts for " + _shatterSeconds.ToString("0.0#") + " s (grows to x"
                    + _shatterGrowth.ToString("0.0#") + " and fades).");
            }
        }

        private void OnBallDeflected(BallColor color, Vector3 worldPoint)
        {
            _flashLeft = _flashSeconds;
            _flashColor = GetDisplayColor(color);
            _rattleLeft = _rattleSeconds;

            if (_sparks == null)
            {
                if (_debugLog)
                {
                    Log("Wrong color " + color + ": the shield glows " + color + " for " + _flashSeconds.ToString("0.0#") + " s and the model rattles for "
                        + _rattleSeconds.ToString("0.0#") + " s. No sparks: the Sparks field is empty.");
                }

                return;
            }

            // A particle system on an inactive object cannot emit, so switch it on when it is needed.
            if (!_sparks.gameObject.activeSelf)
            {
                _sparks.gameObject.SetActive(true);
            }

            _sparks.transform.position = worldPoint;
            ParticleSystem.MainModule main = _sparks.main;
            main.startColor = _flashColor;
            _sparks.Emit(_sparkCount);

            if (_debugLog)
            {
                Log("Wrong color " + color + ": the shield glows " + color + " for " + _flashSeconds.ToString("0.0#") + " s, the model rattles for "
                    + _rattleSeconds.ToString("0.0#") + " s, and " + _sparkCount + " sparks were emitted at " + worldPoint.ToString("F2")
                    + " (particles alive now: " + _sparks.particleCount + ").");
            }
        }

        private void UpdateShield(MakeyState state)
        {
            if (state == MakeyState.Shielded)
            {
                // A glow in the ball's color fades back into the shield's own color.
                Color shieldColor = GetDisplayColor(_makey.ShieldColor);
                float glow = _flashSeconds > 0f ? _flashLeft / _flashSeconds : 0f;
                Color color = Color.Lerp(shieldColor, _flashColor, glow);
                float alpha = Mathf.Lerp(_shieldAlpha, 0.9f, glow) * UrgencyFlicker();

                // A wrong color makes it flare up for a moment.
                ShowShield(color, alpha, 1f, 1f + FlashGlowBoost * glow);
                return;
            }

            if (_shatterLeft > 0f)
            {
                // Bursting: the shield grows and fades away, glowing brighter as it goes.
                float burst = 1f - _shatterLeft / _shatterSeconds;
                ShowShield(_shatterColor, _shieldAlpha * (1f - burst), Mathf.Lerp(1f, _shatterGrowth, burst), 1f + burst);
                return;
            }

            _shield.enabled = false;
            _shieldShown = false;
        }

        private void ShowShield(Color color, float alpha, float sizeFactor, float glowFactor)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(BaseColorProperty, new Color(color.r, color.g, color.b, alpha));
            _block.SetFloat(GlowProperty, _shieldGlow * glowFactor);
            _shield.SetPropertyBlock(_block);
            _shield.transform.localScale = _shieldScale * sizeFactor;
            _shield.enabled = true;

            _shieldShown = true;
            _shieldDrawnColor = color;
            _shieldDrawnAlpha = alpha;
            _shieldDrawnSize = sizeFactor;
        }

        private void UpdatePivot(MakeyState state)
        {
            Vector3 position = _pivotPosition;
            Quaternion lifeRotation = Quaternion.identity;
            Vector3 lifeScale = Vector3.one;

            if (state != MakeyState.Away)
            {
                position.y += Mathf.Sin(Time.time * 2f * Mathf.PI * _bobsPerSecond) * _bobHeight;
            }

            ApplyLife(state, ref position, ref lifeRotation, ref lifeScale);

            // The lively tilt is applied first, so the daze spin and the head shake turn around the tilted ship.
            Quaternion baseRotation = lifeRotation * _pivotRotation;
            Quaternion rotation = baseRotation;

            if (state == MakeyState.Dazed)
            {
                // Fast at first and slowing to a stop, finishing on a whole number of turns.
                float progress = _makey.StateProgress;
                float eased = 1f - (1f - progress) * (1f - progress);
                rotation = baseRotation * Quaternion.Euler(0f, 360f * _dazeTurns * eased, 0f);
            }
            else if (state == MakeyState.Recovering)
            {
                // Swings from side to side, less and less.
                float progress = _makey.StateProgress;
                float swing = Mathf.Sin(progress * _headShakeCycles * 2f * Mathf.PI) * _headShakeDegrees * (1f - progress);
                rotation = baseRotation * Quaternion.Euler(0f, swing, 0f);
            }

            if (_makey.AbductionPhase == AbductionPhase.Celebrating)
            {
                // Hops and wiggles, ending where it started.
                float progress = _makey.AbductionProgress;
                position.y += Mathf.Abs(Mathf.Sin(progress * _celebrateHops * Mathf.PI)) * _celebrateHopHeight;
                float wiggle = Mathf.Sin(progress * _celebrateHops * 2f * Mathf.PI) * _celebrateWiggleDegrees;
                rotation *= Quaternion.Euler(0f, 0f, wiggle);
            }

            if (_rattleLeft > 0f)
            {
                // A quick tilt from side to side, dying away.
                float elapsed = 1f - _rattleLeft / _rattleSeconds;
                float tilt = Mathf.Sin(elapsed * _rattleCycles * 2f * Mathf.PI) * _rattleDegrees * (1f - elapsed);
                rotation *= Quaternion.Euler(0f, 0f, tilt);
            }

            _modelPivot.localPosition = position;
            _modelPivot.localRotation = rotation;
            _modelPivot.localScale = Vector3.Scale(_pivotScale, lifeScale);
        }

        // The lively motion, all on the pivot and all read from Makey, never written back: he faces the
        // camera and tips towards it, leans into his own movement and stretches with it (springs, so he
        // overshoots and settles when he stops), sways and breathes while he hovers, and shakes harder
        // as a lifted cow nears him. It fades in and out with the state, so it never fights the
        // fly-in and fly-off clips.
        private void ApplyLife(MakeyState state, ref Vector3 position, ref Quaternion rotation, ref Vector3 scale)
        {
            float deltaTime = Time.deltaTime;
            bool active = state == MakeyState.Shielded || state == MakeyState.Dazed || state == MakeyState.Recovering;
            _lifeWeight = Mathf.MoveTowards(_lifeWeight, active ? 1f : 0f, deltaTime * LifeBlendPerSecond);

            // How fast Makey moves. Measured only while the motion is on, so that being put at the
            // hover point when he enters never counts as a dash.
            Vector3 makeyPosition = _makey.transform.position;
            Vector3 velocity = Vector3.zero;
            if (active && _hasLastMakeyPosition && deltaTime > 0f)
            {
                velocity = (makeyPosition - _lastMakeyPosition) / deltaTime;
            }

            _lastMakeyPosition = makeyPosition;
            _hasLastMakeyPosition = active;

            if (!active && _lifeWeight <= 0f)
            {
                _rollSpring.Reset();
                _pitchSpring.Reset();
                _stretchSpring.Reset();
                return;
            }

            // The directions the players see: right and away from the camera, along the ground.
            Vector3 right = Vector3.right;
            Vector3 forward = Vector3.forward;
            float yaw = 0f;
            float tilt = 0f;
            Camera viewCamera = _makey.ViewCamera;
            if (viewCamera != null)
            {
                Vector3 flatForward = viewCamera.transform.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude > 1e-4f)
                {
                    forward = flatForward.normalized;
                    right = Vector3.Cross(Vector3.up, forward);
                }

                yaw = MakeyMotion.FacingYawDegrees(viewCamera.transform.position, viewCamera.transform.forward, makeyPosition) * _facingWeight;
                tilt = _cockpitTiltDegrees;
            }

            float rightSpeed = Mathf.Clamp(Vector3.Dot(velocity, right) / _leanFullSpeed, -1f, 1f);
            float forwardSpeed = Mathf.Clamp(Vector3.Dot(velocity, forward) / _leanFullSpeed, -1f, 1f);
            float speed = Mathf.Clamp01(velocity.magnitude / _leanFullSpeed);

            // The top leans the way he moves: to the right is a turn the opposite way around the view axis.
            _rollSpring.Step(-rightSpeed * _leanMaxDegrees, _leanStiffness, _leanDamping, deltaTime);
            _pitchSpring.Step(forwardSpeed * _leanMaxDegrees * 0.6f, _leanStiffness, _leanDamping, deltaTime);
            _stretchSpring.Step(speed * _stretchAmount, _leanStiffness, _leanDamping, deltaTime);

            float time = Time.time;
            Transform parent = _modelPivot.parent;
            Vector3 sway = parent != null ? parent.InverseTransformDirection(right) : right;
            sway *= Mathf.Sin(time * 2f * Mathf.PI * _swaysPerSecond) * _swayDistance;
            float breath = 1f + Mathf.Sin(time * 2f * Mathf.PI * _breathsPerSecond) * _breathAmount;

            // The strain grows with the square of the lift, so it is faint for a while and then builds.
            float shakeRoll = 0f;
            float shakePitch = 0f;
            Vector3 jitter = Vector3.zero;
            if (state == MakeyState.Shielded && _makey.AbductionPhase == AbductionPhase.Lifting)
            {
                float strain = _makey.AbductionProgress * _makey.AbductionProgress;
                float noiseTime = time * _strainShakeSpeed;
                shakeRoll = SignedNoise(noiseTime, 0f) * _strainShakeDegrees * strain;
                shakePitch = SignedNoise(noiseTime, StrainNoiseSeed) * _strainShakeDegrees * strain;
                jitter = new Vector3(SignedNoise(noiseTime, 31.7f), SignedNoise(noiseTime, 47.1f), 0f) * (_strainShakeDistance * strain);
            }

            // Turn to face the camera, tip the top towards it (up is a turn away from it), lean, shake.
            Quaternion world = Quaternion.AngleAxis(_rollSpring.Value + shakeRoll, forward)
                * Quaternion.AngleAxis(_pitchSpring.Value + shakePitch - tilt, right)
                * Quaternion.AngleAxis(yaw, Vector3.up);
            Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
            Quaternion local = Quaternion.Inverse(parentRotation) * world * parentRotation;

            float stretch = _stretchSpring.Value;
            Vector3 lifeScale = new Vector3(1f - stretch * 0.5f, 1f + stretch, 1f - stretch * 0.5f) * breath;

            rotation = Quaternion.Slerp(Quaternion.identity, local, _lifeWeight);
            position += (sway + jitter) * _lifeWeight;
            scale = Vector3.Lerp(Vector3.one, lifeScale, _lifeWeight);
        }

        // Smooth random noise between -1 and 1, so the strain shake never repeats exactly.
        private static float SignedNoise(float x, float seed)
        {
            return (Mathf.PerlinNoise(x, seed) - 0.5f) * 2f;
        }

        // While a cow is being lifted and is nearly there, the shield flickers, faster and deeper as
        // it gets closer, which tells the players time is running out. 1 means no flicker.
        private float UrgencyFlicker()
        {
            if (_makey.AbductionPhase != AbductionPhase.Lifting || _urgencyStart >= 1f)
            {
                return 1f;
            }

            float urgency = Mathf.InverseLerp(_urgencyStart, 1f, _makey.AbductionProgress);
            if (urgency <= 0f)
            {
                return 1f;
            }

            float rate = Mathf.Lerp(4f, _flickerMaxPerSecond, urgency);
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 2f * Mathf.PI * rate);
            return 1f - _flickerDepth * urgency * wave;
        }

        private void UpdateDazeEffect(MakeyState state)
        {
            if (_dazeEffect == null)
            {
                return;
            }

            bool dazed = state == MakeyState.Dazed;
            if (dazed != _dazeShown)
            {
                _dazeShown = dazed;
                _dazeEffect.SetActive(dazed);
            }

            if (dazed)
            {
                // The stars circle around Makey. The ring itself is laid out in the scene.
                float angle = Time.time * _starSpinDegreesPerSecond;
                _dazeEffect.transform.localRotation = _dazeEffectRotation * Quaternion.Euler(0f, angle, 0f);
            }
        }

        // Flags a setup that would leave the shield invisible or wrong. The warnings always show;
        // the summary is part of the debug log.
        private void CheckWiring()
        {
            Material material = _shield.sharedMaterial;
            string shaderName = material != null && material.shader != null ? material.shader.name : "none";

            if (material == null)
            {
                Debug.LogWarning("The shield renderer '" + _shield.name + "' has no material, so it will draw nothing.", this);
            }
            else if (!material.HasProperty(BaseColorProperty))
            {
                Debug.LogWarning("The shield material '" + material.name + "' (shader " + shaderName
                    + ") has no _BaseColor, so the tint will not show. Use a URP Lit material.", this);
            }
            else if (material.HasProperty("_Surface") && material.GetFloat("_Surface") < 0.5f)
            {
                Debug.LogWarning("The shield material '" + material.name + "' is Opaque, so the shield will be a solid ball. Set its Surface Type to Transparent.", this);
            }

            if (material != null && !material.HasProperty(GlowProperty))
            {
                Debug.LogWarning("The shield material '" + material.name + "' (shader " + shaderName
                    + ") has no " + GlowProperty + ", so the shield will not glow. Give the material the shader 'PlanetMakey/Shield'.", this);
            }

            if (!_shield.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("The shield object '" + _shield.name + "' is inactive, so it can never show.", this);
            }

            if (_shield.GetComponent<Collider>() != null)
            {
                Debug.LogWarning("The shield object '" + _shield.name + "' has a collider. Remove it, or balls meant for Makey may stop at the shield.", this);
            }

            if (_makey.ViewCamera == null)
            {
                Debug.LogWarning("The Makey component has no camera wired, so Makey will not turn to face the camera or tip towards it. Wire the camera on the Makey component.", this);
            }

            CheckDazeEffect();
            CheckSparks();

            if (!_debugLog)
            {
                return;
            }

            Log("Enabled. Watching " + _makey.name + ". Model pivot '" + _modelPivot.name + "' at local " + _pivotPosition.ToString("F2")
                + ". Shield '" + _shield.name + "', material " + (material != null ? material.name : "none") + ", shader " + shaderName
                + ", scale " + _shieldScale.ToString("F2") + ". Palette " + (_palette != null ? _palette.name : "MISSING")
                + ". Daze stars " + (_dazeEffect != null ? _dazeEffect.name : "not set (no stars)")
                + ". Sparks " + (_sparks != null ? _sparks.name : "not set (no sparks)") + ".");
        }

        // The game switches only the Daze Effect object on. Its children have to be on already,
        // and spread out, or nothing is seen.
        private void CheckDazeEffect()
        {
            if (_dazeEffect == null)
            {
                return;
            }

            Transform root = _dazeEffect.transform;
            int count = root.childCount;

            if (count == 0)
            {
                if (_dazeEffect.GetComponentInChildren<Renderer>(true) == null)
                {
                    Debug.LogWarning("The daze effect '" + _dazeEffect.name + "' has no children and no renderer, so there is nothing to show. Add a few small stars under it.", this);
                }

                return;
            }

            int active = 0;
            float spread = 0f;
            Vector3 first = root.GetChild(0).localPosition;
            for (int i = 0; i < count; i++)
            {
                Transform child = root.GetChild(i);
                if (child.gameObject.activeSelf)
                {
                    active++;
                }

                spread = Mathf.Max(spread, Vector3.Distance(child.localPosition, first));
            }

            if (active == 0)
            {
                Debug.LogWarning("All " + count + " children of the daze effect '" + _dazeEffect.name
                    + "' are switched off, and only the '" + _dazeEffect.name + "' object itself is switched on, so nothing will show. Tick the active box on the stars.", this);
            }

            bool oneStarAtCenter = count == 1 && first.sqrMagnitude < 0.0001f;
            if ((count > 1 && spread < 0.01f) || oneStarAtCenter)
            {
                Debug.LogWarning("The stars under '" + _dazeEffect.name + "' sit at the same spot (or the only one is at the center), so they will not circle. Spread them in a ring around Makey's head.", this);
            }
        }

        private void CheckSparks()
        {
            if (_sparks == null)
            {
                return;
            }

            ParticleSystemRenderer particleRenderer = _sparks.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer == null || particleRenderer.sharedMaterial == null)
            {
                Debug.LogWarning("The sparks particle system '" + _sparks.name + "' has no material on its renderer, so the particles will not draw. Give it a URP particle material.", this);
            }

            if (!_sparks.gameObject.activeSelf)
            {
                Log("The sparks object '" + _sparks.name + "' is switched off. It is switched on automatically at the first wrong-color hit.");
            }
            else if (!_sparks.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("The sparks object '" + _sparks.name + "' is switched on, but something above it is switched off, so it can never emit.", this);
            }
        }

        // Says what the view is doing, once for every state it sees and then once a second.
        private void LogState(MakeyState state)
        {
            if (!_debugLog)
            {
                return;
            }

            if (!_hasLoggedState || state != _lastLoggedState)
            {
                _hasLoggedState = true;
                _lastLoggedState = state;
                Log("Sees Makey " + state + ". " + DescribeLook(state));
            }

            if (Time.unscaledTime < _nextStatusLogTime)
            {
                return;
            }

            _nextStatusLogTime = Time.unscaledTime + StatusLogInterval;
            Log("Status: " + state + ". Shield " + (_shieldShown
                    ? "shown, color " + _shieldDrawnColor.ToString("F2") + ", alpha " + _shieldDrawnAlpha.ToString("0.00") + ", size x" + _shieldDrawnSize.ToString("0.00")
                    : "hidden")
                + " (renderer enabled " + _shield.enabled + "). Pivot at local " + _modelPivot.localPosition.ToString("F2")
                + ", turned " + _modelPivot.localEulerAngles.ToString("F0")
                + ". Effects left: burst " + _shatterLeft.ToString("0.00") + " s, glow " + _flashLeft.ToString("0.00")
                + " s, rattle " + _rattleLeft.ToString("0.00") + " s.");
        }

        private string DescribeLook(MakeyState state)
        {
            switch (state)
            {
                case MakeyState.Away:
                    return "Shield hidden, model not bobbing.";
                case MakeyState.Entering:
                    return "Shield hidden while he flies in; the model bobs.";
                case MakeyState.Shielded:
                    return "Showing a " + _makey.ShieldColor + " shield.";
                case MakeyState.Dazed:
                    return "Shield hidden (unless still bursting); the model spins " + _dazeTurns.ToString("0.#") + " turns"
                        + (_dazeEffect != null ? " and the stars are on." : " (no stars set).");
                case MakeyState.Recovering:
                    return "Shield hidden; the head swings side to side " + _headShakeCycles.ToString("0.#") + " times.";
                case MakeyState.Leaving:
                    return "Shield hidden; he flies off" + (_escapeUsable ? " with the escape clip." : " (no escape clip: he keeps his idle pose until hidden).");
                default:
                    return string.Empty;
            }
        }

        private void Log(string message)
        {
            if (_debugLog)
            {
                Debug.Log("[MakeyView] " + message, this);
            }
        }

        private Color GetDisplayColor(BallColor ballColor)
        {
            if (_palette == null)
            {
                return Color.white;
            }

            return _palette.GetColor(ballColor);
        }
    }
}
