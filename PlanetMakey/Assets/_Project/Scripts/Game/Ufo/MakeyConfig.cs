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

        /// <summary>Seconds spent flying in.</summary>
        public float EnterSeconds => _enterSeconds;

        /// <summary>Seconds spent dazed after the shield breaks.</summary>
        public float DazeSeconds => _dazeSeconds;

        /// <summary>Seconds spent recovering before the next shield.</summary>
        public float RecoverSeconds => _recoverSeconds;
    }
}
