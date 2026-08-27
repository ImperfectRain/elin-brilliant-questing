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
