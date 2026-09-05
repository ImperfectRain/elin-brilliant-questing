using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Actions.Library
{
    public sealed class PhysicalObstacleSpec
    {
        public PhysicalObstacleSpec(string kind, int difficulty = 0)
        {
            Kind = string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim();
            Difficulty = difficulty;
        }

        public string Kind { get; }

        public int Difficulty { get; }

        public static readonly PhysicalObstacleSpec Rockfall = new PhysicalObstacleSpec("rockfall", 4);

        public string ToFactValue() => Kind + " difficulty " + Difficulty;

        public static PhysicalObstacleSpec Parse(string value)
        {
            string[] words = string.IsNullOrWhiteSpace(value)
                ? new string[0]
                : value.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            string kind = words.Length == 0 ? string.Empty : words[0];
            return new PhysicalObstacleSpec(kind, ActionSupport.ReadNumber(words, "difficulty"));
        }
    }

    public abstract class PhysicalBarrierAction : NarrativeAction, ISpatialRouteVerb
    {
        private readonly CheckProfile _profile;
        private readonly string[] _kinds;
        private readonly bool _removesBarrier;

        protected PhysicalBarrierAction(
            string id,
            string label,
            CheckProfile profile,
            string[] kinds,
            bool removesBarrier,
            SiteAffordance answers)
            : base(id, ActionFamily.Physical, label)
        {
            _profile = profile;
            _kinds = kinds;
            _removesBarrier = removesBarrier;

            // BQ-090. Answering a barrier means finding it first, and the barrier is a thing
            // standing in the place rather than something anybody is carrying - which is the one
            // read the live adapter does not have (`ELIN-Q-0008`). So the physical ways through a
            // place are proven headlessly and refused in game until that read exists, rather than
            // offered to a player they would do nothing for.
            List<VanillaCapability> needs = new List<VanillaCapability> { VanillaCapability.ReadPlaceContents };
            if (removesBarrier)
            {
                needs.Add(VanillaCapability.DestroyItems);
            }

            SpatialRoute = new SpatialRouteClaim(
                new[] { answers },
                RouteEvidence.SourceObserved,
                "reading the obstruction standing in the place (ELIN-Q-0008)",
                needs.ToArray());
        }

        public SpatialRouteClaim SpatialRoute { get; }

        public override Availability GetAvailability(ActionContext context)
        {
            Fact blockage = FindBlockage(context, out ItemDescriptor barrier, out NarrativeSite blockedSite, out PhysicalObstacleSpec spec);
            if (blockage == null)
            {
                return Availability.NotRelevant("there is no matching barrier here");
            }

            if (_removesBarrier && !context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                return Availability.Impossible("barriers cannot be removed on this build");
            }

            return Availability.Available("answers the " + barrier.Name + " blocking " + blockedSite.Name);
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact blockage = FindBlockage(context, out ItemDescriptor barrier, out NarrativeSite blockedSite, out PhysicalObstacleSpec spec);
            if (blockage == null)
            {
                ActionOutcome none = new ActionOutcome(Id, null, "There is no barrier here this will answer.");
                none.Notes.Add("no reachable matching blocks_access_to fact");
                return none;
            }

            if (_removesBarrier && !context.Vanilla.Supports(VanillaCapability.DestroyItems))
            {
                ActionOutcome unsupported = new ActionOutcome(Id, null, "This build cannot remove the barrier from the world.");
                unsupported.Notes.Add("DestroyItems unavailable");
                return unsupported;
            }

            CheckRequest request = new CheckRequest(_profile, context.Actor, EntityId.None)
                .WithModifier("the barrier", spec.Difficulty);
            CheckResult check = context.Checks.Resolve(request, context.Rng);

            switch (check.Outcome)
            {
                case CheckOutcome.CriticalPass:
                    return Open(context, blockage, barrier, blockedSite, check, "You make short work of " + barrier.Name + ".");
                case CheckOutcome.Pass:
                    return Open(context, blockage, barrier, blockedSite, check, "You get past " + barrier.Name + ".");
                case CheckOutcome.Fail:
                {
                    ActionOutcome failed = new ActionOutcome(Id, check, "The " + barrier.Name + " does not give.");
                    failed.Notes.Add("the barrier still blocks access");
                    return failed;
                }
                default:
                {
                    ActionOutcome botch = new ActionOutcome(Id, check, "The " + barrier.Name + " shifts badly, and you are caught in it.");
                    botch.Events.Add(context.World.Record(
                        WorldEventType.Harmed,
                        context.Actor,
                        context.Actor,
                        context.Now,
                        0.4,
                        context.Zone,
                        related: new[] { blockage.Id },
                        witnesses: ActionSupport.Bystanders(context, true),
                        evidence: new[] { barrier.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                    botch.Notes.Add("the route remains, but the failed attempt is now history");
                    return botch;
                }
            }
        }

        private ActionOutcome Open(ActionContext context, Fact blockage, ItemDescriptor barrier, NarrativeSite blockedSite, CheckResult check, string narration)
        {
            blockage.Truth = TruthState.Superseded;
            blockedSite.Admit(context.Actor);

            if (_removesBarrier)
            {
                context.Vanilla.TryDestroyItem(barrier.Id, context.Zone);
                context.World.Knowledge.RevokeProofOfItem(barrier.Id);
            }

            ActionOutcome outcome = new ActionOutcome(Id, check, narration);
            outcome.Events.Add(context.World.Record(
                WorldEventType.SiteCleared,
                context.Actor,
                blockedSite.Id,
                context.Now,
                check.Outcome == CheckOutcome.CriticalPass ? 0.8 : 0.6,
                context.Zone,
                related: new[] { blockage.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                evidence: new[] { barrier.Id },
                threadId: context.Thread?.Id ?? EntityId.None));

            outcome.Notes.Add("admitted to " + blockedSite.Name);
            ResolveThreadIfOpen(context, outcome);
            return outcome;
        }

        private static void ResolveThreadIfOpen(ActionContext context, ActionOutcome outcome)
        {
            if (context.Thread == null)
            {
                return;
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.BlocksAccessTo && fact.Truth == TruthState.True)
                {
                    return;
                }
            }

            ActionSupport.Resolve(context, outcome, "passage_opened", 0.7);
        }

        private Fact FindBlockage(ActionContext context, out ItemDescriptor barrier, out NarrativeSite blockedSite, out PhysicalObstacleSpec spec)
        {
            barrier = null;
            blockedSite = null;
            spec = null;

            List<EntityId> candidates = new List<EntityId>();
            if (!context.SubjectFact.IsNone)
            {
                candidates.Add(context.SubjectFact);
            }

            if (context.Thread != null)
            {
                candidates.AddRange(context.Thread.FactIds);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(candidates[i]);
                if (fact == null || fact.Predicate != FactPredicates.BlocksAccessTo || fact.Truth != TruthState.True)
                {
                    continue;
                }

                PhysicalObstacleSpec parsed = PhysicalObstacleSpec.Parse(fact.Value);
                if (!ActionSupport.LooksLike(parsed.Kind, _kinds))
                {
                    continue;
                }

                ItemDescriptor item = Find(context.Vanilla.GetInventory(context.Zone), fact.Object);
                NarrativeSite site = context.World.Registry.GetSite(fact.Subject);
                if (item != null && site != null && !site.Admits(context.Actor))
                {
                    barrier = item;
                    blockedSite = site;
                    spec = parsed;
                    return fact;
                }
            }

            return null;
        }

        private static ItemDescriptor Find(IReadOnlyList<ItemDescriptor> inventory, EntityId itemId)
        {
            for (int i = 0; i < inventory.Count; i++)
            {
                if (inventory[i] != null && inventory[i].Id == itemId)
                {
                    return inventory[i];
                }
            }

            return null;
        }
    }

    public sealed class ClearObstructionAction : PhysicalBarrierAction
    {
        public ClearObstructionAction() : base(
            "clear_obstruction",
            "Clear the obstruction",
            ProceduralCheckProfiles.Clearing,
            new[] { "obstruction", "rubble", "debris", "rockfall" },
            removesBarrier: true,
            answers: SiteAffordance.BreakableBarrier)
        {
        }
    }

    public sealed class MineBypassAction : PhysicalBarrierAction
    {
        public MineBypassAction() : base(
            "mine_bypass",
            "Mine a bypass",
            ProceduralCheckProfiles.MiningBypass,
            new[] { "rock", "stone", "ore", "cave", "mine", "rockfall" },
            removesBarrier: false,
            answers: SiteAffordance.DiggableBypass)
        {
        }
    }

    public sealed class BreakBarrierAction : PhysicalBarrierAction
    {
        public BreakBarrierAction() : base(
            "break_barrier",
            "Break the barrier",
            ProceduralCheckProfiles.Breaking,
            new[] { "barrier", "door", "gate", "barricade", "rockfall" },
            removesBarrier: true,
            answers: SiteAffordance.BreakableBarrier)
        {
        }
    }

    public sealed class CarryAction : NarrativeAction
    {
        public CarryAction() : base("carry", ActionFamily.Physical, "Carry it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("items cannot be moved on this build");
            }

            return FindReachableItem(context, out ItemDescriptor item)
                ? Availability.Available("can lift " + item.Name)
                : Availability.NotRelevant("nothing here to carry");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                ActionOutcome unsupported = new ActionOutcome(Id, null, "This build cannot move items.");
                unsupported.Notes.Add("TransferItems unavailable");
                return unsupported;
            }

            if (!FindReachableItem(context, out ItemDescriptor item))
            {
                return new ActionOutcome(Id, null, "There is nothing here to carry.");
            }

            CheckResult check = context.Checks.Resolve(new CheckRequest(ProceduralCheckProfiles.Carrying, context.Actor, EntityId.None), context.Rng);
            if (!check.Outcome.IsSuccess())
            {
                return new ActionOutcome(Id, check, "You cannot get a clean hold on " + item.Name + ".");
            }

            context.Vanilla.TryTransferItem(item.Id, context.Zone, context.Actor);
            ActionOutcome outcome = new ActionOutcome(Id, check, "You shoulder " + item.Name + ".");
            outcome.Events.Add(context.World.Record(WorldEventType.ItemGiven, context.Actor, context.Actor, context.Now, 0.1, context.Zone, evidence: new[] { item.Id }));
            return outcome;
        }

        internal static bool FindReachableItem(ActionContext context, out ItemDescriptor item)
        {
            IReadOnlyList<ItemDescriptor> here = context.Vanilla.GetInventory(context.Zone);
            for (int i = 0; i < here.Count; i++)
            {
                if (here[i] != null && (context.SubjectItem.IsNone || here[i].Id == context.SubjectItem))
                {
                    item = here[i];
                    return true;
                }
            }

            item = null;
            return false;
        }
    }

    public sealed class TransportAction : NarrativeAction
    {
        public TransportAction() : base("transport", ActionFamily.Physical, "Transport it")
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                return Availability.Impossible("items cannot be moved on this build");
            }

            if (!ActionSupport.Present(context, context.Target))
            {
                return Availability.NotRelevant("nobody to receive it");
            }

            return ActionSupport.FindItem(context, context.Actor) == null
                ? Availability.NotRelevant("nothing carried to transport")
                : Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            if (!context.Vanilla.Supports(VanillaCapability.TransferItems))
            {
                ActionOutcome unsupported = new ActionOutcome(Id, null, "This build cannot move items.");
                unsupported.Notes.Add("TransferItems unavailable");
                return unsupported;
            }

            ItemDescriptor item = ActionSupport.FindItem(context, context.Actor);
            if (item == null || context.Target.IsNone)
            {
                return new ActionOutcome(Id, null, "There is nothing ready to move.");
            }

            CheckResult check = context.Checks.Resolve(new CheckRequest(ProceduralCheckProfiles.Transport, context.Actor, EntityId.None), context.Rng);
            if (!check.Outcome.IsSuccess())
            {
                return new ActionOutcome(Id, check, "You fail to get " + item.Name + " there intact.");
            }

            context.Vanilla.TryTransferItem(item.Id, context.Actor, context.Target);
            ActionOutcome outcome = new ActionOutcome(Id, check, "You deliver " + item.Name + " to " + context.NameOf(context.Target) + ".");
            outcome.Events.Add(context.World.Record(WorldEventType.ItemGiven, context.Actor, context.Target, context.Now, 0.4, context.Zone, evidence: new[] { item.Id }));
            return outcome;
        }
    }

    public sealed class RescueAction : PhysicalPersonAction
    {
        public RescueAction() : base("rescue", "Rescue them", ProceduralCheckProfiles.Rescue, WorldEventType.Rescued)
        {
        }
    }

    public sealed class EscortAction : PhysicalPersonAction
    {
        public EscortAction() : base("escort", "Escort them", ProceduralCheckProfiles.Escort, WorldEventType.Helped)
        {
        }

        public override Availability GetAvailability(ActionContext context)
        {
            Availability baseAvailability = base.GetAvailability(context);
            if (!baseAvailability.IsAvailable)
            {
                return baseAvailability;
            }

            return FindEscortDemand(context, out ProductionSpec spec) == null
                ? baseAvailability
                : Availability.Available("escorts " + context.NameOf(context.Target) + " so " + spec.Describe() + " reaches town");
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            Fact demand = FindEscortDemand(context, out ProductionSpec spec);
            if (demand == null)
            {
                return base.Perform(context);
            }

            if (!ActionSupport.Present(context, context.Target))
            {
                return new ActionOutcome(Id, null, "There is nobody here for that.");
            }

            CheckResult check = context.Checks.Resolve(new CheckRequest(ProceduralCheckProfiles.Escort, context.Actor, context.Target), context.Rng);
            if (!check.Outcome.IsSuccess())
            {
                ActionOutcome failed = new ActionOutcome(Id, check, context.NameOf(context.Target) + " turns back before the supplies arrive.");
                failed.Notes.Add("the demand still stands");
                if (check.Outcome == CheckOutcome.CriticalFail)
                {
                    failed.Events.Add(context.World.Record(
                        WorldEventType.Harmed,
                        context.Actor,
                        context.Actor,
                        context.Now,
                        0.3,
                        context.Zone,
                        related: new[] { demand.Id },
                        threadId: context.Thread?.Id ?? EntityId.None));
                }

                return failed;
            }

            demand.Truth = TruthState.Superseded;
            ActionOutcome outcome = new ActionOutcome(Id, check,
                "You see " + context.NameOf(context.Target) + " through, and " + spec.Describe() + " reaches town.");
            outcome.Events.Add(context.World.Record(
                WorldEventType.Helped,
                context.Actor,
                demand.Subject,
                context.Now,
                check.Outcome == CheckOutcome.CriticalPass ? 0.85 : 0.65,
                context.Zone,
                related: new[] { demand.Id },
                witnesses: ActionSupport.Bystanders(context, true),
                threadId: context.Thread?.Id ?? EntityId.None));
            outcome.Notes.Add(context.NameOf(demand.Subject) + " is no longer short of " + spec.Describe());

            if (context.Thread != null && !ProductionDemand.AnyOpenIn(context.Thread, context.World.Knowledge))
            {
                ActionSupport.Resolve(context, outcome, "caravan_escorted", 0.65);
            }

            return outcome;
        }

        private static Fact FindEscortDemand(ActionContext context, out ProductionSpec spec)
        {
            spec = null;
            if (context.Thread == null || !ActionBinding.HasRequiredSemanticSlots("escort", context))
            {
                return null;
            }

            ActionBinding binding = ActionBinding.Infer(context);
            if (binding.HasProposition)
            {
                Fact named = context.World.Knowledge.GetFact(binding.PropositionFact);
                if (named != null
                    && named.Predicate == FactPredicates.Needs
                    && named.Truth == TruthState.True)
                {
                    spec = ProductionSpec.Parse(named.Value);
                    return spec == null ? null : named;
                }
            }

            for (int i = 0; i < context.Thread.FactIds.Count; i++)
            {
                Fact fact = context.World.Knowledge.GetFact(context.Thread.FactIds[i]);
                if (fact != null && fact.Predicate == FactPredicates.Needs && fact.Truth == TruthState.True)
                {
                    spec = ProductionSpec.Parse(fact.Value);
                    return spec == null ? null : fact;
                }
            }

            return null;
        }
    }

    public sealed class CaptureAction : PhysicalPersonAction
    {
        public CaptureAction() : base("capture", "Capture them", ProceduralCheckProfiles.Capture, WorldEventType.Captured)
        {
        }
    }

    public sealed class RestrainAction : PhysicalPersonAction
    {
        public RestrainAction() : base("restrain", "Restrain them", ProceduralCheckProfiles.Restrain, WorldEventType.Captured)
        {
        }
    }

    public abstract class PhysicalPersonAction : NarrativeAction
    {
        private readonly CheckProfile _profile;
        private readonly WorldEventType _eventType;

        protected PhysicalPersonAction(string id, string label, CheckProfile profile, WorldEventType eventType)
            : base(id, ActionFamily.Physical, label)
        {
            _profile = profile;
            _eventType = eventType;
        }

        public override Availability GetAvailability(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target) || context.Target == context.Actor)
            {
                return Availability.NotRelevant("nobody here to " + Label.ToLowerInvariant());
            }

            if (!ActionBinding.HasRequiredSemanticSlots(Id, context))
            {
                return Availability.NotRelevant("no destination or persistent objective");
            }

            return Availability.Available();
        }

        public override ActionOutcome Perform(ActionContext context)
        {
            if (!ActionSupport.Present(context, context.Target))
            {
                return new ActionOutcome(Id, null, "There is nobody here for that.");
            }

            CheckResult check = context.Checks.Resolve(new CheckRequest(_profile, context.Actor, context.Target), context.Rng);
            if (!check.Outcome.IsSuccess())
            {
                ActionOutcome failed = new ActionOutcome(Id, check, context.NameOf(context.Target) + " gets clear.");
                if (check.Outcome == CheckOutcome.CriticalFail)
                {
                    failed.Events.Add(context.World.Record(WorldEventType.Harmed, context.Actor, context.Actor, context.Now, 0.3, context.Zone));
                }

                return failed;
            }

            ActionOutcome outcome = new ActionOutcome(Id, check, context.NameOf(context.Target) + " is moved by force and circumstance.");
            outcome.Events.Add(context.World.Record(_eventType, context.Actor, context.Target, context.Now, 0.6, context.Zone, witnesses: ActionSupport.Bystanders(context, true)));
            return outcome;
        }
    }
}
