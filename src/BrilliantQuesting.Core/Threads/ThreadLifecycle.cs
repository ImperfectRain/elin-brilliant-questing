using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// Keeps broken threads from continuing to behave as playable situations.
    ///
    /// This is deliberately a lifecycle pass over saved state, not a second situation resolver:
    /// the original thread stays in history, a successor carries inherited responsibility, and a
    /// malformed thread is quarantined with a reason instead of being repaired by guessing.
    /// </summary>
    public static class ThreadLifecycle
    {
        public const string InheritedTag = "thread_lifecycle:inherited";
        public const string QuarantinedTag = "thread_lifecycle:quarantined";
        public const string ReactivatedTag = "thread_lifecycle:reactivated";
        public const string MergedTag = "thread_lifecycle:merged";

        public static int Review(NarrativeWorldState world, IVanillaState vanilla, GameTime now)
        {
            if (world == null || vanilla == null)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < world.Threads.Count; i++)
            {
                NarrativeThread thread = world.Threads[i];
                if (thread == null || !thread.IsLive)
                {
                    continue;
                }

                string malformed = MalformedReason(world, thread);
                if (!string.IsNullOrEmpty(malformed))
                {
                    Quarantine(world, thread, now, malformed);
                    changed++;
                    continue;
                }

                ParticipantLifeSummary life = SummarizeParticipantLife(thread, vanilla);
                if (life.AliveCount > 0 || life.UnknownCount > 0)
                {
                    continue;
                }

                EntityId inheritor = FindInheritor(world, vanilla, thread);
                if (inheritor.IsNone)
                {
                    Quarantine(world, thread, now, "no living participant or inheritor remains");
                    changed++;
                    continue;
                }

                Inherit(world, thread, inheritor, now);
                changed++;
            }

            return changed;
        }

        public static bool Reactivate(NarrativeWorldState world, NarrativeThread thread, GameTime now, string reason)
        {
            if (world == null || thread == null || thread.State != ThreadState.Dormant)
            {
                return false;
            }

            thread.State = ThreadState.Active;
            thread.LastAdvancedAt = now;
            thread.LifecycleReason = string.IsNullOrEmpty(reason) ? "reactivated" : reason;
            world.Record(
                WorldEventType.ThreadReactivated,
                EntityId.None,
                EntityId.None,
                now,
                0.2,
                tags: new[] { ReactivatedTag, thread.LifecycleReason },
                threadId: thread.Id);
            return true;
        }

        public static bool Merge(NarrativeWorldState world, NarrativeThread target, NarrativeThread source, GameTime now, string reason)
        {
            if (world == null || target == null || source == null || target == source || !target.IsLive || !source.IsLive)
            {
                return false;
            }

            AddMissing(target.ParticipantIds, source.ParticipantIds);
            AddMissing(target.SiteIds, source.SiteIds);
            AddMissing(target.FactIds, source.FactIds);
            AddMissing(target.OpenQuestions, source.OpenQuestions);
            AddMissing(target.GenerationCauses, source.GenerationCauses);
            AddMissing(target.CompletedSteps, source.CompletedSteps);

            source.State = ThreadState.Inherited;
            source.SuccessorThreadId = target.Id;
            source.LastAdvancedAt = now;
            source.LifecycleReason = string.IsNullOrEmpty(reason) ? "merged into " + target.Id : reason;

            target.Tension = target.Tension > source.Tension ? target.Tension : source.Tension;
            target.Importance = target.Importance > source.Importance ? target.Importance : source.Importance;
            target.LastAdvancedAt = now;

            world.Record(
                WorldEventType.ThreadMerged,
                EntityId.None,
                EntityId.None,
                now,
                0.3,
                tags: new[] { MergedTag, "source:" + source.Id.Value, "target:" + target.Id.Value, source.LifecycleReason },
                threadId: source.Id);
            return true;
        }

        private static void Inherit(NarrativeWorldState world, NarrativeThread thread, EntityId inheritor, GameTime now)
        {
            NarrativeThread successor = new NarrativeThread(world.NewId("thread"), thread.ArchetypeId, now)
            {
                OriginEventId = thread.OriginEventId,
                ParentThreadId = thread.Id,
                LastAdvancedAt = now,
                Tension = thread.Tension,
                Importance = thread.Importance,
                State = ThreadState.Active,
                LifecycleReason = "inherited from " + thread.Id
            };

            successor.ParticipantIds.Add(inheritor);
            AddMissing(successor.SiteIds, thread.SiteIds);
            AddMissing(successor.FactIds, thread.FactIds);
            AddMissing(successor.OpenQuestions, thread.OpenQuestions);
            AddMissing(successor.GenerationCauses, thread.GenerationCauses);
            AddMissing(successor.Escalation, thread.Escalation);
            AddMissing(successor.CompletedSteps, thread.CompletedSteps);

            thread.State = ThreadState.Inherited;
            thread.SuccessorThreadId = successor.Id;
            thread.LastAdvancedAt = now;
            thread.LifecycleReason = "inherited by " + inheritor;

            world.Threads.Add(successor);
            world.Record(
                WorldEventType.ThreadInherited,
                inheritor,
                EntityId.None,
                now,
                0.4,
                related: thread.FactIds,
                tags: new[] { InheritedTag, "from:" + thread.Id.Value, "to:" + successor.Id.Value },
                threadId: thread.Id);
        }

        private static void Quarantine(NarrativeWorldState world, NarrativeThread thread, GameTime now, string reason)
        {
            thread.State = ThreadState.Quarantined;
            thread.LastAdvancedAt = now;
            thread.LifecycleReason = reason;
            world.Record(
                WorldEventType.ThreadQuarantined,
                EntityId.None,
                EntityId.None,
                now,
                0.1,
                related: thread.FactIds,
                tags: new[] { QuarantinedTag, reason },
                threadId: thread.Id);
        }

        private static string MalformedReason(NarrativeWorldState world, NarrativeThread thread)
        {
            if (thread.Id.IsNone)
            {
                return "thread has no id";
            }

            if (string.IsNullOrEmpty(thread.ArchetypeId))
            {
                return "thread has no archetype";
            }

            if (thread.ParticipantIds.Count == 0 && thread.FactIds.Count == 0 && thread.SiteIds.Count == 0)
            {
                return "thread has no participants, facts or sites";
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                if (world.Knowledge.GetFact(thread.FactIds[i]) == null)
                {
                    return "thread names missing fact " + thread.FactIds[i];
                }
            }

            return string.Empty;
        }

        private static ParticipantLifeSummary SummarizeParticipantLife(NarrativeThread thread, IVanillaState vanilla)
        {
            ParticipantLifeSummary summary = new ParticipantLifeSummary();
            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                EntityId participant = thread.ParticipantIds[i];
                if (participant.IsNone)
                {
                    summary.UnknownCount++;
                    continue;
                }

                switch (vanilla.GetLifeState(participant))
                {
                    case VanillaLifeState.Alive:
                        summary.AliveCount++;
                        break;
                    case VanillaLifeState.Dead:
                        summary.DeadCount++;
                        break;
                    default:
                        summary.UnknownCount++;
                        break;
                }
            }

            return summary;
        }

        private static EntityId FindInheritor(NarrativeWorldState world, IVanillaState vanilla, NarrativeThread thread)
        {
            EntityId best = EntityId.None;
            int bestScore = int.MinValue;

            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                foreach (RelationshipEdge edge in world.Relationships.EdgesTo(thread.ParticipantIds[i]))
                {
                    if (thread.ParticipantIds.Contains(edge.From) || vanilla.GetLifeState(edge.From) != VanillaLifeState.Alive)
                    {
                        continue;
                    }

                    int score = InheritanceScore(edge);
                    if (score > bestScore)
                    {
                        best = edge.From;
                        bestScore = score;
                    }
                }
            }

            return bestScore > 0 ? best : EntityId.None;
        }

        private static int InheritanceScore(RelationshipEdge edge)
        {
            switch (edge.Kind)
            {
                case RelationKind.Spouse:
                    return 100 + edge.Sentiment;
                case RelationKind.Family:
                    return 90 + edge.Sentiment;
                case RelationKind.Employer:
                case RelationKind.Employee:
                    return 70 + edge.Sentiment;
                case RelationKind.Friend:
                case RelationKind.Accomplice:
                    return 50 + edge.Sentiment;
                case RelationKind.Creditor:
                case RelationKind.Debtor:
                    return 40 + edge.Sentiment;
                default:
                    return 0;
            }
        }

        private static void AddMissing<T>(List<T> target, IEnumerable<T> source)
        {
            foreach (T item in source)
            {
                if (!target.Contains(item))
                {
                    target.Add(item);
                }
            }
        }

        private struct ParticipantLifeSummary
        {
            public int AliveCount;
            public int DeadCount;
            public int UnknownCount;
        }
    }
}
