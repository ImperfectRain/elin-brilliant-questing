using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-012. What actually happens comes back round as what presses next.
    ///
    /// BQa-006 made a pressure reading affordable by letting the caller say what changed, and then
    /// nothing said it: every consumer either read the whole save or read a stale world. This is
    /// the seam that closes that, and the properties worth testing are not "does it collect ids" -
    /// they are the ones that decide whether a living world stays honest while it does.
    ///
    /// A change must reach the next reading through the owner that actually holds it, whether or
    /// not an event announced it. It must reach the people the record names even when those people
    /// want nothing at present. It must not reach anybody else, invent a culprit for a thing that
    /// simply went missing, mistake a sleeping shopkeeper for a failing business, or tell the same
    /// story twice because the game was reloaded.
    ///
    /// The fixture is one small town with two counters in it, because the interesting failures are
    /// the ones that cross from one family of trouble into another: a thing going missing is a
    /// property change, a shop with nothing to sell is a service change, and what the town then
    /// knows about either is a belief change.
    /// </summary>
    public class PressureFeedbackTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Far = EntityId.Parse("zone_far");
        private static readonly EntityId Shopkeeper = EntityId.Parse("npc_shopkeeper");
        private static readonly EntityId Customer = EntityId.Parse("npc_customer");
        private static readonly EntityId Rival = EntityId.Parse("npc_rival");
        private static readonly EntityId Outsider = EntityId.Parse("npc_outsider");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Shop = EntityId.Parse("business_shop");
        private static readonly EntityId Stall = EntityId.Parse("business_stall");
        private static readonly EntityId Grain = EntityId.Parse("item_grain");
        private static readonly EntityId Ring = EntityId.Parse("item_ring");

        // -- the done-when -------------------------------------------------------------------

        /// <summary>
        /// A real change in each of the three families reaches the next reading, and reaches the
        /// people the record it changed already names.
        ///
        /// The three arrive by three different routes on purpose, because that is the shape of the
        /// problem rather than an incidental variety: the theft is an observation the adapter
        /// hands in, the shop closing is a BQ-owned transition that records an event of its own,
        /// and one more person believing something changes a store and appends nothing at all. A
        /// seam that only listened to history would have two of the three.
        ///
        /// Nothing here injects a goal, a matter or a second incident. The inputs are one observed
        /// deed, one state transition and one person being told something.
        /// </summary>
        [Fact]
        public void AChangeInEachFamilyReachesTheNextReadingAndTheRecordsOwnPeople()
        {
            Town_ town = new Town_();
            Assert.Empty(Ids(DevelopmentDetector.Detect(town.World)));

            // Property/crime: the adapter saw who did it.
            town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft, Thief, Customer, Ring, "a ring", Town, "act_1"));

            // Business/service: durable continuity meaning, changed by its own owner.
            town.Businesses.TryChangeState(Shop, BusinessContinuityState.TemporarilyClosed, town.Vanilla.Now);

            // Social belief: one more person holds a claim. No event says so, so somebody has to.
            EntityId theft = TheftClaim(town.World);
            town.World.Knowledge.Teach(Rival, theft, KnowledgeSource.Hearsay, 0.7, town.Vanilla.Now, canProve: false);
            Assert.True(town.Feedback.Changed(theft));

            PressurePass pass = town.Feedback.Take();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(town.World, pass.Scope);

            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.ServiceInterruption));
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));

            // The same bounded work set is the whole-world answer for what it names: a change that
            // reached a smaller reading than a full pass would have is a change half-delivered.
            IReadOnlyList<Development> everything = DevelopmentDetector.Detect(town.World);
            Assert.Equal(Ids(everything), Ids(pressures));

            // The people the changed records name, and no one else. The shopkeeper's counter is
            // shut, the ring was the customer's, the thief is the subject of the claim about it,
            // and the rival was just told - all four are in a record that changed.
            Assert.Contains(Shopkeeper, pass.Actors);
            Assert.Contains(Customer, pass.Actors);
            Assert.Contains(Thief, pass.Actors);
            Assert.Contains(Rival, pass.Actors);
            Assert.DoesNotContain(Outsider, pass.Actors);

            // And the local readings moved with the world. The customer was in nothing a moment
            // ago and is now missing a ring; the shopkeeper's own counter is shut.
            Assert.NotEmpty(ActorPressureView.Of(town.World, Customer, pressures, town.Vanilla));
            Assert.Contains(
                ActorPressureView.Of(town.World, Shopkeeper, pressures, town.Vanilla),
                p => p.PressureTags.Contains(DevelopmentPressures.ServiceInterruption));
        }

        /// <summary>
        /// A supply loss moves the business record and the town's demand together, and what it
        /// does is felt past the counter it happened to.
        ///
        /// Both owners cite the same claim, so the shop's trouble and the town's shortage are one
        /// standing condition read twice rather than two troubles that happen to rhyme - and it is
        /// the demand at the place, not the business record, that carries it to a customer who
        /// simply lives there and to a rival who trades there. Neither of them is party to the
        /// shop's record at all, which is precisely the reach the step asks for.
        ///
        /// Nothing pretends to know what is on Elin's shelf. What BQ owns is the meaning: the cart
        /// did not come, and the counter cannot serve.
        /// </summary>
        [Fact]
        public void SupplyLossMovesBusinessAndDemandTogetherAndIsFeltBeyondThatBusiness()
        {
            Town_ town = new Town_();
            EntityId stock = town.GrainClaimId;

            // The goods are gone, read back rather than witnessed. This is the whole of the cause.
            town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.PossessionChanged,
                EntityId.None, Shopkeeper, Grain, "the winter grain", Town, "act_stock"));

            Assert.True(town.Businesses.TryRecordSupplyLoss(
                Shop, LocalDemandCategory.Food, stock, 60, town.Vanilla.Now));

            // Both existing owners, one cause.
            Assert.Equal(BusinessContinuityState.ShortOnStock, town.World.Businesses.Of(Shop).State);
            Assert.Equal(stock, town.World.Businesses.Of(Shop).CauseFactId);
            LocalDemandPressure demand = town.World.Demands.Get(Town, LocalDemandCategory.Food, stock);
            Assert.NotNull(demand);
            Assert.Equal(60, demand.Severity);

            PressurePass pass = town.Feedback.Take();
            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(town.World, pass.Scope);
            Development shortage = Assert.Single(
                pressures, d => d.HasPressure(DevelopmentPressures.Shortage));
            Assert.Equal("dev.shortage:" + stock.Value, shortage.Id);
            Assert.Contains(Town, shortage.SiteIds);
            Assert.Contains(pressures, d => d.Id == "dev.business_continuity:" + Shop.Value);

            // Beyond the business: somebody who buys there and somebody who competes there.
            Assert.Contains(
                ActorPressureView.Of(town.World, Customer, pressures, town.Vanilla),
                p => p.PressureTags.Contains(DevelopmentPressures.Shortage));
            Assert.Contains(
                ActorPressureView.Of(town.World, Rival, pressures, town.Vanilla),
                p => p.PressureTags.Contains(DevelopmentPressures.Shortage));

            // And no further. Somebody who lives elsewhere has no route to a town's empty shelves.
            Assert.Empty(ActorPressureView.Of(town.World, Outsider, pressures, town.Vanilla));
        }

        /// <summary>
        /// A thing that went missing while nobody was looking stays a thing that went missing.
        ///
        /// This is the single most tempting inference in the whole feedback loop and the one that
        /// would quietly ruin it: an empty shelf is a wonderful story hook, and a layer that
        /// answered "who took it" would be writing the incident it is supposed to be reacting to.
        /// So the record says the grain is not the shopkeeper's any more and says nothing else -
        /// no thief, no witness, no crime - and the pressure reading holds nothing about a wrong,
        /// because no wrong is known to have been done.
        /// </summary>
        [Fact]
        public void AnUndetectedPropertyChangeFabricatesNoCulpritAndNoWitness()
        {
            Town_ town = new Town_();
            EntityId stock = town.GrainClaimId;

            Events.WorldEvent observed = town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.PossessionChanged,
                EntityId.None, Shopkeeper, Grain, "the winter grain", Town, "act_stock"));

            Assert.NotNull(observed);
            Assert.Equal(Events.WorldEventType.PossessionChanged, observed.Type);
            Assert.True(observed.Actor.IsNone);
            Assert.Equal(Shopkeeper, observed.Target);
            Assert.Empty(observed.Witnesses);
            Assert.Contains(Grain, observed.Evidence);

            // The claim is superseded, not deleted and not repointed at somebody new.
            Assert.Equal(TruthState.Superseded, town.World.Knowledge.GetFact(stock).Truth);
            Assert.Equal(Shopkeeper, town.World.Knowledge.GetFact(stock).Subject);
            Assert.DoesNotContain(
                town.World.Knowledge.Facts.Values,
                f => f.Predicate == FactPredicates.Stole || f.Predicate == FactPredicates.Witnessed);
            Assert.DoesNotContain(
                town.World.Knowledge.Facts.Values,
                f => f.Predicate == FactPredicates.Possesses && f.Object == Grain && f.Truth == TruthState.True);

            // Nobody has been taught anything, and nothing derives a wrong to be answered.
            foreach (EntityId person in new[] { Customer, Rival, Outsider, Thief })
            {
                Assert.Empty(town.World.Knowledge.BeliefsOf(person));
            }

            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(town.World, town.Feedback.Take().Scope);
            Assert.DoesNotContain(pressures, d => d.HasPressure(DevelopmentPressures.UnresolvedCrime));
            Assert.DoesNotContain(pressures, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));
        }

        /// <summary>
        /// Somebody being told later is a change worth waking them for, and it wakes only them.
        ///
        /// A theft nobody but its thief knows about is a wrong the world holds and nothing can be
        /// done about. What makes it a live matter is not the passage of time and not the detector
        /// noticing harder - it is one legitimate route by which one more person comes to hold the
        /// claim. That changes no fact about what happened and changes everything about the
        /// world's position: there is now a secret somebody cannot prove, the person it is about is
        /// in a different situation than they were this morning, and neither of those existed a
        /// moment ago.
        ///
        /// So the seam wakes the two people that change is actually between - the one who was told
        /// and the one it is about - and the response is formed by the woken actor's own goal
        /// evolution from their own reading, with no goal injected here. Everybody else in the town
        /// is exactly where they were: not woken, told nothing, and in nothing.
        /// </summary>
        [Fact]
        public void ALaterNoticeWakesAResponseAndLeavesUnrelatedActorsUnaware()
        {
            Town_ town = new Town_();
            town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft, Thief, Customer, Ring, "a ring", Town, "act_1"));
            town.Feedback.Take();

            EntityId theft = TheftClaim(town.World);
            IReadOnlyList<Development> before = DevelopmentDetector.Detect(town.World);
            Assert.DoesNotContain(before, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));
            Assert.Empty(ActorPressureView.Of(town.World, Rival, before, town.Vanilla));

            // The legitimate later notice: one more person now holds the claim. The knowledge owner
            // appends no event for it, so its caller is what says the world moved.
            town.Vanilla.AdvanceDays(3);
            town.World.Knowledge.Teach(Rival, theft, KnowledgeSource.Hearsay, 0.8, town.Vanilla.Now, canProve: false);
            Assert.True(town.Feedback.Changed(theft));

            PressurePass pass = town.Feedback.Take();
            Assert.Contains(Rival, pass.Actors);
            Assert.Contains(Thief, pass.Actors);
            Assert.DoesNotContain(Outsider, pass.Actors);
            Assert.DoesNotContain(Shopkeeper, pass.Actors);

            IReadOnlyList<Development> pressures = DevelopmentDetector.Detect(town.World, pass.Scope);
            Assert.Contains(pressures, d => d.HasPressure(DevelopmentPressures.UnprovenKnowledge));
            Assert.NotEmpty(ActorPressureView.Of(town.World, Rival, pressures, town.Vanilla));

            // A response, formed by the woken actor's own evolution from their own reading. The
            // seam chose nothing: it said whose world had changed.
            NarrativeNpc thief = town.World.Registry.GetNpc(Thief);
            Assert.DoesNotContain(thief.Goals, g => g.IsActive);
            IReadOnlyList<ActorLocalPressure> theirs =
                ActorPressureView.Of(town.World, Thief, pressures, town.Vanilla);
            Assert.Contains(theirs, p => p.PressureTags.Contains(DevelopmentPressures.UnprovenKnowledge));
            ActorGoalEvolution.Advance(town.World, Thief, theirs, town.Vanilla.Now);
            Assert.Contains(thief.Goals, g => g.IsActive);

            // And the rest of the town is still outside it, in both directions.
            Assert.Empty(ActorPressureView.Of(town.World, Outsider, pressures, town.Vanilla));
            Assert.DoesNotContain(town.World.Registry.GetNpc(Outsider).Goals, g => g.IsActive);
            Assert.False(town.World.Knowledge.Knows(Outsider, theft));
        }

        /// <summary>
        /// A shopkeeper asleep behind an empty counter is a working day, and produces nothing.
        ///
        /// BQ-051 keeps the live service surface and durable continuity meaning apart, and this is
        /// the point of the loop at which the separation would be easiest to lose: the projection
        /// quite correctly says the counter is not serving, and a feedback seam that took that for
        /// a state change would have every shop in Elin failing every night. Reading the surface
        /// records nothing, collects nothing and derives nothing.
        ///
        /// The same refusal holds for the supply loss: it is a transition against a cause the world
        /// already holds, so a shortage with nothing behind it, or with a claim the world holds to
        /// be false behind it, is refused outright rather than recorded on trust.
        /// </summary>
        [Fact]
        public void QuietServiceControlsAndUnjustifiedLossesCreateNoDistress()
        {
            Town_ town = new Town_();
            town.Vanilla.AdvanceDays(1);

            BusinessProjection asleep = town.Businesses.Project(
                Shop,
                new BusinessServiceSnapshot(OperatorAvailability.Sleeping, hasUsableStock: false),
                town.Vanilla.Now);
            Assert.NotEqual(ServiceContinuitySurface.Available, asleep.Surface);

            Assert.Equal(0, town.Feedback.PendingCount);
            Assert.Equal(0, town.Feedback.WokenCount);
            Assert.Equal(BusinessContinuityState.Normal, town.World.Businesses.Of(Shop).State);

            // An unjustified loss is refused by its owner, so there is nothing to feed back.
            Assert.False(town.Businesses.TryRecordSupplyLoss(
                Shop, LocalDemandCategory.Food, EntityId.None, 60, town.Vanilla.Now));

            Fact rumour = new Fact(
                town.World.NewId("fact"), Shopkeeper, FactPredicates.Needs, EntityId.None,
                "grain", TruthState.False);
            town.World.Knowledge.AddFact(rumour);
            Assert.False(town.Businesses.TryRecordSupplyLoss(
                Shop, LocalDemandCategory.Food, rumour.Id, 60, town.Vanilla.Now));
            Assert.False(town.Businesses.TryRecordSupplyLoss(
                EntityId.Parse("business_unknown"), LocalDemandCategory.Food,
                town.GrainClaimId, 60, town.Vanilla.Now));

            Assert.Equal(BusinessContinuityState.Normal, town.World.Businesses.Of(Shop).State);
            Assert.Empty(town.World.Demands.Pressures);
            Assert.Empty(DevelopmentDetector.Detect(town.World));
        }

        /// <summary>
        /// Telling the world the same thing twice changes it once.
        ///
        /// Two ways round, because there are two ways it happens. A reconciliation that re-reads an
        /// inventory it has already reconciled must find the world already agreeing and do nothing
        /// - which is a question about the record, not about whether this session remembers having
        /// been told, because the session that reloaded the save does not. And a reload itself is
        /// not a replay: restored history is not dispatched, so a seam attached afterwards starts
        /// with an empty work set rather than with the entire save to re-read.
        /// </summary>
        [Fact]
        public void ReplayAndReconciliationDoNotDuplicateNativeEffects()
        {
            Town_ town = new Town_();
            ObservedVanillaAction gone = new ObservedVanillaAction(
                ObservedVanillaActionKind.PossessionChanged,
                EntityId.None, Shopkeeper, Grain, "the winter grain", Town, "act_stock");

            Assert.NotNull(town.Recorder.Record(gone));
            int events = town.World.Ledger.Count;
            int facts = town.World.Knowledge.Facts.Count;

            // The same readback again, and a fresh recorder doing the same reconciliation.
            Assert.Null(town.Recorder.Record(gone));
            Assert.Null(new VanillaActionRecorder(town.World, town.Vanilla).Record(gone));
            Assert.Equal(events, town.World.Ledger.Count);
            Assert.Equal(facts, town.World.Knowledge.Facts.Count);

            string saved = WorldStateSerializer.Save(town.World);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(saved);

            PressureFeedback after = new PressureFeedback(reloaded);
            after.Attach();
            after.Attach();
            Assert.Equal(0, after.PendingCount);
            Assert.Equal(0, after.WokenCount);
            Assert.True(after.Take().IsEmpty);

            // Reconciling against the reloaded world is still a no-op, and the world still reads
            // the same way it did before the save.
            Assert.Null(new VanillaActionRecorder(reloaded, town.Vanilla).Record(gone));
            Assert.Equal(events, reloaded.Ledger.Count);
            Assert.Equal(
                Ids(DevelopmentDetector.Detect(town.World)),
                Ids(DevelopmentDetector.Detect(reloaded)));

            // One live event after attaching reaches it, which is what proves the listener is on.
            new BusinessContinuity(reloaded).TryChangeState(
                Shop, BusinessContinuityState.Failed, town.Vanilla.Now);
            Assert.Single(after.Take().Scope.BusinessIds);
        }

        /// <summary>
        /// What nobody announced is still caught, a few records at a time.
        ///
        /// The two intakes above both need somebody to have noticed: an event, or a caller who
        /// remembered to say. Neither covers a condition that changes because time passed, and
        /// neither covers the invalidation somebody simply forgot. The rotation is why those are
        /// allowed to be incomplete - it comes round to every record the detector reads, a budget
        /// at a time, and it walks records rather than actors so a long history costs nothing.
        ///
        /// The shortage here is relieved directly in the ledger, which appends nothing and tells
        /// nobody: exactly the change a pure event listener would go on deriving forever.
        /// </summary>
        [Fact]
        public void TheRotationCatchesNonEventOwnedChangesWithoutScanningTheWorld()
        {
            Town_ town = new Town_();
            EntityId stock = town.GrainClaimId;
            town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.PossessionChanged,
                EntityId.None, Shopkeeper, Grain, "the winter grain", Town, "act_stock"));
            town.Businesses.TryRecordSupplyLoss(Shop, LocalDemandCategory.Food, stock, 60, town.Vanilla.Now);
            town.Feedback.Take();

            Assert.Contains(
                DevelopmentDetector.Detect(town.World),
                d => d.HasPressure(DevelopmentPressures.Shortage));

            // Somebody answered it, in the ledger that owns it. No event, no notice.
            town.Vanilla.AdvanceDays(4);
            Assert.True(town.World.Demands.Relieve(Town, LocalDemandCategory.Food, stock, 60, 5, town.Vanilla.Now));
            Assert.Equal(0, town.Feedback.PendingCount);

            // One inspection is a budget, not a scan.
            Assert.Equal(2, town.Feedback.Inspect(2));
            Assert.True(town.Feedback.PendingCount <= 2);

            // And the rotation comes round: keep taking budgets and the relieved place arrives.
            bool reached = town.Feedback.Take().Scope.SiteIds.Contains(Town);
            for (int i = 0; i < 40 && !reached; i++)
            {
                town.Feedback.Inspect(2);
                reached = town.Feedback.Take().Scope.SiteIds.Contains(Town);
            }

            Assert.True(reached, "the rotation never came round to the place whose demand changed");
            Assert.DoesNotContain(
                DevelopmentDetector.Detect(town.World),
                d => d.HasPressure(DevelopmentPressures.Shortage));
        }

        // -- what the seam is not --------------------------------------------------------------

        /// <summary>
        /// Collecting is not deciding, and it is not a second store.
        ///
        /// The detector's guarantee is that reading the world leaves it identical; this seam sits
        /// upstream of that and has to make the same promise, or "what changed" becomes one more
        /// thing that changes. So a pass over a world in which nothing happened writes nothing,
        /// wakes nobody and derives nothing - and the pass type has nowhere to put a goal, a
        /// matter or a scene even if something wanted to.
        /// </summary>
        [Fact]
        public void CollectingChangesNothingAndNamesNothingDramatic()
        {
            Town_ town = new Town_();
            town.Recorder.Record(new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft, Thief, Customer, Ring, "a ring", Town, "act_1"));
            town.Feedback.Take();

            string before = WorldStateSerializer.Save(town.World);
            town.Feedback.Inspect(100);
            PressurePass pass = town.Feedback.Take();
            DevelopmentDetector.Detect(town.World, pass.Scope);
            Assert.Equal(before, WorldStateSerializer.Save(town.World));

            // Two identical inspections of an unchanged world consider the same records.
            PressureFeedback twin = new PressureFeedback(WorldStateSerializer.Load(before));
            twin.Inspect(100);
            Assert.Equal(pass.Scope.ToString(), twin.Take().Scope.ToString());

            Assert.True(town.Feedback.Take().IsEmpty);
            foreach (string dramatic in new[] { "Goal", "Storylet", "StoryletId", "Situation", "Scene", "Quest" })
            {
                Assert.Empty(typeof(PressurePass).GetMember(dramatic, BindingFlags.Public | BindingFlags.Instance));
                Assert.Empty(typeof(PressureFeedback).GetMember(dramatic, BindingFlags.Public | BindingFlags.Instance));
            }
        }

        // -- fixture ---------------------------------------------------------------------------

        private static IEnumerable<string> Ids(IReadOnlyList<Development> pressures)
        {
            return pressures.Select(d => d.Id);
        }

        private static EntityId TheftClaim(NarrativeWorldState world)
        {
            return world.Knowledge.Facts.Values
                .Single(f => f.Predicate == FactPredicates.Stole && f.Truth == TruthState.True).Id;
        }

        /// <summary>
        /// One town, two counters, four people and a sack of grain that belongs to somebody.
        ///
        /// Deliberately quiet at rest: nothing is wrong here until a test makes something go wrong,
        /// so a pressure in any assertion below arrived through the seam rather than through the
        /// fixture.
        /// </summary>
        private sealed class Town_
        {
            internal Town_()
            {
                World = new NarrativeWorldState(12);
                Vanilla = new SandboxVanillaState(Player);
                World.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
                World.Registry.Add(new NarrativeSite(Far, "Palmia", "city"));
                World.Registry.Add(new NarrativeNpc(Shopkeeper, "Mira")
                    { Occupation = "shopkeeper", HomeSiteId = Town });
                World.Registry.Add(new NarrativeNpc(Customer, "Bran") { HomeSiteId = Town });
                World.Registry.Add(new NarrativeNpc(Rival, "Elsi")
                    { Occupation = "trader", HomeSiteId = Town });
                World.Registry.Add(new NarrativeNpc(Outsider, "Garron") { HomeSiteId = Far });
                World.Registry.Add(new NarrativeNpc(Thief, "Tovar") { HomeSiteId = Town });

                Businesses = new BusinessContinuity(World);
                Businesses.TryRegister(Shop, Town, Shopkeeper, GameTime.Zero);
                Businesses.TryRegister(Stall, Town, Rival, GameTime.Zero);

                Fact stock = new Fact(
                    World.NewId("fact"), Shopkeeper, FactPredicates.Possesses, Grain, "the winter grain");
                World.Knowledge.AddFact(stock);
                GrainClaimId = stock.Id;

                Recorder = new VanillaActionRecorder(World, Vanilla);
                Feedback = new PressureFeedback(World);
                Feedback.Attach();
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal BusinessContinuity Businesses { get; }

            internal VanillaActionRecorder Recorder { get; }

            internal PressureFeedback Feedback { get; }

            internal EntityId GrainClaimId { get; }
        }
    }
}
