using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;

namespace BrilliantQuesting.Relationships
{
    /// <summary>
    /// How somebody belongs to the player's household. Not what they are - a chicken, a hired
    /// adventurer and a rescued sister can each hold any of these, and which of the three they are
    /// is <see cref="IVanillaState.GetCharacterIdentity"/>'s answer and never this one's.
    /// </summary>
    public enum HouseholdBond
    {
        /// <summary>Not of this household. The answer for most of the world, permanently.</summary>
        None = 0,

        /// <summary>On the player's Home roll: they live on the player's own land.</summary>
        Resident = 1,

        /// <summary>In the player's party: they go where the player goes.</summary>
        Companion = 2
    }

    /// <summary>One actor of the player's household, and what makes them one.</summary>
    public sealed class HouseholdMember
    {
        internal HouseholdMember(EntityId actor, HouseholdBond bond, string name, string because)
        {
            Actor = actor;
            Bond = bond;
            Name = name ?? string.Empty;
            Because = because ?? string.Empty;
        }

        public EntityId Actor { get; }

        /// <summary>
        /// The stronger of the ties the game reports, where it reports both.
        /// <see cref="HouseholdBond.Resident"/> wins over <see cref="HouseholdBond.Companion"/>:
        /// a pet that both lives on the player's land and walks beside them keeps belonging there
        /// when it is left at home, and the party it is in this hour does not.
        /// </summary>
        public HouseholdBond Bond { get; }

        /// <summary>Whatever the game or the world model calls them. Never an identity.</summary>
        public string Name { get; }

        /// <summary>Inspector-only sentence naming the tie.</summary>
        public string Because { get; }

        public override string ToString() => Name + " [" + Actor + ", " + Bond + "]";
    }

    /// <summary>
    /// BQ-123. Whose household this is, read from the game and never written down.
    ///
    /// The player's own pets, residents and companions are the one part of the cast that Elin has
    /// already made the player care about, so casting is allowed to draw on them. This is the one
    /// place that says who they are, for the same reason <see cref="PlayerFamiliarity"/> is the one
    /// place that says who the player knows: two surfaces deciding privately what counts as "the
    /// player's own" is how a settlement ends up with two disagreeing ideas of a household.
    ///
    /// Two grounds, both of them the game's:
    ///
    /// - **The Home roll** (<see cref="IVanillaState.GetHomeState"/>). Somebody living on the
    ///   player's land, whether or not they are standing here.
    /// - **The party** (<see cref="IVanillaState.GetPlayerCompanions"/>). Somebody the player
    ///   travels with, whether or not they live anywhere.
    ///
    /// **Nothing here is stored, and that is the whole of the lifecycle.** A pet that is sold, a
    /// resident married off into another household, a companion dismissed, a character the build
    /// can no longer resolve at all - none of them needs an event, a hook or a cleanup pass,
    /// because membership was never a fact this mod held. It is asked again, and the answer is
    /// simply different. The ids they were cast under stay in the registry and in the firings that
    /// name them, so a scene the player has already played keeps reading correctly (`D004`).
    ///
    /// **Death and disappearance are the same answer.** A member is somebody the game says is
    /// alive now; <see cref="VanillaLifeState.Dead"/> and <see cref="VanillaLifeState.Unknown"/>
    /// both drop out, the second because an actor the adapter cannot resolve is one this mod must
    /// not go on describing as part of the player's home.
    ///
    /// **A ground that could not be read contributes nothing rather than nobody** (`D017`).
    /// <see cref="ResidentsRead"/> and <see cref="CompanionsRead"/> say which of the two answered,
    /// so a player with no companions and a build that cannot see a party are distinguishable -
    /// they are the same empty list and they are not the same fact.
    ///
    /// Deliberately not a gate and deliberately not an identity. Belonging to the household admits
    /// somebody to roles that need a subject rather than a speaker; whether they can testify stays
    /// <see cref="SocialAgency"/>'s answer, how far the mod may reach into them stays
    /// <see cref="NarrativeActorClass"/>'s, and what the game says they are stays
    /// <see cref="CharacterIdentity"/>'s.
    /// </summary>
    public sealed class PlayerHousehold
    {
        private static readonly HouseholdMember[] NoMembers = new HouseholdMember[0];

        private readonly List<HouseholdMember> _members = new List<HouseholdMember>();
        private readonly Dictionary<EntityId, HouseholdMember> _byActor =
            new Dictionary<EntityId, HouseholdMember>();

        private PlayerHousehold()
        {
        }

        /// <summary>An empty household nobody asked the game about. Never a claim.</summary>
        public static PlayerHousehold Unread => new PlayerHousehold();

