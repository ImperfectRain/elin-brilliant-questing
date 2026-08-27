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
    }
}
