using UnityEngine;

namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// The timings that set how hard Makey is to beat. They live in an asset so they can be
    /// tuned at the booth without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "MakeyConfig", menuName = "Planet Makey/Makey Config")]
    public sealed class MakeyConfig : ScriptableObject
    {
        [Tooltip("Seconds Makey takes to fly in. Balls do not count until he arrives.")]
        [Min(0f)]
        [SerializeField] private float _enterSeconds = 1.5f;

        [Tooltip("Seconds Makey stays dazed after his shield breaks. Balls do not count.")]
        [Min(0f)]
        [SerializeField] private float _dazeSeconds = 3f;

        [Tooltip("Seconds Makey spends shaking his head clear before a new shield goes up. Balls do not count.")]
        [Min(0f)]
        [SerializeField] private float _recoverSeconds = 1f;

        [Tooltip("Seconds Makey takes to fly off screen when the round ends. Match the length of the Escape animation clip (about 4.1). Balls do not count.")]
        [Min(0f)]
        [SerializeField] private float _leaveSeconds = 4.1f;

        [Header("Abduction")]
        [Tooltip("Seconds Makey takes to glide over the cow he picked.")]
        [Min(0f)]
        [SerializeField] private float _approachSeconds = 1.2f;

        [Tooltip("Seconds the beam takes to reach down to the cow.")]
        [Min(0f)]
        [SerializeField] private float _beamSeconds = 1f;

        [Tooltip("Seconds the cow takes to rise to the ship. This is how long the player has to break the shield, so it sets how hard the game is.")]
        [Min(0f)]
        [SerializeField] private float _liftSeconds = 6f;

        [Tooltip("Seconds Makey celebrates after absorbing a cow, before he picks the next one.")]
        [Min(0f)]
        [SerializeField] private float _celebrateSeconds = 1.5f;

        [Header("Hover height")]
        [Tooltip("Lowest height, in world units above the cow's resting position, that Makey hovers at over a cow. He picks a random height between this and the maximum each time he picks a cow.")]
        [Min(0f)]
        [SerializeField] private float _hoverHeightMin = 1f;

        [Tooltip("Highest height, in world units above the cow's resting position, that Makey hovers at over a cow.")]
        [Min(0f)]
        [SerializeField] private float _hoverHeightMax = 3f;

        [Tooltip("How much of the hover height range (the maximum minus the minimum) Makey may use over a cow that is at or nearer than Near Distance. 1 uses all of it, 0.5 half. The same height looks higher on screen over a near cow, so a smaller value keeps him from going too high there. The minimum height is never reduced.")]
        [Range(0f, 1f)]
        [SerializeField] private float _nearHeightScale = 0.5f;

        [Tooltip("Distance from the camera, in world units, at or below which Near Height Scale applies in full. Measured along the camera's view direction to the cow's resting position.")]
        [Min(0f)]
        [SerializeField] private float _nearDistance = 8f;

        [Tooltip("Distance from the camera at or beyond which Makey may use the whole hover height range. Between Near Distance and this, the share of the range grows steadily.")]
        [Min(0f)]
        [SerializeField] private float _farDistance = 14f;

        [Tooltip("Lowest screen height Makey's center may be at, from 0 (bottom of the picture) to 1 (top). A hover height that would put him lower is raised to this.")]
        [Range(0f, 1f)]
        [SerializeField] private float _flightBandMinY = 0.4f;

        [Tooltip("Highest screen height Makey's center may be at. Keeps him on screen and clear of the HUD at the top. A hover height that would put him higher is lowered to this.")]
        [Range(0f, 1f)]
        [SerializeField] private float _flightBandMaxY = 0.85f;

        /// <summary>Seconds spent flying in.</summary>
        public float EnterSeconds => _enterSeconds;

        /// <summary>Seconds spent dazed after the shield breaks.</summary>
        public float DazeSeconds => _dazeSeconds;

        /// <summary>Seconds spent recovering before the next shield.</summary>
        public float RecoverSeconds => _recoverSeconds;

        /// <summary>Seconds spent flying off screen at the end of a round.</summary>
        public float LeaveSeconds => _leaveSeconds;

        /// <summary>Seconds spent gliding over the cow.</summary>
        public float ApproachSeconds => _approachSeconds;

        /// <summary>Seconds the beam takes to reach the cow.</summary>
        public float BeamSeconds => _beamSeconds;

        /// <summary>Seconds the cow takes to rise: the player's time.</summary>
        public float LiftSeconds => _liftSeconds;

        /// <summary>Seconds spent celebrating an absorbed cow.</summary>
        public float CelebrateSeconds => _celebrateSeconds;

        /// <summary>Lowest hover height above a cow's resting position, in world units.</summary>
        public float HoverHeightMin => _hoverHeightMin;

        /// <summary>Highest hover height above a cow's resting position, in world units.</summary>
        public float HoverHeightMax => _hoverHeightMax;

        /// <summary>Share of the hover height range used over a near cow, 0 to 1.</summary>
        public float NearHeightScale => _nearHeightScale;

        /// <summary>Camera distance at or below which the near share applies in full.</summary>
        public float NearDistance => _nearDistance;

        /// <summary>Camera distance at or beyond which the whole hover height range is used.</summary>
        public float FarDistance => _farDistance;

        /// <summary>Lowest screen height of Makey's center, 0 to 1.</summary>
        public float FlightBandMinY => _flightBandMinY;

        /// <summary>Highest screen height of Makey's center, 0 to 1.</summary>
        public float FlightBandMaxY => _flightBandMaxY;

        // Keeps each range the right way round when one end is edited in the Inspector.
        private void OnValidate()
        {
            if (_hoverHeightMax < _hoverHeightMin)
            {
                _hoverHeightMax = _hoverHeightMin;
            }

            if (_farDistance < _nearDistance)
            {
                _farDistance = _nearDistance;
            }

            if (_flightBandMaxY < _flightBandMinY)
            {
                _flightBandMaxY = _flightBandMinY;
            }
        }
    }
}
