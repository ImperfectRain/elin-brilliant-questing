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
        public const string Investigating = "investigating";

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
                // Where a thing ended up is worth repeating precisely because it is actionable -
                // it is what turns hearing about a theft into being able to go and look.
                case LocatedAt:
                    return true;

                // Standing arrangements. True, queryable, and nobody's news: who owns what, who
                // is in which guild, who is whose cousin.
                default:
                    return false;
            }
        }
    }
}
