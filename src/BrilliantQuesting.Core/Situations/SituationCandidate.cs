using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Situations
{
    /// <summary>
    /// The role names a candidate binds things to.
    ///
    /// Strings rather than an enum for the same reason <see cref="World.NarrativeNpc.Roles"/> is:
    /// archetypes, adapters and eventually organizations all name roles, and the generic layer
    /// should not have to enumerate every source before an archetype can exist. The four below are
    /// what BQ-039 needs; a shortage naming a "supplier" or a caravan naming a "route" adds its own
    /// constant beside its archetype rather than editing this list.
    /// </summary>
    public static class SituationRoles
    {
        /// <summary>Whoever does the thing. A thief, a defaulting debtor, a failing supplier.</summary>
        public const string Actor = "actor";

        /// <summary>Whoever it is done to.</summary>
        public const string Target = "target";

        /// <summary>
        /// Somebody who saw it. Bound zero or more times: a theft nobody watched is a perfectly
        /// ordinary theft, and the day several people see one, the binding takes several.
        /// </summary>
        public const string Witness = "witness";

        /// <summary>What is actually at issue - the ring, the cargo, the debt.</summary>
        public const string Stake = "stake";

        /// <summary>Where it happens.</summary>
        public const string Place = "place";
    }

    /// <summary>
    /// One thing the world could produce, described without reference to what kind of thing it is.
    ///
    /// This deliberately does not look like a theft. The first cut of BQ-039 had
    /// actor/target/witness/item fields, which meant every archetype after it either inherited a
    /// shape it did not fit - a shortage has a supplier and a town, not a victim and a witness - or
    /// forced a second redesign at BQ-041. So a candidate is bindings: named roles pointing at
    /// actors, an item, a place, plus the named numbers that produced its score and the sentences
    /// that explain them.
    ///
    /// Not serialised. A candidate is a proposal that lives for one evaluation; what survives is
    /// the situation built from it and the causes copied onto its thread.
    /// </summary>
    public sealed class SituationCandidate
    {
        private readonly Dictionary<string, List<EntityId>> _actors;
        private readonly Dictionary<string, ItemDescriptor> _items;
        private readonly Dictionary<string, EntityId> _sites;
        private readonly Dictionary<string, int> _pressures;
        private readonly List<string> _causes;

        internal SituationCandidate(
            string archetypeId,
            Dictionary<string, List<EntityId>> actors,
            Dictionary<string, ItemDescriptor> items,
            Dictionary<string, EntityId> sites,
            Dictionary<string, int> pressures,
            List<string> causes)
        {
            ArchetypeId = archetypeId;
            _actors = new Dictionary<string, List<EntityId>>();
            foreach (KeyValuePair<string, List<EntityId>> role in actors)
            {
                _actors.Add(role.Key, new List<EntityId>(role.Value));
            }

            _items = new Dictionary<string, ItemDescriptor>(items);
            _sites = new Dictionary<string, EntityId>(sites);
            _pressures = new Dictionary<string, int>(pressures);
            _causes = new List<string>(causes);

            foreach (KeyValuePair<string, int> pressure in pressures)
            {
                Score += pressure.Value;
            }
        }

        public string ArchetypeId { get; }

        /// <summary>The sum of the named pressures. There is no score that is not accounted for.</summary>
        public int Score { get; }

        /// <summary>Inspector-only sentences naming the world state behind each pressure.</summary>
        public IReadOnlyList<string> Causes => _causes;

        public IReadOnlyDictionary<string, int> Pressures => _pressures;

        public int Pressure(string name) => _pressures.TryGetValue(name, out int value) ? value : 0;

        public bool HasRole(string role) => _actors.TryGetValue(role, out List<EntityId> bound) && bound.Count > 0;

        /// <summary>Everybody bound to a role, in binding order.</summary>
        public IReadOnlyList<EntityId> ActorsIn(string role) =>
            _actors.TryGetValue(role, out List<EntityId> bound) ? bound : EmptyActors;

        /// <summary>The first actor in a role, or <see cref="EntityId.None"/> when nothing is bound.</summary>
        public EntityId ActorIn(string role)
        {
            IReadOnlyList<EntityId> bound = ActorsIn(role);
            return bound.Count == 0 ? EntityId.None : bound[0];
        }

        public ItemDescriptor ItemIn(string role) => _items.TryGetValue(role, out ItemDescriptor item) ? item : null;

        public EntityId SiteIn(string role) => _sites.TryGetValue(role, out EntityId site) ? site : EntityId.None;

        private static readonly EntityId[] EmptyActors = new EntityId[0];
    }

    /// <summary>
    /// Assembles a candidate. Separate from the candidate so a built one cannot be edited after it
    /// has been scored, ranked or suppressed.
    /// </summary>
    public sealed class SituationCandidateBuilder
    {
        private readonly Dictionary<string, List<EntityId>> _actors = new Dictionary<string, List<EntityId>>();
        private readonly Dictionary<string, ItemDescriptor> _items = new Dictionary<string, ItemDescriptor>();
        private readonly Dictionary<string, EntityId> _sites = new Dictionary<string, EntityId>();
        private readonly Dictionary<string, int> _pressures = new Dictionary<string, int>();
        private readonly List<string> _causes = new List<string>();
        private readonly string _archetypeId;

        public SituationCandidateBuilder(string archetypeId)
        {
            _archetypeId = archetypeId;
        }

        /// <summary>Binds somebody to a role. Called more than once for a role that takes several.</summary>
        public SituationCandidateBuilder Bind(string role, EntityId actor)
        {
            if (!actor.IsNone)
            {
                if (!_actors.TryGetValue(role, out List<EntityId> bound))
                {
                    bound = new List<EntityId>();
                    _actors.Add(role, bound);
                }

                if (!bound.Contains(actor))
                {
                    bound.Add(actor);
                }
            }

            return this;
        }

        public SituationCandidateBuilder BindItem(string role, ItemDescriptor item)
        {
            if (item != null)
            {
                _items[role] = item;
            }

            return this;
        }

        public SituationCandidateBuilder BindSite(string role, EntityId site)
        {
            if (!site.IsNone)
            {
                _sites[role] = site;
            }

            return this;
        }

        /// <summary>
        /// Records one named contribution to the score, with the sentence that explains it.
        ///
        /// Score and explanation are recorded together on purpose: a pressure that cannot say what
        /// produced it is exactly the hand-tuned constant BQ-039 is not allowed to contain, and the
        /// inspector's account of a situation is only honest if it names every term.
        /// </summary>
        public SituationCandidateBuilder Pressure(string name, int value, string because)
        {
            _pressures[name] = value;
            return Cause(because);
        }

        public SituationCandidateBuilder Cause(string cause)
        {
            if (!string.IsNullOrEmpty(cause))
            {
                _causes.Add(cause);
            }

            return this;
        }

        public SituationCandidate Build() =>
            new SituationCandidate(_archetypeId, _actors, _items, _sites, _pressures, _causes);
    }

    /// <summary>
    /// A theft's reading of a generic candidate.
    ///
    /// The bindings are the truth; this is a lens over them, so the theft code that consumes a
    /// candidate reads <c>ThiefId</c> rather than <c>ActorIn("actor")</c> without the generic type
    /// having to know what a thief is.
    /// </summary>
    public readonly struct PettyTheftCandidate
    {
        public PettyTheftCandidate(SituationCandidate candidate)
        {
            Candidate = candidate;
        }

        public SituationCandidate Candidate { get; }

        public EntityId ThiefId => Candidate.ActorIn(SituationRoles.Actor);

        public EntityId VictimId => Candidate.ActorIn(SituationRoles.Target);

        /// <summary><see cref="EntityId.None"/> when nobody saw it, which is a normal outcome.</summary>
        public EntityId WitnessId => Candidate.ActorIn(SituationRoles.Witness);

        public IReadOnlyList<EntityId> WitnessIds => Candidate.ActorsIn(SituationRoles.Witness);

        public bool Witnessed => Candidate.HasRole(SituationRoles.Witness);

        public ItemDescriptor Item => Candidate.ItemIn(SituationRoles.Stake);
    }
}
