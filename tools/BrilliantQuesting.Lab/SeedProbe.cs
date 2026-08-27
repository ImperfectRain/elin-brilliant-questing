using System;
using BrilliantQuesting.Situations;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// Finds a seed whose unscripted playthrough exercises the interesting path. Used once when
    /// choosing the demo default; kept because "which seed showed that bug" is a question the
    /// project will ask again.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --find-seed
    /// </summary>
    internal static class SeedProbe
    {
        public static void Run()
        {
            for (ulong seed = 1; seed < 400; seed++)
            {
                TheftLaboratory lab = TheftLaboratory.Create(seed);
                string question = lab.Perform("question", lab.Situation.WitnessId).Outcome.ToString();
                string theft = lab.Perform("pickpocket", lab.Situation.ThiefId).Outcome.ToString();
                bool returned = lab.Perform("return_item", lab.Situation.VictimId).Succeeded
                                && lab.Situation.Thread.Resolution == "property_returned";
                if (returned && question.Contains("Pass"))
                {
                    Console.WriteLine(seed + "  question=" + question + " pickpocket=" + theft + " returned=" + returned);
                }
            }
        }
    }
}
