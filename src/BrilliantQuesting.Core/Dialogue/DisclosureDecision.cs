using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Dialogue
{
    /// <summary>
    /// How forthcoming somebody has decided to be about one claim, right now, with this person.
    ///
    /// A ladder rather than a flag, because the interesting failure of a boolean is that the only
    /// two things anybody can ever do are tell everything and say nothing, and a world of those
    /// two moves has no interrogation in it. The order is meaningful and is relied on: a decision
    /// is more forthcoming than another exactly when its strategy sorts higher, which is what lets
    /// a test assert that raising a tie moved somebody up the ladder without naming thresholds.
    ///
    /// None of the four is a falsehood. What an unwilling speaker does <em>instead</em> - evading,
    /// changing the subject, saying something they do not believe - is BQ-073's, and the reason it
    /// is not here is that a refusal that silently became a lie would put an untrue claim into the
    /// world with nothing recording that it was one.
    /// </summary>
    public enum DisclosureStrategy
    {
        /// <summary>
        /// Not a decision about willingness at all: the speaker holds no belief about the claim,
        /// so there was nothing to weigh and nothing to withhold.
        ///
        /// Distinguished from <see cref="Refuse"/> on purpose. "I will not tell you" and "I do not
        /// know" are different states of the world, they are reached differently, and collapsing
        /// them is the exact shape of the mistake this step exists to prevent - somebody who ought
        /// to know a thing being treated as somebody who does.
        /// </summary>
        NothingToDisclose = 0,

        /// <summary>Declines. The claim is not put forward, and nothing else is put forward in its place.</summary>
        Refuse = 1,

        /// <summary>
        /// Will not say it and does not decline outright - the question is let go of rather than
        /// answered. Still no false claim: turning a deflection into words that assert something
        /// is BQ-073's, and this layer stops at deciding that the claim itself stays unsaid.
        /// </summary>
        Deflect = 2,

        /// <summary>
        /// Says it, without standing behind it. The same claim, put forward as something they
        /// think rather than something they will swear to, which is what somebody does with a
        /// belief they hold too weakly to be held to - or hold firmly and would rather not be
        /// quoted on.
        ///
        /// Hedging is a weaker <em>commitment</em>, never a smaller <em>part</em> of the fact. How
        /// much of one claim comes out in stages is BQ-072's, and the two would be easy to
        /// conflate: this one changes how firmly the same thing is said.
        /// </summary>
        Hedge = 3,

        /// <summary>Says it, and stands behind it.</summary>
        Disclose = 4
    }

    /// <summary>
    /// The controlled vocabulary of reasons somebody speaks or does not.
    ///
    /// Tags rather than an enum for the reason <c>FactPredicates</c>, <c>EventTags</c> and
    /// <c>DevelopmentPressures</c> are: a later rule adds a word without reshaping every consumer.
    /// The list is closed for a different reason though - it is CD §17.5's list, minus the entries
    /// no authoritative state supports yet. Social practice is the notable absence: §16's norms do
    /// not exist as state, and a pressure derived from nothing would be a number pretending to be
    /// a reason.
    /// </summary>
    public static class DisclosurePressures
    {
        /// <summary>How sure they are. A belief held weakly is its own reason not to state it.</summary>
        public const string Confidence = "confidence";

        /// <summary>The standing disposition to say what one holds - <c>PersonalityWeights.Honesty</c>.</summary>
        public const string Candour = "candour";

        /// <summary>What the speaker is to the person asking, and how far they trust anybody.</summary>
        public const string Relationship = "relationship";

        /// <summary>The claim is kept, or is the speaker's own business.</summary>
        public const string Privacy = "privacy";

        /// <summary>Present fear, stress and suspicion. Transient by construction - it decays.</summary>
        public const string Fear = "fear";

        /// <summary>A tie to the person the claim is about, which telling would spend.</summary>
        public const string Loyalty = "loyalty";

        /// <summary>Something held over the claim's subject that is only worth holding while it is quiet.</summary>
        public const string Leverage = "leverage";

        /// <summary>
        /// How the law bears on this claim for this speaker. Signed, because it is one pressure
        /// with two directions: their own crime is the strongest reason in the model to stay
        /// quiet, and somebody else's is a reason to speak for whoever values law.
        /// </summary>
        public const string LegalRisk = "legal_risk";

        /// <summary>What saying it would cost them socially: shame, embarrassment, standing.</summary>
        public const string SocialRisk = "social_risk";

        /// <summary>Anger at the person the claim is about. The pressure that makes people talk.</summary>
        public const string Grievance = "grievance";
    }

    /// <summary>
    /// One reason, with its sign, its size and the state it was read from.
    ///
    /// The <see cref="Because"/> string is diagnostic and nothing branches on it, the same rule
    /// <c>SpeechAct.WhyNot</c> holds: an explanation that anything reads becomes a contract, and
    /// then the explanation cannot be improved.
    /// </summary>
    public readonly struct DisclosurePressure
    {
        public DisclosurePressure(string tag, double weight, string because)
        {
            Tag = tag;
            Weight = weight;
            Because = because ?? string.Empty;
        }

        /// <summary>One of <see cref="DisclosurePressures"/>.</summary>
        public string Tag { get; }

        /// <summary>Positive pushes toward saying it, negative toward keeping it.</summary>
        public double Weight { get; }

        /// <summary>Which state this was read from, in words, for the inspector.</summary>
        public string Because { get; }

        public bool TowardDisclosure => Weight > 0.0;

        public double Magnitude => Weight < 0.0 ? -Weight : Weight;

        public override string ToString()
        {
            return Tag + " " + (Weight >= 0.0 ? "+" : "-") + Magnitude.ToString("0.00");
        }
    }

    /// <summary>
    /// What one character decided to do about one claim when one person asked - and why.
    ///
    /// CD §17.5's conceptual result, with two of its fields deliberately absent. There is no
    /// maximum-detail field, because how much of a claim comes out in stages is BQ-072's and a
    /// half-built ladder here would be the thing BQ-072 then had to argue with. There is no lie
    /// strategy, because a lie is a stance held against the speaker's own belief rather than a way
    /// of answering, and BQ-073 owns both deciding one and recording it so it can be caught.
    ///
    /// The decision is transient, like the act it may become (D030): it is what somebody would do
    /// if asked now, recomputed from authoritative state every time, and it enters no save. The
    /// <see cref="Balance"/> is arithmetic over that state rather than a standing on it - nothing
    /// stores it, nothing accumulates it, and no second social score exists to disagree with the
    /// relationships, values, emotions and beliefs it was read from.
    /// </summary>
    public sealed class DisclosureDecision
    {
        private static readonly DisclosurePressure[] NoPressures = new DisclosurePressure[0];

        internal DisclosureDecision(
            EntityId speaker,
            EntityId asker,
            EntityId factId,
            DisclosureStrategy strategy,
            double balance,
            IReadOnlyList<DisclosurePressure> pressures,
            IReadOnlyList<DisclosurePressure> decisive,
            string note)
        {
            Speaker = speaker;
            Asker = asker;
            FactId = factId;
            Strategy = strategy;
            Balance = balance;
            Pressures = pressures ?? NoPressures;
            Decisive = decisive ?? NoPressures;
            Note = note ?? string.Empty;
        }

        public EntityId Speaker { get; }

        /// <summary>Who put the question. Disclosure is always to somebody; there is no general willingness.</summary>
        public EntityId Asker { get; }

        public EntityId FactId { get; }

        public DisclosureStrategy Strategy { get; }

        /// <summary>
        /// Whether the claim itself comes out. True for <see cref="DisclosureStrategy.Disclose"/>
        /// and <see cref="DisclosureStrategy.Hedge"/> - a hedged claim is still the claim.
        /// </summary>
        public bool WillDisclose => Strategy >= DisclosureStrategy.Hedge;

        /// <summary>
        /// Whether the speaker will stand behind what they say. False for a hedge, and the whole
        /// of the difference between the two forthcoming strategies.
        /// </summary>
        public bool Committed => Strategy == DisclosureStrategy.Disclose;

        /// <summary>Signed sum of <see cref="Pressures"/>. Derived, never stored, never a standing.</summary>
        public double Balance { get; }

        /// <summary>Every pressure that applied, strongest first. Empty when nothing was weighed.</summary>
        public IReadOnlyList<DisclosurePressure> Pressures { get; }

        /// <summary>
        /// The pressures that actually settled it: the ones whose removal, one at a time, would
        /// have produced a different strategy. Empty when no single pressure carried the decision,
        /// which is itself worth seeing - it means the answer came from the balance rather than
        /// from one thing.
        /// </summary>
        public IReadOnlyList<DisclosurePressure> Decisive { get; }

        /// <summary>Why there was nothing to weigh, when there was nothing to weigh.</summary>
        public string Note { get; }

        public override string ToString()
        {
            return Speaker.Value + "->" + Asker.Value + " " + FactId.Value + ": " + Strategy;
        }
    }
}
