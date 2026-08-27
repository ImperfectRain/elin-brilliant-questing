namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Solution families. The generation-side rule from the design document is that a major
    /// situation should expose at least three of these and usually more, so that a build with no
    /// Charisma is never locked out of a problem - it just solves it by another route.
    /// </summary>
    public enum ActionFamily
    {
        Social,
        Information,
        Crime,
        Economic,
        Physical,
        Crafting,
        MagicFaith,
        HomeCommunity
    }
}
