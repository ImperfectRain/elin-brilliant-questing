using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-094 with no game attached: the player hears about a shortage, does nothing about it, and
    /// comes back to find that somebody else has dealt with it.
    ///
    /// The run exists to be read. It prints who the world weighed and why, what their activity was
    /// worth as an opening, which routes could have ended the matter and which were left alone,
    /// the attempt itself, and then the two things the roadmap actually asks for: that the matter
    /// ended without the player, and that the player can find out how without being handed it.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run autonomy [seed]
    /// </summary>
    internal static class AutonomyRun
    {
        public const ulong DefaultSeed = 94UL;

        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Marla = EntityId.Parse("npc_marla");
        private static readonly EntityId Bram = EntityId.Parse("npc_bram");
        private static readonly EntityId Sella = EntityId.Parse("npc_sella");
        private static readonly EntityId Town = EntityId.Parse("zone_rillford");

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);

            Banner(output, "A SHORTAGE THE PLAYER LEFT ALONE (seed " + seed + ")");
            output.WriteLine("  Marla has run short of food. The player heard about it five days ago and");
            output.WriteLine("  has done nothing since. Bram farms next door, has money and a full pantry,");
            output.WriteLine("  and has said he means to see her fed. Nobody has asked him to.\n");

            Banner(output, "1. WHAT THE PLAYER KNOWS, AND HAS DONE");
            output.Write(Indent(NarrativeJournal.Describe(bench.World, Player)));
            output.WriteLine("  acts by the player inside this matter: " + PlayerActs(bench));

            // The whole of the new behaviour: time moved, so the world got a turn.
            Banner(output, "2. TIME MOVES, AND THE WORLD GETS A TURN");
            int acted = bench.Autonomy.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Vanilla.Now);
            output.WriteLine("  matters taken up this pass: " + acted + "\n");
            foreach (InterventionTrace trace in bench.Autonomy.LastPass)
            {
                output.Write(Indent(NarrativeInspector.DescribeIntervention(bench.World, trace)));
            }

            Banner(output, "3. WHAT IS IN HISTORY, AND WHAT IS NOT");
            foreach (WorldEvent recorded in bench.World.Ledger.Events)
            {
                output.WriteLine("  " + recorded.Type + " by " + bench.World.Registry.NameOf(recorded.Actor)
                                 + " (" + recorded.Witnesses.Count + " recorded as having seen it)");
            }

            output.WriteLine("  and nothing about where anybody was, what their timetable said, or what");
            output.WriteLine("  vanilla had them doing. That was read, weighed, and thrown away.\n");

            Banner(output, "4. WHAT THE PLAYER IS TOLD (NOTHING)");
            EntityId claim = SettledClaim(bench);
            output.WriteLine("  Bram can say he settled it:   " + bench.World.Knowledge.Knows(Bram, claim));
            output.WriteLine("  the player has heard it:      " + bench.World.Knowledge.Knows(Player, claim));
            output.WriteLine("  their chronicle of it:        " + Count(Chronicle.Entries(bench.World, Player).Count));

            Banner(output, "5. AND THEN SOMEBODY MENTIONS IT");
            RumorSystem rumours = new RumorSystem(bench.World.Knowledge, bench.World.Ledger, bench.World.Ids);
            bool told = rumours.Tell(Bram, Sella, claim, bench.Vanilla.Now);
            bool passedOn = rumours.Tell(Sella, Player, claim, bench.Vanilla.Now);
            output.WriteLine("  Bram tells Sella:             " + told);
            output.WriteLine("  Sella passes it on to them:   " + passedOn);
            output.WriteLine();
            output.Write(Indent(ChronicleNarrative.Export(bench.World, Player, bench.Vanilla.Now)));
        }

        private static EntityId SettledClaim(Bench bench)
        {
            foreach (Fact fact in bench.World.Knowledge.Facts.Values)
            {
                if (fact.Predicate == FactPredicates.Settled)
                {
                    return fact.Id;
                }
            }

            return EntityId.None;
        }

        private static string PlayerActs(Bench bench)
        {
            int acts = 0;
            foreach (WorldEvent recorded in bench.World.Ledger.Events)
            {
                if (recorded.Actor == Player && bench.Thread.IsNamedBy(recorded))
                {
                    acts++;
                }
            }

            return acts == 0 ? "none, which is what makes it anybody else's" : acts.ToString();
        }

        private static string Count(int entries)
        {
            return entries == 0 ? "empty, because nobody has told them anything" : entries + " entries";
        }

        private static string Indent(string block)
        {
            return "  " + block.TrimEnd('\n').Replace("\n", "\n  ") + "\n";
        }

        private static void Banner(TextWriter output, string title)
        {
            output.WriteLine();
            output.WriteLine("== " + title + " " + new string('=', Math.Max(3, 68 - title.Length)));
        }

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla, NarrativeThread thread)
            {
                World = world;
                Vanilla = vanilla;
                Thread = thread;
                Checks = new VanillaStyleCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
                Autonomy = new AutonomousInterventions();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ICheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public AutonomousInterventions Autonomy { get; }

            public NarrativeThread Thread { get; }

            public static Bench Create(ulong seed)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeNpc(Player, "the player"));
                world.Registry.Add(new NarrativeNpc(Marla, "Marla") { Occupation = "weaver" });
                NarrativeNpc bram = world.Registry.Add(new NarrativeNpc(Bram, "Bram") { Occupation = "farmer" });
                world.Registry.Add(new NarrativeNpc(Sella, "Sella") { Occupation = "baker" });

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Player, Marla, Bram, Sella })
                {
                    vanilla.Define(who, zone: Town);
                }

                vanilla.Define(Bram, zone: Town, money: 400);
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);
                for (int i = 0; i < 4; i++)
                {
                    vanilla.GiveItem(Bram, new ItemDescriptor(
                        EntityId.Parse("item_grain_" + i), "a sack of grain", "grain", 30, "grain"));
                }

                GameTime start = vanilla.Now;
                Fact need = new Fact(world.NewId("fact"), Marla, FactPredicates.Needs, EntityId.None, "food");
                world.Knowledge.AddFact(need);
                world.Knowledge.Teach(Marla, need.Id, KnowledgeSource.Participant, 1.0, start, false);
                world.Knowledge.Teach(Bram, need.Id, KnowledgeSource.Hearsay, 0.9, start, false);
                world.Knowledge.Teach(Player, need.Id, KnowledgeSource.Hearsay, 0.8, start, false);

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "shortage", start)
                {
                    State = ThreadState.Active,
                    Tension = 30
                };
                thread.ParticipantIds.Add(Marla);
                thread.ParticipantIds.Add(Bram);
                thread.FactIds.Add(need.Id);
                thread.OpenQuestions.Add("Who is going to see Marla fed?");
                world.Threads.Add(thread);

                bram.Goals.Add(new NpcGoal("see_marla_fed", Marla, 80, "she kept his herd alive one winter"));
                bram.ProblemSolving.PaySomeone = 0.9;

                new ConsequenceEngine(world, vanilla).Attach();
                vanilla.AdvanceDays(5);
                return new Bench(world, vanilla, thread);
            }
        }
    }
}
