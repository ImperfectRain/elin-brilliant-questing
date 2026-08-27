using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Diagnostics
{
    /// <summary>
    /// The "why?" tooling the design document treats as non-negotiable.
    ///
    /// A procedural system that cannot explain itself cannot be debugged, tuned or trusted. Every
    /// question a developer will actually ask - why is this option here, why did that NPC react
    /// like that, why does this thread exist - should be answerable from these dumps without
    /// re-running the world.
    /// </summary>
    public static class WorldInspector
    {
        /// <summary>Every option the registry considered, including the ones it rejected and why.</summary>
        public static string DescribeOptions(ActionRegistry registry, ActionContext context)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("options against ").Append(context.NameOf(context.Target)).Append('\n');

            List<ActionOffer> offers = registry.Discover(context, includeUnavailable: true);
            foreach (ActionOffer offer in offers)
            {
                sb.Append(offer.Availability.IsAvailable ? "  [x] " : "  [ ] ");
                sb.Append(offer.Action.Id.PadRight(14));
                sb.Append(offer.Action.Family.ToString().PadRight(14));
                if (!string.IsNullOrEmpty(offer.Availability.Reason))
                {
                    sb.Append("- ").Append(offer.Availability.Reason);
                }

                sb.Append('\n');
            }

            HashSet<ActionFamily> families = registry.AvailableFamilies(context);
            sb.Append("  solution families open: ").Append(families.Count).Append('\n');
            return sb.ToString();
        }

        public static string DescribeCharacter(NarrativeWorldState world, IVanillaState vanilla, EntityId id)
        {
            NarrativeNpc npc = world.Registry.GetNpc(id);
            if (npc == null)
            {
                return id + " is not a known character.\n";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append(npc.Name).Append(" (").Append(npc.Occupation).Append(", ").Append(npc.Importance).Append(")\n");
            sb.Append("  alive: ").Append(vanilla.IsAlive(id)).Append("   affinity to player: ").Append(vanilla.GetAffinity(id)).Append('\n');

            sb.Append("  goals:");
            if (npc.Goals.Count == 0)
            {
                sb.Append(" none");
            }

            foreach (Goal goal in npc.Goals)
            {
                sb.Append(' ').Append(goal);
            }

            sb.Append('\n');

            sb.Append("  believes:\n");
            foreach (KnowledgeRecord belief in world.Knowledge.BeliefsOf(id))
            {
                Fact fact = world.Knowledge.GetFact(belief.FactId);
                sb.Append("    ").Append(Render(world, fact));
                sb.Append("  [").Append(belief.Source).Append(", confidence ").Append(belief.Confidence.ToString("0.00"));
                sb.Append(belief.CanProve ? ", can prove]" : ", cannot prove]").Append('\n');
            }

            sb.Append("  remembers about the player:\n");
            foreach (MemoryRecord memory in world.Memories.MemoriesAbout(id, vanilla.PlayerId))
            {
                sb.Append("    ").Append(memory.SummaryTag);
                if (memory.Occurrences > 1)
                {
                    sb.Append(" x").Append(memory.Occurrences);
                }

                sb.Append(" (").Append(memory.Weight).Append(", ").Append(memory.AffinityContribution).Append(" affinity)\n");
            }

            IReadOnlyList<RelationshipEdge> edges = world.Relationships.EdgesOf(id);
            if (edges.Count > 0)
            {
                sb.Append("  ties:");
                for (int i = 0; i < edges.Count; i++)
                {
                    sb.Append(' ').Append(world.Registry.NameOf(edges[i].To)).Append('(').Append(edges[i].Kind).Append(' ').Append(edges[i].Sentiment).Append(')');
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        public static string DescribeThread(NarrativeWorldState world, NarrativeThread thread)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("thread ").Append(thread.ArchetypeId).Append(" [").Append(thread.State).Append(", tension ").Append(thread.Tension).Append("]\n");
            if (!string.IsNullOrEmpty(thread.Resolution))
            {
                sb.Append("  resolution: ").Append(thread.Resolution).Append('\n');
            }

            sb.Append("  participants:");
            foreach (EntityId participant in thread.ParticipantIds)
            {
                sb.Append(' ').Append(world.Registry.NameOf(participant));
            }

            sb.Append('\n');

            sb.Append("  open questions:\n");
            foreach (string question in thread.OpenQuestions)
            {
                sb.Append("    - ").Append(question).Append('\n');
            }

            sb.Append("  escalation:\n");
            foreach (EscalationStep step in thread.Escalation)
            {
                sb.Append(thread.CompletedSteps.Contains(step.Id) ? "    [done] " : "    [    ] ");
                sb.Append("day +").Append(step.DayOffset).Append("  ").Append(step.Description).Append('\n');
            }

            return sb.ToString();
        }

        /// <summary>Who currently knows a fact, and which of them could demonstrate it.</summary>
        public static string DescribeFactSpread(NarrativeWorldState world, EntityId factId)
        {
            Fact fact = world.Knowledge.GetFact(factId);
            StringBuilder sb = new StringBuilder();
            sb.Append("fact: ").Append(Render(world, fact)).Append('\n');
            foreach (EntityId knower in world.Knowledge.Knowers(factId))
            {
                world.Knowledge.TryGetBelief(knower, factId, out KnowledgeRecord belief);
                sb.Append("  ").Append(world.Registry.NameOf(knower).PadRight(12));
                sb.Append(belief.Source.ToString().PadRight(12));
                sb.Append("confidence ").Append(belief.Confidence.ToString("0.00"));
                sb.Append(belief.CanProve ? "  (can prove)" : "  (cannot prove)").Append('\n');
            }

            return sb.ToString();
        }

        public static string DescribeHistory(NarrativeWorldState world, int limit = 20)
        {
            StringBuilder sb = new StringBuilder();
            IReadOnlyList<Events.WorldEvent> events = world.Ledger.Events;
            int start = events.Count > limit ? events.Count - limit : 0;
            for (int i = start; i < events.Count; i++)
            {
                Events.WorldEvent worldEvent = events[i];
                sb.Append(worldEvent.Time).Append("  ").Append(worldEvent.Type.ToString().PadRight(18));
                sb.Append(world.Registry.NameOf(worldEvent.Actor));
                if (!worldEvent.Target.IsNone)
                {
                    sb.Append(" -> ").Append(world.Registry.NameOf(worldEvent.Target));
                }

                if (worldEvent.Witnesses.Count > 0)
                {
                    sb.Append("  (seen by ").Append(worldEvent.Witnesses.Count).Append(')');
                }

                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string Render(NarrativeWorldState world, Fact fact)
        {
            if (fact == null)
            {
                return "(missing fact)";
            }

            string subject = world.Registry.NameOf(fact.Subject);
            string obj = world.Registry.Npcs.ContainsKey(fact.Object)
                ? world.Registry.NameOf(fact.Object)
                : !string.IsNullOrEmpty(fact.Value) ? fact.Value : fact.Object.Value;
            string truth = fact.Truth == Knowledge.TruthState.True ? string.Empty : " (" + fact.Truth + "!)";
            return subject + " " + fact.Predicate.Replace('_', ' ') + " " + obj + truth;
        }
    }
}
