using System;
using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    /// <summary>
    /// BQ-035 end to end, with no game and nothing scripted: one person watches a theft, the town
    /// talks for a fortnight, and a player who asks nobody anything and opens no menu still finds
    /// out that something happened.
    ///
    /// The player here does exactly one thing - stand in the market and let time pass. Every line
    /// under "heard" is somebody speaking within earshot; nothing in this probe announces a
    /// situation, names a thread or offers an objective. What the journal holds at the end is the
    /// whole of what the player has to go on.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --ambient [seed]
    /// </summary>
    internal static class AmbientRun
    {
        /// <summary>How often the player's day is sampled. Three hours is a walk across town.</summary>
        private const int HoursPerStep = 3;

        public static void Run(ulong seed, int days = 14, int bystanders = 12)
        {
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            for (int i = 0; i < bystanders; i++)
            {
                EntityId id = lab.World.NewId("npc");
                lab.World.Registry.Add(new NarrativeNpc(id, "Townsperson " + i));
                lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
            }

            Banner("A TOWN, A THEFT, AND A PLAYER WHO ASKS NOBODY ANYTHING (seed " + seed + ")");
            Console.WriteLine("  " + lab.World.Registry.NameOf(lab.Situation.ThiefId) + " stole from "
                              + lab.World.Registry.NameOf(lab.Situation.VictimId) + "; "
                              + lab.World.Registry.NameOf(lab.Situation.WitnessId) + " saw it.");
            Console.WriteLine("  " + bystanders + " other people are standing about. The player knows nothing,");
            Console.WriteLine("  opens no menu, and speaks to no one.\n");

            // Establishes today. The scheduler refuses to back-date, so nothing is owed yet.
            lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);

            List<string> heard = new List<string>();
            for (int day = 1; day <= days; day++)
            {
                for (int step = 0; step < 24 / HoursPerStep; step++)
                {
                    lab.Vanilla.Now = lab.Vanilla.Now.PlusHours(HoursPerStep);
                    lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);

                    AmbientRemark remark = lab.Ambient.Next(lab.World, lab.Vanilla, lab.Vanilla.Now);
                    if (remark == null)
                    {
                        continue;
                    }

                    // The order the plugin uses, and the reason this is safe: the line happens
                    // first, and only a line the player heard is allowed to teach them anything.
                    string line = remark.SpeakerName + ": \"" + remark.Line + "\"";
                    bool took = lab.Ambient.Deliver(lab.World, lab.Vanilla, remark, lab.Vanilla.Now);
                    heard.Add("day " + day + "  " + line + (took ? string.Empty : "   (it did not take)"));
                    Console.WriteLine("day " + day + " " + lab.Vanilla.Now.Hour.ToString("00") + ":00  " + line);
                }
            }

            if (heard.Count == 0)
            {
                Console.WriteLine("  nobody said anything in " + days + " days.");
            }

            Banner("WHAT THE PLAYER ENDED UP KNOWING");
            Console.Write(NarrativeJournal.Describe(lab.World, lab.Player));
            Console.WriteLine();
            Console.WriteLine("  remarks overheard: " + heard.Count);
            Console.WriteLine("  provable: " + Provable(lab) + " (hearing about a thing never makes it provable)");
            Console.WriteLine();
            Console.Write(NarrativeInspector.DescribeFactSpread(lab.World, lab.Situation.TheftFactId));
        }

        private static int Provable(TheftLaboratory lab)
        {
            int provable = 0;
            foreach (JournalEntry entry in NarrativeJournal.Entries(lab.World, lab.Player))
            {
                if (entry.CanProve)
                {
                    provable++;
                }
            }

            return provable;
        }

        private static void Banner(string title)
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 78));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 78));
        }
    }
}
