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
    }
}
