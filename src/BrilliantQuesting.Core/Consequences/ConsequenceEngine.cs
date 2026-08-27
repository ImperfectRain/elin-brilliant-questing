using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Consequences
{
    /// <summary>
    /// Turns history into consequences.
    ///
    /// Every verb in the action library does the same small thing - it records an event - and this
    /// engine derives the rest: who remembers it, whose affinity moves, what the player's legal
    /// standing becomes, who now knows the fact behind it, and which thread just got tenser.
    /// Concentrating that here is what makes a consequence traceable to a cause instead of being
    /// smeared across fifty quest scripts.
    /// </summary>
    public sealed class ConsequenceEngine
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public ConsequenceEngine(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
            Trace = new List<string>();
        }

        /// <summary>Lines describing what the last events actually changed.</summary>
        public List<string> Trace { get; }

        public void Attach()
        {
            _world.Ledger.Subscribe(Handle);
        }

        private void Handle(WorldEvent worldEvent)
        {
            PropagateKnowledge(worldEvent);
            RaiseThreadTension(worldEvent);

            ConsequenceProfile profile = ConsequenceProfiles.For(worldEvent.Type);
            if (profile == null)
            {
                return;
            }

            // Nobody noticed, so nobody reacts. The event still stands in history, which is what
            // makes a later discovery - a witness talking, evidence surfacing - possible.
            if (HasTag(worldEvent, EventTags.Unnoticed))
            {
                return;
            }

            double magnitude = Clamp(worldEvent.Magnitude, 0.0, 2.0);
            bool actorIsPlayer = worldEvent.Actor == _vanilla.PlayerId;

            ApplyToTarget(worldEvent, profile, magnitude, actorIsPlayer);
            ApplyToWitnesses(worldEvent, profile, magnitude, actorIsPlayer);

            if (actorIsPlayer)
            {
                ApplyToPlayerStanding(profile, magnitude);
            }
        }

        private void ApplyToTarget(WorldEvent worldEvent, ConsequenceProfile profile, double magnitude, bool actorIsPlayer)
        {
            if (worldEvent.Target.IsNone || worldEvent.Target == worldEvent.Actor)
            {
                return;
            }

            NarrativeNpc target = _world.Registry.GetNpc(worldEvent.Target);
            if (target == null)
            {
                return;
            }

            int affinityDelta = Scale(profile.TargetAffinity, magnitude);

            // Vanilla affinity stays the single player-facing relationship number; the memory
            // records which slice of it this event accounts for.
            if (actorIsPlayer && affinityDelta != 0 && _vanilla.Supports(VanillaCapability.ReadWriteAffinity))
            {
                _vanilla.ChangeAffinity(worldEvent.Target, affinityDelta);
                Trace.Add(_world.Registry.NameOf(worldEvent.Target) + " affinity " + Signed(affinityDelta) + " (" + profile.SummaryTag + ")");
            }

            _world.Memories.Add(new MemoryRecord(
                _world.NewId("mem"),
                worldEvent.Target,
                worldEvent.Actor,
                worldEvent.Type,
                profile.Weight,
                worldEvent.Time,
                actorIsPlayer ? affinityDelta : 0,
                profile.SummaryTag));

            if (profile.Weight >= MemoryWeight.Important)
            {
                target.Promote(NarrativeImportance.Recurring);
            }
        }

        private void ApplyToWitnesses(WorldEvent worldEvent, ConsequenceProfile profile, double magnitude, bool actorIsPlayer)
        {
            if (profile.WitnessAffinity == 0 && profile.Weight < MemoryWeight.Notable)
            {
                return;
            }

            int delta = Scale(profile.WitnessAffinity, magnitude);
            for (int i = 0; i < worldEvent.Witnesses.Count; i++)
            {
                EntityId witness = worldEvent.Witnesses[i];
                if (witness == worldEvent.Actor || witness == worldEvent.Target)
                {
                    continue;
                }

                if (_world.Registry.GetNpc(witness) == null)
                {
                    continue;
                }

                if (actorIsPlayer && delta != 0 && _vanilla.Supports(VanillaCapability.ReadWriteAffinity))
                {
                    _vanilla.ChangeAffinity(witness, delta);
                }

                _world.Memories.Add(new MemoryRecord(
                    _world.NewId("mem"),
                    witness,
                    worldEvent.Actor,
                    worldEvent.Type,
                    profile.Weight == MemoryWeight.Defining ? MemoryWeight.Important : profile.Weight,
                    worldEvent.Time,
                    actorIsPlayer ? delta : 0,
                    "saw_" + profile.SummaryTag));
            }
        }

        private void ApplyToPlayerStanding(ConsequenceProfile profile, double magnitude)
        {
            int karma = Scale(profile.Karma, magnitude);
            if (karma != 0 && _vanilla.Supports(VanillaCapability.ReadWriteKarma))
            {
                _vanilla.ChangeKarma(karma);
                Trace.Add("karma " + Signed(karma));
            }

            int fame = Scale(profile.Fame, magnitude);
            if (fame != 0 && _vanilla.Supports(VanillaCapability.ReadWriteFame))
            {
                _vanilla.ChangeFame(fame);
                Trace.Add("fame " + Signed(fame));
            }
        }

        /// <summary>
        /// Witnesses learn what they saw - and only that. Nobody else in the world is told, which
        /// is precisely what makes intimidating a witness, buying their silence or destroying the
        /// evidence worth a player's time.
        /// </summary>
        private void PropagateKnowledge(WorldEvent worldEvent)
        {
            if (worldEvent.Witnesses.Count == 0 || worldEvent.Related.Count == 0)
            {
                return;
            }

            for (int f = 0; f < worldEvent.Related.Count; f++)
            {
                EntityId factId = worldEvent.Related[f];
                if (factId.Kind != "fact" || _world.Knowledge.GetFact(factId) == null)
                {
                    continue;
                }

                bool provable = worldEvent.Evidence.Count > 0;
                for (int w = 0; w < worldEvent.Witnesses.Count; w++)
                {
                    EntityId witness = worldEvent.Witnesses[w];
                    _world.Knowledge.Teach(witness, factId, KnowledgeSource.Witnessed, 1.0, worldEvent.Time, provable);
                    Trace.Add(_world.Registry.NameOf(witness) + " witnessed " + factId);
                }
            }
        }

        private void RaiseThreadTension(WorldEvent worldEvent)
        {
            if (worldEvent.ThreadId.IsNone)
            {
                return;
            }

            NarrativeThread thread = _world.GetThread(worldEvent.ThreadId);
            if (thread == null || !thread.IsLive)
            {
                return;
            }

            thread.Tension = (int)Math.Min(100, thread.Tension + worldEvent.Magnitude * 10);
            thread.State = ThreadState.Active;
        }

        private static bool HasTag(WorldEvent worldEvent, string tag)
        {
            for (int i = 0; i < worldEvent.Tags.Count; i++)
            {
                if (worldEvent.Tags[i] == tag)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Scale(int value, double magnitude)
        {
            if (value == 0)
            {
                return 0;
            }

            double scaled = value * magnitude;
            int rounded = (int)(scaled >= 0 ? scaled + 0.5 : scaled - 0.5);

            // A consequence that rounds away to nothing is worse than a small one: the player
            // should always feel that something moved.
            return rounded == 0 ? Math.Sign(value) : rounded;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    }
}
