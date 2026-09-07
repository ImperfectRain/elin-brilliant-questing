using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-095. Named people keep pursuing local intentions while the player is away.
    ///
    /// The proof is deliberately a month-sized catch-up rather than a clock. The scheduler decides
    /// who is due for another opportunity; the chosen verb and its consequences are still the
    /// ordinary BQ-093 action path.
    /// </summary>
    public class OffScreenSchemeTests
    {
        [Fact]
        public void AMonthAwayProducesNamedCausalEventsWithoutWatchingTheRoom()
        {
            SchemeTown town = SchemeTown.CreateMixedMonth();

            int acted = town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now);

            Assert.Equal(6, acted);
            Assert.Equal(town.FarRoad, town.Vanilla.GetZoneOf(town.Player));

            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.Theft && e.Actor == town.Thief);
            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.Conversed && e.Actor == town.Suitor);
            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.WentAbsent && e.Actor == town.Debtor);
            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.EvidenceDestroyed && e.Actor == town.Hider);
            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.Harmed && e.Actor == town.Avenger);
            Assert.Contains(town.World.Ledger.Events, e => e.Type == WorldEventType.Helped && e.Actor == town.Investor);
            Assert.DoesNotContain(town.World.Ledger.Events, e => e.Type == WorldEventType.CrimeWitnessed);

            Assert.Equal(town.Thief, town.HolderOf(town.Purse));
            Assert.DoesNotContain(town.Purse, town.Vanilla.GetInventory(town.Victim).Select(i => i.Id));
            Assert.True(town.World.Absences.IsAbsent(town.Debtor));
            Assert.DoesNotContain(town.Evidence, town.Vanilla.GetInventory(town.Hider).Select(i => i.Id));
            Assert.DoesNotContain(town.Wagon, town.Vanilla.GetInventory(town.Rival).Select(i => i.Id));

            OffScreenSchemeTrace failed = Assert.Single(
                town.Schemes.LastPass,
                trace => trace.Actor == town.Avenger && trace.Attempt?.Outcome?.Outcome == CheckOutcome.CriticalFail);
            Assert.False(failed.GoalSatisfied);
            Assert.False(town.World.Registry.GetNpc(town.Avenger).Goals.Single().Satisfied);

            OffScreenSchemeTrace travelling = Assert.Single(town.Schemes.LastPass, trace => trace.Actor == town.Traveller);
            Assert.False(travelling.Acted);
            Assert.Contains("vanilla is already carrying", travelling.Refusal);
            Assert.Equal(town.Road, town.Vanilla.GetZoneOf(town.Traveller));
            Assert.False(town.World.Absences.IsAbsent(town.Traveller));

            Assert.All(town.Schemes.LastPass.Where(trace => trace.Acted), trace =>
                Assert.Equal(ContextObservation.OffScreen, trace.Attempt.Outcome.Observation));
            Assert.All(town.World.Ledger.Events, e => Assert.Empty(e.Witnesses));
            Assert.DoesNotContain(
                town.World.Knowledge.Facts.Values,
                fact => fact.Predicate == FactPredicates.LocatedAt);
        }

        [Fact]
        public void TheInspectorExplainsWhyTheActionWasChosenAndWhatHappened()
        {
            SchemeTown town = SchemeTown.CreateMixedMonth();

            town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now);

            OffScreenSchemeTrace trace = Assert.Single(town.Schemes.LastPass, t => t.Actor == town.Hider);
            string described = trace.Describe(town.World);

            Assert.Contains("goal mattered: avoid_exposure", described);
            Assert.Contains("option destroy_evidence", described);
            Assert.Contains("opportunity", described);
            Assert.Contains("selected NarrativeAction: destroy_evidence", described);
            Assert.Contains("recorded EvidenceDestroyed", described);
        }

        [Fact]
        public void SaveReloadAndProcessingTheSameElapsedIntervalDoesNotDuplicateEffects()
        {
            SchemeTown town = SchemeTown.CreateMixedMonth();
            town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now);

            int eventsAfterFirstPass = town.World.Ledger.Count;
            string saved = WorldStateSerializer.Save(town.World);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(saved);

            OffScreenSchemes reopened = new OffScreenSchemes
            {
                MostActorsPerPass = 20,
                MostAttemptsPerPass = 10
            };

            int actedAgain = reopened.Advance(reloaded, town.Vanilla, town.Checks, town.Actions, town.Now);

            Assert.Equal(0, actedAgain);
            Assert.Equal(eventsAfterFirstPass, reloaded.Ledger.Count);
            Assert.Empty(reopened.LastPass);
            Assert.Equal(town.Now, reloaded.Registry.GetNpc(town.Thief).LastSimulatedAt);
        }

        [Fact]
        public void APassCappedBeforeEveryActorStillConsumesThatElapsedWindow()
        {
            SchemeTown town = SchemeTown.CreateMixedMonth();
            town.Schemes.MostAttemptsPerPass = 1;

            Assert.Equal(1, town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now));
            int eventsAfterCappedPass = town.World.Ledger.Count;

            Assert.Equal(0, town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now));
            Assert.Equal(eventsAfterCappedPass, town.World.Ledger.Count);
        }

        [Fact]
        public void TheSchedulerOnlySelectsExistingNarrativeActions()
        {
            SchemeTown town = SchemeTown.CreateMixedMonth();

            town.Schemes.Advance(town.World, town.Vanilla, town.Checks, town.Actions, town.Now);

            string[] selected = town.Schemes.LastPass
                .Where(trace => trace.Acted)
                .Select(trace => trace.Attempt.Intent.ActionId)
                .OrderBy(id => id)
                .ToArray();

            Assert.Contains("pickpocket", selected);
            Assert.Contains("rapport", selected);
            Assert.Contains("go_to_ground", selected);
            Assert.Contains("destroy_evidence", selected);
            Assert.Contains("sabotage", selected);
            Assert.Contains("invest_in_supplier", selected);
            Assert.DoesNotContain(selected, id => id.Contains("scheme"));
            Assert.All(selected, id => Assert.NotNull(town.Actions.Get(id)));
        }

        private sealed class SchemeTown
        {
            private SchemeTown()
            {
            }

            public readonly EntityId Player = EntityId.Parse("npc_player");
            public readonly EntityId Thief = EntityId.Parse("npc_01_thief");
            public readonly EntityId Suitor = EntityId.Parse("npc_02_suitor");
            public readonly EntityId Debtor = EntityId.Parse("npc_03_debtor");
            public readonly EntityId Hider = EntityId.Parse("npc_04_hider");
            public readonly EntityId Avenger = EntityId.Parse("npc_05_avenger");
            public readonly EntityId Investor = EntityId.Parse("npc_06_investor");
            public readonly EntityId Traveller = EntityId.Parse("npc_07_traveller");
            public readonly EntityId Victim = EntityId.Parse("npc_20_victim");
            public readonly EntityId Beloved = EntityId.Parse("npc_21_beloved");
            public readonly EntityId Creditor = EntityId.Parse("npc_22_creditor");
            public readonly EntityId Rival = EntityId.Parse("npc_23_rival");
            public readonly EntityId Miller = EntityId.Parse("npc_24_miller");
            public readonly EntityId Reeve = EntityId.Parse("npc_25_reeve");
            public readonly EntityId Town = EntityId.Parse("zone_cordwall");
            public readonly EntityId Road = EntityId.Parse("zone_road");
            public readonly EntityId FarRoad = EntityId.Parse("zone_far_road");
            public readonly EntityId Purse = EntityId.Parse("item_purse");
            public readonly EntityId Evidence = EntityId.Parse("item_black_ledger");
            public readonly EntityId Wagon = EntityId.Parse("item_wagon");
            public readonly EntityId MillWheel = EntityId.Parse("item_mill_wheel");

            public NarrativeWorldState World { get; private set; }

            public SandboxVanillaState Vanilla { get; private set; }

            public ActionRegistry Actions { get; private set; }

            public ICheckResolver Checks { get; private set; }

            public OffScreenSchemes Schemes { get; private set; }

            public GameTime Now => Vanilla.Now;

            public static SchemeTown CreateMixedMonth()
            {
                SchemeTown town = new SchemeTown
                {
                    World = new NarrativeWorldState(95095),
                    Actions = StandardActions.CreateRegistry(),
                    Checks = new FixedCheckResolver(CheckOutcome.Pass)
                        .Then(CheckOutcome.Pass)
                        .Then(CheckOutcome.Pass)
                        .Then(CheckOutcome.Pass)
                        .Then(CheckOutcome.CriticalFail),
                    Schemes = new OffScreenSchemes
                    {
                        MostActorsPerPass = 20,
                        MostAttemptsPerPass = 10
                    }
                };

                town.Vanilla = new SandboxVanillaState(town.Player);
                town.World.Registry.Add(new NarrativeSite(town.Town, "Cordwall", "town"));
                town.World.Registry.Add(new NarrativeSite(town.Road, "the trade road", "road"));
                town.World.Registry.Add(new NarrativeSite(town.FarRoad, "the far road", "road"));

                town.AddActor(town.Player, "You", town.FarRoad, 400);
                town.AddActor(town.Thief, "Lysa", town.Town, 20);
                town.AddActor(town.Suitor, "Merren", town.Town, 40);
                town.AddActor(town.Debtor, "Pavel", town.Town, 60);
                town.AddActor(town.Hider, "Nessa", town.Town, 80);
                town.AddActor(town.Avenger, "Caro", town.Town, 25);
                town.AddActor(town.Investor, "Ivet", town.Town, 800);
                town.AddActor(town.Traveller, "Ordel", town.Road, 35);
                town.AddActor(town.Victim, "Jorin", town.Town, 120);
                town.AddActor(town.Beloved, "Elira", town.Town, 50);
                town.AddActor(town.Creditor, "Voss", town.Town, 300);
                town.AddActor(town.Rival, "Sanne", town.Town, 70);
                town.AddActor(town.Miller, "Doran", town.Town, 90);
                town.AddActor(town.Reeve, "Herrick", town.Town, 150);

                town.Vanilla.GiveItem(town.Victim, new ItemDescriptor(town.Purse, "embroidered purse", "purse", 300));
                town.Vanilla.GiveItem(town.Hider, new ItemDescriptor(town.Evidence, "black ledger", "document", 200));
                town.Vanilla.GiveItem(town.Rival, new ItemDescriptor(town.Wagon, "market wagon", "wagon", 350));
                town.Vanilla.GiveItem(town.Miller, new ItemDescriptor(town.MillWheel, "mill wheel", "tool", 600));

                town.World.Relationships.ConnectMutual(town.Suitor, town.Beloved, RelationKind.Friend, 35);
                town.World.Relationships.Connect(town.Debtor, town.Creditor, RelationKind.Creditor, -35);
                town.World.Relationships.Connect(town.Avenger, town.Rival, RelationKind.Enemy, -80);

                town.AddTheftGoal();
                town.AddCourtshipGoal();
                town.AddDebtGoal();
                town.AddEvidenceGoal();
                town.AddRevengeGoal();
                town.AddInvestmentGoal();
                town.AddTravellingGoal();

                town.Vanilla.SetActorActivity(town.Traveller, new ActorActivityBuilder(town.Traveller)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                    .WithGlobalActivity(GlobalActivityKind.Travelling)
                    .WithZoneTransition(ZoneTransitionState.Pending)
                    .Build());

                new ConsequenceEngine(town.World, town.Vanilla).Attach();
                town.Vanilla.AdvanceDays(30);
                return town;
            }

            public EntityId HolderOf(EntityId item)
            {
                foreach (EntityId actor in World.Registry.Npcs.Keys)
                {
                    if (Vanilla.GetInventory(actor).Any(held => held.Id == item))
                    {
                        return actor;
                    }
                }

                return EntityId.None;
            }

            private void AddActor(EntityId id, string name, EntityId zone, int money)
            {
                World.Registry.Add(new NarrativeNpc(id, name));
                Vanilla.Define(id, zone: zone, money: money);
            }

            private void AddTheftGoal()
            {
                World.Registry.GetNpc(Thief).ProblemSolving.Conceal = 0.95;
                World.Registry.GetNpc(Thief).Goals.Add(new NpcGoal("steal", Purse, 90, "Jorin carries the purse Lysa wants"));
            }

            private void AddCourtshipGoal()
            {
                World.Registry.GetNpc(Suitor).ProblemSolving.AskFriends = 0.9;
                World.Registry.GetNpc(Suitor).Goals.Add(new NpcGoal("court", Beloved, 85, "Merren wants Elira to think warmly of him"));
            }

            private void AddDebtGoal()
            {
                Fact debt = AddFact(Debtor, FactPredicates.Owes, Creditor, "90 orens");
                World.Knowledge.Teach(Debtor, debt.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, false);
                World.Knowledge.Teach(Creditor, debt.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true);
                World.Obligations.Add(new SocialObligation(
                    World.NewId("obligation"),
                    SocialObligationKind.Debt,
                    Debtor,
                    Creditor,
                    debt.Id,
                    "repay 90 orens",
                    Vanilla.Now,
                    EntityId.None,
                    2));

                NarrativeThread thread = Thread("debt", debt.Id, Debtor, Creditor);
                thread.OpenQuestions.Add("Will Pavel answer Voss's debt?");
                World.Registry.GetNpc(Debtor).ProblemSolving.Flee = 0.95;
                World.Registry.GetNpc(Debtor).Goals.Add(new NpcGoal("flee_debt", debt.Id, 90, "Voss is pressing him"));
            }

            private void AddEvidenceGoal()
            {
                Fact secret = AddFact(Hider, FactPredicates.Extorted, Victim, "Jorin paid 40 orens", secrecy: 80);
                secret.EvidenceIds.Add(Evidence);
                World.Knowledge.Teach(Hider, secret.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true, Evidence);
                World.Knowledge.Teach(Victim, secret.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true, Evidence);

                NarrativeThread thread = Thread("blackmail", secret.Id, Hider, Victim);
                thread.OpenQuestions.Add("Can Nessa keep the black ledger quiet?");
                World.Registry.GetNpc(Hider).ProblemSolving.Conceal = 0.95;
                World.Registry.GetNpc(Hider).Goals.Add(new NpcGoal("avoid_exposure", secret.Id, 95, "the ledger can prove the extortion"));
            }

            private void AddRevengeGoal()
            {
                World.Registry.GetNpc(Avenger).ProblemSolving.UseViolence = 0.95;
                World.Registry.GetNpc(Avenger).Goals.Add(new NpcGoal("seek_revenge", Rival, 88, "Sanne humiliated Caro"));
            }

            private void AddInvestmentGoal()
            {
                Fact owner = AddFact(Miller, FactPredicates.Possesses, MillWheel, "mill wheel");
                Fact damaged = AddFact(MillWheel, FactPredicates.Damaged, EntityId.None, "split axle");
                Fact need = AddFact(Reeve, FactPredicates.Needs, MillWheel, new ProductionSpec("flour").ToFactValue());

                NarrativeThread thread = Thread("shortage", damaged.Id, Investor, Miller, Reeve);
                thread.FactIds.Add(owner.Id);
                thread.FactIds.Add(need.Id);
                thread.SiteIds.Add(Town);
                thread.OpenQuestions.Add("Who will fund the mill?");

                World.Registry.GetNpc(Investor).ProblemSolving.PaySomeone = 0.95;
                World.Registry.GetNpc(Investor).Goals.Add(new NpcGoal("fund_supplier", damaged.Id, 82, "the mill can earn again if Doran has backing"));
            }

            private void AddTravellingGoal()
            {
                Fact debt = AddFact(Traveller, FactPredicates.Owes, Creditor, "55 orens");
                NarrativeThread thread = Thread("travelling_debt", debt.Id, Traveller, Creditor);
                thread.OpenQuestions.Add("Will Ordel duck the debt?");
                World.Registry.GetNpc(Traveller).ProblemSolving.Flee = 0.95;
                World.Registry.GetNpc(Traveller).Goals.Add(new NpcGoal("flee_debt", debt.Id, 90, "already moving under Elin's global travel"));
            }

            private Fact AddFact(
                EntityId subject,
                string predicate,
                EntityId obj,
                string value,
                int secrecy = 0)
            {
                Fact fact = new Fact(World.NewId("fact"), subject, predicate, obj, value, TruthState.True, secrecy);
                World.Knowledge.AddFact(fact);
                return fact;
            }

            private NarrativeThread Thread(string archetype, EntityId fact, params EntityId[] participants)
            {
                NarrativeThread thread = new NarrativeThread(World.NewId("thread"), archetype, Vanilla.Now)
                {
                    State = ThreadState.Active
                };
                for (int i = 0; i < participants.Length; i++)
                {
                    thread.ParticipantIds.Add(participants[i]);
                }

                thread.FactIds.Add(fact);
                thread.SiteIds.Add(Town);
                World.Threads.Add(thread);
                return thread;
            }
        }
    }
}
