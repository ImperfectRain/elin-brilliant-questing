namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// Elin's eight primary attributes. The mod never invents a ninth: if a procedural action
    /// needs a capability, it must be expressible in these plus the skill list below.
    /// </summary>
    public enum VanillaAttribute
    {
        Strength,
        Endurance,
        Dexterity,
        Perception,
        Learning,
        Will,
        Magic,
        Charisma
    }

    /// <summary>
    /// The vanilla skills the procedural layer currently reads. Deliberately a short list -
    /// every entry has to earn its place by being the mechanical spine of at least one action
    /// in the action library.
    /// </summary>
    public enum VanillaSkill
    {
        Negotiation,
        Investing,
        Pickpocket,
        Stealth,
        Lockpicking,
        DisarmTrap,
        SpotHidden,
        Literacy,
        Appraising,
        Anatomy,
        Alchemy,
        Cooking,
        Faith,
        Travel,
        Mining,

        /// <summary>Working wood. The spine of putting a broken thing back into service.</summary>
        Carpentry,

        /// <summary>Elin's own construction skill, and the spine of raising something new.</summary>
        Building,

        /// <summary>
        /// Elin's generic making skill, and the spine of the verb that makes to a specification
        /// nobody's named craft covers.
        /// </summary>
        Handicraft,

        /// <summary>Public performance as Elin levels it, not a private dialogue stat.</summary>
        Music,

        /// <summary>Bringing food out of water, as a route for players who fish.</summary>
        Fishing,

        /// <summary>Growing useful supplies, as a route for players who farm.</summary>
        Farming
    }

    public enum GuildId
    {
        None,
        Fighters,
        Mages,
        Thieves,
        Merchants
    }

    /// <summary>
    /// Elin is in active Early Access, so some integrations may not exist (or may not be safe)
    /// on a given build. Adapters advertise what they can actually do and the action layer asks
    /// before relying on it, rather than failing halfway through a resolution.
    /// </summary>
    public enum VanillaCapability
    {
        ReadAttributes,
        ReadSkills,
        ReadWriteAffinity,
        ReadWriteKarma,
        ReadWriteFame,
        ReadWriteInfluence,
        ReadGuildRank,
        ReadFaith,
        ReadInventory,

        /// <summary>
        /// The game will say what is standing loose in a place, as opposed to what somebody is
        /// carrying.
        ///
        /// Separate from <see cref="ReadInventory"/> because the two are different reads and this
        /// build answers only one of them: `GetInventory` resolves a character, so a thing on a
        /// floor is invisible to the live adapter even though the headless reference returns it
        /// (`ELIN-Q-0008`). A route that has to find the rockfall blocking a mine leans on this
        /// one, and unsupported means that route is not promised rather than promised and then
        /// silently empty.
        /// </summary>
        ReadPlaceContents,
        TransferItems,

        /// <summary>
        /// Objects can be taken out of the world for good.
        ///
        /// Separate from <see cref="TransferItems"/> because destruction is the irreversible one:
        /// a build where moving a thing works but unmaking it does not is perfectly ordinary, and
        /// a burned ledger that quietly stayed in somebody's pack would leave the simulation
        /// believing evidence was gone while the game still had it.
        /// </summary>
        DestroyItems,
        SpendMoney,
        ReadHomeState,

        /// <summary>
        /// The game will say who a character is: the `SourceChara` kind, race, job, hobbies, the
        /// service traits and the institutional markers.
        ///
        /// Separate from every other read because it is answered by a different part of the game
        /// - the source sheets and trait subclasses rather than a live Chara member - and a build
        /// that stops exposing them loses identity and nothing else. Unsupported means every facet
        /// is unknown for everybody, which closes nothing: identity grants affordances and never
        /// gates presence, testimony or safety.
        /// </summary>
        ReadCharacterIdentity,

        /// <summary>
        /// Somebody can be moved into the player's Home as a resident.
        ///
        /// Separate from <see cref="ReadHomeState"/> for the same reason destruction is separate
        /// from transfer: reading a settlement and altering its roll are different reaches into
        /// the game, and a build that lists residents perfectly well may have no member this mod
        /// can call to add one. It covers residency and nothing else - Home Skill elements and
        /// resident jobs stay vanilla's to compute (see decision D018).
        /// </summary>
        WriteHomeResidents,

        /// <summary>
        /// A character can be moved from one zone to another and left there.
        ///
        /// The whole of what Grade B absence needs from the game, and deliberately the whole of
        /// it: this mod does not have, and must not acquire, a way to take a Chara out of the
        /// world. Somebody who is away is somewhere else, which is a thing Elin's own travelling
        /// NPCs already are - so the save keeps one character, in one place, and the worst a bug
        /// can do is leave a villager in the wrong town.
        ///
        /// Separate from every other capability because it is the one write in this contract that
        /// alters where a save keeps a person, and the roadmap requires it to stay off until it
        /// has survived an adversarial test on a real save. An adapter that has not been through
        /// that reports it unsupported, and Grade B is then impossible rather than unreliable.
        /// </summary>
        MoveCharaBetweenZones,
        ObserveCrimeWitnesses,

        /// <summary>
        /// The game will list who keeps the player company: the party they travel with, and the
        /// pets in it.
        ///
        /// Separate from <see cref="ReadHomeState"/> because the two answer different halves of
        /// the same question and a build can lose either on its own - the Home roll is read off
        /// the settlement branch, the party off the player. Unsupported means the household is
        /// whatever the Home roll says and nothing else, which is a narrower answer rather than a
        /// wrong one: it never claims somebody has no companions, and
        /// <see cref="BrilliantQuesting.Relationships.PlayerHousehold.CompanionsRead"/> is what
        /// says which of the two it is.
        /// </summary>
        ReadPlayerCompanions
    }
}
