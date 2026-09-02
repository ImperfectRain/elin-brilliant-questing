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
    }

    /// <summary>Who ended up in each role, and the sentences explaining why.</summary>
    public sealed class StoryletCastingResult
    {
        internal StoryletCastingResult(
            Dictionary<string, EntityId> bindings,
            List<string> notes,
            string uncastRole)
        {
            Bindings = bindings;
            Notes = notes;
            UncastRequiredRole = uncastRole ?? string.Empty;
        }

        public Dictionary<string, EntityId> Bindings { get; }

        /// <summary>Inspector-only sentences: one per bound role, naming what qualified them.</summary>
        public List<string> Notes { get; }

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
    /// for knowledge, proof, ownership or standing; negative requirements reject the dead, the
    /// absent, the socially incapable, whatever is not a person at all, and anybody already
    /// holding another role in the same scene.
    ///
    /// Selection among the qualified is deliberately unscored: the first candidate in a stable
    /// order. Preferring the *best* group - goal conflict, shared history, power asymmetry - is
    /// BQ-068's role chemistry, and putting a score here would leave two of them to reconcile.
    /// </summary>
    public static class StoryletCasting
    {
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

            Dictionary<string, EntityId> bindings = new Dictionary<string, EntityId>(StringComparer.Ordinal);
            List<string> notes = new List<string>();
            HashSet<EntityId> taken = new HashSet<EntityId>();
            if (focus == null || context.World == null || context.Vanilla == null || context.Thread == null)
            {
                return new StoryletCastingResult(bindings, notes, FirstRequiredRole(definition));
            }

            List<EntityId> pool = BuildPool(context);

            // Named sources first, then searched ones: a role that already knows who it wants
            // must not lose them to a role that would have taken anybody. Required before
            // optional for the same reason - an optional corroborator never steals the accuser.
            BindPass(definition, context, focus, pool, bindings, notes, taken, named: true, required: true);
            BindPass(definition, context, focus, pool, bindings, notes, taken, named: true, required: false);
            BindPass(definition, context, focus, pool, bindings, notes, taken, named: false, required: true);
            BindPass(definition, context, focus, pool, bindings, notes, taken, named: false, required: false);

            string uncast = string.Empty;
            for (int i = 0; i < definition.RequiredRoles.Count; i++)
            {
                if (!bindings.ContainsKey(definition.RequiredRoles[i].Id))
                {
                    uncast = definition.RequiredRoles[i].Id;
                    break;
                }
            }

            return new StoryletCastingResult(bindings, notes, uncast);
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

        private static void BindPass(
            StoryletDefinition definition,
            StoryletCastingContext context,
            Fact focus,
            List<EntityId> pool,
            Dictionary<string, EntityId> bindings,
            List<string> notes,
            HashSet<EntityId> taken,
            bool named,
            bool required)
        {
            IReadOnlyList<StoryletRole> roles = required ? definition.RequiredRoles : definition.OptionalRoles;
            for (int i = 0; i < roles.Count; i++)
            {
                StoryletRole role = roles[i];
                if (IsNamedSource(role.Source) != named || bindings.ContainsKey(role.Id))
                {
                    continue;
                }

                EntityId cast = named
                    ? Named(role.Source, context, focus)
                    : Searched(role.Source, context, focus, pool, taken);

                if (cast.IsNone || taken.Contains(cast) || !IsCastablePerson(context, cast))
                {
                    continue;
                }

                bindings[role.Id] = cast;
                taken.Add(cast);
                notes.Add(role.Id + ": " + context.World.Registry.NameOf(cast) + " (" + Because(role.Source) + ")");
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

        private static EntityId Searched(
            StoryletRoleSource source,
            StoryletCastingContext context,
            Fact focus,
            List<EntityId> pool,
            HashSet<EntityId> taken)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                EntityId candidate = pool[i];
                if (taken.Contains(candidate))
                {
                    continue;
                }

                if (Qualifies(source, context, focus, candidate))
                {
                    return candidate;
                }
            }

            return EntityId.None;
        }

        private static bool Qualifies(
            StoryletRoleSource source,
            StoryletCastingContext context,
            Fact focus,
            EntityId candidate)
        {
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
                default:
                    return false;
            }
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
        /// the name, which is not a claim about who is better suited to the role.
        /// </summary>
        private static List<EntityId> BuildPool(StoryletCastingContext context)
        {
            List<EntityId> pool = new List<EntityId>();
            HashSet<EntityId> seen = new HashSet<EntityId>();

            IReadOnlyList<EntityId> here = context.Place.IsNone
                ? new List<EntityId>()
                : context.Vanilla.GetCharactersInZone(context.Place);

            HashSet<EntityId> present = new HashSet<EntityId>();
            for (int i = 0; i < here.Count; i++)
            {
                present.Add(here[i]);
            }

            // A build that cannot say who is in a zone must not silently cast nobody: the thread's
            // own participants are then the whole pool, which is what the engine had before there
            // was a place at all.
            bool trustPresence = present.Count > 0;

            for (int i = 0; i < context.Thread.ParticipantIds.Count; i++)
            {
                EntityId participant = context.Thread.ParticipantIds[i];
                if (trustPresence && !present.Contains(participant))
                {
                    continue;
                }

                if (seen.Add(participant) && IsCastablePerson(context, participant) && IsAvailable(context, participant))
                {
                    pool.Add(participant);
                }
            }

            List<EntityId> others = new List<EntityId>();
            for (int i = 0; i < here.Count; i++)
            {
                EntityId candidate = here[i];
                if (seen.Add(candidate) && IsCastablePerson(context, candidate) && IsAvailable(context, candidate))
                {
                    others.Add(candidate);
                }
            }

            if (others.Count > 1)
            {
                // Read only when there is actually a choice to make. Every definition the engine
                // offers is cast separately, and walking the player's whole history to order a
                // pool of one would make finding scenes cost more than playing them.
                PlayerFamiliarity familiarity = PlayerFamiliarity.Read(context.World, context.Vanilla);
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
        /// Somebody the narrative knows as a person. An item, a zone or an id the registry has
        /// never heard of is not a candidate for a speaking role, whatever slot of a fact it
        /// happens to sit in.
        /// </summary>
        private static bool IsCastablePerson(StoryletCastingContext context, EntityId candidate)
        {
            return !candidate.IsNone && context.World.Registry.GetNpc(candidate) != null;
        }

        /// <summary>
        /// The negative requirements: dead, or unable to carry an ordinary social role. Unknown
        /// social agency fails closed, as the seam says it must. Applied when searching for
        /// somebody, not to an actor the caller or the fact named - a storylet may deliberately
        /// be about the dead, and says so with its own <c>RoleAlive</c> precondition.
        /// </summary>
        private static bool IsAvailable(StoryletCastingContext context, EntityId candidate)
        {
            if (candidate == context.Vanilla.PlayerId || !context.Vanilla.IsAlive(candidate))
            {
                return false;
            }

            SocialAgency agency = context.Vanilla.GetSocialAgency(candidate);
            return agency == SocialAgency.Full || agency == SocialAgency.Limited;
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
