using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Diagnostics;
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
    /// BQa-001. An event used to be able to say what it touched and never why it happened, so the
    /// only route from a consequence back to its cause was to guess: the event before it in the
    /// list, the nearest matching timestamp, the one id that happened to be in `Related`. These
    /// prove the guess is gone, that motive is never mistaken for cause, and that the whole thing
    /// survives a reload without any of it being replayed.
    /// </summary>
    public class CausalProvenanceTests
    {
        // -- the shape itself --------------------------------------------------------------

        [Fact]
        public void AnEventWithNothingRecordedReadsAsUnknownRatherThanAsUncaused()
        {
            NarrativeWorldState world = new NarrativeWorldState(1);
            WorldEvent plain = world.Record(WorldEventType.Theft, Npc(world), Npc(world), new GameTime(0));

            Assert.True(plain.Provenance.IsUnknown);
            Assert.Empty(plain.Provenance.Links);
            Assert.True(CausalHistory.Read(world, plain).IsUnknown);
            Assert.Contains("unknown", NarrativeInspector.DescribeCausality(world, plain.Id));
        }

        [Fact]
        public void AnUnknownReferenceIsNotRecordedAsALink()
        {
            EventProvenance provenance = EventProvenance.Draft()
                .Trigger(EntityId.None)
                .About(EntityId.Parse("evt_1"))
                .Build();

            Assert.Single(provenance.Links);
            Assert.Equal(CausalRole.About, provenance.Links[0].Role);
            Assert.Throws<ArgumentException>(() => new CausalLink(CausalRole.Trigger, EntityId.None));
        }

        [Fact]
        public void ProvenanceCarriesSeveralCausesAndKeepsTheirRolesApart()
        {
            EventProvenance provenance = EventProvenance.Draft()
                .Trigger(EntityId.Parse("evt_1"))
                .Trigger(EntityId.Parse("evt_2"))
                .Motive(EntityId.Parse("fact_1"))
                .Build();

            Assert.Equal(2, provenance.References(CausalRole.Trigger).Count);
            Assert.Equal(EntityId.Parse("evt_1"), provenance.FirstReference(CausalRole.Trigger));
            Assert.Equal(EntityId.Parse("fact_1"), provenance.FirstReference(CausalRole.Motive));
            Assert.False(provenance.Has(CausalRole.Outcome));
            Assert.True(provenance.FirstReference(CausalRole.Outcome).IsNone);
        }

        [Fact]
        public void ADecisionRecordRefusesToBecomeASnapshot()
        {
            // Bounded structurally: a decision that wants to explain itself by copying the
            // candidate list is refused, not quietly trimmed to fit.
            string[] tooMany = new string[DecisionEvidence.MaxReasons + 1];
            for (int i = 0; i < tooMany.Length; i++) tooMany[i] = "reason" + i;

            Assert.Throws<ArgumentException>(() => new DecisionEvidence("authority.acts", tooMany));
            Assert.Throws<ArgumentException>(() => new DecisionEvidence("authority.acts", new[] { new string('x', 65) }));
            Assert.Throws<ArgumentException>(() => new DecisionEvidence("the guard believed the witness"));
            Assert.Throws<ArgumentException>(() => new DecisionEvidence(" "));

            List<CausalLink> tooManyLinks = new List<CausalLink>();
            for (int i = 0; i <= EventProvenance.MaxLinks; i++)
                tooManyLinks.Add(new CausalLink(CausalRole.Motive, EntityId.Mint("fact", (ulong)i + 1)));
            Assert.Throws<ArgumentException>(() => new EventProvenance(tooManyLinks));
        }

        // -- identity: reserved, never predicted ---------------------------------------------

        [Fact]
        public void AFactCanNameTheEventItComesFromBeforeThatEventIsRecorded()
        {
            NarrativeWorldState world = new NarrativeWorldState(2);
            EntityId thief = Npc(world);
            EntityId victim = Npc(world);

            EventReservation reserved = world.ReserveEvent();
            Fact stolen = new Fact(
                world.NewId("fact"), thief, FactPredicates.Stole, world.NewId("item"),
                originEvent: reserved.Id);
            world.Knowledge.AddFact(stolen);

            WorldEvent theft = world.Record(
                reserved, WorldEventType.Theft, thief, victim, new GameTime(10),
                provenance: EventProvenance.Draft().Outcome(stolen.Id).Build());

            Assert.Equal(theft.Id, stolen.OriginEvent);
            Assert.Same(theft, CausalHistory.FindEvent(world, stolen.OriginEvent));
        }

        [Fact]
        public void OneReservationCannotBecomeTwoEvents()
        {
            NarrativeWorldState world = new NarrativeWorldState(3);
            EventReservation reserved = world.ReserveEvent();
            world.Record(reserved, WorldEventType.Theft, Npc(world), Npc(world), new GameTime(0));

            Assert.Throws<InvalidOperationException>(() =>
                world.Record(reserved, WorldEventType.Theft, Npc(world), Npc(world), new GameTime(1)));
            Assert.Single(world.Ledger.Events);
        }

        [Fact]
        public void ReadOnlyProvenanceInspectionThatIsRejectedConsumesNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create(31337);
            lab.Checks = new AlwaysFails();
            IReadOnlyDictionary<string, ulong> before = Counters(lab.World);
            int events = lab.World.Ledger.Events.Count;

            // Look at everything, then turn the action down. None of it may cost an identity.
            foreach (WorldEvent worldEvent in lab.World.Ledger.Events)
            {
                CausalHistory.Read(lab.World, worldEvent);
                CausalHistory.Naming(lab.World, worldEvent.Id, CausalRole.About);
                NarrativeInspector.DescribeCausality(lab.World, worldEvent.Id);
            }

            ActionContext context = lab.Context(lab.Situation.ThiefId);
            Assert.False(lab.Actions.Get("return_item").GetAvailability(context).IsAvailable);

            Assert.Equal(before, Counters(lab.World));
            Assert.Equal(events, lab.World.Ledger.Events.Count);
        }

        // -- nested reactions ----------------------------------------------------------------

        [Fact]
        public void ANestedReactionNamesItsCauseRatherThanRelyingOnBeingRecordedNextToIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create(31337);
            lab.Checks = new AlwaysCriticallyFails();

            // A caught pickpocket records the theft and, as a reaction to it, the witnessing.
            lab.Perform("pickpocket", lab.Situation.ThiefId);

            WorldEvent theft = Last(lab.World, WorldEventType.Theft);
            WorldEvent witnessed = Last(lab.World, WorldEventType.CrimeWitnessed);
            Assert.Equal(theft.Id, witnessed.Provenance.FirstReference(CausalRole.Trigger));

            // Push unrelated history in between and the link still resolves, which adjacency
            // would not: the two events are no longer neighbours.
            lab.World.Record(WorldEventType.RumorSpread, lab.Player, EntityId.None, lab.Vanilla.Now);
            lab.World.Record(WorldEventType.RumorSpread, lab.Player, EntityId.None, lab.Vanilla.Now);

            CausalReading reading = CausalHistory.Read(lab.World, witnessed);
            Assert.Same(theft, Assert.Single(reading.Triggers).Occurrence);
            Assert.Equal(new[] { witnessed.Id },
                Ids(CausalHistory.Naming(lab.World, theft.Id, CausalRole.Trigger)));
        }

        [Fact]
        public void AFactMintedInsideANestedRecordingStillResolvesToItsOwnEventAfterReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create(31337);
            lab.Checks = new AlwaysPasses();
            lab.Perform("pickpocket", lab.Situation.ThiefId);

            WorldEvent theft = Last(lab.World, WorldEventType.Theft);
            EntityId producedFact = theft.Provenance.FirstReference(CausalRole.Outcome);
            Assert.False(producedFact.IsNone);
            Assert.Equal(theft.Id, lab.World.Knowledge.GetFact(producedFact).OriginEvent);

            NarrativeWorldState reloaded = Reload(lab.World);
            Assert.Equal(theft.Id, reloaded.Knowledge.GetFact(producedFact).OriginEvent);
            Assert.Equal(
                producedFact,
                CausalHistory.FindEvent(reloaded, theft.Id).Provenance.FirstReference(CausalRole.Outcome));
        }

        // -- motive is not cause --------------------------------------------------------------

        [Fact]
        public void AnAccusationSeparatesWhatTheAccuserBelievedFromWhatTheWorldHolds()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            FalseAccusationSituation situation = Frame(vanilla, out NarrativeWorldState world);

            // The player reports the true claim. The claim is motive evidence; what it is about
            // is the theft that claim came from, and the two are different kinds of thing.
            Report(world, vanilla, situation, situation.TrueTheftFactId);

            WorldEvent accusation = LastAccusation(world);
            CausalReading reading = CausalHistory.Read(world, accusation);

            Assert.Equal(situation.TrueTheftFactId, Assert.Single(reading.Motives).Reference);
            Assert.Equal(WorldEventType.Theft, Assert.Single(reading.About).Occurrence.Type);
            Assert.Empty(reading.Triggers);
            Assert.NotNull(reading.Decision);
            Assert.StartsWith("authority.", reading.Decision.Decision);

            string report = NarrativeInspector.DescribeCausality(world, accusation.Id);
            Assert.Contains("motive evidence, not established fact", report);
            Assert.Contains("about:", report);
        }

        [Fact]
        public void AnAccusationBuiltOnAFalseClaimIsRecordedAsSincereMotiveAndNotAsAnObjectiveCause()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            FalseAccusationSituation situation = Frame(vanilla, out NarrativeWorldState world);

            // A planted claim: false, with its own origin event, naming the innocent.
            WorldEvent planting = world.Record(
                WorldEventType.EvidenceCreated, situation.ThiefId, situation.InnocentId, vanilla.Now,
                0.7, situation.MarketZoneId);
            Fact lie = new Fact(
                world.NewId("fact"), situation.InnocentId, FactPredicates.Stole, situation.ItemId,
                truth: TruthState.False, originEvent: planting.Id);
            world.Knowledge.AddFact(lie);
            world.Knowledge.Teach(vanilla.PlayerId, lie.Id, KnowledgeSource.Hearsay, 0.9, vanilla.Now, true);

            Report(world, vanilla, situation, lie.Id);

            CausalReading reading = CausalHistory.Read(world, LastAccusation(world));
            ResolvedCause motive = Assert.Single(reading.Motives);

            // The accusation happened, and it is about the planting - but nothing in provenance
            // asserts the claim is true. Truth stays with the fact, where it can be corrected.
            Assert.Equal(lie.Id, motive.Reference);
            Assert.Equal(TruthState.False, motive.Claim.Truth);
            Assert.Equal(planting.Id, Assert.Single(reading.About).Reference);
            Assert.Empty(reading.Triggers);
        }

        // -- the repeated-transfer/repeated-accusation contract --------------------------------

        [Fact]
        public void OnePhysicalItemStolenTwiceKeepsEveryOccurrenceDistinctAcrossReload()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            FalseAccusationSituation situation = Frame(vanilla, out NarrativeWorldState world);
            EntityId item = situation.ItemId;

            // 1. The original theft is already in history, and the true claim came out of it.
            WorldEvent originalTheft = Single(world, WorldEventType.Theft);
            Assert.Equal(originalTheft.Id, world.Knowledge.GetFact(situation.TrueTheftFactId).OriginEvent);

            // 2. Recovery: the victim gets it back, answering the matter's own origin.
            WorldEvent recovery = world.Record(
                WorldEventType.ItemReturned, situation.VictimId, situation.VictimId, vanilla.Now,
                0.8, situation.MarketZoneId, evidence: new[] { item },
                threadId: situation.Thread.Id,
                provenance: EventProvenance.Draft().About(situation.Thread.OriginEventId).Build());

            // 3. A second, later theft of the very same object by somebody else.
            EventReservation second = world.ReserveEvent();
            Fact secondClaim = new Fact(
                world.NewId("fact"), situation.InnocentId, FactPredicates.Stole, item,
                originEvent: second.Id);
            world.Knowledge.AddFact(secondClaim);
            WorldEvent laterTheft = world.Record(
                second, WorldEventType.Theft, situation.InnocentId, situation.VictimId,
                vanilla.Now.PlusDays(1), 0.6, situation.MarketZoneId, evidence: new[] { item },
                provenance: EventProvenance.Draft().Outcome(secondClaim.Id).Build());

            // 4. Transferred back again, this time answering the later theft rather than the first.
            WorldEvent transferBack = world.Record(
                WorldEventType.ItemReturned, situation.InnocentId, situation.VictimId,
                vanilla.Now.PlusDays(2), 0.8, situation.MarketZoneId, evidence: new[] { item },
                provenance: EventProvenance.Draft().About(laterTheft.Id).Build());

            // 5. An accusation about the *original* theft, made after all of that.
            Report(world, vanilla, situation, situation.TrueTheftFactId);
            WorldEvent accusation = LastAccusation(world);

            NarrativeWorldState reloaded = Reload(world);

            // Every one of the five is a separate occurrence, and each says which of the others
            // it answers. Item identity cannot do this: all five name the same object.
            foreach (WorldEvent occurrence in new[] { originalTheft, recovery, laterTheft, transferBack, accusation })
                Assert.NotNull(CausalHistory.FindEvent(reloaded, occurrence.Id));

            Assert.NotEqual(originalTheft.Id, laterTheft.Id);
            Assert.Equal(
                originalTheft.Id,
                CausalHistory.FindEvent(reloaded, recovery.Id).Provenance.FirstReference(CausalRole.About));
            Assert.Equal(
                laterTheft.Id,
                CausalHistory.FindEvent(reloaded, transferBack.Id).Provenance.FirstReference(CausalRole.About));

            // The accusation is about the first theft and not the most recent one, which is the
            // answer every reading off the ledger by item or by recency gets wrong.
            CausalReading accused = CausalHistory.Read(reloaded, accusation.Id);
            Assert.Equal(originalTheft.Id, Assert.Single(accused.About).Reference);
            Assert.Equal(situation.TrueTheftFactId, Assert.Single(accused.Motives).Reference);
            Assert.Equal(
                new[] { recovery.Id, accusation.Id },
                Ids(CausalHistory.Naming(reloaded, originalTheft.Id, CausalRole.About)));
            Assert.Equal(
                new[] { transferBack.Id },
                Ids(CausalHistory.Naming(reloaded, laterTheft.Id, CausalRole.About)));
        }

        // -- persistence ----------------------------------------------------------------------

        [Fact]
        public void ProvenanceSurvivesASaveWithoutAnythingBeingRedispatched()
        {
            TheftLaboratory lab = TheftLaboratory.Create(31337);
            lab.Checks = new AlwaysPasses();
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            WorldStateLoadResult first = WorldStateSerializer.LoadWithDiagnostics(WorldStateSerializer.Save(lab.World));
            Assert.Empty(first.Diagnostics);

            // Byte-identical from the canonical form on, so nothing is dropped or reordered.
            // (The first save is not its own fixed point: the reader has always normalized an
            // absent optional string to empty, which is older than this seam.)
            string canonical = WorldStateSerializer.Save(first.World);
            WorldStateLoadResult result = WorldStateSerializer.LoadWithDiagnostics(canonical);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(canonical, WorldStateSerializer.Save(result.World));

            // Attaching consequences must not replay a single restored event.
            ConsequenceEngine consequences = new ConsequenceEngine(result.World, lab.Vanilla);
            consequences.Attach();
            Assert.Empty(consequences.Trace);
            Assert.Equal(canonical, WorldStateSerializer.Save(result.World));

            WorldEvent returned = Last(result.World, WorldEventType.ItemReturned);
            Assert.Equal(
                lab.Situation.Thread.OriginEventId,
                returned.Provenance.FirstReference(CausalRole.About));
        }

        [Fact]
        public void ADecisionRecordSurvivesTheWorldChangingUnderIt()
        {
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            FalseAccusationSituation situation = Frame(vanilla, out NarrativeWorldState world);
            Report(world, vanilla, situation, situation.TrueTheftFactId);

            DecisionEvidence before = LastAccusation(world).Provenance.Decision;
            Assert.NotNull(before);

            NarrativeWorldState reloaded = Reload(world);
            DecisionEvidence after = LastAccusation(reloaded).Provenance.Decision;

            Assert.Equal(before.Decision, after.Decision);
            Assert.Equal(before.Reasons, after.Reasons);
        }

        [Fact]
        public void AReferenceWhoseRecordIsGoneIsReportedAsMissingRatherThanAsNoCause()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            EntityId vanished = world.NewId("fact");
            WorldEvent accusation = world.Record(
                WorldEventType.AccusationMade, Npc(world), Npc(world), new GameTime(5),
                provenance: EventProvenance.Draft().Motive(vanished).Build());

            NarrativeWorldState reloaded = Reload(world);
            CausalReading reading = CausalHistory.Read(reloaded, accusation.Id);
            ResolvedCause motive = Assert.Single(reading.Motives);

            Assert.True(motive.IsMissing);
            Assert.Equal(vanished, motive.Reference);
            Assert.False(reading.IsUnknown);
            Assert.Contains("no longer in the world", NarrativeInspector.DescribeCausality(reloaded, accusation.Id));
        }

        [Fact]
        public void AnOldSaveKeepsItsEventsAndGainsNoInventedCauses()
        {
            // Schema 11 had nowhere to record why anything happened. Migrating must not fill that
            // in from adjacency, and must not replay a single restored event.
            NarrativeWorldState world = new NarrativeWorldState(11);
            EntityId thief = Npc(world);
            EntityId victim = Npc(world);
            world.Record(WorldEventType.Theft, thief, victim, new GameTime(0));
            world.Record(WorldEventType.CrimeWitnessed, thief, victim, new GameTime(1));

            JsonValue document = JsonValue.Parse(WorldStateSerializer.Save(world));
            document.Set("schemaVersion", 11);
            foreach (JsonValue worldEvent in document.GetArray("events"))
                worldEvent.Set("provenance", JsonValue.Null());

            WorldStateLoadResult result = WorldStateSerializer.LoadWithDiagnostics(document.ToJson());
            Assert.Empty(result.Diagnostics);
            Assert.Equal(NarrativeWorldState.CurrentSchemaVersion, result.World.SchemaVersion);
            Assert.Equal(2, result.World.Ledger.Events.Count);
            Assert.All(result.World.Ledger.Events, e => Assert.True(e.Provenance.IsUnknown));
        }

        // -- helpers --------------------------------------------------------------------------

        private static EntityId Npc(NarrativeWorldState world)
        {
            NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), "Somebody");
            world.Registry.Add(npc);
            return npc.Id;
        }

        private static FalseAccusationSituation Frame(SandboxVanillaState vanilla, out NarrativeWorldState world)
        {
            world = new NarrativeWorldState(4242);
            EntityId market = world.NewId("zone");
            vanilla.Define(vanilla.PlayerId, level: 5, money: 2000, zone: market);
            world.Registry.Add(new NarrativeNpc(vanilla.PlayerId, "You") { Importance = NarrativeImportance.Major });
            return FalseAccusationSituation.Create(world, new SandboxStager(vanilla), vanilla.PlayerId, market, vanilla.Now);
        }

        /// <summary>Runs the production report verb, which is what actually records an accusation.</summary>
        private static void Report(
            NarrativeWorldState world, SandboxVanillaState vanilla, FalseAccusationSituation situation, EntityId claim)
        {
            ActionContext context = new ActionContext(
                world, vanilla, new AlwaysPasses(), new DeterministicRng(1).Fork("actions"),
                vanilla.PlayerId, situation.GuardId)
            {
                Thread = situation.Thread,
                SubjectFact = claim
            };

            ReportToAuthorityAction report = new ReportToAuthorityAction();
            Assert.True(report.GetAvailability(context).IsAvailable);
            report.Perform(context);
        }

        private static WorldEvent LastAccusation(NarrativeWorldState world)
        {
            WorldEvent found = null;
            foreach (WorldEvent worldEvent in world.Ledger.Events)
            {
                if (worldEvent.Type == WorldEventType.CrimeReported
                    || worldEvent.Type == WorldEventType.InquiryOpened
                    || worldEvent.Type == WorldEventType.AccusationRejected
                    || worldEvent.Type == WorldEventType.AccusationMade
                    || worldEvent.Type == WorldEventType.FalseAccusation)
                {
                    found = worldEvent;
                }
            }

            Assert.NotNull(found);
            return found;
        }

        private static WorldEvent Last(NarrativeWorldState world, WorldEventType type)
        {
            WorldEvent found = null;
            foreach (WorldEvent worldEvent in world.Ledger.OfType(type)) found = worldEvent;
            Assert.NotNull(found);
            return found;
        }

        private static WorldEvent Single(NarrativeWorldState world, WorldEventType type)
        {
            List<WorldEvent> found = new List<WorldEvent>();
            foreach (WorldEvent worldEvent in world.Ledger.OfType(type)) found.Add(worldEvent);
            return Assert.Single(found);
        }

        private static NarrativeWorldState Reload(NarrativeWorldState world)
        {
            WorldStateLoadResult result = WorldStateSerializer.LoadWithDiagnostics(WorldStateSerializer.Save(world));
            Assert.Empty(result.Diagnostics);
            return result.World;
        }

        private static EntityId[] Ids(IReadOnlyList<WorldEvent> events)
        {
            EntityId[] ids = new EntityId[events.Count];
            for (int i = 0; i < events.Count; i++) ids[i] = events[i].Id;
            return ids;
        }

        private static Dictionary<string, ulong> Counters(NarrativeWorldState world)
        {
            Dictionary<string, ulong> copy = new Dictionary<string, ulong>();
            foreach (KeyValuePair<string, ulong> counter in world.Ids.Counters) copy[counter.Key] = counter.Value;
            return copy;
        }

        private sealed class AlwaysPasses : ICheckResolver
        {
            public CheckResult Resolve(CheckRequest request, DeterministicRng rng) =>
                new CheckResult(request.Profile.Id, request.Profile.BaseDifficulty, new List<CheckTerm>(), 10, 15, CheckOutcome.Pass);
        }

        private sealed class AlwaysFails : ICheckResolver
        {
            public CheckResult Resolve(CheckRequest request, DeterministicRng rng) =>
                new CheckResult(request.Profile.Id, request.Profile.BaseDifficulty, new List<CheckTerm>(), 15, 10, CheckOutcome.Fail);
        }

        private sealed class AlwaysCriticallyFails : ICheckResolver
        {
            public CheckResult Resolve(CheckRequest request, DeterministicRng rng) =>
                new CheckResult(request.Profile.Id, request.Profile.BaseDifficulty, new List<CheckTerm>(), 20, 1, CheckOutcome.CriticalFail);
        }
    }
}
