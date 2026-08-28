using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// Whether a situation can still be played, checked against the world as it is now.
    ///
    /// A thread is a description of people and facts, and both can stop being true between the
    /// moment a scene is offered and the moment the player acts on it. The cast can die - the
    /// first playtest had the player attack the thief inside the first ten minutes - and a thread
    /// that keeps naming them goes on describing a conversation nobody can have. That is the
    /// failure BQ-008 exists to prevent: not a crash, but dialogue that lies.
    ///
    /// This only reports. Deciding what a broken scene *becomes* - inherited by a relative,
    /// abandoned, turned into a different situation - is thread lifecycle work and belongs to
    /// BQ-052. Knowing it is broken has to come first, and is what stops the player being offered
    /// a route through somebody who is no longer there.
    /// </summary>
    public sealed class SceneStatus
    {
        private SceneStatus(bool playable, string reason, IReadOnlyList<EntityId> missing)
        {
            IsPlayable = playable;
            Reason = reason ?? string.Empty;
            Missing = missing ?? EmptyIds;
        }

        private static readonly IReadOnlyList<EntityId> EmptyIds = new List<EntityId>();

        /// <summary>True when the situation can still be acted on as written.</summary>
        public bool IsPlayable { get; }

        /// <summary>Why not, in words a player-facing line can use. Empty when playable.</summary>
        public string Reason { get; }

        /// <summary>Participants the world can no longer produce - dead, or never bound.</summary>
        public IReadOnlyList<EntityId> Missing { get; }

        /// <summary>
        /// Checks a thread against the live world.
        ///
        /// The subject - whoever the player is dealing with right now - is checked hardest,
        /// because a scene with a dead person in front of you is not a scene at all. Other
        /// participants going missing degrades a situation rather than ending it: a theft with a
        /// dead witness is still a theft, and is arguably a better one.
        /// </summary>
        public static SceneStatus Check(
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            EntityId subject)
        {
            if (world == null || vanilla == null || thread == null)
            {
                return new SceneStatus(false, "there is nothing here to act on", null);
            }

            if (!thread.IsLive)
            {
                return new SceneStatus(false, "this matter is settled", null);
            }

            List<EntityId> missing = new List<EntityId>();
            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                EntityId participant = thread.ParticipantIds[i];
                if (!vanilla.IsAlive(participant))
                {
                    missing.Add(participant);
                }
            }

            if (!subject.IsNone && !vanilla.IsAlive(subject))
            {
                return new SceneStatus(false, "they are past being asked", missing);
            }

            if (!subject.IsNone && !thread.ParticipantIds.Contains(subject))
            {
                return new SceneStatus(false, "they have nothing to do with this", missing);
            }

            // Everyone who mattered is gone, so there is no route left through people. The thread
            // is still true; it just cannot be played by talking to anybody.
            if (missing.Count > 0 && missing.Count >= thread.ParticipantIds.Count - 1)
            {
                return new SceneStatus(false, "there is nobody left to take this up with", missing);
            }

            return new SceneStatus(true, string.Empty, missing);
        }

        /// <summary>
        /// A short clause naming who is gone, for a situation description that would otherwise
        /// keep talking about them as though they were standing there. Empty when nobody is.
        /// </summary>
        public string DescribeMissing(NarrativeWorldState world)
        {
            if (world == null || Missing.Count == 0)
            {
                return string.Empty;
            }

            string names = string.Empty;
            for (int i = 0; i < Missing.Count; i++)
            {
                if (i > 0)
                {
                    names += i == Missing.Count - 1 ? " and " : ", ";
                }

                names += world.Registry.NameOf(Missing[i]);
            }

            return Missing.Count == 1
                ? names + " is dead, which closes some of this off."
                : names + " are dead, which closes most of this off.";
        }

        /// <summary>Facts the thread rests on that the knowledge graph can no longer produce.</summary>
        public static bool FocusStillResolvable(NarrativeWorldState world, NarrativeThread thread, EntityId factId)
        {
            if (world == null || thread == null || factId.IsNone)
            {
                return false;
            }

            Fact fact = world.Knowledge.GetFact(factId);
            return fact != null && thread.FactIds.Contains(factId);
        }
    }
}
