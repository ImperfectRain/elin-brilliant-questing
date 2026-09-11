using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Events
{
    /// <summary>
    /// What one causal reference on an event means.
    ///
    /// The roles exist because an event already carries a pile of untyped ids - `Related`,
    /// `Evidence`, `Witnesses` - and nothing said which of them, if any, was the reason the event
    /// happened. An accusation put the accused claim in `Related` beside everything else, so the
    /// only way back to the occurrence being accused of was to guess from position, timestamp or a
    /// neighbouring event. That guess is what this replaces.
    ///
    /// The vocabulary is deliberately four words. Affected entities are not here: `Actor`,
    /// `Target` and `Related` already own them, and a fifth role copying them into provenance
    /// would be a second answer to a question history already answers. What did not exist before
    /// is the *causal* reading, and that is all this adds.
    /// </summary>
    public enum CausalRole
    {
        /// <summary>
        /// The occurrence that prompted this one. Objective: the recorder knows this happened and
        /// knows it led here. Not "the event before it in the list".
        /// </summary>
        Trigger,

        /// <summary>
        /// The occurrence this one is *about*, when that is not the one that prompted it. An
        /// accusation made today about a theft last month is triggered by whatever brought the
        /// accuser to the guard and is about the theft. Both can be present, and they can differ.
        ///
        /// A reference here says the event names that occurrence, never that the naming is true -
        /// truth belongs to the <see cref="Knowledge.Fact"/> and to nothing else.
        /// </summary>
        About,

        /// <summary>
        /// What the actor acted on: a claim they believe, a goal they hold. Motive evidence, and
        /// only ever that. A false claim is a perfectly good motive, so a reference here must
        /// never be read as an objective cause - which is exactly the distinction an inspector
        /// has to be able to draw.
        /// </summary>
        Motive,

        /// <summary>
        /// The record this occurrence produced: a fact, an obligation, a matter. The actual
        /// result, as opposed to what it was for.
        /// </summary>
        Outcome
    }

    /// <summary>One typed causal reference. A role and the id it points at, and nothing else.</summary>
    public readonly struct CausalLink : IEquatable<CausalLink>
    {
        public CausalLink(CausalRole role, EntityId reference)
        {
            if (reference.IsNone)
            {
                throw new ArgumentException(
                    "A causal link must reference something; unknown provenance is expressed by having no link.",
                    nameof(reference));
            }

            Role = role;
            Reference = reference;
        }

        public CausalRole Role { get; }

        public EntityId Reference { get; }

        public bool Equals(CausalLink other) => Role == other.Role && Reference == other.Reference;

        public override bool Equals(object obj) => obj is CausalLink other && Equals(other);

        public override int GetHashCode() => ((int)Role * 397) ^ Reference.GetHashCode();

        public override string ToString() => Role + " " + Reference;
    }

    /// <summary>
    /// Why a committed decision went the way it did, in reason codes.
    ///
    /// Bounded on purpose, and bounded structurally rather than by promise: a decision that wants
    /// to explain itself by copying the pressure list, the world snapshot or every candidate it
    /// turned down is refused rather than trimmed. History is not a debug log, and a record that
    /// can grow with the size of the world would make every save grow with it.
    ///
    /// Codes, not prose. Whitespace is rejected for the same reason the count is capped: a field
    /// that accepts a sentence eventually accepts a paragraph.
    /// </summary>
    public sealed class DecisionEvidence
    {
        public const int MaxReasons = 8;

        public const int MaxCodeLength = 64;

        private static readonly string[] NoReasons = new string[0];

        public DecisionEvidence(string decision, IReadOnlyList<string> reasons = null)
        {
            Decision = Code(decision, nameof(decision));

            if (reasons == null || reasons.Count == 0)
            {
                Reasons = NoReasons;
                return;
            }

            if (reasons.Count > MaxReasons)
            {
                throw new ArgumentException(
                    "A decision record carries at most " + MaxReasons + " reason codes; " + reasons.Count +
                    " were given. Retain the reasons that decided it, not everything that was considered.",
                    nameof(reasons));
            }

            string[] codes = new string[reasons.Count];
            for (int i = 0; i < reasons.Count; i++)
            {
                codes[i] = Code(reasons[i], nameof(reasons));
            }

            Reasons = codes;
        }

        /// <summary>The decision itself, as a code: "authority.rebounds", "theft.opportunity".</summary>
        public string Decision { get; }

        /// <summary>The inputs that decided it, as codes. Never a snapshot and never a candidate list.</summary>
        public IReadOnlyList<string> Reasons { get; }

        public override string ToString()
        {
            return Reasons.Count == 0 ? Decision : Decision + " (" + string.Join(", ", Reasons) + ")";
        }

        private static string Code(string value, string parameter)
        {
            string trimmed = value == null ? null : value.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                throw new ArgumentException("A decision record needs a non-empty code.", parameter);
            }

            if (trimmed.Length > MaxCodeLength)
            {
                throw new ArgumentException(
                    "Decision codes are at most " + MaxCodeLength + " characters; \"" + trimmed + "\" is longer.",
                    parameter);
            }

            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsWhiteSpace(trimmed[i]))
                {
                    throw new ArgumentException(
                        "Decision codes are codes, not prose: \"" + trimmed + "\" contains whitespace.",
                        parameter);
                }
            }

            return trimmed;
        }
    }

    /// <summary>
    /// The typed causal reading of one recorded transition: what prompted it, what it is about,
    /// what the actor acted on, what it produced, and - when a committed decision needs it - the
    /// reason codes that explain the choice after the world has moved on.
    ///
    /// <b>Unknown stays unknown.</b> An event with no links is not an event with no causes; it is
    /// an event whose causes were never recorded, which is what every event in every save written
    /// before this existed is. Nothing infers a cause from adjacency, timestamps or tags to fill
    /// the gap, and nothing rewrites a historical event to give it one.
    ///
    /// Distinct from <see cref="Continuity.ItemProvenance"/>, which is a live reading of what the
    /// ledger did with one *object*. This is durable, event-keyed, and recorded at the moment the
    /// transition is written, because by the time anyone asks, the reason is gone.
    /// </summary>
    public sealed class EventProvenance
    {
        /// <summary>
        /// A cap, not a budget to spend. Carrying several causes is expected; carrying twelve
        /// means something is being copied into history rather than referenced.
        /// </summary>
        public const int MaxLinks = 12;

        private static readonly CausalLink[] NoLinks = new CausalLink[0];

        /// <summary>Provenance that was never recorded. Shared: it holds nothing.</summary>
        public static readonly EventProvenance Unknown = new EventProvenance(null, null);

        public EventProvenance(IReadOnlyList<CausalLink> links, DecisionEvidence decision = null)
        {
            Decision = decision;

            if (links == null || links.Count == 0)
            {
                Links = NoLinks;
                return;
            }

            if (links.Count > MaxLinks)
            {
                throw new ArgumentException(
                    "An event carries at most " + MaxLinks + " causal links; " + links.Count + " were given.",
                    nameof(links));
            }

            List<CausalLink> kept = new List<CausalLink>(links.Count);
            for (int i = 0; i < links.Count; i++)
            {
                // The same reference in the same role twice says nothing the once did not.
                if (!kept.Contains(links[i]))
                {
                    kept.Add(links[i]);
                }
            }

            Links = kept.ToArray();
        }

        public IReadOnlyList<CausalLink> Links { get; }

        public DecisionEvidence Decision { get; }

        /// <summary>Nothing was recorded about why this happened.</summary>
        public bool IsUnknown => Links.Count == 0 && Decision == null;

        /// <summary>Starts a draft. References that are <see cref="EntityId.None"/> are simply not added.</summary>
        public static ProvenanceDraft Draft() => new ProvenanceDraft();

        public bool Has(CausalRole role) => !FirstReference(role).IsNone;

        /// <summary>The first reference in this role, or <see cref="EntityId.None"/> when there is none.</summary>
        public EntityId FirstReference(CausalRole role)
        {
            for (int i = 0; i < Links.Count; i++)
            {
                if (Links[i].Role == role)
                {
                    return Links[i].Reference;
                }
            }

            return EntityId.None;
        }

        /// <summary>Every reference in this role, in the order they were recorded.</summary>
        public IReadOnlyList<EntityId> References(CausalRole role)
        {
            List<EntityId> found = null;
            for (int i = 0; i < Links.Count; i++)
            {
                if (Links[i].Role != role)
                {
                    continue;
                }

                found ??= new List<EntityId>(2);
                found.Add(Links[i].Reference);
            }

            return found == null ? (IReadOnlyList<EntityId>)EmptyIds : found;
        }

        private static readonly EntityId[] EmptyIds = new EntityId[0];

        public override string ToString()
        {
            if (IsUnknown)
            {
                return "unknown provenance";
            }

            List<string> parts = new List<string>(Links.Count + 1);
            for (int i = 0; i < Links.Count; i++)
            {
                parts.Add(Links[i].ToString());
            }

            if (Decision != null)
            {
                parts.Add("decided " + Decision);
            }

            return string.Join("; ", parts.ToArray());
        }
    }

    /// <summary>
    /// Builds an <see cref="EventProvenance"/> at a call site that may or may not know each part.
    ///
    /// An unknown reference is skipped rather than stored as nothing, so a recorder can write
    /// <c>.About(thread?.OriginEventId ?? EntityId.None)</c> without a branch and get honest
    /// silence when there is no matter to point at.
    /// </summary>
    public sealed class ProvenanceDraft
    {
        private readonly List<CausalLink> _links = new List<CausalLink>(3);
        private DecisionEvidence _decision;

        /// <summary>The occurrence that prompted this one.</summary>
        public ProvenanceDraft Trigger(EntityId eventId) => Add(CausalRole.Trigger, eventId);

        /// <summary>The occurrence this one names, when it is not the one that prompted it.</summary>
        public ProvenanceDraft About(EntityId eventId) => Add(CausalRole.About, eventId);

        /// <summary>The claim or goal the actor acted on. Motive evidence, never an objective cause.</summary>
        public ProvenanceDraft Motive(EntityId claimOrGoal) => Add(CausalRole.Motive, claimOrGoal);

        /// <summary>The record this occurrence produced.</summary>
        public ProvenanceDraft Outcome(EntityId record) => Add(CausalRole.Outcome, record);

        public ProvenanceDraft Decided(string decision, params string[] reasons)
        {
            _decision = new DecisionEvidence(decision, reasons);
            return this;
        }

        public EventProvenance Build()
        {
            return _links.Count == 0 && _decision == null
                ? EventProvenance.Unknown
                : new EventProvenance(_links, _decision);
        }

        private ProvenanceDraft Add(CausalRole role, EntityId reference)
        {
            if (!reference.IsNone)
            {
                _links.Add(new CausalLink(role, reference));
            }

            return this;
        }
    }
}
