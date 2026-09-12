using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// What became of an attempt, as three answers rather than two (BQa-013).
    ///
    /// <see cref="Failed"/> and <see cref="Refused"/> are routinely confused because both end with
    /// the actor holding nothing, and they are not the same event in the world. A failure is
    /// something that happened: the lift was tried and fumbled, the lie was told and disbelieved,
    /// and there is a roll, a moment and usually a room that saw it. A refusal is something that
    /// did not happen at all - the money was not there, the item would not move, the claim the
    /// verb was pointed at had gone - and nothing about it may read as an act.
    ///
    /// The distinction is the whole reason this enum exists rather than a bool. "The attempt
    /// produced no change" used to be reported as success whenever a verb resolved without a
    /// roll, which is how an unsupported native write came to be narrated as a deed.
    /// </summary>
    public enum ActionResolution
    {
        /// <summary>It happened, and what the verb claims for success actually changed.</summary>
        Succeeded,

        /// <summary>It was performed and did not come off. A real occurrence with real fallout.</summary>
        Failed,

        /// <summary>
        /// It never took place: a native write the build would not carry, or a precondition that
        /// had gone by the time the attempt resolved. Never an act, and never somebody's fault.
        /// </summary>
        Refused
    }

    /// <summary>
    /// What a performed failure leaves behind (BQa-013).
    ///
    /// Read off the roadmap's five classes and off what the library already does, in that order:
    /// every key here is a shape some registered verb's failure branch actually has. There is
    /// deliberately no "complication" class and no rule that a failure must add one - `lie`
    /// failing simply means nobody was convinced, and a verb that invented a consequence to fill
    /// the silence would be writing drama rather than simulating an attempt.
    ///
    /// A class says what kind of residue a failure may leave. What actually changed is
    /// <see cref="ActionOutcome.Changed"/>, in the same <see cref="SemanticEffects"/> vocabulary
    /// success uses, because a failure that moves state moves the same state success would.
    /// </summary>
    public static class FailureOutcomes
    {
        /// <summary>
        /// Nothing moved. The lock held, the story was not believed, the trail was cold.
        ///
        /// The class the others must earn their way past: a verb declaring only this may move no
        /// state at all when it fails, and the audit says so if it does.
        /// </summary>
        public const string NoMaterialChange = "failure.nothing_changed";

        /// <summary>
        /// The attempt told somebody something. A botched lift in front of a room, a tail that
        /// was noticed, an accusation that rebounded onto the accuser.
        ///
        /// It is a *knowledge* route and not a licence to invent one: whoever learns has to have
        /// been in a position to, which is why an act nobody was watching cannot claim it.
        /// </summary>
        public const string InformationRevealed = "failure.information_revealed";

        /// <summary>
        /// The attempt was paid for anyway. The bribe is pocketed, the forger keeps the fee, the
        /// supply is consumed and the shortage still stands.
        /// </summary>
        public const string CostPaid = "failure.cost_paid";

        /// <summary>Somebody got hurt, or a thing somebody depended on did.</summary>
        public const string HarmDone = "failure.harm_done";

        /// <summary>
        /// What can be tried next is different now, without anybody having been told or paid or
        /// hurt: a belief settled the wrong way round, a mark who will not hear the same
        /// accusation twice, a rival who now holds a grudge that colours the next ask.
        /// </summary>
        public const string OptionsTransformed = "failure.options_transformed";

        private static readonly string[] Vocabulary =
        {
            NoMaterialChange,
            InformationRevealed,
            CostPaid,
            HarmDone,
            OptionsTransformed
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
    /// What a verb claims for each of the three ways an attempt can end (BQa-013).
    ///
    /// The fifth thing a verb declares about itself, after <see cref="NarrativeAction.ActorScope"/>,
    /// <see cref="NarrativeAction.Embodiment"/>, <see cref="NarrativeAction.SettlesMatters"/> and
    /// <see cref="NarrativeAction.Effects"/>, and it refines the fourth rather than repeating it.
    /// <see cref="ActionEffects"/> answers "what could this verb ever change", which is the
    /// question a want asks before anybody has attempted anything. This answers "and what does
    /// each ending actually leave behind", which is the question the world asks afterwards.
    ///
    /// Success is the load-bearing half. A verb saying success means <c>possession.transferred</c>
    /// is making a checkable claim: <see cref="NarrativeAction.Perform"/> holds the outcome to it,
    /// and an attempt that recorded the event but not the transfer is demoted to
    /// <see cref="ActionResolution.Refused"/> rather than being allowed to stand as a deed. That
    /// is the one guard that stops a build without <see cref="Integration.VanillaCapability"/>
    /// support from producing a history of things that never happened.
    ///
    /// Refusal is not declared per verb, because there is nothing to vary: a refusal changes
    /// nothing and records nothing, for every verb, and the audit checks that rather than trusting
    /// each verb to say so.
    ///
    /// <see cref="Undeclared"/> is a reported gap, exactly as it is for effects - never a claim
    /// that failing this verb is free.
    /// </summary>
    public sealed class ActionPostconditions
    {
        private static readonly string[] Nothing = new string[0];

        /// <summary>Nobody has classified this verb's endings yet. The default, and a reported gap.</summary>
        public static readonly ActionPostconditions Undeclared =
            new ActionPostconditions(false, Nothing, Nothing, Nothing);

        private ActionPostconditions(
            bool declared,
            IReadOnlyList<string> successChanges,
            IReadOnlyList<string> failureClasses,
            IReadOnlyList<string> failureChanges)
        {
            IsDeclared = declared;
            SuccessChanges = successChanges;
            FailureClasses = failureClasses;
            FailureChanges = failureChanges;
        }

        /// <summary>
        /// Declares what a successful use of this verb changes: any one of these appearing in
        /// <see cref="ActionOutcome.Changed"/> is what makes the outcome a success at all.
        ///
        /// Named kinds must be <see cref="SemanticEffects"/> the verb already declares it could
        /// advance. A success postcondition outside the verb's own effect declaration would be a
        /// second, disagreeing description of the same verb, and
        /// <see cref="ActionRegistry.PostconditionCoverage"/> reports it as one.
        /// </summary>
        public static ActionPostconditions Succeeding(params string[] effectKinds)
        {
            if (effectKinds == null || effectKinds.Length == 0)
            {
                throw new ArgumentException(
                    "A classification names what success changes; a verb with nothing to say stays Undeclared so the gap is reported.",
                    nameof(effectKinds));
            }

            return new ActionPostconditions(true, Copy(effectKinds), Nothing, Nothing);
        }

        /// <summary>
        /// Declares what a performed failure of this verb leaves behind, from
        /// <see cref="FailureOutcomes"/>. More than one is normal: which of them happens depends
        /// on the roll and on who was standing there, never on a rule that every failure must
        /// cost something.
        /// </summary>
        public ActionPostconditions Failing(params string[] failureClasses)
        {
            if (failureClasses == null || failureClasses.Length == 0)
            {
                return this;
            }

            return new ActionPostconditions(IsDeclared, SuccessChanges, Copy(failureClasses), FailureChanges);
        }

        /// <summary>
        /// The state a failure of this verb may itself move - the fee that is kept, the supply
        /// that is consumed, the exemplar that is ruined.
        ///
        /// Empty is the common and the strict case: a failure that recorded a change it never
        /// declared is a verb quietly doing something on the way to not working.
        /// </summary>
        public ActionPostconditions AlsoChangingOnFailure(params string[] effectKinds)
        {
            if (effectKinds == null || effectKinds.Length == 0)
            {
                return this;
            }

            return new ActionPostconditions(IsDeclared, SuccessChanges, FailureClasses, Copy(effectKinds));
        }

        /// <summary>False when nobody has classified this verb's endings yet.</summary>
        public bool IsDeclared { get; }

        /// <summary>What success changes; any one of them is enough to call it a success.</summary>
        public IReadOnlyList<string> SuccessChanges { get; }

        /// <summary>Which of the five shapes a performed failure of this verb can take.</summary>
        public IReadOnlyList<string> FailureClasses { get; }

        /// <summary>Effect kinds a performed failure of this verb may itself move.</summary>
        public IReadOnlyList<string> FailureChanges { get; }

        /// <summary>Whether a failure of this verb is declared to be able to leave that residue.</summary>
        public bool CanFailAs(string failureClass) => Contains(FailureClasses, failureClass);

        /// <summary>Whether that change is one this verb says success produces.</summary>
        public bool SucceedsBy(string effectKind) => Contains(SuccessChanges, effectKind);

        /// <summary>
        /// Whether a failure of this verb moves no state at all: it names only
        /// <see cref="FailureOutcomes.NoMaterialChange"/> and no change to go with it.
        ///
        /// Deliberately a claim about state and not about history. An event saying an attempt
        /// happened is not a material change, and a rule that forbade one would make a verb
        /// choose between recording that somebody tried and admitting that trying cost nothing.
        /// What is enforced is the state claim, by <see cref="FailureChanges"/> being empty:
        /// such a verb may move nothing when it fails, and the audit says so if it does.
        /// </summary>
        public bool FailsWithoutMovingState =>
            FailureClasses.Count == 1
            && string.Equals(FailureClasses[0], FailureOutcomes.NoMaterialChange, StringComparison.Ordinal)
            && FailureChanges.Count == 0;

        internal static bool Contains(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Copy(string[] source)
        {
            string[] copy = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (string.IsNullOrEmpty(source[i]))
                {
                    throw new ArgumentException("A classification names registered vocabulary, never an empty key.", nameof(source));
                }

                copy[i] = source[i];
            }

            return copy;
        }
    }

    /// <summary>
    /// Holds a finished attempt against what its verb said each ending would leave behind (BQa-013).
    ///
    /// Side-effect free, like every other question asked of a verb, and it reports rather than
    /// throws: a contract breach in a shipped build should leave a diagnosable outcome in the
    /// inspector, not take the turn down. <see cref="NarrativeAction.Perform"/> runs it on every
    /// declared verb and the findings ride on <see cref="ActionOutcome.ContractViolations"/>,
    /// where a test can insist there are none and a live session can be asked why.
    ///
    /// The four things it looks for are the four ways an outcome can lie about the world:
    /// claiming a success nothing carried, dressing a refusal up as an occurrence, moving state a
    /// failure never declared, and producing witnesses to an act nobody was watching.
    /// </summary>
    public static class ActionPostconditionAudit
    {
        /// <summary>Whether this success claim is one the verb's declaration will bear out.</summary>
        public static bool CarriedItsSuccess(ActionPostconditions postconditions, ActionOutcome outcome)
        {
            if (postconditions == null || !postconditions.IsDeclared || outcome == null)
            {
                return true;
            }

            for (int i = 0; i < outcome.Changed.Count; i++)
            {
                if (postconditions.SucceedsBy(outcome.Changed[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everything this outcome says that its verb's declaration does not support, in a stable
        /// order. Empty for an honest outcome, and empty for an undeclared verb - an unclassified
        /// verb is a coverage gap that <see cref="ActionRegistry.PostconditionCoverage"/> reports,
        /// not a breach to be invented here.
        /// </summary>
        public static IReadOnlyList<string> Check(NarrativeAction action, ActionOutcome outcome)
        {
            List<string> violations = new List<string>();
            if (action == null || outcome == null || !action.Postconditions.IsDeclared)
            {
                return violations;
            }

            ActionPostconditions declared = action.Postconditions;

            for (int i = 0; i < outcome.Changed.Count; i++)
            {
                if (!SemanticEffects.IsRegistered(outcome.Changed[i]))
                {
                    violations.Add("recorded a change outside the vocabulary: " + outcome.Changed[i]);
                }
            }

            switch (outcome.Resolution)
            {
                case ActionResolution.Succeeded:
                    if (!CarriedItsSuccess(declared, outcome))
                    {
                        violations.Add("claims success but recorded none of " + Join(declared.SuccessChanges));
                    }

                    break;

                case ActionResolution.Failed:
                    for (int i = 0; i < outcome.Changed.Count; i++)
                    {
                        if (ActionPostconditions.Contains(declared.FailureChanges, outcome.Changed[i]))
                        {
                            continue;
                        }

                        violations.Add(declared.FailsWithoutMovingState
                            ? "declared to fail without moving anything, but moved " + outcome.Changed[i]
                            : "failure moved undeclared state: " + outcome.Changed[i]);
                    }

                    break;

                default:
                    // A refusal is not an occurrence. An event recorded for one would be exactly
                    // the label standing in for the change it names.
                    if (outcome.Changed.Count > 0)
                    {
                        violations.Add("refused, but recorded a change: " + Join(outcome.Changed));
                    }

                    if (outcome.Events.Count > 0)
                    {
                        violations.Add("refused, but recorded " + outcome.Events.Count + " event(s) as history");
                    }

                    break;
            }

            // Nobody's presence was read, so nobody can have seen it. A failure that produces its
            // own witnesses off screen is the manufactured eyewitness `VS 5.4` and `D017` refuse,
            // and it is the only route by which a hidden failure could cost somebody trust.
            if (outcome.Observation == ContextObservation.OffScreen)
            {
                for (int i = 0; i < outcome.Events.Count; i++)
                {
                    WorldEvent recorded = outcome.Events[i];
                    if (recorded != null && recorded.Witnesses != null && recorded.Witnesses.Count > 0)
                    {
                        violations.Add("named " + recorded.Witnesses.Count + " witness(es) to " + recorded.Type + " where nobody's presence was read");
                    }
                }
            }

            return violations;
        }

        private static string Join(IReadOnlyList<string> values)
        {
            if (values.Count == 0)
            {
                return "(nothing)";
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }

                sb.Append(values[i]);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Where the library has classified its endings and where it has not (BQa-013).
    ///
    /// The same reporting contract as <see cref="ActionEffectCoverage"/>, for the same reason: to
    /// a consumer that only asks what a failure costs, a verb nobody classified and a verb that
    /// genuinely costs nothing read identically.
    /// </summary>
    public sealed class ActionPostconditionCoverage
    {
        internal ActionPostconditionCoverage(
            IReadOnlyList<NarrativeAction> unclassified,
            IReadOnlyList<NarrativeAction> withoutDeclaredEffects,
            IReadOnlyList<string> unregisteredFailureClasses,
            IReadOnlyList<string> successChangesOutsideEffects)
        {
            Unclassified = unclassified;
            WithoutDeclaredEffects = withoutDeclaredEffects;
            UnregisteredFailureClasses = unregisteredFailureClasses;
            SuccessChangesOutsideEffects = successChangesOutsideEffects;
        }

        /// <summary>
        /// Verbs that say what they could change but not what each ending leaves behind. The gap
        /// this report exists for.
        /// </summary>
        public IReadOnlyList<NarrativeAction> Unclassified { get; }

        /// <summary>
        /// Verbs that have not declared their effects either, and so cannot be classified: what
        /// success means for them is the earlier open question (BQa-010), not this one.
        /// </summary>
        public IReadOnlyList<NarrativeAction> WithoutDeclaredEffects { get; }

        /// <summary>Failure classes outside the vocabulary - a typo, or a verb inventing a term.</summary>
        public IReadOnlyList<string> UnregisteredFailureClasses { get; }

        /// <summary>
        /// "verb: kind" for every success postcondition naming a change the verb never said it
        /// could make. Two descriptions of one verb that disagree, which is the drift this whole
        /// declaration style exists to prevent.
        /// </summary>
        public IReadOnlyList<string> SuccessChangesOutsideEffects { get; }

        public bool IsComplete =>
            Unclassified.Count == 0
            && UnregisteredFailureClasses.Count == 0
            && SuccessChangesOutsideEffects.Count == 0;
    }
}
