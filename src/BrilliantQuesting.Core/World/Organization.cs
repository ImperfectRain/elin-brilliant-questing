using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// A generated group - a smuggler crew, a farming family, a merchant association. These
    /// overlay vanilla guilds and factions rather than replacing them: the Thieves Guild is still
    /// the Thieves Guild, but the four people who actually fence goods in this town are ours.
    /// </summary>
    public sealed class Organization
    {
        public Organization(EntityId id, string name, string type)
        {
            Id = id;
            Name = name;
            Type = type;
            Goals = new List<OrganizationGoal>();
            MemberIds = new List<EntityId>();
            SiteIds = new List<EntityId>();
            Receipts = new InstitutionalReceiptLedger(id);
        }

        public EntityId Id { get; }

        public string Name { get; set; }

        /// <summary>Ontology term: "criminal_crew", "merchant_association", "family", "cult".</summary>
        public string Type { get; }

        public EntityId LeaderId { get; set; }

        public List<OrganizationGoal> Goals { get; }

        public List<EntityId> MemberIds { get; }

        public List<EntityId> SiteIds { get; }

        /// <summary>
        /// What this body has been told, and through which channel (BQa-018).
        ///
        /// The whole of what the institution knows. Members' private beliefs are theirs and are
        /// deliberately not readable from here: an unreported thing a member holds is a thing the
        /// body has not been told, however many of them hold it.
        /// </summary>
        public InstitutionalReceiptLedger Receipts { get; }

        /// <summary>Coarse band rather than a modelled treasury; see the economy scope limit.</summary>
        public int Wealth { get; set; }

        /// <summary>0..100. How openly it can operate, and whether authorities will act for it.</summary>
        public int Legitimacy { get; set; } = 50;

        /// <summary>0..100. How readily it answers a problem with violence.</summary>
        public int Aggression { get; set; } = 30;

        /// <summary>Last day this organization took an autonomous off-screen action.</summary>
        public GameTime LastActedAt { get; set; }

        public override string ToString() => Name + " [" + Type + ", " + MemberIds.Count + " members]";
    }

    /// <summary>
    /// Something an organization is trying to accomplish with its own resources.
    ///
    /// Carries the same machine-readable contract an individual want does (BQa-008): what would
    /// satisfy it, why it exists and where it is in its life. The same three questions, because a
    /// body that wants its cart back and a carter who wants his cart back want the same kind of
    /// thing, and giving the institution a private vocabulary for it would mean every consumer
    /// asking the question twice.
    ///
    /// <see cref="Progress"/> stays the organization's own coarse effort counter, which is a
    /// different question from whether the desired condition holds and is not evidence about it.
    /// </summary>
    public sealed class OrganizationGoal
    {
        private static readonly GoalOrigin UnknownOrigin = new GoalOrigin(GoalSourceKind.Unknown);

        public OrganizationGoal(
            string kind,
            EntityId subject,
            int weight,
            GoalCondition condition = null,
            GoalOrigin origin = null)
        {
            Kind = kind;
            Subject = subject;
            Weight = weight;
            Condition = condition;
            Origin = origin ?? UnknownOrigin;
            Identity = Kind + "|" + Subject.Value + "|" + (condition == null ? string.Empty : condition.Key);
        }

        public string Kind { get; }

        public EntityId Subject { get; }

        /// <summary>
        /// The world-state condition that would satisfy this, or null for a desire nothing can yet
        /// evaluate. Null is honest: an old save's organization goals and hand-established fixture
        /// goals genuinely have none, and inventing one would be guessing what the author meant.
        /// </summary>
        public GoalCondition Condition { get; }

        public bool HasCondition => Condition != null;

        /// <summary>Why this goal exists. Never null; unknown provenance says so explicitly.</summary>
        public GoalOrigin Origin { get; }

        /// <summary>What makes two organization goals the same want: kind, subject and condition.</summary>
        public string Identity { get; }

        public int Weight { get; set; }

        /// <summary>How much effort the body has put in. Not evidence that the condition holds.</summary>
        public int Progress { get; set; }

        public GoalLifecycle Lifecycle { get; private set; } = GoalLifecycle.Active;

        public bool IsActive => Lifecycle == GoalLifecycle.Active;

        /// <summary>When it was retired, for a goal that has been. Meaningless while active.</summary>
        public GameTime RetiredAt { get; private set; }

        /// <summary>Why it was retired, as a code: "condition_met", "lapsed", "reappraised".</summary>
        public string RetirementCode { get; private set; } = string.Empty;

        /// <summary>The <see cref="Identity"/> of the goal that took this one's place.</summary>
        public string SupersededBy { get; private set; } = string.Empty;

        /// <summary>
        /// The boolean view of the lifecycle the rest of the simulation has always used. Lifecycle
        /// is the authority and this is a projection of it.
        /// </summary>
        public bool Satisfied
        {
            get => Lifecycle == GoalLifecycle.Satisfied;
            set
            {
                if (value) Satisfy(RetiredAt);
                else if (Lifecycle == GoalLifecycle.Satisfied) Reopen();
            }
        }

        /// <summary>
        /// Reads the desired condition against authoritative state. A pure read: it reports what
        /// the world holds and never moves the goal's own lifecycle.
        /// </summary>
        public GoalConditionState Evaluate(NarrativeWorldState world) =>
            GoalConditionRegistry.Evaluate(world, Condition);

        /// <summary>The condition holds. Retires the goal without removing it from history.</summary>
        public void Satisfy(GameTime when, string code = "")
        {
            Retire(GoalLifecycle.Satisfied, when, code, string.Empty);
        }

        /// <summary>Given up on. The condition did not hold, and the want stops anyway.</summary>
        public void Abandon(GameTime when, string code = "")
        {
            Retire(GoalLifecycle.Abandoned, when, code, string.Empty);
        }

        /// <summary>Handed over to another goal, named by identity so the link survives a save.</summary>
        public void SupersedeWith(OrganizationGoal replacement, GameTime when, string code = "")
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            if (ReferenceEquals(replacement, this))
            {
                throw new ArgumentException("A goal cannot supersede itself.", nameof(replacement));
            }

            Retire(GoalLifecycle.Superseded, when, code, replacement.Identity);
        }

        /// <summary>Back to wanted, clearing the retirement that no longer applies.</summary>
        public void Reopen()
        {
            if (Lifecycle == GoalLifecycle.Active)
            {
                return;
            }

            Lifecycle = GoalLifecycle.Active;
            RetiredAt = default;
            RetirementCode = string.Empty;
            SupersededBy = string.Empty;
        }

        /// <summary>Puts back exactly what a save recorded, with no policy of its own.</summary>
        public void Restore(GoalLifecycle lifecycle, GameTime retiredAt, string retirementCode, string supersededBy)
        {
            Lifecycle = lifecycle;
            RetiredAt = lifecycle == GoalLifecycle.Active ? default : retiredAt;
            RetirementCode = lifecycle == GoalLifecycle.Active ? string.Empty : GoalCodes.Normalize(retirementCode);
            SupersededBy = lifecycle == GoalLifecycle.Superseded ? supersededBy ?? string.Empty : string.Empty;
        }

        private void Retire(GoalLifecycle lifecycle, GameTime when, string code, string supersededBy)
        {
            Lifecycle = lifecycle;
            RetiredAt = when;
            RetirementCode = GoalCodes.Normalize(code);
            SupersededBy = supersededBy;
        }

        public override string ToString()
        {
            string text = Kind + "(" + Subject + ") w" + Weight + " p" + Progress;
            if (Condition != null)
            {
                text += " wanting " + Condition.Key;
            }

            if (Origin.Kind != GoalSourceKind.Unknown)
            {
                text += " from " + Origin;
            }

            return text + (Lifecycle == GoalLifecycle.Active
                ? string.Empty
                : " [" + Lifecycle.ToString().ToLowerInvariant() + "]");
        }
    }
}
