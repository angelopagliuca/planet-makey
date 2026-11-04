using UnityEngine;

namespace PlanetMakey.Game.Round
{
    /// <summary>
    /// How long each part of a round lasts. They live in an asset so they can be tuned at the
    /// booth without touching code.
    /// </summary>
    /// <remarks>
    /// The number of cows is not here: it is the number of cow objects in the <c>CowHerd</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "RoundConfig", menuName = "Planet Makey/Round Config")]
    public sealed class RoundConfig : ScriptableObject
    {
        [Tooltip("Seconds of dusk before Makey arrives. The cows are back and the light fades.")]
        [Min(0f)]
        [SerializeField] private float _duskSeconds = 4f;

        [Tooltip("Seconds of night: the time the players have to defend the herd. The brief says 3 minutes.")]
        [Min(0f)]
        [SerializeField] private float _nightSeconds = 180f;

        [Tooltip("Seconds of dawn, with the score on screen, before the game goes back to idle. Keep it longer than Makey's Leave Seconds, or he is still leaving when the next round starts.")]
        [Min(0f)]
        [SerializeField] private float _dawnSeconds = 8f;

        /// <summary>Seconds from the start request until Makey arrives.</summary>
        public float DuskSeconds => _duskSeconds;

        /// <summary>Seconds the players have to defend the herd.</summary>
        public float NightSeconds => _nightSeconds;

        /// <summary>Seconds the score is shown before the game returns to idle.</summary>
        public float DawnSeconds => _dawnSeconds;
    }
}
