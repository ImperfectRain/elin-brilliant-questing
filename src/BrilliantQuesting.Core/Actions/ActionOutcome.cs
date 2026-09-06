using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// What an attempt produced: the roll, what the player is told, what history recorded, and the
    /// trace that explains all three.
    /// </summary>
    public sealed class ActionOutcome
    {
        public ActionOutcome(string actionId, CheckResult check, string narration)
        {
            ActionId = actionId;
            Check = check;
            Narration = narration ?? string.Empty;
            Events = new List<WorldEvent>();
            Notes = new List<string>();
        }

        public string ActionId { get; }

        /// <summary>Null for actions that resolve without a roll (paying a debt, handing back a ring).</summary>
        public CheckResult Check { get; }

        public CheckOutcome Outcome => Check?.Outcome ?? CheckOutcome.Pass;

        public bool Succeeded => Check == null || Check.Succeeded;

        public string Narration { get; }

        public List<WorldEvent> Events { get; }

        /// <summary>Free-form trace lines for the "why did that happen" inspector.</summary>
        public List<string> Notes { get; }

        /// <summary>
        /// Which embodiment branch this attempt took (BQ-093, `D021`), stamped by
        /// <see cref="NarrativeAction.Perform"/> from the verb's own declaration so that an
        /// outcome cannot describe a branch its verb did not take.
        ///
        /// It is on the outcome and printed by <see cref="Explain"/> because the roadmap asks for
        /// the choice to be *visible*: a reader of the inspector must be able to tell a physical
        /// result vanilla actually performed from one BQ resolved coarsely, without reading the
        /// verb's source.
        /// </summary>
        public ActorEmbodiment Embodiment { get; internal set; } = ActorEmbodiment.Narrative;

        public string Explain()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ActionId).Append(": ").Append(Narration);
            if (Embodiment != null && Embodiment.Mode != EmbodimentMode.Narrative)
            {
                sb.Append("\n  ").Append(Embodiment.Describe());
            }

            if (Check != null)
            {
                sb.Append("\n  ").Append(Check.Explain());
            }

            foreach (string note in Notes)
            {
                sb.Append("\n  - ").Append(note);
            }

            foreach (WorldEvent worldEvent in Events)
            {
                sb.Append("\n  * recorded ").Append(worldEvent.Type);
            }

            return sb.ToString();
        }
    }
}
