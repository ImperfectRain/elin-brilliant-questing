using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;

namespace BrilliantQuesting.Lab.Playground
{
    /// <summary>
    /// A count of everything durable the world holds, taken either side of a conversation.
    ///
    /// Counts rather than contents, because the question this answers is "did talking change the
    /// world, and where" - and a diff of contents would be a second history sitting beside the
    /// ledger. What actually changed is read from the ledger itself, which is where it already is.
    ///
    /// Taken around the whole exchange rather than per turn, because days may pass between two
    /// exchanges and a thread advancing is a world change the conversation did not make. The per
    /// turn figures on <see cref="PlaygroundTurn"/> stay the narrower claim.
    /// </summary>
    public readonly struct PlaygroundWorldCounts
    {
        private PlaygroundWorldCounts(int events, int facts, int obligations, int beliefs, int threads)
        {
            Events = events;
            Facts = facts;
            Obligations = obligations;
            SpeakerBeliefs = beliefs;
            OpenThreads = threads;
        }

        public int Events { get; }

        public int Facts { get; }

        public int Obligations { get; }

        /// <summary>How many claims the speaker holds a belief about.</summary>
        public int SpeakerBeliefs { get; }

        public int OpenThreads { get; }

        public static PlaygroundWorldCounts Of(PlaygroundStage stage, EntityId speaker)
        {
            int beliefs = 0;
            foreach (KnowledgeRecord unused in stage.World.Knowledge.BeliefsOf(speaker))
            {
                beliefs++;
            }

            return new PlaygroundWorldCounts(
                stage.World.Ledger.Count,
                stage.World.Knowledge.Facts.Count,
                stage.World.Obligations.Records.Count,
                beliefs,
                stage.World.Threads.Count);
        }

        /// <summary>What moved between two readings, or an empty list when nothing did.</summary>
        public IReadOnlyList<string> Since(PlaygroundWorldCounts earlier)
        {
            List<string> moved = new List<string>();
            Note(moved, "events", earlier.Events, Events);
            Note(moved, "facts", earlier.Facts, Facts);
            Note(moved, "obligations", earlier.Obligations, Obligations);
            Note(moved, "speaker beliefs", earlier.SpeakerBeliefs, SpeakerBeliefs);
            Note(moved, "threads", earlier.OpenThreads, OpenThreads);
            return moved;
        }

        private static void Note(List<string> into, string what, int before, int after)
        {
            if (before != after)
            {
                into.Add(what + " " + before + "->" + after);
            }
        }
    }
}
