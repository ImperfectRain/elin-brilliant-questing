using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The three-NPC theft laboratory: the same generated situation, played two ways, with every
    /// option, roll and consequence explained, then saved and reloaded.
    ///
    /// This is the project's Gate B evidence. It needs no game process and no assets - which is the
    /// point of keeping the simulation free of Elin references.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run theft --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- 15
    /// </summary>
    internal sealed class TheftLaboratoryScenario : LabScenario
    {
        public override string Id => "theft";

        public override string Summary => "three-NPC theft laboratory, played two ways, with its reasoning shown";

        public override string Description =>
            "Generates one small situation - A stole something from B, C saw it - and plays it twice:\n"
            + "once with a player who investigates, once with a player who never turns up. Prints the\n"
            + "options the world offers and why the rest are refused, then saves and reloads the world\n"
            + "to show the ledger and the thread surviving the round trip.\n"
            + "Runs when the laboratory is invoked with no command, which is why a bare seed works.";

        public override int Run(LabRunContext context)
        {
            ulong seed = context.Seed;
            TextWriter output = context.Output;

            output.WriteLine("Elin Brilliant Questing - three-NPC laboratory");
            output.WriteLine("seed " + seed);

            ShowGeneratedSituation(output, seed);
            ShowAvailableRoutes(output, seed);
            PlayInvestigativeRoute(output, seed);
            PlayIgnoredRoute(output, seed);
            ShowPersistence(output, seed);
            return LabExit.Success;
        }

        private static void ShowGeneratedSituation(TextWriter output, ulong seed)
        {
            LabText.Header(output, "what the world generated");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            output.WriteLine("cause: " + lab.World.Registry.NameOf(lab.Situation.ThiefId)
                             + " took something from " + lab.World.Registry.NameOf(lab.Situation.VictimId)
                             + ", and " + lab.World.Registry.NameOf(lab.Situation.WitnessId) + " saw it.\n");

            output.Write(NarrativeInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
            output.WriteLine();
            output.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));
        }

        private static void ShowAvailableRoutes(TextWriter output, ulong seed)
        {
            LabText.Header(output, "what the player may attempt, and why not");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            foreach (EntityId target in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
            {
                ActionContext actionContext = lab.Context(target);
                actionContext.SubjectFact = lab.Situation.TheftFactId;
                output.Write(NarrativeInspector.DescribeOptions(lab.Actions, actionContext));
                output.WriteLine();
            }
        }

        private static void PlayInvestigativeRoute(TextWriter output, ulong seed)
        {
            LabText.Header(output, "playthrough A - ask, take, return");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            Step(output, lab, "question", lab.Situation.WitnessId);
            Step(output, lab, "pickpocket", lab.Situation.ThiefId);
            Step(output, lab, "return_item", lab.Situation.VictimId);

            output.WriteLine();
            output.Write(NarrativeInspector.DescribeCharacter(lab.World, lab.Vanilla, lab.Situation.VictimId));
            output.WriteLine();
            output.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));

            output.WriteLine("ten more days pass...");
            lab.AdvanceDays(10);
            output.WriteLine("thread state: " + lab.Situation.Thread.State + " (" + (lab.Situation.Thread.Resolution ?? "open") + ")");
        }

        private static void PlayIgnoredRoute(TextWriter output, ulong seed)
        {
            LabText.Header(output, "playthrough B - the player never turns up");
            TheftLaboratory lab = TheftLaboratory.Create(seed);

            for (int day = 0; day < 16; day += 2)
            {
                lab.AdvanceDays(2);
                foreach (string applied in lab.Threads.LastApplied)
                {
                    output.WriteLine("  day " + lab.Vanilla.Now.TotalDays + ": " + applied);
                }
            }

            output.WriteLine();
            output.Write(NarrativeInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
            output.WriteLine();
            output.Write(NarrativeInspector.DescribeThread(lab.World, lab.Situation.Thread));
            output.WriteLine("history:");
            output.Write(NarrativeInspector.DescribeHistory(lab.World));
        }

        private static void ShowPersistence(TextWriter output, ulong seed)
        {
            LabText.Header(output, "save, reload, continue");
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            lab.Perform("question", lab.Situation.WitnessId);
            lab.AdvanceDays(8);

            string json = WorldStateSerializer.Save(lab.World);
            var reloaded = WorldStateSerializer.Load(json);

            output.WriteLine("save is " + json.Length + " characters of readable JSON");
            output.WriteLine("events " + lab.World.Ledger.Count + " -> " + reloaded.Ledger.Count);
            output.WriteLine("facts  " + lab.World.Knowledge.Facts.Count + " -> " + reloaded.Knowledge.Facts.Count);
            output.WriteLine("people " + lab.World.Registry.Npcs.Count + " -> " + reloaded.Registry.Npcs.Count);
            output.WriteLine("thread " + lab.Situation.Thread.State + " -> " + reloaded.Threads[0].State);
        }

        private static void Step(TextWriter output, TheftLaboratory lab, string actionId, EntityId target)
        {
            output.WriteLine("> " + actionId + " " + lab.World.Registry.NameOf(target));
            output.WriteLine("  " + lab.Perform(actionId, target).Explain().Replace("\n", "\n  "));
        }
    }
}
