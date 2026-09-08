using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Threads
{
    /// <summary>
    /// Admission and unsolicited attention limits, not a development scorer. Reads the existing
    /// threads, beliefs and delivery history; owns no copy of player knowledge or world history.
    /// These are runtime policy values, not save state or player intensity presets.
    /// </summary>
    public sealed class NarrativeAttentionBudget
    {
        public int MaximumLiveThreads { get; set; } = 6;
        public int MaximumExposedThreads { get; set; } = 3;
        public double MinimumRemarkSalience { get; set; } = 0.5;

        public int LiveCount(NarrativeWorldState world)
        {
            int count = 0;
            foreach (NarrativeThread thread in world.Threads)
            {
                if (thread.IsLive) count++;
            }
            return count;
        }

        /// <summary>Refuse new generated pressure before vanilla is mutated. Never evicts history.</summary>
        public string GenerationRefusal(NarrativeWorldState world)
        {
            return LiveCount(world) >= Math.Max(0, MaximumLiveThreads)
                ? "live-thread budget is full"
                : null;
        }

        public AttentionSnapshot Read(NarrativeWorldState world, EntityId player, GameTime now, int quietMinutes)
        {
            HashSet<EntityId> exposed = new HashSet<EntityId>();
            long last = world.LastAmbientRemarkMinute;
            foreach (WorldEvent entry in world.Ledger.Events)
            {
                // An earned visitor is a world consequence, not an optional news suggestion.
                // It spends attention so a bark cannot immediately pile on. Home visits while
                // the player is away are not evidence that the player has encountered anything.
                if (!HasTag(entry, ConsequenceArrivals.ArrivalTag)
                    || (!HasTag(entry, ConsequenceArrivals.PlayerSurfaceTag)
                        && !HasTag(entry, ConsequenceArrivals.PlayerPresentTag))) continue;
                last = Math.Max(last, entry.Time.TotalMinutes);
                NarrativeThread thread = world.GetThread(entry.ThreadId);
                if (thread != null && thread.IsLive) exposed.Add(thread.Id);
            }

            foreach (NarrativeThread thread in world.Threads)
            {
                if (!thread.IsLive) continue;
                foreach (EntityId fact in thread.FactIds)
                {
                    if (world.Knowledge.Knows(player, fact))
                    {
                        exposed.Add(thread.Id);
                        break;
                    }
                }
            }

            // A backwards clock cannot grant another unsolicited delivery. The saved stamp
            // remains authoritative until time catches up; reads never repair it.
            bool cooling = last != NarrativeWorldState.NothingSaidYet
                && (now.TotalMinutes < last || now.TotalMinutes - last < Math.Max(0, quietMinutes));
            return new AttentionSnapshot(exposed, cooling, Math.Max(0, MaximumExposedThreads), MinimumRemarkSalience);
        }

        private static bool HasTag(WorldEvent entry, string tag)
        {
            for (int i = 0; i < entry.Tags.Count; i++)
                if (entry.Tags[i] == tag) return true;
            return false;
        }
    }

    /// <summary>A pure reading for one ambient selection pass, also usable by the inspector.</summary>
    public sealed class AttentionSnapshot
    {
        private readonly HashSet<EntityId> _exposed;
        private readonly int _maximum;
        private readonly double _minimumSalience;

        internal AttentionSnapshot(HashSet<EntityId> exposed, bool cooling, int maximum, double minimumSalience)
        {
            _exposed = exposed;
            CoolingDown = cooling;
            _maximum = maximum;
            _minimumSalience = minimumSalience;
        }

        public int ExposedThreadCount => _exposed.Count;
        public bool CoolingDown { get; }

        public string RemarkRefusal(NarrativeWorldState world, EntityId factId, double salience)
        {
            if (CoolingDown) return "unsolicited exposure cooldown";
            if (double.IsNaN(salience) || salience < _minimumSalience) return "below ambient salience threshold";

            int introduced = 0;
            foreach (NarrativeThread thread in world.Threads)
            {
                if (thread.IsLive && thread.FactIds.Contains(factId) && !_exposed.Contains(thread.Id)) introduced++;
            }
            // Updates to an already encountered matter do not take a second slot. A single
            // shared claim can introduce multiple threads, so all of them must fit together.
            string refusal = IntroductionRefusal(introduced);
            return refusal == null ? null : refusal + "; this matter remains available by asking";
        }

        public string IntroductionRefusal(int introduced)
        {
            return introduced > 0 && (long)_exposed.Count + introduced > _maximum
                ? "exposed-thread budget is full"
                : null;
        }
    }
}
