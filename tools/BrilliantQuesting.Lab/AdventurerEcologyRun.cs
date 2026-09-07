using System;
using System.IO;
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
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-096 with no game attached: an adventuring party hears about a rescue the player has
    /// left alone, attempts it through the shared rescue verb, and leaves a claim the player can
    /// discover later.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run adventurer-ecology [seed]
    /// </summary>
    internal static class AdventurerEcologyRun
    {
        public const ulong DefaultSeed = 96UL;

        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Brewer = EntityId.Parse("npc_brewer");
        private static readonly EntityId Captor = EntityId.Parse("npc_captor");
        private static readonly EntityId Leader = EntityId.Parse("npc_adventurer_leader");
        private static readonly EntityId Scout = EntityId.Parse("npc_adventurer_scout");
        private static readonly EntityId Party = EntityId.Parse("org_bracken_knives");
        private static readonly EntityId Town = EntityId.Parse("zone_cordwall");
        private static readonly EntityId Mine = EntityId.Parse("zone_old_mine");

        public static void Run(TextWriter output, ulong seed)
        {
            Bench bench = Bench.Create(seed);

            Banner(output, "A RESCUE THE PLAYER DECLINED (seed " + seed + ")");
            output.WriteLine("  Ordel the brewer was taken near an old mine. The player heard about it");
            output.WriteLine("  four days ago and has done nothing. The Bracken Knives heard the same");
            output.WriteLine("  claim and hold a rescue goal for the missing brewer.\n");

            Banner(output, "1. BEFORE THE PARTY GOES");
            output.Write(Indent(NarrativeJournal.Describe(bench.World, Player)));
            output.WriteLine("  player acts inside this matter: " + PlayerActs(bench));
            output.WriteLine("  party goal: " + bench.Party.Goals[0]);

            Banner(output, "2. THE ADVENTURING PARTY TAKES IT UP");
            int acted = bench.Ecology.Advance(bench.World, bench.Vanilla, bench.Checks, bench.Actions, bench.Vanilla.Now);
            output.WriteLine("  party attempts this pass: " + acted + "\n");
            foreach (AdventurerEcologyTrace trace in bench.Ecology.LastPass)
            {
                output.Write(Indent(NarrativeInspector.DescribeAdventurerEcology(bench.World, trace)));
            }

            Banner(output, "3. WHAT HISTORY RECORDED");
            foreach (WorldEvent recorded in bench.World.Ledger.Events)
            {
                output.WriteLine("  " + recorded.Type + " by " + bench.World.Registry.NameOf(recorded.Actor)
                                 + " -> " + bench.World.Registry.NameOf(recorded.Target)
                                 + " (" + recorded.Witnesses.Count + " recorded witnesses)");
            }

            output.WriteLine("  No travel route, path or exact position was written; the rescue verb");
            output.WriteLine("  resolved coarsely and the party record says whose banner it happened under.\n");

            Banner(output, "4. THE RESULT IS NEWS, NOT OMNISCIENCE");
            AdventurerEcologyTrace last = bench.Ecology.LastPass.Single();
            output.WriteLine("  party can report the attempt: " + bench.World.Knowledge.Knows(Leader, last.AttemptFactId));
            output.WriteLine("  player has heard the attempt: " + bench.World.Knowledge.Knows(Player, last.AttemptFactId));
            output.WriteLine("  player chronicle entries:     " + Chronicle.Entries(bench.World, Player).Count);

            Banner(output, "5. AND THEN THE PARTY TELLS THEM");
            RumorSystem rumors = new RumorSystem(bench.World.Knowledge, bench.World.Ledger, bench.World.Ids);
            bool attemptTold = rumors.Tell(Leader, Player, last.AttemptFactId, bench.Vanilla.Now);
            bool endingTold = last.SettledFactId.IsNone || rumors.Tell(Leader, Player, last.SettledFactId, bench.Vanilla.Now);
            output.WriteLine("  attempt told: " + attemptTold);
            output.WriteLine("  ending told:  " + endingTold);
            output.WriteLine();
            output.Write(Indent(ChronicleNarrative.Export(bench.World, Player, bench.Vanilla.Now)));
        }

        private static string PlayerActs(Bench bench)
        {
            int acts = bench.World.Ledger.Events.Count(recorded =>
                recorded.Actor == Player && bench.Thread.IsNamedBy(recorded));

            return acts == 0 ? "none, which leaves it open to the world" : acts.ToString();
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
                Checks = new FixedRescueCheckResolver();
                Actions = StandardActions.CreateRegistry();
                Ecology = new AdventurerEcology();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public NarrativeThread Thread { get; }

            public ICheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public AdventurerEcology Ecology { get; }

            public Organization Party => World.Registry.GetOrganization(AdventurerEcologyRun.Party);

            public static Bench Create(ulong seed)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeNpc(Player, "the player"));
                world.Registry.Add(new NarrativeNpc(Brewer, "Ordel") { Occupation = "brewer" });
                world.Registry.Add(new NarrativeNpc(Captor, "Fenn") { Occupation = "bandit" });
                world.Registry.Add(new NarrativeNpc(Leader, "Nessa") { Occupation = "adventurer" });
                world.Registry.Add(new NarrativeNpc(Scout, "Tavin") { Occupation = "scout" });

                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (EntityId who in new[] { Player, Brewer, Captor, Leader, Scout })
                {
                    vanilla.Define(who, level: 8, zone: Town);
                }

                vanilla.SetSkill(Leader, VanillaSkill.Travel, 12);
                vanilla.SetAttribute(Leader, VanillaAttribute.Strength, 12);
                vanilla.SetAttribute(Leader, VanillaAttribute.Endurance, 12);

                Fact risk = new Fact(world.NewId("fact"), Brewer, FactPredicates.AtRisk, Captor, "abducted");
                world.Knowledge.AddFact(risk);
                world.Knowledge.Teach(Brewer, risk.Id, KnowledgeSource.Participant, 1.0, vanilla.Now, false);
                world.Knowledge.Teach(Player, risk.Id, KnowledgeSource.Hearsay, 0.8, vanilla.Now, false);
                world.Knowledge.Teach(Leader, risk.Id, KnowledgeSource.Hearsay, 0.9, vanilla.Now, false);
                world.Knowledge.Teach(Scout, risk.Id, KnowledgeSource.Hearsay, 0.9, vanilla.Now, false);

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "abduction", vanilla.Now)
                {
                    State = ThreadState.Active,
                    Tension = 55,
                    Importance = 60
                };
                thread.ParticipantIds.Add(Brewer);
                thread.ParticipantIds.Add(Captor);
                thread.SiteIds.Add(Mine);
                thread.FactIds.Add(risk.Id);
                thread.OpenQuestions.Add("Who brings Ordel back?");
                world.Threads.Add(thread);

                Organization party = new Organization(AdventurerEcologyRun.Party, "Bracken Knives", AdventurerEcology.PartyType)
                {
                    LeaderId = Leader,
                    Wealth = 35,
                    Legitimacy = 60,
                    Aggression = 45
                };
                party.MemberIds.Add(Leader);
                party.MemberIds.Add(Scout);
                party.SiteIds.Add(Town);
                party.Goals.Add(new OrganizationGoal(AdventurerEcology.RescueGoal, Brewer, 90));
                world.Registry.Add(party);
                world.Registry.GetNpc(Leader).OrganizationIds.Add(AdventurerEcologyRun.Party);
                world.Registry.GetNpc(Scout).OrganizationIds.Add(AdventurerEcologyRun.Party);

                vanilla.AdvanceDays(4);
                return new Bench(world, vanilla, thread);
            }
        }

        private sealed class FixedRescueCheckResolver : ICheckResolver
        {
            public CheckResult Resolve(CheckRequest request, DeterministicRng rng)
            {
                return new CheckResult(
                    request.Profile.Id,
                    request.Profile.BaseDifficulty,
                    new CheckTerm[0],
                    12,
                    15,
                    CheckOutcome.Pass);
            }
        }
    }
}
