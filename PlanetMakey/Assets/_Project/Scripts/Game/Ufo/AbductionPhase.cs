namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// Where Makey is in an attempt to abduct a cow.
    /// </summary>
    public enum AbductionPhase
    {
        /// <summary>No attempt is running.</summary>
        Idle,

        /// <summary>Gliding over the cow he picked.</summary>
        Approaching,

        /// <summary>The beam is reaching down to the cow.</summary>
        LoweringBeam,

        /// <summary>The cow is rising to the ship. The player's time is running out.</summary>
        Lifting,

        /// <summary>The cow is absorbed and Makey celebrates.</summary>
        Celebrating
    }
}
