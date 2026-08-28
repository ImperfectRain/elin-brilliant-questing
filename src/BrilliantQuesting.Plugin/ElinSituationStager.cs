using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Plugin
{
    /// <summary>
    /// Turns generated descriptions into things that exist in the running game.
    ///
    /// Everything it makes is an ordinary Elin object: a Chara built from a normal archetype, real
    /// Things from real source rows. Nothing is flagged as belonging to the mod, because the moment
    /// a generated person is a special kind of person, every system that makes Elin interesting -
    /// recruiting, trading, fighting, marrying, killing - stops applying to them.
    /// </summary>
    internal sealed class ElinSituationStager : ISituationStager
    {
        private readonly ElinBindings _bindings;
        private readonly ManualLogSource _log;

        internal ElinSituationStager(ElinBindings bindings, ManualLogSource log)
        {
            _bindings = bindings;
            _log = log;
        }

        /// <summary>Archetype used when a blueprint does not name one.</summary>
        internal string DefaultArchetypeId { get; set; } = "villager";

        public void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone)
        {
            Zone target = EClass._zone;
            if (target == null)
            {
                _log.LogWarning("Cannot stage " + blueprint.Name + ": no zone loaded.");
                return;
            }

            string archetype = string.IsNullOrEmpty(blueprint.ArchetypeId) ? DefaultArchetypeId : blueprint.ArchetypeId;

            Chara chara;
            try
            {
                chara = CharaGen.Create(archetype, blueprint.Level);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not create '" + archetype + "' for " + blueprint.Name + ": " + ex.Message);
                return;
            }

            if (chara == null)
            {
                _log.LogWarning("Archetype '" + archetype + "' produced nothing for " + blueprint.Name + ".");
                return;
            }

            // The generated name is the display name only. Identity is the EntityId binding below.
            if (!string.IsNullOrEmpty(blueprint.Name))
            {
                chara.c_altName = blueprint.Name;
            }

            ApplyStats(chara, blueprint);

            target.AddCard(chara, target.GetSpawnPos(chara) ?? EClass.pc.pos);
            _bindings.Bind(id, chara.uid);

            if (blueprint.Affinity != 0)
            {
                chara.ModAffinity(EClass.pc, blueprint.Affinity, false, false);
            }

            if (blueprint.Money != 0)
            {
                chara.ModCurrency(blueprint.Money, "money");
            }

            foreach (ItemDescriptor item in blueprint.Items)
            {
                StageItem(id, item);
            }

            _log.LogInfo("Staged " + blueprint.Name + " as " + archetype + " (uid " + chara.uid + ", " + id + ")");
        }

        public void StageItem(EntityId owner, ItemDescriptor item)
        {
            if (string.IsNullOrEmpty(item.SourceId))
            {
                // A descriptor read back out of a live inventory describes something that already
                // exists; there is nothing to create.
                _log.LogWarning("Cannot stage '" + item.Name + "': no Thing source id on the descriptor.");
                return;
            }

            Chara holder = _bindings.ResolveChara(owner);
            if (holder == null)
            {
                _log.LogWarning("Cannot stage '" + item.Name + "': " + owner + " is not bound to a live character.");
                return;
            }

            Thing thing;
            try
            {
                thing = ThingGen.Create(item.SourceId, -1, item.Level());
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not create thing '" + item.SourceId + "': " + ex.Message);
                return;
            }

            if (thing == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(item.Name))
            {
                thing.c_altName = item.Name;
            }

            holder.Pick(thing, false, true);
            _bindings.Bind(item.Id, thing.uid);
        }

        private void ApplyStats(Chara chara, CharacterBlueprint blueprint)
        {
            foreach (KeyValuePair<VanillaAttribute, int> attribute in blueprint.Attributes)
            {
                if (ElementAliases.TryGet(attribute.Key, out int elementId))
                {
                    chara.elements.SetBase(elementId, attribute.Value, 0);
                }
            }

            foreach (KeyValuePair<VanillaSkill, int> skill in blueprint.Skills)
            {
                if (ElementAliases.TryGet(skill.Key, out int elementId))
                {
                    chara.elements.SetBase(elementId, skill.Value, 0);
                }
            }
        }
    }

    internal static class ItemDescriptorExtensions
    {
        /// <summary>
        /// Item level from its value. Generation talks about worth, not level, and this keeps the
        /// simulation from having to know Elin's level curve.
        /// </summary>
        internal static int Level(this ItemDescriptor item)
        {
            return item.Value <= 0 ? 1 : Math.Max(1, item.Value / 100);
        }
    }
}
