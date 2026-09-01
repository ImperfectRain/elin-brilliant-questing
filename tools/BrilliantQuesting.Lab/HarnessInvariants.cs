using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public static class HarnessInvariants
    {
        public static void Check(HarnessState state, HarnessRunResult result, Dictionary<EntityId, ThreadState> priorStates)
        {
            HashSet<EntityId> eventIds = new HashSet<EntityId>();
            foreach (WorldEvent evt in state.World.Ledger.Events)
            {
                if (!eventIds.Add(evt.Id))
                {
                    result.Failures.Add("duplicate event id " + evt.Id);
                }

                for (int i = 0; i < evt.Witnesses.Count; i++)
                {
                    if (evt.Witnesses[i].IsNone || evt.Witnesses[i] == evt.Actor || evt.Witnesses[i] == evt.Target)
                    {
                        result.Failures.Add("event " + evt.Id + " has invalid unrelated witness " + evt.Witnesses[i]);
                    }
                }

                for (int i = 0; i < evt.Related.Count; i++)
                {
                    EntityId related = evt.Related[i];
                    if (related.Kind == "fact" && state.World.Knowledge.GetFact(related) == null)
                    {
                        result.Failures.Add("event " + evt.Id + " names missing related fact " + related);
                    }
                }
            }

            foreach (NarrativeThread thread in state.World.Threads)
            {
                if (priorStates.TryGetValue(thread.Id, out ThreadState previous)
                    && previous == ThreadState.Resolved
                    && thread.State != ThreadState.Resolved)
                {
                    result.Failures.Add("resolved thread became live again: " + thread.Id);
                }

                for (int i = 0; i < thread.FactIds.Count; i++)
                {
                    Fact fact = state.World.Knowledge.GetFact(thread.FactIds[i]);
                    if (fact == null)
                    {
                        result.Failures.Add("thread " + thread.Id + " names missing fact " + thread.FactIds[i]);
                    }
                }

                priorStates[thread.Id] = thread.State;
            }

            foreach (ProductionSystemDescriptor descriptor in ProductionSystemRegistry.Descriptors())
            {
                if (!result.Coverage.Entries.ContainsKey(descriptor.Id))
                {
                    result.Failures.Add("production system missing from coverage report: " + descriptor.Id);
                }
            }
        }
    }
}
