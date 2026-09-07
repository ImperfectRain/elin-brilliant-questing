using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-096. Adventuring parties are persistent organizations that can take up a rescue the
    /// player left alone. The attempt still resolves through the ordinary action library, and
    /// what reaches the player is a claim that can travel rather than free omniscience.
    /// </summary>
    public class AdventurerEcologyTests
    {
        [Fact]
        public void APartyAttemptsARescueThePlayerLeftAlone()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Pass));

            int acted = bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);

            Assert.Equal(1, acted);
            Assert.Equal(ThreadState.Resolved, bench.Thread.State);
            Assert.Equal("rescued", bench.Thread.Resolution);
            Assert.Equal(TruthState.Superseded, bench.Risk.Truth);

            WorldEvent rescue = Assert.Single(bench.World.Ledger.Events, e => e.Type == WorldEventType.Rescued);
            Assert.Equal(bench.Leader, rescue.Actor);
            Assert.Equal(bench.Brewer, rescue.Target);
            Assert.Equal(bench.Thread.Id, rescue.ThreadId);
            Assert.Empty(rescue.Witnesses);

            WorldEvent party = Assert.Single(bench.World.Ledger.Events, e => e.Type == WorldEventType.OrganizationActed);
            Assert.Equal(bench.Party, party.Target);
            Assert.Contains(AdventurerEcology.RescueAttemptTag, party.Tags);
            Assert.Contains("rescued", party.Tags);
        }

        [Fact]
        public void TheOutcomeIsDiscoverableButNotGivenToThePlayerForFree()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Pass));

            bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);

            AdventurerEcologyTrace trace = Assert.Single(bench.Ecology.LastPass);
            Assert.True(bench.World.Knowledge.Knows(bench.Leader, trace.AttemptFactId));
            Assert.True(bench.World.Knowledge.Knows(bench.Scout, trace.AttemptFactId));
            Assert.True(bench.World.Knowledge.Knows(bench.Leader, trace.SettledFactId));
            Assert.False(bench.World.Knowledge.Knows(bench.Player, trace.AttemptFactId));
            Assert.False(bench.World.Knowledge.Knows(bench.Player, trace.SettledFactId));
            Assert.Empty(Chronicle.Entries(bench.World, bench.Player));

            RumorSystem rumors = new RumorSystem(bench.World.Knowledge, bench.World.Ledger, bench.World.Ids);
            Assert.True(rumors.Tell(bench.Leader, bench.Player, trace.AttemptFactId, bench.Now));
            Assert.True(rumors.Tell(bench.Leader, bench.Player, trace.SettledFactId, bench.Now));

            Assert.True(bench.World.Knowledge.Knows(bench.Player, trace.AttemptFactId));
            ChronicleEntry entry = Assert.Single(Chronicle.Entries(bench.World, bench.Player));
            Assert.Equal(bench.Party, entry.ResolvedBy);
            Assert.Equal("rescued", entry.Outcome);
            Assert.Empty(entry.WhatThePlayerDid);
        }

        [Fact]
        public void AFailedPartyRescueLeavesTheMatterOpenButTheAttemptCanStillBeHeard()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Fail));

            int acted = bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);

            AdventurerEcologyTrace trace = Assert.Single(bench.Ecology.LastPass);
            Assert.Equal(1, acted);
            Assert.False(trace.Attempt.Outcome.Succeeded);
            Assert.Equal("rescue_failed", trace.OutcomeName);
            Assert.Equal(ThreadState.Active, bench.Thread.State);
            Assert.Equal(TruthState.True, bench.Risk.Truth);
            Assert.NotEqual(EntityId.None, trace.AttemptFactId);
            Assert.Equal(EntityId.None, trace.SettledFactId);
            Assert.DoesNotContain(bench.World.Knowledge.Facts.Values, f => f.Predicate == FactPredicates.Settled);

            RumorSystem rumors = new RumorSystem(bench.World.Knowledge, bench.World.Ledger, bench.World.Ids);
            Assert.True(rumors.Tell(bench.Leader, bench.Player, trace.AttemptFactId, bench.Now));
            Fact attempt = bench.World.Knowledge.GetFact(trace.AttemptFactId);
            Assert.Equal(FactPredicates.AttemptedRescue, attempt.Predicate);
            Assert.Equal(bench.Party, attempt.Subject);
            Assert.Equal("rescue_failed", attempt.Value);
        }

        [Fact]
        public void APartyDoesNotTakeAMatterThePlayerHasAlreadyActedIn()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Pass));
            bench.World.Record(
                WorldEventType.Conversed,
                bench.Player,
                bench.Brewer,
                bench.Now,
                0.2,
                bench.Town,
                threadId: bench.Thread.Id);

            int acted = bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);

            Assert.Equal(0, acted);
            Assert.Empty(bench.Ecology.LastPass);
            Assert.Equal(ThreadState.Active, bench.Thread.State);
        }

        [Fact]
        public void SameDaySaveLoadDoesNotRepeatAFailedRescue()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Fail));
            bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);
            string saved = WorldStateSerializer.Save(bench.World);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(saved);
            RescueBench reopened = bench.Reopen(reloaded, new FixedCheckResolver(CheckOutcome.Fail));

            int sameDay = reopened.Ecology.Advance(reopened.World, reopened.Vanilla, reopened.Checks, reopened.Actions, reopened.Now);
            reopened.Vanilla.AdvanceDays(1);
            int nextDay = reopened.Ecology.Advance(reopened.World, reopened.Vanilla, reopened.Checks, reopened.Actions, reopened.Now);

            Assert.Equal(0, sameDay);
            Assert.Equal(1, nextDay);
            Assert.Equal(2, reopened.World.Ledger.Events.Count(e => e.Type == WorldEventType.OrganizationActed));
        }

        [Fact]
        public void TheInspectorNamesThePartyAttemptAndTheClaim()
        {
            RescueBench bench = RescueBench.Create(new FixedCheckResolver(CheckOutcome.Pass));
            bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Now);

            string report = NarrativeInspector.DescribeAdventurerEcology(bench.World, bench.Ecology.LastPass.Single());

            Assert.Contains("adventurer ecology", report);
            Assert.Contains("Bracken Knives", report);
            Assert.Contains("rescue", report);
            Assert.Contains("party claim", report);
            Assert.Contains("settled claim", report);
        }

        private sealed class RescueBench
        {
            private static readonly EntityId PlayerId = EntityId.Parse("npc_player");
            private static readonly EntityId BrewerId = EntityId.Parse("npc_brewer");
            private static readonly EntityId LeaderId = EntityId.Parse("npc_adventurer_leader");
            private static readonly EntityId ScoutId = EntityId.Parse("npc_adventurer_scout");
            private static readonly EntityId CaptorId = EntityId.Parse("npc_captor");
            private static readonly EntityId PartyId = EntityId.Parse("org_bracken_knives");
            private static readonly EntityId TownId = EntityId.Parse("zone_cordwall");
            private static readonly EntityId MineId = EntityId.Parse("zone_old_mine");

            private RescueBench(NarrativeWorldState world, SandboxVanillaState vanilla, ICheckResolver checks)
            {
                World = world;
                Vanilla = vanilla;
                Checks = checks;
                Actions = StandardActions.CreateRegistry();
                Ecology = new AdventurerEcology();
                Thread = world.Threads.Single(t => t.ArchetypeId == "abduction");
                Risk = world.Knowledge.GetFact(Thread.FactIds.Single(id => world.Knowledge.GetFact(id).Predicate == FactPredicates.AtRisk));
            }

            public EntityId Player => PlayerId;

            public EntityId Brewer => BrewerId;

            public EntityId Leader => LeaderId;

            public EntityId Scout => ScoutId;

            public EntityId Party => PartyId;

            public EntityId Town => TownId;

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public AdventurerEcology Ecology { get; }

            public NarrativeThread Thread { get; }

            public Fact Risk { get; }

            public GameTime Now => Vanilla.Now;

            public static RescueBench Create(ICheckResolver checks)
            {
                NarrativeWorldState world = new NarrativeWorldState(96);
                world.Registry.Add(new NarrativeNpc(PlayerId, "the player"));
                world.Registry.Add(new NarrativeNpc(BrewerId, "Ordel") { Occupation = "brewer" });
                world.Registry.Add(new NarrativeNpc(CaptorId, "Fenn") { Occupation = "bandit" });
                world.Registry.Add(new NarrativeNpc(LeaderId, "Nessa") { Occupation = "adventurer" });
                world.Registry.Add(new NarrativeNpc(ScoutId, "Tavin") { Occupation = "scout" });

                SandboxVanillaState vanilla = new SandboxVanillaState(PlayerId);
                foreach (EntityId who in new[] { PlayerId, BrewerId, CaptorId, LeaderId, ScoutId })
                {
                    vanilla.Define(who, level: 8, zone: TownId);
                }

                vanilla.SetSkill(LeaderId, VanillaSkill.Travel, 12);
                vanilla.SetAttribute(LeaderId, VanillaAttribute.Strength, 12);
                vanilla.SetAttribute(LeaderId, VanillaAttribute.Dexterity, 12);

                Fact risk = new Fact(world.NewId("fact"), BrewerId, FactPredicates.AtRisk, CaptorId, "abducted");
                world.Knowledge.AddFact(risk);
                world.Knowledge.Teach(BrewerId, risk.Id, KnowledgeSource.Participant, 1.0, vanilla.Now, false);
                world.Knowledge.Teach(PlayerId, risk.Id, KnowledgeSource.Hearsay, 0.8, vanilla.Now, false);
                world.Knowledge.Teach(LeaderId, risk.Id, KnowledgeSource.Hearsay, 0.9, vanilla.Now, false);
                world.Knowledge.Teach(ScoutId, risk.Id, KnowledgeSource.Hearsay, 0.9, vanilla.Now, false);

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "abduction", vanilla.Now)
                {
                    State = ThreadState.Active,
                    Tension = 55,
                    Importance = 60
                };
                thread.ParticipantIds.Add(BrewerId);
                thread.ParticipantIds.Add(CaptorId);
                thread.SiteIds.Add(MineId);
                thread.FactIds.Add(risk.Id);
                thread.OpenQuestions.Add("Who brings Ordel back?");
                world.Threads.Add(thread);

                Organization party = new Organization(PartyId, "Bracken Knives", AdventurerEcology.PartyType)
                {
                    LeaderId = LeaderId,
                    Wealth = 35,
                    Legitimacy = 60,
                    Aggression = 45
                };
                party.MemberIds.Add(LeaderId);
                party.MemberIds.Add(ScoutId);
                party.SiteIds.Add(TownId);
                party.Goals.Add(new OrganizationGoal(AdventurerEcology.RescueGoal, BrewerId, 90));
                world.Registry.Add(party);
                world.Registry.GetNpc(LeaderId).OrganizationIds.Add(PartyId);
                world.Registry.GetNpc(ScoutId).OrganizationIds.Add(PartyId);

                vanilla.AdvanceDays(4);
                return new RescueBench(world, vanilla, checks);
            }

            public RescueBench Reopen(NarrativeWorldState world, ICheckResolver checks)
            {
                SandboxVanillaState vanilla = new SandboxVanillaState(PlayerId);
                foreach (EntityId who in new[] { PlayerId, BrewerId, CaptorId, LeaderId, ScoutId })
                {
                    vanilla.Define(who, level: 8, zone: TownId);
                }

                vanilla.Now = Vanilla.Now;
                return new RescueBench(world, vanilla, checks);
            }
        }
    }
}
