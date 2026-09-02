using System;
using System.Collections.Generic;
using System.Globalization;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// The four things that can make a group of qualified people worth watching together.
    ///
    /// Deliberately four, deliberately named, and deliberately not one number. A single opaque
    /// total would be impossible to argue with and impossible to tune: when a scene casts the
    /// wrong pair the question is always *which* of these fired, and a report that cannot answer
    /// that is a report that gets ignored.
    /// </summary>
    public enum ChemistryDimension
    {
        /// <summary>Two people want things that cannot both happen.</summary>
        GoalConflict,

        /// <summary>They already have a past, and it is charged rather than neutral.</summary>
        SharedHistory,

        /// <summary>One of them knows, or can prove, what the other cannot.</summary>
        KnowledgeAsymmetry,

        /// <summary>One of them can do something to the other that the other cannot do back.</summary>
        PowerAsymmetry
    }

    /// <summary>
    /// One contribution to a group's chemistry: which dimension, which two roles, how much, and
    /// the sentence a human reads to see whether it was a fair reading of the world.
    ///
    /// Every reason names <em>two</em> roles. There is no such thing as a one-actor chemistry
    /// term - that is the shape a stereotype arrives in ("this one is a Punk, so they are the
    /// accuser"), and the type simply cannot express it.
    /// </summary>
    public sealed class ChemistryReason
    {
        public ChemistryReason(
            ChemistryDimension dimension,
            string leftRole,
            string rightRole,
            double weight,
            string detail)
        {
            Dimension = dimension;
            LeftRole = leftRole ?? string.Empty;
            RightRole = rightRole ?? string.Empty;
            Weight = weight;
            Detail = detail ?? string.Empty;
        }

        public ChemistryDimension Dimension { get; }

        public string LeftRole { get; }

        public string RightRole { get; }

        /// <summary>How much this contributed to the group's total. Never negative.</summary>
        public double Weight { get; }

        /// <summary>What in the world state supports it, in the world's own terms.</summary>
        public string Detail { get; }

        public static string DimensionName(ChemistryDimension dimension)
        {
            switch (dimension)
            {
                case ChemistryDimension.GoalConflict:
                    return "goal conflict";
                case ChemistryDimension.SharedHistory:
                    return "shared history";
                case ChemistryDimension.KnowledgeAsymmetry:
                    return "knowledge asymmetry";
                case ChemistryDimension.PowerAsymmetry:
                    return "power asymmetry";
                default:
                    return dimension.ToString();
            }
        }

        public string Describe()
        {
            return DimensionName(Dimension) + " " + LeftRole + "/" + RightRole + " +"
                   + Weight.ToString("0.00", CultureInfo.InvariantCulture) + " (" + Detail + ")";
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// What one candidate group scored, and why.
    ///
    /// The total is the sum of the reasons and nothing else: there is no hidden term, no bias and
    /// no per-actor bonus, so a report that lists the reasons has accounted for the whole number.
    /// </summary>
    public sealed class StoryletChemistryScore
    {
        internal static readonly StoryletChemistryScore Empty =
            new StoryletChemistryScore(new List<ChemistryReason>());

        internal StoryletChemistryScore(List<ChemistryReason> reasons)
        {
            _reasons = reasons ?? new List<ChemistryReason>();
            double total = 0.0;
            for (int i = 0; i < _reasons.Count; i++)
            {
                total += _reasons[i].Weight;
            }

            Total = total;
        }

        private readonly List<ChemistryReason> _reasons;

        public double Total { get; }

        /// <summary>Only the terms that actually fired, in a stable order.</summary>
        public IReadOnlyList<ChemistryReason> Reasons => _reasons;

        public bool IsFlat => _reasons.Count == 0;

        public double TotalFor(ChemistryDimension dimension)
        {
            double total = 0.0;
            for (int i = 0; i < _reasons.Count; i++)
            {
                if (_reasons[i].Dimension == dimension)
                {
                    total += _reasons[i].Weight;
                }
            }

            return total;
        }

        public IReadOnlyList<string> Explain()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < _reasons.Count; i++)
            {
                lines.Add(_reasons[i].Describe());
            }

            return lines;
        }
    }

    /// <summary>
    /// Which of two qualified groups makes the better scene (BQ-068).
    ///
    /// **This decides nothing about who may be cast.** BQ-067 owns that, and every group scored
    /// here has already passed it in full: same requirements, same negative checks, same
    /// one-role-per-person rule. Chemistry only chooses among groups the casting engine would
    /// have accepted anyway, so no term here can put an ineligible person into a role, and a
    /// storylet nobody qualifies for stays uncast however good the chemistry would have been.
    /// That ordering is the whole safety argument, and <see cref="StoryletCasting"/> enforces it
    /// structurally by filtering first and scoring second.
    ///
    /// **It scores groups, not people.** Every term is a relation between two members of the
    /// same cast, and <see cref="ChemistryReason"/> cannot express anything else: there is no
    /// per-actor bonus to add, so "this character type makes a better accuser" is not a sentence
    /// this model can say. A proud debtor facing his creditor scores; a proud man on his own does
    /// not.
    ///
    /// **Identity enters only as asymmetry between the two.** The step before this one
    /// (<see cref="IdentityAffordances"/>) is the anti-stereotype gate, and this consumes it on
    /// the gate's own terms: an office one of them holds and the other does not, a service one of
    /// them runs, a trade they both work in that gives an existing rivalry something to be about.
    /// Race and character archetype derive nothing at all, so they cannot reach this at any
    /// weight; and every identity term here is a *difference*, which means two guards score
    /// exactly what two nobodies score. Nobody is a better suspect for being a Punk.
    ///
    /// **Determinism.** The reasons are produced in a fixed order over a fixed role order, the
    /// arithmetic is a plain sum, and the comparison in <see cref="StoryletCasting"/> falls back
    /// to enumeration order within an epsilon. Identical authoritative state casts identically.
    /// </summary>
    public static class StoryletChemistry
    {
        /// <summary>
        /// Two scores closer than this are the same score. Chemistry is a preference, not a
        /// measurement, and letting the last bit of a double decide who gets cast would make the
        /// answer depend on the order the floating-point sum happened to run in.
        /// </summary>
        public const double Epsilon = 1e-9;

        /// <summary>
        /// What this group is worth, and every reason it is worth it.
        ///
        /// <paramref name="roleOrder"/> is the order the roles were bound in, and it is what makes
        /// the reason list stable: pairs are walked in that order, so the same world always
        /// produces the same sentences in the same sequence.
        /// </summary>
        public static StoryletChemistryScore Score(
            StoryletCastingContext context,
            Fact focus,
            IReadOnlyList<string> roleOrder,
            IReadOnlyDictionary<string, EntityId> bindings,
            ChemistryIdentityCache identities)
        {
            List<ChemistryReason> reasons = new List<ChemistryReason>();
            if (context == null || context.World == null || roleOrder == null || bindings == null)
            {
                return StoryletChemistryScore.Empty;
            }

            for (int i = 0; i < roleOrder.Count; i++)
            {
                EntityId left;
                if (!bindings.TryGetValue(roleOrder[i], out left) || left.IsNone)
                {
                    continue;
                }

                for (int j = i + 1; j < roleOrder.Count; j++)
                {
                    EntityId right;
                    if (!bindings.TryGetValue(roleOrder[j], out right) || right.IsNone || right == left)
                    {
                        continue;
                    }

                    ScorePair(context, focus, roleOrder[i], left, roleOrder[j], right, identities, reasons);
                }
            }

            return new StoryletChemistryScore(reasons);
        }

        private static void ScorePair(
            StoryletCastingContext context,
            Fact focus,
            string leftRole,
            EntityId left,
            string rightRole,
            EntityId right,
            ChemistryIdentityCache identities,
            List<ChemistryReason> reasons)
        {
            NarrativeNpc leftNpc = context.World.Registry.GetNpc(left);
            NarrativeNpc rightNpc = context.World.Registry.GetNpc(right);
            if (leftNpc == null || rightNpc == null)
            {
                return;
            }

            GoalConflict(context, leftRole, leftNpc, rightRole, rightNpc, reasons);
            SharedHistory(context, leftRole, left, rightRole, right, identities, reasons);
            KnowledgeAsymmetry(context, focus, leftRole, left, rightRole, right, reasons);
            PowerAsymmetry(context, leftRole, left, rightRole, right, identities, reasons);
        }

        // -- goal conflict ---------------------------------------------------------------------

        /// <summary>
        /// Two people who want things that cannot both happen.
        ///
        /// Two shapes, and both are read off goals the simulation already formed (BQ-056 …
        /// BQ-060) rather than from any new state. A goal aimed *at* the other person is the
        /// sharp one - "recover the ring from him" is a scene the moment he is in the room. A goal
        /// they both hold over the same subject is the slower one, and it is worth more when their
        /// aims for it differ than when they merely both want it.
        ///
        /// A satisfied goal is not a conflict: somebody who already got what they wanted has
        /// nothing left to press.
        /// </summary>
        private static void GoalConflict(
            StoryletCastingContext context,
            string leftRole,
            NarrativeNpc left,
            string rightRole,
            NarrativeNpc right,
            List<ChemistryReason> reasons)
        {
            AimedAt(context, leftRole, left, rightRole, right, reasons);
            AimedAt(context, rightRole, right, leftRole, left, reasons);

            for (int i = 0; i < left.Goals.Count; i++)
            {
                NpcGoal mine = left.Goals[i];
                if (mine.Satisfied || mine.Subject.IsNone || mine.Weight <= 0)
                {
                    continue;
                }

                for (int j = 0; j < right.Goals.Count; j++)
                {
                    NpcGoal theirs = right.Goals[j];
                    if (theirs.Satisfied || theirs.Subject != mine.Subject || theirs.Weight <= 0)
                    {
                        continue;
                    }

                    bool opposed = !string.Equals(mine.Kind, theirs.Kind, StringComparison.Ordinal);
                    double pressure = Math.Min(mine.Weight, theirs.Weight) / 100.0;
                    double weight = (opposed ? ContestedOpposedAim : ContestedSameAim) * pressure;
                    reasons.Add(new ChemistryReason(
                        ChemistryDimension.GoalConflict,
                        leftRole,
                        rightRole,
                        weight,
                        (opposed ? "opposed aims over " : "both want the same of ")
                        + Name(context, mine.Subject) + ": " + mine.Kind + " vs " + theirs.Kind));
                }
            }
        }

        private static void AimedAt(
            StoryletCastingContext context,
            string actorRole,
            NarrativeNpc actor,
            string subjectRole,
            NarrativeNpc subject,
            List<ChemistryReason> reasons)
        {
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                NpcGoal goal = actor.Goals[i];
                if (goal.Satisfied || goal.Subject != subject.Id || goal.Weight <= 0)
                {
                    continue;
                }

                reasons.Add(new ChemistryReason(
                    ChemistryDimension.GoalConflict,
                    actorRole,
                    subjectRole,
                    GoalAimedAtPerson * (goal.Weight / 100.0),
                    context.World.Registry.NameOf(actor.Id) + " wants " + goal.Kind + " of "
                    + context.World.Registry.NameOf(subject.Id)));
            }
        }

        // -- shared history --------------------------------------------------------------------

        /// <summary>
        /// They already know each other, and it is not neutral.
        ///
        /// The relationship graph is the whole input - no second social model, and nothing written
        /// back. A tie is worth what its kind implies plus how charged it is, and the two extra
        /// terms are the ones that make a *scene* rather than a fact sheet: a tie the two of them
        /// read differently (he still counts her a friend; she does not), and a trade they share
        /// that gives an existing rivalry something concrete to be about.
        ///
        /// That last one is the only place identity reaches shared history, and it is gated on the
        /// tie: two farmers who have never had a cross word score nothing for both being farmers.
        /// A shared trade is what a quarrel is about, never a reason to expect one.
        /// </summary>
        private static void SharedHistory(
            StoryletCastingContext context,
            string leftRole,
            EntityId left,
            string rightRole,
            EntityId right,
            ChemistryIdentityCache identities,
            List<ChemistryReason> reasons)
        {
            RelationshipGraph graph = context.World.Relationships;
            RelationshipEdge forward = graph.Find(left, right);
            RelationshipEdge back = graph.Find(right, left);
            if (forward == null && back == null)
            {
                return;
            }

            RelationshipEdge strongest = Stronger(forward, back);
            double charge = Math.Abs(strongest.Sentiment) / 100.0;
            reasons.Add(new ChemistryReason(
                ChemistryDimension.SharedHistory,
                leftRole,
                rightRole,
                KindWeight(strongest.Kind) + (SentimentWeight * charge),
                Describe(context, strongest)));

            // A tie the two of them do not read the same way. The proud former friend is exactly
            // this: the edge still says Friend, and one of the two has stopped meaning it.
            if (forward != null && back != null && Math.Sign(forward.Sentiment) != Math.Sign(back.Sentiment))
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.SharedHistory,
                    leftRole,
                    rightRole,
                    MismatchedSentiment,
                    "they do not read the tie the same way: "
                    + Describe(context, forward) + " against " + Describe(context, back)));
            }
            else if (forward == null || back == null)
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.SharedHistory,
                    leftRole,
                    rightRole,
                    UnreturnedTie,
                    "the tie is not returned: " + Describe(context, strongest)));
            }

            if (!IsCharged(strongest.Kind) && strongest.Sentiment >= 0)
            {
                return;
            }

            IdentityAffordances leftIdentity = identities.Of(context, left);
            IdentityAffordances rightIdentity = identities.Of(context, right);
            IdentityDomain shared;
            double overlap;
            if (SharedDomain(leftIdentity, rightIdentity, out shared, out overlap))
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.SharedHistory,
                    leftRole,
                    rightRole,
                    SharedTrade * overlap,
                    "a charged tie in a trade they share - "
                    + leftIdentity.ExplainKnowledge(shared) + " and " + rightIdentity.ExplainKnowledge(shared)));
            }
        }

        // -- knowledge asymmetry ---------------------------------------------------------------

        /// <summary>
        /// One of them knows what the other does not, about the thing the scene is about.
        ///
        /// Read straight off the knowledge graph, which already keeps knowing, believing and being
        /// able to prove apart - so this does too, and in that order of sharpness. Somebody who can
        /// prove it standing opposite somebody who merely heard it is a different scene from two
        /// people who both saw it happen, and a scene where neither of them knows anything is not
        /// about this fact at all.
        ///
        /// Nothing here grants knowledge, and nothing here reads a belief into existence: an actor
        /// with no record simply does not know.
        /// </summary>
        private static void KnowledgeAsymmetry(
            StoryletCastingContext context,
            Fact focus,
            string leftRole,
            EntityId left,
            string rightRole,
            EntityId right,
            List<ChemistryReason> reasons)
        {
            if (focus == null)
            {
                return;
            }

            KnowledgeGraph knowledge = context.World.Knowledge;
            bool leftKnows = knowledge.Knows(left, focus.Id);
            bool rightKnows = knowledge.Knows(right, focus.Id);
            if (!leftKnows && !rightKnows)
            {
                return;
            }

            if (leftKnows != rightKnows)
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.KnowledgeAsymmetry,
                    leftRole,
                    rightRole,
                    KnowsAgainstIgnorant,
                    Name(context, leftKnows ? left : right) + " knows what happened and "
                    + Name(context, leftKnows ? right : left) + " does not"));
                return;
            }

            bool leftProves = knowledge.CanProve(left, focus.Id);
            bool rightProves = knowledge.CanProve(right, focus.Id);
            if (leftProves != rightProves)
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.KnowledgeAsymmetry,
                    leftRole,
                    rightRole,
                    ProofAgainstBelief,
                    Name(context, leftProves ? left : right) + " can prove it and "
                    + Name(context, leftProves ? right : left) + " can only say it"));
            }

            double gap = Math.Abs(ConfidenceOf(knowledge, left, focus.Id) - ConfidenceOf(knowledge, right, focus.Id));
            if (gap > 0.0)
            {
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.KnowledgeAsymmetry,
                    leftRole,
                    rightRole,
                    ConfidenceGap * gap,
                    "they are not equally sure of it (confidence gap "
                    + gap.ToString("0.00", CultureInfo.InvariantCulture) + ")"));
            }
        }

        // -- power asymmetry -------------------------------------------------------------------

        /// <summary>
        /// One of them can do something to the other that the other cannot do back.
        ///
        /// Three sources, all of them relations. The graph carries the personal ones - a debt, an
        /// employment - and the derived identity carries the institutional ones: an office one of
        /// them holds and the other does not, and a service one of them actually runs.
        ///
        /// Every identity term is a *difference*, which is the whole of the anti-stereotype
        /// argument here. Two guards score nothing, two nobodies score nothing, and the term is
        /// symmetric in which of the two holds the office - so it can prefer a group where power
        /// is unequal, and can never prefer a person for being the kind of person who holds power.
        /// A debt and an employment are also counted by shared history, deliberately: that they
        /// have a past and that the past is unequal are two different facts about the same scene.
        /// </summary>
        private static void PowerAsymmetry(
            StoryletCastingContext context,
            string leftRole,
            EntityId left,
            string rightRole,
            EntityId right,
            ChemistryIdentityCache identities,
            List<ChemistryReason> reasons)
        {
            RelationshipGraph graph = context.World.Relationships;
            Leverage(context, leftRole, left, rightRole, right, graph.Find(left, right), reasons);
            Leverage(context, rightRole, right, leftRole, left, graph.Find(right, left), reasons);

            IdentityAffordances leftIdentity = identities.Of(context, left);
            IdentityAffordances rightIdentity = identities.Of(context, right);

            Office(leftRole, leftIdentity, rightRole, rightIdentity, IdentityRole.Authority, OfficeOverNobody, reasons);
            Office(leftRole, leftIdentity, rightRole, rightIdentity, IdentityRole.GuildStanding, GuildStandingOverNobody, reasons);

            if (leftIdentity.Service.IsProvider != rightIdentity.Service.IsProvider)
            {
                IdentityAffordances provider = leftIdentity.Service.IsProvider ? leftIdentity : rightIdentity;
                reasons.Add(new ChemistryReason(
                    ChemistryDimension.PowerAsymmetry,
                    leftRole,
                    rightRole,
                    ServiceDependence,
                    "one of them runs a service the other does not - " + provider.Service.Describe()));
            }
        }

        private static void Office(
            string leftRole,
            IdentityAffordances left,
            string rightRole,
            IdentityAffordances right,
            IdentityRole role,
            double weight,
            List<ChemistryReason> reasons)
        {
            bool leftHolds = left.IsEligibleFor(role);
            if (leftHolds == right.IsEligibleFor(role))
            {
                // Both, or neither. Standing only becomes chemistry where it is unequal.
                return;
            }

            IdentityAffordances holder = leftHolds ? left : right;
            reasons.Add(new ChemistryReason(
                ChemistryDimension.PowerAsymmetry,
                leftRole,
                rightRole,
                weight,
                "one of them is entitled and the other is not - " + holder.ExplainEligibility(role)));
        }

        private static void Leverage(
            StoryletCastingContext context,
            string holderRole,
            EntityId holder,
            string overRole,
            EntityId over,
            RelationshipEdge edge,
            List<ChemistryReason> reasons)
        {
            if (edge == null)
            {
                return;
            }

            double weight;
            switch (edge.Kind)
            {
                case RelationKind.Creditor:
                    weight = DebtLeverage;
                    break;
                case RelationKind.Employer:
                    weight = EmploymentLeverage;
                    break;
                default:
                    return;
            }

            reasons.Add(new ChemistryReason(
                ChemistryDimension.PowerAsymmetry,
                holderRole,
                overRole,
                weight,
                Name(context, holder) + " holds " + edge.Kind.ToString().ToLowerInvariant()
                + "'s leverage over " + Name(context, over)));
        }

        // -- the numbers -----------------------------------------------------------------------

        // Small, bounded and all in one place, so the model can be argued with rather than
        // reverse-engineered. Their absolute size means nothing; only their size relative to each
        // other, because the total is never compared to anything but another group's total.
        private const double GoalAimedAtPerson = 0.90;
        private const double ContestedOpposedAim = 0.70;
        private const double ContestedSameAim = 0.40;
        private const double SentimentWeight = 0.50;
        private const double MismatchedSentiment = 0.45;
        private const double UnreturnedTie = 0.20;
        private const double SharedTrade = 0.25;
        private const double KnowsAgainstIgnorant = 0.50;
        private const double ProofAgainstBelief = 0.40;
        private const double ConfidenceGap = 0.30;
        private const double OfficeOverNobody = 0.50;
        private const double GuildStandingOverNobody = 0.30;
        private const double ServiceDependence = 0.20;
        private const double DebtLeverage = 0.40;
        private const double EmploymentLeverage = 0.35;

        /// <summary>
        /// What a tie is worth before its sentiment is counted. Enmity, rivalry and debt carry a
        /// scene on their own; an acquaintance is barely a fact.
        /// </summary>
        private static double KindWeight(RelationKind kind)
        {
            switch (kind)
            {
                case RelationKind.Enemy:
                    return 0.90;
                case RelationKind.Rival:
                    return 0.80;
                case RelationKind.Creditor:
                case RelationKind.Debtor:
                    return 0.70;
                case RelationKind.Accomplice:
                    return 0.65;
                case RelationKind.Spouse:
                    return 0.55;
                case RelationKind.Family:
                    return 0.50;
                case RelationKind.Employer:
                case RelationKind.Employee:
                    return 0.45;
                case RelationKind.Friend:
                    return 0.40;
                case RelationKind.GuildMate:
                    return 0.25;
                default:
                    return 0.15;
            }
        }

        /// <summary>A tie that already has an edge to it, which is what a shared trade sharpens.</summary>
        private static bool IsCharged(RelationKind kind)
        {
            return kind == RelationKind.Enemy
                   || kind == RelationKind.Rival
                   || kind == RelationKind.Creditor
                   || kind == RelationKind.Debtor
                   || kind == RelationKind.Accomplice;
        }

        private static RelationshipEdge Stronger(RelationshipEdge left, RelationshipEdge right)
        {
            if (left == null)
            {
                return right;
            }

            if (right == null)
            {
                return left;
            }

            double leftWeight = KindWeight(left.Kind) + (Math.Abs(left.Sentiment) / 100.0 * SentimentWeight);
            double rightWeight = KindWeight(right.Kind) + (Math.Abs(right.Sentiment) / 100.0 * SentimentWeight);
            if (Math.Abs(leftWeight - rightWeight) > Epsilon)
            {
                return leftWeight > rightWeight ? left : right;
            }

            // Ties are broken by id so the sentence a report prints does not depend on which way
            // the pair happened to be walked.
            return string.CompareOrdinal(left.From.Value, right.From.Value) <= 0 ? left : right;
        }

        private static bool SharedDomain(
            IdentityAffordances left,
            IdentityAffordances right,
            out IdentityDomain shared,
            out double overlap)
        {
            shared = IdentityDomain.Cultivation;
            overlap = 0.0;
            for (int i = 0; i < left.PlausibleKnowledge.Count; i++)
            {
                IdentityDomain domain = left.PlausibleKnowledge[i].Domain;
                double both = Math.Min(
                    left.PlausibleKnowledgeOf(domain),
                    right.PlausibleKnowledgeOf(domain));
                if (both > overlap)
                {
                    overlap = both;
                    shared = domain;
                }
            }

            return overlap > 0.0;
        }

        private static double ConfidenceOf(KnowledgeGraph knowledge, EntityId knower, EntityId factId)
        {
            KnowledgeRecord record;
            return knowledge.TryGetBelief(knower, factId, out record) ? record.Confidence : 0.0;
        }

        private static string Describe(StoryletCastingContext context, RelationshipEdge edge)
        {
            return Name(context, edge.From) + " is " + Name(context, edge.To) + "'s "
                   + edge.Kind.ToString().ToLowerInvariant() + " (sentiment "
                   + edge.Sentiment.ToString(CultureInfo.InvariantCulture) + ")";
        }

        private static string Name(StoryletCastingContext context, EntityId id)
        {
            return context.World.Registry.NameOf(id);
        }
    }

    /// <summary>
    /// The derived identity of everybody in one casting pass, read once.
    ///
    /// <see cref="IdentityAffordances.Of(NarrativeNpc, IVanillaState)"/> asks the seam for a fresh
    /// observation every time it is called, and chemistry asks about the same handful of people
    /// once per candidate group. Holding the answers for exactly one pass is the same bargain
    /// <see cref="StoryletCastingContext.Household"/> already makes: the read is live, and it is
    /// live at one instant rather than at a hundred slightly different ones - which is also what
    /// keeps the score deterministic while the world underneath is not frozen.
    /// </summary>
    public sealed class ChemistryIdentityCache
    {
        private readonly Dictionary<EntityId, IdentityAffordances> _derived =
            new Dictionary<EntityId, IdentityAffordances>();

        public IdentityAffordances Of(StoryletCastingContext context, EntityId actor)
        {
            IdentityAffordances affordances;
            if (_derived.TryGetValue(actor, out affordances))
            {
                return affordances;
            }

            NarrativeNpc npc = context.World.Registry.GetNpc(actor);
            affordances = npc == null
                ? IdentityAffordances.Nothing
                : IdentityAffordances.Of(npc, context.Vanilla);
            _derived[actor] = affordances;
            return affordances;
        }
    }
}
