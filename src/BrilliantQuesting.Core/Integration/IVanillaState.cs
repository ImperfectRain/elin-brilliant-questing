using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    public enum VanillaLifeState
    {
        Unknown,
        Alive,
        Dead
    }

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

        /// <summary>
        /// What kind of actor this is, so the mod knows how far it may reach into them.
        ///
        /// A read, and the only input the mutation policy has. The game is the authority: only it
        /// knows whether a Chara is somebody a vanilla quest line depends on. A build that cannot
        /// tell answers <see cref="NarrativeActorClass.Unknown"/>, which keeps the reversible
        /// reaches and closes the irreversible ones - never a guess in either direction.
        /// </summary>
        NarrativeActorClass GetActorClass(EntityId chara);

        /// <summary>
        /// Broad narrative kind for role casting. Unknown is a real answer, not a fallback to
        /// personhood.
        /// </summary>
        NarrativeActorKind GetActorKind(EntityId chara);

        /// <summary>How much ordinary testimony/commerce/deception agency this actor presents.</summary>
        SocialAgency GetSocialAgency(EntityId chara);

        /// <summary>
        /// Who the game says this character is: character archetype, race, work, hobby, service
        /// role and institutional standing, each carrying Elin's own id and each unknown on its
        /// own terms.
        ///
        /// Always an observation, never null - an actor this build cannot resolve is somebody
        /// every facet is unknown about, which is a true statement and a usable one. It says
        /// nothing about what any facet means, grants nothing, and is emphatically not an input to
        /// the mutation policy: how far the mod may reach into somebody is
        /// <see cref="GetActorClass"/>'s answer and only ever will be. A costume is not a
        /// permission.
        ///
        /// A live read, like <see cref="GetHomeState"/> and for the same reason: nothing here is
        /// persisted, so a save cannot carry a stale claim about who somebody is. Hold it for one
        /// pass and ask again after a zone change, a save or a load.
        /// </summary>
        CharacterIdentity GetCharacterIdentity(EntityId chara);

        // -- characters -------------------------------------------------------------------

        /// <summary>
        /// The game's current life answer for a character. Unknown means the adapter cannot
        /// resolve the actor now, not that the actor is dead.
        /// </summary>
        VanillaLifeState GetLifeState(EntityId chara);

        bool IsAlive(EntityId chara);

        int GetAttribute(EntityId chara, VanillaAttribute attribute);

        int GetSkill(EntityId chara, VanillaSkill skill);

        int GetLevel(EntityId chara);

        /// <summary>Vanilla affinity of <paramref name="chara"/> toward the player.</summary>
        int GetAffinity(EntityId chara);

        [VanillaMutation(MutationKind.Social, "chara")]
        void ChangeAffinity(EntityId chara, int delta);

        // -- player standing --------------------------------------------------------------
        int Karma { get; }

        [VanillaMutation(MutationKind.Social)]
        void ChangeKarma(int delta);

        int Fame { get; }

        [VanillaMutation(MutationKind.Social)]
        void ChangeFame(int delta);

        int GetInfluence(EntityId townId);

        [VanillaMutation(MutationKind.Social)]
        void ChangeInfluence(EntityId townId, int delta);

        bool IsGuildMember(GuildId guild);

        int GetGuildRank(GuildId guild);

        /// <summary>
        /// What this member has put into that guild, as the game's own per-guild progression.
        ///
        /// Rank says what the player is inside the guild; this says how much of it they earned
        /// rather than how long ago. Kept separate from <see cref="GetGuildRank"/> rather than
        /// folded into it because the two answer different questions and vanilla keeps them apart
        /// as well, and separate from the player-wide `contribution` currency, which is one number
        /// for all four guilds and so cannot say which of them is owed anything.
        ///
        /// Zero for a guild the player is not in, and zero on a build that cannot read it. Zero is
        /// "nothing put in" rather than "unknown", so a caller may let it lower a difficulty and
        /// must not let it open a route: an unread number that decided availability would be
        /// exactly the guess decision D017 refuses.
        /// </summary>
        int GetGuildContribution(GuildId guild);

        string GetWorshippedDeity(EntityId chara);

        int GetPiety(EntityId chara);

        // -- money and things -------------------------------------------------------------
        int GetMoney(EntityId owner);

        [VanillaMutation(MutationKind.Inventory, "payer", "payee")]
        bool TrySpendMoney(EntityId payer, EntityId payee, int amount);

        IReadOnlyList<ItemDescriptor> GetInventory(EntityId owner);

        [VanillaMutation(MutationKind.Inventory, "from", "to")]
        bool TryTransferItem(EntityId itemId, EntityId from, EntityId to);

        /// <summary>
        /// Takes an object out of the world permanently, and reports whether it actually went.
        ///
        /// Burning a ledger, melting a ring, feeding a note to a fire. The caller names the holder
        /// so the adapter looks in one inventory rather than searching the map for anything with
        /// that id - and so a request to destroy something the holder is not carrying fails
        /// instead of quietly reaching across the world for it.
        /// </summary>
        [VanillaMutation(MutationKind.Inventory, "holder")]
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
        [VanillaMutation(MutationKind.Relocate, "chara")]
        bool TryAdmitResident(EntityId chara);

        // -- whereabouts --------------------------------------------------------------------

        /// <summary>
        /// Moves a character to another zone and leaves them there, and reports whether they
        /// actually went.
        ///
        /// The whole of Grade B absence (LW 5.2), and deliberately expressed as travel rather than
        /// as removal. Elin already moves characters between zones, keeps them in the save while
        /// they are elsewhere, and hands back the same character when the player follows: reusing
        /// that means one person exists throughout, so a citizen refresh or a reloaded zone cannot
        /// produce a second copy of somebody the mod sent away. There is no member here that takes
        /// a character out of the world, and there must not be.
        ///
        /// Never called speculatively. <see cref="AbsenceLifecycle"/> is the only caller, it names
        /// a zone the game reported, and it records an absence only once this has returned true -
        /// so procedural state never claims a departure the game refused.
        /// </summary>
        [VanillaMutation(MutationKind.TemporaryAbsence, "chara")]
        bool TrySendAway(EntityId chara, EntityId zone);

        /// <summary>
        /// Puts a character back, and reports whether they are there afterwards.
        ///
        /// Mechanically the same move as <see cref="TrySendAway"/> and implemented by the same
        /// code; what differs is permission. This one is a <see cref="VanillaWithdrawalAttribute"/>
        /// and is never refused, because the alternative is somebody the mod moved being left
        /// where it moved them when their classification changes underneath the absence. Bringing
        /// a person home cannot be the call that a safety rule blocks.
        /// </summary>
        [VanillaWithdrawal]
        bool TryBringBack(EntityId chara, EntityId zone);

        // -- world ------------------------------------------------------------------------

        /// <summary>
        /// Where the game currently keeps this entity, or nobody when it cannot say.
        ///
        /// <see cref="EntityId.None"/> means "unknown", never "here". Reconciliation reads this to
        /// decide whether the game has quietly brought an absentee home, and an adapter that
        /// answered with the player's own zone for a character it could not resolve would report
        /// every unresolvable absentee as standing in front of the player.
        /// </summary>
        EntityId GetZoneOf(EntityId entity);

        IReadOnlyList<EntityId> GetCharactersInZone(EntityId zoneId);
    }
}
