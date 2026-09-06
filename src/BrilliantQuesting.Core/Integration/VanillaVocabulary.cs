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
        /// The game will say what an actor is doing right now: the timetable and its current span,
        /// the goal vanilla has them at, and whether the off-screen mechanism is carrying them
        /// somewhere.
        ///
        /// Separate from <see cref="ReadCharacterIdentity"/> because the two are different reads
        /// of different things with different lifetimes - identity comes off the source sheets and
        /// trait subclasses, activity off live `Chara` members and the hourly global mechanism -
        /// and a build can lose either alone. It earns its place rather than taking it for
        /// symmetry: none of the members behind it has been watched work on a running game
        /// (`ELIN-Q-0014`, `API-048`), so the live adapter's own probe is what decides, and a build
        /// that answers no facet for the player reports this unsupported.
        ///
        /// Unsupported means every facet is unknown for everybody, which closes nothing and opens
        /// nothing: activity is a plausibility input, never a gate on presence, testimony or
        /// safety. What it must never become is a licence - a readable timetable is not a reason
        /// to set one, and a readable global goal is not a reason to write one (`D019`, `D021`).
        /// </summary>
        ReadActorActivity,

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
        ReadPlayerCompanions,

        /// <summary>
        /// The game will make a place with a physical shape: a zone this mod owns, with authored
        /// map pieces applied into it.
        ///
        /// Separate from every read above because it is the one write that creates terrain, and
        /// separate from staging a character into a loaded zone because that binds to a place the
        /// game already made. `Region.CreateRandomSite`, `addMap` for a predeclared mod zone,
        /// `GenBounds.TryAddMapPiece`, `PartialMap.Apply` and whether a created site's map survives
        /// a save are all unanswered on the installed build (`ELIN-Q-0032`), so an adapter that has
        /// not exercised them reports this unsupported and a scenario dungeon is then impossible
        /// rather than half-built: <see cref="BrilliantQuesting.World.SiteRealization"/> refuses to
        /// plan one, and <see cref="ISituationStager.StageSite"/> refuses a blueprint that carries
        /// a structure, on which genesis already fails closed.
        /// </summary>
        BuildPlaceStructure,

        /// <summary>
        /// The game will add one authored piece into a place this mod already made, after that
        /// place has been visited, and will say whether a patch of ground in it is free.
        ///
        /// Separate from <see cref="BuildPlaceStructure"/> because it is a different write on a
        /// different map: building makes terrain nobody has stood in, and this changes terrain a
        /// player may have walked, dug, built on and left things standing in. A build that can do
        /// the first has not thereby shown it can do the second, and the read is inseparable from
        /// the write - adding a piece without being able to ask what is already there is how a
        /// mod overwrites somebody's workshop.
        ///
        /// Unanswered on the installed build (`ELIN-Q-0033`), so an adapter that has not exercised
        /// it reports this unsupported, <see cref="BrilliantQuesting.World.SiteMutation"/> refuses
        /// before anything is chosen, and <see cref="IVanillaState.InspectGround"/> answers
        /// <see cref="VanillaGround.Unknown"/> - which is itself a refusal, because ground nobody
        /// can read is never treated as free.
        /// </summary>
        AddPlaceFixture
    }

    /// <summary>
    /// What the game says is standing on a patch of a place's ground (BQ-143).
    ///
    /// Four answers rather than a bool, because the three ways ground can be unusable are not the
    /// same fact and a mutation that conflated them would refuse for the wrong reason. Only
    /// <see cref="Free"/> permits a write; every other value refuses, <see cref="Unknown"/>
    /// included (`D017`) - an unread patch of ground is not an empty one, and the failure
    /// direction has to be an addition that does not happen rather than an addition on top of
    /// somebody's cellar.
    /// </summary>
    public enum VanillaGround
    {
        /// <summary>The build was not asked, could not answer, or does not know. Never "free".</summary>
        Unknown,

        /// <summary>Nothing stands there and nobody has changed it.</summary>
        Free,

        /// <summary>Something the place was made with stands there.</summary>
        Occupied,

        /// <summary>The player changed this ground: built on it, dug it, or left something in it.</summary>
        PlayerChanged
    }
}
