using System;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public sealed class HarnessChronicle
    {
        private readonly bool _all;

        public HarnessChronicle(bool all)
        {
            _all = all;
        }

        public void Print(NarrativeWorldState world, int eventStart, int threadStart, int day)
        {
            if (world.Ledger.Count == eventStart && world.Threads.Count == threadStart)
            {
                return;
            }

            Console.WriteLine(day == 0 ? "OPENING" : "DAY " + day);
            for (int i = threadStart; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                Console.WriteLine("  [THREAD] " + Pretty(thread.ArchetypeId) + " " + thread.State + Site(world, thread));
            }

            int rumors = 0;
            for (int i = eventStart; i < world.Ledger.Count; i++)
            {
                WorldEvent evt = world.Ledger.Events[i];
                if (!_all && (evt.Type == WorldEventType.RumorSpread || evt.Type == WorldEventType.RumorDistorted))
                {
                    rumors++;
                    continue;
                }

                if (!_all && LowSignal(evt.Type))
                {
                    continue;
                }

                Console.WriteLine("  " + Describe(world, evt));
            }

            if (!_all && rumors > 0)
            {
                Console.WriteLine("  [RUMOR] " + rumors + " rumor event(s).");
            }
        }

        private static string Describe(NarrativeWorldState world, WorldEvent evt)
        {
            string actor = Name(world, evt.Actor);
            string target = Name(world, evt.Target);
            string zone = evt.Zone.IsNone ? string.Empty : " at " + Name(world, evt.Zone);
            switch (evt.Type)
            {
                case WorldEventType.Harmed:
                    return "[EVENT] " + actor + " is harmed" + HarmTarget(evt, target) + zone + ".";
                case WorldEventType.ThreadResolved:
                case WorldEventType.ThreadEscalated:
                case WorldEventType.ThreadReactivated:
                case WorldEventType.ThreadInherited:
                case WorldEventType.ThreadMerged:
                case WorldEventType.ThreadQuarantined:
                    return "[LIFECYCLE] " + Pretty(evt.Type.ToString()) + " " + ThreadName(world, evt) + zone + ".";
                default:
                    return "[EVENT] " + Pretty(evt.Type.ToString()) + " " + actor
                           + (evt.Target.IsNone ? string.Empty : " -> " + target)
                           + zone + ".";
            }
        }

        private static string HarmTarget(WorldEvent evt, string target)
        {
            if (evt.Target.IsNone)
            {
                return string.Empty;
            }

            return evt.Target == evt.Actor ? " by their own failed action" : " involving " + target;
        }

        private static bool LowSignal(WorldEventType type)
        {
            return type == WorldEventType.Met
                   || type == WorldEventType.Conversed
                   || type == WorldEventType.SecretLearned
                   || type == WorldEventType.CrimeWitnessed;
        }

        private static string Site(NarrativeWorldState world, NarrativeThread thread)
        {
            return thread.SiteIds.Count == 0 ? string.Empty : " at " + Name(world, thread.SiteIds[0]);
        }

        private static string ThreadName(NarrativeWorldState world, WorldEvent evt)
        {
            NarrativeThread thread = world.GetThread(evt.ThreadId);
            return thread == null ? evt.ThreadId.Value : Pretty(thread.ArchetypeId);
        }

        private static string Name(NarrativeWorldState world, EntityId id)
        {
            return id.IsNone ? "unknown" : world.Registry.NameOf(id);
        }

        private static string Pretty(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "Unknown";
            }

            string spaced = value.Replace('_', ' ');
            return char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }
    }
}
