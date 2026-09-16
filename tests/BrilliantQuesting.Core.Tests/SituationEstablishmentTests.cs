using System;
using System.Linq;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SituationEstablishmentTests
    {
        private static readonly EntityId Actor = EntityId.Parse("npc_actor");
        private static readonly EntityId Target = EntityId.Parse("npc_target");
        private static readonly GameTime Now = GameTime.FromDays(5);

        [Fact]
        public void SelectedOwnerFulfillsNewRecordAlongsideExistingBindingsWithoutInventingAnIncident()
        {
            var world = World();
            var selection = Select(world);
            string before = WorldStateSerializer.Save(world);
            Assert.Equal("recognition", selection.Offer.Proposal.Candidate.EstablishmentRequirement);
            Assert.True(selection.Offer.RequiresCreation);
            Assert.Contains("requires Core establishment record recognition", selection.Offer.Explain());
            Assert.Equal(before, WorldStateSerializer.Save(world));
            Fact incident = Assert.Single(world.Knowledge.Facts.Values);
            int events = world.Ledger.Count;

            SituationEstablishmentResult result = selection.Fulfill(Now);

            Assert.True(result.Established, result.Refusal);
            Assert.Empty(result.Diagnostics);
            var thread = Assert.Single(world.Threads);
            Assert.Same(thread, result.Thread);
            Assert.Equal(Now, thread.CreatedAt);
            Assert.Equal(new[] { Actor, Target }, thread.ParticipantIds);
            Assert.Same(incident, Assert.Single(world.Knowledge.Facts.Values));
            Assert.Equal(2, world.Registry.Npcs.Count);
            Assert.Empty(world.Obligations.Records);
            Assert.Equal(events + 1, world.Ledger.Count);
            Assert.Equal(new[] { Actor, Target }, thread.Establishment.Bindings.Select(b => b.ExistingActor));
            Assert.Equal(new[] { SituationRoles.Actor, SituationRoles.Target }, thread.Establishment.Bindings.Select(b => b.Role));
            WorldEvent established = world.Ledger.Find(thread.OriginEventId);
            Assert.Equal(WorldEventType.SituationEstablished, established.Type);
            Assert.Equal(Now, established.Time);
            Assert.Empty(established.Witnesses);
            Assert.True(established.Actor.IsNone);
            Assert.Contains(new CausalLink(CausalRole.About, incident.OriginEvent), established.Provenance.Links);
            Assert.Contains(new CausalLink(CausalRole.Outcome, thread.Establishment.Id), established.Provenance.Links);
            Assert.All(CausalHistory.Read(world, established).Causes, c => Assert.False(c.IsMissing));
            Assert.Empty(SituationProposalEcology.Standard().Read(world).Offers);

            string committed = WorldStateSerializer.Save(world);
            Assert.Same(thread, selection.Fulfill(Now).Thread);
            Assert.Equal(committed, WorldStateSerializer.Save(world));
        }

        [Theory]
        [InlineData((int)EstablishmentFault.BeforePrepare)]
        [InlineData((int)EstablishmentFault.AfterPrepare)]
        [InlineData((int)EstablishmentFault.BeforeCommit)]
        [InlineData((int)EstablishmentFault.AfterEventStaged)]
        [InlineData((int)EstablishmentFault.AfterThreadStaged)]
        public void FailureAtEveryUnpublishedBoundaryLeavesOnlyMonotonicIds(int injected)
        {
            var fault = (EstablishmentFault)injected;
            var world = World();
            var selected = Select(world);
            var owner = (UnresolvedCrimeProducer)selected.Offer.Producer;
            string before = WithoutCounters(world);
            var counters = world.Ids.Counters.ToDictionary(p => p.Key, p => p.Value);
            int dispatched = 0;
            world.Ledger.Subscribe(_ => dispatched++);

            var result = owner.Fulfill(world, selected, Now, fault);

            Assert.False(result.Established);
            Assert.Contains("injected failure", result.Refusal);
            Assert.Equal(before, WithoutCounters(world));
            Assert.Equal(0, dispatched);
            Assert.Empty(world.Threads);
            foreach (var counter in counters) Assert.True(world.Ids.Counters[counter.Key] >= counter.Value);
            if (fault != EstablishmentFault.BeforePrepare)
            {
                Assert.Equal(1UL, world.Ids.Counters["est"]);
                Assert.Equal(1UL, world.Ids.Counters["thread"]);
                Assert.Equal(2UL, world.Ids.Counters["evt"]);
                Assert.Null(world.Ledger.Find(EntityId.Mint("evt", 2)));
            }
            var retry = selected.Fulfill(Now);
            Assert.True(retry.Established, retry.Refusal);
            Assert.Equal(1, dispatched);
            if (fault != EstablishmentFault.BeforePrepare)
                Assert.Equal(EntityId.Mint("est", 2), retry.Thread.Establishment.Id);
        }

        [Fact]
        public void AfterCommitFailureAndReentrantThrowingListenerRemainCompleteAndRetryable()
        {
            var world = World();
            var selection = Select(world);
            int delivered = 0;
            world.Ledger.Subscribe(e =>
            {
                if (e.Type != WorldEventType.SituationEstablished) return;
                var complete = Assert.Single(world.Threads);
                Assert.NotNull(complete.Establishment);
                Assert.Same(e, world.Ledger.Find(complete.OriginEventId));
                string saved = WorldStateSerializer.Save(world);
                Assert.Same(complete, selection.Fulfill(Now).Thread);
                Assert.Equal(saved, WorldStateSerializer.Save(world));
                Assert.Empty(SituationProposalEcology.Standard().Read(world).Offers);
                throw new InvalidOperationException("listener fault");
            });
            world.Ledger.Subscribe(e => { if (e.Type == WorldEventType.SituationEstablished) delivered++; });
            var result = ((UnresolvedCrimeProducer)selection.Offer.Producer)
                .Fulfill(world, selection, Now, EstablishmentFault.AfterCommit);
            Assert.True(result.Established);
            Assert.Equal(1, delivered);
            Assert.Contains(result.Diagnostics, d => d.Contains("after commit"));
            Assert.Contains(result.Diagnostics, d => d.Contains("listener fault"));
            string committed = WorldStateSerializer.Save(world);
            selection.Fulfill(Now);
            Assert.Equal(committed, WorldStateSerializer.Save(world));
            Assert.Equal(1, delivered);
        }

        [Fact]
        public void EstablishingDuringAnotherDispatchQueuesTheCompleteOccurrenceAndIsolatesItsListenerFailure()
        {
            var world = World();
            var selected = Select(world);
            SituationEstablishmentResult result = null;
            int delivered = 0;
            world.Ledger.Subscribe(e =>
            {
                if (e.Type == WorldEventType.ThreadReactivated) result = selected.Fulfill(Now);
                if (e.Type == WorldEventType.SituationEstablished) throw new InvalidOperationException("queued failure");
            });
            world.Ledger.Subscribe(e => { if (e.Type == WorldEventType.SituationEstablished) delivered++; });
            world.Record(WorldEventType.ThreadReactivated, EntityId.None, EntityId.None, Now);
            Assert.True(result.Established);
            Assert.Equal(1, delivered);
            Assert.Contains(result.Diagnostics, d => d.Contains("queued failure"));
        }

        [Fact]
        public void RejectedRankingAndClosedBudgetAllocateNothing()
        {
            var world = World();
            world.AttentionBudget.MaximumLiveThreads = 0;
            string before = WorldStateSerializer.Save(world);
            var pass = SituationProposalEcology.Standard().Read(world);
            Assert.Null(pass.Select());
            Assert.Single(pass.Suppressed);
            Assert.Equal(before, WorldStateSerializer.Save(world));
            world.AttentionBudget.MaximumLiveThreads = 1;
            var selected = Select(world);
            world.AttentionBudget.MaximumLiveThreads = 0;
            Assert.Contains("budget", selected.Fulfill(Now).Refusal);
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        [Fact]
        public void AForeignOwnerOrWorldCannotFulfillTheSelection()
        {
            var world = World();
            var selected = Select(world);
            string before = WorldStateSerializer.Save(world);
            Assert.False(new UnresolvedCrimeProducer().Fulfill(world, selected, Now).Established);
            Assert.False(((UnresolvedCrimeProducer)selected.Offer.Producer).Fulfill(World(), selected, Now).Established);
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        [Theory]
        [InlineData("truth")]
        [InlineData("owner")]
        [InlineData("budget")]
        [InlineData("retired")]
        public void StalePreparationIsRevalidatedBeforePublication(string change)
        {
            var world = World();
            var selection = Select(world);
            string afterExternalChange = null;
            var result = ((UnresolvedCrimeProducer)selection.Offer.Producer).Fulfill(
                world, selection, Now, EstablishmentFault.None, () =>
                {
                    if (change == "truth") Assert.Single(world.Knowledge.Facts.Values).Truth = TruthState.False;
                    if (change == "owner")
                    {
                        var existing = new NarrativeThread(world.NewId("thread"), "other", Now);
                        existing.FactIds.Add(Assert.Single(world.Knowledge.Facts.Values).Id);
                        world.Threads.Add(existing);
                    }
                    if (change == "budget") world.AttentionBudget.MaximumLiveThreads = 0;
                    if (change == "retired") world.Registry.Retire(Actor, Target);
                    afterExternalChange = WorldStateSerializer.Save(world);
                });
            Assert.False(result.Established);
            Assert.Equal(afterExternalChange, WorldStateSerializer.Save(world));
            Assert.DoesNotContain(world.Threads, t => t.Establishment != null);
        }

        [Fact]
        public void UnsupportedActorRequirementStaysUnfulfilledAndDoesNotFallBackToAnotherPremise()
        {
            var world = new NarrativeWorldState(17);
            var body = new Organization(world.NewId("org"), "Watch", "watch");
            // Use the actual production goal vocabulary, as in the ecology fixture.
            world.Registry.Add(body);
            body.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, EntityId.None, 70));
            string before = WorldStateSerializer.Save(world);
            var pass = SituationProposalEcology.Standard().Read(world);
            var selection = Assert.IsType<SelectedSituationProposal>(pass.Select());
            Assert.True(selection.Offer.Proposal.Candidate.RequiresActorCreation);
            Assert.False(selection.Fulfill(Now).Established);
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        [Fact]
        public void SaveReloadPreservesRecognitionAndRepeatedPassCannotReestablishIt()
        {
            var world = World();
            var result = Select(world).Fulfill(Now);
            Assert.True(result.Established, result.Refusal);
            string saved = WorldStateSerializer.Save(world);
            var loaded = WorldStateSerializer.LoadWithDiagnostics(saved);
            Assert.Empty(loaded.Diagnostics);
            Assert.Equal(saved, WorldStateSerializer.Save(loaded.World));
            var thread = Assert.Single(loaded.World.Threads);
            Assert.Equal(result.Thread.Establishment.Id, thread.Establishment.Id);
            Assert.Equal(result.Thread.OriginEventId, thread.OriginEventId);
            Assert.Null(SituationProposalEcology.Standard().Read(loaded.World).Select());
            Assert.Equal(saved, WorldStateSerializer.Save(loaded.World));
            Assert.Equal(world.NewId("est"), loaded.World.NewId("est"));
            Assert.Equal(world.Rng.NextUInt64(), loaded.World.Rng.NextUInt64());
        }

        [Fact]
        public void LegacyThreadDefaultsToNoEstablishmentAndMalformedRecordIsQuarantined()
        {
            var world = World();
            var thread = Select(world).Fulfill(Now).Thread;
            JsonValue document = WorldStateSerializer.ToJson(world);
            var savedThread = document.GetArray("threads").First();
            savedThread.Set("establishment", JsonValue.Null());
            var legacy = WorldStateSerializer.FromJsonWithDiagnostics(document);
            Assert.Empty(legacy.Diagnostics);
            Assert.Null(Assert.Single(legacy.World.Threads).Establishment);
            savedThread.Set("establishment", JsonValue.Object());
            var broken = WorldStateSerializer.FromJsonWithDiagnostics(document);
            Assert.NotEmpty(broken.Diagnostics);
            Assert.Equal(ThreadState.Quarantined, Assert.Single(broken.World.Threads).State);
        }

        [Fact]
        public void ProductionConsequencesDoNotMakeRecognitionIntoKnowledgeOrANativeDeed()
        {
            var world = World();
            var native = new SandboxVanillaState(EntityId.Parse("player"));
            native.Define(Actor);
            native.Define(Target);
            var consequences = new ConsequenceEngine(world, native);
            consequences.Attach();
            JsonValue before = WorldStateSerializer.ToJson(world);
            var result = Select(world).Fulfill(Now);
            Assert.True(result.Established, result.Refusal);
            Assert.Empty(result.Diagnostics);
            Assert.Equal(ThreadState.Latent, result.Thread.State);
            foreach (string key in new[] { "facts", "beliefs", "memories", "relationships", "obligations" })
                Assert.Equal(before[key]?.ToJson(), WorldStateSerializer.ToJson(world)[key]?.ToJson());
            Assert.Equal(0, native.Karma);
            Assert.Equal(0, native.GetAffinity(Actor));
            Assert.Equal(0, native.GetAffinity(Target));
            string saved = WorldStateSerializer.Save(world);
            var loaded = WorldStateSerializer.Load(saved);
            var restoredConsequences = new ConsequenceEngine(loaded, native);
            restoredConsequences.Attach();
            Assert.Empty(restoredConsequences.Trace);
            Assert.Equal(saved, WorldStateSerializer.Save(loaded));
        }

        [Fact]
        public void ReplayProducesIdenticalCommittedIdsBindingsAndHistory()
        {
            var first = World();
            var second = World();
            Select(first).Fulfill(Now);
            Select(second).Fulfill(Now);
            Assert.Equal(WorldStateSerializer.Save(first), WorldStateSerializer.Save(second));
        }

        [Fact]
        public void RecognitionCannotBeBackdatedBeforeItsKnownCause()
        {
            var world = World();
            string before = WorldStateSerializer.Save(world);
            Assert.False(Select(world).Fulfill(GameTime.Zero).Established);
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        [Theory]
        [InlineData("actor")]
        [InlineData("premise")]
        [InlineData("record")]
        public void OwnerRefusesAnyUnsupportedRequirementEvenOnAnInternallyMalformedSelection(string kind)
        {
            var world = World();
            var original = Select(world);
            var builder = new SituationCandidateBuilder(UnresolvedCrimeProducer.Archetype)
                .Bind(SituationRoles.Actor, Actor).Bind(SituationRoles.Target, Target);
            builder.RequireEstablishmentRecord(kind == "record" ? "unsupported" : "recognition");
            if (kind == "actor") builder.RequireNewActor(SituationRoles.Witness, "unproved_native_witness");
            if (kind == "premise") builder.RequireNewWeirdPremise("unproved_premise");
            var candidate = builder.Build();
            var offer = new SituationProposalOffer(original.Offer.Producer, original.Offer.Cause,
                new SituationProposal(original.Offer.Key, candidate), SituationProposalEcology.BindingKey(candidate));
            var malformed = new SelectedSituationProposal(world, offer);
            string before = WorldStateSerializer.Save(world);
            Assert.Contains("every declared requirement", malformed.Fulfill(Now).Refusal);
            Assert.Equal(before, WorldStateSerializer.Save(world));
        }

        [Fact]
        public void OriginalFrozenSaveHasNoInventedEstablishments()
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "Saves", "schema-13.json");
            var loaded = WorldStateSerializer.LoadWithDiagnostics(System.IO.File.ReadAllText(path));
            Assert.Empty(loaded.Diagnostics);
            Assert.NotEmpty(loaded.World.Threads);
            Assert.All(loaded.World.Threads, thread => Assert.Null(thread.Establishment));
        }

        [Fact]
        public void RequirementIsDetachedFromBuilderAndPreservedByRescoring()
        {
            var builder = new SituationCandidateBuilder("property_recovery").RequireEstablishmentRecord("recognition");
            var candidate = builder.Build();
            Assert.Throws<InvalidOperationException>(() => builder.RequireEstablishmentRecord("different"));
            Assert.Equal("recognition", candidate.WithPressure("test", 1, "test").EstablishmentRequirement);
        }

        private static SelectedSituationProposal Select(NarrativeWorldState world)
            => SituationProposalEcology.Standard().Read(world).Select();

        private static NarrativeWorldState World()
        {
            var world = new NarrativeWorldState(17);
            world.Registry.Add(new NarrativeNpc(Actor, "Actor"));
            world.Registry.Add(new NarrativeNpc(Target, "Target"));
            var incident = world.Record(WorldEventType.Theft, Actor, Target, GameTime.FromDays(1));
            world.Knowledge.AddFact(new Fact(world.NewId("fact"), Actor, FactPredicates.Stole,
                Target, originEvent: incident.Id));
            return WorldStateSerializer.Load(WorldStateSerializer.Save(world));
        }

        private static string WithoutCounters(NarrativeWorldState world)
            => WorldStateSerializer.ToJson(world).Set("idCounters", JsonValue.Object()).ToJson();
    }
}
