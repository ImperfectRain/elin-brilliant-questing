using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The vanilla reads every settlement affordance is built from.
    ///
    /// Deliberately a short list, and deliberately in one place: an archetype that needs a new
    /// number adds it here once, and every other archetype gets it for free rather than reaching
    /// past the profile into <see cref="IVanillaState"/> and reinterpreting it privately. Each
    /// entry has to be something an archetype already in the roadmap uses.
    /// </summary>
    public static class AffordanceReads
    {
        /// <summary>
        /// Taking without being caught, and noticing somebody who tried. Pickpocket and Stealth are
        /// the means half of petty theft; SpotHidden is what makes a bystander an actual witness
        /// rather than somebody who merely happened to be standing there.
        /// </summary>
        public static readonly VanillaSkill[] Skills =
        {
            VanillaSkill.Pickpocket,
            VanillaSkill.Stealth,
            VanillaSkill.SpotHidden
        };

        /// <summary>Hands quick enough to do it, and eyes good enough to see it done.</summary>
        public static readonly VanillaAttribute[] Attributes =
        {
            VanillaAttribute.Dexterity,
            VanillaAttribute.Perception
        };
    }

    /// <summary>
    /// What one local actor affords, as the game currently has them.
    ///
    /// An observation, never a judgement. Nothing here knows what a theft is: it records that
    /// somebody has fifteen orens, a Pickpocket of eight and nothing worth taking, and leaves it to
    /// an archetype to decide whether that is a thief. Keeping the split means the shortage,
    /// sanctuary and caravan archetypes read the same structure without inheriting theft's opinions
    /// about what the numbers mean.
    /// </summary>
    public sealed class ActorAffordances
    {
        private readonly Dictionary<VanillaSkill, int> _skills;
        private readonly Dictionary<VanillaAttribute, int> _attributes;
        private readonly List<ItemDescriptor> _carried;

        internal ActorAffordances(
            EntityId actorId,
            string name,
            NarrativeActorClass actorClass,
            NarrativeActorKind actorKind,
            SocialAgency socialAgency,
            EntityId zoneId,
            string occupation,
            IReadOnlyCollection<string> roles,
            IdentityAffordances identity,
            int money,
            List<ItemDescriptor> carried,
            Dictionary<VanillaSkill, int> skills,
            Dictionary<VanillaAttribute, int> attributes,
            FamiliarityReading familiarity,
            EarlyContact earlyContact)
        {
            ActorId = actorId;
            Name = name;
            ActorClass = actorClass;
            ActorKind = actorKind;
            SocialAgency = socialAgency;
            ZoneId = zoneId;
            Occupation = occupation ?? string.Empty;
            Roles = roles;
            Identity = identity ?? IdentityAffordances.Nothing;
            Money = money;
            _carried = carried;
            _skills = skills;
            _attributes = attributes;
            Familiarity = familiarity;
            EarlyContact = earlyContact;

            for (int i = 0; i < carried.Count; i++)
            {
                ItemDescriptor item = carried[i];
                CarriedValue += item.Value;
                if (MostValuableCarried == null
                    || item.Value > MostValuableCarried.Value
                    || (item.Value == MostValuableCarried.Value
                        && string.CompareOrdinal(item.Id.Value, MostValuableCarried.Id.Value) < 0))
                {
                    MostValuableCarried = item;
                }
            }

            IsCommercial = Identity.Service.IsProvider;
        }

        public EntityId ActorId { get; }

        public string Name { get; }

        /// <summary>How far the mod may reach into this person. Not a statement about who they are.</summary>
        public NarrativeActorClass ActorClass { get; }

        /// <summary>Broad narrative casting kind. Not a mutation-policy classification.</summary>
        public NarrativeActorKind ActorKind { get; }

        /// <summary>Whether ordinary social roles can treat this actor as a speaker/participant.</summary>
        public SocialAgency SocialAgency { get; }

        /// <summary>Where the game has them standing, which is the whole of "present" today.</summary>
        public EntityId ZoneId { get; }

        /// <summary>
        /// How well the player already knows them, and why (BQ-114). A stranger reads zero, which
        /// is what most of a town reads and is not a defect in them.
        /// </summary>
        public FamiliarityReading Familiarity { get; }

        /// <summary>Shorthand for the common question: has the player any history with them at all.</summary>
        public bool KnownToPlayer => Familiarity.IsKnown;

        /// <summary>
        /// BQ-115. Whether this save elected them as one of the handful of faces it keeps bringing
        /// back, and on what ground. Null for almost everybody, which is the ordinary answer.
        ///
        /// Deliberately separate from <see cref="Familiarity"/>: one is history the player made,
        /// the other is a casting decision made before they made any.
        /// </summary>
        public EarlyContact EarlyContact { get; }

        /// <summary>Whether the player will recognise them at all - through history or through casting.</summary>
        public bool RecognisableToPlayer => KnownToPlayer || EarlyContact != null;

        public string Occupation { get; }

        /// <summary>Standing the world model records - guard, guild personnel, and whatever else grants one.</summary>
        public IReadOnlyCollection<string> Roles { get; }

        /// <summary>
        /// What this actor's identity makes plausible, who it makes them eligible to be, and what it
        /// puts at risk (BQ-145).
        ///
        /// The generator's one route to what a job means. It does not read the identity observation
        /// itself and holds no occupation vocabulary of its own: two ideas of what a shopkeeper is
        /// would mean a face the generator treats as commercial and the early-contact pass does not.
        /// Empty for somebody the game and this simulation both declined to describe, which is an
        /// answer and not a gap.
        /// </summary>
        public IdentityAffordances Identity { get; }

        /// <summary>
        /// Whether this person handles goods and money with strangers for a living. BQ-145's
        /// derived service capability, so a modded shopkeeper is one too and nothing here has to
        /// keep a list of names.
        /// </summary>
        public bool IsCommercial { get; }

        public int Money { get; }

        public IReadOnlyList<ItemDescriptor> Carried => _carried;

        /// <summary>Total worth of what they are carrying. Zero for somebody carrying nothing.</summary>
        public int CarriedValue { get; }

        /// <summary>
        /// The single best thing on them, by value and then by id so the answer does not depend on
        /// the order the game happened to hand back an inventory. Null when they carry nothing.
        /// </summary>
        public ItemDescriptor MostValuableCarried { get; }

        public int Skill(VanillaSkill skill) => _skills.TryGetValue(skill, out int value) ? value : 0;

        public int Attribute(VanillaAttribute attribute) =>
            _attributes.TryGetValue(attribute, out int value) ? value : 0;

    }

    /// <summary>
    /// A settlement read as affordances: who is here, what they have, what they can do, and how
    /// they stand relative to each other.
    ///
    /// The generic half of BQ-039. It answers "what is true of this place" and nothing else - it
    /// runs no archetype's arithmetic, holds no archetype's thresholds, and never writes to the
    /// game. A place earns its situations from what this reports; nothing in here has ever heard of
    /// a town id or a zone name.
    /// </summary>
    public sealed class LocalAffordanceProfile
    {
        private readonly List<ActorAffordances> _actors = new List<ActorAffordances>();
        private readonly Dictionary<EntityId, ActorAffordances> _byId = new Dictionary<EntityId, ActorAffordances>();
        private readonly List<string> _features = new List<string>();

        private LocalAffordanceProfile(EntityId zoneId)
        {
            ZoneId = zoneId;
        }

        public EntityId ZoneId { get; }

        /// <summary>Everybody eligible to take part in a situation here, in a stable order.</summary>
        public IReadOnlyList<ActorAffordances> Actors => _actors;

        /// <summary>
        /// What the middle of this place has in its purse.
        ///
        /// The one number that makes a mark a mark *here*: eight hundred orens is conspicuous in a
        /// hamlet of subsistence farmers and unremarkable in a merchant quarter. Reading wealth
        /// against the local middle rather than against a fixed figure is what lets two settlements
        /// produce different situations without either of them being named anywhere.
        /// </summary>
        public int MedianMoney { get; private set; }

        /// <summary>Everything carried by everybody here, added up.</summary>
        public int TotalCarriedValue { get; private set; }

        public int SocialActorCount { get; private set; }

        public int OtherLivingActorCount { get; private set; }

        /// <summary>Human-readable summary lines, for the inspector and the live log.</summary>
        public IReadOnlyList<string> Features => _features;

        public ActorAffordances Of(EntityId actorId) =>
            _byId.TryGetValue(actorId, out ActorAffordances found) ? found : null;

        public static LocalAffordanceProfile Read(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            LocalAffordanceProfile profile = new LocalAffordanceProfile(zoneId);
            if (world == null || vanilla == null || zoneId.IsNone)
            {
                return profile;
            }

            // Read once for the settlement: the generator asks about every ordered pair here, and
            // the player's history does not change between two of them.
            PlayerFamiliarity familiarity = PlayerFamiliarity.Read(world, vanilla);

            // BQ-115, read on the same terms and for the same reason. In a save the mod has only
            // just attached to the reading above is empty for the whole town, and this is the only
            // thing that can tell one face here from another.
            EarlyContactCast earlyContacts = EarlyContacts.Elect(world, vanilla, zoneId);

            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zoneId);
            for (int i = 0; i < present.Count; i++)
            {
                EntityId actor = present[i];
                if (actor.IsNone || actor == vanilla.PlayerId || !vanilla.IsAlive(actor))
                {
                    continue;
                }

                NarrativeNpc npc = world.Registry.GetNpc(actor);
                NarrativeActorClass actorClass = vanilla.GetActorClass(actor);
                if (npc == null || actorClass == NarrativeActorClass.Unknown || profile._byId.ContainsKey(actor))
                {
                    continue;
                }

                NarrativeActorKind actorKind = vanilla.GetActorKind(actor);
                SocialAgency socialAgency = vanilla.GetSocialAgency(actor);
                Dictionary<VanillaSkill, int> skills = new Dictionary<VanillaSkill, int>();
                for (int s = 0; s < AffordanceReads.Skills.Length; s++)
                {
                    VanillaSkill skill = AffordanceReads.Skills[s];
                    skills[skill] = vanilla.GetSkill(actor, skill);
                }

                Dictionary<VanillaAttribute, int> attributes = new Dictionary<VanillaAttribute, int>();
                for (int a = 0; a < AffordanceReads.Attributes.Length; a++)
                {
                    VanillaAttribute attribute = AffordanceReads.Attributes[a];
                    attributes[attribute] = vanilla.GetAttribute(actor, attribute);
                }

                List<ItemDescriptor> carried = new List<ItemDescriptor>();
                IReadOnlyList<ItemDescriptor> inventory = vanilla.GetInventory(actor);
                for (int item = 0; item < inventory.Count; item++)
                {
                    if (inventory[item] != null)
                    {
                        carried.Add(inventory[item]);
                    }
                }

                ActorAffordances affordances = new ActorAffordances(
                    actor,
                    world.Registry.NameOf(actor),
                    actorClass,
                    actorKind,
                    socialAgency,
                    zoneId,
                    npc.Occupation,
                    npc.Roles,
                    IdentityAffordances.Of(npc, vanilla),
                    vanilla.GetMoney(actor),
                    carried,
                    skills,
                    attributes,
                    familiarity.Of(actor),
                    earlyContacts.Of(actor));

                profile._actors.Add(affordances);
                profile._byId.Add(actor, affordances);
            }

            profile.Summarize();
            return profile;
        }

        private void Summarize()
        {
            int[] purses = new int[_actors.Count];
            int commercial = 0;
            int carrying = 0;
            int known = 0;
            int elected = 0;
            for (int i = 0; i < _actors.Count; i++)
            {
                ActorAffordances actor = _actors[i];
                purses[i] = actor.Money;
                TotalCarriedValue += actor.CarriedValue;
                if (actor.IsCommercial)
                {
                    commercial++;
                }

                if (actor.SocialAgency == SocialAgency.Full)
                {
                    SocialActorCount++;
                }
                else
                {
                    OtherLivingActorCount++;
                }

                if (actor.MostValuableCarried != null)
                {
                    carrying++;
                }

                if (actor.KnownToPlayer)
                {
                    known++;
                }

                if (actor.EarlyContact != null)
                {
                    elected++;
                }
            }

            Array.Sort(purses);
            MedianMoney = purses.Length == 0
                ? 0
                : purses.Length % 2 == 1
                    ? purses[purses.Length / 2]
                    : (purses[purses.Length / 2 - 1] + purses[purses.Length / 2]) / 2;

            _features.Add("locals present: " + _actors.Count);
            _features.Add("social locals: " + SocialActorCount);
            _features.Add("other living locals: " + OtherLivingActorCount);
            _features.Add("locals carrying something: " + carrying);
            _features.Add("carried value here: " + TotalCarriedValue);
            _features.Add("median local purse: " + MedianMoney);
            _features.Add("commercial locals: " + commercial);
            _features.Add("locals the player knows: " + known);
            _features.Add("early contacts elected here: " + elected);
        }
    }
}