        /// <summary>
        /// Reads both grounds once and answers about anybody afterwards.
        ///
        /// Held for one pass - one generation run, one casting decision, one scene - and asked
        /// again after a zone change, a save or a load, exactly as the Home snapshot and the
        /// identity observation it is built from are.
        /// </summary>
        public static PlayerHousehold Read(World.NarrativeWorldState world, IVanillaState vanilla)
        {
            PlayerHousehold household = new PlayerHousehold();
            if (vanilla == null)
            {
                return household;
            }

            HomeState home = vanilla.GetHomeState();
            household.ResidentsRead = home != null;
            household.CompanionsRead = vanilla.Supports(VanillaCapability.ReadPlayerCompanions);

            if (home != null)
            {
                for (int i = 0; i < home.Residents.Count; i++)
                {
                    HomeResident resident = home.Residents[i];
                    if (resident == null)
                    {
                        continue;
                    }

                    household.Admit(
                        world,
                        vanilla,
                        resident.Id,
                        HouseholdBond.Resident,
                        resident.Name,
                        "lives on the player's own land"
                        + (resident.HasJob ? " as the household's " + resident.Job : string.Empty));
                }
            }

            IReadOnlyList<EntityId> companions = vanilla.GetPlayerCompanions();
            if (companions != null)
            {
                for (int i = 0; i < companions.Count; i++)
                {
                    household.Admit(
                        world,
                        vanilla,
                        companions[i],
                        HouseholdBond.Companion,
                        null,
                        "keeps the player company");
                }
            }

            return household;
        }

        /// <summary>Residents first in the order the Home listed them, then the party.</summary>
        public IReadOnlyList<HouseholdMember> Members => _members.Count == 0 ? NoMembers : _members;

        public int Count => _members.Count;

        /// <summary>Whether the Home roll answered at all. False for a player with no Home.</summary>
        public bool ResidentsRead { get; private set; }

        /// <summary>Whether the party answered at all. See <see cref="ResidentsRead"/>.</summary>
        public bool CompanionsRead { get; private set; }

        /// <summary>
        /// True when this household is empty only because nothing was readable. An empty household
        /// that *was* read is a player who lives alone, which is a fact; this one is a silence.
        /// </summary>
        public bool IsUnread => !ResidentsRead && !CompanionsRead;

        /// <summary>How this actor belongs, or <see cref="HouseholdBond.None"/>.</summary>
        public HouseholdBond BondOf(EntityId actor)
        {
            return !actor.IsNone && _byActor.TryGetValue(actor, out HouseholdMember member)
                ? member.Bond
                : HouseholdBond.None;
        }

        public bool Includes(EntityId actor) => BondOf(actor) != HouseholdBond.None;

        /// <summary>This actor's membership, or null for anybody else.</summary>
        public HouseholdMember Find(EntityId actor)
        {
            return !actor.IsNone && _byActor.TryGetValue(actor, out HouseholdMember member) ? member : null;
        }

        /// <summary>
        /// One line, written so a live log distinguishes a household of nobody from a build that
        /// could not see one.
        /// </summary>
        public string Describe()
        {
            if (IsUnread)
            {
                return "household unread (no Home, and no party this build can list)";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < _members.Count; i++)
            {
                parts.Add(_members[i].ToString());
            }

            string body = parts.Count == 0 ? "nobody" : string.Join(", ", parts.ToArray());
            return body
                   + " (residents " + (ResidentsRead ? "read" : "?")
                   + ", companions " + (CompanionsRead ? "read" : "?") + ")";
        }

        public override string ToString() => Describe();

        /// <summary>
        /// Takes one candidate on to the roll, or refuses them for a reason worth having.
        ///
        /// The player is never of their own household - a scene may be with them and the caller
        /// says so, exactly as casting does. Anybody the game does not currently say is alive is
        /// refused, which is the whole of "sold, removed, killed": the game stops answering for
        /// them, or answers that they are dead, and either way they stop being household without
        /// anything here having to be told.
        /// </summary>
        private void Admit(
            World.NarrativeWorldState world,
            IVanillaState vanilla,
            EntityId actor,
            HouseholdBond bond,
            string name,
            string because)
        {
            if (actor.IsNone || actor == vanilla.PlayerId)
            {
                return;
            }

            if (vanilla.GetLifeState(actor) != VanillaLifeState.Alive)
            {
                return;
            }

            if (_byActor.TryGetValue(actor, out HouseholdMember existing))
            {
                // Both ties at once - the pet that lives here and walks beside the player. The
                // stronger one stands, and the sentence says both, so an inspector is not left
                // wondering why somebody in the party reads as a resident.
                Replace(existing, new HouseholdMember(
                    actor,
                    Stronger(existing.Bond, bond),
                    existing.Name,
                    existing.Because + "; " + because));
                return;
            }

            HouseholdMember member = new HouseholdMember(
                actor,
                bond,
                NameOf(world, actor, name),
                because);
            _members.Add(member);
            _byActor[actor] = member;
        }

        /// <summary>
        /// Residency outranks the party. Both are real ties; only one of them survives the player
        /// leaving somebody at home for a season.
        /// </summary>
        private static HouseholdBond Stronger(HouseholdBond left, HouseholdBond right)
        {
            if (left == HouseholdBond.Resident || right == HouseholdBond.Resident)
            {
                return HouseholdBond.Resident;
            }

            return left == HouseholdBond.None ? right : left;
        }

        private void Replace(HouseholdMember existing, HouseholdMember replacement)
        {
            int index = _members.IndexOf(existing);
            if (index >= 0)
            {
                _members[index] = replacement;
            }

            _byActor[replacement.Actor] = replacement;
        }

        /// <summary>
        /// What to call them. The Home roll carries the game's own name; the party does not, so the
        /// world model is asked, and the id is the last resort a trace can always print.
        /// </summary>
        private static string NameOf(World.NarrativeWorldState world, EntityId actor, string given)
        {
            if (!string.IsNullOrEmpty(given))
            {
                return given;
            }

            return world == null ? actor.ToString() : world.Registry.NameOf(actor);
        }
    }
}
