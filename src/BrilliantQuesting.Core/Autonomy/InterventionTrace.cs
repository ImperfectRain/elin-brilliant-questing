using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Autonomy
{
    /// <summary>
    /// One option the autonomy pass weighed: somebody, a verb, and a person to use it on
    /// (BQ-094).
    ///
    /// Scored even when a personal line takes it off the table, for the reason BQ-077 gives: the
    /// cost of a prohibition is only visible beside the score of the thing it took away.
    /// </summary>
    public sealed class InterventionOption
    {
        public InterventionOption(
            EntityId actor,
            EntityId target,
            string actionId,
            Availability availability,
            ProblemSolvingStyle style,
            double opportunity,
            double styleFit,
            double motive,
            double score,
            ProhibitionRuling ruling,
            IReadOnlyList<string> scoreTerms,
            string barred)
        {
            Actor = actor;
            Target = target;
            ActionId = actionId ?? string.Empty;
            Availability = availability;
            Style = style;
            Opportunity = opportunity;
            StyleFit = styleFit;
            Motive = motive;
            Score = score;
            Ruling = ruling;
            ScoreTerms = scoreTerms ?? new string[0];
            Barred = barred ?? string.Empty;
        }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        /// <summary>A registered <see cref="NarrativeAction.Id"/>. Never a style label.</summary>
        public string ActionId { get; }

        public Availability Availability { get; }

        /// <summary>The disposition this verb's family reads as, for the personality term.</summary>
        public ProblemSolvingStyle Style { get; }

        public double Opportunity { get; }

        public double StyleFit { get; }

        /// <summary>Why this actor cares. Zero means nothing here is theirs, and is disqualifying.</summary>
        public double Motive { get; }

        public double Score { get; }

        public ProhibitionRuling Ruling { get; }

        public IReadOnlyList<string> ScoreTerms { get; }

        /// <summary>
        /// What stops this off screen even though the verb itself allows it, or empty.
        ///
        /// A separate answer from <see cref="Availability"/> on purpose: availability is the
        /// verb's own verdict about the world, and this is the pass refusing to ask for something
        /// nobody has watched work. Conflating them would put words in the verb's mouth.
        /// </summary>
        public string Barred { get; }

        /// <summary>Whether this could have been chosen at all.</summary>
        public bool Eligible => Availability.IsAvailable && Motive > 0.0 && !Ruling.Forbids && Barred.Length == 0;

        public override string ToString()
        {
            return ActionId + " score " + Score.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                   + (Eligible ? string.Empty : " (not eligible)");
        }
    }

    /// <summary>
    /// Why the world did, or did not, take one matter up on its own (BQ-094).
    ///
    /// Transient and inspector-facing, exactly like <see cref="GoalFormationTrace"/> and
    /// <see cref="Integration.ActorActivity"/>: nothing here is persisted and nothing here is
    /// player knowledge. What survives a save is what actually happened - the acts the verb
    /// recorded, the ending, and the claim somebody can now make about it - and those are in the
    /// ledger and the knowledge graph like anybody else's.
    ///
    /// The separation is the point of the roadmap's "only the causally meaningful steps recorded,
    /// not the actor's working day". The working day is read, weighed and thrown away here; the
    /// deed is history.
    /// </summary>
    public sealed class InterventionTrace
    {
        public InterventionTrace(EntityId threadId, string archetypeId)
        {
            ThreadId = threadId;
            ArchetypeId = archetypeId ?? string.Empty;
            Options = new List<InterventionOption>();
            Opportunities = new List<InterventionOpportunity>();
        }

        public EntityId ThreadId { get; }

        public string ArchetypeId { get; }

        /// <summary>Why nobody acted, or empty when somebody did.</summary>
        public string Refusal { get; set; } = string.Empty;

        public List<InterventionOpportunity> Opportunities { get; }

        public List<InterventionOption> Options { get; }

        /// <summary>What was attempted, or null when nothing was.</summary>
        public ActionAttempt Attempt { get; set; }

        /// <summary>Whether the matter ended because of this attempt.</summary>
        public bool Resolved { get; set; }

        /// <summary>The outcome name history recorded, when the matter ended.</summary>
        public string Resolution { get; set; } = string.Empty;

        /// <summary>The claim the actor can now make about having ended it, when there is one.</summary>
        public EntityId SettledFactId { get; set; }

        public bool Acted => Attempt != null;

        public string Describe(NarrativeWorldState world)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("matter: ").Append(ArchetypeId);
            if (!Acted)
            {
                sb.Append("\n  nobody took it up: ").Append(Refusal.Length > 0 ? Refusal : "no reason recorded");
            }

            for (int i = 0; i < Opportunities.Count; i++)
            {
                InterventionOpportunity opportunity = Opportunities[i];
                sb.Append("\n  ").Append(world == null ? opportunity.Actor.ToString() : world.Registry.NameOf(opportunity.Actor));
                sb.Append(": ").Append(opportunity.Describe().Replace("\n", "\n    "));
            }

            for (int i = 0; i < Options.Count; i++)
            {
                InterventionOption option = Options[i];
                sb.Append("\n  option ").Append(option);
                for (int t = 0; t < option.ScoreTerms.Count; t++)
                {
                    sb.Append("\n      ").Append(option.ScoreTerms[t]);
                }

                if (!option.Availability.IsAvailable)
                {
                    sb.Append("\n      ").Append(option.Availability);
                }

                if (option.Ruling.Held)
                {
                    sb.Append("\n      ").Append(option.Ruling);
                }

                if (option.Barred.Length > 0)
                {
                    sb.Append("\n      not asked for off screen: ").Append(option.Barred);
                }
            }

            if (Attempt != null)
            {
                sb.Append("\n  ").Append(Attempt.Explain().Replace("\n", "\n  "));
            }

            if (Resolved)
            {
                sb.Append("\n  the matter ended: ").Append(Resolution);
                sb.Append("\n  and can now be said out loud by whoever ended it");
            }

            return sb.ToString();
        }
    }
}
