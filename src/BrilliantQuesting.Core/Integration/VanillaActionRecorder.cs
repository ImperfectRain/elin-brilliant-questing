using System.Collections.Generic;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Integration
{
    /// <summary>Turns observed vanilla play into the same ledger entries procedural verbs create.</summary>
    public sealed class VanillaActionRecorder
    {
        private readonly NarrativeWorldState _world;
        private readonly IVanillaState _vanilla;

        public VanillaActionRecorder(NarrativeWorldState world, IVanillaState vanilla)
        {
            _world = world;
            _vanilla = vanilla;
        }

        /// <summary>
        /// Admission before native witness work or registration. Passive identity intake alone
        /// does not make background NPC combat narratively significant. Explicit Record callers
        /// already own their observation; this gate is for the live completion-event observer.
        /// </summary>
        public bool ShouldObserveViolence(EntityId actor, EntityId target)
        {
            if (actor.IsNone || target.IsNone) return false;
            if (actor == _vanilla.PlayerId) return true;
            return IsKnown(actor) && IsKnown(target);
        }

        private bool IsKnown(EntityId id) => id == _vanilla.PlayerId
            || (_world.Registry.GetNpc(id)?.Importance >= NarrativeImportance.Known);

        public WorldEvent Record(ObservedVanillaAction action)
        {
            if (action == null)
            {
                return null;
            }

            // The one observation with no actor of its own: a thing that is simply gone is still
            // something the world has to be able to record, and insisting on somebody to blame for
            // it is exactly how a missing ring becomes a theft nobody committed.
            if (action.Kind == ObservedVanillaActionKind.PossessionChanged)
            {
                return RecordPossessionChange(action);
            }

            if (action.Actor.IsNone)
            {
                return null;
            }

            switch (action.Kind)
            {
                case ObservedVanillaActionKind.Theft:
                    return RecordTheft(action);
                case ObservedVanillaActionKind.Attacked:
                    return RecordViolence(action, WorldEventType.Attacked, EntityId.None);
                case ObservedVanillaActionKind.Killed:
                    return RecordKilling(action);
                case ObservedVanillaActionKind.Crafted:
                    return RecordProduction(action);
                default:
                    return null;
            }
        }

        private WorldEvent RecordTheft(ObservedVanillaAction action)
        {
            if (action.Item.IsNone)
            {
                return null;
            }

            Fact theft = new Fact(
                _world.NewId("fact"),
                action.Actor,
                FactPredicates.Stole,
                action.Item,
                action.ItemName,
                secrecy: 60);
            theft.EvidenceIds.Add(action.Item);
            _world.Knowledge.AddFact(theft);
            _world.Knowledge.Teach(action.Actor, theft.Id, KnowledgeSource.Participant, 1.0, _vanilla.Now, true);

            return _world.Record(
                WorldEventType.Theft,
                action.Actor,
                action.Target,
                _vanilla.Now,
                0.7,
                action.Zone,
                related: new[] { theft.Id },
                witnesses: action.Witnesses,
                evidence: new[] { action.Item },
                tags: new[] { EventTags.Observed, action.SourceActionId });
        }

        /// <summary>
        /// Who made this thing, on the word of the game that made it.
        ///
        /// Deliberately quiet: no witnesses, no secrecy, no judgement. Production is not a story
        /// beat, it is the record that lets a later question - where did this come from, who could
        /// have made it - be answered by something other than a guess.
        /// </summary>
        private WorldEvent RecordProduction(ObservedVanillaAction action)
        {
            if (action.Item.IsNone)
            {
                return null;
            }

            Fact made = new Fact(
                _world.NewId("fact"),
                action.Actor,
                FactPredicates.Produced,
                action.Item,
                action.ItemName,
                secrecy: 0);
            made.EvidenceIds.Add(action.Item);
            _world.Knowledge.AddFact(made);
            _world.Knowledge.Teach(action.Actor, made.Id, KnowledgeSource.Participant, 1.0, _vanilla.Now, true);

            return _world.Record(
                WorldEventType.GoodsProduced,
                action.Actor,
                EntityId.None,
                _vanilla.Now,
                0.2,
                action.Zone,
                related: new[] { made.Id },
                evidence: new[] { action.Item },
                tags: new[] { EventTags.Observed, action.SourceActionId });
        }

        /// <summary>
        /// A thing has changed hands, and nobody watched it happen.
        ///
        /// What it records is the possession and nothing else. The standing claim that the old
        /// holder has it is superseded rather than rewritten, a claim for the new holder is made
        /// only where the game named one, and no culprit, no crime and no witness is minted on the
        /// way - because none of those was observed. Somebody's property going missing is a real
        /// change to the world whether or not there is anybody to accuse, and it is the reading of
        /// it that has to stay honest, not the fact of it that has to be suppressed.
        ///
        /// Idempotent against the record rather than against a memory of having been called: if
        /// the world already agrees with what the game is showing, there is nothing to record and
        /// nothing is. That is what makes a reconciliation after a reload a no-op instead of a
        /// second telling - a session-scoped set of seen ids could not survive the reload that
        /// makes the question worth asking.
        /// </summary>
        private WorldEvent RecordPossessionChange(ObservedVanillaAction action)
        {
            if (action.Item.IsNone)
            {
                return null;
            }

            Fact standing = Ownership.ClaimOn(_world, action.Item);
            EntityId recorded = standing == null ? EntityId.None : standing.Subject;
            if (recorded == action.Actor)
            {
                // Already taken in, or never disagreed in the first place.
                return null;
            }

            EventReservation reservation = _world.ReserveEvent();
            if (standing != null)
            {
                standing.Truth = TruthState.Superseded;
            }

            List<EntityId> related = new List<EntityId>();
            if (standing != null)
            {
                related.Add(standing.Id);
            }

            if (!action.Actor.IsNone)
            {
                Fact held = new Fact(
                    _world.NewId("fact"),
                    action.Actor,
                    FactPredicates.Possesses,
                    action.Item,
                    action.ItemName,
                    TruthState.True,
                    secrecy: 0,
                    originEvent: reservation.Id);
                held.EvidenceIds.Add(action.Item);
                _world.Knowledge.AddFact(held);
                related.Add(held.Id);
            }

            return _world.Record(
                reservation,
                WorldEventType.PossessionChanged,
                action.Actor,
                recorded.IsNone ? action.Target : recorded,
                _vanilla.Now,
                0.3,
                action.Zone,
                related: related,

                // Whoever the adapter actually saw, which for a change noticed by reading an
                // inventory is nobody. A witness list is an observation, never a consolation for
                // not having one.
                witnesses: action.Witnesses,
                evidence: new[] { action.Item },
                tags: new[] { EventTags.Observed, action.SourceActionId });
        }

        private WorldEvent RecordKilling(ObservedVanillaAction action)
        {
            if (action.Target.IsNone)
            {
                return null;
            }

            Fact killed = new Fact(
                _world.NewId("fact"),
                action.Actor,
                FactPredicates.Killed,
                action.Target,
                null,
                secrecy: 10);
            _world.Knowledge.AddFact(killed);
            _world.Knowledge.Teach(action.Actor, killed.Id, KnowledgeSource.Participant, 1.0, _vanilla.Now, true);

            return RecordViolence(action, WorldEventType.Killed, killed.Id);
        }

        private WorldEvent RecordViolence(ObservedVanillaAction action, WorldEventType type, EntityId relatedFact)
        {
            if (action.Target.IsNone)
            {
                return null;
            }

            return _world.Record(
                type,
                action.Actor,
                action.Target,
                _vanilla.Now,
                type == WorldEventType.Killed ? 1.0 : 0.9,
                action.Zone,
                related: relatedFact.IsNone ? null : new[] { relatedFact },
                witnesses: action.Witnesses,
                tags: new[] { EventTags.Observed, action.SourceActionId });
        }
    }
}
