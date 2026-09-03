using System.Collections.Generic;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>What the act does to the proposition it carries. Fixed per act type.</summary>
    public enum SpeechActStance
    {
        /// <summary>The speaker puts the proposition forward as so.</summary>
        Affirms,

        /// <summary>The speaker puts it forward as not so.</summary>
        Denies,

        /// <summary>The speaker takes no position and asks for one.</summary>
        Questions,

        /// <summary>The act is not about the truth of anything - it asks for or withholds a doing.</summary>
        None
    }

    /// <summary>Which way the act moves information or compliance. Fixed per act type.</summary>
    public enum SpeechActDirection
    {
        SeeksInformation,
        GivesInformation,
        SeeksAction,
        WithholdsAction,

        /// <summary>
        /// Keeps information back without declining to give it. Distinct from
        /// <see cref="WithholdsAction"/>, which is what an open refusal does: a refusal is an
        /// answer of a kind and lands as one, and an evasion leaves the asker without even that.
        /// </summary>
        WithholdsInformation,
        Repairs,

        /// <summary>
        /// The speaker binds themself to a future doing, rather than asking anybody else for one.
        /// </summary>
        CommitsToAction
    }

    /// <summary>How much content the act must carry to be the act it claims to be.</summary>
    public enum SpeechActContentRule
    {
        /// <summary>Content may be empty - the antecedent already named the matter.</summary>
        Optional,

        /// <summary>Something must be named: a fact, an object, a destination or a purpose.</summary>
        Required,

        /// <summary>A claim must be named. Nothing else will do: this act is about a proposition.</summary>
        PropositionRequired
    }

    /// <summary>Who the content is about, relative to the people in the act.</summary>
    public enum SpeechActReferentRule
    {
        /// <summary>The act may name somebody the content is about, and need not.</summary>
        Optional,

        /// <summary>The speaker owns it. Naming anybody else makes it a different act.</summary>
        MustBeSpeaker,

        /// <summary>Somebody must be named, and it cannot be the speaker - that would be an admission.</summary>
        MustNotBeSpeaker,

        /// <summary>
        /// Somebody must be named who is neither the speaker nor anybody being spoken to. Talk
        /// about a person to their face is not gossip whatever else it is.
        /// </summary>
        MustBeAbsentThirdParty
    }

    /// <summary>
    /// The semantic shape of one act type: everything true of every instance of it, held once
    /// rather than repeated at every construction site.
    ///
    /// This table is the whole of what "meaning before wording" buys. A consumer that wants to
    /// know whether an act asserts something, asks for something, owns something or passes it on
    /// about somebody absent reads it here, with no line of dialogue in existence and none
    /// planned. A realizer (BQ-074) may later choose a thousand ways to say an <see cref="SpeechActType.Accuse"/>;
    /// none of them may change what it did.
    ///
    /// New act types belong here rather than as behaviour at call sites, for the same reason
    /// predicates belong in <c>FactPredicates</c>: a vocabulary spread over its users stops being
    /// a vocabulary.
    /// </summary>
    public sealed class SpeechActProfile
    {
        private static readonly SpeechActType[] Nothing = new SpeechActType[0];

        private SpeechActProfile(
            SpeechActType type,
            SpeechActStance stance,
            SpeechActDirection direction,
            SpeechActContentRule content,
            SpeechActReferentRule referent,
            bool referentDefaultsToSpeaker,
            bool antecedentRequired,
            SpeechActType[] respondsTo)
        {
            Type = type;
            Stance = stance;
            Direction = direction;
            Content = content;
            Referent = referent;
            ReferentDefaultsToSpeaker = referentDefaultsToSpeaker;
            AntecedentRequired = antecedentRequired;
            RespondsTo = respondsTo ?? Nothing;
        }

        public SpeechActType Type { get; }

        public SpeechActStance Stance { get; }

        public SpeechActDirection Direction { get; }

        public SpeechActContentRule Content { get; }

        public SpeechActReferentRule Referent { get; }

        /// <summary>
        /// Whether an unnamed referent means the speaker. True for the acts whose ordinary case is
        /// about oneself, so a caller owning their own doing does not have to say so twice.
        /// </summary>
        public bool ReferentDefaultsToSpeaker { get; }

        /// <summary>
        /// Whether the act is unintelligible without something to respond to. An answer nobody
        /// asked for is not an answer, and a refusal of nothing is not a refusal.
        /// </summary>
        public bool AntecedentRequired { get; }

        /// <summary>
        /// The act types this one may respond to. Empty means unconstrained rather than none: a
        /// question asked back at a question is a real move and this layer does not forbid it.
        /// </summary>
        public IReadOnlyList<SpeechActType> RespondsTo { get; }

        public bool MayRespondTo(SpeechActType type)
        {
            if (RespondsTo.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < RespondsTo.Count; i++)
            {
                if (RespondsTo[i] == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Dictionary<SpeechActType, SpeechActProfile> Table = Build();

        public static SpeechActProfile Of(SpeechActType type)
        {
            return Table.TryGetValue(type, out SpeechActProfile profile) ? profile : null;
        }

        /// <summary>Every act in the vocabulary, so a caller can enumerate it without a literal list.</summary>
        public static IReadOnlyList<SpeechActType> Vocabulary { get; } = new[]
        {
            SpeechActType.Ask,
            SpeechActType.Answer,
            SpeechActType.Accuse,
            SpeechActType.Deny,
            SpeechActType.Admit,
            SpeechActType.Request,
            SpeechActType.Refuse,
            SpeechActType.Threaten,
            SpeechActType.Apologize,
            SpeechActType.Gossip,
            SpeechActType.Evade,
            SpeechActType.Promise
        };

        private static Dictionary<SpeechActType, SpeechActProfile> Build()
        {
            Dictionary<SpeechActType, SpeechActProfile> table = new Dictionary<SpeechActType, SpeechActProfile>();

            // Asking takes no position on the matter; that is exactly what makes it askable.
            Add(table, SpeechActType.Ask, SpeechActStance.Questions, SpeechActDirection.SeeksInformation,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, false, false, null);

            // An answer supplies the claim. The speaker who wants to put the claim the other way
            // is not answering, they are denying, and the vocabulary already has that word.
            Add(table, SpeechActType.Answer, SpeechActStance.Affirms, SpeechActDirection.GivesInformation,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, false, true,
                new[] { SpeechActType.Ask });

            // The charge, and the one act that must name somebody other than the speaker. Nothing
            // here checks that the named person is the one the claim is about: an accusation that
            // names the wrong person is a well-formed false accusation, and correcting it silently
            // would delete the move the whole investigation layer exists to make catchable.
            Add(table, SpeechActType.Accuse, SpeechActStance.Affirms, SpeechActDirection.GivesInformation,
                SpeechActContentRule.PropositionRequired, SpeechActReferentRule.MustNotBeSpeaker, false, false, null);

            Add(table, SpeechActType.Deny, SpeechActStance.Denies, SpeechActDirection.GivesInformation,
                SpeechActContentRule.PropositionRequired, SpeechActReferentRule.Optional, true, false,
                new[] { SpeechActType.Accuse, SpeechActType.Ask, SpeechActType.Gossip, SpeechActType.Threaten });

            // Owning it. The referent must be the speaker - an admission about somebody else is a
            // charge or an answer, and calling it an admission would lose the difference that
            // makes a confession worth staging.
            Add(table, SpeechActType.Admit, SpeechActStance.Affirms, SpeechActDirection.GivesInformation,
                SpeechActContentRule.PropositionRequired, SpeechActReferentRule.MustBeSpeaker, true, false,
                new[] { SpeechActType.Accuse, SpeechActType.Ask });

            Add(table, SpeechActType.Request, SpeechActStance.None, SpeechActDirection.SeeksAction,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, false, false, null);

            // A refusal need carry no content of its own: what is being refused is the thing it
            // answers, which is why that antecedent is the one part it cannot do without.
            Add(table, SpeechActType.Refuse, SpeechActStance.None, SpeechActDirection.WithholdsAction,
                SpeechActContentRule.Optional, SpeechActReferentRule.Optional, false, true,
                new[] { SpeechActType.Request, SpeechActType.Ask, SpeechActType.Threaten });

            Add(table, SpeechActType.Threaten, SpeechActStance.None, SpeechActDirection.SeeksAction,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, false, false, null);

            // Repair, and an affirming one: an apology for something the speaker denies doing is
            // not an apology.
            Add(table, SpeechActType.Apologize, SpeechActStance.Affirms, SpeechActDirection.Repairs,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, true, false, null);

            Add(table, SpeechActType.Gossip, SpeechActStance.Affirms, SpeechActDirection.GivesInformation,
                SpeechActContentRule.PropositionRequired, SpeechActReferentRule.MustBeAbsentThirdParty, false, false, null);

            // Sliding away from what was put to you (BQ-073). Stance is None, and that is the
            // load-bearing entry in the row: an evasion puts no proposition forward either way, so
            // nothing that reads stance can ever score one as an assertion, and a speaker who
            // evades cannot thereby have lied. Content is optional for the same reason a
            // refusal's is - the matter is whatever is being slid away from - and the antecedent
            // is the one part it cannot do without, because an evasion of nothing is just talk.
            //
            // What may be evaded is what presses for something a speaker can decline to give: a
            // question, a charge, a request. A threat is complied with or refused, and treating a
            // shrug at one as the same move would flatten the difference the coercion verbs turn
            // on.
            Add(table, SpeechActType.Evade, SpeechActStance.None, SpeechActDirection.WithholdsInformation,
                SpeechActContentRule.Optional, SpeechActReferentRule.Optional, false, true,
                new[] { SpeechActType.Ask, SpeechActType.Accuse, SpeechActType.Request });

            // A commitment, not a claim (BQ-083). Stance is None for the same reason Request's and
            // Threaten's are: nothing here is put forward as true or false, so Deception reads it
            // as asserting nothing and no promise can be classified a lie at the moment it is
            // spoken - it is kept or broken later, by what its speaker does. Unconstrained on what
            // it may respond to, the same as Threaten and Apologize: a promise offered unprompted
            // and one that answers a request are both ordinary.
            Add(table, SpeechActType.Promise, SpeechActStance.None, SpeechActDirection.CommitsToAction,
                SpeechActContentRule.Required, SpeechActReferentRule.Optional, false, false, null);

            return table;
        }

        private static void Add(
            Dictionary<SpeechActType, SpeechActProfile> table,
            SpeechActType type,
            SpeechActStance stance,
            SpeechActDirection direction,
            SpeechActContentRule content,
            SpeechActReferentRule referent,
            bool referentDefaultsToSpeaker,
            bool antecedentRequired,
            SpeechActType[] respondsTo)
        {
            table[type] = new SpeechActProfile(
                type, stance, direction, content, referent, referentDefaultsToSpeaker, antecedentRequired, respondsTo);
        }
    }
}
