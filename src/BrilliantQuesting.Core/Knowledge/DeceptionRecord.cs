using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// The durable trace of somebody having lied about something (BQ-020, BQ-073).
    ///
    /// One primitive, shared by the two places a deliberate falsehood can come from: a rumour
    /// deliberately seeded (<c>RumorSystem.Lie</c>) and a lie told in a conversation
    /// (<c>Dialogue.Deception</c>). Sharing it is the point rather than a tidiness: the world must
    /// hold one answer to "has Kip lied about the theft", not one per subsystem that noticed, and
    /// anything that can later expose the one exposes the other for free.
    ///
    /// The trace is a fact, not a flag on a character. `X lied_about F` is true of the world, it
    /// is kept at high secrecy because only the liar knows it, and it lives beside the claim it is
    /// about instead of inside it - so the graph can hold "Kip took the ring" and "Mira lied about
    /// Kip taking the ring" at once without either being an annotation on the other.
    ///
    /// It says nothing about *what* was said instead. That is the statement's own record - the
    /// event the caller writes - and keeping the two apart is why a person who tells one lie to
    /// six people has lied about one thing and told six statements.
    /// </summary>
    public static class DeceptionRecord
    {
        /// <summary>
        /// Writes down that this speaker has lied about this claim, once.
        ///
        /// Idempotent per speaker and claim: a person who repeats the same lie all week has lied
        /// about one thing, and a fact per telling would make the graph a transcript. Returns the
        /// record either way, so a caller can link a statement to it.
        ///
        /// The liar is taught their own act at full conviction from <see cref="KnowledgeSource.Participant"/>,
        /// because they were there. Nobody else is taught anything, which is the whole of what
        /// makes the lie worth catching later.
        /// </summary>
        public static Fact Of(KnowledgeGraph knowledge, IdMinter ids, EntityId liar, EntityId aboutFactId, GameTime now)
        {
            if (knowledge == null || ids == null || liar.IsNone || aboutFactId.IsNone)
            {
                return null;
            }

            foreach (Fact existing in knowledge.Facts.Values)
            {
                if (existing.Subject == liar
                    && existing.Predicate == FactPredicates.LiedAbout
                    && existing.Object == aboutFactId)
                {
                    return existing;
                }
            }

            Fact lie = new Fact(ids.Next("fact"), liar, FactPredicates.LiedAbout, aboutFactId, secrecy: 90);
            knowledge.AddFact(lie);
            knowledge.Teach(liar, lie.Id, KnowledgeSource.Participant, 1.0, now, false);
            return lie;
        }
    }
}
