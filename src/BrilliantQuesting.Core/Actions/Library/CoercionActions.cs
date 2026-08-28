using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Lean on someone. Cheap, fast, and it costs you the relationship - which is the point:
    /// the fighter's social route exists, it is just expensive in a different currency.
    /// </summary>
    public sealed class IntimidateAction : NarrativeAction
    {
        public IntimidateAction() : base("intimidate", ActionFamily.Social, "Intimidate")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to lean on");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            EntityId factId = ActionSupport.FindTeachableFact(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Intimidation, context.Actor, context.Target)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: true))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: true));

            // Friends are harder to frighten - they do not believe you would go through with it.
            int affinity = context.Affinity;
            if (affinity > 25)
            {
                request.WithModifier("they trust you too much to be scared", affinity / 25);
            }

            NarrativeNpc npc = context.TargetNpc;
            if (npc != null)
            {
                request.WithModifier("their nerve", (int)(npc.Personality.Courage * 6.0) - 3);
            }

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);

            // Whether they have anything to give up decides the words as much as the roll does.
            // Narrating from the roll alone produced the first live run's worst line: "Ansel tells
            // you what you want to know", immediately followed by "they had nothing to give up".
            // A threat that lands on someone with nothing still lands - they comply, they are
            // frightened, and they remember it - so the outcome is real either way; it just is
            // not the outcome the player was fishing for, and it should not claim to be.
            bool hasSomethingToGive = !factId.IsNone;
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    outcome = new ActionOutcome(Id, check, hasSomethingToGive
                        ? who + " folds completely and volunteers more than you asked for."
                        : who + " folds completely, and swears blind they know nothing more.");
                    Concede(context, factId, 0.9, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.7, context.Zone, witnesses: seen));
                    break;

                case CheckOutcome.Pass:
                    outcome = new ActionOutcome(Id, check, hasSomethingToGive
                        ? who + " tells you what you want to know."
                        : who + " backs down, but has nothing you did not already know.");
                    Concede(context, factId, 0.7, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.6, context.Zone, witnesses: seen));
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " holds their nerve and remembers this.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.5, context.Zone, witnesses: seen));
                    break;

                default:
                    // Elin's favourite kind of failure: your threat is misread as a challenge.
                    outcome = new ActionOutcome(Id, check, who + " takes it as a challenge and swings first.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.5, context.Zone, witnesses: seen));
                    outcome.Events.Add(context.World.Record(WorldEventType.Attacked, context.Target, context.Actor, context.Now, 0.5, context.Zone, witnesses: seen));
                    outcome.Notes.Add("combat is Elin's to resolve; the simulation only records that it started");
                    break;
            }

            return outcome;
        }

        private static void Concede(ActionContext context, EntityId factId, double confidence, ActionOutcome outcome)
        {
            if (factId.IsNone)
            {
                outcome.Notes.Add("they had nothing to give up");
                return;
            }

            context.World.Knowledge.Teach(context.Actor, factId, KnowledgeSource.Hearsay, confidence, context.Now, false, context.Target);
            outcome.Notes.Add("learned under duress: " + ActionSupport.Describe(context, factId));
        }
    }

    /// <summary>
    /// Buy cooperation with real orens.
    ///
    /// The money is a hard requirement - you cannot offer coin you do not have - but whether the
    /// bribe works is a check, and a botched one has them pocket it and give you nothing.
    /// </summary>
    public sealed class BribeAction : NarrativeAction
    {
        public BribeAction() : base("bribe", ActionFamily.Economic, "Offer money")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to pay");
            }

            if (!context.Vanilla.Supports(VanillaCapability.SpendMoney))
            {
                return Availability.Impossible("money transfers are unavailable on this build");
            }

            int price = PriceFor(context);
            if (context.Vanilla.GetMoney(context.Actor) < price)
            {
                return Availability.Impossible("you cannot offer " + price + " orens you do not have");
            }

            return Availability.Available(ActionSupport.FindTeachableFact(context).IsNone
                ? "costs about " + price + " orens, and they may have nothing to sell"
                : "costs about " + price + " orens");
        }

        /// <summary>
        /// What this particular person expects. A greedy low-level tough is cheap; a proud
        /// official is not. Deliberately visible in the offer so the player can price the route.
        /// </summary>
        public static int PriceFor(ActionContext context)
        {
            int basePrice = 50 + context.Vanilla.GetLevel(context.Target) * 25;
            NarrativeNpc npc = context.TargetNpc;
            double greed = npc?.Personality.Greed ?? 0.5;
            double multiplier = 1.6 - greed;
            return (int)(basePrice * multiplier);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            int price = PriceFor(context);
            EntityId factId = ActionSupport.FindTeachableFact(context);

            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Bribery, context.Actor, context.Target);
            NarrativeNpc npc = context.TargetNpc;
            if (npc != null)
            {
                request.WithModifier("their scruples", (int)(npc.Personality.Honesty * 8.0) - 4);
                request.WithModifier("their greed", -(int)(npc.Personality.Greed * 6.0));
            }

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);

            if (!context.Vanilla.TrySpendMoney(context.Actor, context.Target, price))
            {
                ActionOutcome broke = new ActionOutcome(Id, check, "You cannot cover the offer.");
                broke.Notes.Add("payment failed: insufficient funds at resolution time");
                return broke;
            }

            // As with intimidation: whether they have anything to sell decides the words. A
            // success against someone with nothing still buys goodwill, which is a real thing to
            // have bought, but it is not "pockets it and talks".
            bool hasSomethingToSell = !factId.IsNone;
            ActionOutcome outcome;
            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    outcome = new ActionOutcome(Id, check, who + " takes the money and decides you are worth keeping happy.");
                    Reveal(context, factId, 0.9, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.7, context.Zone));
                    break;

                case CheckOutcome.Pass:
                    outcome = new ActionOutcome(Id, check, hasSomethingToSell
                        ? who + " pockets it and talks."
                        : who + " pockets it, willing enough - but they have nothing you do not already know.");
                    Reveal(context, factId, 0.7, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.5, context.Zone));
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " takes the money, says nothing useful, and looks unimpressed.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.3, context.Zone));
                    outcome.Notes.Add("paid " + price + " orens for nothing");
                    break;

                default:
                    // The money is gone and they are insulted. Both halves matter.
                    outcome = new ActionOutcome(Id, check, who + " is insulted, keeps the money anyway, and tells people you tried to buy them.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Theft, context.Target, context.Actor, context.Now, 0.4, context.Zone));
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.3, context.Zone, witnesses: ActionSupport.Bystanders(context, true)));
                    break;
            }

            return outcome;
        }

        private static void Reveal(ActionContext context, EntityId factId, double confidence, ActionOutcome outcome)
        {
            if (factId.IsNone)
            {
                outcome.Notes.Add("they had nothing to sell");
                return;
            }

            context.World.Knowledge.Teach(context.Actor, factId, KnowledgeSource.Hearsay, confidence, context.Now, false, context.Target);
            outcome.Notes.Add("bought: " + ActionSupport.Describe(context, factId));
        }
    }
}
