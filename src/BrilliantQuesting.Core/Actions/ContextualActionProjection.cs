using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions
{
    /// <summary>
    /// Turns legal reusable verbs into player-facing intents for the current context.
    ///
    /// This is presentation, not legality: the action registry still decides what can be tried,
    /// and click-time execution must still revalidate. The labels here may use only information
    /// the acting character already knows or information already visible in the interaction.
    /// </summary>
    public static class ContextualActionProjection
    {
        public static List<ActionIntentOption> Project(IReadOnlyList<ActionOffer> offers, ActionContext context, int max)
        {
            List<ActionIntentOption> projected = new List<ActionIntentOption>();
            if (offers == null || context == null || max <= 0)
            {
                return projected;
            }

            List<ActionOffer> eligible = new List<ActionOffer>();
            for (int i = 0; i < offers.Count; i++)
            {
                ActionOffer offer = offers[i];
                if (offer != null && ActionBinding.HasRequiredSemanticSlots(offer.Action.Id, context))
                {
                    eligible.Add(offer);
                }
            }

            List<ActionOffer> display = OfferPresentation.TakeForDisplay(eligible, max);
            for (int i = 0; i < display.Count; i++)
            {
                ActionOffer offer = display[i];
                projected.Add(new ActionIntentOption(
                    offer,
                    LabelFor(offer.Action, context),
                    SurfaceFor(offer.Action),
                    IntentFamilyFor(offer.Action)));
            }

            return projected;
        }

        public static string LabelFor(NarrativeAction action, ActionContext context)
        {
            if (action == null)
            {
                return "Act";
            }

            string target = Name(context, context.Target);
            Fact subject = SubjectFact(context);
            ActionBinding binding = ActionBinding.Infer(context);
            string matter = binding.Describe(context);
            bool actorKnowsSubject = subject != null && context.World.Knowledge.Knows(context.Actor, subject.Id);
            bool targetIsSubject = subject != null && subject.Subject == context.Target;

            switch (action.Id)
            {
                case "question":
                    return QuestionLabel(context, subject, target, matter);

                case "call_favor":
                    return "Call in the favour " + target + " owes you, for " + matter;

                case "persuade":
                    return actorKnowsSubject && targetIsSubject
                        ? "Ask " + target + " to put " + matter + " right"
                        : "Ask " + target + " to help with " + matter;

                case "intimidate":
                    return actorKnowsSubject && targetIsSubject
                        ? "Press " + target + " to answer for " + matter
                        : "Pressure " + target + " for help with " + matter;

                case "bribe":
                    return "Offer " + target + " payment for help with " + matter;

                case "rapport":
                    return "Build rapport with " + target;

                case "lie":
                    return "Mislead " + target + " about " + matter;

                case "expose":
                    return "Tell " + target + " what you know about " + matter;

                case "search":
                    return "Search for evidence about " + matter;

                case "search_records":
                    return "Search records for " + matter;

                case "compare_testimony":
                    return "Compare testimony about " + matter;

                case "track":
                    return "Track leads around " + matter;

                case "follow":
                    return "Follow " + target + " for a lead";

                case "eavesdrop":
                    return "Listen for talk about " + matter;

                case "return_item":
                    return "Return " + ItemName(context) + " to " + target;

                case "keep_item":
                    return "Keep " + ItemName(context);

                case "pickpocket":
                    return actorKnowsSubject && targetIsSubject
                        ? "Try to recover " + ItemName(context) + " from " + target
                        : "Pick " + target + "'s pocket";

                case "report":
                    return "Report " + matter + " to " + target;

                case "invoke_authority":
                    return "Ask " + target + " to use authority on " + matter;

                case "escort":
                    return "Escort " + target + " somewhere safe";

                case "capture":
                    return "Capture " + target + " for " + matter;

                case "restrain":
                    return "Restrain " + target + " over " + matter;

                default:
                    return action.Label;
            }
        }

        private static string QuestionLabel(ActionContext context, Fact subject, string target, string matter)
        {
            if (subject != null
                && context.World.Knowledge.TryGetBelief(context.Target, subject.Id, out KnowledgeRecord belief)
                && belief.Source == KnowledgeSource.Witnessed)
            {
                return "Ask " + target + " what they saw";
            }

            return "Ask " + target + " about " + matter;
        }

        private static string SurfaceFor(NarrativeAction action)
        {
            if (action == null)
            {
                return "Talk";
            }

            switch (action.Family)
            {
                case ActionFamily.Physical:
                    return "World";
                case ActionFamily.Crafting:
                case ActionFamily.HomeCommunity:
                case ActionFamily.MagicFaith:
                    return "Local";
                default:
                    return "Talk";
            }
        }

        private static string IntentFamilyFor(NarrativeAction action)
        {
            if (action == null)
            {
                return "Action";
            }

            switch (action.Family)
            {
                case ActionFamily.Information:
                    return "Investigate";
                case ActionFamily.Crime:
                    return "Illicit";
                case ActionFamily.Economic:
                    return "Bargain";
                case ActionFamily.Physical:
                    return "Move";
                case ActionFamily.Crafting:
                    return "Make";
                case ActionFamily.MagicFaith:
                    return "Petition";
                case ActionFamily.HomeCommunity:
                    return "Shelter";
                default:
                    return "Talk";
            }
        }

        private static Fact SubjectFact(ActionContext context)
        {
            return context.SubjectFact.IsNone ? null : context.World.Knowledge.GetFact(context.SubjectFact);
        }

        private static string Matter(Fact fact)
        {
            if (fact == null)
            {
                return "this matter";
            }

            if (fact.Predicate == FactPredicates.Stole)
            {
                return string.IsNullOrEmpty(fact.Value) ? "the missing property" : "the missing " + fact.Value;
            }

            if (!string.IsNullOrEmpty(fact.Value))
            {
                return fact.Value;
            }

            return fact.Predicate.Replace('_', ' ');
        }

        private static string ItemName(ActionContext context)
        {
            Fact subject = SubjectFact(context);
            if (subject != null && !string.IsNullOrEmpty(subject.Value))
            {
                return "the " + subject.Value;
            }

            return context.SubjectItem.IsNone ? "the item" : "the item";
        }

        private static string Name(ActionContext context, EntityId id)
        {
            if (context == null || id.IsNone)
            {
                return "someone";
            }

            string name = context.NameOf(id);
            return string.IsNullOrEmpty(name) ? "someone" : name;
        }
    }
}
