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
            Changed = new List<string>();
            ContractViolations = new List<string>();
            Resolution = check == null || check.Succeeded ? ActionResolution.Succeeded : ActionResolution.Failed;
        }

        public string ActionId { get; }

        /// <summary>Null for actions that resolve without a roll (paying a debt, handing back a ring).</summary>
        public CheckResult Check { get; }

        public CheckOutcome Outcome => Check?.Outcome ?? CheckOutcome.Pass;

        /// <summary>
        /// Which of the three endings this was (BQa-013).
        ///
        /// Seeded from the roll, because that is what it meant before there was a third answer: a
        /// verb that resolves without one has done the thing, and one that rolled has done it or
        /// not. A verb then corrects it where the roll is not the whole story - a precondition
        /// that had gone, a native write the build would not carry - and
        /// <see cref="NarrativeAction.Perform"/> corrects it once more where the verb's own
        /// success claim was not borne out.
        /// </summary>
        public ActionResolution Resolution { get; private set; }

        /// <summary>
        /// Whether this was a success: the act happened and what the verb claims for success
        /// actually changed.
        ///
        /// It used to be read off the roll alone, which made every no-roll refusal - the purse
        /// that was empty, the item that would not move, the shortage that had already been
        /// answered - indistinguishable from the deed itself.
        /// </summary>
        public bool Succeeded => Resolution == ActionResolution.Succeeded;

        /// <summary>Whether the attempt never took place at all (BQa-013).</summary>
        public bool Refused => Resolution == ActionResolution.Refused;

        public string Narration { get; }

        public List<WorldEvent> Events { get; }

        /// <summary>Free-form trace lines for the "why did that happen" inspector.</summary>
        public List<string> Notes { get; }

        /// <summary>
        /// What this attempt actually moved, in the <see cref="SemanticEffects"/> vocabulary the
        /// verb declares its capability in (BQa-013).
        ///
        /// Written where the change is made rather than where the outcome is built, because the
        /// place that knows whether the transfer went through is the line after the transfer. It
        /// is the evidence behind <see cref="Succeeded"/>: a verb declaring that success means an
        /// object changed hands, whose native move was refused, has nothing to put here and is
        /// demoted rather than allowed to stand as a deed.
        /// </summary>
        public List<string> Changed { get; }

        /// <summary>
        /// Where this outcome said something its verb's declaration does not support (BQa-013),
        /// filled by <see cref="ActionPostconditionAudit"/> during
        /// <see cref="NarrativeAction.Perform"/>.
        ///
        /// Reported rather than thrown: a contract breach should be diagnosable in the inspector
        /// and assertable in a test, not a crash in somebody's save.
        /// </summary>
        public List<string> ContractViolations { get; }

        /// <summary>
        /// Records that this attempt never took place, and why (BQa-013).
        ///
        /// For the branch that discovers, at resolution time, that the thing the verb needed is
        /// not there: the purse that will not cover it, the item vanilla would not move, the
        /// shortage somebody else answered between the option being drawn and being taken. None
        /// of that is a failed attempt, and none of it may read as one.
        /// </summary>
        public ActionOutcome Refuse(string why)
        {
            Resolution = ActionResolution.Refused;
            if (!string.IsNullOrEmpty(why))
            {
                Notes.Add("nothing was attempted: " + why);
            }

            return this;
        }

        /// <summary>
        /// Records that this was performed and did not come off, for a verb whose ending is not
        /// decided by a roll - an authority that heard the report and would not act on it.
        /// </summary>
        public ActionOutcome Fail()
        {
            Resolution = ActionResolution.Failed;
            return this;
        }

        /// <summary>Records that this attempt moved that piece of state, and returns it.</summary>
        public ActionOutcome Change(string effectKind)
        {
            if (!string.IsNullOrEmpty(effectKind) && !Changed.Contains(effectKind))
            {
                Changed.Add(effectKind);
            }

            return this;
        }

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

        /// <summary>
        /// How much of the room was read when this was resolved (BQ-094), stamped by
        /// <see cref="NarrativeAction.Perform"/> from the context it ran in.
        ///
        /// Beside <see cref="Embodiment"/> because it answers the neighbouring question and has
        /// the same failure mode if it is left implicit: an outcome with no witnesses is somebody
        /// acting in an empty room when the room was looked at, and somebody acting where nobody
        /// was looking when it was not. Only one of those is evidence of anything.
        /// </summary>
        public ContextObservation Observation { get; internal set; } = ContextObservation.Observed;

        public string Explain()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(ActionId).Append(": ").Append(Narration);
            if (Resolution != ActionResolution.Succeeded)
            {
                sb.Append("\n  ").Append(Resolution == ActionResolution.Refused
                    ? "refused: the attempt did not take place"
                    : "failed: performed, and it did not come off");
            }

            if (Changed.Count > 0)
            {
                sb.Append("\n  changed: ").Append(string.Join(", ", Changed));
            }

            foreach (string violation in ContractViolations)
            {
                sb.Append("\n  ! contract: ").Append(violation);
            }

            if (Embodiment != null && Embodiment.Mode != EmbodimentMode.Narrative)
            {
                sb.Append("\n  ").Append(Embodiment.Describe());
            }

            if (Observation == ContextObservation.OffScreen)
            {
                sb.Append("\n  nobody was watching: witnesses unread, not absent");
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
