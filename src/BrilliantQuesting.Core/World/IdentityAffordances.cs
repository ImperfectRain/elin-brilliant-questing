using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The subjects an identity makes it plausible somebody knows or cares about.
    ///
    /// Deliberately short, and deliberately closed. Every member is here because a consumer that
    /// exists today asks for it - cultivation, alchemy and public order are the three lenses
    /// <see cref="ActorLocalInterpreter"/> disagrees along, trade is what decides whether somebody
    /// handles goods and money with strangers, and craft is the one large block of vanilla work
    /// that would otherwise derive nothing at all (the step's own worked example, "a brewer
    /// plausibly knows who buys ale here", is a craft one). Widening this list is how a derivation
    /// turns into a taxonomy nobody consumes, so a new member arrives with the consumer that needs
    /// it and not before.
    /// </summary>
    public enum IdentityDomain
    {
        /// <summary>Growing things and keeping animals: land work, and what goes wrong with it.</summary>
        Cultivation,
        Alchemy,
        Craft,
        Trade,
        PublicOrder
    }

    /// <summary>
    /// What an identity puts at risk - not what somebody values, which is
    /// <see cref="ValueProfile"/>'s answer and stays independent of this one.
    ///
    /// Identity contributes candidates; the value profile weighs them against everything else the
    /// character is (CD 6.1). Two shopkeepers may care very differently about the same shop.
    /// </summary>
    public enum IdentityStakeKind
    {
        /// <summary>A trade to lose.</summary>
        Livelihood,

        /// <summary>A going concern that can close.</summary>
        Business,

        /// <summary>A standing somebody else grants and can withdraw.</summary>
        Standing
    }

    /// <summary>
    /// A role an identity makes somebody *eligible* for. Never a preference for a character kind:
    /// nobody is a better Accuser for being a Punk, and nothing here says who should be cast, only
    /// who could be without the world contradicting itself.
    /// </summary>
    public enum IdentityRole
    {
        /// <summary>Entitled to act on the settlement's behalf - a watch office, not a temperament.</summary>
        Authority,

        /// <summary>Speaks for, or is answerable to, an organised body.</summary>
        GuildStanding,

        /// <summary>Actually runs the service, as opposed to merely being of that kind.</summary>
        ServiceOperator
    }

    /// <summary>
    /// Where a derived affordance came from: the game, or this simulation's own authorship.
    ///
    /// The distinction matters for honesty rather than for arithmetic. A staged miller is a miller
    /// because BQ made her one, and saying so is different from claiming Elin's character sheet
    /// said it. An unread vanilla facet still contributes nothing - the authored source is BQ's own
    /// state, not a fallback guess about somebody the game declined to describe.
    /// </summary>
    public enum IdentityOrigin
    {
        Observed,
        Authored
    }

    /// <summary>
    /// The one facet, and the one id in it, behind a derived affordance.
    ///
    /// Every weight this derivation produces carries at least one of these, which is the whole of
    /// the explainability requirement: an identity-derived number a report cannot attribute to a
    /// facet is a number nobody can argue with.
    /// </summary>
    public sealed class IdentityFacetReference
    {
        public IdentityFacetReference(IdentityFacetKind facet, IdentityOrigin origin, string vanillaId)
        {
            Facet = facet;
            Origin = origin;
            VanillaId = vanillaId ?? string.Empty;
        }

        public IdentityFacetKind Facet { get; }

        public IdentityOrigin Origin { get; }

        /// <summary>The id the answer came in, carried verbatim. Never normalised or minted.</summary>
        public string VanillaId { get; }

        /// <summary>"work 'farmer'", or "authored work 'miller'". Named facet, named id, always.</summary>
        public string Describe()
        {
            string facet = FacetName(Facet);
            string prefix = Origin == IdentityOrigin.Authored ? "authored " : string.Empty;
            return prefix + facet + " '" + VanillaId + "'";
        }

        internal static string FacetName(IdentityFacetKind facet)
        {
            switch (facet)
            {
                case IdentityFacetKind.CharacterArchetype:
                    return "character archetype";
                case IdentityFacetKind.Race:
                    return "race";
                case IdentityFacetKind.Work:
                    return "work";
                case IdentityFacetKind.Hobby:
                    return "hobby";
                case IdentityFacetKind.Service:
                    return "service";
                case IdentityFacetKind.Institution:
                    return "institution";
                default:
                    return facet.ToString().ToLowerInvariant();
            }
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// One subject this identity makes plausible, how plausible, and which facets say so.
    ///
    /// <see cref="Plausibility"/> is 0..1 and means "how readily this identity accounts for
    /// somebody knowing or caring about this". It is not a confidence that they *do*: plausible
    /// knowledge is not knowledge, and nothing downstream may turn one of these into a fact.
    /// </summary>
    public sealed class IdentityDomainAffordance
    {
        private readonly List<IdentityFacetReference> _sources;

        internal IdentityDomainAffordance(
            IdentityDomain domain,
            double plausibility,
            List<IdentityFacetReference> sources)
        {
            Domain = domain;
            Plausibility = plausibility;
            _sources = sources ?? new List<IdentityFacetReference>();
        }

        public IdentityDomain Domain { get; }

        public double Plausibility { get; }

        /// <summary>Never empty. An affordance nothing supports is not created at all.</summary>
        public IReadOnlyList<IdentityFacetReference> Sources => _sources;

        public string Describe()
        {
            return DomainName(Domain) + " " + Plausibility.ToString("0.00") + " (" + DescribeSources(_sources) + ")";
        }

        /// <summary>Spelled out, because a score term is read by a person and "publicorder" is not a word.</summary>
        internal static string DomainName(IdentityDomain domain)
        {
            return domain == IdentityDomain.PublicOrder ? "public order" : domain.ToString().ToLowerInvariant();
        }

        internal static string DescribeSources(IReadOnlyList<IdentityFacetReference> sources)
        {
            if (sources == null || sources.Count == 0)
            {
                return "no identity facet";
            }

            List<string> named = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                named.Add(sources[i].Describe());
            }

            return string.Join(", ", named.ToArray());
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Something this identity exposes to loss, and the facet that exposes it.
    ///
    /// <see cref="Exposure"/> is ordinal rather than calibrated: it ranks how much of somebody is
    /// on the table, and the goal pipeline weighs it against the rest of the character rather than
    /// reading it as a probability.
    /// </summary>
    public sealed class IdentityStake
    {
        internal IdentityStake(IdentityStakeKind kind, double exposure, IdentityFacetReference source)
        {
            Kind = kind;
            Exposure = exposure;
            Source = source;
        }

        public IdentityStakeKind Kind { get; }

        public double Exposure { get; }

        public IdentityFacetReference Source { get; }

        public string Describe()
        {
            return Kind.ToString().ToLowerInvariant() + " " + Exposure.ToString("0.00")
                   + " (" + Source.Describe() + ")";
        }

        public override string ToString() => Describe();
    }

    /// <summary>One role this identity qualifies somebody for, and the facet that qualifies them.</summary>
    public sealed class IdentityRoleEligibility
    {
        internal IdentityRoleEligibility(IdentityRole role, IdentityFacetReference source)
        {
            Role = role;
            Source = source;
        }

        public IdentityRole Role { get; }

        public IdentityFacetReference Source { get; }

        public string Describe()
        {
            return RoleName(Role) + " (" + Source.Describe() + ")";
        }

        internal static string RoleName(IdentityRole role)
        {
            switch (role)
            {
                case IdentityRole.Authority:
                    return "authority";
                case IdentityRole.GuildStanding:
                    return "guild standing";
                case IdentityRole.ServiceOperator:
                    return "service operator";
                default:
                    return role.ToString().ToLowerInvariant();
            }
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// Whether this identity means somebody can be traded with, and whether they can be right now.
    ///
    /// Kind and availability stay apart for the same reason they do at the seam: the shopkeeper is
    /// still the shopkeeper when the shop is shut, and a build that cannot see opening state has
    /// not told anybody the shop is closed. <see cref="AvailableNow"/> and
    /// <see cref="KnownUnavailable"/> are therefore both false on an unread availability, which is
    /// the only pair of answers that does not invent one.
    /// </summary>
    public sealed class IdentityServiceCapability
    {
        internal static readonly IdentityServiceCapability None = new IdentityServiceCapability(
            false, ServiceAvailability.Unknown, null);

        internal IdentityServiceCapability(
            bool isProvider,
            ServiceAvailability availability,
            IdentityFacetReference source)
        {
            IsProvider = isProvider;
            Availability = isProvider ? availability : ServiceAvailability.Unknown;
            Source = isProvider ? source : null;
        }

        /// <summary>Whether they handle goods or money with strangers for a living.</summary>
        public bool IsProvider { get; }

        public ServiceAvailability Availability { get; }

        /// <summary>The game says the service is on offer. False on an unread availability.</summary>
        public bool AvailableNow => IsProvider && Availability == ServiceAvailability.Offered;

        /// <summary>The game says it is not. Also false on an unread availability.</summary>
        public bool KnownUnavailable => IsProvider && Availability == ServiceAvailability.NotOffered;

        /// <summary>The facet that made them a provider. Null when they are not one.</summary>
        public IdentityFacetReference Source { get; }

        public string Describe()
        {
            if (!IsProvider)
            {
                return "none";
            }

            return "provider, " + Availability.ToString().ToLowerInvariant()
                   + " (" + Source.Describe() + ")";
        }

        public override string ToString() => Describe();
    }

    /// <summary>
    /// BQ-145. The one derivation of what a vanilla identity makes plausible, and the only thing
    /// downstream systems are allowed to read when they want to know what somebody's work, service
    /// or standing implies.
    ///
    /// BQ-144 answers *who the game says this is*. This answers *what that makes plausible, who it
    /// makes eligible, and what it puts at risk* - once, so that "a brewer plausibly knows who buys
    /// ale here" is written here rather than re-derived inside generation, interpretation, casting
    /// and vocabulary. The two fail differently and that is why they are separate: the observation
    /// is wrong when the adapter reports the wrong facet; this is wrong when a facet is allowed to
    /// dictate a decision.
    ///
    /// Five rules hold it together, and each of them is a failure mode this file exists to prevent:
    ///
    /// **Plausible knowledge is not knowledge.** Nothing here adds a fact, teaches anybody
    /// anything, or touches the knowledge graph. It says what is worth *asking* somebody and what
    /// they could credibly turn out to know; the knowledge system still has to have given it to
    /// them (CD 6.1).
    ///
    /// **Identity never dictates personality.** BQ-056 ... BQ-060 do not read this and must not.
    /// A Punk is not aggressive because they are a Punk. Identity changes what is plausible,
    /// available and at stake - never what somebody is like, and the interesting characters are the
    /// ones where the two disagree.
    ///
    /// **Identity never feeds mutation policy.** How far the mod may reach into somebody is
    /// <see cref="NarrativeActorClass"/>'s answer and only ever will be (BQ-031). A costume is not
    /// a permission.
    ///
    /// **An unread facet contributes nothing.** Not a default occupation, not an ordinary citizen,
    /// not a stereotype standing in for the answer the build declined to give (D017). An actor
    /// whose every facet is unknown derives <see cref="Nothing"/>, and that is a complete answer.
    ///
    /// **Race and character archetype derive nothing at all.** They are the two facets a
    /// stereotype would arrive through, and neither one tells anybody what somebody can do, is
    /// entitled to, or would lose. They stay observable at the seam and stay out of every weight
    /// here. If a consumer ever genuinely needs one, it arrives as a named affordance with a named
    /// consumer, not as a general licence to read a species.
    ///
    /// Nothing derived is persisted. This is a projection of a live read (VS 4.4) and is recomputed
    /// per pass on the same terms as the observation it comes from.
    /// </summary>
    public sealed class IdentityAffordances
    {
        /// <summary>
        /// What an identity nobody could read implies: nothing. The answer for a fully unknown
        /// observation, and the answer a consumer gets when no game is attached.
        /// </summary>
        public static readonly IdentityAffordances Nothing = new IdentityAffordances(
            EntityId.None,
            new List<IdentityDomainAffordance>(),
            new List<IdentityDomainAffordance>(),
            new List<IdentityRoleEligibility>(),
            new List<IdentityStake>(),
            IdentityServiceCapability.None,
            new List<IdentityFacetKind>());

        private readonly List<IdentityDomainAffordance> _knowledge;
        private readonly List<IdentityDomainAffordance> _interests;
        private readonly List<IdentityRoleEligibility> _roles;
        private readonly List<IdentityStake> _stakes;
        private readonly List<IdentityFacetKind> _contributing;

        private IdentityAffordances(
            EntityId actor,
            List<IdentityDomainAffordance> knowledge,
            List<IdentityDomainAffordance> interests,
            List<IdentityRoleEligibility> roles,
            List<IdentityStake> stakes,
            IdentityServiceCapability service,
            List<IdentityFacetKind> contributing)
        {
            Actor = actor;
            _knowledge = knowledge;
            _interests = interests;
            _roles = roles;
            _stakes = stakes;
            Service = service;
            _contributing = contributing;
        }

        public EntityId Actor { get; }

        /// <summary>What this identity makes it plausible they know about. Not what they know.</summary>
        public IReadOnlyList<IdentityDomainAffordance> PlausibleKnowledge => _knowledge;

        /// <summary>What this identity makes it plausible they care about. Not what they value.</summary>
        public IReadOnlyList<IdentityDomainAffordance> PlausibleInterests => _interests;

        /// <summary>Roles this identity qualifies them for. Eligibility only, never a preference.</summary>
        public IReadOnlyList<IdentityRoleEligibility> RoleEligibility => _roles;

        /// <summary>What this identity exposes to loss. Candidates for a value profile to weigh.</summary>
        public IReadOnlyList<IdentityStake> Stakes => _stakes;

        public IdentityServiceCapability Service { get; }

        /// <summary>Which facets actually produced something, in facet order. Empty is the ordinary answer.</summary>
        public IReadOnlyList<IdentityFacetKind> ContributingFacets => _contributing;

        /// <summary>True when this identity implies nothing. Not a defect, and not a stereotype's cue.</summary>
        public bool IsEmpty =>
            _knowledge.Count == 0 && _interests.Count == 0 && _roles.Count == 0
            && _stakes.Count == 0 && !Service.IsProvider;

        public double PlausibleKnowledgeOf(IdentityDomain domain) => Weight(_knowledge, domain);

        public double PlausibleInterestIn(IdentityDomain domain) => Weight(_interests, domain);

        public bool IsEligibleFor(IdentityRole role) => EligibilityFor(role) != null;

        public IdentityRoleEligibility EligibilityFor(IdentityRole role)
        {
            for (int i = 0; i < _roles.Count; i++)
            {
                if (_roles[i].Role == role)
                {
                    return _roles[i];
                }
            }

            return null;
        }

        public double ExposureTo(IdentityStakeKind kind)
        {
            for (int i = 0; i < _stakes.Count; i++)
            {
                if (_stakes[i].Kind == kind)
                {
                    return _stakes[i].Exposure;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// The name a score term gets when a consumer weighs plausible knowledge of a domain.
        ///
        /// Reads "plausible knowledge cultivation (work 'farmer')", or "... (no identity facet)"
        /// when nothing supports it - which is what makes an identity-derived weight of zero as
        /// explainable as one that fired.
        /// </summary>
        public string ExplainKnowledge(IdentityDomain domain) => Explain("plausible knowledge", _knowledge, domain);

        public string ExplainInterest(IdentityDomain domain) => Explain("plausible interest", _interests, domain);

        /// <summary>
        /// The name a score term gets when a consumer weighs eligibility for a role. Same contract
        /// as <see cref="ExplainKnowledge"/>: the facet is named whether or not the term fired.
        /// </summary>
        public string ExplainEligibility(IdentityRole role)
        {
            IdentityRoleEligibility eligibility = EligibilityFor(role);
            return "identity eligibility " + IdentityRoleEligibility.RoleName(role) + " ("
                   + (eligibility == null ? "no identity facet" : eligibility.Source.Describe()) + ")";
        }

        /// <summary>
        /// Every derived affordance as one line each, facet named, for the inspector and the live
        /// log. Empty when the identity implies nothing, which reports as such rather than silently.
        /// </summary>
        public IReadOnlyList<string> Explain()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < _knowledge.Count; i++)
            {
                lines.Add("plausible knowledge " + _knowledge[i].Describe());
            }

            for (int i = 0; i < _interests.Count; i++)
            {
                lines.Add("plausible interest " + _interests[i].Describe());
            }

            for (int i = 0; i < _roles.Count; i++)
            {
                lines.Add("eligible for " + _roles[i].Describe());
            }

            for (int i = 0; i < _stakes.Count; i++)
            {
                lines.Add("at stake " + _stakes[i].Describe());
            }

            if (Service.IsProvider)
            {
                lines.Add("service " + Service.Describe());
            }

            return lines;
        }

        private static string Explain(
            string label,
            List<IdentityDomainAffordance> from,
            IdentityDomain domain)
        {
            for (int i = 0; i < from.Count; i++)
            {
                if (from[i].Domain == domain)
                {
                    return label + " " + IdentityDomainAffordance.DomainName(domain) + " ("
                           + IdentityDomainAffordance.DescribeSources(from[i].Sources) + ")";
                }
            }

            return label + " " + IdentityDomainAffordance.DomainName(domain) + " (no identity facet)";
        }

        private static double Weight(List<IdentityDomainAffordance> from, IdentityDomain domain)
        {
            for (int i = 0; i < from.Count; i++)
            {
                if (from[i].Domain == domain)
                {
                    return from[i].Plausibility;
                }
            }

            return 0.0;
        }

        // -- derivation ----------------------------------------------------------------------

        /// <summary>
        /// What a BQ-144 observation implies, and nothing else. The form the adapter and the
        /// authority intake use, because reconciling standing must be about what the game said and
        /// never about what this simulation wrote down earlier.
        /// </summary>
        public static IdentityAffordances Derive(CharacterIdentity identity)
        {
            return Derive(identity, null, null);
        }

        /// <summary>
        /// The canonical read for a live pass: what the game says about this character, plus what
        /// BQ itself authored about them where the game said nothing.
        ///
        /// The authored half is not a fallback for an unread facet - it is this simulation's own
        /// state, and a staged miller is a miller because BQ made her one. It is marked
        /// <see cref="IdentityOrigin.Authored"/> everywhere it appears so a report never passes
        /// BQ's authorship off as Elin's answer, and an actor with neither still derives nothing.
        /// </summary>
        public static IdentityAffordances Of(NarrativeNpc npc, IVanillaState vanilla)
        {
            if (npc == null)
            {
                return Nothing;
            }

            CharacterIdentity observed = vanilla == null
                ? CharacterIdentity.UnknownFor(npc.Id)
                : vanilla.GetCharacterIdentity(npc.Id);

            return Derive(observed, npc.Occupation, npc.Roles);
        }

        /// <summary>What this simulation authored about somebody, with no game attached.</summary>
        public static IdentityAffordances Of(NarrativeNpc npc) => Of(npc, null);

        private static IdentityAffordances Derive(
            CharacterIdentity identity,
            string authoredWork,
            IReadOnlyCollection<string> authoredRoles)
        {
            if (identity == null)
            {
                return Nothing;
            }

            Accumulator acc = new Accumulator(identity.Actor);

            // Work. The observation first; BQ's own authorship only where the game said nothing,
            // so an authored label can never overrule what Elin actually reports.
            IdentityFacetReference work = null;
            if (identity.Work.IsKnown)
            {
                work = new IdentityFacetReference(
                    IdentityFacetKind.Work, IdentityOrigin.Observed, identity.Work.VanillaId);
            }
            else if (!string.IsNullOrEmpty(authoredWork))
            {
                work = new IdentityFacetReference(
                    IdentityFacetKind.Work, IdentityOrigin.Authored, authoredWork);
            }

            if (work != null)
            {
                acc.AddDomains(work, WorkKnowledge, WorkInterest);
                acc.AddStake(IdentityStakeKind.Livelihood, LivelihoodExposure, work);
                acc.AddRoles(work);
            }

            // Hobby. The weakest facet, and weighted as such: what somebody does on their own time
            // is a better guide to what they will talk about than to what they can be relied on for.
            for (int i = 0; i < identity.Hobbies.Count; i++)
            {
                IdentityFacetReference hobby = new IdentityFacetReference(
                    IdentityFacetKind.Hobby, IdentityOrigin.Observed, identity.Hobbies[i].VanillaId);
                acc.AddDomains(hobby, HobbyKnowledge, HobbyInterest);
            }

            // Service. A read service makes somebody a provider outright; otherwise a trade-reading
            // work facet does, which is what keeps a staged shopkeeper and an observed one the same
            // kind of person.
            IdentityFacetReference service = null;
            ServiceAvailability availability = ServiceAvailability.Unknown;
            if (identity.Service.IsKnown)
            {
                service = new IdentityFacetReference(
                    IdentityFacetKind.Service, IdentityOrigin.Observed, identity.Service.Kind.VanillaId);
                availability = identity.Service.Availability;
                acc.AddDomains(service, ServiceKnowledge, ServiceInterest);
                acc.AddStake(IdentityStakeKind.Business, BusinessExposure, service);
                acc.AddRole(IdentityRole.ServiceOperator, service);
            }
            else if (work != null && Matches(work.VanillaId, IdentityDomain.Trade))
            {
                service = work;
                acc.AddRole(IdentityRole.ServiceOperator, work);
                acc.AddStake(IdentityStakeKind.Business, BusinessExposure, work);
            }

            // Institutional standing. Observed offices first; where the facet went unread, whatever
            // standing this simulation granted itself. Never both, so a live guard's standing is
            // attributed to the game rather than to the row BQ wrote from it.
            if (identity.InstitutionsRead)
            {
                for (int i = 0; i < identity.Institutions.Count; i++)
                {
                    InstitutionalRole institution = identity.Institutions[i];
                    if (!institution.Role.IsKnown)
                    {
                        continue;
                    }

                    AddInstitution(acc, new IdentityFacetReference(
                        IdentityFacetKind.Institution, IdentityOrigin.Observed, institution.Role.VanillaId));
                }
            }
            else if (authoredRoles != null)
            {
                foreach (string role in authoredRoles)
                {
                    if (string.IsNullOrEmpty(role))
                    {
                        continue;
                    }

                    AddInstitution(acc, new IdentityFacetReference(
                        IdentityFacetKind.Institution, IdentityOrigin.Authored, role));
                }
            }

            // Race and character archetype derive nothing, on purpose. See the type comment: they
            // are the two facets a stereotype would arrive through, and neither answers what
            // somebody can do, is entitled to, or stands to lose.

            return acc.Build(new IdentityServiceCapability(service != null, availability, service));
        }

        private static void AddInstitution(Accumulator acc, IdentityFacetReference office)
        {
            // An office grants standing whether or not this build recognises which one, because the
            // game saying somebody holds an office is already a fact about them. What the office
            // *amounts to* is recognised or it is not, and an unrecognised one grants no role.
            acc.AddStake(IdentityStakeKind.Standing, StandingExposure, office);
            acc.AddDomains(office, InstitutionKnowledge, InstitutionInterest);
            acc.AddRoles(office);
        }

        // -- vocabulary ----------------------------------------------------------------------

        // Plausibility is 0..1 and ordinal. A facet the game states outright reads 1.0 not because
        // the derivation is certain of anything, but because there is nothing more plausible than
        // the trade somebody actually holds; everything else is scaled against that.
        private const double WorkKnowledge = 1.0;
        private const double WorkInterest = 0.5;
        private const double ServiceKnowledge = 1.0;
        private const double ServiceInterest = 0.5;
        private const double InstitutionKnowledge = 0.85;
        private const double InstitutionInterest = 0.4;
        private const double HobbyKnowledge = 0.4;
        private const double HobbyInterest = 0.8;

        private const double LivelihoodExposure = 0.6;
        private const double BusinessExposure = 0.75;
        private const double StandingExposure = 0.7;

        /// <summary>
        /// Which of the game's own words read as which domain, and which as which role.
        ///
        /// Substring matching over Elin's ids rather than an enumerated catalogue of occupations,
        /// for the same reason the seam carries ids verbatim: a build or a mod that spells a trade
        /// differently should derive nothing rather than be mapped onto the nearest familiar thing.
        /// Unrecognised is a legitimate answer here and stays one.
        /// </summary>
        private static readonly DomainRule[] DomainRules =
        {
            new DomainRule(IdentityDomain.Cultivation,
                "farm", "gardener", "garden", "grower", "harvest", "herb",
                "rancher", "ranch", "shepherd", "herder", "drover", "stable"),
            new DomainRule(IdentityDomain.Alchemy,
                "alchem", "apothecary", "healer", "physician", "herbalist", "chemist"),
            new DomainRule(IdentityDomain.Craft,
                "smith", "carpenter", "weaver", "tailor", "mason", "miner", "brewer",
                "baker", "chef", "artisan", "craft", "miller", "tanner"),
            new DomainRule(IdentityDomain.Trade,
                "merchant", "shop", "trader", "trade"),
            new DomainRule(IdentityDomain.PublicOrder,
                "guard", "watch", "reeve", "sheriff", "constable", "marshal", "warden", "authority")
        };

        private static readonly RoleRule[] RoleRules =
        {
            new RoleRule(IdentityRole.Authority,
                "guard", "watch", "reeve", "sheriff", "constable", "marshal", "warden", "authority", "court"),
            new RoleRule(IdentityRole.GuildStanding, "guild")
        };

        private static bool Matches(string id, IdentityDomain domain)
        {
            for (int i = 0; i < DomainRules.Length; i++)
            {
                if (DomainRules[i].Domain == domain)
                {
                    return DomainRules[i].Matches(id);
                }
            }

            return false;
        }

        private struct DomainRule
        {
            private readonly string[] _tokens;

            public DomainRule(IdentityDomain domain, params string[] tokens)
            {
                Domain = domain;
                _tokens = tokens;
            }

            public IdentityDomain Domain { get; }

            public bool Matches(string id) => ContainsAny(id, _tokens);
        }

        private struct RoleRule
        {
            private readonly string[] _tokens;

            public RoleRule(IdentityRole role, params string[] tokens)
            {
                Role = role;
                _tokens = tokens;
            }

            public IdentityRole Role { get; }

            public bool Matches(string id) => ContainsAny(id, _tokens);
        }

        private static bool ContainsAny(string id, string[] tokens)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < tokens.Length; i++)
            {
                if (id.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gathers what each facet implies, keeping the strongest reading of a domain and every
        /// facet that supports it. One place, so that the rule "an affordance names its facets" is
        /// structural rather than something each derivation branch has to remember.
        /// </summary>
        private sealed class Accumulator
        {
            private readonly EntityId _actor;
            private readonly List<Entry> _knowledge = new List<Entry>();
            private readonly List<Entry> _interests = new List<Entry>();
            private readonly List<IdentityRoleEligibility> _roles = new List<IdentityRoleEligibility>();
            private readonly List<IdentityStake> _stakes = new List<IdentityStake>();
            private readonly List<IdentityFacetKind> _contributing = new List<IdentityFacetKind>();

            public Accumulator(EntityId actor)
            {
                _actor = actor;
            }

            public void AddDomains(IdentityFacetReference source, double knowledge, double interest)
            {
                for (int i = 0; i < DomainRules.Length; i++)
                {
                    if (!DomainRules[i].Matches(source.VanillaId))
                    {
                        continue;
                    }

                    Contribute(source);
                    Merge(_knowledge, DomainRules[i].Domain, knowledge, source);
                    Merge(_interests, DomainRules[i].Domain, interest, source);
                }
            }

            public void AddRoles(IdentityFacetReference source)
            {
                for (int i = 0; i < RoleRules.Length; i++)
                {
                    if (RoleRules[i].Matches(source.VanillaId))
                    {
                        AddRole(RoleRules[i].Role, source);
                    }
                }
            }

            public void AddRole(IdentityRole role, IdentityFacetReference source)
            {
                for (int i = 0; i < _roles.Count; i++)
                {
                    if (_roles[i].Role == role)
                    {
                        return;
                    }
                }

                Contribute(source);
                _roles.Add(new IdentityRoleEligibility(role, source));
            }

            public void AddStake(IdentityStakeKind kind, double exposure, IdentityFacetReference source)
            {
                for (int i = 0; i < _stakes.Count; i++)
                {
                    if (_stakes[i].Kind == kind)
                    {
                        return;
                    }
                }

                Contribute(source);
                _stakes.Add(new IdentityStake(kind, exposure, source));
            }

            public IdentityAffordances Build(IdentityServiceCapability service)
            {
                _contributing.Sort();
                return new IdentityAffordances(
                    _actor,
                    Freeze(_knowledge),
                    Freeze(_interests),
                    _roles,
                    _stakes,
                    service,
                    _contributing);
            }

            private void Contribute(IdentityFacetReference source)
            {
                if (!_contributing.Contains(source.Facet))
                {
                    _contributing.Add(source.Facet);
                }
            }

            private static void Merge(
                List<Entry> into,
                IdentityDomain domain,
                double plausibility,
                IdentityFacetReference source)
            {
                if (plausibility <= 0.0)
                {
                    return;
                }

                for (int i = 0; i < into.Count; i++)
                {
                    if (into[i].Domain != domain)
                    {
                        continue;
                    }

                    if (plausibility > into[i].Plausibility)
                    {
                        into[i].Plausibility = plausibility;
                    }

                    if (!into[i].Sources.Contains(source))
                    {
                        into[i].Sources.Add(source);
                    }

                    return;
                }

                Entry entry = new Entry { Domain = domain, Plausibility = plausibility };
                entry.Sources.Add(source);
                into.Add(entry);
            }

            private static List<IdentityDomainAffordance> Freeze(List<Entry> entries)
            {
                entries.Sort(CompareDomain);
                List<IdentityDomainAffordance> frozen = new List<IdentityDomainAffordance>();
                for (int i = 0; i < entries.Count; i++)
                {
                    frozen.Add(new IdentityDomainAffordance(
                        entries[i].Domain, entries[i].Plausibility, entries[i].Sources));
                }

                return frozen;
            }

            private static int CompareDomain(Entry left, Entry right) =>
                ((int)left.Domain).CompareTo((int)right.Domain);

            private sealed class Entry
            {
                public IdentityDomain Domain;
                public double Plausibility;
                public readonly List<IdentityFacetReference> Sources = new List<IdentityFacetReference>();
            }
        }
    }
}
