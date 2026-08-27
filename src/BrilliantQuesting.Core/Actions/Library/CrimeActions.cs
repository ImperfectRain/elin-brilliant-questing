using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>
    /// Take something out of a real inventory.
    ///
    /// The important half is not the item, it is the witness list: a clean lift creates no
    /// knowledge at all, and a botched one hands the whole room a provable fact about you.
    /// </summary>
    public sealed class PickpocketAction : NarrativeAction
    {
        public PickpocketAction() : base("pickpocket", ActionFamily.Crime, "Pick their pocket")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to steal from");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (SelectTarget(context) == null)
            {
                return Availability.NotRelevant("they are carrying nothing worth taking");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = SelectTarget(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Pickpocketing, context.Actor, context.Target);

            // Heavier, more valuable things are harder to palm - and a crowd is a complication.
            request.WithModifier("value of the item", item.Value / 500);
            request.WithModifier("onlookers", context.Witnesses.Count > 1 ? context.Witnesses.Count - 1 : 0);

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    bool taken = context.Vanilla.TryTransferItem(item.Id, context.Target, context.Actor);
                    // Nobody saw it. The theft is real, it is recorded, and no one knows.
                    outcome = new ActionOutcome(Id, check, taken
                        ? "You lift the " + item.Name + " without anyone noticing."
                        : "Your fingers close on nothing.");
                    if (taken)
                    {
                        outcome.Events.Add(context.World.Record(
                            WorldEventType.Theft, context.Actor, context.Target, context.Now, 0.5, context.Zone,
                            new[] { item.Id }, tags: new[] { EventTags.Unnoticed }));
                        outcome.Notes.Add("no witnesses: nobody in the world knows this happened");
                    }

                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "You think better of it halfway through. They shift, suspicious.");
                    break;

                default:
                {
                    // Caught in the act, in front of whoever is standing there.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    Fact caught = RecordTheftFact(context, item);
                    outcome = new ActionOutcome(Id, check, "Your hand is caught in their pocket in front of everyone.");
                    outcome.Events.Add(context.World.Record(WorldEventType.Theft, context.Actor, context.Target, context.Now, 0.8, context.Zone, new[] { caught.Id }, seen, new[] { item.Id }));
                    outcome.Events.Add(context.World.Record(WorldEventType.CrimeWitnessed, context.Actor, context.Target, context.Now, 0.8, context.Zone, new[] { caught.Id }, seen));
                    outcome.Notes.Add(seen.Count + " witness(es) can now prove it");
                    break;
                }
            }

            return outcome;
        }

        private static Fact RecordTheftFact(ActionContext context, ItemDescriptor item)
        {
            Fact fact = new Fact(context.World.NewId("fact"), context.Actor, FactPredicates.Stole, item.Id, item.Name, TruthState.True, secrecy: 0);
            fact.EvidenceIds.Add(item.Id);
            context.World.Knowledge.AddFact(fact);
            return fact;
        }

        private static ItemDescriptor SelectTarget(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(context.Target);
            ItemDescriptor best = null;
            for (int i = 0; i < inventory.Count; i++)
            {
                ItemDescriptor item = inventory[i];
                if (!context.SubjectItem.IsNone)
                {
                    if (item.Id == context.SubjectItem)
                    {
                        return item;
                    }

                    continue;
                }

                if (best == null || item.Value > best.Value)
                {
                    best = item;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Put something of someone else's where a third party will be blamed for it.
    ///
    /// Requires an actual object in your actual inventory. The fact it creates is flagged False,
    /// so the world knows the accusation is a lie even while every character in it believes the
    /// opposite - which is what lets the truth come out later.
    /// </summary>
    public sealed class PlantEvidenceAction : NarrativeAction
    {
        public PlantEvidenceAction() : base("frame", ActionFamily.Crime, "Plant it on someone")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.ThirdParty.IsNone)
            {
                return Availability.NotRelevant("nobody chosen to take the blame");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (FindPlantable(context) == null)
            {
                return Availability.Impossible("you are not carrying anything that would incriminate them");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            ItemDescriptor item = FindPlantable(context);
            CheckRequest request = new CheckRequest(ProceduralCheckProfiles.Fabrication, context.Actor, context.ThirdParty)
                .With(SituationalModifiers.Reputation(context, helpfulWhenFamous: false));

            CheckResult check = context.Checks.Resolve(request, context.Rng);
            string patsy = context.NameOf(context.ThirdParty);
            ActionOutcome outcome;

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                case CheckOutcome.Pass:
                {
                    context.Vanilla.TryTransferItem(item.Id, context.Actor, context.ThirdParty);

                    Fact lie = new Fact(context.World.NewId("fact"), context.ThirdParty, FactPredicates.Possesses, item.Id, item.Name, TruthState.False);
                    lie.EvidenceIds.Add(item.Id);
                    context.World.Knowledge.AddFact(lie);

                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    for (int i = 0; i < seen.Count; i++)
                    {
                        context.World.Knowledge.Teach(seen[i], lie.Id, KnowledgeSource.Witnessed, 0.9, context.Now, true);
                    }

                    outcome = new ActionOutcome(Id, check, "The " + item.Name + " is in " + patsy + "'s belongings now.");
                    outcome.Notes.Add("created a fact flagged False: the world knows this is a frame even though nobody in it does");
                    outcome.Events.Add(context.World.Record(WorldEventType.EvidenceCreated, context.Actor, context.ThirdParty, context.Now, 0.7, context.Zone, new[] { lie.Id }, seen, new[] { item.Id }));
                    break;
                }

                case CheckOutcome.Fail:
                    outcome = new ActionOutcome(Id, check, "You cannot get near enough to place it.");
                    break;

                default:
                {
                    // Seen doing it. Now the provable fact is about you.
                    IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
                    outcome = new ActionOutcome(Id, check, "You are seen slipping the " + item.Name + " into " + patsy + "'s things.");
                    outcome.Events.Add(context.World.Record(WorldEventType.FalseAccusation, context.Actor, context.ThirdParty, context.Now, 0.8, context.Zone, witnesses: seen, evidence: new[] { item.Id }));
                    break;
                }
            }

            return outcome;
        }

        private static ItemDescriptor FindPlantable(ActionContext context)
        {
            IReadOnlyList<ItemDescriptor> inventory = context.Vanilla.GetInventory(context.Actor);
            for (int i = 0; i < inventory.Count; i++)
            {
                if (context.SubjectItem.IsNone || inventory[i].Id == context.SubjectItem)
                {
                    return inventory[i];
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Start a fight.
    ///
    /// Deliberately thin: the mod does not resolve combat. Elin does that far better than a
    /// narrative layer could, so this records intent and lets the game decide the rest. Reading
    /// the result back in is the adapter's job.
    /// </summary>
    public sealed class AttackAction : NarrativeAction
    {
        public AttackAction() : base("attack", ActionFamily.Physical, "Attack")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (context.Target.IsNone || !context.Vanilla.IsAlive(context.Target))
            {
                return Availability.NotRelevant("nobody to fight");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            IReadOnlyList<EntityId> seen = ActionSupport.Bystanders(context, true);
            ActionOutcome outcome = new ActionOutcome(Id, null, "You go for " + context.NameOf(context.Target) + ".");
            outcome.Events.Add(context.World.Record(WorldEventType.Attacked, context.Actor, context.Target, context.Now, 0.9, context.Zone, witnesses: seen));
            outcome.Notes.Add("no check: vanilla combat resolves this, and the adapter records how it ended");
            return outcome;
        }
    }
}
