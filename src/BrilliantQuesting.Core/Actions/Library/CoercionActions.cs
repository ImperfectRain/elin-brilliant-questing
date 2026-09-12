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

        public override ActionEffects Effects => ActionEffects
            .Declaring(ActionEffect.Recorded(SemanticEffects.StandingAltered))
            .NeedingAnyOf(
                SemanticSlots.Proposition,
                SemanticSlots.Item,
                SemanticSlots.Destination,
                SemanticSlots.Purpose);

        /// <summary>
        /// A threat lands whatever the roll (BQa-013): every branch records it, and how the two
        /// of them stand is different afterwards in all four. So success is standing changing,
        /// failure is standing changing too, and the honest classification says so rather than
        /// pretending that holding your nerve costs the person who was threatened nothing.
        ///
        /// What separates the branches is what comes with it - what they gave up, what the room
        /// heard, and at the far end a fight the game resolves and BQ only records the start of.
        /// </summary>
        public override ActionPostconditions Postconditions => ActionPostconditions
            .Succeeding(SemanticEffects.StandingAltered)
            .Failing(FailureOutcomes.OptionsTransformed, FailureOutcomes.InformationRevealed, FailureOutcomes.HarmDone)
            .AlsoChangingOnFailure(SemanticEffects.StandingAltered);

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to lean on");
            }

            if (!ActionBinding.HasRequiredSemanticSlots(this, context))
            {
                return Availability.NotRelevant("nothing specific to demand");
            }

            return Availability.Available();
        }

        protected override ActionOutcome PerformCore(ActionContext context)
        {
            ActionBinding binding = ActionBinding.Infer(context);
            EntityId factId = !binding.PropositionFact.IsNone
                ? binding.PropositionFact
                : ActionSupport.FindTeachableFact(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Intimidation, context.Actor, context.Target)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: true))
                .With(SituationalModifiers.LegalStanding(context, helpfulWhenNotorious: true));

            // Friends are harder to frighten - they do not believe you would go through with it.
            // Only the player has such a number to read; for anybody else the term is absent
            // rather than zero, and says so (BQ-093, `D017`).
            if (context.TryGetAffinityToActor(context.Target, out int affinity))
            {
                if (affinity > 25)
                {
                    request.WithModifier("they trust you too much to be scared", affinity / 25);
                }
            }
            else
            {
                request.WithUnreadTerm("their trust in the actor unread: vanilla keeps goodwill toward the player only");
            }

            NarrativeNpc npc = context.TargetNpc;
            if (npc != null)
            {
                request.WithModifier("their nerve", (int)(npc.Personality.Courage * 6.0) - 3);
            }

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string who = context.NameOf(context.Target);
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            ActionOutcome Threatening(string narration) =>
                new ActionOutcome(Id, check, narration).Change(SemanticEffects.StandingAltered);

            // Whether they have anything to give up decides the words as much as the roll does.
            // Narrating from the roll alone produced the first live run's worst line: "Ansel tells
            // you what you want to know", immediately followed by "they had nothing to give up".
            // A threat that lands on someone with nothing still lands - they comply, they are
            // frightened, and they remember it - so the outcome is real either way; it just is
            // not the outcome the player was fishing for, and it should not claim to be.
            bool pressuredAdmission = !factId.IsNone && ActionSupport.IsSelfIncriminatingDisclosure(context, factId);
            bool actorAlreadyKnows = !factId.IsNone && context.World.Knowledge.Knows(context.Actor, factId);
            bool hasSomethingToGive = !factId.IsNone && !actorAlreadyKnows && !pressuredAdmission;
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    if (pressuredAdmission)
                    {
                        outcome = Threatening(who + " cracks and admits " + ActionSupport.Describe(context, factId) + ".");
                        Admit(context, factId, 0.9, outcome);
                        outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.7, context.Zone, related: new[] { factId }, witnesses: seen, tags: new[] { EventTags.Admission }, threadId: ThreadId(context)));
                        break;
                    }

                    outcome = Threatening(hasSomethingToGive
                        ? who + " folds completely and volunteers more than you asked for."
                        : Acknowledgement(who, binding, factId, context, "folds completely"));
                    RecordConcession(context, factId, 0.9, outcome, hasSomethingToGive);
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.7, context.Zone, related: Related(factId), witnesses: seen, threadId: ThreadId(context)));
                    break;

                case CheckOutcome.Pass:
                    if (pressuredAdmission)
                    {
                        outcome = Threatening(who + " understands exactly what you mean about " + binding.Describe(context) + ", but refuses to say it out loud.");
                        outcome.Notes.Add("withheld under pressure: " + ActionSupport.Describe(context, factId));
                        outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.6, context.Zone, related: new[] { factId }, witnesses: seen, tags: new[] { EventTags.Withheld }, threadId: ThreadId(context)));
                        break;
                    }

                    outcome = Threatening(hasSomethingToGive
                        ? who + " tells you what you want to know."
                        : Acknowledgement(who, binding, factId, context, "backs down"));
                    RecordConcession(context, factId, 0.7, outcome, hasSomethingToGive);
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.6, context.Zone, related: Related(factId), witnesses: seen, threadId: ThreadId(context)));
                    break;

                case CheckOutcome.Fail:
                    outcome = Threatening(who + " holds their nerve and remembers this.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.5, context.Zone, related: Related(factId), witnesses: seen, threadId: ThreadId(context)));
                    break;

                default:
                    // Elin's favourite kind of failure: your threat is misread as a challenge.
                    outcome = Threatening(who + " takes it as a challenge and swings first.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Threatened, context.Actor, context.Target, context.Now, 0.5, context.Zone, related: Related(factId), witnesses: seen, threadId: ThreadId(context)));
                    outcome.Events.Add(context.World.Record(WorldEventType.Attacked, context.Target, context.Actor, context.Now, 0.5, context.Zone, related: Related(factId), witnesses: seen, threadId: ThreadId(context)));
                    outcome.Notes.Add("combat is Elin's to resolve; the simulation only records that it started");
                    break;
            }

            return outcome;
        }

        private static EntityId ThreadId(ActionContext context)
        {
            return context.Thread?.Id ?? EntityId.None;
        }

        private static EntityId[] Related(EntityId factId)
        {
            return factId.IsNone ? null : new[] { factId };
        }

        private static void Admit(ActionContext context, EntityId factId, double confidence, ActionOutcome outcome)
        {
            context.World.Knowledge.Teach(context.Actor, factId, KnowledgeSource.Admission, confidence, context.Now, false, context.Target);
            outcome.Notes.Add("admitted under pressure: " + ActionSupport.Describe(context, factId));
        }

        private static string Acknowledgement(string who, ActionBinding binding, EntityId factId, ActionContext context, string result)
        {
            if (!factId.IsNone)
            {
                return who + " " + result + " over " + binding.Describe(context) + ", but adds nothing new.";
            }

            return who + " " + result + ", but cannot give you anything specific.";
        }

        private static void RecordConcession(ActionContext context, EntityId factId, double confidence, ActionOutcome outcome, bool teach)
        {
            if (factId.IsNone)
            {
                outcome.Notes.Add("no specific fact was conceded");
                return;
            }

            if (teach)
            {
                context.World.Knowledge.Teach(context.Actor, factId, ActionSupport.DisclosureSource(context, factId), confidence, context.Now, false, context.Target);
                outcome.Notes.Add("learned under duress: " + ActionSupport.Describe(context, factId));
            }
            else
            {
                outcome.Notes.Add("pressed under duress: " + ActionSupport.Describe(context, factId));
            }
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

        public override ActionEffects Effects => ActionEffects
            .Declaring(ActionEffect.Delegated(
                SemanticEffects.StandingAltered,
                "IVanillaState.TrySpendMoney",
                VanillaCapability.SpendMoney))
            .NeedingAnyOf(
                SemanticSlots.Proposition,
                SemanticSlots.Item,
                SemanticSlots.Destination,
                SemanticSlots.Purpose);

        /// <summary>
        /// The money moving is what makes this an act at all (BQa-013), so success is standing
        /// bought; a purse the build would not open is a refusal, and nothing about it may read
        /// as an offer anybody heard.
        ///
        /// Once the coin has changed hands every ending has been paid for. Failing means they
        /// took it and gave nothing back; failing badly means they took it, kept it and told
        /// people - which is the one branch where what it cost is more than the orens.
        /// </summary>
        public override ActionPostconditions Postconditions => ActionPostconditions
            .Succeeding(SemanticEffects.StandingAltered)
            .Failing(FailureOutcomes.CostPaid, FailureOutcomes.InformationRevealed, FailureOutcomes.OptionsTransformed)
            .AlsoChangingOnFailure(SemanticEffects.StandingAltered);

        protected override Availability GetAvailabilityCore(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
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

            if (!ActionBinding.HasRequiredSemanticSlots(this, context))
            {
                return Availability.NotRelevant("nothing specific to buy");
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

        protected override ActionOutcome PerformCore(ActionContext context)
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
                return broke.Refuse("payment failed: insufficient funds at resolution time");
            }

            // As with intimidation: whether they have anything to sell decides the words. A
            // success against someone with nothing still buys goodwill, which is a real thing to
            // have bought, but it is not "pockets it and talks".
            bool hasSomethingToSell = !factId.IsNone;
            ActionOutcome outcome;
            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    outcome = new ActionOutcome(Id, check, who + " takes the money and decides you are worth keeping happy.").Change(SemanticEffects.StandingAltered);
                    Reveal(context, factId, 0.9, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.7, context.Zone));
                    break;

                case CheckOutcome.Pass:
                    outcome = new ActionOutcome(Id, check, hasSomethingToSell
                        ? who + " pockets it and talks."
                        : who + " pockets it, willing enough - but they have nothing you do not already know.")
                        .Change(SemanticEffects.StandingAltered);
                    Reveal(context, factId, 0.7, outcome);
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.5, context.Zone));
                    break;

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, who + " takes the money, says nothing useful, and looks unimpressed.").Change(SemanticEffects.StandingAltered);
                    outcome.Events.Add(context.World.Record(WorldEventType.Bribed, context.Actor, context.Target, context.Now, 0.3, context.Zone));
                    outcome.Notes.Add("paid " + price + " orens for nothing");
                    break;

                default:
                    // The money is gone and they are insulted. Both halves matter.
                    outcome = new ActionOutcome(Id, check, who + " is insulted, keeps the money anyway, and tells people you tried to buy them.").Change(SemanticEffects.StandingAltered);
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

            context.World.Knowledge.Teach(context.Actor, factId, ActionSupport.DisclosureSource(context, factId), confidence, context.Now, false, context.Target);
            outcome.Notes.Add("bought: " + ActionSupport.Describe(context, factId));
        }
    }
}
