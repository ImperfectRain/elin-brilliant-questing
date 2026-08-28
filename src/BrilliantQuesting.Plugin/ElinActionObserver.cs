using System;
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
    /// Act differently. BQ-014 records only confirmed thefts; BQ-015 owns witness derivation.
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
                    _log.LogInfo("Observed vanilla theft: " + _world.Registry.NameOf(recorded.Actor)
                                 + " took " + action.ItemName + " [" + action.Item + "]"
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
            if (!IsTheft(act, sourceId))
            {
                return null;
            }

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

            return new ObservedVanillaAction(
                ObservedVanillaActionKind.Theft,
                actorId,
                targetId,
                itemId,
                item.Name,
                zone,
                sourceId);
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

            return target == null || target == actor ? EntityId.None : EntityIdFor(target);
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
