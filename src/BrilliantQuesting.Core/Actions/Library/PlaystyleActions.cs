using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Actions.Library
{
    /// <summary>BQ-122 routes for Elin playstyles that are not just talking, crafting or buying.</summary>
    public sealed class PerformForCrowdAction : NarrativeAction
    {
        public PerformForCrowdAction() : base("perform", ActionFamily.Social, "Perform for them")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here needs the room won over");
            }

            return FindSocialTrouble(context) == null
                ? Availability.NotRelevant("there is no social trouble a performance can soften")
                : Availability.Available("uses Music to turn the room toward " + context.NameOf(context.Target));
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact trouble = FindSocialTrouble(context);
            if (trouble == null)
            {
                ActionOutcome none = new ActionOutcome(Id, null, "There is no room to win over.");
                none.Notes.Add("no open social trouble");
                return none;
            }

            CheckResult check = context.Checks.Resolve(
                new CheckRequest(ProceduralCheckProfiles.Performance, context.Actor, context.Target),
                context.Rng);
            if (!check.Succeeded)
            {
                ActionOutcome missed = new ActionOutcome(Id, check, "The song lands badly, and the room stays cold.");
                missed.Events.Add(context.World.Record(
                    WorldEventType.Conversed,
                    context.Actor,
                    context.Target,
                    context.Now,
                    0.1,
                    context.Zone,
                    related: new[] { trouble.Id },
                    witnesses: ActionSupport.Bystanders(context, true),
                    threadId: ThreadId(context)));
                return missed;
            }

            trouble.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, check,
                "You play until the room is listening to " + context.NameOf(context.Target) + " differently.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Conversed,
                context.Actor,
                context.Target,
                context.Now,
                0.5,
                context.Zone,
                related: new[] { trouble.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                context.Target,
                context.Now,
                0.5,
                context.Zone,
                related: new[] { trouble.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Notes.Add("social trouble softened through Music");
            ActionSupport.Resolve(context, outcome, "performed_for_them", 0.5);
            return outcome;
        }

        private static Fact FindSocialTrouble(ActionContext context)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsOpenSocialTrouble(context, named))
                {
                    return named;
                }
            }

            if (context.Thread == null)
            {
                return null;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (IsOpenSocialTrouble(context, fact))
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsOpenSocialTrouble(ActionContext context, Fact fact)
        {
            return fact != null
                   && fact.Predicate == FactPredicates.AtRisk
                   && fact.Truth == TruthState.True
                   && fact.Subject == context.Target
                   && (fact.Value == null || fact.Value.IndexOf("social", System.StringComparison.OrdinalIgnoreCase) >= 0
                       || fact.Value.IndexOf("shame", System.StringComparison.OrdinalIgnoreCase) >= 0
                       || fact.Value.IndexOf("honour", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static EntityId ThreadId(ActionContext context) => context.Thread?.Id ?? EntityId.None;
    }

    public sealed class DonateToMuseumAction : NarrativeAction
    {
        private static readonly string[] MuseumKinds = { "artifact", "curio", "relic", "museum", "statue", "painting" };

        public DonateToMuseumAction() : base("donate_to_museum", ActionFamily.Economic, "Donate to the museum")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("no curator here to receive a donation");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (FindDebtOfHonour(context) == null)
            {
                return Availability.NotRelevant("there is no debt of honour this donation would settle");
            }

            return FindMuseumPiece(context) == null
                ? Availability.Impossible("you are carrying nothing the museum would take")
                : Availability.Available("settles the honour debt with a real museum piece");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact debt = FindDebtOfHonour(context);
            ItemDescriptor piece = FindMuseumPiece(context);
            if (debt == null || piece == null || !context.Vanilla.TryTransferItem(piece.Id, context.Actor, context.Target))
            {
                ActionOutcome none = new ActionOutcome(Id, null, "The donation cannot be made.");
                none.Notes.Add("missing honour debt, museum piece, or transfer support");
                return none;
            }

            debt.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You place " + piece.Name + " with the museum, and " + context.NameOf(context.Target) + " accepts the debt as settled.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.ItemGiven,
                context.Actor,
                context.Target,
                context.Now,
                0.6,
                context.Zone,
                related: new[] { debt.Id },
                evidence: new[] { piece.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Events.Add(context.World.Record(
                WorldEventType.DebtPaid,
                context.Actor,
                context.Target,
                context.Now,
                0.6,
                context.Zone,
                related: new[] { debt.Id },
                evidence: new[] { piece.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Notes.Add("honour debt settled by museum donation, not a loot payout");
            ActionSupport.Resolve(context, outcome, "honour_debt_settled", 0.6);
            return outcome;
        }

        private static Fact FindDebtOfHonour(ActionContext context)
        {
            if (context.Thread == null)
            {
                return null;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null
                    && fact.Predicate == FactPredicates.Owes
                    && fact.Truth == TruthState.True
                    && fact.Object == context.Target
                    && (context.ThirdParty.IsNone || fact.Subject == context.ThirdParty)
                    && ActionSupport.LooksLike(fact.Value, new[] { "honour", "honor" }))
                {
                    return fact;
                }
            }

            return null;
        }

        private static ItemDescriptor FindMuseumPiece(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, item => ActionSupport.LooksLike(item, MuseumKinds));
        }

        private static EntityId ThreadId(ActionContext context) => context.Thread?.Id ?? EntityId.None;
    }

    public sealed class GiveBredAnimalAction : NarrativeAction
    {
        private static readonly string[] AnimalKinds = { "animal", "livestock", "pet", "mount", "egg" };

        public GiveBredAnimalAction() : base("give_bred_animal", ActionFamily.HomeCommunity, "Give a bred animal")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here can receive the animal");
            }

            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("item transfers are unavailable on this build");
            }

            if (FindGiftMatter(context) == null)
            {
                return Availability.NotRelevant("there is no animal gift matter here");
            }

            return FindBredAnimal(context) == null
                ? Availability.Impossible("you are carrying no animal with breeding provenance")
                : Availability.Available("gives a bred animal as a relationship-changing gift");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact matter = FindGiftMatter(context);
            ItemDescriptor animal = FindBredAnimal(context);
            if (matter == null || animal == null || !context.Vanilla.TryTransferItem(animal.Id, context.Actor, context.Target))
            {
                ActionOutcome none = new ActionOutcome(Id, null, "There is no animal here to give.");
                none.Notes.Add("missing animal gift matter or bred animal in the actor's keeping");
                return none;
            }

            matter.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, null,
                "You give " + animal.Name + " to " + context.NameOf(context.Target) + ".");
            EntityId provenance = ProducedFact(context, animal.Id);
            EntityId[] related = provenance.IsNone ? new[] { matter.Id } : new[] { matter.Id, provenance };
            outcome.Events.Add(context.World.Record(
                WorldEventType.ItemGiven,
                context.Actor,
                context.Target,
                context.Now,
                0.7,
                context.Zone,
                related: related,
                evidence: new[] { animal.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Notes.Add("bred animal gift uses carried livestock/pet provenance");
            ActionSupport.Resolve(context, outcome, "bred_animal_given", 0.7);
            return outcome;
        }

        private static ItemDescriptor FindBredAnimal(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, item =>
                ActionSupport.LooksLike(item, AnimalKinds) && !ProducedFact(context, item.Id).IsNone);
        }

        private static Fact FindGiftMatter(ActionContext context)
        {
            if (!context.SubjectFact.IsNone)
            {
                Fact named = context.World.Knowledge.GetFact(context.SubjectFact);
                if (IsGiftMatter(context, named))
                {
                    return named;
                }
            }

            if (context.Thread == null)
            {
                return null;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (IsGiftMatter(context, fact))
                {
                    return fact;
                }
            }

            return null;
        }

        private static bool IsGiftMatter(ActionContext context, Fact fact)
        {
            return fact != null
                   && fact.Subject == context.Target
                   && fact.Truth == TruthState.True
                   && fact.Predicate == FactPredicates.AtRisk
                   && ActionSupport.LooksLike(fact.Value, new[] { "animal", "pet", "companionship", "lonely", "ranch", "relationship" });
        }

        private static EntityId ProducedFact(ActionContext context, EntityId item)
        {
            foreach (Fact fact in context.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.Produced
                    && fact.Subject == context.Actor
                    && fact.Object == item
                    && fact.Truth == TruthState.True)
                {
                    return fact.Id;
                }
            }

            return EntityId.None;
        }

        private static EntityId ThreadId(ActionContext context) => context.Thread?.Id ?? EntityId.None;
    }

    public sealed class DeliverFishingHaulAction : SupplyPlaystyleAction
    {
        public DeliverFishingHaulAction() : base(
            "deliver_fishing_haul",
            "Bring a fishing haul",
            ProceduralCheckProfiles.FishingHaul,
            new[] { "food", "meal", "ration", "shortage" },
            new[] { "fish", "seafood" },
            "fishing haul")
        {
        }
    }

    public sealed class DeliverHarvestAction : SupplyPlaystyleAction
    {
        public DeliverHarvestAction() : base(
            "deliver_harvest",
            "Bring a harvest",
            ProceduralCheckProfiles.Harvest,
            new[] { "food", "meal", "ration", "crop", "grain", "vegetable", "fruit", "medicine", "remedy", "herb" },
            new[] { "crop", "grain", "vegetable", "fruit", "herb" },
            "harvest")
        {
        }
    }

    public abstract class SupplyPlaystyleAction : NarrativeAction
    {
        private readonly CheckProfile _profile;
        private readonly string[] _demandKinds;
        private readonly string[] _stockKinds;
        private readonly string _routeName;

        protected SupplyPlaystyleAction(string id, string label, CheckProfile profile, string[] demandKinds, string[] stockKinds, string routeName)
            : base(id, ActionFamily.Crafting, label)
        {
            _profile = profile;
            _demandKinds = demandKinds;
            _stockKinds = stockKinds;
            _routeName = routeName;
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody here is short of anything");
            }

            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            if (demand == null)
            {
                return Availability.NotRelevant("nobody here has asked for supplies");
            }

            if (!context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("supplies cannot be consumed on this build");
            }

            if (!ActionSupport.LooksLike(spec.CategoryTag, _demandKinds))
            {
                return Availability.NotRelevant("a " + _routeName + " does not answer " + spec.CategoryTag);
            }

            ItemDescriptor haul = FindSupply(context);
            if (haul == null)
            {
                return Availability.Impossible("you are carrying no " + _routeName + " that can answer " + spec.CategoryTag);
            }

            return Availability.Available("uses a real " + _routeName + " to answer " + spec.Describe());
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact demand = ProductionDemand.Find(context, out ProductionSpec spec);
            ItemDescriptor supply = FindSupply(context);
            if (demand == null
                || !ActionSupport.LooksLike(spec.CategoryTag, _demandKinds)
                || supply == null
                || !context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                ActionOutcome none = new ActionOutcome(Id, null, "There are no supplies to bring.");
                none.Notes.Add("missing demand, supply, or consumption support");
                return none;
            }

            CheckRequest request = new CheckRequest(_profile, context.Actor, EntityId.None)
                .WithModifier("the standard they set", spec.MinimumQuality / 4);
            CheckResult check = context.Checks.Resolve(request, context.Rng);
            if (!context.Vanilla.TryDestroyItem(supply.Id, context.Actor))
            {
                ActionOutcome missing = new ActionOutcome(Id, check, "The " + supply.Name + " is not where you thought it was.");
                missing.Notes.Add("supply consumption refused");
                return missing;
            }

            if (!check.Succeeded)
            {
                ActionOutcome failed = new ActionOutcome(Id, check, "The " + _routeName + " spoils before it answers the need.");
                failed.Notes.Add("supply spent; demand still stands");
                return failed;
            }

            demand.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, check,
                "You bring in " + supply.Name + ", and " + context.NameOf(context.Target) + " can cover the shortage.");
            ActionSupport.RelieveDemand(context, demand, spec, outcome, 30, 3);
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                context.Target,
                context.Now,
                0.6,
                context.Zone,
                related: new[] { demand.Id },
                evidence: new[] { supply.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: ThreadId(context)));
            outcome.Notes.Add(_routeName + " answered " + spec.Describe());
            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                ActionSupport.Resolve(context, outcome, Id == "deliver_fishing_haul" ? "shortage_fished" : "shortage_harvested", 0.6);
            }

            return outcome;
        }

        private ItemDescriptor FindSupply(ActionContext context)
        {
            return ActionSupport.FindItem(context, context.Actor, item => ActionSupport.LooksLike(item, _stockKinds));
        }

        private static EntityId ThreadId(ActionContext context) => context.Thread?.Id ?? EntityId.None;
    }
}
