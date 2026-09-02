namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// Controlled vocabulary for what kind of unresolved pressure a <see cref="Development"/> is.
    ///
    /// Tags, not an enum, for the same reason <c>EventTags</c> and <c>FactPredicates</c> are: a
    /// later detection rule adds a word without reshaping every consumer's switch. The list is
    /// deliberately short. A tag earns its place by being something a composer would sort or
    /// filter on; a tag that only restates the rule that produced it is noise.
    /// </summary>
    public static class DevelopmentPressures
    {
        /// <summary>
        /// Somebody believes something true they cannot demonstrate. The gap between believing and
        /// proving is what makes accusation, corroboration and blackmail worth staging at all.
        /// </summary>
        public const string UnprovenKnowledge = "unproven_knowledge";

        /// <summary>
        /// The person the belief is about knows it is believed. They have something to lose and
        /// know it, which is a different scene from one where only the accuser is aware.
        /// </summary>
        public const string Contested = "contested";

        /// <summary>A social debt nobody has settled, forgiven or broken.</summary>
        public const string UnmetObligation = "unmet_obligation";

        /// <summary>
        /// The obligation runs against its subject rather than toward them - a grudge is owed at
        /// somebody, not to them.
        /// </summary>
        public const string Adversarial = "adversarial";
    }
}
