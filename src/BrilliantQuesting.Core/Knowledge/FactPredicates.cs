namespace BrilliantQuesting.Knowledge
{
    /// <summary>
    /// The controlled predicate ontology. Generators, actions and dialogue all have to speak the
    /// same vocabulary or the graph stops being queryable, so new predicates belong here rather
    /// than as ad-hoc strings at call sites.
    /// </summary>
    public static class FactPredicates
    {
        public const string Stole = "stole";
        public const string Killed = "killed";
        public const string Owes = "owes";
        public const string Hired = "hired";
        public const string Funds = "funds";
        public const string MemberOf = "member_of";
        public const string LocatedAt = "located_at";
        public const string Possesses = "possesses";
        public const string LiedAbout = "lied_about";
        public const string RelatedTo = "related_to";
        public const string IsDead = "is_dead";
        public const string Witnessed = "witnessed";

        /// <summary>
        /// What actually killed somebody, in <see cref="Fact.Value"/>: a blade, a fall, a poison.
        ///
        /// Separate from <see cref="IsDead"/> because the two are known by different people for
        /// different reasons. That a person is dead is public the moment the body is found; what
        /// killed them is a reading of the body, and a poisoner's whole plan is that the second
        /// one never gets made.
        /// </summary>
        public const string KilledBy = "killed_by";
        public const string Investigating = "investigating";

        /// <summary>
        /// Somebody made a document say a thing it was never party to.
        ///
        /// Subject is whoever did the work, object is the paper. It is a fact in its own right and
        /// a true one, which is the whole point: a forgery that only existed as a flag on the
        /// document it produced could never be found out, and the thing that makes forging
        /// interesting is that it can be.
        /// </summary>
        public const string Forged = "forged";

        /// <summary>One person is squeezing another over something they would rather stayed quiet.</summary>
        public const string Extorted = "extorted";

        /// <summary>
        /// Somebody wants goods they cannot get, described by what the goods must be rather than
        /// by which object would do.
        ///
        /// Subject is whoever is short, and <see cref="Fact.Value"/> carries the specification -
        /// the category, and the quality and worth the goods have to reach. Stating the demand as
        /// a property constraint rather than a named item is the whole of what makes it answerable
        /// by production: any object that meets it answers it, and no object that does not.
        /// </summary>
        public const string Needs = "needs";

        /// <summary>
        /// Who made a particular object. Subject is the maker, object is the thing.
        ///
        /// Written when the game says a production finished, not when the simulation decides one
        /// did. It is provenance, which is a different question from ownership: a pie somebody
        /// baked and then sold is still a pie they baked.
        /// </summary>
        public const string Produced = "produced";

        /// <summary>
        /// A thing is not working. Subject is the object itself, as with <see cref="IsDead"/>.
        ///
        /// The state a repair removes, and the state sabotage would leave behind if breaking
        /// something short of destroying it were on the table. Kept as a fact rather than a flag
        /// on the item so that it can be believed, doubted, evidenced and superseded like
        /// everything else the world knows.
        /// </summary>
        public const string Damaged = "damaged";

        /// <summary>
        /// Whether this is the kind of thing people repeat to each other.
        ///
        /// Gossip is about what happened, not about how the world is arranged. "Kip stole the
        /// ring" travels; "Tovar owns a ring" is not news, and the first live circulation spent
        /// half a day's budget on exactly that - which reads as nonsense the moment it reaches
        /// dialogue, and crowds out the fact anybody actually cares about.
        ///
        /// The distinction is a property of the vocabulary, so it lives with the vocabulary. A
        /// predicate not listed here is silent: adding one means deciding whether it is news, and
        /// a town that says nothing about something new is a quieter failure than a town that
        /// gossips about who owns what.
        /// </summary>
        public static bool IsNewsworthy(string predicate)
        {
            switch (predicate)
            {
                case Stole:
                case Killed:
                case Owes:
                case Hired:
                case Funds:
                case LiedAbout:
                case IsDead:
                case Witnessed:
                case Investigating:
                case Forged:
                case Extorted:
                // A town short of something, and the thing it depends on being broken, are both
                // exactly what people tell each other - and both are what turns hearing about a
                // shortage into being able to do something about it.
                case Needs:
                case Damaged:
                // How somebody died travels for the same reason the death does, and it is the
                // half that changes what anyone does about it.
                case KilledBy:
                // Where a thing ended up is worth repeating precisely because it is actionable -
                // it is what turns hearing about a theft into being able to go and look.
                case LocatedAt:
                    return true;

                // Standing arrangements. True, queryable, and nobody's news: who owns what, who
                // is in which guild, who is whose cousin. Who baked a loaf belongs here too: it
                // is provenance, worth having on the record and worth nobody's breath.
                default:
                    return false;
            }
        }
    }
}
