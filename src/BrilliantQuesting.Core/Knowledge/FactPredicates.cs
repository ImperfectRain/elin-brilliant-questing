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
        /// Something falls within the domain of a deity, named in <see cref="Fact.Value"/>.
        ///
        /// Subject is either a place - ground an altar has made the god's - or the matter itself:
        /// a blighted field, a barren herd, a spring that has stopped. The value is a
        /// <c>DevotionSpec</c>, so the same predicate says both "this is Kumiromi's ground" and
        /// "lifting this is in Kumiromi's gift, and he asks this much of whoever asks him".
        ///
        /// Stating it as a fact rather than as a table of gods and their portfolios is what keeps
        /// the faith routes generatable: the situation says whose matter this is, and any
        /// worshipper of that god can answer it. Nothing in the simulation has to know what
        /// Kumiromi is the god of.
        /// </summary>
        public const string SacredTo = "sacred_to";

        /// <summary>
        /// Somebody has laid goods on a god's ground and they have not been spent yet.
        ///
        /// Subject is the giver, object is the ground, and <see cref="Fact.Value"/> carries the
        /// deity and what the offering was worth. Standing rather than accumulated on a counter,
        /// so that a petition can spend it, a save can carry it, and two small offerings can add
        /// up to one the god will hear.
        /// </summary>
        public const string OfferedTo = "offered_to";

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
        /// A physical thing bars access to a site. Subject is the site whose kept things are out
        /// of reach, object is the obstruction, and <see cref="Fact.Value"/> names what kind of
        /// work can answer it. It is not a permission problem: a guard can be talked past, but a
        /// rockfall has to be moved, broken, mined around or otherwise answered in the world.
        /// </summary>
        public const string BlocksAccessTo = "blocks_access_to";

        /// <summary>
        /// A damaged plant or field reads as a cultivation problem to somebody whose work is tied
        /// to growing things. It is an interpretation of evidence, not a replacement for the
        /// original fact that the crop is damaged.
        /// </summary>
        public const string HasSoilTrouble = "has_soil_trouble";

        /// <summary>
        /// A damaged object, body or crop reads as contamination to somebody with an alchemical
        /// or medical frame. The same physical evidence may still support other local readings.
        /// </summary>
        public const string IsContaminated = "is_contaminated";

        /// <summary>
        /// A damaged object or crop reads as possible hostile action to somebody whose local
        /// frame is law, security or social order.
        /// </summary>
        public const string MayBeSabotaged = "may_be_sabotaged";

        /// <summary>
        /// Somebody is not safe where they are. Subject is the person, object is whoever or
        /// whatever they are not safe from (nobody, when the danger has no face), and
        /// <see cref="Fact.Value"/> says what kind of exposure it is - a witness, a refugee, a
        /// fugitive, somebody burned out of their house.
        ///
        /// Deliberately not a <see cref="Needs"/> demand, which is a *property constraint on
        /// goods*: a person with nowhere safe to sleep is not a shortage a handicraft roll can
        /// fill, and stating it as one would have let the generalist craft answer it. Stating it
        /// as its own claim is what makes it generatable - a situation says once that somebody is
        /// exposed, and every route that can answer exposure becomes available at once.
        /// </summary>
        public const string AtRisk = "at_risk";

        /// <summary>
        /// A person is under somebody's roof and protection. Subject is the person taken in,
        /// object is whoever took them in, and <see cref="Fact.Value"/> names the undertaking -
        /// a resident, a guest, a specialist taken on, somebody being watched over.
        ///
        /// One predicate for the whole family because it is one claim with four strengths, and
        /// because what it costs the person who made it is the same either way: it is public,
        /// it travels, and it tells anyone who hears it exactly where to look.
        /// </summary>
        public const string ShelteredBy = "sheltered_by";

        /// <summary>
        /// Somebody won a public contest. Subject is the winner, object is the contest site, and
        /// value names the event, because the later reference people care about is "who won what".
        /// </summary>
        public const string WonCompetition = "won_competition";

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
                case BlocksAccessTo:
                // Both halves of a sanctuary travel, and they are the reason it is a decision
                // rather than a free good deed. That somebody is exposed is what a town talks
                // about; where they went afterwards is what the person they are hiding from is
                // listening for.
                case AtRisk:
                case ShelteredBy:
                // How somebody died travels for the same reason the death does, and it is the
                // half that changes what anyone does about it.
                case KilledBy:
                // Where a thing ended up is worth repeating precisely because it is actionable -
                // it is what turns hearing about a theft into being able to go and look.
                case LocatedAt:
                case WonCompetition:
                    return true;

                // Standing arrangements. True, queryable, and nobody's news: who owns what, who
                // is in which guild, who is whose cousin. Who baked a loaf belongs here too: it
                // is provenance, worth having on the record and worth nobody's breath.
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether this claim is something that is wrong *now*, rather than something that
        /// happened.
        ///
        /// The difference decides what anybody can be asked to put right. A killing is history and
        /// stays true for ever; a person who is not safe, a thing that is broken, a road that is
        /// shut and a town that is short are conditions, and each of them is already something the
        /// verb library supersedes when a route answers it - shelter ends an exposure, a blessing
        /// or a repair ends a damage, clearing ends an obstruction, goods end a demand. This names
        /// that set once so a verb that answers *a matter* rather than a named predicate can ask
        /// which claims are matters at all.
        ///
        /// Silent by default, like <see cref="IsNewsworthy"/>: a predicate nobody has thought
        /// about is not a standing trouble, so the failure of forgetting one is a route that does
        /// not appear rather than a verb that offers to undo the past.
        /// </summary>
        public static bool IsStandingTrouble(string predicate)
        {
            switch (predicate)
            {
                case AtRisk:
                case Damaged:
                case BlocksAccessTo:
                case Needs:
                    return true;

                default:
                    return false;
            }
        }
    }
}
