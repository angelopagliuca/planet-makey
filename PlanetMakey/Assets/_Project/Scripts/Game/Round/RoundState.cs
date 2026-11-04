namespace PlanetMakey.Game.Round
{
    /// <summary>Where a round is. A round runs Idle, Dusk, Night, Dawn, and then Idle again.</summary>
    public enum RoundState
    {
        /// <summary>Between rounds. Waiting for an operator to start one.</summary>
        Idle,

        /// <summary>The cows are back and night is falling. Makey is not here yet.</summary>
        Dusk,

        /// <summary>Makey is abducting cows and the players defend them. The clock runs.</summary>
        Night,

        /// <summary>The night is over: the sun comes up, Makey leaves and the score shows.</summary>
        Dawn
    }
}
