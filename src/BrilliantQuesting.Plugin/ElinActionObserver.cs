using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Translates Elin's ActPerformed event into the simulation's event vocabulary.
    ///
    /// The event payload is deliberately accepted as object: the public event bus publishes object
    /// payloads, and this keeps a small diagnostic path alive if an Early Access build wraps the
    /// Act differently.
    /// </summary>
    internal sealed class ElinActionObserver
    {
        private readonly NarrativeWorldState _world;
        private readonly ElinVanillaState _vanilla;
        private readonly ElinBindings _bindings;
        private readonly VanillaActionRecorder _recorder;
        private readonly ManualLogSource _log;
        private bool _reportedUnknownShape;

        internal ElinActionObserver(
            NarrativeWorldState world,
            ElinVanillaState vanilla,
            ElinBindings bindings,
            ManualLogSource log)
        {
            _world = world;
            _vanilla = vanilla;
            _bindings = bindings;
            _recorder = new VanillaActionRecorder(world, vanilla);
            _log = log;
        }

        internal void Observe(object payload)
        {
            try
            {
                object unwrapped = Unwrap(payload);
                Act act = unwrapped as Act;
                if (act == null)
                {
                    ReportUnknownPayload(payload, unwrapped);
                    return;
                }

                ObservedVanillaAction action = ToObservedAction(act);
                if (action == null)
                {
                    return;
                }

                WorldEvent recorded = _recorder.Record(action);
                if (recorded != null)
                {
                    _log.LogInfo("Observed vanilla " + recorded.Type + ": "
                                 + _world.Registry.NameOf(recorded.Actor) + " -> "
                                 + _world.Registry.NameOf(recorded.Target)
                                 + Detail(action)
                                 + " via " + action.SourceActionId + " (" + recorded.Id + ").");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Skipped ActPerformed observation after an exception: " + ex);
            }
        }

        private ObservedVanillaAction ToObservedAction(Act act)
        {
            string sourceId = SourceId(act);
            if (IsTheft(act, sourceId))
            {
                return ToObservedTheft(act, sourceId);
            }

            if (IsHostile(act, sourceId))
            {
                return ToObservedViolence(act, sourceId);
            }

            return null;
        }

        private ObservedVanillaAction ToObservedTheft(Act act, string sourceId)
        {
            Chara actor = ActorOf(act);
            Thing item = ItemOf(act);
            if (actor == null || item == null)
            {
                _log.LogInfo("Observed theft-like act " + sourceId + " without "
                             + (actor == null ? "an actor" : "a real item") + "; ignored.");
                return null;
            }

            EntityId actorId = EntityIdFor(actor);
            EntityId targetId = TargetOf(act, actor);
            EntityId itemId = EntityIdFor(item);
            EntityId zone = _vanilla.GetZoneOf(actorId);
            IReadOnlyList<EntityId> witnesses = WitnessesOf(actor, item);

            return new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                actorId,
                targetId,
                itemId,
                item.Name,
                zone,
                sourceId,
                witnesses);
        }

        private ObservedVanillaAction ToObservedViolence(Act act, string sourceId)
        {
            Chara actor = ActorOf(act);
            Chara target = TargetCharaOf(act, actor);
            if (actor == null || target == null)
            {
                return null;
            }

            EntityId actorId = EntityIdFor(actor);
            EntityId targetId = EntityIdFor(target);
            EntityId zone = _vanilla.GetZoneOf(actorId);
            IReadOnlyList<EntityId> witnesses = WitnessesOf(actor, target);
            ObservedVanillaActionKind kind = target.isDead
                ? ObservedVanillaActionKind.Killed
                : ObservedVanillaActionKind.Attacked;

            return new ObservedVanillaAction(
                kind,
                actorId,
                targetId,
                EntityId.None,
                "",
                zone,
                sourceId,
                witnesses);
        }

        private static string Detail(ObservedVanillaAction action)
        {
            return action.Item.IsNone ? string.Empty : " with " + action.ItemName + " [" + action.Item + "]";
        }

        private static object Unwrap(object payload)
        {
            if (payload == null)
            {
                return null;
            }

            Type type = payload.GetType();
            PropertyInfo data = type.GetProperty("data", BindingFlags.Public | BindingFlags.Instance)
                                ?? type.GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
            return data == null ? payload : data.GetValue(payload, null);
        }

        private void ReportUnknownPayload(object payload, object unwrapped)
        {
            if (_reportedUnknownShape)
            {
                return;
            }

            _reportedUnknownShape = true;
            string outer = payload == null ? "<null>" : payload.GetType().FullName;
            string inner = unwrapped == null ? "<null>" : unwrapped.GetType().FullName;
            _log.LogInfo("ActPerformed payload was not an Act; outer=" + outer + ", data=" + inner + ".");
        }

        private static bool IsTheft(Act act, string sourceId)
        {
            string typeName = act.GetType().Name;
            return string.Equals(typeName, "AI_Steal", StringComparison.Ordinal)
                   || Contains(sourceId, "steal")
                   || Contains(typeName, "steal");
        }

        private static bool IsHostile(Act act, string sourceId)
        {
            string typeName = act.GetType().Name;
            return act.IsHostileAct
                   || act is ActBaseAttack
                   || Contains(typeName, "attack")
                   || Contains(typeName, "melee")
                   || Contains(typeName, "ranged")
                   || Contains(sourceId, "attack");
        }

        private static bool Contains(string text, string value)
        {
            return !string.IsNullOrEmpty(text)
                   && text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string SourceId(Act act)
        {
            string id = act.ID;
            return string.IsNullOrEmpty(id) ? act.GetType().Name : id;
        }

        private static Chara ActorOf(Act act)
        {
            Chara actor = GetField<Chara>(act, "CC");
            if (actor != null)
            {
                return actor;
            }

            return GetField<Chara>(act, "owner");
        }

        private static Thing ItemOf(Act act)
        {
            Card targetCard = GetField<Card>(act, "TC");
            if (targetCard?.Thing != null)
            {
                return targetCard.Thing;
            }

            Thing tool = GetField<Thing>(act, "TOOL");
            if (tool != null)
            {
                return tool;
            }

            return GetField<Thing>(act, "target");
        }

        private EntityId TargetOf(Act act, Chara actor)
        {
            Chara target = TargetCharaOf(act, actor);
            return target == null || target == actor ? EntityId.None : EntityIdFor(target);
        }

        private static Chara TargetCharaOf(Act act, Chara actor)
        {
            Chara target = null;
            Card targetCard = GetField<Card>(act, "TC");
            if (targetCard?.Chara != null && targetCard.Chara != actor)
            {
                target = targetCard.Chara;
            }

            if (target == null)
            {
                target = GetField<Chara>(act, "target");
            }

            return target == actor ? null : target;
        }

        private IReadOnlyList<EntityId> WitnessesOf(Chara actor, Card focus)
        {
            List<EntityId> witnesses = new List<EntityId>();
            Map map = EClass._map;
            if (actor == null || map?.charas == null)
            {
                return witnesses;
            }

            int stealth = _vanilla.GetSkill(EntityIdFor(actor), VanillaSkill.Stealth);
            foreach (Chara candidate in map.charas)
            {
                if (!CanWitness(candidate, actor, focus, stealth))
                {
                    continue;
                }

                EntityId witness = EntityIdFor(candidate);
                if (!witnesses.Contains(witness))
                {
                    witnesses.Add(witness);
                }
            }

            return witnesses;
        }

        private bool CanWitness(Chara witness, Chara actor, Card focus, int actorStealth)
        {
            if (witness == null || witness == actor || witness.isDead || actor == null)
            {
                return false;
            }

            int distance = witness.Dist(actor);
            int sightRadius = witness.GetSightRadius();
            int maxDistance = Math.Min(Math.Max(1, sightRadius), 8);
            if (distance > maxDistance)
            {
                return false;
            }

            if (!witness.CanSeeLos(actor, maxDistance) && (focus == null || !witness.CanSeeLos(focus, maxDistance)))
            {
                return false;
            }

            EntityId witnessId = EntityIdFor(witness);
            int perception = _vanilla.GetAttribute(witnessId, VanillaAttribute.Perception);
            int spotting = _vanilla.GetSkill(witnessId, VanillaSkill.SpotHidden);
            int detection = perception + spotting + Math.Max(0, maxDistance - distance);
            return detection >= actorStealth;
        }

        private EntityId EntityIdFor(Chara chara)
        {
            if (_bindings.TryGetEntity(chara.uid, out EntityId existing))
            {
                return existing;
            }

            EntityId id = chara.IsPC ? _vanilla.PlayerId : EntityId.Parse("npc_vanilla_" + chara.uid);
            _bindings.Bind(id, chara.uid);

            NarrativeNpc npc = _world.Registry.GetNpc(id);
            if (npc == null)
            {
                npc = new NarrativeNpc(id, string.IsNullOrEmpty(chara.Name) ? id.ToString() : chara.Name)
                {
                    VanillaCharaRef = chara.uid.ToString()
                };
                _world.Registry.Add(npc);
            }
            else
            {
                npc.VanillaCharaRef = chara.uid.ToString();
            }

            return id;
        }

        private EntityId EntityIdFor(Thing thing)
        {
            if (_bindings.TryGetEntity(thing.uid, out EntityId existing))
            {
                return existing;
            }

            EntityId id = EntityId.Parse("item_" + thing.uid);
            _bindings.Bind(id, thing.uid);
            return id;
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            if (instance == null)
            {
                return null;
            }

            Type type = instance.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null && typeof(T).IsAssignableFrom(field.FieldType))
                {
                    return field.GetValue(field.IsStatic ? null : instance) as T;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
