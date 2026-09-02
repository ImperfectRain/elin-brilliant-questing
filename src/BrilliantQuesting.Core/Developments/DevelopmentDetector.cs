using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Developments
{
    /// <summary>
    /// Step 7 of the expression pipeline (CD §37): read the world as it stands and say what is
    /// still pressing.
    ///
    /// This is a pure function of authoritative state. It appends no event, writes no fact, opens
    /// no thread and touches nothing it reads - running it twice returns the same developments,
    /// and running it never would leave the world identical. That is what keeps a derived reading
    /// from turning into a second authority; it is also why nothing needs to be saved.
    ///
    /// The rule set is deliberately small: two rules over two different stores. Its shape matters
    /// more than its size. Developments are keyed by the <em>pressure</em>, so many events can
    /// feed one development and most events feed none - which is what stops this from becoming a
    /// wrapper around the ledger - and they do not correspond one-to-one with threads, since one
    /// thread can hold several pressures, a settled thread can still hold one, and a pressure can
    /// exist with no thread at all - which is what stops it from becoming a second thread system.
    /// Later steps add rules here; they do not add fields to <see cref="Development"/>.
    /// </summary>
    public static class DevelopmentDetector
    {
        private static readonly IReadOnlyList<Development> Nothing = new Development[0];

        /// <summary>
        /// Every unresolved pressure the world currently holds, in stable id order.
        ///
        /// Deterministic across a reload: everything is read from persisted state and every list
        /// is ordered by id rather than by the enumeration order of a dictionary, so the same save
        /// derives the same developments in the same order whether or not it has been through a
        /// round trip.
        /// </summary>
        public static IReadOnlyList<Development> Detect(NarrativeWorldState world)
        {
            if (world == null)
            {
                return Nothing;
            }

            List<Development> developments = new List<Development>();
            DetectUnprovenKnowledge(world, developments);
            DetectUnmetObligations(world, developments);
            developments.Sort(ById);
            return developments;
        }

        /// <summary>
        /// Somebody believes something true about somebody else and cannot demonstrate it.
        ///
        /// One development per fact rather than per believer: two people who both saw the same
        /// theft are one pressure with two names on it, not two pressures. A public fact produces
        /// nothing - there is no pressure in a thing everybody may say freely - and neither does a
        /// fact only its own subject believes, because a thief knowing what they did is not a
        /// matter waiting on anybody.
        /// </summary>
        private static void DetectUnprovenKnowledge(NarrativeWorldState world, List<Development> into)
        {
            List<EntityId> factIds = new List<EntityId>(world.Knowledge.Facts.Keys);
            factIds.Sort();

            for (int i = 0; i < factIds.Count; i++)
            {
                Fact fact = world.Knowledge.GetFact(factIds[i]);
                if (fact == null || fact.Truth != TruthState.True || fact.Secrecy <= 0)
                {
                    continue;
                }

                List<EntityId> believers = new List<EntityId>();
                foreach (EntityId knower in world.Knowledge.Knowers(fact.Id))
                {
                    if (knower != fact.Subject && !world.Knowledge.CanProve(knower, fact.Id))
                    {
                        believers.Add(knower);
                    }
                }

                if (believers.Count == 0)
                {
                    continue;
                }

                believers.Sort();

                List<string> tags = new List<string> { DevelopmentPressures.UnprovenKnowledge };
                bool contested = !fact.Subject.IsNone && world.Knowledge.Knows(fact.Subject, fact.Id);
                if (contested)
                {
                    tags.Add(DevelopmentPressures.Contested);
                }

                List<EntityId> subjects = new List<EntityId>(believers);
                if (!fact.Subject.IsNone && !subjects.Contains(fact.Subject))
                {
                    subjects.Add(fact.Subject);
                    subjects.Sort();
                }

                NarrativeThread carrier = ThreadCarrying(world, fact.Id);

                into.Add(new Development(
                    "dev.unproven_knowledge:" + fact.Id.Value,
                    tags,
                    carrier == null ? EntityId.None : carrier.Id,
                    fact.Id,
                    Ids(fact.OriginEvent),
                    subjects,
                    carrier == null ? null : Copy(carrier.SiteIds),
                    // A secret nobody can prove presses harder the more mouths it is in: each
                    // believer past the first is one more person who could say it out loud.
                    fact.Secrecy + (10 * (believers.Count - 1))));
            }
        }

        /// <summary>
        /// A social debt that is still open. Pressure between two people rather than about a
        /// claim, so it has no focus fact - and therefore nothing for a storylet to build roles
        /// around, however much of a matter it is.
        /// </summary>
        private static void DetectUnmetObligations(NarrativeWorldState world, List<Development> into)
        {
            IReadOnlyList<SocialObligation> records = world.Obligations.Records;
            if (records.Count == 0)
            {
                return;
            }

            Dictionary<EntityId, WorldEvent> sources = SourceIndex(world);

            for (int i = 0; i < records.Count; i++)
            {
                SocialObligation obligation = records[i];
                if (!obligation.IsOpen)
                {
                    continue;
                }

                List<string> tags = new List<string> { DevelopmentPressures.UnmetObligation };
                if (obligation.Kind == SocialObligationKind.Grudge)
                {
                    tags.Add(DevelopmentPressures.Adversarial);
                }

                List<EntityId> subjects = new List<EntityId>();
                Add(subjects, obligation.Debtor);
                Add(subjects, obligation.Creditor);
                subjects.Sort();

                // The obligation itself records only its source event, so where and what matter it
                // belongs to are read back off that event rather than stored twice.
                EntityId threadId = EntityId.None;
                List<EntityId> sites = null;
                WorldEvent source;
                if (!obligation.SourceEventId.IsNone && sources.TryGetValue(obligation.SourceEventId, out source))
                {
                    threadId = source.ThreadId;
                    if (!source.Zone.IsNone)
                    {
                        sites = new List<EntityId> { source.Zone };
                    }
                }

                into.Add(new Development(
                    "dev.unmet_obligation:" + obligation.Id.Value,
                    tags,
                    threadId,
                    EntityId.None,
                    Ids(obligation.SourceEventId),
                    subjects,
                    sites,
                    obligation.Strength * 20));
            }
        }

        private static Dictionary<EntityId, WorldEvent> SourceIndex(NarrativeWorldState world)
        {
            IReadOnlyList<WorldEvent> events = world.Ledger.Events;
            Dictionary<EntityId, WorldEvent> index = new Dictionary<EntityId, WorldEvent>(events.Count);
            for (int i = 0; i < events.Count; i++)
            {
                index[events[i].Id] = events[i];
            }

            return index;
        }

        /// <summary>
        /// The thread that holds this fact, whatever state it is in. A settled thread does not
        /// unmake an unproven secret; whether the matter can still be <em>played</em> is a scene
        /// question, answered by <c>SceneStatus</c> when something tries.
        /// </summary>
        private static NarrativeThread ThreadCarrying(NarrativeWorldState world, EntityId factId)
        {
            for (int i = 0; i < world.Threads.Count; i++)
            {
                if (world.Threads[i].FactIds.Contains(factId))
                {
                    return world.Threads[i];
                }
            }

            return null;
        }

        private static IReadOnlyList<EntityId> Ids(EntityId single)
        {
            return single.IsNone ? null : new[] { single };
        }

        private static List<EntityId> Copy(List<EntityId> source)
        {
            return source == null || source.Count == 0 ? null : new List<EntityId>(source);
        }

        private static void Add(List<EntityId> into, EntityId id)
        {
            if (!id.IsNone && !into.Contains(id))
            {
                into.Add(id);
            }
        }

        private static int ById(Development left, Development right)
        {
            return string.CompareOrdinal(left.Id, right.Id);
        }
    }
}
