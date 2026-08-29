using System;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// Runs the three-NPC laboratory and prints what happened, with the reasoning attached.
    ///
    /// This is the project's Gate B evidence: the same generated situation, played two ways, with
    /// every option, roll and consequence explained. It needs no game process and no assets -
    /// which is the point of keeping the simulation free of Elin references.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab [seed]
    /// </summary>
    public static class Program
    {
        /// <summary>Chosen with --find-seed so the demo shows a run that actually goes somewhere.</summary>
        private const ulong DefaultSeed = 15UL;

        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--find-seed")
            {
                SeedProbe.Run();
                return 0;
            }

            if (args.Length > 0 && args[0] == "--questline-sweep")
            {
                Questline.Sweep(args.Length > 1 && int.TryParse(args[1], out int count) ? count : 50);
                return 0;
            }

            if (args.Length > 0 && args[0] == "--ambient")
            {
                ulong ambientSeed = args.Length > 1 && ulong.TryParse(args[1], out ulong chosen) ? chosen : DefaultSeed;
                AmbientRun.Run(ambientSeed);
                return 0;
            }

            if (args.Length > 0 && args[0] == "--questline")
            {
                ulong questSeed = args.Length > 1 && ulong.TryParse(args[1], out ulong given) ? given : DefaultSeed;
                Questline.Run(questSeed);
                return 0;
            }

            ulong seed = args.Length > 0 && ulong.TryParse(args[0], out ulong parsed) ? parsed : DefaultSeed;

            Console.WriteLine("Elin Brilliant Questing - three-NPC laboratory");
            Console.WriteLine("seed " + seed);

            ShowGeneratedSituation(seed);
            ShowAvailableRoutes(seed);
            PlayInvestigativeRoute(seed);
            PlayIgnoredRoute(seed);
            ShowPersistence(seed);
            return 0;
        }

        private static void ShowGeneratedSituation(ulong seed)
        {
            Header("what the world generated");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            Console.WriteLine("cause: " + lab.World.Registry.NameOf(lab.Situation.ThiefId)
                              + " took something from " + lab.World.Registry.NameOf(lab.Situation.VictimId)
                              + ", and " + lab.World.Registry.NameOf(lab.Situation.WitnessId) + " saw it.\n");

            Console.Write(NarrativeInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));
        }

        private static void ShowAvailableRoutes(ulong seed)
        {
            Header("what the player may attempt, and why not");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            foreach (EntityId target in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
            {
                ActionContext context = lab.Context(target);
                context.SubjectFact = lab.Situation.TheftFactId;
                Console.Write(NarrativeInspector.DescribeOptions(lab.Actions, context));
                Console.WriteLine();
            }
        }

        private static void PlayInvestigativeRoute(ulong seed)
        {
            Header("playthrough A - ask, take, return");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            Step(lab, "question", lab.Situation.WitnessId);
            Step(lab, "pickpocket", lab.Situation.ThiefId);
            Step(lab, "return_item", lab.Situation.VictimId);

            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeCharacter(lab.World, lab.Vanilla, lab.Situation.VictimId));
            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));

            Console.WriteLine("ten more days pass...");
            lab.AdvanceDays(10);
            Console.WriteLine("thread state: " + lab.Situation.Thread.State + " (" + (lab.Situation.Thread.Resolution ?? "open") + ")");
        }

        private static void PlayIgnoredRoute(ulong seed)
        {
            Header("playthrough B - the player never turns up");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            for (int day = 0; day < 16; day += 2)
            {
                lab.AdvanceDays(2);
                foreach (string applied in lab.Threads.LastApplied)
                {
                    Console.WriteLine("  day " + lab.Vanilla.Now.TotalDays + ": " + applied);
                }
            }

            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));
            Console.WriteLine("history:");
            Console.Write(NarrativeInspector.DescribeHistory(lab.World));
        }

        private static void ShowPersistence(ulong seed)
        {
            Header("save, reload, continue");
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            lab.Perform("question", lab.Situation.WitnessId);
            lab.AdvanceDays(8);

            string json = WorldStateSerializer.Save(lab.World);
            var reloaded = WorldStateSerializer.Load(json);

            Console.WriteLine("save is " + json.Length + " characters of readable JSON");
            Console.WriteLine("events " + lab.World.Ledger.Count + " -> " + reloaded.Ledger.Count);
            Console.WriteLine("facts  " + lab.World.Knowledge.Facts.Count + " -> " + reloaded.Knowledge.Facts.Count);
            Console.WriteLine("people " + lab.World.Registry.Npcs.Count + " -> " + reloaded.Registry.Npcs.Count);
            Console.WriteLine("thread " + lab.Situation.Thread.State + " -> " + reloaded.Threads[0].State);
        }

        private static void Step(TheftLaboratory lab, string actionId, EntityId target)
        {
            Console.WriteLine("> " + actionId + " " + lab.World.Registry.NameOf(target));
            Console.WriteLine("  " + lab.Perform(actionId, target).Explain().Replace("\n", "\n  "));
        }

        private static void Header(string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine(title.ToUpperInvariant());
            Console.WriteLine(new string('=', 78));
        }
    }
}
