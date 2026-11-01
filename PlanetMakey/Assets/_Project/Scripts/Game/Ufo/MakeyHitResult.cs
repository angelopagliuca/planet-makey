namespace PlanetMakey.Game.Ufo
{
    /// <summary>
    /// What a ball did to Makey.
    /// </summary>
    public enum MakeyHitResult
    {
        /// <summary>Nothing: the shield is not up, so the ball does not count.</summary>
        Ignored,

        /// <summary>The ball matched the shield's color and broke it.</summary>
        Shattered,

        /// <summary>The ball was the wrong color and bounced off the shield.</summary>
        Deflected
    }
}
