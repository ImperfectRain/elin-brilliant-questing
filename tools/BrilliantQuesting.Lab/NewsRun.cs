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
    /// BQ-036 with no game attached: two things happen in one town, gossip carries them unevenly,
    /// and then the player walks round asking people what has been going on.
    ///
    /// The point of the probe is the column of answers. Nobody was assigned a line; each person
    /// says what the circulation happened to leave in their head, so the tavern's answer and the
    /// answer three doors down are different reports of the same week. The player's journal at the
    /// end is the sum of what two of them said - not of what the town knows.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --news [seed]
    /// </summary>
    internal static class NewsRun
    {
        public static void Run(ulong seed, int days = 5, int townspeople = 12)
        {
            TheftLaboratory lab = TheftLaboratory.Create(seed);
            List<EntityId> town = new List<EntityId>();
            for (int i = 0; i < townspeople; i++)
            {
                EntityId id = lab.World.NewId("npc");
                lab.World.Registry.Add(new NarrativeNpc(id, "Townsperson " + i));
                lab.Vanilla.Define(id, level: 3, zone: lab.Zone);
                town.Add(id);
            }

            // A second development, so there is more than one thing to be unevenly informed about.
            EntityId sten = lab.World.NewId("npc");
            lab.World.Registry.Add(new NarrativeNpc(sten, "Sten"));
            lab.Vanilla.Define(sten, level: 3, zone: lab.Zone);
            EntityId debt = lab.World.NewId("fact");
            lab.World.Knowledge.AddFact(new Fact(debt, sten, FactPredicates.Owes, lab.Situation.VictimId, "80 orens"));
            lab.World.Knowledge.Teach(lab.Situation.VictimId, debt, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, false);

            Banner("A WEEK IN ONE TOWN, THEN ASKING AROUND (seed " + seed + ")");
            Console.WriteLine("  " + lab.World.Registry.NameOf(lab.Situation.ThiefId) + " stole from "
                              + lab.World.Registry.NameOf(lab.Situation.VictimId) + "; "
                              + lab.World.Registry.NameOf(lab.Situation.WitnessId) + " saw it.");
            Console.WriteLine("  Sten owes " + lab.World.Registry.NameOf(lab.Situation.VictimId)
                              + " 80 orens, and only " + lab.World.Registry.NameOf(lab.Situation.VictimId)
                              + " knows it first-hand.");
            Console.WriteLine("  " + days + " days of ordinary gossip follow. The player is told nothing.\n");

            lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            for (int day = 1; day <= days; day++)
            {
                lab.Vanilla.AdvanceDays(1);
                lab.Circulation.Run(lab.World, lab.Vanilla, lab.Vanilla.Now);
            }

            Banner("\"WHAT'S BEEN HAPPENING?\"");
            HashSet<string> answers = new HashSet<string>();
            List<EntityId> asked = new List<EntityId>();
            for (int i = 0; i < town.Count; i++)
            {
                IReadOnlyList<SpokenRemark> answer = lab.News.Ask(lab.World, lab.Vanilla, town[i]);
                Console.WriteLine(lab.World.Registry.NameOf(town[i]) + ":");
                if (answer.Count == 0)
                {
                    Console.WriteLine("    (nothing they have not already told you)");
                    continue;
                }

                List<string> facts = new List<string>();
                for (int r = 0; r < answer.Count; r++)
                {
                    Console.WriteLine("    \"" + answer[r].Line + "\"");
                    facts.Add(answer[r].FactId.Value);
                }

                answers.Add(string.Join("|", facts));
                asked.Add(town[i]);
            }

            Console.WriteLine();
            Console.WriteLine("  distinct answers in one town: " + answers.Count
                              + " (the same question, asked at the same minute)");

            // The two people who know the theft best will pass on the week's other gossip and say
            // nothing at all about the theft: one saw it, which is testimony, and the other did it.
            Console.WriteLine("  " + lab.World.Registry.NameOf(lab.Situation.WitnessId)
                              + " (who saw it) mentions the theft: " + MentionsTheTheft(lab, lab.Situation.WitnessId));
            Console.WriteLine("  " + lab.World.Registry.NameOf(lab.Situation.ThiefId)
                              + " (who did it) mentions the theft: " + MentionsTheTheft(lab, lab.Situation.ThiefId));

            Banner("AFTER LISTENING TO TWO OF THEM");
            int taken = 0;
            for (int i = 0; i < asked.Count && i < 2; i++)
            {
                foreach (SpokenRemark remark in lab.News.Ask(lab.World, lab.Vanilla, asked[i]))
                {
                    // The order the plugin uses: the line is said, and only then may it teach.
                    Console.WriteLine(remark.SpeakerName + ": \"" + remark.Line + "\"");
                    if (lab.News.Deliver(lab.World, lab.Vanilla, remark, lab.Vanilla.Now))
                    {
                        taken++;
                    }
                }
            }

            Console.WriteLine();
            Console.Write(NarrativeJournal.Describe(lab.World, lab.Player));
            Console.WriteLine();
            Console.WriteLine("  claims taken on: " + taken + ", provable: " + Provable(lab)
                              + " (being told a thing never makes it provable)");
        }

        /// <summary>Whether asking this person for the news gets the theft mentioned at all.</summary>
        private static bool MentionsTheTheft(TheftLaboratory lab, EntityId who)
        {
            foreach (SpokenRemark remark in lab.News.Ask(lab.World, lab.Vanilla, who))
            {
                if (remark.FactId == lab.Situation.TheftFactId)
                {
                    return true;
                }
            }

            return false;
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
