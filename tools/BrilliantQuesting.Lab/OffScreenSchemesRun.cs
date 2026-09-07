using System;
using System.Collections.Generic;
using System.IO;
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

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-095 proof run: the player leaves a town for a month, and named NPC goals continue
    /// through the same action library the player uses.
    /// </summary>
    internal static class OffScreenSchemesRun
    {
        public const ulong DefaultSeed = 95095UL;

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);

            Banner(output, "BQ-095: A MONTH AWAY");
            output.WriteLine("  Player zone: " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Player)));
            output.WriteLine("  Town zone:   " + bench.World.Registry.NameOf(bench.Town));
            output.WriteLine("  Elapsed:     " + bench.Vanilla.Now.TotalDays + " days");
            output.WriteLine();

            int acted = bench.Schemes.Advance(
                bench.World,
                bench.Vanilla,
                bench.Checks,
                bench.Actions,
                bench.Vanilla.Now);

            output.WriteLine("  Coarse scheduler attempts: " + acted);
            output.WriteLine();
            for (int i = 0; i < bench.Schemes.LastPass.Count; i++)
            {
                output.WriteLine(Indent(bench.Schemes.LastPass[i].Describe(bench.World)));
            }

            Banner(output, "EVENT LEDGER");
            foreach (WorldEvent recorded in bench.World.Ledger.Events)
            {
                output.WriteLine("  " + recorded.Type
                                 + " by " + bench.World.Registry.NameOf(recorded.Actor)
                                 + " -> " + bench.World.Registry.NameOf(recorded.Target)
                                 + " related " + Join(recorded.Related)
                                 + " witnesses " + recorded.Witnesses.Count);
            }

            Banner(output, "VANILLA OWNERSHIP");
            OffScreenSchemeTrace traveller = bench.Schemes.LastPass.Single(trace => trace.Actor == bench.Traveller);
            output.WriteLine("  " + bench.World.Registry.NameOf(bench.Traveller) + ": " + traveller.Refusal);
            output.WriteLine("  Vanilla still reports them in " + bench.World.Registry.NameOf(bench.Vanilla.GetZoneOf(bench.Traveller)) + ".");
            output.WriteLine("  BQ absence filed for them: " + bench.World.Absences.IsAbsent(bench.Traveller));

            Banner(output, "SAVE/RELOAD IDEMPOTENCY");
            int eventsBeforeReload = bench.World.Ledger.Count;
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(bench.World));
            OffScreenSchemes reopened = new OffScreenSchemes
            {
                MostActorsPerPass = 20,
                MostAttemptsPerPass = 10
            };
            int duplicateAttempts = reopened.Advance(
                reloaded,
                bench.Vanilla,
                bench.Checks,
                bench.Actions,
                bench.Vanilla.Now);
            output.WriteLine("  attempts after reload on the same elapsed interval: " + duplicateAttempts);
            output.WriteLine("  event delta after duplicate pass: " + (reloaded.Ledger.Count - eventsBeforeReload));
        }

        private static string Indent(string text)
        {
            return "  " + text.TrimEnd().Replace("\n", "\n  ") + "\n";
        }

        private static string Join(IReadOnlyList<EntityId> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return "none";
            }

            return string.Join(", ", ids.Select(id => id.ToString()).ToArray());
        }

        private static void Banner(TextWriter output, string title)
        {
            output.WriteLine();
            output.WriteLine("== " + title + " " + new string('=', Math.Max(3, 68 - title.Length)));
        }

        private sealed class ScriptedChecks : ICheckResolver
        {
            private readonly Queue<CheckOutcome> _scripted = new Queue<CheckOutcome>();

            public ScriptedChecks(params CheckOutcome[] outcomes)
            {
                for (int i = 0; outcomes != null && i < outcomes.Length; i++)
                {
                    _scripted.Enqueue(outcomes[i]);
                }
            }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                CheckOutcome outcome = _scripted.Count == 0 ? CheckOutcome.Pass : _scripted.Dequeue();
                int roll = outcome == CheckOutcome.CriticalPass ? 20
                    : outcome == CheckOutcome.CriticalFail ? 1
                    : outcome == CheckOutcome.Pass ? 15
                    : 5;
                return new CheckResult(
                    request.Profile.Id,
                    request.Profile.BaseDifficulty,
                    new List<CheckTerm>(),
                    10,
                    roll,
                    outcome);
            }
        }

        private sealed class Bench
        {
            private Bench()
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

            public static Bench Create(ulong seed)
            {
                Bench bench = new Bench
                {
                    World = new NarrativeWorldState(seed),
                    Actions = StandardActions.CreateRegistry(),
                    Checks = new ScriptedChecks(
                        CheckOutcome.Pass,
                        CheckOutcome.Pass,
                        CheckOutcome.Pass,
                        CheckOutcome.CriticalFail),
                    Schemes = new OffScreenSchemes
                    {
                        MostActorsPerPass = 20,
                        MostAttemptsPerPass = 10
                    }
                };

                bench.Vanilla = new SandboxVanillaState(bench.Player);
                bench.World.Registry.Add(new NarrativeSite(bench.Town, "Cordwall", "town"));
                bench.World.Registry.Add(new NarrativeSite(bench.Road, "the trade road", "road"));
                bench.World.Registry.Add(new NarrativeSite(bench.FarRoad, "the far road", "road"));

                bench.AddActor(bench.Player, "the player", bench.FarRoad, 400);
                bench.AddActor(bench.Thief, "Lysa", bench.Town, 20);
                bench.AddActor(bench.Suitor, "Merren", bench.Town, 40);
                bench.AddActor(bench.Debtor, "Pavel", bench.Town, 60);
                bench.AddActor(bench.Hider, "Nessa", bench.Town, 80);
                bench.AddActor(bench.Avenger, "Caro", bench.Town, 25);
                bench.AddActor(bench.Investor, "Ivet", bench.Town, 800);
                bench.AddActor(bench.Traveller, "Ordel", bench.Road, 35);
                bench.AddActor(bench.Victim, "Jorin", bench.Town, 120);
                bench.AddActor(bench.Beloved, "Elira", bench.Town, 50);
                bench.AddActor(bench.Creditor, "Voss", bench.Town, 300);
                bench.AddActor(bench.Rival, "Sanne", bench.Town, 70);
                bench.AddActor(bench.Miller, "Doran", bench.Town, 90);
                bench.AddActor(bench.Reeve, "Herrick", bench.Town, 150);

                bench.Vanilla.GiveItem(bench.Victim, new ItemDescriptor(bench.Purse, "embroidered purse", "purse", 300));
                bench.Vanilla.GiveItem(bench.Hider, new ItemDescriptor(bench.Evidence, "black ledger", "document", 200));
                bench.Vanilla.GiveItem(bench.Rival, new ItemDescriptor(bench.Wagon, "market wagon", "wagon", 350));
                bench.Vanilla.GiveItem(bench.Miller, new ItemDescriptor(bench.MillWheel, "mill wheel", "tool", 600));

                bench.World.Relationships.ConnectMutual(bench.Suitor, bench.Beloved, RelationKind.Friend, 35);
                bench.World.Relationships.Connect(bench.Debtor, bench.Creditor, RelationKind.Creditor, -35);
                bench.World.Relationships.Connect(bench.Avenger, bench.Rival, RelationKind.Enemy, -80);

                bench.AddGoalsAndThreads();
                bench.Vanilla.SetActorActivity(bench.Traveller, new ActorActivityBuilder(bench.Traveller)
                    .WithPresence(PhysicalPresence.OutsideActiveZone)
                    .WithGlobalGoalEligibility(GlobalGoalEligibility.Eligible)
                    .WithGlobalActivity(GlobalActivityKind.Travelling)
                    .WithZoneTransition(ZoneTransitionState.Pending)
                    .Build());

                new ConsequenceEngine(bench.World, bench.Vanilla).Attach();
                bench.Vanilla.AdvanceDays(30);
                return bench;
            }

            private void AddActor(EntityId id, string name, EntityId zone, int money)
            {
                World.Registry.Add(new NarrativeNpc(id, name));
                Vanilla.Define(id, zone: zone, money: money);
            }

            private void AddGoalsAndThreads()
            {
                World.Registry.GetNpc(Thief).ProblemSolving.Conceal = 0.95;
                World.Registry.GetNpc(Thief).Goals.Add(new NpcGoal("steal", Purse, 90, "Jorin carries the purse Lysa wants"));

                World.Registry.GetNpc(Suitor).ProblemSolving.AskFriends = 0.9;
                World.Registry.GetNpc(Suitor).Goals.Add(new NpcGoal("court", Beloved, 85, "Merren wants Elira to think warmly of him"));

                Fact debt = AddFact(Debtor, FactPredicates.Owes, Creditor, "90 orens");
                World.Knowledge.Teach(Debtor, debt.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, false);
                World.Knowledge.Teach(Creditor, debt.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true);
                World.Obligations.Add(new SocialObligation(
                    World.NewId("obligation"), SocialObligationKind.Debt, Debtor, Creditor, debt.Id,
                    "repay 90 orens", Vanilla.Now, EntityId.None, 2));
                Thread("debt", debt.Id, Debtor, Creditor);
                World.Registry.GetNpc(Debtor).ProblemSolving.Flee = 0.95;
                World.Registry.GetNpc(Debtor).Goals.Add(new NpcGoal("flee_debt", debt.Id, 90, "Voss is pressing him"));

                Fact secret = AddFact(Hider, FactPredicates.Extorted, Victim, "Jorin paid 40 orens", secrecy: 80);
                secret.EvidenceIds.Add(Evidence);
                World.Knowledge.Teach(Hider, secret.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true, Evidence);
                World.Knowledge.Teach(Victim, secret.Id, KnowledgeSource.Participant, 1.0, Vanilla.Now, true, Evidence);
                Thread("blackmail", secret.Id, Hider, Victim);
                World.Registry.GetNpc(Hider).ProblemSolving.Conceal = 0.95;
                World.Registry.GetNpc(Hider).Goals.Add(new NpcGoal("avoid_exposure", secret.Id, 95, "the ledger can prove the extortion"));

                World.Registry.GetNpc(Avenger).ProblemSolving.UseViolence = 0.95;
                World.Registry.GetNpc(Avenger).Goals.Add(new NpcGoal("seek_revenge", Rival, 88, "Sanne humiliated Caro"));

                Fact owner = AddFact(Miller, FactPredicates.Possesses, MillWheel, "mill wheel");
                Fact damaged = AddFact(MillWheel, FactPredicates.Damaged, EntityId.None, "split axle");
                Fact need = AddFact(Reeve, FactPredicates.Needs, MillWheel, new ProductionSpec("flour").ToFactValue());
                NarrativeThread shortage = Thread("shortage", damaged.Id, Investor, Miller, Reeve);
                shortage.FactIds.Add(owner.Id);
                shortage.FactIds.Add(need.Id);
                World.Registry.GetNpc(Investor).ProblemSolving.PaySomeone = 0.95;
                World.Registry.GetNpc(Investor).Goals.Add(new NpcGoal("fund_supplier", damaged.Id, 82, "the mill can earn again if Doran has backing"));

                Fact travelDebt = AddFact(Traveller, FactPredicates.Owes, Creditor, "55 orens");
                Thread("travelling_debt", travelDebt.Id, Traveller, Creditor);
                World.Registry.GetNpc(Traveller).ProblemSolving.Flee = 0.95;
                World.Registry.GetNpc(Traveller).Goals.Add(new NpcGoal("flee_debt", travelDebt.Id, 90, "already moving under Elin's global travel"));
            }

            private Fact AddFact(EntityId subject, string predicate, EntityId obj, string value, int secrecy = 0)
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
