namespace PlanetMakey.Game.Cows
{
    /// <summary>
    /// Where a cow stands in the abduction.
    /// </summary>
    public enum CowState
    {
        /// <summary>Safe in the herd, free to be picked.</summary>
        Free,

        /// <summary>Picked by Makey. It is still in the herd until it is absorbed.</summary>
        Targeted,

        /// <summary>Absorbed by Makey and lost for the round.</summary>
        Gone
    }
}
