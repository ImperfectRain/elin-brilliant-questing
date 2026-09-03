using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Storylets
{
    /// <summary>
    /// Where a scene is being cast, and what the caller has already decided about it.
    ///
    /// A storylet is a shape, not a cast list: the same public accusation has to work in a
    /// fishing hamlet and in a guild town, with nobody named in the definition. So the definition
    /// says what each role *requires* and this says where to look for somebody who meets it -
    /// which thread, which fact is at issue, and which place the scene happens in.
    ///
    /// <see cref="Actor"/> and <see cref="Target"/> are the one deliberate exception: the two
    /// people a caller may already know, because the player is standing in front of one of them.
    /// Everything else is found. Both may be <see cref="EntityId.None"/>, and a storylet that
    /// names neither is cast entirely from who qualifies.
    /// </summary>
    public sealed class StoryletCastingContext
    {
        public StoryletCastingContext(
            NarrativeWorldState world,
            IVanillaState vanilla,
            NarrativeThread thread,
            EntityId focusFactId)
        {
            World = world;
            Vanilla = vanilla;
            Thread = thread;
            FocusFactId = focusFactId;
            Place = thread != null && thread.SiteIds.Count > 0 ? thread.SiteIds[0] : EntityId.None;
        }

        public NarrativeWorldState World { get; }

        public IVanillaState Vanilla { get; }

        public NarrativeThread Thread { get; }

        public EntityId FocusFactId { get; }

        /// <summary>
        /// Where the scene is. Defaults to the thread's first site; a caller staging a scene
        /// somewhere else - a guild hall, the player's Home - names that instead.
        /// </summary>
        public EntityId Place { get; set; }

        /// <summary>Somebody the caller has already cast, or none. Usually who the player is with.</summary>
        public EntityId Actor { get; set; }

        /// <summary>The other person the caller has already cast, or none.</summary>
        public EntityId Target { get; set; }

        /// <summary>
        /// Who the game says is of the player's household right now, read once for this pass.
        ///
        /// A live read held for exactly as long as the seam allows one to be held - one casting
        /// pass - because <see cref="StoryletEngine.Find"/> casts every registered definition
        /// against one context and re-reading the settlement and the party per definition would
        /// make finding scenes cost more than playing them. A new context is a new read, so a pet
        /// sold between two passes is household in neither of them for longer than that pass.
        /// </summary>
        internal PlayerHousehold Household
        {
            get { return _household ?? (_household = PlayerHousehold.Read(World, Vanilla)); }
        }

        private PlayerHousehold _household;
    }

    /// <summary>Who ended up in each role, and the sentences explaining why.</summary>
    public sealed class StoryletCastingResult
    {
        internal StoryletCastingResult(
            Dictionary<string, EntityId> bindings,
            List<string> notes,
            string uncastRole)
            : this(bindings, notes, uncastRole, StoryletChemistryScore.Empty, 0)
        {
        }

        internal StoryletCastingResult(
            Dictionary<string, EntityId> bindings,
            List<string> notes,
            string uncastRole,
            StoryletChemistryScore chemistry,
            int groupsConsidered)
        {
            Bindings = bindings;
            Notes = notes;
            UncastRequiredRole = uncastRole ?? string.Empty;
            Chemistry = chemistry ?? StoryletChemistryScore.Empty;
            GroupsConsidered = groupsConsidered;
        }

        public Dictionary<string, EntityId> Bindings { get; }

        /// <summary>Inspector-only sentences: one per bound role, naming what qualified them.</summary>
        public List<string> Notes { get; }

        /// <summary>
        /// Why this group of qualified people was preferred to the other qualified groups (BQ-068).
        ///
        /// Flat when there was nothing to choose between - one group, or several with no relation
        /// between anybody in them - which is a real answer and is reported as one.
        /// </summary>
        public StoryletChemistryScore Chemistry { get; }

        /// <summary>
        /// How many complete qualified groups this pass actually weighed. One means chemistry
        /// changed nothing, because there was only ever one way to cast the scene.
        /// </summary>
        public int GroupsConsidered { get; }

        /// <summary>The first required role nobody qualified for, or empty when the scene is cast.</summary>
        public string UncastRequiredRole { get; }

        public bool IsCast => UncastRequiredRole.Length == 0;
    }

    /// <summary>
    /// Casts a storylet's temporary roles from whoever actually qualifies here and now.
    ///
    /// Two rules carry the whole of it, and they are the two the first cut of the storylet engine
    /// did not have.
    ///
    /// **Roles are temporary, and nobody is their role.** A binding lives on the firing, never on
    /// the person: the neighbour who is the Accuser today is not an accuser, and the next scene
    /// re-casts from the world as it is then. Nothing here writes to a <see cref="NarrativeNpc"/>.
    ///
    /// **A role is a requirement, not a position.** Before this existed, a role was resolved by
    /// where the caller happened to put somebody - the first participant who knew the fact, the
    /// object slot of the focus - which meant the corroborating knower of a theft was usually the
    /// thief, and the injured party of a stolen ring was the ring. Positive requirements now ask
    /// for knowledge, proof, ownership, standing or belonging to the player's household; negative
    /// requirements reject the dead, the absent, whatever the registry does not know as an actor
    /// at all, and anybody already holding another role in the same scene.
    ///
    /// **Speaking is a requirement of the role, not of the pool.** Social agency used to be a
    /// negative requirement applied to every candidate before any role saw them, which is why the
    /// player's own chicken could not be the victim of anything: a role that needs testimony,
    /// commerce or deception does need <see cref="SocialAgency"/>, and one that needs somebody for
    /// the scene to be *about* does not (BQ-123). So the check moved from
    /// <see cref="BuildPool"/> to the requirement, and unknown agency still fails closed for every
    /// role that asks for it.
    ///
    /// **Selection among the qualified is chemistry's (BQ-068), and it happens strictly after
    /// eligibility.** This engine forms whole groups first - every complete cast the rules above
    /// would have accepted - and only then asks <see cref="StoryletChemistry"/> which of them
    /// makes the better scene. The two steps cannot be reconciled the wrong way round, because a
    /// score is never consulted about whether somebody may hold a role: an ineligible actor is
    /// never in a group to begin with, and a storylet nobody qualifies for stays uncast whatever
    /// the chemistry would have been.
    ///
    /// Groups are formed in the order the unscored engine would have picked them, so the first
    /// one enumerated is exactly the old first-candidate-in-a-stable-order answer. It is always
    /// weighed, it wins every tie, and it is what a scene falls back to when nothing in the world
    /// distinguishes one group from another - which is the common case in a town where nobody has
    /// any history yet.
    /// </summary>
    public static class StoryletCasting
    {
        /// <summary>
        /// How many qualified candidates one searched role offers to the group search.
        ///
        /// A bound, not a filter: the shortlist is always at least one longer than the number of
        /// searched roles, so a role can never run out of candidates the unscored engine would
        /// have found for it, and the answer with chemistry disabled is provably the answer
        /// without it. Beyond that the extra candidates only add groups that differ in who the
        /// fifth-most-familiar bystander is.
        /// </summary>
        private const int MaxCandidatesPerRole = 8;

        /// <summary>
        /// How many complete groups one casting pass will weigh.
        ///
        /// Finding scenes must not cost more than playing them, and the product of the shortlists
        /// is a product: five roles offering eight people each is thousands of casts for a
        /// preference nobody would notice past the first few dozen. The search is depth-first in
        /// the unscored engine's own order, so the cap can only remove groups it would never have
        /// reached first - the fallback group is enumerated before anything else and is always
        /// weighed.
        /// </summary>
        private const int MaxGroupsConsidered = 128;

        public static StoryletCastingResult Cast(
            StoryletDefinition definition,
            StoryletCastingContext context,
            Fact focus)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (focus == null || context.World == null || context.Vanilla == null || context.Thread == null)
            {
                return new StoryletCastingResult(
                    new Dictionary<string, EntityId>(StringComparer.Ordinal),
                    new List<string>(),
                    FirstRequiredRole(definition));
            }

            List<EntityId> pool = BuildPool(context);

            // Named sources first, then searched ones: a role that already knows who it wants
            // must not lose them to a role that would have taken anybody. Required before
            // optional for the same reason - an optional corroborator never steals the accuser.
            // Named roles are not a choice at all, so they are settled once and are the same in
            // every group; only the searched roles have alternatives to weigh.
            Dictionary<string, EntityId> named = new Dictionary<string, EntityId>(StringComparer.Ordinal);
            List<StoryletRole> order = new List<StoryletRole>();
            HashSet<EntityId> taken = new HashSet<EntityId>();
            BindNamed(definition, context, focus, named, order, taken, required: true);
            BindNamed(definition, context, focus, named, order, taken, required: false);

            List<SearchedRole> searched = new List<SearchedRole>();
            CollectSearched(definition, named, searched, required: true);
            CollectSearched(definition, named, searched, required: false);
            for (int i = 0; i < searched.Count; i++)
            {
                order.Add(searched[i].Role);
                Shortlist(searched[i], context, focus, pool, taken, searched.Count);
            }

            GroupSearch search = new GroupSearch(definition, context, focus, named, order);
            search.Walk(searched, 0, new Dictionary<string, EntityId>(StringComparer.Ordinal), new HashSet<EntityId>(taken));

            Dictionary<string, EntityId> bindings = search.Chosen;
            string uncast = string.Empty;
            for (int i = 0; i < definition.RequiredRoles.Count; i++)
            {
                if (!bindings.ContainsKey(definition.RequiredRoles[i].Id))
                {
                    uncast = definition.RequiredRoles[i].Id;
                    break;
                }
            }

            return new StoryletCastingResult(
                bindings,
                Notes(context, order, bindings),
                uncast,
                search.ChosenChemistry,
                search.Considered);
        }

        /// <summary>One sentence per bound role, in the order the roles were bound.</summary>
        private static List<string> Notes(
            StoryletCastingContext context,
            List<StoryletRole> order,
            Dictionary<string, EntityId> bindings)
        {
            List<string> notes = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                EntityId cast;
                if (!bindings.TryGetValue(order[i].Id, out cast))
                {
                    continue;
                }

                notes.Add(order[i].Id + ": " + context.World.Registry.NameOf(cast)
                          + " (" + Because(order[i].Source) + ")");
            }

            return notes;
        }

        private static void BindNamed(
            StoryletDefinition definition,
            StoryletCastingContext context,
            Fact focus,
            Dictionary<string, EntityId> bindings,
            List<StoryletRole> order,
            HashSet<EntityId> taken,
            bool required)
        {
            IReadOnlyList<StoryletRole> roles = required ? definition.RequiredRoles : definition.OptionalRoles;
            for (int i = 0; i < roles.Count; i++)
            {
                StoryletRole role = roles[i];
                if (!IsNamedSource(role.Source) || bindings.ContainsKey(role.Id))
                {
                    continue;
                }

                EntityId cast = Named(role.Source, context, focus);
                if (cast.IsNone || taken.Contains(cast) || !IsCastableActor(context, cast))
                {
                    continue;
                }

                bindings[role.Id] = cast;
                taken.Add(cast);
                order.Add(role);
            }
        }

        /// <summary>
        /// The searched roles still to fill, in binding order and each listed once.
        ///
        /// A definition that spells the same role id twice - in both lists, or once named and once
        /// searched - binds it once, exactly as it did before groups existed. The role is a name
        /// for one part in the scene, and the first source that fills it is the one that wins.
        /// </summary>
        private static void CollectSearched(
            StoryletDefinition definition,
            Dictionary<string, EntityId> named,
            List<SearchedRole> searched,
            bool required)
        {
            IReadOnlyList<StoryletRole> roles = required ? definition.RequiredRoles : definition.OptionalRoles;
            for (int i = 0; i < roles.Count; i++)
            {
                StoryletRole role = roles[i];
                if (IsNamedSource(role.Source) || named.ContainsKey(role.Id) || Holds(searched, role.Id))
                {
                    continue;
                }

                searched.Add(new SearchedRole(role));
            }
        }

        private static bool Holds(List<SearchedRole> searched, string roleId)
        {
            for (int i = 0; i < searched.Count; i++)
            {
                if (string.Equals(searched[i].Role.Id, roleId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Everybody in the pool this role's requirement admits, in pool order, bounded.
        ///
        /// This is the whole of eligibility for a searched role, and it runs before any group
        /// exists: chemistry is handed shortlists it did not build and cannot add to.
        /// </summary>
        private static void Shortlist(
            SearchedRole role,
            StoryletCastingContext context,
            Fact focus,
            List<EntityId> pool,
            HashSet<EntityId> takenByName,
            int searchedRoleCount)
        {
            int limit = Math.Max(MaxCandidatesPerRole, searchedRoleCount + 1);
            for (int i = 0; i < pool.Count && role.Candidates.Count < limit; i++)
            {
                EntityId candidate = pool[i];
                if (!takenByName.Contains(candidate) && Qualifies(role.Role.Source, context, focus, candidate))
                {
                    role.Candidates.Add(candidate);
                }
            }
        }

        /// <summary>A searched role and the people who actually meet its requirement.</summary>
        private sealed class SearchedRole
        {
            public SearchedRole(StoryletRole role)
            {
                Role = role;
                Candidates = new List<EntityId>();
            }

            public StoryletRole Role { get; }

            public List<EntityId> Candidates { get; }
        }

        /// <summary>
        /// Walks every complete group the requirements allow, and keeps the best one.
        ///
        /// Depth-first in the unscored engine's own order - roles in binding order, candidates in
        /// pool order - which is what makes the first group reached identical to what BQ-067 would
        /// have cast, and therefore what makes it a safe fallback and a stable tie-break.
        ///
        /// Backtracking is the one thing group formation adds to eligibility, and it only ever
        /// finds casts the requirements already permit: where taking the obvious person for one
        /// role would leave a later role with nobody, the search tries the next person for the
        /// first role instead of reporting a scene uncastable. Nobody enters a role they do not
        /// qualify for by this route; a role that genuinely nobody here meets still goes unfilled.
        /// </summary>
        private sealed class GroupSearch
        {
            private readonly StoryletDefinition _definition;
            private readonly StoryletCastingContext _context;
            private readonly Fact _focus;
            private readonly Dictionary<string, EntityId> _named;
            private readonly List<StoryletRole> _order;
            private readonly List<string> _roleIds;
            private readonly ChemistryIdentityCache _identities = new ChemistryIdentityCache();
            private Dictionary<string, EntityId> _fallback;
            private Dictionary<string, EntityId> _best;
            private StoryletChemistryScore _bestChemistry = StoryletChemistryScore.Empty;
            private int _bestBound = -1;

            public GroupSearch(
                StoryletDefinition definition,
                StoryletCastingContext context,
                Fact focus,
                Dictionary<string, EntityId> named,
                List<StoryletRole> order)
            {
                _definition = definition;
                _context = context;
                _focus = focus;
                _named = named;
                _order = order;
                _roleIds = new List<string>();
                for (int i = 0; i < order.Count; i++)
                {
                    _roleIds.Add(order[i].Id);
                }
            }

            /// <summary>How many complete groups were weighed.</summary>
            public int Considered { get; private set; }

            /// <summary>
            /// The group that won, or - when no group filled every required role - the one the
            /// unscored engine would have produced, so the uncast role it reports is unchanged.
            /// </summary>
            public Dictionary<string, EntityId> Chosen
            {
                get { return _best ?? _fallback ?? new Dictionary<string, EntityId>(StringComparer.Ordinal); }
            }

            public StoryletChemistryScore ChosenChemistry
            {
                get { return _best == null ? StoryletChemistryScore.Empty : _bestChemistry; }
            }

            public void Walk(
                List<SearchedRole> searched,
                int index,
                Dictionary<string, EntityId> assignment,
                HashSet<EntityId> taken)
            {
                if (index >= searched.Count)
                {
                    Consider(assignment);
                    return;
                }

                SearchedRole role = searched[index];
                bool anyFree = false;
                for (int i = 0; i < role.Candidates.Count; i++)
                {
                    EntityId candidate = role.Candidates[i];
                    if (taken.Contains(candidate))
                    {
                        continue;
                    }

                    anyFree = true;
                    assignment[role.Role.Id] = candidate;
                    taken.Add(candidate);
                    Walk(searched, index + 1, assignment, taken);
                    taken.Remove(candidate);
                    assignment.Remove(role.Role.Id);

                    if (Considered >= MaxGroupsConsidered)
                    {
                        return;
                    }
                }

                // Nobody left who meets this requirement. The role goes unfilled, exactly as it
                // did before groups existed - which fails the scene if it was required and simply
                // costs an optional corroborator if it was not.
                if (!anyFree)
                {
                    Walk(searched, index + 1, assignment, taken);
                }
            }

            private void Consider(Dictionary<string, EntityId> assignment)
            {
                Considered++;
                Dictionary<string, EntityId> bindings = new Dictionary<string, EntityId>(_named, StringComparer.Ordinal);
                foreach (KeyValuePair<string, EntityId> pair in assignment)
                {
                    bindings[pair.Key] = pair.Value;
                }

                if (_fallback == null)
                {
                    _fallback = bindings;
                }

                for (int i = 0; i < _definition.RequiredRoles.Count; i++)
                {
                    if (!bindings.ContainsKey(_definition.RequiredRoles[i].Id))
                    {
                        return;
                    }
                }

                StoryletChemistryScore chemistry = StoryletChemistry.Score(
                    _context, _focus, _roleIds, bindings, _identities);

                // More of the scene cast beats better chemistry, because an optional role that
                // could be filled is content the group search must not trade away; and a tie on
                // both goes to the group enumerated first, which is the unscored answer.
                if (_best != null
                    && (bindings.Count < _bestBound
                        || (bindings.Count == _bestBound
                            && chemistry.Total <= _bestChemistry.Total + StoryletChemistry.Epsilon)))
                {
                    return;
                }

                _best = bindings;
                _bestChemistry = chemistry;
                _bestBound = bindings.Count;
            }
        }

        /// <summary>
        /// Whether this source finds its actor by name - the caller's, or the focus fact's -
        /// rather than by searching the place for somebody who qualifies.
        /// </summary>
        public static bool IsNamedSource(StoryletRoleSource source)
        {
            switch (source)
            {
                case StoryletRoleSource.Actor:
                case StoryletRoleSource.Target:
                case StoryletRoleSource.FactSubject:
                case StoryletRoleSource.FactObject:
                case StoryletRoleSource.OwnerOfFocusObject:
                    return true;
                default:
                    return false;
            }
        }

        private static EntityId Named(StoryletRoleSource source, StoryletCastingContext context, Fact focus)
        {
            switch (source)
            {
                case StoryletRoleSource.Actor:
                    return context.Actor;
                case StoryletRoleSource.Target:
                    return context.Target;
                case StoryletRoleSource.FactSubject:
                    return focus.Subject;
                case StoryletRoleSource.FactObject:
                    return focus.Object;
                case StoryletRoleSource.OwnerOfFocusObject:
                    return OwnerOf(context.World, focus.Object);
                default:
                    return EntityId.None;
            }
        }

        private static bool Qualifies(
            StoryletRoleSource source,
            StoryletCastingContext context,
            Fact focus,
            EntityId candidate)
        {
            // Every role below this line but the last needs somebody who can carry an ordinary
            // social role, and unknown agency fails closed as the seam says it must. The
            // household role does not ask, because being the subject of a scene is not something
            // an actor does.
            if (RequiresSocialAgency(source) && !CanSpeak(context, candidate))
            {
                return false;
            }

            switch (source)
            {
                // The legacy spelling of the same requirement. Old bundles and old saves keep
                // loading, and they get the corrected behaviour rather than the old collapse.
                case StoryletRoleSource.AnyParticipantWhoKnowsFocus:
                case StoryletRoleSource.AnyoneWhoKnowsFocus:
                    return context.World.Knowledge.Knows(candidate, focus.Id);
                case StoryletRoleSource.AnyoneWhoCanProveFocus:
                    return context.World.Knowledge.CanProve(candidate, focus.Id);
                case StoryletRoleSource.AnyoneWithStandingHere:
                    NarrativeNpc npc = context.World.Registry.GetNpc(candidate);
                    return npc != null && npc.Roles.Count > 0;
                case StoryletRoleSource.HouseholdMemberHere:
                    return context.Household.Includes(candidate);
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether this requirement is one only somebody socially capable can meet.
        ///
        /// Testimony, proof and standing are things an actor *does*, and BQ-031's
        /// <see cref="SocialAgency"/> is the game's answer to whether they can. Being of the
        /// player's household is something an actor *is*, so the household requirement is the one
        /// that does not ask - which is what lets a scene be about somebody's pet without the
        /// pet's having to be able to give evidence about it.
        ///
        /// Named sources never come through here: a caller or a focus fact that named somebody has
        /// already decided, and a storylet that is deliberately about the dead or the speechless
        /// says so with its own preconditions.
        /// </summary>
        private static bool RequiresSocialAgency(StoryletRoleSource source)
        {
            return source != StoryletRoleSource.HouseholdMemberHere;
        }

        /// <summary>
        /// Whether this actor can carry an ordinary social role. Unknown agency fails closed, as
        /// the seam says it must - a build that cannot classify somebody does not get to put words
        /// in their mouth.
        /// </summary>
        private static bool CanSpeak(StoryletCastingContext context, EntityId candidate)
        {
            SocialAgency agency = context.Vanilla.GetSocialAgency(candidate);
            return agency == SocialAgency.Full || agency == SocialAgency.Limited;
        }

        /// <summary>
        /// Who the world says holds the thing the focus fact is about.
        ///
        /// The injured party of a theft is not in the theft fact - that fact says who took what -
        /// so it is read from the ownership the knowledge graph already carries. A thing nobody is
        /// recorded as owning has no injured party, which is a real answer.
        /// </summary>
        private static EntityId OwnerOf(NarrativeWorldState world, EntityId thing)
        {
            if (thing.IsNone)
            {
                return EntityId.None;
            }

            EntityId owner = EntityId.None;
            EntityId fromFact = EntityId.None;
            foreach (KeyValuePair<EntityId, Fact> pair in world.Knowledge.Facts)
            {
                Fact fact = pair.Value;
                if (fact.Object != thing
                    || !string.Equals(fact.Predicate, FactPredicates.Possesses, StringComparison.Ordinal)
                    || fact.Truth != TruthState.True
                    || world.Registry.GetNpc(fact.Subject) == null)
                {
                    continue;
                }

                // Stable regardless of the order the graph hands facts back.
                if (fromFact.IsNone || string.CompareOrdinal(fact.Id.Value, fromFact.Value) < 0)
                {
                    fromFact = fact.Id;
                    owner = fact.Subject;
                }
            }

            return owner;
        }

        /// <summary>
        /// Everybody who could be named into a scene here, in a stable order.
        ///
        /// Thread participants first, in the order the situation cast them, because the people a
        /// matter is already about are the people it is most likely to be about again; then
        /// everybody else the game says is standing here, the faces the player will recognise before
        /// the strangers - the ones they already know (BQ-114), and in a save too new to hold any
        /// history, the ones it elected to keep bringing back (BQ-115) - and by id within each so
        /// two runs agree. The player is never in the pool: a scene may be *with* the player, and
        /// the caller says so through
        /// <see cref="StoryletCastingContext.Actor"/>, but the mod does not write the player into
        /// a role they did not choose.
        ///
        /// Familiarity orders the search; it does not score the result. A role still takes the
        /// first candidate that meets its requirement, so `D026` holds unchanged - what a corrobo-
        /// rating neighbour has over a corroborating stranger is that the player will recognise
        /// the name, which is not a claim about who is better suited to the role. The player's own
        /// household sorts to the front of that order for free, because living on their land or
        /// walking beside them is the strongest ground `BQ-114` reads.
        ///
        /// The pool is not filtered by social agency (`BQ-123`). It was, and the effect was that
        /// the player's own animals were gone before any role could ask for one - which is the
        /// wrong place for the question, because a scene needs a speaker for some of its roles and
        /// a subject for others. The pool is now everybody here the registry knows as an actor and
        /// the game says is alive; who may speak is <see cref="Qualifies"/>'s answer, per role.
        /// </summary>
        private static List<EntityId> BuildPool(StoryletCastingContext context)
        {
            List<EntityId> pool = new List<EntityId>();
            HashSet<EntityId> seen = new HashSet<EntityId>();

            IReadOnlyList<EntityId> here = context.Place.IsNone
                ? new List<EntityId>()
                : context.Vanilla.GetCharactersInZone(context.Place);

            // Canonicalised on the way in, both here and for the thread's own participants below.
            // The pool is a set of bodies, not of ids: an actor the thread cast under an id that
            // has since been retired is present because the character is, and reaches the pool
            // once, as themselves.
            HashSet<EntityId> present = new HashSet<EntityId>();
            for (int i = 0; i < here.Count; i++)
            {
                present.Add(context.World.Registry.Canonical(here[i]));
            }

            // A build that cannot say who is in a zone must not silently cast nobody: the thread's
            // own participants are then the whole pool, which is what the engine had before there
            // was a place at all.
            bool trustPresence = present.Count > 0;

            for (int i = 0; i < context.Thread.ParticipantIds.Count; i++)
            {
                EntityId participant = context.World.Registry.Canonical(context.Thread.ParticipantIds[i]);
                if (trustPresence && !present.Contains(participant))
                {
                    continue;
                }

                if (seen.Add(participant) && IsCastableActor(context, participant) && IsAvailable(context, participant))
                {
                    pool.Add(participant);
                }
            }

            List<EntityId> others = new List<EntityId>();
            for (int i = 0; i < here.Count; i++)
            {
                EntityId candidate = context.World.Registry.Canonical(here[i]);
                if (seen.Add(candidate) && IsCastableActor(context, candidate) && IsAvailable(context, candidate))
                {
                    others.Add(candidate);
                }
            }

            if (others.Count > 1)
            {
                // Read only when there is actually a choice to make. Every definition the engine
                // offers is cast separately, and walking the player's whole history to order a
                // pool of one would make finding scenes cost more than playing them.
                PlayerFamiliarity familiarity = PlayerFamiliarity.Read(
                    context.World,
                    context.Vanilla,
                    context.Household);
                EarlyContactCast elected = EarlyContacts.Elect(context.World, context.Vanilla, context.Place);
                others.Sort(delegate(EntityId left, EntityId right)
                {
                    int known = Recognisability(familiarity, elected, right)
                        .CompareTo(Recognisability(familiarity, elected, left));
                    return known != 0 ? known : string.CompareOrdinal(left.Value, right.Value);
                });
            }

            pool.AddRange(others);
            return pool;
        }

        /// <summary>
        /// How likely the player is to recognise a name, from either half of the evidence.
        ///
        /// BQ-115. The history the player made says most when there is any; in a save the mod has
        /// only just attached to there is none for anybody, and without the elected cast this
        /// ordering collapsed to id order - which is not a reason for anything.
        /// </summary>
        private static int Recognisability(
            PlayerFamiliarity familiarity,
            EarlyContactCast elected,
            EntityId actor)
        {
            return Math.Max(familiarity.ScoreOf(actor), elected.WeightOf(actor));
        }

        /// <summary>
        /// Somebody the narrative knows as an actor. An item, a zone or an id the registry has
        /// never heard of is not a candidate for any role, whatever slot of a fact it happens to
        /// sit in - and it is also what makes a binding survive the save, because the registry
        /// keeps its entries after the game has stopped answering for the character.
        ///
        /// Deliberately not a test of personhood. A named pet the player keeps is an actor the
        /// world model knows, and whether the scene may ask it to speak is the role's question
        /// (<see cref="RequiresSocialAgency"/>), not this one's.
        /// </summary>
        private static bool IsCastableActor(StoryletCastingContext context, EntityId candidate)
        {
            // Participating actors, not every person record. A retired alias of somebody standing
            // here is the same body as the actor it was retired onto, and casting both would put
            // one character in two roles of one scene.
            return context.World.Registry.IsActor(candidate);
        }

        /// <summary>
        /// The negative requirements every searched role shares: the player, and anybody the game
        /// does not currently say is alive.
        ///
        /// That second one is also the whole of the household lifecycle. A pet that has been sold,
        /// a resident married out of the settlement, a companion the adapter can no longer resolve
        /// - the game stops answering <see cref="VanillaLifeState.Alive"/> for them or stops
        /// listing them here, and they leave the pool on the next pass without anything having to
        /// be cleaned up. What they were cast as before stays true, because it lives on the firing.
        ///
        /// Applied when searching for somebody, not to an actor the caller or the fact named - a
        /// storylet may deliberately be about the dead, and says so with its own <c>RoleAlive</c>
        /// precondition.
        /// </summary>
        private static bool IsAvailable(StoryletCastingContext context, EntityId candidate)
        {
            return candidate != context.Vanilla.PlayerId && context.Vanilla.IsAlive(candidate);
        }

        private static string Because(StoryletRoleSource source)
        {
            switch (source)
            {
                case StoryletRoleSource.Actor:
                    return "named by the scene";
                case StoryletRoleSource.Target:
                    return "named by the scene";
                case StoryletRoleSource.FactSubject:
                    return "the fact is about them";
                case StoryletRoleSource.FactObject:
                    return "the fact names them";
                case StoryletRoleSource.OwnerOfFocusObject:
                    return "owns what is at issue";
                case StoryletRoleSource.AnyParticipantWhoKnowsFocus:
                case StoryletRoleSource.AnyoneWhoKnowsFocus:
                    return "knows what happened";
                case StoryletRoleSource.AnyoneWhoCanProveFocus:
                    return "can prove what happened";
                case StoryletRoleSource.AnyoneWithStandingHere:
                    return "holds standing here";
                case StoryletRoleSource.HouseholdMemberHere:
                    return "belongs to the player's household";
                default:
                    return "qualified";
            }
        }

        private static string FirstRequiredRole(StoryletDefinition definition)
        {
            return definition.RequiredRoles.Count > 0 ? definition.RequiredRoles[0].Id : "role";
        }
    }
}
