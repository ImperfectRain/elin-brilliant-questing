using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// The stats and belongings a generated character needs in the actual game.
    ///
    /// Generation produces one of these; something else decides how it becomes real. In Elin that
    /// means spawning a Chara from a source-sheet archetype and adjusting it; headless it means
    /// filling in a dictionary. The generator does not need to know which.
    /// </summary>
    public sealed class CharacterBlueprint
    {
        public CharacterBlueprint(string name)
        {
            Name = name;
            Attributes = new Dictionary<VanillaAttribute, int>();
            Skills = new Dictionary<VanillaSkill, int>();
            Items = new List<ItemDescriptor>();
        }

        public string Name { get; }

        /// <summary>
        /// Which vanilla archetype to build this person from - a Chara source id such as
        /// "villager". Generation decides the role and the personality; the game decides what a
        /// villager actually is. Empty means the stager picks its configured default.
        /// </summary>
        public string ArchetypeId { get; set; } = string.Empty;

        public int Level { get; set; } = 1;

        public int Money { get; set; }

        /// <summary>Starting vanilla affinity toward the player.</summary>
        public int Affinity { get; set; }

        public Dictionary<VanillaAttribute, int> Attributes { get; }

        public Dictionary<VanillaSkill, int> Skills { get; }

        public List<ItemDescriptor> Items { get; }

        public CharacterBlueprint With(VanillaAttribute attribute, int value)
        {
            Attributes[attribute] = value;
            return this;
        }

        public CharacterBlueprint With(VanillaSkill skill, int value)
        {
            Skills[skill] = value;
            return this;
        }

        public CharacterBlueprint Carrying(ItemDescriptor item)
        {
            Items.Add(item);
            return this;
        }
    }

    /// <summary>
    /// The place a generated site needs in the actual game.
    ///
    /// The mirror of <see cref="CharacterBlueprint"/>, and deliberately physical rather than
    /// semantic: why the place exists, who is in it and how it can be reached stay in the plan the
    /// simulation owns, and what crosses the seam is only what an adapter has to build or bind.
    /// </summary>
    public sealed class SiteBlueprint
    {
        public SiteBlueprint(EntityId siteId, string name, string siteType)
        {
            SiteId = siteId;
            Name = name ?? string.Empty;
            SiteType = siteType ?? string.Empty;
        }

        public EntityId SiteId { get; }

        public string Name { get; }

        /// <summary>Ontology term: "hideout", "ruin", "camp", "workshop", "shrine", "estate".</summary>
        public string SiteType { get; }

        /// <summary>The place is expected to stay on the map rather than being thrown away.</summary>
        public bool Persistent { get; set; }

        /// <summary>What the place keeps is behind something somebody else holds the key to.</summary>
        public bool Restricted { get; set; }

        public int DangerLevel { get; set; }

        /// <summary>Recorded so the same place can be rebuilt identically if it ever has to be.</summary>
        public ulong Seed { get; set; }
    }

    /// <summary>
    /// Turns generated descriptions into things that exist in the running game.
    ///
    /// Keeping this separate from <see cref="IVanillaState"/> matters: reading the world and
    /// creating in it fail in different ways and on different schedules, and a situation generator
    /// that cannot spawn anything should still be able to reason about the world.
    /// </summary>
    public interface ISituationStager
    {
        void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone);

        void StageItem(EntityId owner, ItemDescriptor item);

        /// <summary>
        /// Gives a generated place a body and returns the adapter's handle for it, or an empty
        /// string where this build cannot embody one.
        ///
        /// Called once per place, by genesis, before anything is staged into it - so an adapter
        /// that answers with nothing costs the simulation an unmade site rather than a half-made
        /// one. The handle is opaque to Core in the same way every other external ref is: what is
        /// on the other side is not Core's business.
        /// </summary>
        string StageSite(SiteBlueprint blueprint);
    }

    /// <summary>Headless staging, for the laboratory and the tests.</summary>
    public sealed class SandboxStager : ISituationStager
    {
        private readonly SandboxVanillaState _vanilla;

        public SandboxStager(SandboxVanillaState vanilla)
        {
            _vanilla = vanilla;
        }

        public void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone)
        {
            _vanilla.Define(id, blueprint.Level, blueprint.Money, zone);
            _vanilla.SetAffinity(id, blueprint.Affinity);

            // Somebody this mod made. Nothing in the vanilla game refers to them, which is what
            // makes them the safe place for death, relocation and long causal histories - so the
            // staged actor is the one class the mutation policy lets everything through for.
            _vanilla.SetActorClass(id, NarrativeActorClass.Generated);

            foreach (KeyValuePair<VanillaAttribute, int> attribute in blueprint.Attributes)
            {
                _vanilla.SetAttribute(id, attribute.Key, attribute.Value);
            }

            foreach (KeyValuePair<VanillaSkill, int> skill in blueprint.Skills)
            {
                _vanilla.SetSkill(id, skill.Key, skill.Value);
            }

            for (int i = 0; i < blueprint.Items.Count; i++)
            {
                _vanilla.GiveItem(id, blueprint.Items[i]);
            }
        }

        public void StageItem(EntityId owner, ItemDescriptor item)
        {
            _vanilla.GiveItem(owner, item);
        }

        /// <summary>
        /// The laboratory has no map, so a place is real here as soon as somebody can stand in it -
        /// which is what the site's own zone id already is. Headless, the handle and the id are the
        /// same string; on a live build they are not, and everything downstream reads the handle.
        /// </summary>
        public string StageSite(SiteBlueprint blueprint)
        {
            return blueprint == null || blueprint.SiteId.IsNone ? string.Empty : blueprint.SiteId.Value;
        }
    }
}
