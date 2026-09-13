using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// Where a goal is in its life, as one explicit state rather than as a boolean plus a
    /// convention.
    ///
    /// A goal that ends is retired here, never deleted: a merchant who gave up chasing his cargo
    /// still gave up chasing his cargo, and a later matter that wants to know why he stopped has
    /// nowhere else to read it. The three terminal states are not decoration - they are three
    /// different things to have happened, and a consumer handed one for another draws the wrong
    /// conclusion about the person.
    /// </summary>
    public enum GoalLifecycle
    {
        /// <summary>Still wanted. The only state that counts toward what an actor is pursuing.</summary>
        Active,

        /// <summary>
        /// The desired condition holds in authoritative state. Objective, and deliberately not the
        /// same question as whether its owner believes it - see <see cref="GoalAssessment"/>.
        /// </summary>
        Satisfied,

        /// <summary>Given up while the condition still did not hold.</summary>
        Abandoned,

        /// <summary>Replaced by another goal that took over the same want.</summary>
        Superseded
    }

    /// <summary>
    /// What the goal's owner believes about their own goal, which is a different question from
    /// whether it is objectively satisfied.
    ///
    /// The two are kept apart because another actor's deed can satisfy a condition without telling
    /// anybody. Collapsing them would hand the owner the world's answer for free, and an actor who
    /// stops wanting something the moment it becomes true behind their back is omniscient.
    /// </summary>
    public enum GoalAssessment
    {
        /// <summary>They have not formed a view. The default, and the honest one.</summary>
        Unknown,

        /// <summary>They believe it is still outstanding.</summary>
        BelievedUnmet,

        /// <summary>They believe it is done, whether or not it is.</summary>
        BelievedMet
    }

    /// <summary>What kind of thing caused a goal to exist.</summary>
    public enum GoalSourceKind
    {
        /// <summary>Nothing recorded a cause. Unknown stays unknown; nothing infers one later.</summary>
        Unknown,

        /// <summary>One actor's local reading of a pressure. The reference is that reading's id.</summary>
        ActorPressure,

        /// <summary>
        /// One body's reading of what it has legitimately received. The reference is that reading's
        /// id, and the record is the claim on file - which may be one somebody reported wrongly.
        /// </summary>
        InstitutionalReading,

        /// <summary>A record in authoritative state: an obligation, a fact, a demand.</summary>
        AuthoritativeRecord,

        /// <summary>A situation or fixture put it there when it established the scenario.</summary>
        Established
    }

    /// <summary>How a desired condition reads against authoritative state right now.</summary>
    public enum GoalConditionState
    {
        /// <summary>
        /// Nothing here can answer: no condition, an unregistered term, bindings that do not match
        /// the term's shape, or a referenced record that is not in the world. An unsupported desire
        /// stays inspectable and is never guessed into an answer.
        /// </summary>
        Unsupported,

        /// <summary>The condition does not hold.</summary>
        Unmet,

        /// <summary>The condition holds.</summary>
        Met
    }

    /// <summary>
    /// What one of a condition term's bindings <em>is</em>, so that something pointing a verb at
    /// it does not have to know the term's name (BQa-011).
    ///
    /// The term declares this because the term is the only thing that knows: "item" in
    /// <see cref="GoalConditionKinds.PropertyOwnedBy"/> is an object and "claim" in
    /// <see cref="GoalConditionKinds.ClaimUnproven"/> is a proposition, and a bridge that worked
    /// that out by reading the binding names would be a switch on term names wearing a helper's
    /// clothes. <see cref="Unknown"/> is the honest default for a term registered without saying:
    /// nothing is pointed at that binding, and the gap is reported rather than guessed.
    /// </summary>
    public enum GoalBindingRole
    {
        /// <summary>Nothing was declared. Nothing points a verb at it.</summary>
        Unknown,

        /// <summary>A thing. Fills <c>SemanticSlots.Item</c>.</summary>
        Object,

        /// <summary>A claim in the knowledge graph. Fills <c>SemanticSlots.Proposition</c>.</summary>
        Claim,

        /// <summary>
        /// A record in the obligation ledger. Read for the claim and the two parties it already
        /// names, rather than treated as a proposition it is not.
        /// </summary>
        Undertaking,

        /// <summary>A place. Fills <c>SemanticSlots.Destination</c>.</summary>
        Place,

        /// <summary>Somebody an attempt could be aimed at.</summary>
        Person
    }

    /// <summary>One binding a condition term requires: its name, and what kind of thing it is.</summary>
    public readonly struct GoalConditionSlot
    {
        public GoalConditionSlot(string name, GoalBindingRole role)
        {
            Name = GoalCodes.Require(name, nameof(name));
            Role = role;
        }

        public string Name { get; }

        public GoalBindingRole Role { get; }

        public override string ToString() => Name + ":" + Role;
    }

    /// <summary>One concrete binding of a condition term: a name from the term's shape and the id it points at.</summary>
    public readonly struct GoalBinding : IEquatable<GoalBinding>
    {
        public GoalBinding(string name, EntityId reference)
        {
            Name = GoalCodes.Require(name, nameof(name));
            if (reference.IsNone)
            {
                throw new ArgumentException(
                    "A goal condition binds concrete entities; an absent binding is expressed by a term that does not ask for one.",
                    nameof(reference));
            }

            Reference = reference;
        }

        public string Name { get; }

        public EntityId Reference { get; }

        public bool Equals(GoalBinding other) =>
            string.Equals(Name, other.Name, StringComparison.Ordinal) && Reference == other.Reference;

        public override bool Equals(object obj) => obj is GoalBinding other && Equals(other);

        public override int GetHashCode() => ((Name ?? string.Empty).GetHashCode() * 397) ^ Reference.GetHashCode();

        public override string ToString() => Name + "=" + Reference.Value;
    }

    /// <summary>
    /// The world-state condition that would satisfy a goal: a registered term plus the concrete
    /// entities it is about.
    ///
    /// A term and bindings rather than an expression, because an expression language is a second
    /// simulation nobody can test, and because "satisfied" then becomes something only the author
    /// of the string can evaluate. A term rather than the goal's <see cref="NpcGoal.Kind"/>,
    /// because a central switch on goal names is the thing BQa-010 and BQa-011 have to add verbs
    /// without touching.
    ///
    /// The shape is validated against <see cref="GoalConditionRegistry"/> when production forms a
    /// condition, and is not validated when a save is read: a save written by a build that knew a
    /// term this one does not must load, and the goal it belongs to stays an inspectable
    /// unsupported desire rather than being dropped or guessed.
    /// </summary>
    public sealed class GoalCondition
    {
        private static readonly GoalBinding[] NoBindings = new GoalBinding[0];

        public GoalCondition(string kind, IReadOnlyList<GoalBinding> bindings = null)
        {
            Kind = GoalCodes.Require(kind, nameof(kind));

            if (bindings == null || bindings.Count == 0)
            {
                Bindings = NoBindings;
                Key = Kind + "()";
                return;
            }

            GoalBinding[] ordered = new GoalBinding[bindings.Count];
            for (int i = 0; i < bindings.Count; i++)
            {
                ordered[i] = bindings[i];
            }

            Array.Sort(ordered, CompareByName);
            for (int i = 1; i < ordered.Length; i++)
            {
                if (string.Equals(ordered[i].Name, ordered[i - 1].Name, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Binding '" + ordered[i].Name + "' is bound twice; a name in a condition term names one entity.",
                        nameof(bindings));
                }
            }

            Bindings = ordered;

            System.Text.StringBuilder key = new System.Text.StringBuilder(Kind).Append('(');
            for (int i = 0; i < ordered.Length; i++)
            {
                if (i > 0) key.Append(';');
                key.Append(ordered[i].Name).Append('=').Append(ordered[i].Reference.Value);
            }

            Key = key.Append(')').ToString();
        }

        /// <summary>The registered term. Not the goal's <see cref="NpcGoal.Kind"/>, and not prose.</summary>
        public string Kind { get; }

        /// <summary>The entities this condition is about, in stable name order.</summary>
        public IReadOnlyList<GoalBinding> Bindings { get; }

        /// <summary>
        /// Stable text for this exact condition. Bindings are sorted, so two conditions built in
        /// different orders read the same - which is what makes deduplication survive a reload.
        /// </summary>
        public string Key { get; }

        public EntityId Reference(string name)
        {
            for (int i = 0; i < Bindings.Count; i++)
            {
                if (string.Equals(Bindings[i].Name, name, StringComparison.Ordinal))
                {
                    return Bindings[i].Reference;
                }
            }

            return EntityId.None;
        }

        public override string ToString() => Key;

        private static int CompareByName(GoalBinding left, GoalBinding right) =>
            string.CompareOrdinal(left.Name, right.Name);
    }

    /// <summary>The registered condition terms, as constants rather than as strings at call sites.</summary>
    public static class GoalConditionKinds
    {
        /// <summary>
        /// Bindings <c>item</c>, <c>owner</c>: the ownership record says that thing is that person's.
        ///
        /// Ownership, which is what the possession fact actually records - a stolen ring is still
        /// its owner's ring while the thief is carrying it. Whoever physically has it is a
        /// different question and this term does not answer it.
        /// </summary>
        public const string PropertyOwnedBy = "property.owned_by";

        /// <summary>Binding <c>obligation</c>: that undertaking is no longer outstanding.</summary>
        public const string ObligationDischarged = "obligation.discharged";

        /// <summary>
        /// Bindings <c>claim</c>, <c>subject</c>: nobody but the named person can prove that claim.
        /// The want behind "avoid exposure", stated as proof rather than as truth - a deed that
        /// happened stays happened, and what a person can act on is who can show it.
        /// </summary>
        public const string ClaimUnproven = "claim.unproven";

        /// <summary>Binding <c>person</c>: that person is alive.</summary>
        public const string PersonAlive = "person.alive";

        /// <summary>Bindings <c>place</c>, <c>source</c>: that shortage no longer presses at that place.</summary>
        public const string DemandRelieved = "demand.relieved";

        /// <summary>
        /// Bindings <c>claim</c>, <c>knower</c>: that person holds that claim.
        ///
        /// The want behind "they have to be told", stated as somebody coming to hold the claim
        /// rather than as a conversation happening. Whether the claim is true is not this term's
        /// question and deliberately not a condition of it: an actor who sincerely holds a false
        /// claim can want the reeve told, and the reeve genuinely comes to hold it.
        /// </summary>
        public const string InformationKnownBy = "information.known_by";
    }

    /// <summary>
    /// The vocabulary of desired conditions and how each one reads against authoritative state.
    ///
    /// Registered rather than switched, so that a later verb or a later condition is a registration
    /// and not an edit to a central table of goal names. Evaluation is a pure read: it answers
    /// what the world is holding and changes nothing, including the goal it was asked about.
    /// </summary>
    public static class GoalConditionRegistry
    {
        private static readonly Dictionary<string, Term> Terms = new Dictionary<string, Term>(StringComparer.Ordinal);

        static GoalConditionRegistry()
        {
            Register(
                GoalConditionKinds.PropertyOwnedBy,
                new[]
                {
                    new GoalConditionSlot("item", GoalBindingRole.Object),
                    new GoalConditionSlot("owner", GoalBindingRole.Person)
                },
                OwnedBy,
                new[] { SemanticEffects.PossessionTransferred });
            Register(
                GoalConditionKinds.ObligationDischarged,
                new[] { new GoalConditionSlot("obligation", GoalBindingRole.Undertaking) },
                ObligationDischarged,
                new[] { SemanticEffects.ObligationAltered });
            Register(
                GoalConditionKinds.ClaimUnproven,
                new[]
                {
                    new GoalConditionSlot("claim", GoalBindingRole.Claim),
                    new GoalConditionSlot("subject", GoalBindingRole.Person)
                },
                ClaimUnproven,
                // Removing what can be shown, and only that. Telling somebody makes a claim more
                // provable, not less, so disclosure is not a route to this condition however
                // much it is about the same claim.
                new[] { SemanticEffects.EvidenceRemoved });
            Register(
                GoalConditionKinds.PersonAlive,
                new[] { new GoalConditionSlot("person", GoalBindingRole.Person) },
                PersonAlive,
                new[] { SemanticEffects.PersonSecured });
            Register(
                GoalConditionKinds.DemandRelieved,
                new[]
                {
                    new GoalConditionSlot("place", GoalBindingRole.Place),
                    new GoalConditionSlot("source", GoalBindingRole.Claim)
                },
                DemandRelieved,
                new[] { SemanticEffects.ResourceSupplied });
            Register(
                GoalConditionKinds.InformationKnownBy,
                new[]
                {
                    new GoalConditionSlot("claim", GoalBindingRole.Claim),
                    new GoalConditionSlot("knower", GoalBindingRole.Person)
                },
                InformationKnownBy,
                // Somebody coming to hold the claim because it was put to them. Denying it to
                // them is the opposite change and has its own key, so a want to have somebody
                // told never offers the verb that would talk them out of it.
                new[] { SemanticEffects.InformationDisclosed });
        }

        /// <summary>
        /// Declares a condition term: the binding names it requires, the pure read that answers
        /// it, and which kinds of state change could move it toward holding (BQa-010).
        ///
        /// <paramref name="advancedBy"/> names <see cref="SemanticEffects"/> keys, never verbs.
        /// That indirection is the whole of "a new verb needs no central switch": a term says
        /// what sort of change would satisfy it, a verb says what sort of change it could make,
        /// and nothing in between holds a list of which verbs answer which wants. A term that
        /// names none is inspectable and simply has no declared route.
        /// </summary>
        public static void Register(
            string kind,
            IReadOnlyList<string> requiredBindings,
            Func<NarrativeWorldState, GoalCondition, GoalConditionState> evaluate,
            IReadOnlyList<string> advancedBy = null)
        {
            GoalConditionSlot[] slots = new GoalConditionSlot[requiredBindings == null ? 0 : requiredBindings.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GoalConditionSlot(requiredBindings[i], GoalBindingRole.Unknown);
            }

            Register(kind, slots, evaluate, advancedBy);
        }

        /// <summary>
        /// The same declaration, saying what each binding is (BQa-011).
        ///
        /// A term that says so can have a verb pointed at it by something that has never heard of
        /// the term; a term registered through the name-only overload above stays readable and
        /// evaluable and simply has no route built for it, which is a reported gap rather than a
        /// guess about what "widget" was supposed to mean.
        /// </summary>
        public static void Register(
            string kind,
            IReadOnlyList<GoalConditionSlot> requiredBindings,
            Func<NarrativeWorldState, GoalCondition, GoalConditionState> evaluate,
            IReadOnlyList<string> advancedBy = null)
        {
            string term = GoalCodes.Require(kind, nameof(kind));
            if (evaluate == null)
            {
                throw new ArgumentNullException(nameof(evaluate));
            }

            GoalConditionSlot[] slots = new GoalConditionSlot[requiredBindings == null ? 0 : requiredBindings.Count];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new GoalConditionSlot(requiredBindings[i].Name, requiredBindings[i].Role);
            }

            Array.Sort(slots, CompareSlotsByName);

            string[] names = new string[slots.Length];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = slots[i].Name;
            }

            string[] effects = new string[advancedBy == null ? 0 : advancedBy.Count];
            for (int i = 0; i < effects.Length; i++)
            {
                effects[i] = GoalCodes.Require(advancedBy[i], nameof(advancedBy));
            }

            Terms[term] = new Term(names, slots, evaluate, effects);
        }

        public static bool IsRegistered(string kind) => kind != null && Terms.ContainsKey(kind);

        /// <summary>The binding names a term requires, in stable order, or null when it is unregistered.</summary>
        public static IReadOnlyList<string> RequiredBindings(string kind)
        {
            return kind != null && Terms.TryGetValue(kind, out Term term) ? term.Bindings : null;
        }

        /// <summary>
        /// The same bindings with what each one is, in the same order, or null when the term is
        /// unregistered (BQa-011). A slot of <see cref="GoalBindingRole.Unknown"/> is a term that
        /// never said, not a term whose binding means nothing.
        /// </summary>
        public static IReadOnlyList<GoalConditionSlot> Slots(string kind)
        {
            return kind != null && Terms.TryGetValue(kind, out Term term) ? term.Slots : null;
        }

        /// <summary>
        /// The effect kinds that could move this term toward holding, or null when it is
        /// unregistered. Empty is a real answer: the term is known and nothing has been said
        /// about how it would ever be satisfied.
        /// </summary>
        public static IReadOnlyList<string> AdvancedBy(string kind)
        {
            return kind != null && Terms.TryGetValue(kind, out Term term) ? term.AdvancedBy : null;
        }

        /// <summary>Every registered term, in stable order.</summary>
        public static IReadOnlyList<string> RegisteredKinds()
        {
            List<string> kinds = new List<string>(Terms.Keys);
            kinds.Sort(StringComparer.Ordinal);
            return kinds;
        }

        /// <summary>
        /// Builds a condition that the vocabulary admits. Production goal formation goes through
        /// here so that an unregistered term or a mis-shaped binding set is refused where it is
        /// written rather than read back later as a desire nothing can answer.
        /// </summary>
        public static GoalCondition Create(string kind, params GoalBinding[] bindings)
        {
            GoalCondition condition = new GoalCondition(kind, bindings);
            if (!Terms.TryGetValue(condition.Kind, out Term term))
            {
                throw new ArgumentException(
                    "'" + condition.Kind + "' is not a registered goal condition term.", nameof(kind));
            }

            if (!Matches(term, condition))
            {
                throw new ArgumentException(
                    "'" + condition.Kind + "' requires exactly the bindings [" + string.Join(", ", term.Bindings) + "].",
                    nameof(bindings));
            }

            return condition;
        }

        /// <summary>Whether this condition's term is registered and its bindings match that term's shape.</summary>
        public static bool IsSupported(GoalCondition condition)
        {
            return condition != null
                   && Terms.TryGetValue(condition.Kind, out Term term)
                   && Matches(term, condition);
        }

        /// <summary>
        /// Reads the condition against authoritative state. Side-effect free, and
        /// <see cref="GoalConditionState.Unsupported"/> rather than a guess whenever the question
        /// cannot honestly be answered.
        /// </summary>
        public static GoalConditionState Evaluate(NarrativeWorldState world, GoalCondition condition)
        {
            if (world == null || !IsSupported(condition))
            {
                return GoalConditionState.Unsupported;
            }

            return Terms[condition.Kind].Evaluate(world, condition);
        }

        private static bool Matches(Term term, GoalCondition condition)
        {
            if (term.Bindings.Count != condition.Bindings.Count)
            {
                return false;
            }

            for (int i = 0; i < term.Bindings.Count; i++)
            {
                if (!string.Equals(term.Bindings[i], condition.Bindings[i].Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static GoalConditionState OwnedBy(NarrativeWorldState world, GoalCondition condition)
        {
            EntityId recorded = Ownership.OwnerOf(world, condition.Reference("item"));
            if (recorded.IsNone)
            {
                // Nothing in the save says whose it is. That is not "somebody else's".
                return GoalConditionState.Unsupported;
            }

            return recorded == condition.Reference("owner") ? GoalConditionState.Met : GoalConditionState.Unmet;
        }

        private static GoalConditionState ObligationDischarged(NarrativeWorldState world, GoalCondition condition)
        {
            SocialObligation obligation = world.Obligations.Find(condition.Reference("obligation"));
            if (obligation == null)
            {
                return GoalConditionState.Unsupported;
            }

            return obligation.Status == SocialObligationStatus.Fulfilled
                   || obligation.Status == SocialObligationStatus.Forgiven
                ? GoalConditionState.Met
                : GoalConditionState.Unmet;
        }

        private static GoalConditionState ClaimUnproven(NarrativeWorldState world, GoalCondition condition)
        {
            EntityId claim = condition.Reference("claim");
            if (world.Knowledge.GetFact(claim) == null)
            {
                return GoalConditionState.Unsupported;
            }

            EntityId subject = condition.Reference("subject");
            foreach (EntityId knower in world.Knowledge.Knowers(claim))
            {
                if (knower != subject && world.Knowledge.CanProve(knower, claim))
                {
                    return GoalConditionState.Unmet;
                }
            }

            return GoalConditionState.Met;
        }

        private static GoalConditionState PersonAlive(NarrativeWorldState world, GoalCondition condition)
        {
            NarrativeNpc person = world.Registry.GetNpc(condition.Reference("person"));
            if (person == null)
            {
                return GoalConditionState.Unsupported;
            }

            return person.Alive ? GoalConditionState.Met : GoalConditionState.Unmet;
        }

        private static GoalConditionState DemandRelieved(NarrativeWorldState world, GoalCondition condition)
        {
            EntityId source = condition.Reference("source");
            if (world.Knowledge.GetFact(source) == null)
            {
                // The shortage is read off the record that states it; without that record there is
                // no question here, and "no pressure found" would answer it as relieved.
                return GoalConditionState.Unsupported;
            }

            foreach (LocalDemandPressure pressure in world.Demands.At(condition.Reference("place")))
            {
                if (pressure.SourceFactId == source && pressure.Active)
                {
                    return GoalConditionState.Unmet;
                }
            }

            return GoalConditionState.Met;
        }

        private static GoalConditionState InformationKnownBy(NarrativeWorldState world, GoalCondition condition)
        {
            EntityId claim = condition.Reference("claim");
            if (world.Knowledge.GetFact(claim) == null)
            {
                return GoalConditionState.Unsupported;
            }

            EntityId knower = condition.Reference("knower");
            if (!world.Registry.IsActor(knower))
            {
                return GoalConditionState.Unsupported;
            }

            // Holding the claim, not being convinced of it. Confidence is the disclosure owner's
            // business and a want to have somebody told is answered the moment they have been.
            return world.Knowledge.Knows(knower, claim) ? GoalConditionState.Met : GoalConditionState.Unmet;
        }

        private static int CompareSlotsByName(GoalConditionSlot left, GoalConditionSlot right) =>
            string.CompareOrdinal(left.Name, right.Name);

        private sealed class Term
        {
            internal Term(
                IReadOnlyList<string> bindings,
                IReadOnlyList<GoalConditionSlot> slots,
                Func<NarrativeWorldState, GoalCondition, GoalConditionState> evaluate,
                IReadOnlyList<string> advancedBy)
            {
                Bindings = bindings;
                Slots = slots;
                Evaluate = evaluate;
                AdvancedBy = advancedBy;
            }

            internal IReadOnlyList<string> Bindings { get; }

            internal IReadOnlyList<GoalConditionSlot> Slots { get; }

            internal IReadOnlyList<string> AdvancedBy { get; }

            internal Func<NarrativeWorldState, GoalCondition, GoalConditionState> Evaluate { get; }
        }
    }

    /// <summary>
    /// Why a goal exists, in references rather than in a copy.
    ///
    /// The pressure that caused a goal is derived, unsaved and recomputed every pass; storing it
    /// here would freeze one pass's reading into the save and make a goal disagree with the world
    /// it came from. What is stored is the identity of that reading, whether the world was
    /// objectively holding it too, and the record it was read from - enough to go back and look,
    /// and not enough to answer instead of looking.
    /// </summary>
    public sealed class GoalOrigin
    {
        public GoalOrigin(
            GoalSourceKind kind,
            string sourceId = "",
            string objectiveCauseId = "",
            EntityId recordId = default,
            GameTime formedAt = default)
        {
            Kind = kind;
            SourceId = sourceId ?? string.Empty;
            ObjectiveCauseId = objectiveCauseId ?? string.Empty;
            RecordId = recordId;
            FormedAt = formedAt;
        }

        /// <summary>
        /// The provenance of a goal formed from one person's reading of a pressure: that reading's
        /// id, the objective pressure behind it where there is one, and the record it focuses on.
        /// Ids only - the reading itself is derived and recomputed, and freezing a copy of it here
        /// would make the goal disagree with the next pass.
        /// </summary>
        public static GoalOrigin FromPressure(ActorLocalPressure pressure, GameTime formedAt = default)
        {
            if (pressure == null)
            {
                throw new ArgumentNullException(nameof(pressure));
            }

            return new GoalOrigin(
                GoalSourceKind.ActorPressure,
                pressure.Id,
                pressure.DevelopmentId,
                pressure.FocusFactId,
                formedAt);
        }

        /// <summary>
        /// The provenance of a goal formed from one body's reading of what it holds on file: that
        /// reading's id, the objective pressure behind it where there is one, and the claim it is
        /// about. Ids only, for the same reason the actor form gives - the reading is recomputed
        /// every pass, and a frozen copy here would make the goal disagree with the next one.
        /// </summary>
        public static GoalOrigin FromInstitution(OrganizationPressure pressure, GameTime formedAt = default)
        {
            if (pressure == null)
            {
                throw new ArgumentNullException(nameof(pressure));
            }

            return new GoalOrigin(
                GoalSourceKind.InstitutionalReading,
                pressure.Id,
                pressure.DevelopmentId,
                pressure.FocusFactId,
                formedAt);
        }

        public GoalSourceKind Kind { get; }

        /// <summary>The causing reading's id: an actor-local or institutional pressure id, or empty.</summary>
        public string SourceId { get; }

        /// <summary>
        /// The objective pressure behind it, where there is one. Empty is not a gap: a goal formed
        /// from a sincerely mistaken belief has no objective cause, and saying so is the point.
        /// </summary>
        public string ObjectiveCauseId { get; }

        /// <summary>The authoritative record it was read from: a fact, an obligation, a matter.</summary>
        public EntityId RecordId { get; }

        public GameTime FormedAt { get; }

        public bool HasObjectiveCause => ObjectiveCauseId.Length > 0;

        public override string ToString()
        {
            string text = Kind.ToString();
            if (SourceId.Length > 0) text += " " + SourceId;
            if (ObjectiveCauseId.Length > 0) text += " via " + ObjectiveCauseId;
            if (!RecordId.IsNone) text += " on " + RecordId.Value;
            return text;
        }
    }

    /// <summary>
    /// Codes, not prose: the same discipline the event ledger's decision evidence uses, for the
    /// same reason. A field that accepts a sentence eventually accepts a paragraph, and a
    /// paragraph is something a consumer starts parsing.
    /// </summary>
    internal static class GoalCodes
    {
        internal const int MaxLength = 64;

        internal static string Require(string value, string parameter)
        {
            string code = Normalize(value);
            if (code.Length == 0)
            {
                throw new ArgumentException("A code is required and cannot be blank.", parameter);
            }

            return code;
        }

        internal static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            if (trimmed.Length > MaxLength)
            {
                throw new ArgumentException(
                    "A code is at most " + MaxLength + " characters; '" + trimmed + "' is longer. Codes are identifiers, not explanations.");
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsWhiteSpace(trimmed[i]))
                {
                    throw new ArgumentException("A code carries no whitespace; '" + trimmed + "' does.");
                }
            }

            return trimmed;
        }
    }
}
