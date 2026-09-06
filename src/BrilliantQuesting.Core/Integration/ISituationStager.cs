using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;

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

        /// <summary>
        /// The physical shape to build the place with - which authored piece stands for each part
        /// of it, where each one goes and what joins them - or null where the place is only being
        /// bound to one the game already made (BQ-140).
        ///
        /// The one thing on this seam with coordinates in it, and deliberately still not a map: no
        /// tiles, no objects, no doors. An adapter that cannot apply authored pieces answers
        /// <see cref="ISituationStager.StageSite"/> with nothing when this is set, and genesis then
        /// registers no site at all.
        /// </summary>
        public SiteStructure Structure { get; set; }
    }

    /// <summary>
    /// One bounded physical addition to a place that already exists (BQ-143).
    ///
    /// Deliberately not a second <see cref="SiteBlueprint"/>: nothing here says what the place is,
    /// who is in it or what it keeps, because all of that is already true of a place the game made
    /// and the simulation is not restating it. What crosses the seam is one authored piece, the
    /// ground it goes on in the site's own grid, and the handle of the place it goes into - the
    /// least an adapter needs to put one thing down and the most the simulation is entitled to ask
    /// for on a map a player has been walking around in.
    /// </summary>
    public sealed class SiteAdditionBlueprint
    {
        public SiteAdditionBlueprint(
            EntityId siteId,
            string zoneRef,
            string additionId,
            string pieceId,
            int x,
            int y,
            int width,
            int height)
        {
            SiteId = siteId;
            ZoneRef = zoneRef ?? string.Empty;
            AdditionId = additionId ?? string.Empty;
            PieceId = pieceId ?? string.Empty;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public EntityId SiteId { get; }

        /// <summary>The handle genesis bound the place to. The addition goes into that place only.</summary>
        public string ZoneRef { get; }

        /// <summary>
        /// What this addition is, as the one name both sides use for it.
        ///
        /// The same string the site records once the adapter has answered, so "has this already
        /// been applied" is one lookup rather than a resemblance check over geometry.
        /// </summary>
        public string AdditionId { get; }

        /// <summary>The authored piece to put down.</summary>
        public string PieceId { get; }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public override string ToString()
        {
            return AdditionId + " = " + PieceId + " at " + X + "," + Y + " (" + Width + "x" + Height + ")";
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

        /// <summary>
        /// Adds one authored piece to a place that already exists and returns the adapter's handle
        /// for what it made, or an empty string where this build cannot (BQ-143).
        ///
        /// The mirror of <see cref="StageSite"/> for a place already in the save, and it fails the
        /// same way: the empty string costs an addition that did not happen, and
        /// <see cref="BrilliantQuesting.World.SiteMutation"/> then records nothing, so nothing in
        /// the save claims a piece the map does not have. Whether the ground is free was asked
        /// before this was called (<see cref="IVanillaState.InspectGround"/>); an adapter is still
        /// free to refuse, and refusing is always safe.
        /// </summary>
        string ApplySiteAddition(SiteAdditionBlueprint blueprint);
    }

    /// <summary>Headless staging, for the laboratory and the tests.</summary>
    public sealed class SandboxStager : ISituationStager
    {
        private readonly SandboxVanillaState _vanilla;

        private readonly Dictionary<EntityId, SiteStructure> _structures =
            new Dictionary<EntityId, SiteStructure>();

        private readonly List<EntityId> _built = new List<EntityId>();

        private readonly List<string> _added = new List<string>();

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
            if (blueprint == null || blueprint.SiteId.IsNone)
            {
                return string.Empty;
            }

            if (blueprint.Structure != null)
            {
                _structures[blueprint.SiteId] = blueprint.Structure;
                _built.Add(blueprint.SiteId);
            }

            return blueprint.SiteId.Value;
        }

        /// <summary>
        /// The shape each place was built with, and one entry per act of building.
        ///
        /// Two collections rather than one because they answer different questions: what a place
        /// is, and how many times something built it. A site established twice is the defect
        /// BQ-087 exists to prevent, and it is only visible in the count.
        /// </summary>
        public SiteStructure StructureOf(EntityId siteId)
        {
            SiteStructure structure;
            return _structures.TryGetValue(siteId, out structure) ? structure : null;
        }

        public IReadOnlyList<EntityId> Built => _built;

        /// <summary>
        /// Headless, a place can always be added to: the laboratory has no map to disagree with.
        /// What it does keep is one entry per act of adding, because "applied exactly once" is a
        /// claim about how many times the adapter was asked, and nothing else can see that.
        /// </summary>
        public string ApplySiteAddition(SiteAdditionBlueprint blueprint)
        {
            if (blueprint == null || blueprint.SiteId.IsNone || blueprint.AdditionId.Length == 0)
            {
                return string.Empty;
            }

            _added.Add(blueprint.SiteId.Value + "/" + blueprint.AdditionId);
            return blueprint.SiteId.Value + ":" + blueprint.AdditionId;
        }

        /// <summary>One entry per addition actually applied, in order.</summary>
        public IReadOnlyList<string> Added => _added;
    }
}
