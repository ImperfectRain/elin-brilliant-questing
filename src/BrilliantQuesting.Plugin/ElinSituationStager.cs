using System;
using System.Collections.Generic;
using BepInEx.Logging;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

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
        private readonly NarrativeWorldState _world;

        internal ElinSituationStager(ElinBindings bindings, ManualLogSource log, NarrativeWorldState world = null)
        {
            _bindings = bindings;
            _log = log;
            _world = world;
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
            NarrativeNpc npc = _world?.Registry.GetNpc(id);
            if (npc != null)
            {
                npc.VanillaCharaRef = chara.uid.ToString();
            }

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

        /// <summary>
        /// Gives a generated place a body by binding it to the zone the player is standing in, and
        /// answers with the handle every other read is already keyed on.
        ///
        /// It does not create a zone. Native site creation - `Region.CreateRandomSite`, `addMap`
        /// for a predeclared mod zone, and whether a created site's map survives a save at all -
        /// is unverified on this build (`ELIN-Q-0032`, `PP §7`), and guessing at it would put a
        /// place in the save that the game might not agree exists. Binding is the one embodiment
        /// this repository's evidence supports: the zone uid is read, not invented, and it is the
        /// same id <see cref="ElinPresence.IdOf"/> mints everywhere else, so occupants staged
        /// afterwards land in the place the site names. Reusing a location deliberately before
        /// generating one is also BQ-088's rule arriving early rather than a shortcut around it.
        /// </summary>
        public string StageSite(SiteBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return string.Empty;
            }

            Zone target = EClass._zone;
            if (target == null)
            {
                _log.LogWarning("Cannot place " + blueprint.Name + ": no zone loaded.");
                return string.Empty;
            }

            EntityId zone = ElinPresence.IdOf(target);
            _log.LogInfo("Site " + blueprint.Name + " [" + blueprint.SiteType + "] bound to " + zone.Value
                         + " as " + blueprint.SiteId + "; no zone was created.");
            return zone.Value;
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
