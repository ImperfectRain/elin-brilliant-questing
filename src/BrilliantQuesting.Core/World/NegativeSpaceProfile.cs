using System;
using System.Collections.Generic;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// A line an actor holds against a move this simulation already makes (BQ-077, CD §17.7).
    ///
    /// Every member names a <em>move</em> - a problem-solving style the goal pipeline selects, or
    /// a disclosure tactic the disclosure decision reaches - and never a disposition. That is the
    /// whole distinction this step turns on: <c>PersonalityWeights.Honesty</c> is a slope that
    /// makes lying less likely as it rises, and <see cref="NeverLiesDirectly"/> is a line that
    /// takes lying off the table however low the slope sits. A character who trades sharply and
    /// still will not tell you a flat untruth is not expressible as a number, and that is the
    /// character CD §17.7 is about.
    ///
    /// The list is closed and deliberately short. CD §17.7 offers eight examples and this takes
    /// four - the three the roadmap line itself names, plus one more on the action side so the
    /// action seam is general rather than a special case. A prohibition earns its place by naming
    /// a move that already exists: an entry for a move nothing selects would be a personality
    /// trait wearing a prohibition's clothes, and turning every trait into a prohibition is the
    /// failure this vocabulary is sized to avoid.
    ///
    /// <b>Nothing derives a prohibition from race, character archetype or occupation.</b> A line
    /// is declared onto an actor, exactly as a <see cref="ContradictionProfile"/> or a
    /// <see cref="CharacterQuirkProfile"/> is. There is no constructor here that takes an identity
    /// of any kind, which is what keeps "guards never beg" from being a rule this file could grow.
    /// </summary>
    public enum PersonalProhibition
    {
        /// <summary>
        /// Will not ask others to carry their trouble for nothing:
        /// <see cref="ProblemSolvingStyle.AskFriends"/>.
        ///
        /// Paying for help is not begging and is untouched - what this refuses is the appeal, not
        /// the transaction.
        /// </summary>
        NeverBegs = 0,

        /// <summary>
        /// Will not put the matter in front of whoever holds authority:
        /// <see cref="ProblemSolvingStyle.AskAuthority"/>.
        ///
        /// A personal line and never a reading of what somebody is. A guard may hold it and a
        /// thief may not; nothing in this assembly infers it from either.
        /// </summary>
        NeverInvolvesAuthority = 1,

        /// <summary>
        /// Will not assert a claim they do not hold: <c>DisclosureTactic.Falsify</c>.
        ///
        /// The same concept BQ-073 already owns, expressed as a line rather than as a slope, and
        /// applied at BQ-073's own gate rather than beside it. It is not a second honesty score
        /// and there is no second deception record: a speaker who holds this line refuses or
        /// evades, which are the moves that gate already falls through to, and the world records
        /// exactly what it always did.
        /// </summary>
        NeverLiesDirectly = 2,

        /// <summary>
        /// Will not put a discrediting claim about their own kin forward, however the weighing
        /// came out.
        ///
        /// Conditional on the situation rather than standing: it bears on a claim only when the
        /// claim discredits its subject and the speaker's tie to that subject is
        /// <c>RelationKind.Family</c> or <c>RelationKind.Spouse</c>. Distinct from the loyalty
        /// pressure BQ-071 already weighs, which is a warm tie making somebody less willing; this
        /// is a line that holds after the weighing said they would speak.
        /// </summary>
        NeverSpeaksBadlyOfFamily = 3
    }

    /// <summary>
    /// What a line did to one move in one situation - diagnostic in the same sense
    /// <c>DisclosureDecision.Decisive</c> and <see cref="GoalActionTrace.ScoreTerms"/> are.
    ///
    /// A ruling is produced wherever a prohibition could have cost something, including when the
    /// actor holds no such line, so a caller never has to distinguish "was not asked" from "was
    /// asked and said nothing". Nothing branches on <see cref="Because"/>.
    /// </summary>
    public readonly struct ProhibitionRuling
    {
        private ProhibitionRuling(
            PersonalProhibition kind,
            bool held,
            bool broke,
            double firmness,
            double pressure,
            string because)
        {
            Kind = kind;
            Held = held;
            Broke = broke;
            Firmness = firmness;
            Pressure = pressure;
            Because = because ?? string.Empty;
        }

        public PersonalProhibition Kind { get; }

        /// <summary>Whether the actor holds this line at all.</summary>
        public bool Held { get; }

        /// <summary>
        /// Whether enough established pressure came to bear to carry a breakable line. False for
        /// a line nobody holds and for one that is not breakable, whatever the pressure.
        /// </summary>
        public bool Broke { get; }

        /// <summary>Whether the move is off the table: held, and not broken.</summary>
        public bool Forbids => Held && !Broke;

        /// <summary>The bar pressure had to clear, or zero for a line nobody holds.</summary>
        public double Firmness { get; }

        /// <summary>
        /// The pressure that was brought against it, 0..1, read from state the surrounding
        /// decision had already computed. Never a figure this file invents.
        /// </summary>
        public double Pressure { get; }

        /// <summary>Why, in words nothing branches on.</summary>
        public string Because { get; }

        public override string ToString()
        {
            if (!Held)
            {
                return Kind + " not held";
            }

            return Kind + (Broke ? " broken" : " holds")
                + " (firmness " + Firmness.ToString("0.00")
                + ", pressure " + Pressure.ToString("0.00") + ")";
        }

        /// <summary>A line this actor does not hold. Costs nothing and explains nothing.</summary>
        public static ProhibitionRuling NotHeld(PersonalProhibition kind)
        {
            return new ProhibitionRuling(kind, false, false, 0.0, 0.0, string.Empty);
        }

        internal static ProhibitionRuling Of(
            PersonalProhibition kind,
            bool broke,
            double firmness,
            double pressure,
            string because)
        {
            return new ProhibitionRuling(kind, true, broke, firmness, pressure, because);
        }
    }

    /// <summary>
    /// The lines one actor holds. Durable personality, kept and saved beside the
    /// <see cref="ContradictionProfile"/> and the <see cref="CharacterQuirkProfile"/> it is a
    /// sibling of.
    ///
    /// This type decides nothing and scores nothing. It answers one question -
    /// <see cref="Rule"/>: given the pressure the surrounding decision already established, is
    /// this move off the table - and the decision authority that asked keeps its own arithmetic.
    /// There is no parallel utility model here, no per-move weight and no accumulated standing:
    /// a line is a firmness and whether it can break, and everything else about the situation
    /// belongs to whoever is deciding.
    ///
    /// <b>A prohibition is not an impossibility.</b> A breakable line is a thing a character
    /// would rather die than do until the day they do it, which is why breaking one is an outcome
    /// with a recorded reason rather than a silent fallthrough. An unbreakable line is a
    /// statement about the character and still not a statement about the world: the move stays
    /// mechanically available to everyone else, and to this actor through any path that is not
    /// this actor choosing it.
    /// </summary>
    public sealed class NegativeSpaceProfile
    {
        private const int Kinds = 4;

        private static readonly PersonalProhibition[] AllKinds =
        {
            PersonalProhibition.NeverBegs,
            PersonalProhibition.NeverInvolvesAuthority,
            PersonalProhibition.NeverLiesDirectly,
            PersonalProhibition.NeverSpeaksBadlyOfFamily
        };

        private static readonly PersonalProhibition[] None = new PersonalProhibition[0];

        private readonly bool[] _held = new bool[Kinds];
        private readonly bool[] _breakable = new bool[Kinds];
        private readonly double[] _firmness = new double[Kinds];

        /// <summary>Every prohibition in this vocabulary, in enum order. Never varies.</summary>
        public static IReadOnlyList<PersonalProhibition> Vocabulary { get; } = AllKinds;

        /// <summary>Whether this actor holds any line at all.</summary>
        public bool Any
        {
            get
            {
                for (int i = 0; i < Kinds; i++)
                {
                    if (_held[i])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// The lines held, in enum order. Ordering is fixed rather than insertion-dependent so
        /// two actors declared the same lines in different orders behave identically.
        /// </summary>
        public IReadOnlyList<PersonalProhibition> Declared
        {
            get
            {
                if (!Any)
                {
                    return None;
                }

                List<PersonalProhibition> held = new List<PersonalProhibition>();
                for (int i = 0; i < Kinds; i++)
                {
                    if (_held[i])
                    {
                        held.Add(AllKinds[i]);
                    }
                }

                return held;
            }
        }

        public bool Holds(PersonalProhibition kind) => _held[Index(kind)];

        /// <summary>The bar pressure must clear to break this line, or zero when it is not held.</summary>
        public double FirmnessOf(PersonalProhibition kind)
        {
            int i = Index(kind);
            return _held[i] ? _firmness[i] : 0.0;
        }

        /// <summary>Whether pressure can carry this line at all. False for a line not held.</summary>
        public bool IsBreakable(PersonalProhibition kind)
        {
            int i = Index(kind);
            return _held[i] && _breakable[i];
        }

        /// <summary>
        /// Gives this actor a line. <paramref name="firmness"/> is clamped to 0..1 and is the
        /// pressure a break must reach; an unbreakable line ignores it for the purpose of
        /// breaking and keeps it only as a reading of how strongly the line is held.
        /// </summary>
        public void Declare(PersonalProhibition kind, double firmness, bool breakable = true)
        {
            int i = Index(kind);
            _held[i] = true;
            _breakable[i] = breakable;
            _firmness[i] = Clamp01(firmness);
        }

        public void Withdraw(PersonalProhibition kind)
        {
            int i = Index(kind);
            _held[i] = false;
            _breakable[i] = false;
            _firmness[i] = 0.0;
        }

        /// <summary>
        /// What this actor's line does to a move, given the pressure the caller has already
        /// established and the state that pressure was read from.
        ///
        /// <paramref name="pressure"/> is 0..1 and is never computed here: the goal pipeline
        /// passes the need pressure it already derived from the threatened value, and the
        /// disclosure decision passes how far its own weighing ran past the threshold the
        /// forbidden move needed. Requiring the caller to supply it is what stops this type from
        /// becoming a second opinion about how much a situation matters.
        /// </summary>
        public ProhibitionRuling Rule(PersonalProhibition kind, double pressure, string because)
        {
            int i = Index(kind);
            if (!_held[i])
            {
                return ProhibitionRuling.NotHeld(kind);
            }

            double applied = Clamp01(pressure);
            double firmness = _firmness[i];
            string state = string.IsNullOrWhiteSpace(because) ? "no reading given" : because;

            if (_breakable[i] && applied >= firmness)
            {
                return ProhibitionRuling.Of(
                    kind,
                    true,
                    firmness,
                    applied,
                    "breaks the line against " + Describe(kind) + ": " + state
                    + " reached " + applied.ToString("0.00")
                    + ", at or past its firmness " + firmness.ToString("0.00"));
            }

            return ProhibitionRuling.Of(
                kind,
                false,
                firmness,
                applied,
                "will not " + Describe(kind) + ": " + state
                + " reached only " + applied.ToString("0.00")
                + (_breakable[i]
                    ? ", short of its firmness " + firmness.ToString("0.00")
                    : ", and the line does not break"));
        }

        public NegativeSpaceProfile Clone()
        {
            NegativeSpaceProfile copy = new NegativeSpaceProfile();
            for (int i = 0; i < Kinds; i++)
            {
                copy._held[i] = _held[i];
                copy._breakable[i] = _breakable[i];
                copy._firmness[i] = _firmness[i];
            }

            return copy;
        }

        /// <summary>The move a line refuses, in words, for the inspector.</summary>
        public static string Describe(PersonalProhibition kind)
        {
            switch (kind)
            {
                case PersonalProhibition.NeverBegs:
                    return "beg";
                case PersonalProhibition.NeverInvolvesAuthority:
                    return "involve authority";
                case PersonalProhibition.NeverLiesDirectly:
                    return "lie directly";
                case PersonalProhibition.NeverSpeaksBadlyOfFamily:
                    return "speak badly of family";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown prohibition.");
            }
        }

        private static int Index(PersonalProhibition kind)
        {
            int i = (int)kind;
            if (i < 0 || i >= Kinds)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown prohibition.");
            }

            return i;
        }

        private static double Clamp01(double value)
        {
            return value < 0.0 ? 0.0 : value > 1.0 ? 1.0 : value;
        }
    }

    /// <summary>
    /// Where a line meets a move. The one table mapping BQ-077's vocabulary onto the moves the
    /// simulation already selects among, kept in a single place so a decision surface and a
    /// realizer can never disagree about what a prohibition forbids.
    /// </summary>
    public static class NegativeSpace
    {
        /// <summary>
        /// The line that bears on a problem-solving style, if any.
        ///
        /// Two of the four reach this surface, and the rest reach the disclosure decision. That
        /// asymmetry is the point rather than an omission: a prohibition names a move, and the
        /// moves live where they live.
        ///
        /// <see cref="ProblemSolvingStyle.Wait"/> carries no prohibition in this vocabulary and
        /// is a candidate for every problem the goal pipeline poses, so no combination of lines
        /// can leave an actor with nothing they are willing to do.
        /// </summary>
        public static bool Bears(ProblemSolvingStyle style, out PersonalProhibition kind)
        {
            switch (style)
            {
                case ProblemSolvingStyle.AskFriends:
                    kind = PersonalProhibition.NeverBegs;
                    return true;
                case ProblemSolvingStyle.AskAuthority:
                    kind = PersonalProhibition.NeverInvolvesAuthority;
                    return true;
                default:
                    kind = default(PersonalProhibition);
                    return false;
            }
        }

        /// <summary>
        /// What one line does to a style for this actor under this pressure, or a not-held ruling
        /// when no line bears on it.
        /// </summary>
        public static ProhibitionRuling Rule(
            NegativeSpaceProfile profile,
            ProblemSolvingStyle style,
            double pressure,
            string because)
        {
            PersonalProhibition kind;
            if (!Bears(style, out kind))
            {
                return ProhibitionRuling.NotHeld(default(PersonalProhibition));
            }

            if (profile == null)
            {
                return ProhibitionRuling.NotHeld(kind);
            }

            return profile.Rule(kind, pressure, because);
        }
    }
}
