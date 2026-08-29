namespace BrilliantQuesting.Events
{
    /// <summary>
    /// The vocabulary of things that can happen. Kept small and causal on purpose: these are the
    /// verbs history is written in, and every one of them has to be something another subsystem
    /// can react to. "QuestCompleted" is deliberately absent - a quest finishing is not an event
    /// in the world, it is a projection of one.
    /// </summary>
    public enum WorldEventType
    {
        Met,
        Conversed,
        Helped,
        Harmed,
        Threatened,
        Bribed,
        Deceived,
        DeceptionExposed,
        PromiseMade,
        PromiseBroken,
        Theft,
        ItemReturned,
        ItemGiven,
        Trespass,
        Attacked,
        Killed,
        Rescued,
        Captured,
        DebtCreated,
        DebtPaid,
        SecretLearned,
        SecretRevealed,
        /// <summary>
        /// A claim made to somebody with standing to act on it. Says nothing about whether the
        /// claim is true - only that it was put on the record.
        /// </summary>
        AccusationMade,

        /// <summary>An authority declined to act, because the claim could not be backed up.</summary>
        AccusationRejected,

        /// <summary>An authority took the claim seriously enough to look into it.</summary>
        InquiryOpened,

        /// <summary>
        /// An accusation that is actually untrue - it contradicts the fact it names, or the
        /// accuser knew better.
        ///
        /// Reserved for that. It used to be recorded whenever an authority refused to act on an
        /// unprovable claim, which meant a player who correctly identified a thief and simply
        /// could not prove it left a record saying they had lied. Provability is about what can
        /// be demonstrated; truth is about what happened. Conflating them poisons memories,
        /// reputation and the Chronicle, and rumour circulation would then distribute the error.
        /// </summary>
        FalseAccusation,
        EvidenceCreated,
        EvidenceDestroyed,

        /// <summary>
        /// The game finished making something, and the simulation noticed.
        ///
        /// Recorded from observation rather than from any procedural roll: Elin's own cooking,
        /// brewing and building decide what came out and how good it is, and the ledger's job is
        /// to remember who made it. Carries the object as evidence, so provenance stays attached
        /// to a thing somebody can be shown.
        /// </summary>
        GoodsProduced,

        /// <summary>
        /// Real goods were laid on a god's ground and are gone.
        ///
        /// The cost side of the faith routes, and the reason they are not a free menu entry: a
        /// petition is paid for in things the player could have eaten, sold or given to somebody
        /// who would have thanked them for it. Target is nobody - a god is not a character the
        /// simulation holds standing with - so what the ledger records is the act and its price.
        /// </summary>
        OfferingMade,

        /// <summary>
        /// The player's Home took somebody, or something, into its keeping.
        ///
        /// One event for the whole family because it is one causal shape: a household accepted an
        /// exposure it did not have before. What was undertaken - a bed, a guest's night, a watch
        /// posted, an object held - is the fact written beside it, not a separate kind of history,
        /// and the target is a person or an object depending on which. Nobody's standing is
        /// *judged* here: taking a hunted witness in and taking a blacksmith on are the same act
        /// to the ledger, and what the law makes of it is decided where the law is (BQ-046).
        /// </summary>
        TakenIn,
        CrimeWitnessed,
        CrimeReported,
        RumorSpread,

        /// <summary>
        /// A retelling that changed the story. Distinct from `RumorSpread` because the moment a
        /// tale mutates is a causal event in its own right: it is where a false belief entered
        /// the world, and without it in the ledger an accusation against an innocent has no
        /// traceable origin. Carries the garbled fact first and the true one second.
        /// </summary>
        RumorDistorted,
        /// <summary>
        /// Somebody stopped being available - their trade shut, or they left town.
        ///
        /// One event for both grades, because the causal shape is the same: the world lost
        /// something it had, and everything that depended on it has to find another route. Which
        /// grade it was is on the absence record, not in a second name here.
        /// </summary>
        WentAbsent,

        /// <summary>They are back, and whatever they do they are doing again.</summary>
        Returned,
        Recruited,
        OrganizationJoined,
        OrganizationBetrayed,
        SiteDiscovered,
        SiteCleared,
        ThreadEscalated,
        ThreadResolved
    }
}
