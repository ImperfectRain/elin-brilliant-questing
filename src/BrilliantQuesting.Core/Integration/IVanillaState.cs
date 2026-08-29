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

        // -- world ------------------------------------------------------------------------
        EntityId GetZoneOf(EntityId entity);

        IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId);
    }
}
