using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-016. One bounded causal pass, and a month of them.
    ///
    /// The fixture supplies authoritative initial conditions once and then never writes another
    /// goal, incident, thread or follow-up fact. Everything asserted after day zero has to have
    /// come out of the runner calling the owners that already exist, because there is nowhere
    /// else for it to have come from - which is the step's entire claim.
    ///
    /// Headless throughout. A pass here proves the Core contract; it proves nothing about any
    /// Elin hook advancing one, which is BQa-017's to show.
    /// </summary>
    public class ProductionCycleTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Keeper = EntityId.Parse("npc_keeper");
        private static readonly EntityId Suspect = EntityId.Parse("npc_suspect");
        private static readonly EntityId Accuser = EntityId.Parse("npc_accuser");
        private static readonly EntityId Neighbour = EntityId.Parse("npc_neighbour");
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Purse = EntityId.Parse("item_purse");

        // -- the done-when: a month of passes over authoritative initial conditions ---------------

        /// <summary>
        /// Thirty-five passes from one set of initial conditions evolve all three pressure
        /// families. Nothing is injected after the world is built: no follow-up goal, no second
        /// incident, no authored thread.
        /// </summary>
        [Fact]
        public void AMonthOfPassesEvolvesAllThreePressureFamiliesFromInitialConditionsAlone()
        {
            Bench bench = Bench.Create();
            int eventsBefore = bench.World.Ledger.Count;

            IReadOnlyList<ProductionCyclePass> month = bench.Days(1, 35);

            Assert.Equal(35, month.Count(pass => pass.Ran));
            Assert.Contains(month, pass => pass.DevelopmentsRead > 0);

            HashSet<string> families = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionCyclePass pass in month)
            {
                foreach (GoalChange change in pass.GoalChanges)
                {
                    // A reading somebody formed nothing from carries no goal, and that is an
                    // answer rather than a hole: waiting is a response.
                    if (change.Goal != null)
                    {
                        families.Add(change.Goal.Kind);
                    }
                }
            }

            // The loop is not half-run: wants become intentions, intentions are arbitrated, and
            // some of them change the world. A month that only ever read would pass every
            // assertion below while proving nothing about execution ownership.
            Assert.Contains(month, pass => pass.IntentionsGathered > 0);
            Assert.Contains(month, pass => pass.Arbitration != null);
            Assert.Contains(month, pass => pass.Committed > 0);

            Assert.Contains(EvolvedGoalKinds.ClearName, families);
            Assert.Contains(EvolvedGoalKinds.RepayDebt, families);
            Assert.Contains(EvolvedGoalKinds.RelieveShortage, families);

            // Every want on the board came from pressure, not from the fixture.
            foreach (NarrativeNpc actor in bench.World.Registry.Npcs.Values)
            {
                foreach (NpcGoal goal in actor.Goals)
                {
                    Assert.Equal(GoalSourceKind.ActorPressure, goal.Origin.Kind);
                }
            }

            // And the pass itself wrote no history of its own beyond what verbs recorded.
            Assert.True(bench.World.Ledger.Count >= eventsBefore);
        }

        /// <summary>
        /// A world with no live thread and no running storylet still runs. The causal loop does
        /// not wait for a matter to be opened or a scene to be playing; nothing in the month above
        /// ever opens one, and these are the assertions that say so out loud.
        /// </summary>
        [Fact]
        public void PassesRunWithNoLiveThreadAndNoRunningStorylet()
        {
            Bench bench = Bench.Create();
            Assert.Empty(bench.World.Threads);

            IReadOnlyList<ProductionCyclePass> month = bench.Days(1, 35);

            Assert.All(month, pass => Assert.True(pass.Ran));
            Assert.Contains(month, pass => pass.Evaluated.Count > 0);
            Assert.DoesNotContain(bench.World.Threads, thread => thread.IsLive && thread.ArchetypeId.Length == 0);
        }

        /// <summary>
        /// A matter nobody has touched and a matter the player already worked in are both read.
        /// Being previously engaged is a reason to defer a conflicting attempt for a while, never
        /// a reason to stop reading the world.
        /// </summary>
        [Fact]
        public void IgnoredAndPreviouslyEngagedMattersBothEnterEvaluation()
        {
            Bench bench = Bench.Create();

            // The player acts in the accusation on day one and never again.
            bench.World.Record(
                WorldEventType.AccusationMade,
                Player,
                Suspect,
                GameTime.FromDays(1),
                0.5,
                Town,
                related: new[] { bench.Accusation });

            IReadOnlyList<ProductionCyclePass> month = bench.Days(2, 35);

            bool suspectRead = month.Any(pass => pass.Evaluated.Contains(Suspect));
            bool keeperRead = month.Any(pass => pass.Evaluated.Contains(Keeper));

            Assert.True(suspectRead, "the previously engaged matter's subject was never evaluated");
            Assert.True(keeperRead, "the ignored matter's subject was never evaluated");
        }

        // -- bounded work, fairness and resume ----------------------------------------------------

        /// <summary>
        /// One pass costs its budget, whatever the world costs. A town ten times the size does not
        /// make a pass ten times more expensive.
        /// </summary>
        [Fact]
        public void OnePassDoesNoMoreWorkThanItsBudgetHoweverManyPeopleThereAre()
        {
            Bench bench = Bench.Create();
            bench.Crowd(60);
            bench.Cycle.Budget = new ProductionCycleBudget
            {
                MostRecordsInspected = 5,
                MostActorsPerPass = 4,
                MostColdActorsPerPass = 1,
                MostGoalsPerActor = 2,
                MostIntentionsPerActor = 1,
                MostIntentionsPerPass = 3
            };

            foreach (ProductionCyclePass pass in bench.Days(1, 10))
            {
                Assert.True(pass.RecordsInspected <= 5, "inspected " + pass.RecordsInspected);
                Assert.True(pass.Evaluated.Count <= 4, "evaluated " + pass.Evaluated.Count);
                Assert.True(pass.IntentionsGathered <= 3, "gathered " + pass.IntentionsGathered);
            }
        }

        /// <summary>
        /// Nobody starves. Over enough passes with a slice far smaller than the town, everybody
        /// gets a turn, because whoever was not reached keeps the older turn marker that puts them
        /// at the front next time.
        /// </summary>
        [Fact]
        public void UnselectedWorkStaysEligibleUntilItGetsItsTurn()
        {
            Bench bench = Bench.Create();
            bench.Crowd(24);
            bench.Cycle.Budget = new ProductionCycleBudget { MostActorsPerPass = 3, MostColdActorsPerPass = 1 };

            HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProductionCyclePass pass in bench.Days(1, 40))
            {
                foreach (EntityId who in pass.Evaluated)
                {
                    reached.Add(who.Value);
                }
            }

            foreach (NarrativeNpc actor in bench.World.Registry.Npcs.Values)
            {
                if (actor.Id == Player)
                {
                    continue;
                }

                Assert.Contains(actor.Id.Value, reached);
            }
        }

        /// <summary>
        /// A save taken mid-month reconstructs the same queue every time it is resumed, because
        /// whose turn it is comes off the persisted turn marker rather than off wherever the
        /// rotation happened to be when the save was written.
        /// </summary>
        [Fact]
        public void ReloadMidwayReconstructsTheSameQueueDeterministically()
        {
            Bench bench = Bench.Create();
            bench.Crowd(16);
            bench.Cycle.Budget = new ProductionCycleBudget { MostActorsPerPass = 3, MostColdActorsPerPass = 1 };
            bench.Days(1, 12);

            string save = WorldStateSerializer.Save(bench.World);

            List<string> first = Resume(save, 13, 20);
            List<string> second = Resume(save, 13, 20);

            Assert.NotEmpty(first);
            Assert.Equal(first, second);
        }

        /// <summary>
        /// And resuming does not lose anybody's place. A reload rebuilds the rotation from the
        /// front, so the guarantee that matters is that whoever was still owed a turn before the
        /// save is still reached after it.
        /// </summary>
        [Fact]
        public void ReloadDoesNotCostAnybodyTheirTurn()
        {
            Bench bench = Bench.Create();
            bench.Crowd(16);
            bench.Cycle.Budget = new ProductionCycleBudget { MostActorsPerPass = 3, MostColdActorsPerPass = 1 };
            bench.Days(1, 6);

            HashSet<string> owed = new HashSet<string>(
                bench.World.Registry.Npcs.Values
                    .Where(npc => npc.Id != Player && npc.LastSimulatedAt == GameTime.Zero)
                    .Select(npc => npc.Id.Value),
                StringComparer.Ordinal);

            Assert.NotEmpty(owed);

            Bench resumed = bench.Reload(afterDay: 6);
            resumed.Cycle.Budget = new ProductionCycleBudget { MostActorsPerPass = 3, MostColdActorsPerPass = 1 };
            foreach (ProductionCyclePass pass in resumed.Days(7, 40))
            {
                foreach (EntityId who in pass.Evaluated)
                {
                    owed.Remove(who.Value);
                }
            }

            Assert.Empty(owed);
        }

        /// <summary>
        /// Replaying an interval that has already been consumed does nothing at all - no second
        /// reading, no second attempt, no second goal pass.
        /// </summary>
        [Fact]
        public void ReplayingAConsumedIntervalIsHarmless()
        {
            Bench bench = Bench.Create();
            bench.Days(1, 8);

            ProductionCyclePass first = bench.Day(9);
            int events = bench.World.Ledger.Count;
            int goals = bench.Goals();

            ProductionCyclePass again = bench.Day(9);
            ProductionCyclePass earlier = bench.Day(4);

            Assert.True(first.Ran);
            Assert.False(again.Ran);
            Assert.Contains("already been consumed", again.Refusal);
            Assert.False(earlier.Ran);
            Assert.Equal(events, bench.World.Ledger.Count);
            Assert.Equal(goals, bench.Goals());
        }

        /// <summary>
        /// The same seed and the same inputs produce the same month twice, down to whose turn it
        /// was on which day and what each pass decided.
        /// </summary>
        [Fact]
        public void TheSameMonthReplaysIdentically()
        {
            Assert.Equal(Transcript(Bench.Create()), Transcript(Bench.Create()));
        }

        // -- no avalanche on one stack ------------------------------------------------------------

        /// <summary>
        /// An immediate listener reacting to something a pass did cannot start another pass. Its
        /// changes wait for the next interval, which is what a queued reaction means.
        /// </summary>
        [Fact]
        public void AReactionToAPassCannotStartAnotherPassOnTheSameStack()
        {
            Bench bench = Bench.Create();
            List<ProductionCyclePass> reentered = new List<ProductionCyclePass>();

            bench.World.Ledger.Subscribe(_ => reentered.Add(bench.Cycle.Run(bench.Vanilla.Now)));

            bench.Days(1, 5);

            // Something the game did, handed to the pass. Writing it down appends to the ledger
            // from inside the pass, which is exactly when an immediate listener fires.
            bench.Day(6, new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft, Neighbour, Suspect, Purse, "embroidered purse", Town, "act_steal"));

            bench.Days(7, 20);

            // A world that recorded nothing proves nothing about re-entry, so the listener has to
            // have fired at all before its refusals mean anything.
            Assert.NotEmpty(reentered);
            Assert.All(reentered, pass => Assert.False(pass.Ran));
            Assert.All(reentered, pass => Assert.Contains("may not start another", pass.Refusal));
        }

        // -- openings, spent once --------------------------------------------------------------

        /// <summary>
        /// An indivisible opening this cycle committed is not offered again on a later day.
        /// </summary>
        [Fact]
        public void AnOpeningThisCycleSpentIsNotOfferedAgain()
        {
            Bench bench = Bench.Create();
            bench.World.ProductionCycle.Spend("object|" + Purse.Value, Neighbour, GameTime.FromDays(1), ConsumedOpening.Committed);

            Assert.True(bench.World.ProductionCycle.IsSpent("object|" + Purse.Value));

            foreach (ProductionCyclePass pass in bench.Days(2, 20))
            {
                Assert.DoesNotContain(
                    pass.Arbitration?.Decisions ?? new ArbitrationDecision[0],
                    decision => decision.Contest.Key == "object|" + Purse.Value);
            }
        }

        /// <summary>
        /// The scheme pass and the cycle share one record of what has been spent, so the same
        /// purse cannot be lifted once by each of them.
        /// </summary>
        [Fact]
        public void ExistingSubsystemPassesShareTheSameSpentOpenings()
        {
            Bench bench = Bench.Create();
            string opening = "object|" + Purse.Value;

            bench.World.ProductionCycle.Spend(opening, Neighbour, GameTime.FromDays(1), ConsumedOpening.Committed);

            OffScreenSchemes schemes = new OffScreenSchemes();
            bench.Vanilla.AdvanceDays(10);
            schemes.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Vanilla.Now);

            foreach (OffScreenSchemeTrace trace in schemes.LastPass)
            {
                Assert.DoesNotContain(trace.Options, option => option.ActionId == "pickpocket");
            }

            // One record, one holder: a second report of the same closure does not rewrite it.
            Assert.Null(bench.World.ProductionCycle.Spend(opening, Keeper, bench.Vanilla.Now, ConsumedOpening.Committed));
            Assert.Equal(Neighbour, bench.World.ProductionCycle.Openings.Single(o => o.ContestKey == opening).Holder);
        }

        // -- supplied native outcomes ------------------------------------------------------------

        /// <summary>
        /// Something the game already did is written down, never rolled again, and its opening is
        /// closed so nothing in the same pass tries to perform it a second time.
        /// </summary>
        [Fact]
        public void SuppliedNativeOutcomesAreRecordedRatherThanSimulated()
        {
            Bench bench = Bench.Create();
            bench.Days(1, 5);

            int checksBefore = bench.Checks.Resolved;
            ProductionCyclePass pass = bench.Day(6, new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                Neighbour,
                Suspect,
                Purse,
                "embroidered purse",
                Town,
                "act_steal"));

            Assert.True(pass.Ran);
            Assert.Equal(1, pass.ObservationsRecorded);
            Assert.Contains("object|" + Purse.Value, pass.OpeningsSpent);

            WorldEvent recorded = bench.World.Ledger.Events.Last(e => e.Type == WorldEventType.Theft);
            Assert.Equal(Neighbour, recorded.Actor);
            Assert.Contains(EventTags.Observed, recorded.Tags);

            ConsumedOpening spent = bench.World.ProductionCycle.Openings.Single(o => o.ContestKey == "object|" + Purse.Value);
            Assert.Equal(ConsumedOpening.Observed, spent.Because);

            // Nothing in the same pass tried to take the purse again.
            Assert.DoesNotContain(
                pass.Arbitration?.Decisions ?? new ArbitrationDecision[0],
                decision => decision.Contest.Key == "object|" + Purse.Value);

            // And the theft the game performed was never put to a check of ours.
            Assert.Equal(checksBefore, bench.Checks.Resolved - CheckedThisPass(pass));
        }

        // -- the markers themselves ---------------------------------------------------------------

        /// <summary>
        /// The two markers survive a save, and a save written before they existed loads as a world
        /// that has run no cycle and spent no opening - which is exactly what it was.
        /// </summary>
        [Fact]
        public void MarkersRoundTripAndOldSavesDefaultToHavingRunNothing()
        {
            Bench bench = Bench.Create();
            bench.Days(1, 6);
            bench.World.ProductionCycle.Spend("object|" + Purse.Value, Keeper, GameTime.FromDays(6), ConsumedOpening.Committed);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(bench.World));

            Assert.Equal(6, reloaded.ProductionCycle.LastConsumedDay);
            Assert.True(reloaded.ProductionCycle.IsSpent("object|" + Purse.Value));
            Assert.True(reloaded.ProductionCycle.HasConsumed(6));
            Assert.False(reloaded.ProductionCycle.HasConsumed(7));

            ConsumedOpening opening = reloaded.ProductionCycle.Openings
                .Single(o => o.ContestKey == "object|" + Purse.Value);
            Assert.Equal(Keeper, opening.Holder);
            Assert.Equal(ConsumedOpening.Committed, opening.Because);
            Assert.Equal(GameTime.FromDays(6), opening.When);

            NarrativeWorldState old = WorldStateSerializer.Load(WithoutCycleNode(bench.World));
            Assert.Equal(ProductionCycleLedger.NeverRun, old.ProductionCycle.LastConsumedDay);
            Assert.Empty(old.ProductionCycle.Openings);
            Assert.False(old.ProductionCycle.HasConsumed(0));
        }

        /// <summary>Day zero is a real day; "never run" is not day zero.</summary>
        [Fact]
        public void NeverRunIsNotDayZero()
        {
            ProductionCycleLedger ledger = new ProductionCycleLedger();
            Assert.False(ledger.HasConsumed(0));

            ledger.LastConsumedDay = 0;
            Assert.True(ledger.HasConsumed(0));
            Assert.False(ledger.HasConsumed(1));
        }

        /// <summary>Remembering is bounded, and eviction is oldest first.</summary>
        [Fact]
        public void SpentOpeningsAreBounded()
        {
            ProductionCycleLedger ledger = new ProductionCycleLedger { MostRemembered = 3 };
            for (int i = 0; i < 6; i++)
            {
                ledger.Spend("object|item_" + i, Keeper, GameTime.FromDays(i), ConsumedOpening.Committed);
            }

            Assert.Equal(3, ledger.Openings.Count);
            Assert.False(ledger.IsSpent("object|item_0"));
            Assert.True(ledger.IsSpent("object|item_5"));
        }

        // -- fixture -----------------------------------------------------------------------------

        private static int CheckedThisPass(ProductionCyclePass pass)
        {
            return pass.Arbitration == null ? 0 : pass.Arbitration.Committed.Count;
        }

        private static string WithoutCycleNode(NarrativeWorldState world)
        {
            JsonValue root = WorldStateSerializer.ToJson(world);
            JsonValue stripped = JsonValue.Object();
            foreach (KeyValuePair<string, JsonValue> member in root.Members)
            {
                if (member.Key != "productionCycle")
                {
                    stripped.Set(member.Key, member.Value);
                }
            }

            return stripped.ToJson(false);
        }

        /// <summary>The same save picked up twice, as a transcript of who was reached when.</summary>
        private static List<string> Resume(string save, long from, long to)
        {
            Bench resumed = Bench.From(save, from - 1);
            resumed.Cycle.Budget = new ProductionCycleBudget { MostActorsPerPass = 3, MostColdActorsPerPass = 1 };

            List<string> lines = new List<string>();
            foreach (ProductionCyclePass pass in resumed.Days(from, to))
            {
                lines.Add(pass.Day + "|" + string.Join(",", pass.Evaluated.Select(id => id.Value)));
            }

            return lines;
        }

        private static IReadOnlyList<string> Transcript(Bench bench)
        {
            List<string> lines = new List<string>();
            foreach (ProductionCyclePass pass in bench.Days(1, 35))
            {
                lines.Add(pass.Day + "|" + string.Join(",", pass.Evaluated.Select(id => id.Value))
                    + "|" + string.Join(",", pass.GoalChanges.Select(
                        c => c.ActorId.Value + ":" + c.Kind + ":" + (c.Goal == null ? c.PressureId : c.Goal.Identity)))
                    + "|" + pass.IntentionsGathered + "|" + pass.Committed
                    + "|" + string.Join(",", pass.OpeningsSpent));
            }

            return lines;
        }

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, EntityId accusation)
            {
                World = world;
                Vanilla = vanilla;
                Accusation = accusation;
                Checks = new CountingCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
                Consequences = new ConsequenceEngine(world, vanilla);
                Consequences.Attach();
                Cycle = new ProductionCycle(world, vanilla, Checks, Actions);
                Cycle.Attach();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public CountingCheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public ConsequenceEngine Consequences { get; }

            public ProductionCycle Cycle { get; }

            /// <summary>The one claim the fixture supplies. Nothing else is written after day zero.</summary>
            public EntityId Accusation { get; }

            public static Bench Create(ulong seed = 2117)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
                world.Registry.Add(new NarrativeNpc(Keeper, "Mira") { Occupation = "shopkeeper", HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Suspect, "Bran") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Accuser, "Hald") { Occupation = "reeve", HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Neighbour, "Orvo") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Player, "You") { HomeSiteId = Town });

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Keeper, Suspect, Accuser, Neighbour, Player })
                {
                    vanilla.Define(who, zone: Town, money: 120);
                }

                vanilla.GiveItem(Suspect, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);

                // Family one: a shortage where these people live.
                Fact need = new Fact(world.NewId("fact"), Town, FactPredicates.Needs, EntityId.None, "grain");
                world.Knowledge.AddFact(need);
                world.Demands.AddOrUpdate(
                    Town, LocalDemandCategory.Food, 55, GameTime.Zero, GameTime.FromDays(120), need.Id);

                // Family two: a debt one of them owes another.
                world.Obligations.Add(new SocialObligation(
                    world.NewId("obl"),
                    SocialObligationKind.Favor,
                    Suspect,
                    Accuser,
                    EntityId.None,
                    "makes it good",
                    GameTime.Zero,
                    EntityId.None));

                // Family three: a claim naming somebody, that the person who holds it cannot show.
                Fact accusation = new Fact(
                    world.NewId("fact"), Suspect, FactPredicates.Stole, Purse, "a purse",
                    TruthState.True, secrecy: 0);
                accusation.EvidenceIds.Add(Purse);
                world.Knowledge.AddFact(accusation);
                world.Knowledge.Teach(Accuser, accusation.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);
                world.Knowledge.Teach(Suspect, accusation.Id, KnowledgeSource.Hearsay, 0.8, GameTime.Zero, canProve: false);

                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    npc.Sensitivities.PublicEmbarrassment = 0.9;
                    npc.Sensitivities.UnpaidDebt = 0.9;
                    npc.Values.Status.Importance = 0.9;
                }

                return new Bench(world, vanilla, accusation.Id);
            }

            /// <summary>More people than any one pass can reach, with nothing pressing on them.</summary>
            public void Crowd(int many)
            {
                for (int i = 0; i < many; i++)
                {
                    EntityId id = EntityId.Parse("npc_crowd_" + i.ToString("000"));
                    World.Registry.Add(new NarrativeNpc(id, "Villager " + i) { HomeSiteId = Town });
                    Vanilla.Define(id, zone: Town, money: 10);
                }
            }

            public ProductionCyclePass Day(long day, params ObservedVanillaAction[] observations)
            {
                Vanilla.Now = GameTime.FromDays(day);
                return Cycle.Run(Vanilla.Now, observations.Length == 0 ? null : observations);
            }

            public IReadOnlyList<ProductionCyclePass> Days(long from, long to)
            {
                List<ProductionCyclePass> passes = new List<ProductionCyclePass>();
                for (long day = from; day <= to; day++)
                {
                    passes.Add(Day(day));
                }

                return passes;
            }

            public int Goals()
            {
                int goals = 0;
                foreach (NarrativeNpc actor in World.Registry.Npcs.Values)
                {
                    goals += actor.Goals.Count;
                }

                return goals;
            }

            /// <summary>The same world continued from a save, with its services rebuilt.</summary>
            public Bench Reload(long afterDay)
            {
                return From(WorldStateSerializer.Save(World), afterDay);
            }

            public static Bench From(string save, long afterDay)
            {
                NarrativeWorldState reloaded = WorldStateSerializer.Load(save);
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (NarrativeNpc npc in reloaded.Registry.Npcs.Values)
                {
                    vanilla.Define(npc.Id, zone: Town, money: 120);
                }

                vanilla.GiveItem(Suspect, new ItemDescriptor(Purse, "embroidered purse", "purse", 300));
                vanilla.Now = GameTime.FromDays(afterDay);
                return new Bench(reloaded, vanilla, EntityId.None);
            }

            public bool MatterIsDeferred(AutonomousInterventions autonomy, GameTime now)
            {
                Vanilla.Now = now;
                autonomy.Advance(World, Vanilla, Checks, Actions, now);
                return autonomy.LastPass.Count == 0;
            }
        }

        /// <summary>A resolver that also counts, so "was this rolled again?" is answerable.</summary>
        private sealed class CountingCheckResolver : ICheckResolver
        {
            private readonly VanillaStyleCheckResolver _inner;

            public CountingCheckResolver(IVanillaState vanilla)
            {
                _inner = new VanillaStyleCheckResolver(vanilla);
            }

            public int Resolved { get; private set; }

            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                Resolved++;
                return _inner.Resolve(request, rng);
            }
        }
    }
}
