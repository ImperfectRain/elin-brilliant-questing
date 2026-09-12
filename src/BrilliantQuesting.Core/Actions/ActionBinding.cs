using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions
{
    /// <summary>The semantic payload that makes a reusable action mean this particular thing.</summary>
    public sealed class ActionBinding
    {
        public static readonly ActionBinding Empty = new ActionBinding();

        public EntityId PropositionFact { get; set; }

        public EntityId Item { get; set; }

        public EntityId Destination { get; set; }

        public string Purpose { get; set; }

        public bool HasProposition => !PropositionFact.IsNone;

        public bool HasItem => !Item.IsNone;

        public bool HasDestination => !Destination.IsNone;

        public bool HasPurpose => !string.IsNullOrEmpty(Purpose) || HasProposition || HasItem || HasDestination;

        public static ActionBinding Infer(ActionContext context)
        {
            if (context == null)
            {
                return Empty;
            }

            if (context.Binding != null && context.Binding.HasPurpose)
            {
                return context.Binding;
            }

            ActionBinding binding = new ActionBinding
            {
                PropositionFact = context.SubjectFact,
                Item = context.SubjectItem
            };

            if (binding.HasProposition)
            {
                return binding;
            }

            Fact trouble = StandingTrouble(context.World, context.Thread);
            if (trouble != null)
            {
                binding.PropositionFact = trouble.Id;
                if (!trouble.Object.IsNone)
                {
                    binding.Item = trouble.Object;
                }

                return binding;
            }

            return binding.HasPurpose ? binding : Empty;
        }

        /// <summary>
        /// The still-standing thing that is wrong in this matter, or null when the thread rests on
        /// history rather than on a condition.
        ///
        /// The first true <see cref="FactPredicates.IsStandingTrouble"/> claim the thread names,
        /// in the order the thread names them, so the same matter answers the same way twice.
        /// Public because it is the question "what is this situation about, mechanically" and more
        /// than one caller now asks it: a verb inferring what it is being pointed at, and the
        /// autonomy pass working out who has a stake in the matter and where it is.
        /// </summary>
        public static Fact StandingTrouble(NarrativeWorldState world, NarrativeThread thread)
        {
            if (world == null || thread == null)
            {
                return null;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact != null && fact.Truth == TruthState.True && FactPredicates.IsStandingTrouble(fact.Predicate))
                {
                    return fact;
                }
            }

            return null;
        }

        /// <summary>
        /// Whether this verb has been pointed at anything it could be about (BQa-010).
        ///
        /// The requirement is read off <see cref="NarrativeAction.Effects"/> rather than off a
        /// switch on verb ids kept here. A verb declares the slots any one of which would point
        /// it, so a new verb that needs a binding gets one by saying so, and a verb that needs
        /// none is attemptable unbound - which is the great majority of the library and the
        /// reason the default is permissive.
        ///
        /// It is not availability and does not replace it: this asks only whether the caller
        /// said what the attempt is about. Whether that thing makes the attempt possible is the
        /// verb's own question, asked afterwards.
        /// </summary>
        public static bool HasRequiredSemanticSlots(NarrativeAction action, ActionContext context)
        {
            return HasRequiredSemanticSlots(action, Infer(context));
        }

        /// <summary>
        /// The same question asked of a binding that has been built but not yet put in a context
        /// (BQa-011). Candidate construction has the binding before it has anywhere to run it, and
        /// building a whole context to ask a question about the binding would make the answer
        /// depend on a world, a resolver and an RNG stream that have nothing to do with it.
        /// </summary>
        public static bool HasRequiredSemanticSlots(NarrativeAction action, ActionBinding binding)
        {
            IReadOnlyList<string> needed = action == null ? null : action.Effects.NeedsAnyOf;
            if (needed == null || needed.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < needed.Count; i++)
            {
                if (SemanticSlots.IsBound(needed[i], binding))
                {
                    return true;
                }
            }

            return false;
        }

        public string Describe(ActionContext context)
        {
            if (!string.IsNullOrEmpty(Purpose))
            {
                return Purpose;
            }

            Fact fact = HasProposition ? context.World.Knowledge.GetFact(PropositionFact) : null;
            if (fact == null)
            {
                return "this matter";
            }

            if (fact.Predicate == FactPredicates.Stole)
            {
                return string.IsNullOrEmpty(fact.Value) ? "the missing property" : "the missing " + fact.Value;
            }

            if (fact.Predicate == FactPredicates.BlocksAccessTo)
            {
                return string.IsNullOrEmpty(fact.Value) ? "opening the blocked way" : "opening the " + fact.Value;
            }

            if (fact.Predicate == FactPredicates.AtRisk)
            {
                return "getting " + context.NameOf(fact.Subject) + " to safety";
            }

            if (fact.Predicate == FactPredicates.Needs)
            {
                return string.IsNullOrEmpty(fact.Value) ? "meeting the need" : "supplying " + fact.Value;
            }

            if (!string.IsNullOrEmpty(fact.Value))
            {
                return fact.Value;
            }

            return fact.Predicate.Replace('_', ' ');
        }
    }
}
