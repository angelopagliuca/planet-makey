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

        /// <summary>Seconds spent flying in.</summary>
        public float EnterSeconds => _enterSeconds;

        /// <summary>Seconds spent dazed after the shield breaks.</summary>
        public float DazeSeconds => _dazeSeconds;

        /// <summary>Seconds spent recovering before the next shield.</summary>
        public float RecoverSeconds => _recoverSeconds;

        /// <summary>Seconds spent gliding over the cow.</summary>
        public float ApproachSeconds => _approachSeconds;

        /// <summary>Seconds the beam takes to reach the cow.</summary>
        public float BeamSeconds => _beamSeconds;

        /// <summary>Seconds the cow takes to rise: the player's time.</summary>
        public float LiftSeconds => _liftSeconds;

        /// <summary>Seconds spent celebrating an absorbed cow.</summary>
        public float CelebrateSeconds => _celebrateSeconds;
    }
}
