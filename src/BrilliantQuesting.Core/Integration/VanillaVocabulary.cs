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
        Mining
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
        SpendMoney,
        ReadHomeState,
        ObserveCrimeWitnesses
    }
}
