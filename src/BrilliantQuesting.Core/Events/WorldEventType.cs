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
        CrimeWitnessed,
        CrimeReported,
        RumorSpread,
        Recruited,
        OrganizationJoined,
        OrganizationBetrayed,
        SiteDiscovered,
        SiteCleared,
        ThreadEscalated,
        ThreadResolved
    }
}
