using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

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

            if (context.Thread != null)
            {
                for (int i = 0; i < context.Thread.FactIds.Count; i++)
                {
                    Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                    if (fact != null && fact.Truth == TruthState.True && FactPredicates.IsStandingTrouble(fact.Predicate))
                    {
                        binding.PropositionFact = fact.Id;
                        if (!fact.Object.IsNone)
                        {
                            binding.Item = fact.Object;
                        }

                        return binding;
                    }
                }
            }

            return binding.HasPurpose ? binding : Empty;
        }

        public static bool HasRequiredSemanticSlots(string actionId, ActionContext context)
        {
            ActionBinding binding = Infer(context);
            switch (actionId)
            {
                case "persuade":
                case "intimidate":
                case "bribe":
                case "report":
                    return binding.HasProposition || binding.HasPurpose;

                case "return_item":
                    return binding.HasItem;

                case "escort":
                case "capture":
                case "restrain":
                    return binding.HasDestination || !string.IsNullOrEmpty(binding.Purpose);

                default:
                    return true;
            }
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
