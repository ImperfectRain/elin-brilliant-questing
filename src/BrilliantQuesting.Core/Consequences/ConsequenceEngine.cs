using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
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
            ApplyToTies(worldEvent, profile, magnitude, actorIsPlayer);

            // Karma and fame are the world's verdict on what somebody did. An observed act has
            // not been judged yet - the observer can see that the player killed something, not
            // whether it was murder, self-defence, a lawful bounty or clearing a dungeon - so the
            // verdict waits for BQ-046 rather than defaulting to "murder". Affinity and memory
            // still apply: being hit is a reason to think less of someone whatever the law says.
            if (actorIsPlayer && !HasTag(worldEvent, EventTags.Observed))
            {
                ApplyToPlayerStanding(profile, magnitude);
            }
            else if (actorIsPlayer)
            {
                Trace.Add("standing unchanged: " + worldEvent.Type + " was observed, not judged");
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

                int delta = WitnessAffinityDelta(worldEvent, witness, profile, magnitude);
                if (actorIsPlayer && delta != 0 && _vanilla.Supports(VanillaCapability.ReadWriteAffinity))
                {
                    _vanilla.ChangeAffinity(witness, delta);
                }

                Trace.Add(WitnessTrace(worldEvent, witness, profile, delta));

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

        private int WitnessAffinityDelta(WorldEvent worldEvent, EntityId witness, ConsequenceProfile profile, double magnitude)
        {
            if (profile.WitnessAffinity == 0)
            {
                return 0;
            }

            if (BroadWitnessReaction(worldEvent.Type))
            {
                return Scale(profile.WitnessAffinity, magnitude);
            }

            double relevance = WitnessRelevance(worldEvent, witness);
            if (relevance <= 0.0)
            {
                return 0;
            }

            int delta = Scale(profile.WitnessAffinity, magnitude * relevance);
            return delta;
        }

        private string WitnessTrace(WorldEvent worldEvent, EntityId witness, ConsequenceProfile profile, int delta)
        {
            string prefix = _world.Registry.NameOf(witness) + " witnessed " + worldEvent.Type + " toward "
                            + _world.Registry.NameOf(worldEvent.Target) + ": ";

            if (profile.WitnessAffinity == 0)
            {
                return prefix + "no affinity effect for this event type";
            }

            if (BroadWitnessReaction(worldEvent.Type))
            {
                return prefix + "broad witness reaction " + Signed(delta) + " (" + profile.SummaryTag + ")";
            }

            double relevance = WitnessRelevance(worldEvent, witness);
            if (relevance <= 0.0)
            {
                return prefix + "no affinity effect; no positive tie to target, hostile tie to actor, or direct stake";
            }

            return prefix + "relevance " + relevance.ToString("0.00") + " -> " + Signed(delta)
                   + " (" + WitnessReason(worldEvent, witness) + ")";
        }

        private string WitnessReason(WorldEvent worldEvent, EntityId witness)
        {
            RelationshipEdge toTarget = _world.Relationships.Find(witness, worldEvent.Target);
            if (toTarget != null && toTarget.Sentiment > 0)
            {
                return toTarget.Kind + " tie to " + _world.Registry.NameOf(worldEvent.Target);
            }

            RelationshipEdge toActor = _world.Relationships.Find(witness, worldEvent.Actor);
            if (toActor != null && toActor.Sentiment < 0)
            {
                return "hostile " + toActor.Kind + " tie to " + _world.Registry.NameOf(worldEvent.Actor);
            }

            if (HasDirectStake(worldEvent, witness))
            {
                return "direct stake in related fact";
            }

            return "relevant";
        }

        private double WitnessRelevance(WorldEvent worldEvent, EntityId witness)
        {
            double relevance = 0.0;

            RelationshipEdge toTarget = _world.Relationships.Find(witness, worldEvent.Target);
            if (toTarget != null && toTarget.Sentiment > 0)
            {
                relevance = Math.Max(relevance, HarmPropagation.CarriedBy(toTarget.Kind) * (toTarget.Sentiment / 100.0));
            }

            RelationshipEdge toActor = _world.Relationships.Find(witness, worldEvent.Actor);
            if (toActor != null && toActor.Sentiment < 0)
            {
                relevance = Math.Max(relevance, HarmPropagation.CarriedBy(toActor.Kind) * (-toActor.Sentiment / 100.0));
            }

            if (HasDirectStake(worldEvent, witness))
            {
                relevance = Math.Max(relevance, 0.5);
            }

            return relevance > 1.0 ? 1.0 : relevance;
        }

        private bool HasDirectStake(WorldEvent worldEvent, EntityId witness)
        {
            for (int i = 0; i < worldEvent.Related.Count; i++)
            {
                Fact fact = _world.Knowledge.GetFact(worldEvent.Related[i]);
                if (fact != null && (fact.Subject == witness || fact.Object == witness))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BroadWitnessReaction(WorldEventType type)
        {
            return type == WorldEventType.Attacked
                   || type == WorldEventType.Killed
                   || type == WorldEventType.Rescued
                   || type == WorldEventType.ItemReturned
                   || type == WorldEventType.TakenIn
                   || type == WorldEventType.SiteCleared
                   || type == WorldEventType.ThreadResolved;
        }

        /// <summary>
        /// The people tied to whoever it happened to.
        ///
        /// Harm has always stopped at the person it landed on plus whoever was standing there.
        /// That makes a town a collection of strangers who happen to share a map: hurting the
        /// shopkeeper costs the shopkeeper's opinion and nothing else, however many people in the
        /// room are her family. The reaction is derived entirely from the tie graph, so there is
        /// no rule anywhere naming a shopkeeper and a brother - only a Family edge that was put
        /// there for its own reasons, and which now turns out to have been a consequence all
        /// along.
        ///
        /// The reactor's own memory is what makes it explicable later: they did not see it, but
        /// they know it happened to somebody they care about, and they know who did it.
        /// </summary>
        private void ApplyToTies(WorldEvent worldEvent, ConsequenceProfile profile, double magnitude, bool actorIsPlayer)
        {
            // Nobody takes sides against nobody: an event with no actor has no one to resent, and
            // an actor cannot be resented for what they did to themselves.
            if (worldEvent.Target.IsNone || worldEvent.Actor.IsNone || worldEvent.Target == worldEvent.Actor)
            {
                return;
            }

            IReadOnlyList<TieReaction> reactions = HarmPropagation.Reactions(
                _world.Relationships,
                worldEvent.Target,
                profile.TargetAffinity,
                magnitude,
                CanReactOnBehalfOf(worldEvent.Actor));

            for (int i = 0; i < reactions.Count; i++)
            {
                TieReaction reaction = reactions[i];

                if (actorIsPlayer && _vanilla.Supports(VanillaCapability.ReadWriteAffinity))
                {
                    _vanilla.ChangeAffinity(reaction.Reactor, reaction.Delta);
                }
                else if (!actorIsPlayer)
                {
                    // Vanilla affinity only tracks the player, so an NPC's opinion of another NPC
                    // has to land on the tie graph. A first offence creates the tie: they now
                    // hold a view of somebody they may never have met, which is exactly what
                    // hearing about it means.
                    RelationshipEdge toward = _world.Relationships.Find(reaction.Reactor, worldEvent.Actor)
                                              ?? _world.Relationships.Connect(reaction.Reactor, worldEvent.Actor, RelationKind.Acquaintance, 0);
                    toward.Sentiment = ClampSentiment(toward.Sentiment + reaction.Delta);
                }

                _world.Memories.Add(new MemoryRecord(
                    _world.NewId("mem"),
                    reaction.Reactor,
                    worldEvent.Actor,
                    worldEvent.Type,
                    profile.Weight == MemoryWeight.Defining ? MemoryWeight.Important : profile.Weight,
                    worldEvent.Time,
                    actorIsPlayer ? reaction.Delta : 0,
                    "kin_" + profile.SummaryTag));

                Trace.Add(_world.Registry.NameOf(reaction.Reactor) + " " + Signed(reaction.Delta)
                          + " as " + reaction.Through + " of " + _world.Registry.NameOf(worldEvent.Target)
                          + " (" + profile.SummaryTag + ")");
            }
        }

        /// <summary>
        /// Who is allowed to take somebody else's side. Not the person who did it, not the
        /// player - background simulation may move NPC opinion, never the player's own - and
        /// nobody who is not a living character in the registry.
        /// </summary>
        private Func<EntityId, bool> CanReactOnBehalfOf(EntityId actor)
        {
            return candidate =>
            {
                if (candidate == actor || candidate == _vanilla.PlayerId)
                {
                    return false;
                }

                NarrativeNpc npc = _world.Registry.GetNpc(candidate);
                return npc != null && npc.Alive;
            };
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

        private static int ClampSentiment(int value)
        {
            return value < -100 ? -100 : value > 100 ? 100 : value;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }

        private static string Signed(int value) => value >= 0 ? "+" + value : value.ToString();
    }
}
