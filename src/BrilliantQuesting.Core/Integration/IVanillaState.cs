using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// The single seam between the simulation and Elin.
    ///
    /// Nothing above this interface may reference Elin.dll, BepInEx or Unity. When the game
    /// changes shape between Early Access builds, exactly one implementation has to be repaired
    /// and the world model, action library and tests are untouched. The headless
    /// <see cref="SandboxVanillaState"/> implements the same contract, which is what lets the
    /// three-NPC laboratory run in unit tests with no game process at all.
    /// </summary>
    public interface IVanillaState
    {
        GameTime Now { get; }

        EntityId PlayerId { get; }

        bool Supports(VanillaCapability capability);

        // -- characters -------------------------------------------------------------------
        bool IsAlive(EntityId chara);

        int GetAttribute(EntityId chara, VanillaAttribute attribute);

        int GetSkill(EntityId chara, VanillaSkill skill);

        int GetLevel(EntityId chara);

        /// <summary>Vanilla affinity of <paramref name="chara"/> toward the player.</summary>
        int GetAffinity(EntityId chara);

        void ChangeAffinity(EntityId chara, int delta);

        // -- player standing --------------------------------------------------------------
        int Karma { get; }

        void ChangeKarma(int delta);

        int Fame { get; }

        void ChangeFame(int delta);

        int GetInfluence(EntityId townId);

        void ChangeInfluence(EntityId townId, int delta);

        bool IsGuildMember(GuildId guild);

        int GetGuildRank(GuildId guild);

        string GetWorshippedDeity(EntityId chara);

        int GetPiety(EntityId chara);

        // -- money and things -------------------------------------------------------------
        int GetMoney(EntityId owner);

        bool TrySpendMoney(EntityId payer, EntityId payee, int amount);

        IReadOnlyList<ItemDescriptor> GetInventory(EntityId owner);

        bool TryTransferItem(EntityId itemId, EntityId from, EntityId to);

        /// <summary>
        /// Takes an object out of the world permanently, and reports whether it actually went.
        ///
        /// Burning a ledger, melting a ring, feeding a note to a fire. The caller names the holder
        /// so the adapter looks in one inventory rather than searching the map for anything with
        /// that id - and so a request to destroy something the holder is not carrying fails
        /// instead of quietly reaching across the world for it.
        /// </summary>
        bool TryDestroyItem(EntityId itemId, EntityId holder);

        // -- home -------------------------------------------------------------------------

        /// <summary>
        /// The player's Home as the game currently has it, or null when there is no Home or this
        /// build cannot read one.
        ///
        /// Null is the honest answer for "no Home", and an empty <see cref="HomeState"/> is never
        /// used to stand in for it: a settlement with nobody in it and a player who owns no land
        /// are different situations, and the Home verbs refuse or allow on exactly that
        /// difference. A snapshot, not a handle: the only thing above this seam that changes a
        /// Home is <see cref="TryAdmitResident"/>, and a caller that has admitted somebody asks
        /// again rather than assuming what the game did with them.
        ///
        /// This snapshot, not <see cref="VanillaCapability.ReadHomeState"/>, is what a caller acts
        /// on: the capability says what a probe found when the game was attached, and a Home can be
        /// acquired, emptied or lost long after that.
        /// </summary>
        HomeState GetHomeState();

        /// <summary>
        /// Moves somebody into the player's Home as a resident, and reports whether they actually
        /// went. False for a build that cannot write residency, for a Home with no room, and for
        /// anybody the game refused - never a claim that has to be taken on trust.
        ///
        /// This is the only write the Home has. A resident's job and the six Home Skill elements
        /// are vanilla's own arithmetic over who lives there and what they do, and the mod reads
        /// them rather than setting them: writing Public Safety directly would be a second
        /// settlement economy disagreeing with the one the player watches (decision D018).
        /// </summary>
        bool TryAdmitResident(EntityId chara);

        // -- world ------------------------------------------------------------------------
        EntityId GetZoneOf(EntityId entity);

        IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId);
    }
}
