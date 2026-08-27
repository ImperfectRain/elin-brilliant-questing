namespace BrilliantQuesting.Events
{
    /// <summary>Controlled tags that change how the consequence layer reads an event.</summary>
    public static class EventTags
    {
        /// <summary>
        /// It happened, it is in history, and nobody in the world noticed.
        ///
        /// This matters more than it looks: a clean theft must not move the victim's affinity,
        /// because affinity moving is itself information. Without this tag, perfect stealth would
        /// quietly tell the target they had been robbed.
        /// </summary>
        public const string Unnoticed = "unnoticed";
    }
}
