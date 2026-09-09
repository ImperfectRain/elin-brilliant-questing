using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    public enum SimulationTier { Active, Warm, Cold, Archived }

    /// <summary>
    /// Derived work queues. Historical records stay in EntityRegistry; ticks never enumerate them.
    /// Queue positions are transient budget choices. LastSimulatedAt remains the saved time authority.
    /// </summary>
    internal sealed class SimulationIndex
    {
        private readonly LinkedList<NarrativeNpc> _warm = new LinkedList<NarrativeNpc>();
        private readonly LinkedList<NarrativeNpc> _cold = new LinkedList<NarrativeNpc>();
        private readonly Dictionary<EntityId, LinkedListNode<NarrativeNpc>> _nodes = new Dictionary<EntityId, LinkedListNode<NarrativeNpc>>();

        public void Update(NarrativeNpc npc)
        {
            SimulationTier tier = npc.BackgroundTier;
            LinkedList<NarrativeNpc> target = tier == SimulationTier.Warm ? _warm : tier == SimulationTier.Cold ? _cold : null;
            if (_nodes.TryGetValue(npc.Id, out var node))
            {
                if (node.List == target && ReferenceEquals(node.Value, npc)) return;
                node.List.Remove(node);
                _nodes.Remove(npc.Id);
            }
            if (target != null) _nodes[npc.Id] = target.AddLast(npc);
        }

        public List<NarrativeNpc> Take(int warmBudget, int coldBudget)
        {
            var result = new List<NarrativeNpc>();
            Take(_warm, warmBudget, result);
            Take(_cold, coldBudget, result);
            return result;
        }

        private static void Take(LinkedList<NarrativeNpc> queue, int budget, List<NarrativeNpc> result)
        {
            int count = Math.Min(Math.Max(0, budget), queue.Count);
            for (int i = 0; i < count; i++)
            {
                var node = queue.First;
                result.Add(node.Value);
                queue.RemoveFirst();
                queue.AddLast(node);
            }
        }
    }
}
