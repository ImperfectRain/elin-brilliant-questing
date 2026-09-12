using System;
using System.Collections.Generic;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// The vocabulary of state changes a registered verb can potentially advance (BQa-010).
    ///
    /// Read off the verbs that exist rather than designed as a description of the world. Every
    /// key below is here because some registered verb already does that to authoritative state -
    /// an object changes hands, a shortage is answered, somebody comes to know a thing, proof
    /// stops being producible - and nothing is here because a world ontology would want it. That
    /// is the difference between a vocabulary a later verb can join and a table somebody has to
    /// keep in step with the library.
    ///
    /// <see cref="ActionFamily"/> is not this. A family says which kind of character has a route
    /// into a situation, which is a generation-side diversity target; an effect says what would
    /// be different afterwards, which is what a goal can be matched against. `bribe` and
    /// `buy_supplies` are both Economic and only one of them feeds anybody.
    ///
    /// It is potential capability and never a prediction. <see cref="NarrativeAction.GetAvailability"/>
    /// still decides whether the verb makes sense here, the check and the seam still decide what
    /// actually happens, and a failed attempt may do something else entirely.
    /// </summary>
    public static class SemanticEffects
    {
        /// <summary>An object changes hands, or the record of whose it is moves.</summary>
        public const string PossessionTransferred = "possession.transferred";

        /// <summary>A recorded shortage is answered, by goods, by money or by mending its cause.</summary>
        public const string ResourceSupplied = "resource.supplied";

        /// <summary>An undertaking or debt is created, spent, discharged or forgiven.</summary>
        public const string ObligationAltered = "obligation.altered";

        /// <summary>The actor comes to know something they did not.</summary>
        public const string InformationLearned = "information.learned";

        /// <summary>Somebody else comes to know something because the actor said or showed it.</summary>
        public const string InformationDisclosed = "information.disclosed";

        /// <summary>
        /// Somebody else comes to hold a claim less firmly because the actor denied it.
        ///
        /// Separate from <see cref="InformationDisclosed"/> because it is the opposite change to
        /// the same belief, and a want that a person be told something must never be offered the
        /// verb that would talk them out of it (BQa-011).
        /// </summary>
        public const string InformationDenied = "information.denied";

        /// <summary>Something that can be shown comes to exist, true or manufactured.</summary>
        public const string EvidenceCreated = "evidence.created";

        /// <summary>Something that could be shown stops being producible. Belief is untouched.</summary>
        public const string EvidenceRemoved = "evidence.removed";

        /// <summary>A person is put out of reach of what threatens them, or held so they cannot act.</summary>
        public const string PersonSecured = "person.secured";

        /// <summary>A person's safety is reduced.</summary>
        public const string PersonHarmed = "person.harmed";

        /// <summary>How somebody stands with somebody else changes.</summary>
        public const string StandingAltered = "standing.altered";

        /// <summary>What a place or a route lets somebody reach changes.</summary>
        public const string AccessAltered = "access.altered";

        /// <summary>A thing somebody depends on is put back into service.</summary>
        public const string ObjectRepaired = "object.repaired";

        /// <summary>A thing somebody depends on is taken out of service.</summary>
        public const string ObjectDamaged = "object.damaged";

        private static readonly string[] Vocabulary =
        {
            PossessionTransferred,
            ResourceSupplied,
            ObligationAltered,
            InformationLearned,
            InformationDisclosed,
            InformationDenied,
            EvidenceCreated,
            EvidenceRemoved,
            PersonSecured,
            PersonHarmed,
            StandingAltered,
            AccessAltered,
            ObjectRepaired,
            ObjectDamaged
        };

        /// <summary>The whole vocabulary, in a stable order.</summary>
        public static IReadOnlyList<string> All => Vocabulary;

        public static bool IsRegistered(string kind)
        {
            for (int i = 0; i < Vocabulary.Length; i++)
            {
                if (string.Equals(Vocabulary[i], kind, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The named slots of <see cref="ActionBinding"/>, as a vocabulary a verb can require rather
    /// than as a switch on verb ids.
    ///
    /// A verb that means nothing without being pointed at something says which slots would point
    /// it - "any one of these is enough" - and both the availability body and the player-facing
    /// projection read that one declaration. What is bound is still the binding's business; this
    /// only names the question.
    /// </summary>
    public static class SemanticSlots
    {
        /// <summary>The claim this attempt is about.</summary>
        public const string Proposition = "proposition";

        /// <summary>The object this attempt is about.</summary>
        public const string Item = "item";

        /// <summary>Where this attempt is meant to get somebody or something.</summary>
        public const string Destination = "destination";

        /// <summary>A stated purpose, when the caller supplied one in as many words.</summary>
        public const string Purpose = "purpose";

        /// <summary>Whether that slot carries something in this binding.</summary>
        public static bool IsBound(string slot, ActionBinding binding)
        {
            if (binding == null)
            {
                return false;
            }

            switch (slot)
            {
                case Proposition:
                    return binding.HasProposition;
                case Item:
                    return binding.HasItem;
                case Destination:
                    return binding.HasDestination;
                case Purpose:
                    return !string.IsNullOrEmpty(binding.Purpose);
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// One kind of state change a verb could advance, and what the live build has to be able to
    /// do before that half of it is on offer at all.
    ///
    /// The evidence question is <see cref="SpatialRouteClaim"/>'s, asked about a different
    /// primitive and answered by the same gate: is there any build on which this could happen?
    /// It is deliberately not <see cref="NarrativeAction.GetAvailability"/>, which asks whether
    /// this attempt makes sense here, now, against this world and this actor. A descriptor read
    /// with no world at all still has to be honest about a verb whose effect only exists because
    /// vanilla carries it, which is why a delegated effect names its capabilities here rather
    /// than leaving the reader to find the refusal inside an availability body.
    /// </summary>
    public sealed class ActionEffect
    {
        private static readonly VanillaCapability[] Nothing = new VanillaCapability[0];

        private ActionEffect(string kind, RouteEvidence evidence, string leansOn, IReadOnlyList<VanillaCapability> needs)
        {
            if (string.IsNullOrEmpty(kind))
            {
                throw new ArgumentException("An effect names a registered kind.", nameof(kind));
            }

            Kind = kind;
            Evidence = evidence;
            LeansOn = leansOn ?? string.Empty;
            Needs = needs ?? Nothing;
        }

        /// <summary>
        /// BQ owns the record outright: knowledge, belief, standing, obligation, a fact going
        /// stale. Nothing on the live build has to exist for it, so it is offered everywhere.
        /// </summary>
        public static ActionEffect Recorded(string kind)
        {
            return new ActionEffect(kind, RouteEvidence.BqAuthored, string.Empty, Nothing);
        }

        /// <summary>
        /// The change is carried by a write vanilla performs. <paramref name="leansOn"/> names
        /// that write and <paramref name="needs"/> the capabilities the adapter must advertise
        /// before this effect may be claimed at all.
        /// </summary>
        public static ActionEffect Delegated(string kind, string leansOn, params VanillaCapability[] needs)
        {
            return new ActionEffect(
                kind,
                RouteEvidence.SourceObserved,
                leansOn,
                new List<VanillaCapability>(needs ?? Nothing).AsReadOnly());
        }

        /// <summary>The vocabulary key from <see cref="SemanticEffects"/>.</summary>
        public string Kind { get; }

        public RouteEvidence Evidence { get; }

        /// <summary>The vanilla write this leans on, or empty for a BQ-owned record.</summary>
        public string LeansOn { get; }

        /// <summary>Capabilities the adapter must advertise before this effect can be claimed.</summary>
        public IReadOnlyList<VanillaCapability> Needs { get; }

        /// <summary>Whether any part of this effect is somebody else's write.</summary>
        public bool IsDelegated => Needs.Count > 0;

        /// <summary>
        /// Whether this build could carry the effect, and if not, which part is missing. Shares
        /// the BQ-090 gate, so a build that cannot transfer items refuses a possession effect in
        /// the same words a route on the same capability would.
        /// </summary>
        public bool CanBeCarried(IVanillaState vanilla, out string refusal)
        {
            return SpatialRouteClaim.CanLeanOn(vanilla, Evidence, LeansOn, Needs, out refusal);
        }

        public override string ToString()
        {
            return IsDelegated ? Kind + " via " + LeansOn : Kind;
        }
    }

    /// <summary>
    /// One verb's whole semantic declaration: what it could advance, and what it has to be
    /// pointed at before it means anything.
    ///
    /// Declared by the verb for the reason <see cref="SpatialRouteClaim"/> is: a table of effects
    /// kept beside the library is the hand-kept copy that drifts, and the drift is silent -
    /// nothing fails, a goal simply stops finding a route that exists. Registering a verb is the
    /// whole of adding one.
    ///
    /// <see cref="Undeclared"/> is not "this verb changes nothing". It is a coverage gap, and
    /// <see cref="ActionRegistry.EffectCoverage"/> reports it as one rather than letting a silent
    /// default read as a finished answer.
    /// </summary>
    public sealed class ActionEffects
    {
        private static readonly ActionEffect[] NoEffects = new ActionEffect[0];
        private static readonly string[] NoSlots = new string[0];

        /// <summary>Nothing has been said about this verb yet. The default, and a reported gap.</summary>
        public static readonly ActionEffects Undeclared = new ActionEffects(false, NoEffects, NoSlots);

        private ActionEffects(bool declared, IReadOnlyList<ActionEffect> effects, IReadOnlyList<string> needsAnyOf)
        {
            IsDeclared = declared;
            Effects = effects;
            NeedsAnyOf = needsAnyOf;
        }

        /// <summary>Declares what this verb could advance.</summary>
        public static ActionEffects Declaring(params ActionEffect[] effects)
        {
            if (effects == null || effects.Length == 0)
            {
                throw new ArgumentException(
                    "A declaration names at least one effect; a verb with nothing to say stays Undeclared so the gap is reported.",
                    nameof(effects));
            }

            ActionEffect[] copy = new ActionEffect[effects.Length];
            for (int i = 0; i < effects.Length; i++)
            {
                copy[i] = effects[i] ?? throw new ArgumentException("An effect cannot be null.", nameof(effects));
            }

            return new ActionEffects(true, copy, NoSlots);
        }

        /// <summary>
        /// Adds the binding slots this verb has to be pointed at through: any one of them is
        /// enough, and a verb naming none can be attempted unbound.
        /// </summary>
        public ActionEffects NeedingAnyOf(params string[] slots)
        {
            if (slots == null || slots.Length == 0)
            {
                return this;
            }

            string[] copy = new string[slots.Length];
            Array.Copy(slots, copy, slots.Length);
            return new ActionEffects(IsDeclared, Effects, copy);
        }

        /// <summary>False when nobody has declared this verb's effects yet.</summary>
        public bool IsDeclared { get; }

        public IReadOnlyList<ActionEffect> Effects { get; }

        /// <summary>The binding slots, any one of which points this verb at something.</summary>
        public IReadOnlyList<string> NeedsAnyOf { get; }

        public ActionEffect Find(string kind)
        {
            for (int i = 0; i < Effects.Count; i++)
            {
                if (string.Equals(Effects[i].Kind, kind, StringComparison.Ordinal))
                {
                    return Effects[i];
                }
            }

            return null;
        }

        /// <summary>Whether this verb says it could advance that kind of change at all.</summary>
        public bool Advances(string kind) => Find(kind) != null;
    }

    /// <summary>
    /// What the registered library says about itself, and where it says nothing (BQa-010).
    ///
    /// Missing coverage is a reported answer rather than an absence, because the failure mode
    /// this contract exists to prevent is a goal quietly finding no route: a verb nobody declared
    /// and a vocabulary key no verb answers both look exactly like "there is nothing you could
    /// do" to a consumer that only asks what matches.
    /// </summary>
    public sealed class ActionEffectCoverage
    {
        internal ActionEffectCoverage(
            IReadOnlyList<NarrativeAction> undeclared,
            IReadOnlyList<string> declaredKinds,
            IReadOnlyList<string> unansweredKinds,
            IReadOnlyList<string> unregisteredKinds)
        {
            Undeclared = undeclared;
            DeclaredKinds = declaredKinds;
            UnansweredKinds = unansweredKinds;
            UnregisteredKinds = unregisteredKinds;
        }

        /// <summary>Registered verbs that have said nothing about what they could advance.</summary>
        public IReadOnlyList<NarrativeAction> Undeclared { get; }

        /// <summary>Every effect kind some registered verb claims, in vocabulary order.</summary>
        public IReadOnlyList<string> DeclaredKinds { get; }

        /// <summary>Vocabulary keys no registered verb answers. A want of this shape has no route.</summary>
        public IReadOnlyList<string> UnansweredKinds { get; }

        /// <summary>Declared keys outside the vocabulary - a typo, or a verb inventing a term.</summary>
        public IReadOnlyList<string> UnregisteredKinds { get; }

        public bool IsComplete => Undeclared.Count == 0 && UnansweredKinds.Count == 0 && UnregisteredKinds.Count == 0;
    }
}
