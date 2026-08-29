using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-026. A village short of two things, and a player carrying raw stuff and one sack of
    /// something that is food but is not bread.
    ///
    /// The step's done-when is two claims, and they are tested apart: a shortage can be answered
    /// by actually producing goods, and the standard being asked for decides the answer rather
    /// than merely making it less likely.
    /// </summary>
    public class ProductionActionTests
    {
        [Fact]
        public void EveryProductionVerbIsRegisteredWithACheckAndAFamily()
        {
            string[] verbs = { "cook", "brew", "alchemy", "repair", "build", "craft_to_property" };

            ActionRegistry registry = StandardActions.CreateRegistry();
            foreach (string verb in verbs)
            {
                Assert.NotNull(registry.Get(verb));
                Assert.NotNull(ProceduralCheckProfiles.ForAction(verb));
                Assert.Equal(ActionFamily.Crafting, registry.Get(verb).Family);
            }
        }

        // -- the threshold ------------------------------------------------------------------

        /// <summary>
        /// The heart of the step. Coarse meal is food, and the reeve will not take it - not
        /// because the roll would go badly but because it is the wrong object, so the verb refuses
        /// with a reason that names the gap instead of offering a route that could never land.
        /// </summary>
        [Fact]
        public void GoodsUnderTheStandardAreTheWrongObjectRatherThanALongShot()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.LeaveOnly("coarse meal");

            Availability availability = lab.Can("cook", lab.Village, lab.Reeve);

            Assert.False(availability.IsAvailable);
            Assert.Contains("quality 10", availability.Reason);
            Assert.Contains("30", availability.Reason);
        }

        /// <summary>
        /// And the same object, made better, is simply accepted. Nothing about the player changed
        /// between these two tests - only the thing in their hands.
        /// </summary>
        [Fact]
        public void GoodsThatMeetTheStandardAreHandedOverWithNoRollAtAll()
        {
            ShortageLab lab = ShortageLab.Create();
            EntityId loaf = lab.PlayerCrafted("a fine loaf", "food", 40, 55);

            ActionOutcome outcome = lab.Run("cook", lab.Village, lab.Reeve);

            Assert.Null(outcome.Check);
            Assert.Contains(loaf, lab.Held(lab.Reeve));
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped);
        }

        /// <summary>
        /// The reason the hand-over rolls nothing: Elin made the loaf, and the ledger already
        /// knows it did. A procedural roll here would be a second crafting mechanic disagreeing
        /// with the first.
        /// </summary>
        [Fact]
        public void ProductionElinFinishedIsRecordedAsProvenanceWithoutACheck()
        {
            ShortageLab lab = ShortageLab.Create();
            EntityId loaf = lab.PlayerCrafted("a fine loaf", "food", 40, 55);

            Fact made = lab.SingleFact(FactPredicates.Produced);
            Assert.Equal(lab.Player, made.Subject);
            Assert.Equal(loaf, made.Object);
            Assert.Contains(loaf, made.EvidenceIds);
            Assert.Contains(lab.World.Ledger.OfType(WorldEventType.GoodsProduced), e => e.Actor == lab.Player);
        }

        /// <summary>A demanding standard eats more of what you brought, not just better rolls.</summary>
        [Fact]
        public void TheStandardDecidesHowMuchStockTheWorkTakes()
        {
            ShortageLab bread = ShortageLab.Create();
            bread.Run("cook", bread.Village, bread.Reeve);
            Assert.Equal(2, bread.Consumed());

            ShortageLab remedy = ShortageLab.Create();
            remedy.Run("alchemy", remedy.Village, remedy.Physician);
            Assert.Equal(3, remedy.Consumed());
        }

        /// <summary>Too little stock for the standard is impossible, and says which way it is short.</summary>
        [Fact]
        public void NotEnoughStockForTheStandardIsImpossible()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.Remove("sorrel");

            Availability availability = lab.Can("alchemy", lab.Village, lab.Physician);

            Assert.False(availability.IsAvailable);
            Assert.Contains("3", availability.Reason);
        }

        // -- working from stock -------------------------------------------------------------

        [Fact]
        public void WorkingFromStockConsumesItAndFillsTheDemand()
        {
            ShortageLab lab = ShortageLab.Create();

            ActionOutcome outcome = lab.Run("cook", lab.Village, lab.Reeve);

            Assert.True(outcome.Succeeded);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Target == lab.Reeve);
        }

        [Fact]
        public void ACriticalPassWastesNothing()
        {
            ShortageLab lab = ShortageLab.Create(CheckOutcome.CriticalPass);

            ActionOutcome outcome = lab.Run("cook", lab.Village, lab.Reeve);

            Assert.True(outcome.Succeeded);
            Assert.Equal(1, lab.Consumed());
        }

        [Fact]
        public void AFailedBatchIsWastedAndTheDemandStillStands()
        {
            ShortageLab lab = ShortageLab.Create(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Run("cook", lab.Village, lab.Reeve);

            Assert.False(outcome.Succeeded);
            Assert.Equal(2, lab.Consumed());
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.DoesNotContain(outcome.Events, e => e.Type == WorldEventType.Helped);
        }

        [Fact]
        public void ARuinedBatchTakesTheRestOfTheBenchWithIt()
        {
            ShortageLab lab = ShortageLab.Create(CheckOutcome.CriticalFail);

            lab.Run("cook", lab.Village, lab.Reeve);

            Assert.Equal(3, lab.Consumed());
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BreadDemandId).Truth);
        }

        /// <summary>
        /// The whole point of the demand living in the graph rather than in a quest step: two
        /// people are short, and the thread is not over while either of them still is.
        /// </summary>
        [Fact]
        public void TheThreadEndsOnlyWhenNothingIsStillWanted()
        {
            ShortageLab lab = ShortageLab.Create();

            lab.Run("cook", lab.Village, lab.Reeve);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);

            lab.Run("alchemy", lab.Village, lab.Physician);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Equal("need_met", lab.Situation.Thread.Resolution);
        }

        /// <summary>The generalist is available wherever a named craft is; the tiers rely on it.</summary>
        [Fact]
        public void TheGeneralistCoversTheSameDemandsTheNamedCraftsDo()
        {
            ShortageLab lab = ShortageLab.Create();

            Assert.True(lab.Can("craft_to_property", lab.Village, lab.Reeve).IsAvailable);
            Assert.True(lab.Can("craft_to_property", lab.Village, lab.Physician).IsAvailable);

            ActionOutcome outcome = lab.Run("craft_to_property", lab.Village, lab.Reeve);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.Equal("proc_craftsmanship", outcome.Check.ProfileId);
        }

        [Fact]
        public void ACraftIsNotOfferedForWorkItDoesNotDo()
        {
            ShortageLab lab = ShortageLab.Create();

            Assert.False(lab.Can("cook", lab.Village, lab.Physician).IsAvailable);
            Assert.False(lab.Can("alchemy", lab.Village, lab.Reeve).IsAvailable);
        }

        [Fact]
        public void NobodyShortOfAnythingMeansNoProductionRoute()
        {
            ShortageLab lab = ShortageLab.Create();

            Assert.False(lab.Can("cook", lab.Village, lab.Miller).IsAvailable);
        }

        [Fact]
        public void MaterialsThatCannotBeUsedUpCloseTheStockRoute()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.Vanilla.SetCapability(VanillaCapability.DestroyItems, false);

            Availability availability = lab.Can("cook", lab.Village, lab.Reeve);

            Assert.False(availability.IsAvailable);
            Assert.Contains("used up", availability.Reason);
        }

        // -- mending the cause ---------------------------------------------------------------

        /// <summary>
        /// The other half of the family. One repair ends the bread shortage for good, because the
        /// demand names the wheel as its cause - and leaves the remedy untouched, because that
        /// shortage has no machine behind it.
        /// </summary>
        [Fact]
        public void MendingTheCauseClosesTheShortageItWasCausing()
        {
            ShortageLab lab = ShortageLab.Create();

            ActionOutcome outcome = lab.Run("repair", lab.Mill, EntityId.None);

            Assert.True(outcome.Succeeded);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.WheelDamageId).Truth);
            Assert.Equal(TruthState.Superseded, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.RemedyDemandId).Truth);
            Assert.Equal(ThreadState.Active, lab.Situation.Thread.State);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Helped && e.Target == lab.Miller);
        }

        /// <summary>A botched repair does not leave the thing where it was. The route is gone.</summary>
        [Fact]
        public void ABotchedRepairFinishesTheThingOff()
        {
            ShortageLab lab = ShortageLab.Create(CheckOutcome.CriticalFail);

            ActionOutcome outcome = lab.Run("repair", lab.Mill, EntityId.None);

            Assert.False(outcome.Succeeded);
            Assert.DoesNotContain(lab.Situation.MillWheelId, lab.Held(lab.Mill));
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.WheelDamageId).Truth);
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.BreadDemandId).Truth);
            Assert.False(lab.Can("repair", lab.Mill, EntityId.None).IsAvailable);
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Harmed);
        }

        [Fact]
        public void AFailedRepairSpendsThePartsAndLeavesItBroken()
        {
            ShortageLab lab = ShortageLab.Create(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Run("repair", lab.Mill, EntityId.None);

            Assert.False(outcome.Succeeded);
            Assert.Equal(1, lab.Consumed());
            Assert.Equal(TruthState.True, lab.Fact(lab.Situation.WheelDamageId).Truth);
            Assert.True(lab.Can("repair", lab.Mill, EntityId.None).IsAvailable);
        }

        /// <summary>Reach, not a menu entry: the wheel is in the mill and so must you be.</summary>
        [Fact]
        public void YouCannotMendSomethingYouAreNotStandingInFrontOf()
        {
            ShortageLab lab = ShortageLab.Create();

            Assert.False(lab.Can("repair", lab.Village, EntityId.None).IsAvailable);
            Assert.True(lab.Can("repair", lab.Mill, EntityId.None).IsAvailable);
        }

        [Fact]
        public void MendingNeedsSomethingToMendItWith()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.Remove("plank");

            Availability availability = lab.Can("repair", lab.Mill, EntityId.None);

            Assert.False(availability.IsAvailable);
            Assert.Contains("nothing to mend", availability.Reason);
        }

        // -- the done-when ------------------------------------------------------------------

        /// <summary>
        /// "A shortage can be answered by actually producing goods." The other half of that claim
        /// is that nothing else answers it: every verb outside the crafting family is run against
        /// everyone in the village, on its own copy of the world, and neither demand ever closes.
        /// </summary>
        [Fact]
        public void NothingOutsideTheCraftingFamilyCanCloseAShortage()
        {
            List<string> closed = new List<string>();
            foreach (NarrativeAction action in StandardActions.CreateRegistry().Actions)
            {
                if (action.Family == ActionFamily.Crafting)
                {
                    continue;
                }

                for (int who = 0; who < ShortageLab.PeopleToTry; who++)
                {
                    ShortageLab lab = ShortageLab.Create();
                    EntityId target = lab.Everyone()[who];
                    foreach (EntityId zone in new[] { lab.Village, lab.Mill })
                    {
                        if (lab.Can(action.Id, zone, target).IsAvailable)
                        {
                            lab.Run(action.Id, zone, target);
                        }
                    }

                    if (lab.Fact(lab.Situation.BreadDemandId).Truth != TruthState.True
                        || lab.Fact(lab.Situation.RemedyDemandId).Truth != TruthState.True)
                    {
                        closed.Add(action.Id);
                    }
                }
            }

            Assert.Empty(closed);
        }

        /// <summary>And the crafting family does have a route, from the same starting state.</summary>
        [Fact]
        public void TheCraftingFamilyIsOpenWhereTheOthersAreNot()
        {
            ShortageLab lab = ShortageLab.Create();

            Assert.Contains(ActionFamily.Crafting, lab.Actions.AvailableFamilies(lab.Context(lab.Village, lab.Reeve)));
        }

        // -- the specification ----------------------------------------------------------------

        [Fact]
        public void ASpecificationRoundTripsThroughTheFactItLivesIn()
        {
            ProductionSpec spec = new ProductionSpec("medicine", 40, 250);

            ProductionSpec parsed = ProductionSpec.Parse(spec.ToFactValue());

            Assert.Equal("medicine", parsed.CategoryTag);
            Assert.Equal(40, parsed.MinimumQuality);
            Assert.Equal(250, parsed.MinimumValue);
        }

        /// <summary>
        /// A demand with no threshold is a real and common thing to want, and a malformed one
        /// costs its thresholds rather than the whole route.
        /// </summary>
        [Fact]
        public void ASpecificationWithoutThresholdsIsStillASpecification()
        {
            Assert.Equal(0, ProductionSpec.Parse("timber").MinimumQuality);
            Assert.Equal(0, ProductionSpec.Parse("timber quality").MinimumQuality);
            Assert.Equal(0, ProductionSpec.Parse("timber quality plenty").MinimumQuality);
            Assert.Null(ProductionSpec.Parse("   "));
            Assert.Null(ProductionSpec.Parse(null));
        }

        [Fact]
        public void WorthIsAThresholdOfItsOwn()
        {
            ProductionSpec spec = new ProductionSpec("furniture", 0, 500);

            Assert.False(spec.Accepts(new ItemDescriptor(EntityId.Parse("item_1"), "a stool", "furniture", 40)));
            Assert.True(spec.Accepts(new ItemDescriptor(EntityId.Parse("item_2"), "a carved chair", "furniture", 900)));
        }

        /// <summary>
        /// Quality zero means "nobody read it", and a threshold refuses it. That is the safe
        /// direction: a demand with a standard would rather turn away an object nobody can vouch
        /// for than accept one on the strength of a field the adapter never filled in.
        /// </summary>
        [Fact]
        public void AnUnreadQualityIsRefusedByAnyThreshold()
        {
            ProductionSpec spec = new ProductionSpec("food", 1);

            Assert.False(spec.Accepts(new ItemDescriptor(EntityId.Parse("item_1"), "a loaf", "food", 900)));
        }

        /// <summary>
        /// The specification lives in a fact's free value, so a save is the one place it could
        /// quietly stop being readable. A half-solved shortage is reloaded and the demand still
        /// says what it wanted.
        /// </summary>
        [Fact]
        public void AHalfSolvedShortageSurvivesASaveWithItsSpecificationIntact()
        {
            ShortageLab lab = ShortageLab.Create();
            lab.Run("cook", lab.Village, lab.Reeve);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));

            Fact bread = reloaded.Knowledge.GetFact(lab.Situation.BreadDemandId);
            Fact remedy = reloaded.Knowledge.GetFact(lab.Situation.RemedyDemandId);
            Assert.Equal(TruthState.Superseded, bread.Truth);
            Assert.Equal(TruthState.True, remedy.Truth);

            ProductionSpec spec = ProductionSpec.Parse(remedy.Value);
            Assert.Equal("medicine", spec.CategoryTag);
            Assert.Equal(40, spec.MinimumQuality);
        }

        /// <summary>Kell's Ford, its two shortages, and a pack full of raw stuff.</summary>
        private sealed class ShortageLab
        {
            private ShortageLab()
            {
            }

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public FixedCheckResolver Checks { get; private set; }

            public EntityId Player { get; private set; }

            public ShortageSituation Situation { get; private set; }

            private int _startingPack;

            public EntityId Village => Situation.VillageZoneId;

            public EntityId Mill => Situation.MillZoneId;

            public EntityId Reeve => Situation.ReeveId;

            public EntityId Physician => Situation.PhysicianId;

            public EntityId Miller => Situation.MillerId;

            public static ShortageLab Create(CheckOutcome outcome = CheckOutcome.Pass)
            {
                ShortageLab lab = new ShortageLab();
                NarrativeWorldState world = new NarrativeWorldState(26026);
                EntityId player = world.NewId("npc");
                EntityId village = world.NewId("zone");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 5, money: 400, zone: village);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(outcome);

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = ShortageSituation.Create(world, stager, player, village, vanilla.Now);
                lab.Situation.StockThePlayer(world, stager, player);
                lab._startingPack = vanilla.GetInventory(player).Count;

                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            /// <summary>Everyone in the village, plus nobody - which is a real target for `repair`.</summary>
            public const int PeopleToTry = 4;

            public List<EntityId> Everyone()
            {
                return new List<EntityId> { Reeve, Physician, Miller, EntityId.None };
            }

            public Fact Fact(EntityId id) => World.Knowledge.GetFact(id);

            public Fact SingleFact(string predicate)
            {
                foreach (Fact fact in World.Knowledge.Facts.Values)
                {
                    if (fact.Predicate == predicate)
                    {
                        return fact;
                    }
                }

                return null;
            }

            /// <summary>How much of the starting pack the work has eaten.</summary>
            public int Consumed() => _startingPack - Vanilla.GetInventory(Player).Count;

            public List<EntityId> Held(EntityId owner)
            {
                List<EntityId> ids = new List<EntityId>();
                foreach (ItemDescriptor item in Vanilla.GetInventory(owner))
                {
                    ids.Add(item.Id);
                }

                return ids;
            }

            /// <summary>
            /// Something Elin finished making, arriving the way it really would - through the
            /// observation seam, which is what writes the provenance the graph then holds.
            /// </summary>
            public EntityId PlayerCrafted(string name, string category, int quality, int value)
            {
                EntityId id = World.NewId("item");
                Vanilla.GiveItem(Player, new ItemDescriptor(id, name, category, value, null, quality));
                _startingPack = Vanilla.GetInventory(Player).Count;
                new VanillaActionRecorder(World, Vanilla).Record(new ObservedVanillaAction(
                    ObservedVanillaActionKind.Crafted, Player, EntityId.None, id, name, Village, "ActCraft"));
                return id;
            }

            /// <summary>Takes everything out of the pack but the one thing named.</summary>
            public void LeaveOnly(string keep)
            {
                foreach (ItemDescriptor item in new List<ItemDescriptor>(Vanilla.GetInventory(Player)))
                {
                    if (!item.Name.Contains(keep))
                    {
                        Vanilla.DestroyItem(item.Id);
                    }
                }

                _startingPack = Vanilla.GetInventory(Player).Count;
            }

            public void Remove(string named)
            {
                foreach (ItemDescriptor item in new List<ItemDescriptor>(Vanilla.GetInventory(Player)))
                {
                    if (item.Name.Contains(named))
                    {
                        Vanilla.DestroyItem(item.Id);
                    }
                }

                _startingPack = Vanilla.GetInventory(Player).Count;
            }

            public ActionContext Context(EntityId zone, EntityId target)
            {
                Vanilla.SetZone(Player, zone);
                return new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };
            }

            public ActionOutcome Run(string actionId, EntityId zone, EntityId target)
            {
                return Actions.Get(actionId).Perform(Context(zone, target));
            }

            public Availability Can(string actionId, EntityId zone, EntityId target)
            {
                return Actions.Get(actionId).GetAvailability(Context(zone, target));
            }
        }
    }
}
