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

        /// <summary>
        /// ActPerformed is an Act completion signal, not a universal activity bus. Installed
        /// source shows chat returns false and representative production creation paths do not
        /// publish through this event, so production provenance waits for a real hook.
        /// </summary>
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
            IReadOnlyList<EntityId> witnesses = WitnessesOf(actor);

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

            if (!IsWorthRecording(actor, target))
            {
                return null;
            }

            EntityId actorId = EntityIdFor(actor);
            EntityId targetId = EntityIdFor(target);
            EntityId zone = _vanilla.GetZoneOf(actorId);
            IReadOnlyList<EntityId> witnesses = WitnessesOf(actor);
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
            Chara actor = VanillaApiReflection.GetKnownField<Chara>(act, "CC");
            if (actor != null)
            {
                return actor;
            }

            return VanillaApiReflection.GetKnownField<Chara>(act, "owner");
        }

        private static Thing ItemOf(Act act)
        {
            Card targetCard = VanillaApiReflection.GetKnownField<Card>(act, "TC");
            if (targetCard?.Thing != null)
            {
                return targetCard.Thing;
            }

            Thing tool = VanillaApiReflection.GetKnownField<Thing>(act, "TOOL");
            if (tool != null)
            {
                return tool;
            }

            return VanillaApiReflection.GetKnownField<Thing>(act, "target");
        }

        private EntityId TargetOf(Act act, Chara actor)
        {
            Chara target = TargetCharaOf(act, actor);
            return target == null || target == actor ? EntityId.None : EntityIdFor(target);
        }

        private static Chara TargetCharaOf(Act act, Chara actor)
        {
            Chara target = null;
            Card targetCard = VanillaApiReflection.GetKnownField<Card>(act, "TC");
            if (targetCard?.Chara != null && targetCard.Chara != actor)
            {
                target = targetCard.Chara;
            }

            if (target == null)
            {
                target = VanillaApiReflection.GetKnownField<Chara>(act, "target");
            }

            return target == actor ? null : target;
        }

        private IReadOnlyList<EntityId> WitnessesOf(Chara actor)
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
                if (!CanWitness(candidate, actor, stealth))
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

        private bool CanWitness(Chara witness, Chara actor, int actorStealth)
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

            // Seeing the actor is the requirement, not seeing the thing that happened to them.
            //
            // This used to accept line of sight to the victim or the item as sufficient, and the
            // record it produces says the witness Witnessed the fact - "Haron stole the ring" -
            // and can prove it. Somebody who saw only the victim cannot testify to that. They can
            // say a ring is gone; they cannot name who took it, and the difference is the entire
            // substance of an investigation.
            //
            // Splitting that properly - seeing an act, seeing an actor, recognising an actor as
            // somebody in particular - is a real model and belongs with the rest of the epistemic
            // work. Until then the narrow reading is the honest one: no sight of the actor, no
            // testimony about the actor.
            if (!witness.CanSeeLos(actor, maxDistance))
            {
                return false;
            }

            EntityId witnessId = EntityIdFor(witness);
            int perception = _vanilla.GetAttribute(witnessId, VanillaAttribute.Perception);
            int spotting = _vanilla.GetSkill(witnessId, VanillaSkill.SpotHidden);
            int detection = perception + spotting + Math.Max(0, maxDistance - distance);
            return detection >= actorStealth;
        }

        /// <summary>
        /// Whether violence between these two is a story beat or just Elin happening.
        ///
        /// The first live run recorded four combat events in about a minute: a yeek swinging at
        /// the player three times, and a guard shooting a gangster on the far side of town. None
        /// of it means anything, and left alone a dungeon crawl would bury the ledger - and the
        /// chronicle the player is eventually meant to read - under thousands of melee swings.
        /// Mundane content is supposed to give the major beats their weight; a combat log is not
        /// mundane content, it is noise.
        ///
        /// The rule is who the world already cared about before it saw this. Anything the player
        /// does is theirs and is recorded. Otherwise both parties must already be known to the
        /// simulation - staged, or drawn into a situation - which a wandering monster never is.
        /// Whether a wider net is wanted is a director's decision and belongs to BQ-099.
        /// </summary>
        private bool IsWorthRecording(Chara actor, Chara target)
        {
            if (actor.IsPC)
            {
                return true;
            }

            return IsAlreadyKnown(actor) && IsAlreadyKnown(target);
        }

        /// <summary>
        /// Known *before* this observation - deliberately not `EntityIdFor`, which would register
        /// the character it was asked about and make everybody known the first time they swing.
        /// </summary>
        private bool IsAlreadyKnown(Chara chara)
        {
            return chara.IsPC
                   || (_bindings.TryGetEntity(chara.uid, out EntityId id)
                       && _world.Registry.GetNpc(id) != null);
        }

        private EntityId EntityIdFor(Chara chara)
        {
            if (_bindings.TryGetEntity(chara.uid, out EntityId existing))
            {
                return existing;
            }

            EntityId id = ElinBindings.MintCharaId(chara, _vanilla.PlayerId);
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

            // Guards and guild staff have to be recognisable as such the moment they enter the
            // world model, or AuthorityPolicy has nobody to hand an accusation to. Read through
            // the seam now the binding exists, so this is the same observation every other
            // consumer of identity gets rather than a private trait read.
            ElinAuthorityRoles.Apply(npc, _vanilla.GetCharacterIdentity(id));

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
    }
}
