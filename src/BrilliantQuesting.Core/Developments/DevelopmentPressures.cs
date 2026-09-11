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

        /// <summary>
        /// A wrong the world holds as true and nothing records as ended. Not "somebody suspects":
        /// that is <see cref="UnprovenKnowledge"/>, and the two come apart in both directions - a
        /// theft everyone saw is unresolved with nothing to prove, and a theft the thief has
        /// already made good on can still be a secret somebody cannot demonstrate.
        /// </summary>
        public const string UnresolvedCrime = "unresolved_crime";

        /// <summary>
        /// Something is broken, blighted or spoiled and has not been put right. Kept apart from
        /// <see cref="UnresolvedCrime"/> because most damage is nobody's crime: a mill wheel and a
        /// blighted field press on the people who depend on them either way.
        /// </summary>
        public const string DamagedProperty = "damaged_property";

        /// <summary>A place or a person is short of something, in the coarse categories BQ-050
        /// tracks. Never a commodity balance and never inferred from what a town ate today.</summary>
        public const string Shortage = "shortage";

        /// <summary>
        /// A tracked business is not serving as it should. Durable continuity meaning only: an
        /// operator who is asleep, at a hobby or off-shift is the working day, not a pressure, and
        /// BQ-051 keeps those apart precisely so this tag cannot be minted from a nap.
        /// </summary>
        public const string ServiceInterruption = "service_interruption";

        /// <summary>An organization is carrying an unsatisfied goal of its own.</summary>
        public const string OrganizationStake = "organization_stake";

        /// <summary>
        /// Two claims about the same matter stand in the graph, one of them false, and somebody
        /// holds the false one. The world holding a contradiction is objective; which actor is
        /// wrong about what is a reading BQa-007 owns and this tag must not pre-empt.
        /// </summary>
        public const string EvidenceConflict = "evidence_conflict";

        /// <summary>
        /// Modifier: this condition is something to gain by rather than only something to lose to.
        /// A world whose only readings are crises generates only crises, and the detector would be
        /// telling the truth every time - which is how a living world quietly becomes a disaster
        /// feed.
        /// </summary>
        public const string Opportunity = "opportunity";

        /// <summary>
        /// Modifier: the condition already has an answer in place and is easing. Still a reason to
        /// act - a new operator needs custom, an inherited counter needs standing - and the
        /// quietest thing the detector is allowed to say while still saying something.
        /// </summary>
        public const string Recovering = "recovering";
    }
}
