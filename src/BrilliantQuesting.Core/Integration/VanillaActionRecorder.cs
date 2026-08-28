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

        public WorldEvent Record(ObservedVanillaAction action)
        {
            if (action == null || action.Actor.IsNone)
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
                tags: new[] { "observed_vanilla", action.SourceActionId });
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
                tags: new[] { "observed_vanilla", action.SourceActionId });
        }
    }
}
