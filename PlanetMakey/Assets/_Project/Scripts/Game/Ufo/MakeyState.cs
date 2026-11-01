namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// What Makey is doing. The shield can only be broken in <see cref="Shielded"/>.
    /// </summary>
    public enum MakeyState
    {
        /// <summary>Parked off screen, not in play.</summary>
        Away,

        /// <summary>Flying in. The shield is not up yet.</summary>
        Entering,

        /// <summary>Hovering with a shield of one color.</summary>
        Shielded,

        /// <summary>The shield just broke. Makey spins and cannot be hurt.</summary>
        Dazed,

        /// <summary>Shaking his head clear. Still cannot be hurt.</summary>
        Recovering
    }
}
