using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// The six things the game can be asked about who somebody is.
    ///
    /// Named so that a diagnostic can say which of them this build failed to answer without
    /// spelling the list out again at every call site. "Character archetype" is always written in
    /// full: "archetype" alone already means a situation archetype elsewhere in this project.
    /// </summary>
    public enum IdentityFacetKind
    {
        CharacterArchetype,
        Race,

        /// <summary>
        /// The game's work column. Named Work rather than Occupation because that is all the
        /// column has been proven to be - see <see cref="CharacterIdentity.Work"/>.
        /// </summary>
        Work,
        Hobby,
        Service,
        Institution
    }

    /// <summary>
    /// One answer the game gave about a character, in the game's own vocabulary.
    ///
    /// <see cref="VanillaId"/> is Elin's id carried verbatim. Nothing here mints an id, maps an
    /// unfamiliar row onto a familiar one, or normalises anything: an unrecognised id is carried
    /// through *as unrecognised*, which is still a stable discriminator and still honest.
    ///
    /// <see cref="IsKnown"/> is the whole of D017 at this scale. A facet the build did not answer
    /// is <see cref="Unknown"/> - never an empty string, never "none", never a plausible default.
    /// The constructor refuses to build a known facet out of an empty id for exactly that reason:
    /// there is no way to spell "unread" that reads like a measurement.
    /// </summary>
    public sealed class IdentityFacet
    {
        /// <summary>The build did not answer. The only thing an unread facet may be.</summary>
        public static readonly IdentityFacet Unknown = new IdentityFacet(false, string.Empty, string.Empty);

        private IdentityFacet(bool known, string vanillaId, string displayName)
        {
            IsKnown = known;
            VanillaId = vanillaId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        /// <summary>
        /// A facet the game answered. An empty or absent id is <see cref="Unknown"/> rather than a
        /// known-but-blank answer, so a caller can never be handed "" as somebody's occupation.
        /// </summary>
        public static IdentityFacet FromVanilla(string vanillaId, string displayName = null)
        {
            return string.IsNullOrEmpty(vanillaId)
                ? Unknown
                : new IdentityFacet(true, vanillaId, displayName);
        }

        public bool IsKnown { get; }

        /// <summary>Elin's own id. Empty when <see cref="IsKnown"/> is false, and meaningless then.</summary>
        public string VanillaId { get; }

        /// <summary>The game's own text for this id where it was readable. Never a substitute for the id.</summary>
        public string DisplayName { get; }

        /// <summary>One token for a log line: the id, its name where there is one, "?" when unread.</summary>
        public string Describe()
        {
            if (!IsKnown)
            {
                return "?";
            }

            return DisplayName.Length > 0 && DisplayName != VanillaId
                ? VanillaId + " ('" + DisplayName + "')"
                : VanillaId;
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Whether a service is actually on offer right now.
    ///
    /// Three states rather than a bool, because "this build did not say" and "the shop is shut"
    /// are different answers and only one of them is a reason to tell the player a trade is
    /// closed.
    /// </summary>
    public enum ServiceAvailability
    {
        Unknown,
        Offered,
        NotOffered
    }

    /// <summary>
    /// What the player or another actor can *get* from this character, and whether they can get it
    /// now.
    ///
    /// The kind and the availability are separate because a settlement can lose a service without
    /// the person changing: the shopkeeper is still the shopkeeper when the shop is shut.
    /// </summary>
    public sealed class ServiceRole
    {
        public static readonly ServiceRole Unknown =
            new ServiceRole(IdentityFacet.Unknown, ServiceAvailability.Unknown);

        public ServiceRole(IdentityFacet kind, ServiceAvailability availability)
        {
            Kind = kind ?? IdentityFacet.Unknown;
            Availability = Kind.IsKnown ? availability : ServiceAvailability.Unknown;
        }

        /// <summary>The game's own handle for the service, or unknown.</summary>
        public IdentityFacet Kind { get; }

        public ServiceAvailability Availability { get; }

        public bool IsKnown => Kind.IsKnown;

        public string Describe()
        {
            if (!IsKnown)
            {
                return "?";
            }

            return Kind.Describe() + " (" + Availability.ToString().ToLowerInvariant() + ")";
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Standing this character holds on somebody else's behalf: which body, which office, and how
    /// far up it where the game says so.
    ///
    /// Revocable and not the person's own - which is why it is read as its own facet and never
    /// folded into work. A dismissed guard still has whatever job they had.
    /// </summary>
    public sealed class InstitutionalRole
    {
        public InstitutionalRole(IdentityFacet body, IdentityFacet role, int rank, bool rankKnown)
        {
            Body = body ?? IdentityFacet.Unknown;
            Role = role ?? IdentityFacet.Unknown;
            RankKnown = rankKnown;
            Rank = rankKnown ? rank : 0;
        }

        public InstitutionalRole(IdentityFacet body, IdentityFacet role)
            : this(body, role, 0, false)
        {
        }

        /// <summary>The faction, guild or watch this standing belongs to, or unknown.</summary>
        public IdentityFacet Body { get; }

        /// <summary>The office itself - the game's own marker for guard, guild staff, and so on.</summary>
        public IdentityFacet Role { get; }

        /// <summary>Meaningless unless <see cref="RankKnown"/>. Zero is not a rank of zero.</summary>
        public int Rank { get; }

        public bool RankKnown { get; }

        public bool IsKnown => Body.IsKnown || Role.IsKnown;

        public string Describe()
        {
            string text = Role.Describe() + " of " + Body.Describe();
            return RankKnown ? text + " rank " + Rank : text;
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Elin's own answer to <em>who is this character</em>, read through the seam and nothing
    /// more.
    ///
    /// The identity counterpart of <see cref="HomeState"/>: one read-only observation, six
    /// separately typed facets, no BQ vocabulary anywhere in it. What a facet *means* - what
    /// somebody with that job plausibly knows, who is eligible for which role, what a lost service
    /// costs a town - is derived elsewhere and deliberately not here (VS 4.3).
    ///
    /// Three rules hold it together:
    ///
    /// The facets are separate because the simulation asks separate questions of them. Flattened
    /// into one tag list, a shopkeeper who is also in a guild becomes indistinguishable from
    /// somebody whose hobby is shopping.
    ///
    /// A facet the build did not answer is unknown, and its neighbours are unaffected. Race going
    /// unreadable after a patch must not take work, hobby and institutional standing with it, and
    /// an actor whose every facet is unknown is still a full participant on the strength of
    /// presence, class, kind and history - somebody the world has not told BQ anything else about.
    ///
    /// Nothing here is persisted. This is a live read on the same terms as the Home snapshot
    /// (D004, D005): a save that has been away for a patch cycle must not carry a stale claim
    /// about who somebody is. A consumer may hold it for one pass - one generation run, one
    /// casting decision, one scene - and asks again after a zone change, a save or a load.
    /// </summary>
    public sealed class CharacterIdentity
    {
        private readonly List<IdentityFacet> _hobbies;
        private readonly List<InstitutionalRole> _institutions;

        internal CharacterIdentity(
            EntityId actor,
            IdentityFacet archetype,
            IdentityFacet race,
            IdentityFacet work,
            List<IdentityFacet> hobbies,
            bool hobbiesRead,
            ServiceRole service,
            List<InstitutionalRole> institutions,
            bool institutionsRead)
        {
            Actor = actor;
            CharacterArchetype = archetype ?? IdentityFacet.Unknown;
            Race = race ?? IdentityFacet.Unknown;
            Work = work ?? IdentityFacet.Unknown;
            _hobbies = hobbies ?? new List<IdentityFacet>();
            HobbiesRead = hobbiesRead;
            Service = service ?? ServiceRole.Unknown;
            _institutions = institutions ?? new List<InstitutionalRole>();
            InstitutionsRead = institutionsRead;
        }

        /// <summary>An observation about nobody in particular: every facet unread.</summary>
        public static CharacterIdentity UnknownFor(EntityId actor)
        {
            return new CharacterIdentityBuilder(actor).Build();
        }

        public EntityId Actor { get; }

        /// <summary>
        /// What kind of character this is - Little Sister, Punk, Bunny. The `SourceChara` kind, and
        /// not a job: it is a presentation and a role expectation.
        /// </summary>
        public IdentityFacet CharacterArchetype { get; }

        /// <summary>What they are. Embodiment, and frequently orthogonal to every other facet.</summary>
        public IdentityFacet Race { get; }

        /// <summary>
        /// The game's own work column for this character, carried verbatim.
        ///
        /// <b>Not proof of an occupation.</b> This is `SourceChara.job`, and live diagnostics have
        /// it answering with mechanical build and template values rather than with a trade:
        /// shopkeeper-like NPCs and horses alike reporting `predator`, nuns reporting `tourist`,
        /// bartenders reporting combat job templates. The column is honest about what Elin stores
        /// there and BQ reports it honestly, which is why the observation keeps it and why the
        /// name is Work - what the sheet says - rather than Occupation, which would be a claim
        /// about the character.
        ///
        /// So a consumer may not read this as "what they do for a living". What a work id is
        /// allowed to imply is <see cref="World.IdentityAffordances"/>'s single answer, and an id
        /// it does not recognise as a lived trade implies nothing at all.
        /// </summary>
        public IdentityFacet Work { get; }

        /// <summary>What they do when they are not working. Zero or more; the weakest of the six.</summary>
        public IReadOnlyList<IdentityFacet> Hobbies => _hobbies;

        /// <summary>
        /// Whether the hobby facet was answered at all. An empty list with this false is "not
        /// read"; an empty list with this true is the game listing none, which is a different
        /// thing and only the second of them is a fact.
        /// </summary>
        public bool HobbiesRead { get; }

        public ServiceRole Service { get; }

        /// <summary>Every standing this character holds that the build could read.</summary>
        public IReadOnlyList<InstitutionalRole> Institutions => _institutions;

        /// <summary>Whether the institutional facet was answered. See <see cref="HobbiesRead"/>.</summary>
        public bool InstitutionsRead { get; }

        /// <summary>Whether this facet carries an answer. Unknown never grants anything.</summary>
        public bool IsKnown(IdentityFacetKind facet)
        {
            switch (facet)
            {
                case IdentityFacetKind.CharacterArchetype:
                    return CharacterArchetype.IsKnown;
                case IdentityFacetKind.Race:
                    return Race.IsKnown;
                case IdentityFacetKind.Work:
                    return Work.IsKnown;
                case IdentityFacetKind.Hobby:
                    return HobbiesRead;
                case IdentityFacetKind.Service:
                    return Service.IsKnown;
                case IdentityFacetKind.Institution:
                    return InstitutionsRead;
                default:
                    return false;
            }
        }

        /// <summary>True when the build answered nothing at all about this character.</summary>
        public bool IsFullyUnknown => UnreadFacets.Count == FacetKinds.Length;

        /// <summary>
        /// Which facets this build did not answer, in facet order. The diagnostic prints it, and
        /// naming them is the difference between "this town has no shopkeepers" and "this build
        /// cannot see shops".
        /// </summary>
        public IReadOnlyList<IdentityFacetKind> UnreadFacets
        {
            get
            {
                List<IdentityFacetKind> unread = new List<IdentityFacetKind>();
                for (int i = 0; i < FacetKinds.Length; i++)
                {
                    if (!IsKnown(FacetKinds[i]))
                    {
                        unread.Add(FacetKinds[i]);
                    }
                }

                return unread;
            }
        }

        internal static readonly IdentityFacetKind[] FacetKinds =
            (IdentityFacetKind[])Enum.GetValues(typeof(IdentityFacetKind));

        /// <summary>
        /// One line per character, written so that a live log distinguishes an answer from a
        /// silence. Formatted here rather than in the plugin so that the honesty of the line the
        /// adapter prints can be tested with no game attached.
        /// </summary>
        public string Describe()
        {
            List<string> parts = new List<string>
            {
                "character archetype " + CharacterArchetype.Describe(),
                "race " + Race.Describe(),
                "work " + Work.Describe(),
                "hobby " + DescribeHobbies(),
                "service " + Service.Describe(),
                "institution " + DescribeInstitutions()
            };

            return string.Join(", ", parts.ToArray());
        }

        private string DescribeHobbies()
        {
            if (!HobbiesRead)
            {
                return "?";
            }

            if (_hobbies.Count == 0)
            {
                return "none listed";
            }

            List<string> named = new List<string>();
            for (int i = 0; i < _hobbies.Count; i++)
            {
                named.Add(_hobbies[i].Describe());
            }

            return string.Join("/", named.ToArray());
        }

        private string DescribeInstitutions()
        {
            if (!InstitutionsRead)
            {
                return "?";
            }

            if (_institutions.Count == 0)
            {
                return "none";
            }

            List<string> named = new List<string>();
            for (int i = 0; i < _institutions.Count; i++)
            {
                named.Add(_institutions[i].Describe());
            }

            return string.Join("/", named.ToArray());
        }

        public override string ToString() => Actor + ": " + Describe();
    }

    /// <summary>
    /// The one way a <see cref="CharacterIdentity"/> is put together.
    ///
    /// The live adapter and the headless reference implementation build observations through
    /// this, so "unread" means the same thing on both sides of the seam. A facet nobody set stays
    /// unknown, and there is no constructor that lets a caller assert a facet without an id.
    /// </summary>
    public sealed class CharacterIdentityBuilder
    {
        private readonly EntityId _actor;
        private readonly List<IdentityFacet> _hobbies = new List<IdentityFacet>();
        private readonly List<InstitutionalRole> _institutions = new List<InstitutionalRole>();
        private IdentityFacet _archetype = IdentityFacet.Unknown;
        private IdentityFacet _race = IdentityFacet.Unknown;
        private IdentityFacet _work = IdentityFacet.Unknown;
        private ServiceRole _service = ServiceRole.Unknown;
        private bool _hobbiesRead;
        private bool _institutionsRead;

        public CharacterIdentityBuilder(EntityId actor)
        {
            _actor = actor;
        }

        public CharacterIdentityBuilder WithCharacterArchetype(string vanillaId, string displayName = null)
        {
            _archetype = IdentityFacet.FromVanilla(vanillaId, displayName);
            return this;
        }

        public CharacterIdentityBuilder WithRace(string vanillaId, string displayName = null)
        {
            _race = IdentityFacet.FromVanilla(vanillaId, displayName);
            return this;
        }

        /// <summary>
        /// Records the game's work column verbatim. See <see cref="CharacterIdentity.Work"/>: this
        /// is what the sheet says, not an assertion that the character holds that trade.
        /// </summary>
        public CharacterIdentityBuilder WithWork(string vanillaId, string displayName = null)
        {
            _work = IdentityFacet.FromVanilla(vanillaId, displayName);
            return this;
        }

        /// <summary>
        /// Adds a hobby, and records that the hobby facet was answered. An id the game did not
        /// give is not a hobby and is dropped rather than added as a blank one.
        /// </summary>
        public CharacterIdentityBuilder AddHobby(string vanillaId, string displayName = null)
        {
            IdentityFacet hobby = IdentityFacet.FromVanilla(vanillaId, displayName);
            if (!hobby.IsKnown)
            {
                return this;
            }

            _hobbiesRead = true;
            for (int i = 0; i < _hobbies.Count; i++)
            {
                if (_hobbies[i].VanillaId == hobby.VanillaId)
                {
                    return this;
                }
            }

            _hobbies.Add(hobby);
            return this;
        }

        /// <summary>
        /// Says the hobby column was read and listed nothing. Distinct from never calling it,
        /// which leaves the facet unknown - the game saying "no hobbies" is a fact and the adapter
        /// failing to look is not.
        /// </summary>
        public CharacterIdentityBuilder WithHobbiesRead()
        {
            _hobbiesRead = true;
            return this;
        }

        public CharacterIdentityBuilder WithService(
            string vanillaId,
            string displayName = null,
            ServiceAvailability availability = ServiceAvailability.Unknown)
        {
            _service = new ServiceRole(IdentityFacet.FromVanilla(vanillaId, displayName), availability);
            return this;
        }

        /// <summary>
        /// Adds one standing, and records that the institutional facet was answered. A standing
        /// with neither a body nor an office is not a standing and is dropped.
        /// </summary>
        public CharacterIdentityBuilder AddInstitution(InstitutionalRole role)
        {
            if (role == null || !role.IsKnown)
            {
                return this;
            }

            _institutionsRead = true;
            _institutions.Add(role);
            return this;
        }

        public CharacterIdentityBuilder AddInstitution(
            string bodyId,
            string roleId,
            string bodyName = null,
            string roleName = null)
        {
            return AddInstitution(new InstitutionalRole(
                IdentityFacet.FromVanilla(bodyId, bodyName),
                IdentityFacet.FromVanilla(roleId, roleName)));
        }

        /// <summary>Says the institutional markers were read and this character holds nothing.</summary>
        public CharacterIdentityBuilder WithInstitutionsRead()
        {
            _institutionsRead = true;
            return this;
        }

        public CharacterIdentity Build()
        {
            return new CharacterIdentity(
                _actor,
                _archetype,
                _race,
                _work,
                new List<IdentityFacet>(_hobbies),
                _hobbiesRead,
                _service,
                new List<InstitutionalRole>(_institutions),
                _institutionsRead);
        }
    }
}
