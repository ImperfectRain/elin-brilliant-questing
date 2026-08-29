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
        Handicraft
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
        /// Somebody can be moved into the player's Home as a resident.
        ///
        /// Separate from <see cref="ReadHomeState"/> for the same reason destruction is separate
        /// from transfer: reading a settlement and altering its roll are different reaches into
        /// the game, and a build that lists residents perfectly well may have no member this mod
        /// can call to add one. It covers residency and nothing else - Home Skill elements and
        /// resident jobs stay vanilla's to compute (see decision D018).
        /// </summary>
        WriteHomeResidents,
        ObserveCrimeWitnesses
    }
}
