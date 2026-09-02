using System;
using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Relationships
{
    /// <summary>
    /// Why one face was chosen to be a face the player keeps seeing.
    ///
    /// Three grounds, in the order the world already ranks them, and every one of them is a fact
    /// the save holds before the mod says anything: somebody lives on the player's land, somebody
    /// sells things to strangers for a living in the settlement the player is standing in,
    /// somebody has already crossed the player's path.
    /// </summary>
    public enum EarlyContactKind
    {
        /// <summary>Lives on the player's own land. A neighbour with a small complaint.</summary>
        Neighbour,

        /// <summary>Handles goods and money with strangers here. The shopkeeper who remembers you.</summary>
        Shopkeeper,

        /// <summary>Somebody the world has already put in front of the player at least once.</summary>
        Regular
    }

    /// <summary>
    /// One of the handful of people a save decides early it will keep bringing back.
    ///
    /// Not a relationship and not a claim about the player's history: nobody has been befriended,
    /// nothing has been met, and the player owes them nothing. It is a casting decision, and the
    /// only thing it asserts is that when this settlement has a story to tell, this is a face worth
    /// telling it about.
    /// </summary>
    public sealed class EarlyContact
    {
        internal EarlyContact(EntityId actor, EarlyContactKind kind, int weight, string because)
        {
            Actor = actor;
            Kind = kind;
            Weight = weight;
            Because = because ?? string.Empty;
        }

        public EntityId Actor { get; }

        public EarlyContactKind Kind { get; }

        /// <summary>
        /// How much recognising them is worth when two proposals are otherwise equal. Held well
        /// under <see cref="PlayerFamiliarity.Ceiling"/> on purpose: a face the mod elected is
        /// never worth as much as history the player actually made.
        /// </summary>
        public int Weight { get; }

        /// <summary>Inspector-only sentence naming the ground. Never empty for an elected contact.</summary>
        public string Because { get; }

        public override string ToString() => Actor + " " + Kind + " " + Weight;
    }

    /// <summary>The faces one settlement's first hours elected, and nothing else.</summary>
    public sealed class EarlyContactCast
    {
        private static readonly EarlyContact[] Nobody = new EarlyContact[0];

        private readonly List<EarlyContact> _contacts;
        private readonly Dictionary<EntityId, EarlyContact> _byActor;

        internal EarlyContactCast(List<EarlyContact> contacts)
        {
            _contacts = contacts ?? new List<EarlyContact>();
            _byActor = new Dictionary<EntityId, EarlyContact>();
            for (int i = 0; i < _contacts.Count; i++)
            {
                _byActor[_contacts[i].Actor] = _contacts[i];
            }
        }

        /// <summary>Best ground first, then a stable order. Empty is an ordinary answer.</summary>
        public IReadOnlyList<EarlyContact> Contacts =>
            _contacts.Count == 0 ? (IReadOnlyList<EarlyContact>)Nobody : _contacts;

        public int Count => _contacts.Count;

        public EarlyContact Of(EntityId actor) =>
            !actor.IsNone && _byActor.TryGetValue(actor, out EarlyContact contact) ? contact : null;

        /// <summary>What recognising this person is worth, or nothing if they were not elected.</summary>
        public int WeightOf(EntityId actor) =>
            !actor.IsNone && _byActor.TryGetValue(actor, out EarlyContact contact) ? contact.Weight : 0;

        public bool Includes(EntityId actor) => !actor.IsNone && _byActor.ContainsKey(actor);
    }

    /// <summary>
    /// BQ-115. The handful of faces a save keeps bringing back, elected before it has a crisis.
    ///
    /// The problem this exists to fix is an ordering one. BQ-114 reads how well the player knows
    /// somebody from history that already exists, and in a save the mod has just attached to there
    /// is none: the ledger is empty, nobody lives on the player's land yet, and Elin's own affinity
    /// is zero for the whole town. So the first situation a fresh save produces casts strangers,
    /// and a threat to a stranger is an errand (`engagement §4`). The importance ladder made the
    /// same mistake from the other end - <see cref="NarrativeImportance.Recurring"/> was reachable
    /// only *after* something important had already happened to somebody, which is backwards for a
    /// first encounter and is exactly what `PM §19` warns against.
    ///
    /// So this elects, and elects only (`D028`). It manufactures no history: it records no meeting the
    /// player did not have, writes no event, adds no relationship, and puts nothing in the save
    /// beside the truth (`D022`). Election is a pure reading of what the settlement already holds,
    /// which is what makes the same faces come back on the next pass and on the next reload - the
    /// recurrence is the determinism, not a stored roster.
    ///
    /// What it does assert is narrower and honest: of the people already here, these are the ones
    /// worth telling stories about first. <see cref="Establish"/> says so on the ladder, and
    /// casting reads the cast the same way it reads familiarity - after eligibility, never before,
    /// so a settlement that would have stayed quiet stays quiet however many faces were elected in
    /// it (`D027`).
    /// </summary>
    public static class EarlyContacts
    {
        /// <summary>
        /// How many faces a save elects. A handful, deliberately: the point is that the player
        /// starts recognising *these* people, which stops being true the moment the list is long
        /// enough to cover the town.
        /// </summary>
        public const int Handful = 3;

        /// <summary>Lives on the player's own land, which is the strongest tie a fresh save has.</summary>
        public const int NeighbourWeight = 14;

        /// <summary>Sells to strangers here, so the player will end up in front of them anyway.</summary>
        public const int ShopkeeperWeight = 11;

        /// <summary>Already crossed the player's path once, or is simply a local with a life.</summary>
        public const int RegularWeight = 8;

        private static readonly EarlyContactCast Nobody = new EarlyContactCast(new List<EarlyContact>());

        /// <summary>
        /// Reads the settlement and names the faces it would keep bringing back. Writes nothing.
        ///
        /// Ordered by ground and then by a fork of the world seed over the actor, so the answer
        /// survives a reload and is not an artefact of the order the game enumerated a zone in.
        /// </summary>
        public static EarlyContactCast Elect(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            if (world == null || vanilla == null || vanilla.PlayerId.IsNone)
            {
                return Nobody;
            }

            List<Candidate> candidates = new List<Candidate>();
            AddLocals(world, vanilla, zoneId, candidates);
            candidates.Sort(Compare);

            List<EarlyContact> elected = new List<EarlyContact>();
            for (int i = 0; i < candidates.Count && elected.Count < Handful; i++)
            {
                elected.Add(candidates[i].Contact);
            }

            return elected.Count == 0 ? Nobody : new EarlyContactCast(elected);
        }

        /// <summary>
        /// The first-hours pass: elect, and say so on the importance ladder.
        ///
        /// Idempotent, because <see cref="NarrativeNpc.Promote"/> is monotone and election is a
        /// pure reading - running it twice on the same settlement changes nothing, which is what
        /// lets the plugin call it on every attach without a reload becoming a re-roll.
        ///
        /// Promotion is the whole of the write. It is a statement about the mod's own attention,
        /// not about the player's history, so it cannot lie about anything the player did.
        /// </summary>
        public static EarlyContactCast Establish(NarrativeWorldState world, IVanillaState vanilla, EntityId zoneId)
        {
            EarlyContactCast cast = Elect(world, vanilla, zoneId);
            for (int i = 0; i < cast.Contacts.Count; i++)
            {
                world.Registry.GetNpc(cast.Contacts[i].Actor)?.Promote(NarrativeImportance.Recurring);
            }

            return cast;
        }

        /// <summary>Inspector-only. Why this save expects to keep seeing these people.</summary>
        public static string Describe(EarlyContactCast cast)
        {
            if (cast == null || cast.Count == 0)
            {
                return "early contacts: nobody here to keep bringing back";
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < cast.Contacts.Count; i++)
            {
                lines.Add("  " + cast.Contacts[i].Because);
            }

            return "early contacts (" + cast.Count + "):\n" + string.Join("\n", lines);
        }

        /// <summary>
        /// Everybody the settlement itself is holding, sorted into the ground that fits them.
        ///
        /// Only people who are actually here. A face is elected so that casting in *this* place has
        /// somebody the player will recognise, and a resident three zones away cannot be that -
        /// they are already the strongest reading BQ-114 has (`PlayerFamiliarity.HouseholdWeight`),
        /// so electing them as well would spend a slot to say something the other ground says
        /// louder. Standing here and living on the player's land is a neighbour; that is the
        /// overlap worth keeping.
        /// </summary>
        private static void AddLocals(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId zoneId,
            List<Candidate> candidates)
        {
            if (zoneId.IsNone)
            {
                return;
            }

            HashSet<EntityId> household = Household(vanilla);
            HashSet<EntityId> crossed = AlreadyCrossed(world, vanilla.PlayerId);
            HashSet<EntityId> seen = new HashSet<EntityId>();

            IReadOnlyList<EntityId> present = vanilla.GetCharactersInZone(zoneId);
            for (int i = 0; i < present.Count; i++)
            {
                EntityId local = present[i];
                if (!Eligible(world, vanilla, local, seen))
                {
                    continue;
                }

                seen.Add(local);
                NarrativeNpc npc = world.Registry.GetNpc(local);

                if (household.Contains(local))
                {
                    candidates.Add(Build(
                        world,
                        local,
                        EarlyContactKind.Neighbour,
                        NeighbourWeight,
                        "lives on the player's own land"));
                    continue;
                }

                if (World.IdentityAffordances.Of(npc, vanilla).Service.IsProvider)
                {
                    candidates.Add(Build(
                        world,
                        local,
                        EarlyContactKind.Shopkeeper,
                        ShopkeeperWeight,
                        "sells to strangers here, so the player will end up in front of them"));
                    continue;
                }

                candidates.Add(Build(
                    world,
                    local,
                    EarlyContactKind.Regular,
                    RegularWeight,
                    crossed.Contains(local)
                        ? "has already crossed the player's path here"
                        : "is a local the player will keep walking past"));
            }
        }

        /// <summary>Who the game says lives on the player's land, or nobody if it will not say.</summary>
        private static HashSet<EntityId> Household(IVanillaState vanilla)
        {
            HashSet<EntityId> household = new HashSet<EntityId>();
            HomeState home = vanilla.GetHomeState();
            if (home == null)
            {
                return household;
            }

            for (int i = 0; i < home.Residents.Count; i++)
            {
                household.Add(home.Residents[i].Id);
            }

            return household;
        }

        /// <summary>
        /// Anybody the ledger already shows the player and this person both party to.
        ///
        /// Only used to word a Regular's ground and to break ties among them - it is deliberately
        /// not a fourth rung, because BQ-114 already reads real history and reading it twice would
        /// let one encounter count once as history and once as a casting decision.
        /// </summary>
        private static HashSet<EntityId> AlreadyCrossed(NarrativeWorldState world, EntityId player)
        {
            HashSet<EntityId> crossed = new HashSet<EntityId>();
            foreach (WorldEvent past in world.Ledger.Involving(player))
            {
                EntityId other = past.Actor == player ? past.Target : past.Actor;
                if (!other.IsNone && other != player)
                {
                    crossed.Add(other);
                }
            }

            return crossed;
        }

        /// <summary>
        /// Somebody the mod may cast at all: alive, known to the registry, an ordinary person the
        /// mutation policy lets it reach into, and able to hold up a social role. The player is
        /// never their own contact.
        /// </summary>
        private static bool Eligible(
            NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId actor,
            HashSet<EntityId> seen)
        {
            if (actor.IsNone || actor == vanilla.PlayerId || seen.Contains(actor))
            {
                return false;
            }

            if (!vanilla.IsAlive(actor) || world.Registry.GetNpc(actor) == null)
            {
                return false;
            }

            return vanilla.GetActorClass(actor) != NarrativeActorClass.Unknown
                   && vanilla.GetActorKind(actor) == NarrativeActorKind.Person
                   && vanilla.GetSocialAgency(actor) == SocialAgency.Full;
        }

        private static Candidate Build(
            NarrativeWorldState world,
            EntityId actor,
            EarlyContactKind kind,
            int weight,
            string ground)
        {
            string because = world.Registry.NameOf(actor) + " is a face this save keeps: " + ground;
            ulong tieBreak = new DeterministicRng(world.WorldSeed)
                .Fork("early_contact|" + actor.Value)
                .NextUInt64();

            return new Candidate(new EarlyContact(actor, kind, weight, because), tieBreak);
        }

        private static int Compare(Candidate left, Candidate right)
        {
            int byWeight = right.Contact.Weight.CompareTo(left.Contact.Weight);
            if (byWeight != 0)
            {
                return byWeight;
            }

            return left.TieBreak.CompareTo(right.TieBreak);
        }

        private readonly struct Candidate
        {
            public Candidate(EarlyContact contact, ulong tieBreak)
            {
                Contact = contact;
                TieBreak = tieBreak;
            }

            public EarlyContact Contact { get; }

            public ulong TieBreak { get; }
        }
    }
}
